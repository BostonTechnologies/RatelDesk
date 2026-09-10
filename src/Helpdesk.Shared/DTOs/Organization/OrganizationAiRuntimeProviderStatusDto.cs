namespace Helpdesk.Shared.DTOs.Organization;

public sealed class OrganizationAiRuntimeProviderStatusDto
{
    public string Channel { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ProviderId { get; set; }
    public string? ProviderName { get; set; }
    public string? ModelId { get; set; }
    public bool Configured { get; set; }
    public bool Enabled { get; set; }
    public bool ConnectivityOk { get; set; }
    public List<string> Reasons { get; set; } = new();
    public List<string> RecentEvidence { get; set; } = new();
}
