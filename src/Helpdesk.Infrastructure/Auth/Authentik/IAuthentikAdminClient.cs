namespace Helpdesk.Infrastructure.Auth.Authentik;

public interface IAuthentikAdminClient
{
    Task<AuthentikUser?> FindUserByEmailAsync(string email, CancellationToken ct = default);
    Task<AuthentikUser> CreateUserAsync(string name, string email, CancellationToken ct = default);
    Task<AuthentikUser?> GetUserAsync(string userId, CancellationToken ct = default);
    Task SetUserActiveAsync(string userId, bool isActive, CancellationToken ct = default);
    Task AssignUserToGroupAsync(string userId, string groupName, CancellationToken ct = default);
    Task RemoveUserFromGroupAsync(string userId, string groupName, CancellationToken ct = default);
    Task<string> CreateRecoveryLinkAsync(string userId, CancellationToken ct = default);
}
