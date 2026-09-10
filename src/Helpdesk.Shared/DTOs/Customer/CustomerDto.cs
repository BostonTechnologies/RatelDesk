using System.Text.Json.Serialization;
using Helpdesk.Shared.Models;

namespace Helpdesk.Shared.DTOs.Customer;

public class CustomerDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string OrganizationId { get; set; } = string.Empty;
    public string OrganizationName { get; set; } = string.Empty;
    public CustomerAuthStatusDto? AuthStatus { get; set; }

    [JsonIgnore]
    public EntityState State { get; set; }

    public bool IsEnabled
    {
        get => State == EntityState.Enabled;
        set => State = value ? EntityState.Enabled : EntityState.Blocked;
    }
}
