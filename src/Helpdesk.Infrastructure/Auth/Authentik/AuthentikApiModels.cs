using System.Text.Json.Serialization;

namespace Helpdesk.Infrastructure.Auth.Authentik;

public sealed class AuthentikPagedResponse<T>
{
    [JsonPropertyName("results")]
    public List<T> Results { get; set; } = [];
}

public sealed class AuthentikUser
{
    [JsonPropertyName("pk")]
    public int Id { get; set; }

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; } = true;

    [JsonPropertyName("last_login")]
    public DateTimeOffset? LastLogin { get; set; }
}

public sealed class AuthentikGroup
{
    [JsonPropertyName("pk")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public sealed class AuthentikRecoveryLink
{
    [JsonPropertyName("link")]
    public string Link { get; set; } = string.Empty;
}
