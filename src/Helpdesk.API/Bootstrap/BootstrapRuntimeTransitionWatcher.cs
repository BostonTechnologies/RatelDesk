using Microsoft.Extensions.Hosting;

namespace Helpdesk.API.Bootstrap;

/// <summary>
/// Lets an operator-run unattended command complete a bootstrap host already
/// running in another process. Once the durable descriptor is Ready, stopping
/// this restricted host lets the supervisor restart it with normal runtime
/// composition.
/// </summary>
public sealed class BootstrapRuntimeTransitionWatcher(
    IBootstrapStateStore stateStore,
    IHostApplicationLifetime applicationLifetime) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var descriptor = await stateStore.LoadOrCreateAsync(stoppingToken);
            if (descriptor.State is BootstrapState.Ready)
            {
                applicationLifetime.StopApplication();
                return;
            }
        }
    }
}
