using Microsoft.Extensions.Options;

namespace Helpdesk.Mcp.Http.Configuration;

public sealed class AuthentikMcpOptions
{
    public const string SectionName = "Authentication:AuthentikMcp";

    public string Authority { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public string[] RequiredScopes { get; init; } = [];
    public string[] RequiredGroups { get; init; } = [];
}

public sealed class HelpdeskMcpHttpOptions
{
    public const string SectionName = "Helpdesk:Mcp";

    public string ConfigurationPath { get; init; } = string.Empty;
    public string Instance { get; init; } = string.Empty;
    public string ExpectedApiBaseUrl { get; init; } = string.Empty;
    public string PublicResourceUri { get; init; } = string.Empty;
    public string[] AllowedOrigins { get; init; } = [];
}

public sealed class AuthentikMcpOptionsValidator(IOptions<HelpdeskMcpHttpOptions> mcpOptions)
    : IValidateOptions<AuthentikMcpOptions>
{
    public ValidateOptionsResult Validate(string? name, AuthentikMcpOptions options)
    {
        var failures = new List<string>();
        if (!IsAbsoluteHttps(options.Authority)) failures.Add("Authentication:AuthentikMcp:Authority must be an absolute HTTPS URI.");
        if (!IsAbsoluteHttps(options.Audience)) failures.Add("Authentication:AuthentikMcp:Audience must be an absolute HTTPS URI.");
        if (!IsCanonicalMcpResource(mcpOptions.Value.PublicResourceUri)) failures.Add("Helpdesk:Mcp:PublicResourceUri must be an absolute HTTPS /mcp URI.");
        if (!string.Equals(Normalize(options.Audience), Normalize(mcpOptions.Value.PublicResourceUri), StringComparison.Ordinal))
            failures.Add("Authentication:AuthentikMcp:Audience must equal Helpdesk:Mcp:PublicResourceUri.");
        ValidateValues(options.RequiredScopes, "RequiredScopes", failures);
        ValidateValues(options.RequiredGroups, "RequiredGroups", failures);
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateValues(string[] values, string name, ICollection<string> failures)
    {
        if (values.Length == 0 || values.Any(string.IsNullOrWhiteSpace)) failures.Add($"Authentication:AuthentikMcp:{name} must contain non-empty values.");
        if (values.Distinct(StringComparer.Ordinal).Count() != values.Length) failures.Add($"Authentication:AuthentikMcp:{name} must not contain duplicates.");
    }

    internal static bool IsAbsoluteHttps(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
    internal static bool IsCanonicalMcpResource(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && string.Equals(uri.AbsolutePath.TrimEnd('/'), "/mcp", StringComparison.Ordinal);
    internal static string Normalize(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri.AbsoluteUri.TrimEnd('/') : value;
}
