using System.Net.Http.Json;
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
        await EnsureUserAccessAsync(principal, cancellationToken);
    }

    public async Task<CurrentUserAccessDto?> EnsureUserAccessAsync(
        ClaimsPrincipal principal,
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
            var client = _httpClientFactory.CreateClient("SystemApi");
            _logger.LogInformation("Provisioning started for {Email}", email);
            _logger.LogInformation("Provisioning user via system endpoint: {Email}", email);

            var name =
                principal.Identity?.Name ??
                principal.FindFirst("name")?.Value ??
                email;

            var createResponse = await client.PostAsJsonAsync(
                "/api/v1/users/provision",
                new
                {
                    Email = email,
                    Name = name,
                    Issuer = principal.FindFirst("iss")?.Value,
                    Subject = principal.FindFirst("sub")?.Value,
                    AuthentikUserId = principal.FindFirst("authentik_user_id")?.Value
                        ?? principal.FindFirst("ak_user_id")?.Value,
                    PreferredUsername = principal.FindFirst("preferred_username")?.Value
                },
                cancellationToken);

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
