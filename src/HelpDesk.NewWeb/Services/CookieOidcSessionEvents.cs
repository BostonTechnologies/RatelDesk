using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Helpdesk.Shared.Auth;
using Helpdesk.Shared.DTOs.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace HelpDesk.NewWeb.Services;

public sealed class CookieOidcSessionEvents(
    ITokenService tokenService,
    ILogger<CookieOidcSessionEvents> logger,
    IHttpClientFactory? httpClientFactory = null) : CookieAuthenticationEvents
{
    private readonly ITokenService _tokenService = tokenService;
    private readonly ILogger<CookieOidcSessionEvents> _logger = logger;

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        if (context.Principal?.Identity?.IsAuthenticated != true)
            return;

        var auth = AuthenticateResult.Success(new AuthenticationTicket(
            context.Principal,
            context.Properties,
            context.Scheme.Name));

        var sessionIsValid = await _tokenService.TryRefreshSessionAsync(
            context.HttpContext,
            auth,
            context.HttpContext.RequestAborted);

        if (sessionIsValid)
        {
            if (httpClientFactory is not null &&
                await TryRefreshAccessClaimsAsync(context, httpClientFactory))
            {
                context.ShouldRenew = true;
            }

            if (context.HttpContext.Items.ContainsKey(TokenService.SessionRefreshedItemKey))
                context.ShouldRenew = true;

            return;
        }

        _logger.LogWarning(
            "Rejecting expired or invalid OIDC session for {Path}. CookieExpiresUtc={CookieExpiresUtc}",
            context.HttpContext.Request.Path,
            context.Properties.ExpiresUtc);
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    private async Task<bool> TryRefreshAccessClaimsAsync(
        CookieValidatePrincipalContext context,
        IHttpClientFactory httpClientFactory)
    {
        var accessToken = context.HttpContext.Items.TryGetValue(TokenService.RefreshedAccessTokenItemKey, out var refreshedAccessToken)
                          && refreshedAccessToken is string refreshedToken
            ? refreshedToken
            : context.Properties.GetTokenValue("access_token");
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return false;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        try
        {
            using var response = await httpClientFactory.CreateClient("SystemApiNoAuth")
                .SendAsync(request, context.HttpContext.RequestAborted);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(context.Scheme.Name);
                return false;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OIDC access projection could not be refreshed because the API returned {StatusCode}.", response.StatusCode);
                return false;
            }

            var access = await response.Content.ReadFromJsonAsync<CurrentUserAccessDto>(context.HttpContext.RequestAborted);
            if (access is null || !access.IsAuthenticated || context.Principal?.Identity is not ClaimsIdentity identity)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(context.Scheme.Name);
                return false;
            }

            RemoveAccessClaims(identity);
            AddClaim(identity, "organization_id", access.PrimaryOrganizationId);
            AddClaim(identity, "customer_id", access.CustomerId);
            foreach (var organizationId in access.AllowedOrganizationIds)
            {
                AddClaim(identity, "allowed_organization_id", organizationId);
            }
            foreach (var grant in access.ScopedPermissionGrants)
            {
                AddClaim(identity, "scoped_permission", grant.ToString());
            }
            foreach (var role in access.RoleBundles.Concat(access.Permissions))
            {
                AddClaim(identity, ClaimTypes.Role, role);
                AddClaim(identity, "roles", role);
            }

            return true;
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "OIDC access projection could not be refreshed because the API is unavailable.");
            return false;
        }
        catch (TaskCanceledException exception) when (!context.HttpContext.RequestAborted.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "OIDC access projection refresh timed out while calling the API.");
            return false;
        }
    }

    private static void RemoveAccessClaims(ClaimsIdentity identity)
    {
        foreach (var claim in identity.Claims.Where(claim => claim.Type is
                     "organization_id" or
                     "allowed_organization_id" or
                     "customer_id" or
                     "scoped_permission" or
                     ClaimTypes.Role or
                     "roles").ToArray())
        {
            if (claim.Type is not ClaimTypes.Role and not "roles" || IsApplicationRoleValue(claim.Value))
            {
                identity.RemoveClaim(claim);
            }
        }
    }

    private static void AddClaim(ClaimsIdentity identity, string type, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !identity.HasClaim(type, value))
        {
            identity.AddClaim(new Claim(type, value));
        }
    }

    private static bool IsApplicationRoleValue(string value) =>
        value is HelpdeskRoleBundles.User or
            HelpdeskRoleBundles.Technical or
            HelpdeskRoleBundles.DataManagementAdmin or
            HelpdeskRoleBundles.HelpdeskAdmin or
            HelpdeskPermissions.HelpdeskAdmin or
            HelpdeskPermissions.SelfServiceUser or
            HelpdeskPermissions.IncidentUser or
            HelpdeskPermissions.IncidentManager or
            HelpdeskPermissions.RequestUser or
            HelpdeskPermissions.RequestManager or
            HelpdeskPermissions.ChangeUser or
            HelpdeskPermissions.ChangeManager or
            HelpdeskPermissions.DataManagementAdmin or
            HelpdeskPermissions.TenantUsersManage or
            HelpdeskPermissions.TenantRolesAssign or
            HelpdeskPermissions.TenantSettingsManage;
}
