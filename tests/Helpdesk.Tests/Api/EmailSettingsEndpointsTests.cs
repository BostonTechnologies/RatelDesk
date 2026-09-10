using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Helpdesk.API.Endpoints.Email;
using Helpdesk.Application.Services.Email;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.Auth;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Helpdesk.Tests.Api;

public class EmailSettingsEndpointsTests
{
    [Fact]
    public async Task Get_ReturnsDisabledSettings()
    {
        await using var harness = await Harness.CreateAsync();
        var seeded = await harness.SeedAsync(enabled: false, backgroundSyncEnabled: false);

        var settings = await harness.Client.GetFromJsonAsync<List<EmailInboxSettingsDto>>("/api/v1/email-settings");

        var setting = Assert.Single(settings!);
        Assert.Equal(seeded.Id, setting.Id);
        Assert.False(setting.Enabled);
        Assert.False(setting.BackgroundSyncEnabled);
        Assert.Equal("helpdesk@example.com", setting.MailboxAddress);
    }

    [Fact]
    public async Task Get_ReturnsCurrentEnabledSettingsFirst()
    {
        await using var harness = await Harness.CreateAsync();
        var disabledOldest = await harness.SeedAsync(enabled: false, backgroundSyncEnabled: false, mailboxAddress: "older@example.com", updatedAt: DateTimeOffset.UtcNow.AddDays(-3));
        var current = await harness.SeedAsync(enabled: true, backgroundSyncEnabled: true, mailboxAddress: "helpdesk@example.com", updatedAt: DateTimeOffset.UtcNow.AddDays(-1));
        var disabledNewest = await harness.SeedAsync(enabled: false, backgroundSyncEnabled: false, mailboxAddress: "newer@example.com", updatedAt: DateTimeOffset.UtcNow);

        var settings = await harness.Client.GetFromJsonAsync<List<EmailInboxSettingsDto>>("/api/v1/email-settings");

        Assert.NotNull(settings);
        Assert.Equal(3, settings.Count);
        Assert.Equal(current.Id, settings[0].Id);
        Assert.Contains(settings, x => x.Id == disabledOldest.Id);
        Assert.Contains(settings, x => x.Id == disabledNewest.Id);
    }

    [Fact]
    public async Task Put_UpdatesExistingSettingsAndPreservesBlankSecret()
    {
        await using var harness = await Harness.CreateAsync();
        var seeded = await harness.SeedAsync(enabled: true, backgroundSyncEnabled: true, clientSecret: "existing-secret");

        var response = await harness.Client.PutAsJsonAsync($"/api/v1/email-settings/{seeded.Id}", new EmailInboxSettings
        {
            Id = seeded.Id,
            MailHost = "outlook.office365.com",
            Port = 1993,
            UseSsl = false,
            MailboxAddress = "updated@example.com",
            TenantId = "tenant-2",
            ClientId = "client-2",
            ClientSecret = "",
            MailboxFolder = "Inbox/Sub",
            Enabled = false,
            BackgroundSyncEnabled = false
        });

        response.EnsureSuccessStatusCode();
        var all = await harness.AllSettingsAsync();
        var updated = Assert.Single(all);
        Assert.Equal(seeded.Id, updated.Id);
        Assert.Equal("updated@example.com", updated.MailboxAddress);
        Assert.False(updated.UseSsl);
        Assert.False(updated.Enabled);
        Assert.False(updated.BackgroundSyncEnabled);
        Assert.Equal("existing-secret", updated.ClientSecret);
    }

