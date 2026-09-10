using Helpdesk.Application.Sla;
using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;
using NSubstitute;

namespace Helpdesk.Tests.Application.Sla;

public class SlaPolicyResolverTests
{
    [Fact]
    public async Task ResolveAsync_TenantPolicyExists_ReturnsTenantPolicy()
    {
        var repo = Substitute.For<ISlaPolicyRepository>();
        var expected = new SlaPolicy
        {
            Id = "sla-tenant-1",
            ScopeType = SlaScopeType.Tenant,
            TenantId = "tenant-1",
            AppliesTo = TicketType.Incident,
            IsActive = true
        };

        repo.GetActiveTenantPolicyAsync("tenant-1", TicketType.Incident).Returns(expected);
        var resolver = new SlaPolicyResolver(repo);

        var result = await resolver.ResolveAsync("tenant-1", TicketType.Incident);

        Assert.Equal(expected, result);
        await repo.DidNotReceive().GetActiveSystemPolicyAsync(Arg.Any<TicketType>());
    }

    [Fact]
    public async Task ResolveAsync_TenantPolicyMissing_ReturnsSystemDefault()
    {
        var repo = Substitute.For<ISlaPolicyRepository>();
        var fallback = new SlaPolicy
        {
            Id = "sla-system-1",
            ScopeType = SlaScopeType.SystemDefault,
            AppliesTo = TicketType.Request,
            IsActive = true
        };

        repo.GetActiveTenantPolicyAsync("tenant-1", TicketType.Request).Returns((SlaPolicy?)null);
        repo.GetActiveSystemPolicyAsync(TicketType.Request).Returns(fallback);
        var resolver = new SlaPolicyResolver(repo);

        var result = await resolver.ResolveAsync("tenant-1", TicketType.Request);

        Assert.Equal(fallback, result);
    }

    [Fact]
    public async Task ResolveAsync_NeitherExists_ReturnsNull()
    {
        var repo = Substitute.For<ISlaPolicyRepository>();
        repo.GetActiveTenantPolicyAsync("tenant-1", TicketType.Change).Returns((SlaPolicy?)null);
        repo.GetActiveSystemPolicyAsync(TicketType.Change).Returns((SlaPolicy?)null);
        var resolver = new SlaPolicyResolver(repo);

        var result = await resolver.ResolveAsync("tenant-1", TicketType.Change);

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveAsync_SystemDefaultInactive_ReturnsNull()
    {
        var repo = Substitute.For<ISlaPolicyRepository>();
        repo.GetActiveTenantPolicyAsync("tenant-1", TicketType.Incident).Returns((SlaPolicy?)null);
        repo.GetActiveSystemPolicyAsync(TicketType.Incident).Returns((SlaPolicy?)null);
        var resolver = new SlaPolicyResolver(repo);

        var result = await resolver.ResolveAsync("tenant-1", TicketType.Incident);

        Assert.Null(result);
    }
}
