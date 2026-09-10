namespace Helpdesk.Infrastructure.AiAssistant.Chat;

public sealed class AiAssistantChatOptions
{
    public bool Enabled { get; set; }
    public string Instance { get; set; } = "dev";
    public string Endpoint { get; set; } = string.Empty;
    public string DeviceToken { get; set; } = string.Empty;
    public bool AllowPrivateHttp { get; set; }
    public int IdleMinutes { get; set; } = 15;
    public int ConnectionCapacity { get; set; } = 25;
    public TimeSpan TurnInactivityTimeout { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan ActivityHeartbeatInterval { get; set; } = TimeSpan.FromSeconds(15);

    public bool IsValid()
    {
        if (!Enabled) return true;
        if (Instance != "dev" || string.IsNullOrWhiteSpace(DeviceToken) || IdleMinutes <= 0 || ConnectionCapacity <= 0 || TurnInactivityTimeout <= TimeSpan.Zero || ActivityHeartbeatInterval <= TimeSpan.Zero || ActivityHeartbeatInterval >= TurnInactivityTimeout || !Uri.TryCreate(Endpoint, UriKind.Absolute, out var uri) || uri.AbsolutePath != "/hub/session" || uri.UserInfo.Length != 0 || uri.Query.Length != 0) return false;
        if (uri.Scheme == "https") return true;
        if (!AllowPrivateHttp || uri.Scheme != "http" || !System.Net.IPAddress.TryParse(uri.Host, out var ip)) return false;
        var bytes = ip.GetAddressBytes();
        return bytes.Length == 4 && (bytes[0] == 10 || bytes[0] == 192 && bytes[1] == 168 || bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31);
    }
}
