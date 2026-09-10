using Dodo.Primitives;

namespace Helpdesk.Shared.Models;

public class Organization
{
    public string Id { get; set; } = Uuid.CreateVersion7().ToString();
    public string Name { get; set; } = string.Empty;
    public string? DnsName { get; set; }
    public bool EnableAiIntake { get; set; }
    public string? ContactInfo { get; set; }
    public string? AssignedSlaId { get; set; }
    public string? ItSupportOrganizationId { get; set; }
    public int? OrchestrationTenantId { get; set; }
    public string? OrchestrationTenantName { get; set; }
    public DateTimeOffset? OrchestrationTenantLinkedAtUtc { get; set; }
    public EntityState State { get; set; } = EntityState.Enabled;

    public bool IsEnabled
    {
        get => State == EntityState.Enabled;
        set => State = value ? EntityState.Enabled : EntityState.Blocked;
    }
}
