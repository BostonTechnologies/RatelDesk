namespace Helpdesk.Shared.Models;

public class TenantBranding
{
    public int TenantId { get; set; }
    public string BrandName { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string FooterHtml { get; set; } = string.Empty;
    public string PrimaryColor { get; set; } = "#4f46e5";
    public string FromName { get; set; } = string.Empty;
    public string ReplyTo { get; set; } = string.Empty;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