    [Fact]
    public async Task PutTwice_UpdatesExistingSettingsWithoutCreatingDuplicate()
    {
        await using var harness = await Harness.CreateAsync();
        var seeded = await harness.SeedAsync(enabled: true, backgroundSyncEnabled: true, clientSecret: "existing-secret");

        static EmailInboxSettings Payload(Guid id, string mailboxAddress) => new()
        {
            Id = id,
            MailHost = "outlook.office365.com",
            Port = 993,
            UseSsl = true,
            MailboxAddress = mailboxAddress,
            TenantId = "tenant",
            ClientId = "client",
            ClientSecret = "",
            MailboxFolder = "INBOX",
            Enabled = true,
            BackgroundSyncEnabled = true
        };

        var first = await harness.Client.PutAsJsonAsync($"/api/v1/email-settings/{seeded.Id}", Payload(seeded.Id, "updated@example.com"));
        var second = await harness.Client.PutAsJsonAsync($"/api/v1/email-settings/{seeded.Id}", Payload(seeded.Id, "helpdesk@example.com"));

        first.EnsureSuccessStatusCode();
        second.EnsureSuccessStatusCode();
        var updated = Assert.Single(await harness.AllSettingsAsync());
        Assert.Equal(seeded.Id, updated.Id);
        Assert.Equal("helpdesk@example.com", updated.MailboxAddress);
        Assert.Equal("existing-secret", updated.ClientSecret);
    }

    [Fact]
    public async Task PostWithoutId_UpdatesExistingSingleMailboxInsteadOfCreatingDuplicate()
    {
        await using var harness = await Harness.CreateAsync();
        var seeded = await harness.SeedAsync();

        var response = await harness.Client.PostAsJsonAsync("/api/v1/email-settings", new EmailInboxSettings
        {
            MailHost = "outlook.office365.com",
            Port = 993,
            UseSsl = true,
            MailboxAddress = "updated@example.com",
            TenantId = "tenant-updated",
            ClientId = "client-updated",
            ClientSecret = "",
            MailboxFolder = "INBOX",
            Enabled = true,
            BackgroundSyncEnabled = false
        });

        response.EnsureSuccessStatusCode();
        var all = await harness.AllSettingsAsync();
        var updated = Assert.Single(all);
        Assert.Equal(seeded.Id, updated.Id);
        Assert.Equal("updated@example.com", updated.MailboxAddress);
        Assert.Equal("tenant-updated", updated.TenantId);
    }

    [Fact]
    public async Task PostWithoutId_UpdatesCurrentEnabledMailboxWhenOlderDisabledRowsExist()
    {
        await using var harness = await Harness.CreateAsync();
        await harness.SeedAsync(enabled: false, backgroundSyncEnabled: false, mailboxAddress: "older@example.com", updatedAt: DateTimeOffset.UtcNow.AddDays(-3));
        var current = await harness.SeedAsync(enabled: true, backgroundSyncEnabled: true, mailboxAddress: "helpdesk@example.com", updatedAt: DateTimeOffset.UtcNow.AddDays(-1));
        await harness.SeedAsync(enabled: false, backgroundSyncEnabled: false, mailboxAddress: "newer@example.com", updatedAt: DateTimeOffset.UtcNow);

        var response = await harness.Client.PostAsJsonAsync("/api/v1/email-settings", new EmailInboxSettings
        {
            MailHost = "outlook.office365.com",
            Port = 993,
            UseSsl = true,
            MailboxAddress = "helpdesk-updated@example.com",
            TenantId = "tenant-updated",
            ClientId = "client-updated",
            ClientSecret = "",
            MailboxFolder = "INBOX",
            Enabled = true,
            BackgroundSyncEnabled = true
        });

        response.EnsureSuccessStatusCode();
        var all = await harness.AllSettingsAsync();
        Assert.Equal(3, all.Count);
        var updated = Assert.Single(all, x => x.Id == current.Id);
        Assert.Equal("helpdesk-updated@example.com", updated.MailboxAddress);
        Assert.Equal("tenant-updated", updated.TenantId);
    }

