using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;

namespace Helpdesk.Application.Sla;

public class SlaPolicyResolver(ISlaPolicyRepository slaPolicies) : ISlaPolicyResolver
{
    private readonly ISlaPolicyRepository _slaPolicies = slaPolicies;

    public async Task<SlaPolicy?> ResolveAsync(string? tenantId, TicketType ticketType, int? priority = null, string? serviceId = null)
    {
        var candidates = await _slaPolicies.GetActivePoliciesAsync(tenantId, ticketType) ?? new List<SlaPolicy>();
        if (candidates.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                var tenantPolicy = await _slaPolicies.GetActiveTenantPolicyAsync(tenantId, ticketType);
                if (tenantPolicy is not null)
                {
                    return tenantPolicy;
                }
            }

            return await _slaPolicies.GetActiveSystemPolicyAsync(ticketType);
        }

        return candidates
            .Where(x => IsMatch(x, priority, serviceId))
            .OrderByDescending(x => GetSpecificity(x))
            .ThenByDescending(x => x.MatchRank)
            .ThenBy(x => x.Id, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static bool IsMatch(SlaPolicy policy, int? priority, string? serviceId)
    {
        if (policy.Priority.HasValue && policy.Priority.Value != priority)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(policy.ServiceId) &&
            !string.Equals(policy.ServiceId, serviceId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static int GetSpecificity(SlaPolicy policy)
    {
        var score = 0;
        if (!string.IsNullOrWhiteSpace(policy.TenantId))
        {
            score += 4;
        }

        if (!string.IsNullOrWhiteSpace(policy.ServiceId))
        {
            score += 2;
        }

        if (policy.Priority.HasValue)
        {
            score += 1;
        }

        return score;
    }
}
