using System.Collections.Concurrent;

namespace Helpdesk.Application.Services.Tickets;

public class DailySequenceTicketRefGenerator : ITicketRefGeneratorService
{
    private readonly ConcurrentDictionary<string, int> _counters = new();

    public Task<string> NextReferenceAsync(string prefix)
    {
        var date = DateTime.UtcNow.ToString("yyyyMMdd");
        var key = $"{prefix}-{date}";
        var seq = _counters.AddOrUpdate(key, 1, (_, current) => current + 1);
        return Task.FromResult($"{prefix}-{date}-{seq:0000}");
    }
}
