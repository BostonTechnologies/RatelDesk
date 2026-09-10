namespace Helpdesk.API.Services;

public interface IUserPresenceService
{
    void AddConnection(string userId, string connectionId);
    void RemoveConnection(string userId, string connectionId);
    IReadOnlyCollection<string> GetOnlineUsers();
}
