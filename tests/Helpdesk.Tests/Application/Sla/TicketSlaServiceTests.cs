using Helpdesk.Application.Events;
using Helpdesk.Application.Sla;
using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using NSubstitute;

namespace Helpdesk.Tests.Application.Sla;

public class TicketSlaServiceTests
{
    [Fact]
    public async Task PauseAsync_TenantPolicyWithAutoResume_SetsResumeAt()
    {
        var tickets = Substitute.For<IRepository<Ticket>>();
        var states = Substitute.For<ITicketSlaRepository>();
        var resolver = Substitute.For<ISlaPolicyResolver>();

        tickets.GetAsync("t1").Returns(new Incident { Id = "t1", OrganizationId = "tenant-1" });
        var state = new TicketSlaState { TicketId = "t1", Status = SlaStatus.InProgress };
        states.GetByTicketIdForUpdateAsync("t1").Returns(state);
        resolver.ResolveAsync("tenant-1", TicketType.Incident).Returns(new SlaPolicy
        {
            ScopeType = SlaScopeType.Tenant,
            TenantId = "tenant-1",
            AppliesTo = TicketType.Incident,
            AutoResumeAfterHours = 4,
            IsActive = true
        });

        var sut = new TicketSlaService(tickets, states, resolver);
        var before = DateTimeOffset.UtcNow;

        await sut.PauseAsync("t1", "u1", "Waiting");

        await states.Received(1).UpdateAsync(Arg.Is<TicketSlaState>(x =>
            x.Status == SlaStatus.Paused &&
            x.PausedByUserId == "u1" &&
            x.PauseReason == "Waiting" &&
            x.ResumeAt.HasValue &&
            x.ResumeAt.Value >= before.AddHours(4)));
    }

    [Fact]
    public async Task PauseAsync_SystemDefaultPolicy_DoesNotSetResumeAt()
    {
        var tickets = Substitute.For<IRepository<Ticket>>();
        var states = Substitute.For<ITicketSlaRepository>();
        var resolver = Substitute.For<ISlaPolicyResolver>();

        tickets.GetAsync("t1").Returns(new Request { Id = "t1", OrganizationId = "tenant-1" });
        var state = new TicketSlaState { TicketId = "t1", Status = SlaStatus.InProgress };
        states.GetByTicketIdForUpdateAsync("t1").Returns(state);
        resolver.ResolveAsync("tenant-1", TicketType.Request).Returns(new SlaPolicy
        {
            ScopeType = SlaScopeType.SystemDefault,
            AppliesTo = TicketType.Request,
            AutoResumeAfterHours = 4,
            IsActive = true
        });

        var sut = new TicketSlaService(tickets, states, resolver);

        await sut.PauseAsync("t1", "u1", "Waiting");

        await states.Received(1).UpdateAsync(Arg.Is<TicketSlaState>(x =>
            x.Status == SlaStatus.Paused &&
            x.ResumeAt == null));
    }

    [Fact]
    public async Task ResumeAsync_AddsPauseDurationAndClearsPauseFields()
    {
        var tickets = Substitute.For<IRepository<Ticket>>();
        var states = Substitute.For<ITicketSlaRepository>();
        var resolver = Substitute.For<ISlaPolicyResolver>();

        tickets.GetAsync("t1").Returns(new Change { Id = "t1", OrganizationId = "tenant-1" });
        var pausedAt = DateTimeOffset.UtcNow.AddMinutes(-30);
        var state = new TicketSlaState
        {
            TicketId = "t1",
            Status = SlaStatus.Paused,
            PausedAt = pausedAt,
            PauseReason = "Waiting",
            PausedByUserId = "u1",
            ResumeAt = DateTimeOffset.UtcNow.AddHours(1),
            AccumulatedPauseDuration = TimeSpan.FromMinutes(10)
        };
        states.GetByTicketIdForUpdateAsync("t1").Returns(state);

        var sut = new TicketSlaService(tickets, states, resolver);

        await sut.ResumeAsync("t1", "u1");

        await states.Received(1).UpdateAsync(Arg.Is<TicketSlaState>(x =>
            x.Status == SlaStatus.InProgress &&
            x.PausedAt == null &&
            x.PauseReason == null &&
            x.ResumeAt == null &&
            x.AccumulatedPauseDuration > TimeSpan.FromMinutes(10)));
    }

    [Fact]
    public async Task AutoResumeIfDueAsync_ResumesWhenResumeAtPassed()
    {
        var tickets = Substitute.For<IRepository<Ticket>>();
        var states = Substitute.For<ITicketSlaRepository>();
        var resolver = Substitute.For<ISlaPolicyResolver>();

        var state = new TicketSlaState
        {
            TicketId = "t1",
            Status = SlaStatus.Paused,
            PausedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            ResumeAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        };
        states.GetByTicketIdForUpdateAsync("t1").Returns(state);

        var sut = new TicketSlaService(tickets, states, resolver);

        await sut.AutoResumeIfDueAsync("t1");

        await states.Received(1).UpdateAsync(Arg.Is<TicketSlaState>(x =>
            x.Status == SlaStatus.InProgress &&
            x.PausedAt == null &&
            x.ResumeAt == null));
    }

    [Fact]
    public async Task PauseAsync_RaisesTicketSlaPausedDomainEvent()
    {
        var tickets = Substitute.For<IRepository<Ticket>>();
        var states = Substitute.For<ITicketSlaRepository>();
        var resolver = Substitute.For<ISlaPolicyResolver>();
        var domainEvents = Substitute.For<IDomainEventPublisher>();
        var correlation = Substitute.For<ICorrelationContext>();
        correlation.GetCorrelationId().Returns("corr-test");

        tickets.GetAsync("t1").Returns(new Incident { Id = "t1", OrganizationId = "tenant-1" });
        states.GetByTicketIdForUpdateAsync("t1").Returns(new TicketSlaState { TicketId = "t1", Status = SlaStatus.InProgress });

        var sut = new TicketSlaService(tickets, states, resolver, null, null, domainEvents, correlation);

        await sut.PauseAsync("t1", "u1", "Waiting");

        await domainEvents.Received(1).PublishAsync(
            Arg.Is<DomainEvent>(x => x.GetType() == typeof(TicketSlaPausedDomainEvent) && x.EntityId == "t1"),
            Arg.Any<CancellationToken>());
    }
}
