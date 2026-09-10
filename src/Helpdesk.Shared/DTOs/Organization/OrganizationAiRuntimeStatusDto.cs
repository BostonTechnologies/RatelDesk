namespace Helpdesk.Shared.DTOs.Organization;

public sealed class OrganizationAiRuntimeStatusDto
{
    public string Status { get; set; } = string.Empty;
    public bool RuntimeReady { get; set; }
    public bool FallbackEnabled { get; set; }
    public int MaxProviderAttempts { get; set; }
    public int EnabledProviderCount { get; set; }
    public int FallbackProviderCount { get; set; }
    public List<string> Reasons { get; set; } = new();
    public List<string> RecentEvidence { get; set; } = new();
    public List<OrganizationAiRuntimeProviderStatusDto> Providers { get; set; } = new();
}
