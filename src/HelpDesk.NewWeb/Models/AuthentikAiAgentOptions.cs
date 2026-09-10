namespace HelpDesk.NewWeb.Models;

public sealed class AuthentikAiAgentOptions
{
    public bool Enabled { get; set; }
    public string Authority { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string CallbackPath { get; set; } = "/signin-authentik-ai-agent";
    public string SecretStorePath { get; set; } = string.Empty;
    public string[] RequiredGroups { get; set; } = [];
    public string[] SessionRoles { get; set; } = [];
}
