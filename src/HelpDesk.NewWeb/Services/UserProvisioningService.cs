using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Security.Claims;
using Helpdesk.Shared.DTOs.Auth;

namespace HelpDesk.NewWeb.Services;

public class UserProvisioningService : IUserProvisioningService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<UserProvisioningService> _logger;

    public UserProvisioningService(
        IHttpClientFactory httpClientFactory,
        ILogger<UserProvisioningService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task EnsureUserExistsAsync(
        ClaimsPrincipal principal,
        string accessToken,
        CancellationToken cancellationToken)
    {
        await EnsureUserAccessAsync(principal, accessToken, cancellationToken);
    }

    public async Task<CurrentUserAccessDto?> EnsureUserAccessAsync(
        ClaimsPrincipal principal,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var email =
            principal.FindFirst(ClaimTypes.Email)?.Value ??
            principal.FindFirst("email")?.Value ??
            principal.FindFirst("preferred_username")?.Value;

        if (string.IsNullOrWhiteSpace(email))
        {
            _logger.LogWarning("Provisioning skipped: no email claim.");
            return null;
        }

        try
        {
            if (string.IsNullOrWhiteSpace(accessToken))
                throw new InvalidOperationException("A verified user's API access token is required for provisioning.");

            var client = _httpClientFactory.CreateClient("SystemApiNoAuth");
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/users/provision");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var createResponse = await client.SendAsync(request, cancellationToken);

            createResponse.EnsureSuccessStatusCode();
            _logger.LogInformation("Provisioning ensured successfully for {Email}", email);
            return await createResponse.Content.ReadFromJsonAsync<CurrentUserAccessDto>(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Provisioning failed for {Email}", email);
            throw;
        }
    }
}
