using Microsoft.AspNetCore.Authentication;
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
        if (context.Principal?.Identity?.IsAuthenticated != true ||
            !context.Principal.HasClaim("auth_mode", "local") ||
            !context.HttpContext.Request.Cookies.TryGetValue(_localCookieName, out var cookie) ||
            string.IsNullOrWhiteSpace(cookie))
        {
            await RejectAsync(context);
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.TryAddWithoutValidation("Cookie", $"{_localCookieName}={cookie}");

        try
        {
            using var response = await httpClientFactory.CreateClient("SystemApiNoAuth")
                .SendAsync(request, context.HttpContext.RequestAborted);
            if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            {
                await RejectAsync(context);
            }
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Could not validate the local browser session because the API is unavailable.");
        }
        catch (TaskCanceledException exception) when (!context.HttpContext.RequestAborted.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Local browser session validation timed out while calling the API.");
        }
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(context.Scheme.Name);
    }
}
