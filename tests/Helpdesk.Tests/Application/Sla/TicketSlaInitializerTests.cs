using Helpdesk.Application.Sla;
using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;
using NSubstitute;

namespace Helpdesk.Tests.Application.Sla;

public class TicketSlaInitializerTests
{
    [Fact]
    public async Task InitializeAsync_WithResolvedPolicy_CreatesSlaState()
    {
        var resolver = Substitute.For<ISlaPolicyResolver>();
        var repository = Substitute.For<ITicketSlaRepository>();
        var policy = new SlaPolicy
        {
            ScopeType = SlaScopeType.Tenant,
            TenantId = "tenant-1",
            AppliesTo = TicketType.Incident,
            ResponseTimeHours = 2,
            ResolutionTimeHours = 8,
            IsActive = true
        };
        resolver.ResolveAsync("tenant-1", TicketType.Incident).Returns(policy);

        var ticket = new Incident
        {
            Id = "inc-1",
            OrganizationId = "tenant-1"
        };
        var sut = new TicketSlaInitializer(resolver, repository);

        var before = DateTimeOffset.UtcNow;
        await sut.InitializeAsync(ticket);
        var after = DateTimeOffset.UtcNow;

        await repository.Received(1).AddAsync(Arg.Is<TicketSlaState>(x =>
            x.TicketId == "inc-1" &&
            x.Status == SlaStatus.InProgress &&
            !x.ResponseBreached &&
            !x.ResolutionBreached &&
            x.StartedAt >= before &&
            x.StartedAt <= after &&
            x.ResponseDueAt >= before.AddHours(2) &&
            x.ResponseDueAt <= after.AddHours(2) &&
            x.ResolutionDueAt >= before.AddHours(8) &&
            x.ResolutionDueAt <= after.AddHours(8)));
    }

    [Fact]
    public async Task InitializeAsync_UsesResolvedSystemFallbackPolicy_WhenResolverReturnsFallback()
    {
        var resolver = Substitute.For<ISlaPolicyResolver>();
        var repository = Substitute.For<ITicketSlaRepository>();
        resolver.ResolveAsync("tenant-1", TicketType.Request).Returns(new SlaPolicy
        {
            ScopeType = SlaScopeType.SystemDefault,
            TenantId = null,
            AppliesTo = TicketType.Request,
            ResponseTimeHours = 24,
            ResolutionTimeHours = 48,
            IsActive = true
        });

        var ticket = new Request
        {
            Id = "req-1",
            OrganizationId = "tenant-1"
        };
        var sut = new TicketSlaInitializer(resolver, repository);

        await sut.InitializeAsync(ticket);

        await repository.Received(1).AddAsync(Arg.Is<TicketSlaState>(x =>
            x.TicketId == "req-1" &&
            x.ResponseDueAt > x.StartedAt &&
            x.ResolutionDueAt > x.StartedAt));
    }

    [Fact]
    public async Task InitializeAsync_WhenNoPolicy_DoesNotCreateSlaState()
    {
        var resolver = Substitute.For<ISlaPolicyResolver>();
        var repository = Substitute.For<ITicketSlaRepository>();
        resolver.ResolveAsync("tenant-1", TicketType.Change).Returns((SlaPolicy?)null);

        var ticket = new Change
        {
            Id = "chg-1",
            OrganizationId = "tenant-1"
        };
        var sut = new TicketSlaInitializer(resolver, repository);

        await sut.InitializeAsync(ticket);

        await repository.DidNotReceive().AddAsync(Arg.Any<TicketSlaState>());
    }
}
