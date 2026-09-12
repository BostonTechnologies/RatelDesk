using System.Security.Claims;
using Helpdesk.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Helpdesk.API.Endpoints.Authentication;

public static class LocalAuthenticationEndpoints
{
    public static void MapLocalAuthenticationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/local-auth").WithTags("Local authentication");

        group.MapPost("/login", async (
            [FromBody] LocalLoginRequest request,
            [FromServices] UserManager<ApplicationUser> users,
            HttpContext context) =>
        {
            var user = await users.FindByEmailAsync(request.Email);
            if (user is null || !user.IsEnabled || await users.IsLockedOutAsync(user))
            {
                return Results.Unauthorized();
            }

            if (!await users.CheckPasswordAsync(user, request.Password))
            {
                await users.AccessFailedAsync(user);
                return Results.Unauthorized();
            }

            await users.ResetAccessFailedCountAsync(user);
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName ?? user.Email! : user.DisplayName),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
                new Claim("auth_mode", "local")
            };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, LocalAuthenticationOptions.Scheme));
            await context.SignInAsync(LocalAuthenticationOptions.Scheme, principal, new AuthenticationProperties
            {
                IsPersistent = request.RememberMe,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(request.RememberMe ? 24 : 8)
            });

            return Results.NoContent();
        })
        .AllowAnonymous()
        .RequireRateLimiting("LocalLogin");

        group.MapPost("/logout", async (HttpContext context) =>
        {
            await context.SignOutAsync(LocalAuthenticationOptions.Scheme);
            return Results.NoContent();
        })
        .RequireAuthorization();
    }

    public sealed record LocalLoginRequest(string Email, string Password, bool RememberMe = false);
}
