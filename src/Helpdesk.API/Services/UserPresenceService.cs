using System.Collections.Concurrent;

namespace Helpdesk.API.Services;

public class UserPresenceService : IUserPresenceService
{
    private readonly ConcurrentDictionary<string, HashSet<string>> _connections = new();

    public void AddConnection(string userId, string connectionId)
    {
        var set = _connections.GetOrAdd(userId, _ => []);
        lock (set)
        {
            set.Add(connectionId);
        }
    }

    public void RemoveConnection(string userId, string connectionId)
    {
        if (_connections.TryGetValue(userId, out var set))
        {
            lock (set)
            {
                set.Remove(connectionId);
                if (set.Count == 0)
                {
                    _connections.TryRemove(userId, out _);
                }
            }
        }
    }

    public IReadOnlyCollection<string> GetOnlineUsers() => _connections.Keys.ToList();
}
