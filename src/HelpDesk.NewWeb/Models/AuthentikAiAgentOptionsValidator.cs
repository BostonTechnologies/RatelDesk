using Microsoft.Extensions.Options;

namespace HelpDesk.NewWeb.Models;

public sealed class AuthentikAiAgentOptionsValidator : IValidateOptions<AuthentikAiAgentOptions>
{
    public ValidateOptionsResult Validate(string? name, AuthentikAiAgentOptions options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var errors = new List<string>();

        if (!Uri.TryCreate(options.Authority, UriKind.Absolute, out var authorityUri)
            || authorityUri.Scheme is not ("https" or "http"))
        {
            errors.Add("Authentication:AuthentikAiAgent:Authority must be an absolute HTTP or HTTPS URI.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            errors.Add("Authentication:AuthentikAiAgent:Audience is required when AI agent auth is enabled.");
        }

        if (!IsValidPath(options.CallbackPath))
        {
            errors.Add("Authentication:AuthentikAiAgent:CallbackPath must start with '/'.");
        }

        if (options.RequiredGroups.Length == 0)
        {
            errors.Add("Authentication:AuthentikAiAgent:RequiredGroups must contain at least one group when AI agent auth is enabled.");
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }

    private static bool IsValidPath(string? path)
        => !string.IsNullOrWhiteSpace(path) && path.StartsWith("/", StringComparison.Ordinal);
}
