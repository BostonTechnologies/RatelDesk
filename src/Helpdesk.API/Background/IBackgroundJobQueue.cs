using System.Threading.Channels;

namespace Helpdesk.API.Background;

public interface IBackgroundJobQueue
{
    void Queue(Func<IServiceProvider, CancellationToken, Task> work);
    ValueTask<Func<IServiceProvider, CancellationToken, Task>> DequeueAsync(CancellationToken ct);
}
