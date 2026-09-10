using Helpdesk.Application.Events;
using Helpdesk.Application.Sla;
using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Helpdesk.Tests.Application.Sla;

public class SlaEvaluationJobTests
{
    [Fact]
    public async Task RunAsync_ProcessesBatches_AndContinuesOnTicketFailure()
    {
        var query = Substitute.For<ITicketSlaQueryRepository>();
        var repo = Substitute.For<ITicketSlaRepository>();
        var clock = new SlaClockService();
        var evaluator = Substitute.For<ISlaEscalationEvaluator>();
        var logger = Substitute.For<ILogger<SlaEvaluationJob>>();

        query.GetBatchAsync(2, null, Arg.Any<CancellationToken>()).Returns(new List<TicketSlaBatchRow>
        {
            CreateRow("t1", TicketType.Incident),
            CreateRow("t2", TicketType.Request)
        });
        query.GetBatchAsync(2, "t2", Arg.Any<CancellationToken>()).Returns(new List<TicketSlaBatchRow>());

        repo.GetByTicketIdForUpdateAsync("t1").Returns(CreateRow("t1", TicketType.Incident).SlaState);
        repo.GetByTicketIdForUpdateAsync("t2").Returns<Task<TicketSlaState?>>(x => throw new InvalidOperationException("boom"));

        var sut = new SlaEvaluationJob(
            query,
            repo,
            clock,
            evaluator,
            Options.Create(new SlaEvaluationJobSettings { BatchSize = 2 }),
            logger);

        await sut.RunAsync(CancellationToken.None);

        await evaluator.Received(1).EvaluateAndNotifyAsync(Arg.Any<Ticket>(), Arg.Any<TicketSlaState>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_AutoResumes_WhenResumeAtPassed()
    {
        var query = Substitute.For<ITicketSlaQueryRepository>();
        var repo = Substitute.For<ITicketSlaRepository>();
        var clock = new SlaClockService();
        var evaluator = Substitute.For<ISlaEscalationEvaluator>();
        var logger = Substitute.For<ILogger<SlaEvaluationJob>>();

        var state = CreateRow("t1", TicketType.Incident).SlaState;
        state.Status = SlaStatus.Paused;
        state.PausedAt = DateTimeOffset.UtcNow.AddMinutes(-30);
        state.ResumeAt = DateTimeOffset.UtcNow.AddMinutes(-1);

        query.GetBatchAsync(1, null, Arg.Any<CancellationToken>()).Returns(new List<TicketSlaBatchRow> { CreateRow("t1", TicketType.Incident, state) });
        query.GetBatchAsync(1, "t1", Arg.Any<CancellationToken>()).Returns(new List<TicketSlaBatchRow>());
        repo.GetByTicketIdForUpdateAsync("t1").Returns(state);

        var sut = new SlaEvaluationJob(
            query,
            repo,
            clock,
            evaluator,
            Options.Create(new SlaEvaluationJobSettings { BatchSize = 1 }),
            logger);

        await sut.RunAsync(CancellationToken.None);

        await repo.Received(1).UpdateAsync(Arg.Is<TicketSlaState>(x =>
            x.Status == SlaStatus.InProgress &&
            x.ResumeAt == null &&
            x.PausedAt == null));
    }

    [Fact]
    public async Task RunAsync_SetsBreachFlags_WhenBreached()
    {
        var query = Substitute.For<ITicketSlaQueryRepository>();
        var repo = Substitute.For<ITicketSlaRepository>();
        var clock = new SlaClockService();
        var evaluator = Substitute.For<ISlaEscalationEvaluator>();
        var logger = Substitute.For<ILogger<SlaEvaluationJob>>();

        var start = DateTimeOffset.UtcNow.AddHours(-10);
        var state = new TicketSlaState
        {
            TicketId = "t1",
            StartedAt = start,
            ResponseDueAt = start.AddHours(1),
            ResolutionDueAt = start.AddHours(2),
            Status = SlaStatus.InProgress,
            ResponseBreached = false,
            ResolutionBreached = false
        };

        query.GetBatchAsync(1, null, Arg.Any<CancellationToken>()).Returns(new List<TicketSlaBatchRow> { CreateRow("t1", TicketType.Incident, state) });
        query.GetBatchAsync(1, "t1", Arg.Any<CancellationToken>()).Returns(new List<TicketSlaBatchRow>());
        repo.GetByTicketIdForUpdateAsync("t1").Returns(state);

        var sut = new SlaEvaluationJob(
            query,
            repo,
            clock,
            evaluator,
            Options.Create(new SlaEvaluationJobSettings { BatchSize = 1 }),
            logger);

        await sut.RunAsync(CancellationToken.None);

        await repo.Received(1).UpdateAsync(Arg.Is<TicketSlaState>(x =>
            x.ResponseBreached &&
            x.ResolutionBreached &&
            x.Status == SlaStatus.Breached));
    }

    [Fact]
    public async Task RunAsync_InvokesEscalationEvaluator()
    {
        var query = Substitute.For<ITicketSlaQueryRepository>();
        var repo = Substitute.For<ITicketSlaRepository>();
        var clock = new SlaClockService();
        var evaluator = Substitute.For<ISlaEscalationEvaluator>();
        var logger = Substitute.For<ILogger<SlaEvaluationJob>>();

        var state = CreateRow("t1", TicketType.Change).SlaState;
        query.GetBatchAsync(1, null, Arg.Any<CancellationToken>()).Returns(new List<TicketSlaBatchRow> { CreateRow("t1", TicketType.Change, state) });
        query.GetBatchAsync(1, "t1", Arg.Any<CancellationToken>()).Returns(new List<TicketSlaBatchRow>());
        repo.GetByTicketIdForUpdateAsync("t1").Returns(state);

        var sut = new SlaEvaluationJob(
            query,
            repo,
            clock,
            evaluator,
            Options.Create(new SlaEvaluationJobSettings { BatchSize = 1 }),
            logger);

        await sut.RunAsync(CancellationToken.None);

        await evaluator.Received(1).EvaluateAndNotifyAsync(Arg.Any<Ticket>(), Arg.Any<TicketSlaState>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_RaisesStartedAndCompletedDomainEvents()
    {
        var query = Substitute.For<ITicketSlaQueryRepository>();
        var repo = Substitute.For<ITicketSlaRepository>();
        var clock = new SlaClockService();
        var evaluator = Substitute.For<ISlaEscalationEvaluator>();
        var logger = Substitute.For<ILogger<SlaEvaluationJob>>();
        var domainEvents = Substitute.For<IDomainEventPublisher>();
        var correlation = Substitute.For<ICorrelationContext>();
        correlation.GetCorrelationId().Returns("corr-job");

        var state = CreateRow("t1", TicketType.Incident).SlaState;
        query.GetBatchAsync(1, null, Arg.Any<CancellationToken>()).Returns(new List<TicketSlaBatchRow> { CreateRow("t1", TicketType.Incident, state) });
        query.GetBatchAsync(1, "t1", Arg.Any<CancellationToken>()).Returns(new List<TicketSlaBatchRow>());
        repo.GetByTicketIdForUpdateAsync("t1").Returns(state);

        var sut = new SlaEvaluationJob(
            query,
            repo,
            clock,
            evaluator,
            Options.Create(new SlaEvaluationJobSettings { BatchSize = 1 }),
            logger,
            null,
            null,
            domainEvents,
            correlation);

        await sut.RunAsync(CancellationToken.None);

        await domainEvents.Received().PublishAsync(
            Arg.Is<DomainEvent>(x => x is SlaEvaluationJobStartedDomainEvent),
            Arg.Any<CancellationToken>());
        await domainEvents.Received().PublishAsync(
            Arg.Is<DomainEvent>(x => x is SlaEvaluationJobCompletedDomainEvent),
            Arg.Any<CancellationToken>());
    }

    private static TicketSlaBatchRow CreateRow(string ticketId, TicketType ticketType, TicketSlaState? state = null)
    {
        var start = DateTimeOffset.UtcNow.AddHours(-1);
        state ??= new TicketSlaState
        {
            TicketId = ticketId,
            StartedAt = start,
            ResponseDueAt = start.AddHours(4),
            ResolutionDueAt = start.AddHours(8),
            Status = SlaStatus.InProgress
        };

        return new TicketSlaBatchRow(
            Cursor: ticketId,
            TicketId: ticketId,
            TenantId: "tenant-1",
            TicketType: ticketType,
            TicketNumber: ticketId,
            Title: "Ticket",
            IsClosed: false,
            SlaState: state);
    }
}
