using System.Text.Json;
using System.Text.Json.Serialization;

internal sealed record CliConfig(
    string? ApiBaseUrl,
    string? AuthentikTokenUrl,
    string? AuthentikClientId,
    string? AuthentikUsername,
    string? AuthentikAppPassword,
    string? AuthentikScope,
    string? AgentUserEmail)
{
    public string? CredentialMode { get; init; }
    public string? IntegrationCredential { get; init; }
    public const string DefaultApiBaseUrl = "https://api.helpdesk.example.com";
    public const string DefaultScope = "openid profile email";
    public const string DefaultAgentUserEmail = "agent@example.com";

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static CliConfig Empty { get; } = new(null, null, null, null, null, null, null);

    public CliConfig Merge(CliConfig next) => this with
    {
        ApiBaseUrl = Pick(next.ApiBaseUrl, ApiBaseUrl),
        AuthentikTokenUrl = Pick(next.AuthentikTokenUrl, AuthentikTokenUrl),
        AuthentikClientId = Pick(next.AuthentikClientId, AuthentikClientId),
        AuthentikUsername = Pick(next.AuthentikUsername, AuthentikUsername),
        AuthentikAppPassword = Pick(next.AuthentikAppPassword, AuthentikAppPassword),
        AuthentikScope = Pick(next.AuthentikScope, AuthentikScope),
        AgentUserEmail = Pick(next.AgentUserEmail, AgentUserEmail),
        CredentialMode = Pick(next.CredentialMode, CredentialMode),
        IntegrationCredential = Pick(next.IntegrationCredential, IntegrationCredential)
    };

    public ResolvedCliConfig Resolve()
    {
        static string Require(string? value, string name)
            => string.IsNullOrWhiteSpace(value) ? throw new CliValidationException($"{name} is required.") : value;

        var mode = string.IsNullOrWhiteSpace(CredentialMode) ? "authentik" : CredentialMode.Trim().ToLowerInvariant();
        if (mode == "integration")
            return new ResolvedCliConfig(new Uri(string.IsNullOrWhiteSpace(ApiBaseUrl) ? DefaultApiBaseUrl : ApiBaseUrl, UriKind.Absolute), null, null, null, null, null, AgentUserEmail, mode, Require(IntegrationCredential, "RATELDESK_INTEGRATION_CREDENTIAL"));
        if (mode != "authentik") throw new CliValidationException("credentialMode must be authentik or integration.");
        return new ResolvedCliConfig(new Uri(string.IsNullOrWhiteSpace(ApiBaseUrl) ? DefaultApiBaseUrl : ApiBaseUrl, UriKind.Absolute), Require(AuthentikTokenUrl, "RATELDESK_AUTHENTIK_TOKEN_URL"), Require(AuthentikClientId, "RATELDESK_AUTHENTIK_CLIENT_ID"), Require(AuthentikUsername, "RATELDESK_AUTHENTIK_USERNAME"), Require(AuthentikAppPassword, "RATELDESK_AUTHENTIK_APP_PASSWORD"), string.IsNullOrWhiteSpace(AuthentikScope) ? DefaultScope : AuthentikScope, string.IsNullOrWhiteSpace(AgentUserEmail) ? DefaultAgentUserEmail : AgentUserEmail, mode, null);
    }

    private static string? Pick(string? primary, string? fallback)
        => string.IsNullOrWhiteSpace(primary) ? fallback : primary;
}

internal sealed record ResolvedCliConfig(
    Uri ApiBaseUrl,
    string? AuthentikTokenUrl,
    string? AuthentikClientId,
    string? AuthentikUsername,
    string? AuthentikAppPassword,
    string? AuthentikScope,
    string? AgentUserEmail,
    string CredentialMode,
    string? IntegrationCredential);
