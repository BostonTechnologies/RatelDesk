using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Helpdesk.Shared.DTOs.Auth;
using Helpdesk.Shared.DTOs.User;
using Dodo.Primitives;
using Helpdesk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Helpdesk.API.Endpoints.Users;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/users")
            .WithTags("Users")
            .RequireAuthorization();

        group.MapGet("/", async ([FromServices] IRepository<User> repo) =>
            (await repo.GetAllAsync()).Select(ToDto));

        group.MapGet("/{id}", async ([FromRoute] string id, [FromServices] IRepository<User> repo) =>
            await repo.GetAsync(id) is User user
                ? Results.Ok(ToDto(user))
                : Results.Problem("User not found", statusCode: 404));

        group.MapGet("/by-email/{email}", async ([FromRoute] string email, [FromServices] IRepository<User> repo) =>
        {
            var all = await repo.GetAllAsync();
            var user = all.FirstOrDefault(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase));
            return user is null ? Results.Problem("User not found", statusCode: 404) : Results.Ok(ToDto(user));
        });

        group.MapPost("/", async ([FromBody] CreateUserRequest request, [FromServices] IRepository<User> repo) =>
        {
            var user = new User
            {
                Id = Uuid.CreateVersion7().ToString(),
                Name = request.Name,
                Email = request.Email,
                Role = request.Role,
                IsTestUser = request.IsTestUser,
                OrganizationId = string.IsNullOrWhiteSpace(request.OrganizationId) ? null : request.OrganizationId,
                HashedPassword = string.IsNullOrWhiteSpace(request.Password) ? null : BCrypt.Net.BCrypt.HashPassword(request.Password)
            };
            var created = await repo.CreateAsync(user);
            return Results.Created($"/api/v1/users/{created.Id}", ToDto(created));
        });

        group.MapPost("/provision", async (
            [FromBody] ProvisionUserRequest request,
            ClaimsPrincipal principal,
            [FromServices] IRepository<User> repo,
            [FromServices] HelpdeskDbContext db,
            [FromServices] ICurrentUserAccessService accessService,
            [FromServices] ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("UserProvisioningEndpoint");

            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["Email"] = ["Email is required."]
                });
            }

            var all = await repo.GetAllAsync();
            var existing = all.FirstOrDefault(u =>
                string.Equals(u.Email, request.Email, StringComparison.OrdinalIgnoreCase));

            if (existing is not null)
            {
                await LinkCustomerLoginAsync(request, db);
                logger.LogInformation("User already provisioned: {Email}", request.Email);
                return Results.Ok(ToAccessDto(await accessService.ResolveAsync(principal)));
            }

            var user = new User
            {
                Id = Uuid.CreateVersion7().ToString(),
                Name = string.IsNullOrWhiteSpace(request.Name) ? request.Email : request.Name,
                Email = request.Email,
                Role = ResolveProvisionedRole(principal)
            };

            var created = await repo.CreateAsync(user);
            await LinkCustomerLoginAsync(request, db);
            logger.LogInformation("User provisioned successfully: {Email}", request.Email);
            return Results.Ok(ToAccessDto(await accessService.ResolveAsync(principal)));
        });

        group.MapPut("/{id}", async ([FromRoute] string id, [FromBody] UpdateUserRequest request, [FromServices] IRepository<User> repo) =>
        {
            var existing = await repo.GetAsync(id);
            if (existing is null)
            {
                return Results.Problem("User not found", statusCode: 404);
            }

            existing.Name = request.Name;
            existing.Email = request.Email;
            existing.Role = request.Role;
            existing.IsTestUser = request.IsTestUser;
            existing.OrganizationId = string.IsNullOrWhiteSpace(request.OrganizationId) ? null : request.OrganizationId;
            if (!string.IsNullOrEmpty(request.Password))
            {
                existing.HashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);
            }

            var updated = await repo.UpdateAsync(existing);
            return updated is null
                ? Results.Problem("User not found", statusCode: 404)
                : Results.Ok(ToDto(updated));
        });

        group.MapDelete("/{id}", async ([FromRoute] string id, [FromServices] IRepository<User> repo) =>
            await repo.DeleteAsync(id)
                ? Results.NoContent()
                : Results.Problem("User not found", statusCode: 404));
    }

    private static UserDto ToDto(User user) => new(user.Id, user.Name, user.Email, user.Role, user.IsTestUser, user.OrganizationId);

    private static CurrentUserAccessDto ToAccessDto(CurrentUserAccessProfile access) => new(
        access.IsAuthenticated,
        access.Name,
        access.Email,
        access.PrimaryOrganizationId,
        access.PrimaryOrganizationName,
        access.CustomerId,
        access.IsHelpdeskAdmin,
        access.RoleBundles.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
        access.Permissions.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
        access.AllowedOrganizationIds.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
        access.ManagedOrganizationIds.Order(StringComparer.OrdinalIgnoreCase).ToArray());

    private static string ResolveProvisionedRole(ClaimsPrincipal principal)
    {
        var roleClaim = principal.Claims.FirstOrDefault(c =>
            c.Type == ClaimTypes.Role || c.Type == "roles")?.Value;

        if (string.Equals(roleClaim, "helpdeskadmin", StringComparison.OrdinalIgnoreCase))
        {
            return "HelpdeskAdmin";
        }

        return string.IsNullOrWhiteSpace(roleClaim) ? "Customer" : roleClaim;
    }

    private static async Task LinkCustomerLoginAsync(ProvisionUserRequest request, HelpdeskDbContext db)
    {
        var issuer = string.IsNullOrWhiteSpace(request.Issuer) ? null : request.Issuer.TrimEnd('/');
        var subject = string.IsNullOrWhiteSpace(request.Subject) ? null : request.Subject;
        var authentikUserId = string.IsNullOrWhiteSpace(request.AuthentikUserId) ? null : request.AuthentikUserId;

        CustomerAuthLink? link = null;
        if (!string.IsNullOrWhiteSpace(issuer) && !string.IsNullOrWhiteSpace(subject))
        {
            link = await db.CustomerAuthLinks.FirstOrDefaultAsync(x => x.OidcIssuer == issuer && x.OidcSubject == subject);
        }

        if (link is null && !string.IsNullOrWhiteSpace(authentikUserId))
        {
            link = await db.CustomerAuthLinks.FirstOrDefaultAsync(x => x.AuthentikUserId == authentikUserId);
        }

        if (link is null)
        {
            var pendingMatches = await db.CustomerAuthLinks
                .Where(x => x.InviteStatus == CustomerInviteStatus.Pending && x.AuthentikEmail == request.Email)
                .ToListAsync();
            if (pendingMatches.Count == 1)
            {
                link = pendingMatches[0];
            }
        }

        if (link is null)
        {
            return;
        }

        link.OidcIssuer ??= issuer;
        link.OidcSubject ??= subject;
        link.AuthentikUserId ??= authentikUserId;
        link.AuthentikUsername = request.PreferredUsername ?? request.Email;
        link.AuthentikEmail = request.Email;
        link.LastLoginAtUtc = DateTimeOffset.UtcNow;
        if (link.InviteStatus == CustomerInviteStatus.Pending)
        {
            link.InviteStatus = CustomerInviteStatus.Active;
            link.InviteAcceptedAtUtc ??= DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync();
    }

    private sealed record ProvisionUserRequest(
        string Email,
        string? Name,
        string? Issuer,
        string? Subject,
        string? AuthentikUserId,
        string? PreferredUsername);
}
