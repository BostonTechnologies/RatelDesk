using Helpdesk.Application.Services.AI;
using Helpdesk.Infrastructure.Changes;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.DTOs.Change;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Helpdesk.Tests.Application.Services;

public class ChangeReviewServiceTests
{
    [Fact]
    public void ValidateTemplate_ReturnsErrors_WhenRollbackAndStepsAreMissing()
    {
        using var ctx = CreateDbContext();
        var service = CreateService(ctx);

        var result = service.ValidateTemplate("Normal", new ChangeTemplateDto
        {
            ScopeOfChange = "Patch application nodes",
            AffectedSystems = ["portal-app"],
            BusinessJustification = "Security update",
            ImpactAssessment = "Brief restart",
            RiskAssessment = "Low"
        });

        Assert.False(result.IsComplete);
        Assert.Contains(result.Errors, x => x.Contains("Implementation steps", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, x => x.Contains("rollback", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateTemplate_ReturnsComplete_ForEmergencyTemplate_WithRequiredFields()
    {
        using var ctx = CreateDbContext();
        var service = CreateService(ctx);

        var result = service.ValidateTemplate("Emergency", new ChangeTemplateDto
        {
            ScopeOfChange = "Restore API availability",
            AffectedSystems = ["customer-api"],
            ImplementationSteps = ["Drain traffic", "Deploy hotfix", "Warm instances"],
            ValidationSteps = ["Run smoke tests", "Confirm error rate normal"],
            RollbackPlan = "Redeploy previous image and restore prior config.",
            EmergencyReason = "Current release is causing outages.",
            BusinessImpactIfNotImplemented = "Customers cannot place orders.",
            ImmediateRiskAssessment = "Temporary service instability during failover.",
            PostChangeValidation = "Observe health checks and error-rate dashboards for 15 minutes."
        });

        Assert.True(result.IsComplete);
        Assert.Empty(result.Errors);
    }

    private static ChangeReviewService CreateService(HelpdeskDbContext ctx)
    {
        return new ChangeReviewService(
            Substitute.For<IAiRuntime>(),
            Substitute.For<IAiPromptTemplateService>(),
            Substitute.For<IAiOperationAuditService>(),
            ctx,
            Substitute.For<ILogger<ChangeReviewService>>());
    }

    private static HelpdeskDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HelpdeskDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenant = Substitute.For<ITenantContext>();
        return new HelpdeskDbContext(options, tenant, new HttpContextAccessor());
    }
}
