using Microsoft.Extensions.Options;

namespace HelpDesk.NewWeb.Models;

public sealed class AuthentikOidcOptionsValidator(IConfiguration configuration) : IValidateOptions<AuthentikOidcOptions>
{
    public ValidateOptionsResult Validate(string? name, AuthentikOidcOptions options)
    {
        var errors = new List<string>();

        var runtimeOptions = HumanOidcRuntimeOptionsResolver.Resolve(configuration, options);

        if (!Uri.TryCreate(runtimeOptions.Authority, UriKind.Absolute, out var authorityUri)
            || authorityUri.Scheme is not ("https" or "http"))
        {
            errors.Add("Authentication:Authentik:Authority must be an absolute HTTP or HTTPS URI.");
        }

        if (string.IsNullOrWhiteSpace(runtimeOptions.ClientId))
        {
            errors.Add("Authentication:Authentik:ClientId is required.");
        }

        if (string.IsNullOrWhiteSpace(runtimeOptions.ClientSecret))
        {
            errors.Add("AUTHENTIK_CLIENT_SECRET is required.");
        }

        if (string.IsNullOrWhiteSpace(runtimeOptions.ApiScope))
        {
            errors.Add("Authentication:Authentik:ApiScope is required.");
        }

        if (!IsValidPath(runtimeOptions.CallbackPath))
        {
            errors.Add("Authentication:Authentik:CallbackPath must start with '/'.");
        }

        if (!IsValidPath(runtimeOptions.SignedOutCallbackPath))
        {
            errors.Add("Authentication:Authentik:SignedOutCallbackPath must start with '/'.");
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }

    private static bool IsValidPath(string? path)
        => !string.IsNullOrWhiteSpace(path) && path.StartsWith("/", StringComparison.Ordinal);
}
