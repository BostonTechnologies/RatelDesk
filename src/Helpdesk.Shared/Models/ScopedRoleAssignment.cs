using Dodo.Primitives;

namespace Helpdesk.Shared.Models;

public class ScopedRoleAssignment
{
    public string Id { get; set; } = Uuid.CreateVersion7().ToString();
    public string UserId { get; set; } = string.Empty;
    public string RoleKey { get; set; } = string.Empty;
    public string OrganizationId { get; set; } = string.Empty;
}