    [Fact]
    public async Task Test_UsesSubmittedValuesAndDoesNotPersist()
    {
        await using var harness = await Harness.CreateAsync();
        var seeded = await harness.SeedAsync(clientSecret: "stored-secret");

        var response = await harness.Client.PostAsJsonAsync("/api/v1/email-settings/test", new EmailInboxSettings
        {
            Id = seeded.Id,
            MailHost = "imap.example.test",
            Port = 1993,
            UseSsl = false,
            MailboxAddress = "unsaved@example.com",
            TenantId = "tenant-test",
            ClientId = "client-test",
            ClientSecret = "",
            MailboxFolder = "Unsaved",
            Enabled = true,
            BackgroundSyncEnabled = true
        });

        response.EnsureSuccessStatusCode();
        await harness.Imap.Received(1).TestConnectionAsync(
            Arg.Is<ImapEmailSettings>(x =>
                x.Host == "imap.example.test" &&
                x.Port == 1993 &&
                !x.UseSsl &&
                x.UserEmail == "unsaved@example.com" &&
                x.ClientSecret == "stored-secret"),
            Arg.Any<CancellationToken>());
        var persisted = Assert.Single(await harness.AllSettingsAsync());
        Assert.Equal("helpdesk@example.com", persisted.MailboxAddress);
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private Harness(WebApplication app, HttpClient client, IImapEmailService imap)
        {
            _app = app;
            Client = client;
            Imap = imap;
        }

        public HttpClient Client { get; }
        public IImapEmailService Imap { get; }

        public static async Task<Harness> CreateAsync()
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
            builder.WebHost.UseTestServer();
            builder.Services.AddHttpContextAccessor();
            var databaseRoot = new InMemoryDatabaseRoot();
            var databaseName = $"email-settings-{Guid.NewGuid():N}";
            builder.Services.AddDbContext<HelpdeskDbContext>(options => options.UseInMemoryDatabase(databaseName, databaseRoot));
            builder.Services.AddScoped<ITenantContext>(_ => Substitute.For<ITenantContext>());
            var imap = Substitute.For<IImapEmailService>();
            imap.TestConnectionAsync(Arg.Any<ImapEmailSettings>(), Arg.Any<CancellationToken>()).Returns(true);
            builder.Services.AddSingleton(imap);
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "Test";
                options.DefaultChallengeScheme = "Test";
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("HelpdeskAdmin", policy =>
                {
                    policy.AddAuthenticationSchemes("Test");
                    policy.RequireAuthenticatedUser();
                    policy.RequireRole(HelpdeskPermissions.HelpdeskAdmin);
                });
            });

            var app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapEmailSettingsEndpoints();
            await app.StartAsync();
            var client = app.GetTestClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", HelpdeskPermissions.HelpdeskAdmin);
            return new Harness(app, client, imap);
        }

        public async Task<EmailInboxSettings> SeedAsync(
            bool enabled = true,
            bool backgroundSyncEnabled = true,
            string clientSecret = "secret",
            string mailboxAddress = "helpdesk@example.com",
            DateTimeOffset? updatedAt = null)
        {
            using var scope = _app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();
            var timestamp = updatedAt ?? DateTimeOffset.UtcNow;
            var settings = new EmailInboxSettings
            {
                Id = Guid.NewGuid(),
                MailHost = "outlook.office365.com",
                Port = 993,
                UseSsl = true,
                MailboxAddress = mailboxAddress,
                TenantId = "tenant",
                ClientId = "client",
                ClientSecret = clientSecret,
                MailboxFolder = "INBOX",
                Enabled = enabled,
                BackgroundSyncEnabled = backgroundSyncEnabled,
                CreatedAt = timestamp.AddMinutes(-5),
                UpdatedAt = timestamp
            };
            db.EmailInboxSettings.Add(settings);
            await db.SaveChangesAsync();
            return settings;
        }

        public async Task<List<EmailInboxSettings>> AllSettingsAsync()
        {
            using var scope = _app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();
            return await db.EmailInboxSettings.AsNoTracking().OrderBy(x => x.CreatedAt).ToListAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await _app.DisposeAsync();
        }
    }

    private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, "email-settings-test"),
                new Claim(ClaimTypes.Role, HelpdeskPermissions.HelpdeskAdmin)
            };
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
