using System.Security.Claims;
using System.Data;
using Helpdesk.Infrastructure.Identity;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
                new Claim("auth_mode", "local"),
                new Claim("security_stamp", user.SecurityStamp ?? string.Empty),
                new Claim("authorization_revision", user.AuthorizationRevision.ToString(global::System.Globalization.CultureInfo.InvariantCulture))
            }.Concat(user.IsInstanceAdministrator
                ? [new Claim(ClaimTypes.Role, "HelpdeskAdmin"), new Claim("roles", "HelpdeskAdmin")]
                : []);
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

        group.MapPost("/change-password", async (
            [FromBody] ChangeLocalPasswordRequest request,
            [FromServices] UserManager<ApplicationUser> users,
            HttpContext context) =>
        {
            var user = await users.GetUserAsync(context.User);
            if (user is null || !user.IsEnabled)
            {
                return Results.Unauthorized();
            }

            var change = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
            if (!change.Succeeded)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["password"] = ["The current password or new password was not accepted."]
                });
            }

            user.AuthorizationRevision++;
            await users.UpdateAsync(user);
            await context.SignOutAsync(LocalAuthenticationOptions.Scheme);
            return Results.NoContent();
        })
        .RequireAuthorization();

        group.MapPost("/users/{userId}/disable", async (
            string userId,
            [FromServices] RatelDeskIdentityDbContext identityDb,
            CancellationToken cancellationToken) =>
        {
            await using var transaction = identityDb.Database.IsInMemory()
                ? null
                : await identityDb.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var target = await identityDb.Users.SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);
            if (target is null)
            {
                return Results.NotFound();
            }

            if (target.IsInstanceAdministrator && target.IsEnabled)
            {
                var enabledAdministrators = await identityDb.Users.CountAsync(
                    user => user.IsInstanceAdministrator && user.IsEnabled,
                    cancellationToken);
                if (enabledAdministrators <= 1)
                {
                    return Results.Conflict(new { error = "last_instance_administrator" });
                }
            }

            target.IsEnabled = false;
            target.DisabledAtUtc = DateTimeOffset.UtcNow;
            target.AuthorizationRevision++;
            target.SecurityStamp = Guid.NewGuid().ToString("N");
            await identityDb.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
            return Results.NoContent();
        })
        .RequireAuthorization("HelpdeskAdmin");

        group.MapPost("/users", async (
            [FromBody] CreateLocalAccountRequest request,
            [FromServices] UserManager<ApplicationUser> users,
            [FromServices] HelpdeskDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.DisplayName))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["account"] = ["Display name and email are required."]
                });
            }

            var normalizedEmail = request.Email.Trim();
            if (await db.Users.AnyAsync(domainUser => domainUser.Email == normalizedEmail))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["account"] = ["The email is already linked to an application user."]
                });
            }

            var user = new ApplicationUser
            {
                UserName = normalizedEmail,
                Email = normalizedEmail,
                DisplayName = request.DisplayName.Trim(),
                IsInstanceAdministrator = request.IsInstanceAdministrator,
                EmailConfirmed = false
            };
            var created = await users.CreateAsync(user);
            if (!created.Succeeded)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["account"] = ["The local account could not be created. The email may already be in use."]
                });
            }

            try
            {
                db.Users.Add(new User
                {
                    Id = user.Id,
                    Name = user.DisplayName,
                    Email = user.Email!,
                    Role = user.IsInstanceAdministrator ? "HelpdeskAdmin" : "User"
                });
                await db.SaveChangesAsync();
            }
            catch (Exception)
            {
                await users.DeleteAsync(user);
                return Results.Problem("The local account could not be linked to the application user record.", statusCode: StatusCodes.Status409Conflict);
            }

            var activationToken = await users.GeneratePasswordResetTokenAsync(user);
            return Results.Created($"/api/v1/local-auth/users/{user.Id}", new LocalAccountActivationResponse(user.Id, user.Email!, activationToken));
        })
        .RequireAuthorization("HelpdeskAdmin");

        group.MapPost("/users/{userId}/activation-token", async (
            string userId,
            [FromServices] UserManager<ApplicationUser> users) =>
        {
            var user = await users.FindByIdAsync(userId);
            if (user is null)
            {
                return Results.NotFound();
            }

            var activationToken = await users.GeneratePasswordResetTokenAsync(user);
            return Results.Ok(new LocalAccountActivationResponse(user.Id, user.Email!, activationToken));
        })
        .RequireAuthorization("HelpdeskAdmin");

        group.MapPost("/activate", async (
            [FromBody] ActivateLocalAccountRequest request,
            [FromServices] UserManager<ApplicationUser> users) =>
        {
            var user = await users.FindByEmailAsync(request.Email);
            if (user is null || !user.IsEnabled)
            {
                return Results.BadRequest(new { error = "activation_failed" });
            }

            var reset = await users.ResetPasswordAsync(user, request.ActivationToken, request.NewPassword);
            if (!reset.Succeeded)
            {
                return Results.BadRequest(new { error = "activation_failed" });
            }

            user.EmailConfirmed = true;
            user.AuthorizationRevision++;
            await users.UpdateAsync(user);
            return Results.NoContent();
        })
        .AllowAnonymous()
        .RequireRateLimiting("LocalLogin");
    }

    public sealed record LocalLoginRequest(string Email, string Password, bool RememberMe = false);

    public sealed record ChangeLocalPasswordRequest(string CurrentPassword, string NewPassword);

    public sealed record CreateLocalAccountRequest(string DisplayName, string Email, bool IsInstanceAdministrator = false);

    public sealed record ActivateLocalAccountRequest(string Email, string ActivationToken, string NewPassword);

    public sealed record LocalAccountActivationResponse(string UserId, string Email, string ActivationToken);
}
