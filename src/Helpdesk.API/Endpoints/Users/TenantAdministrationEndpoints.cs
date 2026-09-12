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
        var group = app.MapGroup("/api/v1/tenant-admin/organizations/{organizationId}/users")
            .WithTags("Tenant administration")
            .RequireAuthorization();

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
                .Where(assignment => assignment.UserId == userId && assignment.OrganizationId == organizationId)
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
            db.ScopedRoleAssignments.RemoveRange(existing);
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
}
