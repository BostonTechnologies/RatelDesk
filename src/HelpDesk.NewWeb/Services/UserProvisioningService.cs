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
        CancellationToken cancellationToken)
    {
        await EnsureUserAccessAsync(
            principal,
            accessToken: null,
            cancellationToken: cancellationToken);
    }

    public async Task<CurrentUserAccessDto?> EnsureUserAccessAsync(
        ClaimsPrincipal principal,
        string? accessToken,
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

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("OIDC provisioning requires the verified access token from the sign-in callback.");
        }

        try
        {
            var client = _httpClientFactory.CreateClient("HelpdeskApi");
            _logger.LogInformation("Provisioning started for {Email}", email);
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/users/provision")
            {
                Content = JsonContent.Create(new { })
            };
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
