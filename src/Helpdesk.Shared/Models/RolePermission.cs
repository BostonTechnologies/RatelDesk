namespace Helpdesk.Shared.Models;

public class RolePermission
{
    public string RoleId { get; set; } = string.Empty;
    public string Permission { get; set; } = string.Empty;
    public Role? Role { get; set; }
}
