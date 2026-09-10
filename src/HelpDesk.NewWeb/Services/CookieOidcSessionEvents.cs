using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace HelpDesk.NewWeb.Services;

public sealed class CookieOidcSessionEvents(
    ITokenService tokenService,
    ILogger<CookieOidcSessionEvents> logger) : CookieAuthenticationEvents
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
}
