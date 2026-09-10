using System.Text.Json;
using System.Text.Json.Serialization;

namespace Helpdesk.AgentClient;

public sealed class AgentClientValidationException(string message) : Exception(message);
public sealed class AgentClientRemoteException(string code, string message, int statusCode, string? responseBody) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
    public string? ResponseBody { get; } = responseBody;
}

public sealed record AgentClientConfiguration(
    string? ApiBaseUrl,
    string? AuthentikTokenUrl,
    string? AuthentikClientId,
    string? AuthentikUsername,
    string? AuthentikAppPassword,
    string? AuthentikScope,
    string? AgentUserEmail)
{
    public const string DefaultApiBaseUrl = "https://api.helpdesk.example.com";
    public const string DefaultScope = "openid profile email";
    public static AgentClientConfiguration Empty { get; } = new(null, null, null, null, null, null, null);
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web) { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public AgentClientConfiguration Merge(AgentClientConfiguration next) => this with
    {
        ApiBaseUrl = Pick(next.ApiBaseUrl, ApiBaseUrl),
        AuthentikTokenUrl = Pick(next.AuthentikTokenUrl, AuthentikTokenUrl),
        AuthentikClientId = Pick(next.AuthentikClientId, AuthentikClientId),
        AuthentikUsername = Pick(next.AuthentikUsername, AuthentikUsername),
        AuthentikAppPassword = Pick(next.AuthentikAppPassword, AuthentikAppPassword),
        AuthentikScope = Pick(next.AuthentikScope, AuthentikScope),
        AgentUserEmail = Pick(next.AgentUserEmail, AgentUserEmail)
    };

    public ResolvedAgentClientConfiguration Resolve()
    {
        static string Required(string? value, string key) => string.IsNullOrWhiteSpace(value) ? throw new AgentClientValidationException($"{key} is required.") : value;
        return new(new Uri(string.IsNullOrWhiteSpace(ApiBaseUrl) ? DefaultApiBaseUrl : ApiBaseUrl), Required(AuthentikTokenUrl, "RATELDESK_AUTHENTIK_TOKEN_URL"), Required(AuthentikClientId, "RATELDESK_AUTHENTIK_CLIENT_ID"), Required(AuthentikUsername, "RATELDESK_AUTHENTIK_USERNAME"), Required(AuthentikAppPassword, "RATELDESK_AUTHENTIK_APP_PASSWORD"), string.IsNullOrWhiteSpace(AuthentikScope) ? DefaultScope : AuthentikScope, AgentUserEmail);
    }

    private static string? Pick(string? primary, string? fallback) => string.IsNullOrWhiteSpace(primary) ? fallback : primary;
}

public sealed record ResolvedAgentClientConfiguration(Uri ApiBaseUrl, string AuthentikTokenUrl, string AuthentikClientId, string AuthentikUsername, string AuthentikAppPassword, string AuthentikScope, string? AgentUserEmail);

public sealed class AgentClientConfigurationStore(string? path = null)
{
    public string Path { get; } = Expand(path) ?? DefaultPath;
    public static string DefaultPath => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "helpdesk", "cli.json");
    public AgentClientConfiguration Load() => !File.Exists(Path) ? AgentClientConfiguration.Empty : JsonSerializer.Deserialize<AgentClientConfiguration>(File.ReadAllText(Path), AgentClientConfiguration.JsonOptions) ?? AgentClientConfiguration.Empty;
    public void Save(AgentClientConfiguration configuration)
    {
        var directory = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(Path, JsonSerializer.Serialize(configuration, AgentClientConfiguration.JsonOptions) + Environment.NewLine);
        if (!OperatingSystem.IsWindows())
        {
            try { File.SetUnixFileMode(Path, UnixFileMode.UserRead | UnixFileMode.UserWrite); }
            catch { /* Best effort on container and network filesystems. */ }
        }
    }
    private static string? Expand(string? path) => string.IsNullOrWhiteSpace(path) ? null : path == "~" ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) : path.StartsWith("~/", StringComparison.Ordinal) ? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[2..]) : path;
}

public static class AgentClientConfigurationResolver
{
    /// <summary>
    /// Loads the deployment-owned configuration for an isolated MCP process.
    /// Unlike <see cref="Load"/>, this deliberately ignores ambient deployment values
    /// so a second instance's container environment cannot override credentials.
    /// </summary>
    public static AgentClientConfiguration LoadIsolated(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new AgentClientValidationException("RATELDESK_MCP_CONFIG is required for isolated MCP configuration.");

        return new AgentClientConfigurationStore(path).Load();
    }

    public static AgentClientConfiguration Load(string? path = null)
    {
        var file = new AgentClientConfigurationStore(path).Load();
        var env = new AgentClientConfiguration(Environment.GetEnvironmentVariable("RATELDESK_API_BASE_URL"), Environment.GetEnvironmentVariable("RATELDESK_AUTHENTIK_TOKEN_URL"), Environment.GetEnvironmentVariable("RATELDESK_AUTHENTIK_CLIENT_ID"), Environment.GetEnvironmentVariable("RATELDESK_AUTHENTIK_USERNAME"), Environment.GetEnvironmentVariable("RATELDESK_AUTHENTIK_APP_PASSWORD"), Environment.GetEnvironmentVariable("RATELDESK_AUTHENTIK_SCOPE"), Environment.GetEnvironmentVariable("RATELDESK_AGENT_USER_EMAIL"));
        return file.Merge(env);
    }
    public static object Redact(AgentClientConfiguration config) => new { config.ApiBaseUrl, config.AuthentikTokenUrl, config.AuthentikClientId, config.AuthentikUsername, authentikAppPassword = string.IsNullOrWhiteSpace(config.AuthentikAppPassword) ? null : "***REDACTED***", config.AuthentikScope, config.AgentUserEmail };
}
