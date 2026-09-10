using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Infrastructure.Persistence.SeedData;

public static class SlaPolicySeed
{
    public static async Task SeedAsync(HelpdeskDbContext db, CancellationToken cancellationToken = default)
    {
        await EnsureSystemDefaultAsync(db, TicketType.Incident, cancellationToken);
        await EnsureSystemDefaultAsync(db, TicketType.Request, cancellationToken);
    }

    private static async Task EnsureSystemDefaultAsync(
        HelpdeskDbContext db,
        TicketType ticketType,
        CancellationToken cancellationToken)
    {
        var exists = await db.SlaPolicies.AnyAsync(policy =>
            policy.ScopeType == SlaScopeType.SystemDefault &&
            policy.AppliesTo == ticketType &&
            policy.IsActive,
            cancellationToken);

        if (exists)
        {
            return;
        }

        db.SlaPolicies.Add(new SlaPolicy
        {
            Name = ticketType == TicketType.Incident
                ? "System Default - Incident"
                : "System Default - Request",
            Description = "Seeded default SLA policy.",
            ScopeType = SlaScopeType.SystemDefault,
            TenantId = null,
            AppliesTo = ticketType,
            ResponseTimeHours = 168,
            ResolutionTimeHours = 168,
            IsActive = true
        });

        await db.SaveChangesAsync(cancellationToken);
    }
}
