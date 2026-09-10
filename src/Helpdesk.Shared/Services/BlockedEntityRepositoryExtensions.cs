using Helpdesk.Shared.Models;

namespace Helpdesk.Shared.Services;

public static class BlockedEntityRepositoryExtensions
{
    public static async Task<bool> IsEmailBlockedAsync(this IRepository<BlockedEntity> repo, string email)
    {
        var all = await repo.GetAllAsync();
        var domain = email.Split('@').LastOrDefault() ?? string.Empty;
        return all.Any(b =>
            (!string.IsNullOrEmpty(b.Email) && string.Equals(b.Email, email, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrEmpty(b.Domain) && string.Equals(b.Domain, domain, StringComparison.OrdinalIgnoreCase)));
    }

    public static async Task<bool> IsDomainBlockedAsync(this IRepository<BlockedEntity> repo, string domain)
    {
        var all = await repo.GetAllAsync();
        return all.Any(b => !string.IsNullOrEmpty(b.Domain) && string.Equals(b.Domain, domain, StringComparison.OrdinalIgnoreCase));
    }
}
