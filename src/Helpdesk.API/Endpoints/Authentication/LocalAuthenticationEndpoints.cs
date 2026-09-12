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

            if (!await users.CheckPasswordAsync(user, request.Password) ||
                !await IsSecondFactorValidAsync(users, user, request.TwoFactorCode))
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

        group.MapPost("/two-factor/setup", async (
            [FromServices] UserManager<ApplicationUser> users,
            HttpContext context) =>
        {
            var user = await users.GetUserAsync(context.User);
            if (user is null || !user.IsEnabled)
            {
                return Results.Unauthorized();
            }

            var reset = await users.ResetAuthenticatorKeyAsync(user);
            if (!reset.Succeeded)
            {
                return Results.Problem("The authenticator setup could not be started.", statusCode: StatusCodes.Status409Conflict);
            }

            user.AuthorizationRevision++;
            await users.UpdateAsync(user);
            var sharedKey = await users.GetAuthenticatorKeyAsync(user);
            var accountName = user.Email ?? user.UserName ?? user.Id;
            var issuer = "RatelDesk";
            var uri = $"otpauth://totp/{Uri.EscapeDataString($"{issuer}:{accountName}")}?secret={Uri.EscapeDataString(sharedKey!)}&issuer={Uri.EscapeDataString(issuer)}&digits=6";
            return Results.Ok(new AuthenticatorSetupResponse(sharedKey!, uri));
        })
        .RequireAuthorization();

        group.MapPost("/two-factor/enable", async (
            [FromBody] EnableTwoFactorRequest request,
            [FromServices] UserManager<ApplicationUser> users,
            HttpContext context) =>
        {
            var user = await users.GetUserAsync(context.User);
            if (user is null || !user.IsEnabled ||
                !await users.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, NormalizeAuthenticatorCode(request.Code)))
            {
                return Results.BadRequest(new { error = "invalid_authenticator_code" });
            }

            var enabled = await users.SetTwoFactorEnabledAsync(user, true);
            if (!enabled.Succeeded)
            {
                return Results.Problem("Two-factor authentication could not be enabled.", statusCode: StatusCodes.Status409Conflict);
            }

            var recoveryCodes = await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
            user.AuthorizationRevision++;
            await users.UpdateAsync(user);
            return Results.Ok(new TwoFactorRecoveryCodesResponse(recoveryCodes?.ToArray() ?? []));
        })
        .RequireAuthorization();

        group.MapPost("/two-factor/disable", async (
            [FromBody] DisableTwoFactorRequest request,
            [FromServices] UserManager<ApplicationUser> users,
            HttpContext context) =>
        {
            var user = await users.GetUserAsync(context.User);
            if (user is null || !user.IsEnabled ||
                !await users.CheckPasswordAsync(user, request.CurrentPassword) ||
                !await users.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, NormalizeAuthenticatorCode(request.Code)))
            {
                return Results.BadRequest(new { error = "two_factor_disable_failed" });
            }

            var disabled = await users.SetTwoFactorEnabledAsync(user, false);
            if (!disabled.Succeeded)
            {
                return Results.Problem("Two-factor authentication could not be disabled.", statusCode: StatusCodes.Status409Conflict);
            }

            user.AuthorizationRevision++;
            await users.UpdateAsync(user);
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

        group.MapPost("/users/{userId}/enable", async (
            string userId,
            [FromServices] UserManager<ApplicationUser> users) =>
        {
            var target = await users.FindByIdAsync(userId);
            if (target is null)
            {
                return Results.NotFound();
            }

            if (target.IsEnabled)
            {
                return Results.NoContent();
            }

            target.IsEnabled = true;
            target.DisabledAtUtc = null;
            target.AuthorizationRevision++;
            target.SecurityStamp = Guid.NewGuid().ToString("N");
            var update = await users.UpdateAsync(target);
            return update.Succeeded
                ? Results.NoContent()
                : Results.Problem("The local account could not be enabled.", statusCode: StatusCodes.Status409Conflict);
        })
        .RequireAuthorization("HelpdeskAdmin");

        group.MapGet("/users/{userId}/status", async (
            string userId,
            [FromServices] UserManager<ApplicationUser> users) =>
        {
            var target = await users.FindByIdAsync(userId);
            return target is null
                ? Results.NotFound()
                : Results.Ok(new LocalAccountStatusResponse(target.IsEnabled));
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
            var role = request.IsInstanceAdministrator
                ? "HelpdeskAdmin"
                : request.Role?.Trim() ?? string.Empty;
            if (role is not ("User" or "Technician"))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["role"] = ["Local accounts can be assigned the User or Technician access bundle."]
                });
            }

            var organizationId = string.IsNullOrWhiteSpace(request.OrganizationId)
                ? null
                : request.OrganizationId.Trim();
            if (organizationId is not null && !await db.Organizations.AnyAsync(organization => organization.Id == organizationId && organization.IsEnabled))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["organizationId"] = ["The organization does not exist or is disabled."]
                });
            }

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
                    Role = role,
                    OrganizationId = organizationId,
                    IsTestUser = request.IsTestUser
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

    public sealed record LocalLoginRequest(string Email, string Password, bool RememberMe = false, string? TwoFactorCode = null);

    public sealed record ChangeLocalPasswordRequest(string CurrentPassword, string NewPassword);

    public sealed record CreateLocalAccountRequest
    {
        public CreateLocalAccountRequest(string displayName, string email, bool isInstanceAdministrator = false)
        {
            DisplayName = displayName;
            Email = email;
            IsInstanceAdministrator = isInstanceAdministrator;
        }

        public string DisplayName { get; init; }

        public string Email { get; init; }

        public bool IsInstanceAdministrator { get; init; }

        public string Role { get; init; } = "User";

        public string? OrganizationId { get; init; }

        public bool IsTestUser { get; init; }
    }

    public sealed record ActivateLocalAccountRequest(string Email, string ActivationToken, string NewPassword);

    public sealed record LocalAccountActivationResponse(string UserId, string Email, string ActivationToken);

    public sealed record LocalAccountStatusResponse(bool IsEnabled);

    public sealed record EnableTwoFactorRequest(string Code);

    public sealed record DisableTwoFactorRequest(string CurrentPassword, string Code);

    public sealed record AuthenticatorSetupResponse(string SharedKey, string AuthenticatorUri);

    public sealed record TwoFactorRecoveryCodesResponse(IReadOnlyList<string> RecoveryCodes);

    private static async Task<bool> IsSecondFactorValidAsync(UserManager<ApplicationUser> users, ApplicationUser user, string? code)
    {
        if (!await users.GetTwoFactorEnabledAsync(user))
        {
            return true;
        }

        var suppliedCode = code?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(suppliedCode))
        {
            return false;
        }

        return await users.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, NormalizeAuthenticatorCode(suppliedCode)) ||
               (await users.RedeemTwoFactorRecoveryCodeAsync(user, suppliedCode)).Succeeded;
    }

    private static string NormalizeAuthenticatorCode(string? code) =>
        code?.Replace(" ", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal) ?? string.Empty;
}
