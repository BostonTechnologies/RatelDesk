using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Helpdesk.Infrastructure.Auth.Authentik;

public sealed class AuthentikAdminClient(HttpClient httpClient, IOptions<AuthentikOptions> options) : IAuthentikAdminClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient = httpClient;
    private readonly AuthentikOptions _options = options.Value;

    public async Task<AuthentikUser?> FindUserByEmailAsync(string email, CancellationToken ct = default)
    {
        const string operation = "find user by email";
        var path = $"api/v3/core/users/?email={Uri.EscapeDataString(email)}";
        using var response = await SendAsync(HttpMethod.Get, path, null, ct);
        await EnsureSuccessAsync(response, operation, path, ct);
        var users = await response.Content.ReadFromJsonAsync<AuthentikPagedResponse<AuthentikUser>>(JsonOptions, ct);
        return users?.Results.FirstOrDefault(x => string.Equals(x.Email, email, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<AuthentikUser> CreateUserAsync(string name, string email, CancellationToken ct = default)
    {
        var trimmedEmail = email.Trim();
        if (string.IsNullOrWhiteSpace(trimmedEmail))
        {
            throw new InvalidOperationException("Customer email is required before creating an Authentik user.");
        }

        var trimmedName = name.Trim();
        var payload = new AuthentikCreateUserRequest
        {
            Username = trimmedEmail,
            Name = string.IsNullOrWhiteSpace(trimmedName) ? trimmedEmail : trimmedName,
            Email = trimmedEmail
        };
        const string operation = "create user";
        const string path = "api/v3/core/users/";
        using var response = await SendAsync(HttpMethod.Post, path, payload, ct);
        await EnsureSuccessAsync(response, operation, path, ct);
        return await response.Content.ReadFromJsonAsync<AuthentikUser>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Authentik returned an empty user response.");
    }

    public async Task<AuthentikUser?> GetUserAsync(string userId, CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Get, $"api/v3/core/users/{Uri.EscapeDataString(userId)}/", null, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<AuthentikUser>(JsonOptions, ct);
    }

    public async Task SetUserActiveAsync(string userId, bool isActive, CancellationToken ct = default)
    {
        const string operation = "set user active state";
        var path = $"api/v3/core/users/{Uri.EscapeDataString(userId)}/";
        using var response = await SendAsync(HttpMethod.Patch, path, new { is_active = isActive }, ct);
        await EnsureSuccessAsync(response, operation, path, ct);
    }

    public async Task AssignUserToGroupAsync(string userId, string groupName, CancellationToken ct = default)
    {
        var group = await FindGroupByNameAsync(groupName, ct)
            ?? throw new InvalidOperationException($"Authentik group '{groupName}' was not found.");
        const string operation = "assign user to group";
        var path = $"api/v3/core/groups/{group.Id}/add_user/";
        using var response = await SendAsync(HttpMethod.Post, path, new { pk = int.Parse(userId) }, ct);
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            return;
        }

        await EnsureSuccessAsync(response, operation, path, ct);
    }

    public async Task RemoveUserFromGroupAsync(string userId, string groupName, CancellationToken ct = default)
    {
        var group = await FindGroupByNameAsync(groupName, ct);
        if (group is null)
        {
            return;
        }

        const string operation = "remove user from group";
        var path = $"api/v3/core/groups/{group.Id}/remove_user/";
        using var response = await SendAsync(HttpMethod.Post, path, new { pk = int.Parse(userId) }, ct);
        await EnsureSuccessAsync(response, operation, path, ct);
    }

    public async Task<string> CreateRecoveryLinkAsync(string userId, CancellationToken ct = default)
    {
        object? payload = string.IsNullOrWhiteSpace(_options.RecoveryEmailStageId)
            ? null
            : new { email_stage = _options.RecoveryEmailStageId };
        const string operation = "create recovery link";
        var path = $"api/v3/core/users/{Uri.EscapeDataString(userId)}/recovery/";
        using var response = await SendAsync(HttpMethod.Post, path, payload, ct);
        await EnsureSuccessAsync(response, operation, path, ct);
        var link = await response.Content.ReadFromJsonAsync<AuthentikRecoveryLink>(JsonOptions, ct);
        if (string.IsNullOrWhiteSpace(link?.Link))
        {
            throw new InvalidOperationException("Authentik did not return a recovery link.");
        }

        return link.Link;
    }

    private async Task<AuthentikGroup?> FindGroupByNameAsync(string name, CancellationToken ct)
    {
        const string operation = "find group by name";
        var path = $"api/v3/core/groups/?name={Uri.EscapeDataString(name)}";
        using var response = await SendAsync(HttpMethod.Get, path, null, ct);
        await EnsureSuccessAsync(response, operation, path, ct);
        var groups = await response.Content.ReadFromJsonAsync<AuthentikPagedResponse<AuthentikGroup>>(JsonOptions, ct);
        return groups?.Results.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string operation,
        string path,
        CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await ReadSanitizedBodyAsync(response, ct);
        throw new AuthentikAdminRequestException(response.StatusCode, operation, path, body);
    }

    private static async Task<string?> ReadSanitizedBodyAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.Content is null)
        {
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        body = string.Join(' ', body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return body.Length <= 500 ? body : string.Concat(body.AsSpan(0, 500), "...");
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? payload, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiToken))
        {
            throw new AuthentikAdminConfigurationException(
                "Customer invitation is not configured. Set Authentication:AuthentikAdmin:ApiToken for this environment.");
        }

        if (!Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            throw new AuthentikAdminConfigurationException(
                "Customer invitation is not configured. Set Authentication:AuthentikAdmin:BaseUrl to an absolute URL.");
        }

        _httpClient.BaseAddress ??= baseUri;
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiToken);
        if (payload is not null)
        {
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        return await _httpClient.SendAsync(request, ct);
    }

    private sealed class AuthentikCreateUserRequest
    {
        [JsonPropertyName("username")]
        public string Username { get; init; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; init; } = string.Empty;

        [JsonPropertyName("is_active")]
        public bool IsActive { get; init; } = true;

        [JsonPropertyName("path")]
        public string Path { get; init; } = "users";

        [JsonPropertyName("type")]
        public string Type { get; init; } = "internal";
    }
}
