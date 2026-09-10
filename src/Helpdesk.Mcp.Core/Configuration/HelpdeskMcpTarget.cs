using Helpdesk.AgentClient;
using Helpdesk.Mcp.Tools;
using System.Text.Json.Serialization;

namespace Helpdesk.Mcp.Configuration;

public sealed record HelpdeskMcpTarget(string Instance, Uri ApiBaseUrl)
{
    public const string InstanceEnvironmentVariable = "RATELDESK_MCP_INSTANCE";
    public const string ConfigurationEnvironmentVariable = "RATELDESK_MCP_CONFIG";

    public string CanonicalApiBaseUrl => ApiBaseUrl.AbsoluteUri.TrimEnd('/');

    public HelpdeskMcpHostContext ToHostContext(
        string transport,
        string resourceUri = HelpdeskMcpHostContext.DefaultResourceUri,
        string catalogRevision = McpOperationCatalog.Revision)
        => new(Instance, transport, CanonicalApiBaseUrl, resourceUri, catalogRevision);

    public static HelpdeskMcpTarget Resolve(
        AgentClientConfiguration configuration,
        string? configurationPath,
        string? instanceOverride = null,
        string? expectedEndpointOverride = null)
    {
        if (string.IsNullOrWhiteSpace(configurationPath))
            throw new AgentClientValidationException($"{ConfigurationEnvironmentVariable} is required for isolated MCP configuration.");

        var instance = (string.IsNullOrWhiteSpace(instanceOverride) ? Environment.GetEnvironmentVariable(InstanceEnvironmentVariable) : instanceOverride)?.Trim().ToLowerInvariant();
        if (instance is not ("dev" or "prod"))
            throw new AgentClientValidationException($"{InstanceEnvironmentVariable} must be dev or prod.");

        var expectedEndpointVariable = $"RATELDESK_MCP_{instance.ToUpperInvariant()}_API_BASE_URL";
        var expectedEndpoint = ParseUri(string.IsNullOrWhiteSpace(expectedEndpointOverride) ? Environment.GetEnvironmentVariable(expectedEndpointVariable) : expectedEndpointOverride, expectedEndpointVariable);
        var configuredEndpoint = ParseUri(configuration.ApiBaseUrl, "the isolated MCP configuration apiBaseUrl");

        if (!Equivalent(expectedEndpoint, configuredEndpoint))
            throw new AgentClientValidationException($"{InstanceEnvironmentVariable} '{instance}' requires the isolated MCP configuration apiBaseUrl to match {expectedEndpointVariable}.");

        return new HelpdeskMcpTarget(instance, configuredEndpoint);
    }

    public bool MatchesApiBaseUrl(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out var candidate) && IsHttpUri(candidate) && Equivalent(ApiBaseUrl, candidate);

    public HelpdeskMcpTargetInfo ToInfo() => new(Instance, CanonicalApiBaseUrl);

    private static Uri ParseUri(string? value, string name)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || !IsHttpUri(uri))
            throw new AgentClientValidationException($"{name} must be an absolute http or https URL.");

        return uri;
    }

    private static bool IsHttpUri(Uri uri)
        => string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
           || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

    private static bool Equivalent(Uri left, Uri right)
        => string.Equals(Normalize(left), Normalize(right), StringComparison.Ordinal);

    private static string Normalize(Uri uri)
    {
        var builder = new UriBuilder(uri)
        {
            Scheme = uri.Scheme.ToLowerInvariant(),
            Host = uri.Host.ToLowerInvariant(),
            Path = uri.AbsolutePath.TrimEnd('/')
        };

        return builder.Uri.AbsoluteUri.TrimEnd('/');
    }
}

public sealed record HelpdeskMcpTargetInfo(
    [property: JsonPropertyName("instance")] string Instance,
    [property: JsonPropertyName("apiBaseUrl")] string ApiBaseUrl);
