namespace Helpdesk.Infrastructure.Auth.Authentik;

public sealed class AuthentikOptions
{
    public string BaseUrl { get; set; } = "https://id.example.com/";
    public string ApiToken { get; set; } = string.Empty;
    public string SecretStorePath { get; set; } = string.Empty;
    public string InviteFlowId { get; set; } = string.Empty;
    public string RecoveryEmailStageId { get; set; } = string.Empty;
    public string[] CustomerDefaultGroups { get; set; } = [];
    public int InviteLifetimeDays { get; set; } = 7;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiToken) &&
        Uri.TryCreate(BaseUrl, UriKind.Absolute, out var baseUri) &&
        (baseUri.Scheme == Uri.UriSchemeHttp || baseUri.Scheme == Uri.UriSchemeHttps);

    public string ConfigurationError => string.IsNullOrWhiteSpace(ApiToken)
        ? "Customer invitation is not configured. Set Authentication:AuthentikAdmin:ApiToken for this environment."
        : "Customer invitation is not configured. Set Authentication:AuthentikAdmin:BaseUrl to an absolute HTTP or HTTPS URL.";
}
