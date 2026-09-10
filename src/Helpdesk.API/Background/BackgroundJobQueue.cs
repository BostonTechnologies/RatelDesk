using System.Threading.Channels;

namespace Helpdesk.API.Background;

public sealed class BackgroundJobQueue : IBackgroundJobQueue
{
    private readonly Channel<Func<IServiceProvider, CancellationToken, Task>> _channel;

    public BackgroundJobQueue(int capacity = 100)
    {
        var opts = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        };
        _channel = Channel.CreateBounded<Func<IServiceProvider, CancellationToken, Task>>(opts);
    }

    public void Queue(Func<IServiceProvider, CancellationToken, Task> work)
    {
        if (work is null) throw new ArgumentNullException(nameof(work));
        _channel.Writer.TryWrite(work);
    }

    public async ValueTask<Func<IServiceProvider, CancellationToken, Task>> DequeueAsync(CancellationToken ct)
        => await _channel.Reader.ReadAsync(ct);
}
