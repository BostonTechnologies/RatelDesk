using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Helpdesk.API.Background;

public sealed class BackgroundJobRunner : BackgroundService
{
    private readonly IBackgroundJobQueue _queue;
    private readonly IServiceProvider _sp;
    private readonly ILogger<BackgroundJobRunner> _logger;

    public BackgroundJobRunner(IBackgroundJobQueue queue, IServiceProvider sp, ILogger<BackgroundJobRunner> logger)
    {
        _queue = queue;
        _sp = sp;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BackgroundJobRunner started");
        while (!stoppingToken.IsCancellationRequested)
        {
            Func<IServiceProvider, CancellationToken, Task>? job = null;
            try
            {
                job = await _queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                using var scope = _sp.CreateScope();
                await job(scope.ServiceProvider, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background job failed");
            }
        }
        _logger.LogInformation("BackgroundJobRunner stopped");
    }
}
