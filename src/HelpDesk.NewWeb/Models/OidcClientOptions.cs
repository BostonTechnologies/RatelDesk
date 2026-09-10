namespace HelpDesk.NewWeb.Models;

public class OidcClientOptions
{
    public string Provider { get; set; } = "Azure";
    public string Authority { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string PostLogoutRedirectUri { get; set; } = "/";
    public string MetadataUrl { get; set; } = string.Empty;
    public string ResponseType { get; set; } = "code";
    public string Scope { get; set; } = "openid email profile offline_access";
}
