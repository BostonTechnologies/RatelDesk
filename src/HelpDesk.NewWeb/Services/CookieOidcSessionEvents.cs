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

            WebAccessClaimsProjection.Apply(identity, access);

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

}
