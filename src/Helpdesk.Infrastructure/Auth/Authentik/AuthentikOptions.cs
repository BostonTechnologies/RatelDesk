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
}
