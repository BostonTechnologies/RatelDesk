using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.Auth;
using Helpdesk.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Infrastructure.Auth.Rbac;

public static class RoleDefinitionSeeder
{
    public static async Task EnsureBuiltInsAsync(HelpdeskDbContext db, CancellationToken cancellationToken = default)
    {
        var existing = await db.Roles
            .Include(role => role.Permissions)
            .Where(role => role.IsBuiltIn)
            .ToDictionaryAsync(role => role.Key, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var definition in RoleDefinitionCatalog.BuiltIns)
        {
            if (!existing.TryGetValue(definition.Key, out var role))
            {
                role = new Role
                {
                    Key = definition.Key,
                    Name = definition.Name,
                    Scope = definition.Scope,
                    IsBuiltIn = true,
                    IsProtected = definition.IsProtected
                };
                db.Roles.Add(role);
            }

            role.Name = definition.Name;
            role.Scope = definition.Scope;
            role.IsBuiltIn = true;
            role.IsProtected = definition.IsProtected;
            var desired = definition.Permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var permission in role.Permissions.Where(permission => !desired.Contains(permission.Permission)).ToArray())
            {
                db.RolePermissions.Remove(permission);
            }
            foreach (var permission in desired.Where(permission => role.Permissions.All(existingPermission =>
                         !string.Equals(existingPermission.Permission, permission, StringComparison.OrdinalIgnoreCase))))
            {
                role.Permissions.Add(new RolePermission { Permission = permission });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
