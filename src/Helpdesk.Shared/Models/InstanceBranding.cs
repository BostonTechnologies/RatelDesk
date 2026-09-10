namespace Helpdesk.Shared.Models;

/// <summary>Administrator-managed, deployment-neutral instance identity. This is not a security or protocol identity.</summary>
public sealed class InstanceBranding
{
    public int Id { get; set; } = 1;
    public string? ApplicationName { get; set; }
    public string? OrganizationName { get; set; }
    public string? ApplicationUrl { get; set; }
    public string? OrganizationUrl { get; set; }
    public string? SupportUrl { get; set; }
    public string? SupportEmail { get; set; }
    public string? LogoUrl { get; set; }
    public string? CompactLogoUrl { get; set; }
    public string? FaviconUrl { get; set; }
    public string? EmailFromDisplayName { get; set; }
    public string? Tagline { get; set; }
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
