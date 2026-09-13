namespace HelpDesk.NewWeb.Models;

/// <summary>The resolved mode used by both authentication handlers and the login UI.</summary>
public sealed record WebAuthenticationMode(string Value)
{
    public bool SupportsLocalAccounts => Value is "Local" or "Hybrid";
    public bool UsesOidc => Value is "Oidc" or "Hybrid";
    public bool IsHybrid => Value == "Hybrid";

    public static WebAuthenticationMode Resolve(IConfiguration configuration)
    {
        var configured = configuration["Authentication:Mode"]?.Trim();
        if (string.IsNullOrEmpty(configured))
        {
            var oidc = HumanOidcRuntimeOptionsResolver.Resolve(configuration);
            return new(!string.IsNullOrWhiteSpace(oidc.ClientId) && !string.IsNullOrWhiteSpace(oidc.ClientSecret)
                ? "Oidc" : "Local");
        }

        return configured.ToLowerInvariant() switch
        {
            "local" => new("Local"),
            "oidc" => new("Oidc"),
            "hybrid" => new("Hybrid"),
            _ => throw new InvalidOperationException("Authentication:Mode must be Local, Oidc, or Hybrid.")
        };
    }
}
