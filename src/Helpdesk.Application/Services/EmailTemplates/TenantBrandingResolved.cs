namespace Helpdesk.Application.Services.EmailTemplates;

public sealed class TenantBrandingResolved
{
    public string BrandName { get; init; } = "Helpdesk";
    public string LogoHtml { get; init; } = string.Empty;
    public string FooterHtml { get; init; } = string.Empty;
    public string PrimaryColor { get; init; } = "#0b5fff";
    public string FromName { get; init; } = "Helpdesk";
    public string ReplyTo { get; init; } = string.Empty;
}
