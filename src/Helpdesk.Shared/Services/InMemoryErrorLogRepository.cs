using System.Collections.Concurrent;
using Helpdesk.Shared.Models;

namespace Helpdesk.Shared.Services;

public class InMemoryErrorLogRepository : IErrorLogRepository
{
    private readonly ConcurrentBag<ErrorLog> _logs = new();

    public Task SaveAsync(ErrorLog entry)
    {
        _logs.Add(entry);
        return Task.CompletedTask;
    }
}
