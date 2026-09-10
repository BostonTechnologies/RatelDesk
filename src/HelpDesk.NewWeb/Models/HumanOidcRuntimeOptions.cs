namespace HelpDesk.NewWeb.Models;

public sealed record HumanOidcRuntimeOptions(
    string? Authority,
    string? ClientId,
    string? ClientSecret,
    string? ApiScope,
    string? CallbackPath,
    string? SignedOutCallbackPath,
    bool UsesAuthentik)
{
    public string TokenEndpoint
    {
        get
        {
            var authority = Authority?.TrimEnd('/');
            return UsesAuthentik
                ? $"{authority}/token/"
                : $"{TrimAzureVersionSegment(authority)}/oauth2/v2.0/token";
        }
    }

    private static string? TrimAzureVersionSegment(string? authority)
        => authority?.EndsWith("/v2.0", StringComparison.OrdinalIgnoreCase) == true
            ? authority[..^"/v2.0".Length]
            : authority;
}

public static class HumanOidcRuntimeOptionsResolver
{
    public static HumanOidcRuntimeOptions Resolve(IConfiguration configuration, AuthentikOidcOptions? options = null)
    {
        var authentikClientId = FirstNonEmpty(options?.ClientId, configuration["Authentication:Authentik:ClientId"]);
        var authentikClientSecret = configuration["AUTHENTIK_CLIENT_SECRET"];

        return new HumanOidcRuntimeOptions(
            FirstNonEmpty(options?.Authority, configuration["Authentication:Authentik:Authority"]),
            authentikClientId,
            authentikClientSecret,
            FirstNonEmpty(options?.ApiScope, configuration["Authentication:Authentik:ApiScope"]),
            FirstNonEmpty(options?.CallbackPath, configuration["Authentication:Authentik:CallbackPath"]),
            FirstNonEmpty(options?.SignedOutCallbackPath, configuration["Authentication:Authentik:SignedOutCallbackPath"]),
            UsesAuthentik: true);
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
