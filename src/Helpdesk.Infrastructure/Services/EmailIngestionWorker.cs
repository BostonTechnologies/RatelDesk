using Helpdesk.Application.Services.Email;
using Helpdesk.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Helpdesk.Infrastructure.Services;

public class EmailIngestionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailIngestionWorker> _logger;
    private readonly EmailIngestionOptions _options;

    public EmailIngestionWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<EmailIngestionWorker> logger,
        IOptions<EmailIngestionOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
        _logger.LogInformation("EmailIngestionWorker initialized.");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Email ingestion disabled.");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.IntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogDebug("Email ingestion cycle starting.");

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var ingestionService = scope.ServiceProvider.GetService<IEmailIngestionService>();
                if (ingestionService == null)
                {
                    _logger.LogWarning("IEmailIngestionService not registered; skipping ingestion cycle.");
                    await Task.Delay(interval, stoppingToken);
                    continue;
                }

                await ingestionService.IngestEmailsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email ingestion run failed.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
