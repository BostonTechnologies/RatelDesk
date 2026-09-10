using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Helpdesk.API.Background;

public interface IAiSuggestionQueue
{
    ValueTask QueueAsync(string ticketId, CancellationToken ct = default);
    IAsyncEnumerable<string> DequeueAsync(CancellationToken ct);
}

public sealed class AiSuggestionQueue : IAiSuggestionQueue
{
    private readonly Channel<string> _channel = Channel.CreateBounded<string>(
        new BoundedChannelOptions(256) { SingleReader = true, SingleWriter = false });

    public ValueTask QueueAsync(string ticketId, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(ticketId, ct);

    public async IAsyncEnumerable<string> DequeueAsync([EnumeratorCancellation] CancellationToken ct)
    {
        while (await _channel.Reader.WaitToReadAsync(ct))
            while (_channel.Reader.TryRead(out var id))
                yield return id;
    }
}
