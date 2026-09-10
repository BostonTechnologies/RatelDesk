using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;

namespace Helpdesk.Application.Services.SupportNotifications;

public sealed class SupportAccessService(
    IRepository<User> users,
    IRepository<SupportGroup> supportGroups,
    IRepository<SupportGroupMember> supportGroupMembers,
    IRepository<OrganizationSupportCoverage> coverages) : ISupportAccessService
{
    public async Task<bool> CanUserSupportOrganizationAsync(
        string userId,
        string customerOrganizationId,
        CancellationToken ct = default)
    {
        _ = ct;

        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(customerOrganizationId))
        {
            return false;
        }

        var user = await users.GetAsync(userId);
        if (user is null || string.IsNullOrWhiteSpace(user.Email))
        {
            return false;
        }

        var activeMemberships = (await supportGroupMembers.GetAllAsync())
            .Where(x => x.IsEnabled && string.Equals(x.UserId, userId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (activeMemberships.Count == 0)
        {
            return false;
        }

        var groupIds = activeMemberships
            .Select(x => x.SupportGroupId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var activeGroupIds = (await supportGroups.GetAllAsync())
            .Where(x => x.IsEnabled && groupIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (activeGroupIds.Count == 0)
        {
            return false;
        }

        return (await coverages.GetAllAsync()).Any(x =>
            x.IsEnabled &&
            activeGroupIds.Contains(x.SupportGroupId) &&
            string.Equals(x.CustomerOrganizationId, customerOrganizationId, StringComparison.OrdinalIgnoreCase));
    }
}
