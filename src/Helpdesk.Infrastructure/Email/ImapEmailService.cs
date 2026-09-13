using Azure.Core;
using Azure.Identity;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Enums;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Helpdesk.Application.Services.Email;

public class ImapEmailService : BackgroundService, IImapEmailService
{
    private static readonly TokenRequestContext OutlookTokenRequestContext = new(new[] { "https://outlook.office365.com/.default" });

    private bool _inboxSubscribed;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ImapEmailService> _logger;
    private ImapClient? _client;
    private ImapEmailSettings? _settings;
    private ClientSecretCredential? _credential;
    private AccessToken? _accessToken;

    private CancellationTokenSource? _idleTokenSource;
    private bool _isIdle;

    public ImapEmailService(IServiceScopeFactory scopeFactory, ILogger<ImapEmailService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<bool> TestConnectionAsync(Helpdesk.Shared.Models.ImapEmailSettings settings, CancellationToken token)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(settings);
            using var client = new ImapClient();
            await client.ConnectAsync(settings.Host, settings.Port, settings.UseSsl, token);
            var credential = new ClientSecretCredential(settings.TenantId, settings.ClientId, settings.ClientSecret);
            var result = await credential.GetTokenAsync(OutlookTokenRequestContext, token);
            var sasl = new SaslMechanismOAuth2(settings.UserEmail, result.Token);
            await client.AuthenticateAsync(sasl, token);
            await client.DisconnectAsync(true, token);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "IMAP connection failed");
            return false;
        }
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();
            var inboxSettings = (await LoadOrderedInboxSettingsAsync(db.EmailInboxSettings.AsNoTracking(), cancellationToken))
                .FirstOrDefault();

            _settings = inboxSettings is not null
                ? ToImapSettings(inboxSettings, requireBackgroundSync: true)
                : await db.ImapEmailSettings.FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "IMAP settings unavailable. Skipping IMAP service startup.");
            return;
        }

        await base.StartAsync(cancellationToken);
    }

    public static async Task<EmailInboxSettings[]> LoadOrderedInboxSettingsAsync(
        IQueryable<EmailInboxSettings> settings, CancellationToken cancellationToken)
    {
        // This is the small, instance-wide mailbox configuration set. Materialize
        // before ordering so DateTimeOffset chronology also works on SQLite.
        var configured = await settings.ToListAsync(cancellationToken);
        return OrderByCurrentInboxSettings(configured).ToArray();
    }

    public static IOrderedEnumerable<EmailInboxSettings> OrderByCurrentInboxSettings(IEnumerable<EmailInboxSettings> settings)
        => settings
            .OrderByDescending(x => x.Enabled && x.BackgroundSyncEnabled)
            .ThenByDescending(x => x.Enabled)
            .ThenByDescending(x => x.BackgroundSyncEnabled)
            .ThenByDescending(x => x.UpdatedAt)
            .ThenByDescending(x => x.CreatedAt)
            .ThenBy(x => x.Id);

    public static ImapEmailSettings ToImapSettings(EmailInboxSettings settings, bool requireBackgroundSync)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var enabled = settings.Enabled && (!requireBackgroundSync || settings.BackgroundSyncEnabled);
        return new ImapEmailSettings
        {
            Host = string.IsNullOrWhiteSpace(settings.MailHost) ? "outlook.office365.com" : settings.MailHost,
            Port = settings.Port,
            UseSsl = settings.UseSsl,
            Mailbox = string.IsNullOrWhiteSpace(settings.MailboxFolder) ? "INBOX" : settings.MailboxFolder,
            TenantId = settings.TenantId,
            ClientId = settings.ClientId,
            ClientSecret = settings.ClientSecret,
            UserEmail = settings.MailboxAddress,
            Enabled = enabled,
            LastTestStatus = enabled ? ImapTestStatus.Success : ImapTestStatus.Never
        };
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try { _idleTokenSource?.Cancel(); } catch { }
        await DisconnectAsync();
        await base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_settings is null || !_settings.Enabled || _settings.LastTestStatus != ImapTestStatus.Success)
        {
            _logger.LogInformation("IMAP service disabled or not configured correctly. Halting execution.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_client is not null &&
                    _client.IsConnected &&
                    _client.IsAuthenticated &&
                    _accessToken.HasValue &&
                    TokenExpiresWithin(TimeSpan.FromMinutes(5)))
                {
                    _logger.LogInformation("OAuth access token is near expiry. Forcing controlled IMAP reconnect.");
                    await DisconnectAsync();
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                    continue;
                }

                // Ensure connected
                if (_client is null || !_client.IsConnected || !_client.IsAuthenticated || !_client.Inbox.IsOpen)
                    await ConnectAndSubscribeAsync(stoppingToken);

                // Process any messages that arrived while disconnected (or since last cycle)
                await ProcessNewMessagesAsync(stoppingToken);

                // --------- START: CADENCED IDLE ---------
                _isIdle = true;
                _logger.LogInformation("Entering IMAP IDLE state.");

                // Token that lets the event handler break IDLE
                _idleTokenSource?.Dispose();
                _idleTokenSource = new CancellationTokenSource();

                // Periodically wake (O365 tends to drop long IDLEs). 9–10 min is a safe window.
                // Add a little jitter (10–40s) so multiple instances don’t all NOOP at once.
                var jitterSeconds = Random.Shared.Next(10, 40);
                using var idleTimeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(10).Add(TimeSpan.FromSeconds(jitterSeconds)));

                // Linked CTS: stops on app stop, event-cancel, or cadence timeout
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                    stoppingToken, _idleTokenSource.Token, idleTimeoutCts.Token);

                try
                {
                    await _client!.IdleAsync(linked.Token);
                }
                catch (OperationCanceledException) when (idleTimeoutCts.IsCancellationRequested)
                {
                    // Woke up due to cadence timeout → keep connection alive
                    if (_client?.IsConnected == true)
                    {
                        _logger.LogDebug("IDLE cadence timeout reached; sending NOOP to keepalive.");
                        await _client.NoOpAsync(stoppingToken);
                    }
                }
                finally
                {
                    _isIdle = false;
                    // clear the per-cycle CTS
                    _idleTokenSource?.Dispose();
                    _idleTokenSource = null;
                    _logger.LogInformation("Exited IMAP IDLE state.");
                }
                // --------- END: CADENCED IDLE ---------

                // After leaving IDLE (by new mail, cadence timeout, or stop), process arrivals
                await ProcessNewMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping or when our event handler cancels IDLE.
                _isIdle = false;
            }
            catch (ImapProtocolException ex) when (ex.Message.Contains("AccessTokenExpired", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(ex, "IMAP session expired due to OAuth token lifetime. Reconnecting immediately.");
                _accessToken = null;
                await DisconnectAsync();
                continue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "IMAP idle loop failed. Reconnecting in 30 seconds.");
                await DisconnectAsync(); // Clean up before retry
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }

    private async Task ConnectAndSubscribeAsync(CancellationToken token)
    {
        var settings = _settings ?? throw new InvalidOperationException("IMAP settings are not loaded.");

        _logger.LogInformation("Connecting to IMAP server...");
        _client = new ImapClient();
        await _client.ConnectAsync(settings.Host, settings.Port, settings.UseSsl, token);
        var tokenValue = await GetValidAccessTokenAsync(token);
        await _client.AuthenticateAsync(new SaslMechanismOAuth2(settings.UserEmail, tokenValue), token);
        await _client.Inbox.OpenAsync(FolderAccess.ReadWrite, token);

        if (!_inboxSubscribed)
        {
            _client.Inbox.CountChanged += InboxOnCountChanged;
            _inboxSubscribed = true;
        }
        _logger.LogInformation(
            "IMAP connected and subscribed. Connected={Connected} Authenticated={Authenticated} InboxOpen={InboxOpen}",
            _client.IsConnected,
            _client.IsAuthenticated,
            _client.Inbox.IsOpen);
    }

    private async Task DisconnectAsync()
    {
        var client = _client;
        if (client == null) return;

        try { _idleTokenSource?.Cancel(); } catch { /* ignore */ }

        try
        {
            if (client.IsConnected)
            {
                try { client.Inbox.CountChanged -= InboxOnCountChanged; } catch { /* ignore */ }
                try { await client.DisconnectAsync(true, CancellationToken.None); } catch { /* ignore */ }
            }
        }
        finally
        {
            try { client.Dispose(); } catch { /* ignore */ }
            _client = null;
            _inboxSubscribed = false;
            _isIdle = false;
            _idleTokenSource?.Dispose();
            _idleTokenSource = null;
            _logger.LogInformation("IMAP client disconnected.");
        }
    }


    private void InboxOnCountChanged(object? sender, EventArgs e)
    {
        _logger.LogInformation("Inbox count changed.");
        if (_isIdle && _idleTokenSource is { IsCancellationRequested: false })
            _idleTokenSource.Cancel();
    }

    private async Task<string> GetValidAccessTokenAsync(CancellationToken token)
    {
        if (_credential is null)
            _credential = new ClientSecretCredential(_settings!.TenantId, _settings.ClientId, _settings.ClientSecret);

        if (_accessToken.HasValue && !TokenExpiresWithin(TimeSpan.FromMinutes(10)))
            return _accessToken!.Value.Token;

        _logger.LogInformation("Refreshing OAuth access token for IMAP.");
        _accessToken = await _credential.GetTokenAsync(OutlookTokenRequestContext, token);
        return _accessToken.Value.Token;
    }

    private bool TokenExpiresWithin(TimeSpan threshold)
    {
        return _accessToken.HasValue && _accessToken.Value.ExpiresOn <= DateTimeOffset.UtcNow.Add(threshold);
    }

    // --- START: NEW MESSAGE PROCESSING METHOD ---
    private async Task ProcessNewMessagesAsync(CancellationToken token)
    {
        if (_client is null || !_client.IsConnected || !_client.Inbox.IsOpen) return;

        var uids = await _client.Inbox.SearchAsync(SearchQuery.NotSeen, token);
        _logger.LogInformation("Found {Count} new message(s).", uids.Count);

        foreach (var uid in uids)
        {
            if (token.IsCancellationRequested) break;
            await ProcessSingleMessageWithGraphAsync(uid, token);
        }
    }
    // --- END: NEW MESSAGE PROCESSING METHOD ---

    private async Task ProcessSingleMessageWithGraphAsync(UniqueId uid, CancellationToken token)
    {
        if (_client is null || !_client.IsConnected || !_client.Inbox.IsOpen || _settings is null) return;

        try
        {
            var summary = await _client.Inbox.FetchAsync(new[] { uid }, MessageSummaryItems.Envelope, token);
            var internetMessageId = summary.FirstOrDefault()?.Envelope.MessageId;

            if (string.IsNullOrEmpty(internetMessageId))
            {
                _logger.LogWarning("Could not retrieve Internet-Message-ID for email UID {Uid}. Skipping.", uid);
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var graphProcessor = scope.ServiceProvider.GetRequiredService<IGraphEmailProcessor>();

            // This is the corrected call
            await graphProcessor.ProcessEmailByInternetMessageIdAsync(internetMessageId, _settings, token);

            _logger.LogInformation("Successfully delegated processing for email with Message-ID {MessageId}", internetMessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delegate processing for email UID {Uid}", uid);
        }
    }
}
