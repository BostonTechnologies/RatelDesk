using System.Text.Json.Serialization;

namespace HelpDesk.NewWeb.Models;

public sealed class InstanceBrandingAdministrationResponse
{
    public InstanceBrandingSnapshot Effective { get; set; } = new();

    public List<InstanceBrandingFieldState> Fields { get; set; } = [];
}

public sealed class InstanceBrandingFieldState
{
    public string Name { get; set; } = string.Empty;

    public InstanceBrandingValueSource Source { get; set; }

    public bool IsAdminEditable { get; set; }
}

// The API currently serializes this enum as a number. Accept string values as well
// so that the Web client remains compatible if the API changes to string enums.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum InstanceBrandingValueSource
{
    Default,
    Database,
    Environment
}

public class InstanceBrandingSnapshot
{
    public string ApplicationName { get; set; } = "RatelDesk";

    public string OrganizationName { get; set; } = string.Empty;

    public string ApplicationUrl { get; set; } = string.Empty;

    public string OrganizationUrl { get; set; } = string.Empty;

    public string SupportUrl { get; set; } = string.Empty;

    public string SupportEmail { get; set; } = string.Empty;

    public string LogoUrl { get; set; } = string.Empty;

    public string CompactLogoUrl { get; set; } = string.Empty;

    public string FaviconUrl { get; set; } = string.Empty;

    public string EmailFromDisplayName { get; set; } = string.Empty;

    public string Tagline { get; set; } = string.Empty;
}

public sealed class InstanceBrandingUpdate : InstanceBrandingSnapshot
{
    public static InstanceBrandingUpdate From(InstanceBrandingSnapshot value) => new()
    {
        ApplicationName = value.ApplicationName,
        OrganizationName = value.OrganizationName,
        ApplicationUrl = value.ApplicationUrl,
        OrganizationUrl = value.OrganizationUrl,
        SupportUrl = value.SupportUrl,
        SupportEmail = value.SupportEmail,
        LogoUrl = value.LogoUrl,
        CompactLogoUrl = value.CompactLogoUrl,
        FaviconUrl = value.FaviconUrl,
        EmailFromDisplayName = value.EmailFromDisplayName,
        Tagline = value.Tagline
    };
}
