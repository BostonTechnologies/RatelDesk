using Dodo.Primitives;

namespace Helpdesk.Shared.Models;

public class User
{
    public string Id { get; set; } = Uuid.CreateVersion7().ToString();
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? OrganizationId { get; set; }
    public string? HashedPassword { get; set; }
    public string Role { get; set; } = string.Empty;
    public bool IsTestUser { get; set; }
}
