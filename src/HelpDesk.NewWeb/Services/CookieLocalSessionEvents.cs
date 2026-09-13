using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using System.Text.Json;
using Helpdesk.Shared.DTOs.Auth;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace HelpDesk.NewWeb.Services;

/// <summary>
/// Revalidates an API-issued local session before the Web application accepts it.
/// </summary>
public sealed class CookieLocalSessionEvents(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<CookieLocalSessionEvents> logger) : CookieAuthenticationEvents
{
    private readonly string _localCookieName = configuration.GetValue<bool>("Authentication:AllowInsecureLocalhost")
        ? "RatelDesk.Local"
        : "__Host-RatelDesk.Local";

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var cookie = LocalSessionCookieForwarder.GetHeader(context.HttpContext, _localCookieName);
        if (context.Principal?.Identity?.IsAuthenticated != true ||
            !context.Principal.HasClaim("auth_mode", "local") || string.IsNullOrWhiteSpace(cookie))
        {
            await RejectAsync(context);
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.TryAddWithoutValidation("Cookie", cookie);

        try
        {
            using var response = await httpClientFactory.CreateClient("SystemApiNoAuth")
                .SendAsync(request, context.HttpContext.RequestAborted);
            if (!response.IsSuccessStatusCode)
            {
                context.RejectPrincipal();
                if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
                    await RejectAsync(context);
                return;
            }

            var access = await response.Content.ReadFromJsonAsync<CurrentUserAccessDto>(context.HttpContext.RequestAborted);
            if (access?.IsAuthenticated != true || context.Principal.Identity is not ClaimsIdentity identity)
            {
                await RejectAsync(context);
                return;
            }
            WebAccessClaimsProjection.Apply(identity, access);

            // The API owns renewal. Never let the Web handler overwrite its replacement
            // with a ticket carrying stale role or security-stamp claims.
            context.ShouldRenew = false;
            if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
            {
                foreach (var renewedCookie in cookies)
                    context.Response.Headers.Append("Set-Cookie", renewedCookie);
            }
        }
        catch (JsonException exception)
        {
            context.RejectPrincipal();
            logger.LogWarning(exception, "The API returned an invalid local-session projection.");
        }
        catch (HttpRequestException exception)
        {
            context.RejectPrincipal();
            logger.LogWarning(exception, "Could not validate the local browser session because the API is unavailable.");
        }
        catch (TaskCanceledException exception) when (!context.HttpContext.RequestAborted.IsCancellationRequested)
        {
            context.RejectPrincipal();
            logger.LogWarning(exception, "Local browser session validation timed out while calling the API.");
        }
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(context.Scheme.Name);
    }
}
