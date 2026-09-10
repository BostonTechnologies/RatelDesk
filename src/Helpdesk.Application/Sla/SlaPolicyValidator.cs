using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;

namespace Helpdesk.Application.Sla;

public class SlaPolicyValidator : ISlaPolicyValidator
{
    public void ValidateForSave(SlaPolicy policy)
    {
        if (policy.ScopeType == SlaScopeType.SystemDefault && !string.IsNullOrWhiteSpace(policy.TenantId))
        {
            throw new InvalidOperationException("TenantId must be null for SystemDefault scope.");
        }

        if (policy.ScopeType == SlaScopeType.SystemDefault && policy.Priority.HasValue)
        {
            throw new InvalidOperationException("Priority must be null for SystemDefault scope.");
        }

        if (policy.ScopeType == SlaScopeType.SystemDefault && !string.IsNullOrWhiteSpace(policy.ServiceId))
        {
            throw new InvalidOperationException("ServiceId must be null for SystemDefault scope.");
        }

        if (policy.ScopeType == SlaScopeType.Tenant && string.IsNullOrWhiteSpace(policy.TenantId))
        {
            throw new InvalidOperationException("TenantId is required for Tenant scope.");
        }

        if (policy.ScopeType == SlaScopeType.SystemDefault && policy.AutoResumeAfterHours.HasValue)
        {
            throw new InvalidOperationException("AutoResumeAfterHours must be null for SystemDefault scope.");
        }

        if (policy.ScopeType == SlaScopeType.Tenant &&
            policy.AutoResumeAfterHours.HasValue &&
            policy.AutoResumeAfterHours.Value <= 0)
        {
            throw new InvalidOperationException("AutoResumeAfterHours must be greater than zero when provided.");
        }

        if (policy.MatchRank < 0)
        {
            throw new InvalidOperationException("MatchRank must be greater than or equal to zero.");
        }

        var duplicateKey = policy.Escalations
            .Where(x => x.IsActive)
            .GroupBy(x => new { x.Metric, x.TriggerPercent })
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicateKey is not null)
        {
            throw new InvalidOperationException("Duplicate escalation rules are not allowed for the same metric and trigger percent.");
        }

        foreach (var escalation in policy.Escalations)
        {
            if (escalation.TriggerPercent is < 1 or > 99)
            {
                throw new InvalidOperationException("Escalation trigger percent must be between 1 and 99.");
            }

            if (!escalation.IsActive)
            {
                continue;
            }

            var targets = escalation.Targets.Count > 0
                ? escalation.Targets
                : escalation.Recipients
                    .Select(x => new RecipientTarget
                    {
                        Type = RecipientTargetType.Email,
                        Value = x
                    })
                    .ToList();

            if (targets.Count == 0)
            {
                throw new InvalidOperationException("Active escalation rules must have at least one recipient.");
            }

            foreach (var target in targets.Where(x => x.Type == RecipientTargetType.Email))
            {
                if (!LooksLikeEmail(target.Value))
                {
                    throw new InvalidOperationException("Escalation recipients must contain valid email addresses.");
                }
            }
        }
    }

    private static bool LooksLikeEmail(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var email = value.Trim();
        var at = email.IndexOf('@');
        var dot = email.LastIndexOf('.');
        return at > 0 && dot > at + 1 && dot < email.Length - 1;
    }
}
