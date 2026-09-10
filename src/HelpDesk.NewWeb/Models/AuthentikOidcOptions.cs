namespace HelpDesk.NewWeb.Models;

public sealed class AuthentikOidcOptions
{
    public string Authority { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ApiScope { get; set; } = string.Empty;
    public string CallbackPath { get; set; } = "/signin-authentik";
    public string SignedOutCallbackPath { get; set; } = "/signout-authentik";
    public string SecretStorePath { get; set; } = string.Empty;
}
