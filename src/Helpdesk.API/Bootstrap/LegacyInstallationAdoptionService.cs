using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.Models;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Helpdesk.API.Bootstrap;

/// <summary>
/// Records durable rc.4 initialization evidence for a pre-rc.4 installation.
/// Migration history alone is intentionally not sufficient: a newly migrated
/// empty database must still enter first-run setup.
/// </summary>
public sealed class LegacyInstallationAdoptionService
{
    public async Task<LegacyInstallationAdoptionResult> AdoptAsync(
        HelpdeskDbContext database,
        CancellationToken cancellationToken)
    {
        if (await database.InstanceInitializations.AnyAsync(cancellationToken))
        {
            return LegacyInstallationAdoptionResult.AlreadyMarked;
        }

        // A legacy RatelDesk instance must have its application tenant and at
        // least one tenant-linked principal. This deliberately excludes an
        // empty database that has only been brought to the current schema.
        var hasOrganization = await database.Organizations.AnyAsync(cancellationToken);
        var hasTenantLinkedUser = await database.Users.AnyAsync(user =>
            !string.IsNullOrWhiteSpace(user.OrganizationId) &&
            !string.IsNullOrWhiteSpace(user.Email), cancellationToken);
        if (!hasOrganization || !hasTenantLinkedUser)
        {
            return LegacyInstallationAdoptionResult.NotEstablished;
        }

        database.InstanceInitializations.Add(new InstanceInitialization
        {
            Id = InstanceInitialization.SingletonId,
            InstanceId = Guid.NewGuid(),
            OperationId = Guid.NewGuid(),
            SetupVersion = GetSetupVersion(),
            CompletedAtUtc = DateTimeOffset.UtcNow
        });

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            return LegacyInstallationAdoptionResult.Adopted;
        }
        catch (DbUpdateException)
        {
            // Multiple application instances can upgrade together. The
            // singleton primary key makes adoption idempotent across hosts.
            database.ChangeTracker.Clear();
            if (await database.InstanceInitializations.AnyAsync(cancellationToken))
            {
                return LegacyInstallationAdoptionResult.AlreadyMarked;
            }

            throw;
        }
    }

    private static string GetSetupVersion() =>
        typeof(LegacyInstallationAdoptionService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "unknown";
}

public enum LegacyInstallationAdoptionResult
{
    NotEstablished,
    Adopted,
    AlreadyMarked
}
