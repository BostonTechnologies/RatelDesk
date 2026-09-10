using Helpdesk.Application.Sla;
using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;

namespace Helpdesk.Tests.Application.Sla;

public class SlaPolicyValidatorTests
{
    private readonly SlaPolicyValidator _sut = new();

    [Fact]
    public void ValidateForSave_DuplicateMetricAndPercent_Throws()
    {
        var policy = CreateValidPolicy();
        policy.Escalations =
        [
            new SlaEscalationRule { Metric = SlaMetricType.Response, TriggerPercent = 50, Recipients = ["a@b.com"], IsActive = true },
            new SlaEscalationRule { Metric = SlaMetricType.Response, TriggerPercent = 50, Recipients = ["c@d.com"], IsActive = true }
        ];

        Assert.Throws<InvalidOperationException>(() => _sut.ValidateForSave(policy));
    }

    [Fact]
    public void ValidateForSave_ActiveRuleWithoutRecipients_Throws()
    {
        var policy = CreateValidPolicy();
        policy.Escalations =
        [
            new SlaEscalationRule { Metric = SlaMetricType.Response, TriggerPercent = 50, Recipients = [], IsActive = true }
        ];

        Assert.Throws<InvalidOperationException>(() => _sut.ValidateForSave(policy));
    }

    [Fact]
    public void ValidateForSave_SystemDefaultWithTenantId_Throws()
    {
        var policy = CreateValidPolicy();
        policy.ScopeType = SlaScopeType.SystemDefault;
        policy.TenantId = "tenant-1";

        Assert.Throws<InvalidOperationException>(() => _sut.ValidateForSave(policy));
    }

    [Fact]
    public void ValidateForSave_TenantScopeWithoutTenantId_Throws()
    {
        var policy = CreateValidPolicy();
        policy.ScopeType = SlaScopeType.Tenant;
        policy.TenantId = null;

        Assert.Throws<InvalidOperationException>(() => _sut.ValidateForSave(policy));
    }

    private static SlaPolicy CreateValidPolicy()
    {
        return new SlaPolicy
        {
            ScopeType = SlaScopeType.Tenant,
            TenantId = "tenant-1",
            AppliesTo = TicketType.Incident,
            Name = "Policy",
            ResponseTimeHours = 24,
            ResolutionTimeHours = 48,
            IsActive = true
        };
    }
}
