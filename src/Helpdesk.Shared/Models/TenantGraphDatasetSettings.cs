namespace Helpdesk.Shared.Models;

public class TenantGraphDatasetSettings
{
    public string OrganizationId { get; set; } = string.Empty;
    public string? TenantId { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecretProtected { get; set; }
    public bool EnableUsers { get; set; }
    public bool EnableDevices { get; set; }
    public bool EnableGroups { get; set; }
    public bool EnableSharePointSites { get; set; }
    public bool BackgroundSyncEnabled { get; set; }
    public string BackgroundSyncCronExpression { get; set; } = "0 */6 * * *";
    public DateTimeOffset? LastUsersSyncUtc { get; set; }
    public DateTimeOffset? LastDevicesSyncUtc { get; set; }
    public DateTimeOffset? LastGroupsSyncUtc { get; set; }
    public DateTimeOffset? LastSharePointSitesSyncUtc { get; set; }
    public string? LastSyncStatus { get; set; }
    public string? LastSyncMessage { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
