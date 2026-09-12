using Helpdesk.Infrastructure.Identity;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.Auth;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.API.Endpoints.Users;

public static class TenantAdministrationEndpoints
{
    private static readonly HashSet<string> DelegableRoleKeys =
        [ScopedRoleCatalog.SelfServiceUser];

    public static void MapTenantAdministrationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/tenant-admin/organizations", async (
            HttpContext context,
            ICurrentUserAccessService accessService,
            HelpdeskDbContext db,
            CancellationToken cancellationToken) =>
        {
            var access = await accessService.ResolveAsync(context.User, cancellationToken);
            var organizationIds = access.IsHelpdeskAdmin
                ? null
                : access.OrganizationIdsFor(HelpdeskPermissions.TenantRolesAssign);
            if (organizationIds is { Count: 0 })
            {
                return Results.Ok(Array.Empty<TenantOrganizationResponse>());
            }

            var organizations = db.Organizations.AsNoTracking().Where(organization => organization.IsEnabled);
            if (organizationIds is not null)
            {
                organizations = organizations.Where(organization => organizationIds.Contains(organization.Id));
            }

            return Results.Ok(await organizations
                .OrderBy(organization => organization.Name)
                .Select(organization => new TenantOrganizationResponse(organization.Id, organization.Name))
                .ToArrayAsync(cancellationToken));
        })
        .RequireAuthorization()
        .WithName("GetTenantAdministrationOrganizations");

        var group = app.MapGroup("/api/v1/tenant-admin/organizations/{organizationId}/users")
            .WithTags("Tenant administration")
            .RequireAuthorization();

        group.MapGet("/", async (
            string organizationId,
            HttpContext context,
            ICurrentUserAccessService accessService,
            HelpdeskDbContext db,
            UserManager<ApplicationUser> users,
            CancellationToken cancellationToken) =>
        {
            if (!await CanManageAsync(context, accessService, organizationId, cancellationToken)) return Results.Forbid();

            var members = await db.Users.AsNoTracking()
                .Where(user => user.OrganizationId == organizationId)
                .OrderBy(user => user.Name)
                .Select(user => new TenantMemberCandidate(user.Id, user.Name, user.Email))
                .ToArrayAsync(cancellationToken);
            var memberIds = members.Select(member => member.Id).ToArray();
            var localUserIds = await users.Users.AsNoTracking()
                .Where(user => memberIds.Contains(user.Id) && !user.IsInstanceAdministrator)
                .Select(user => user.Id)
                .ToHashSetAsync(StringComparer.OrdinalIgnoreCase, cancellationToken);
            var assignments = await db.ScopedRoleAssignments.AsNoTracking()
                .Where(assignment =>
                    assignment.OrganizationId == organizationId &&
                    localUserIds.Contains(assignment.UserId) &&
                    DelegableRoleKeys.Contains(assignment.RoleKey))
                .OrderBy(assignment => assignment.RoleKey)
                .ToArrayAsync(cancellationToken);
            var rolesByUser = assignments
                .GroupBy(assignment => assignment.UserId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<string>)group.Select(assignment => assignment.RoleKey).ToArray(),
                    StringComparer.OrdinalIgnoreCase);

            return Results.Ok(members
                .Where(member => localUserIds.Contains(member.Id))
                .Select(member => new TenantMemberResponse(
                    member.Id,
                    member.Name,
                    member.Email,
                    rolesByUser.GetValueOrDefault(member.Id, [])))
                .ToArray());
        });

        group.MapGet("/{userId}/assignments", async (
            string organizationId,
            string userId,
            HttpContext context,
            ICurrentUserAccessService accessService,
            HelpdeskDbContext db,
            UserManager<ApplicationUser> users,
            CancellationToken cancellationToken) =>
        {
            if (!await CanManageAsync(context, accessService, organizationId, cancellationToken)) return Results.Forbid();
            var target = await FindTenantLocalUserAsync(userId, organizationId, db, users, cancellationToken);
            if (target is null) return Results.NotFound();
            if (target.IsInstanceAdministrator) return Results.Forbid();

            var assignments = await db.ScopedRoleAssignments.AsNoTracking()
                .Where(assignment =>
                    assignment.UserId == userId &&
                    assignment.OrganizationId == organizationId &&
                    DelegableRoleKeys.Contains(assignment.RoleKey))
                .OrderBy(assignment => assignment.RoleKey)
                .Select(assignment => assignment.RoleKey)
                .ToArrayAsync(cancellationToken);
            return Results.Ok(new TenantMembershipResponse(userId, organizationId, assignments));
        });

        group.MapPut("/{userId}/assignments", async (
            string organizationId,
            string userId,
            ReplaceTenantMembershipRequest request,
            HttpContext context,
            ICurrentUserAccessService accessService,
            HelpdeskDbContext db,
            UserManager<ApplicationUser> users,
            CancellationToken cancellationToken) =>
        {
            if (!await CanManageAsync(context, accessService, organizationId, cancellationToken)) return Results.Forbid();
            var target = await FindTenantLocalUserAsync(userId, organizationId, db, users, cancellationToken);
            if (target is null) return Results.NotFound();
            if (target.IsInstanceAdministrator) return Results.Forbid();

            var roleKeys = (request.RoleKeys ?? [])
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Select(key => key.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (roleKeys.Any(key => !DelegableRoleKeys.Contains(key)))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["roleKeys"] = ["Tenant administrators may assign only the self-service role through this workflow."]
                });
            }

            var existing = await db.ScopedRoleAssignments
                .Where(assignment => assignment.UserId == userId && assignment.OrganizationId == organizationId)
                .ToListAsync(cancellationToken);
            db.ScopedRoleAssignments.RemoveRange(existing.Where(assignment => DelegableRoleKeys.Contains(assignment.RoleKey)));
            db.ScopedRoleAssignments.AddRange(roleKeys.Select(key => new ScopedRoleAssignment
            {
                UserId = userId,
                OrganizationId = organizationId,
                RoleKey = key
            }));
            await db.SaveChangesAsync(cancellationToken);

            target.AuthorizationRevision++;
            target.SecurityStamp = Guid.NewGuid().ToString("N");
            var update = await users.UpdateAsync(target);
            return update.Succeeded ? Results.NoContent() : Results.Conflict();
        });
    }

    private static async Task<bool> CanManageAsync(HttpContext context, ICurrentUserAccessService accessService, string organizationId, CancellationToken cancellationToken)
    {
        var access = await accessService.ResolveAsync(context.User, cancellationToken);
        return access.IsHelpdeskAdmin || access.HasPermission(HelpdeskPermissions.TenantRolesAssign, organizationId);
    }

    private static async Task<ApplicationUser?> FindTenantLocalUserAsync(
        string userId,
        string organizationId,
        HelpdeskDbContext db,
        UserManager<ApplicationUser> users,
        CancellationToken cancellationToken)
    {
        if (!await db.Users.AsNoTracking().AnyAsync(user => user.Id == userId && user.OrganizationId == organizationId, cancellationToken))
        {
            return null;
        }

        return await users.FindByIdAsync(userId);
    }

    public sealed record ReplaceTenantMembershipRequest(IReadOnlyList<string> RoleKeys);
    public sealed record TenantMembershipResponse(string UserId, string OrganizationId, IReadOnlyList<string> RoleKeys);
    public sealed record TenantOrganizationResponse(string Id, string Name);
    public sealed record TenantMemberResponse(string UserId, string Name, string Email, IReadOnlyList<string> RoleKeys);

    private sealed record TenantMemberCandidate(string Id, string Name, string Email);
}
