using Helpdesk.Application.Events;
using Helpdesk.Application.RequestTasks;
using Helpdesk.Application.Services.Notifications;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using NSubstitute;

namespace Helpdesk.Tests.Application.RequestTasks;

public class RequestTaskStateServiceTests
{
    [Fact]
    public async Task EvaluateParentRequestState_AnyFailed_SetsOnHoldAndPublishesEvent()
    {
        var requests = new InMemoryRepository<Request>();
        var tasks = new InMemoryRepository<RequestTask>();
        var events = Substitute.For<IDomainEventPublisher>();
        var correlation = Substitute.For<ICorrelationContext>();
        correlation.GetCorrelationId().Returns("corr-task");

        await requests.CreateAsync(new Request { Id = "req-1", State = TicketState.New, OrganizationId = "tenant-1" });
        await tasks.CreateAsync(new RequestTask { Id = "task-1", RequestId = "req-1", Status = RequestTaskStatus.Failed });

        var sut = new RequestTaskStateService(requests, tasks, events, correlation);

        await sut.EvaluateParentRequestState("req-1");

        var request = await requests.GetAsync("req-1");
        Assert.NotNull(request);
        Assert.Equal(TicketState.OnHold, request.State);

        await events.Received(1).PublishAsync(
            Arg.Is<DomainEvent>(x => x is RequestStateChangedEvent && x.EntityId == "req-1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EvaluateParentRequestState_AllCompleted_SetsResolved()
    {
        var requests = new InMemoryRepository<Request>();
        var tasks = new InMemoryRepository<RequestTask>();
        var events = Substitute.For<IDomainEventPublisher>();
        var correlation = Substitute.For<ICorrelationContext>();

        await requests.CreateAsync(new Request { Id = "req-2", State = TicketState.New, OrganizationId = "tenant-1" });
        await tasks.CreateAsync(new RequestTask { Id = "task-2", RequestId = "req-2", Status = RequestTaskStatus.Completed });
        await tasks.CreateAsync(new RequestTask { Id = "task-3", RequestId = "req-2", Status = RequestTaskStatus.Completed });

        var sut = new RequestTaskStateService(requests, tasks, events, correlation);

        await sut.EvaluateParentRequestState("req-2");

        var request = await requests.GetAsync("req-2");
        Assert.NotNull(request);
        Assert.Equal(TicketState.Resolved, request.State);
        Assert.NotNull(request.ClosedAt);
    }

    [Fact]
    public async Task EvaluateParentRequestState_SelfServiceCompleted_SendsCompletionEmail()
    {
        var requests = new InMemoryRepository<Request>();
        var tasks = new InMemoryRepository<RequestTask>();
        var events = Substitute.For<IDomainEventPublisher>();
        var correlation = Substitute.For<ICorrelationContext>();
        var notifications = Substitute.For<ITicketNotificationService>();
        var request = new Request
        {
            Id = "req-3",
            State = TicketState.InProgress,
            OrganizationId = "tenant-1",
            RequestFormId = "form-1",
            RequesterEmail = "customer@example.com",
            TrackingId = "REQ-3"
        };

        await requests.CreateAsync(request);
        await tasks.CreateAsync(new RequestTask { Id = "task-4", RequestId = "req-3", Status = RequestTaskStatus.Completed });

        var sut = new RequestTaskStateService(requests, tasks, events, correlation, notifications);

        await sut.EvaluateParentRequestState("req-3");

        await notifications.Received(1).SendSelfServiceRequestCompletedAsync(
            Arg.Is<Request>(x => x.Id == "req-3" && x.State == TicketState.Resolved),
            "customer@example.com",
            "customer@example.com",
            Arg.Any<IEnumerable<string>?>(),
            Arg.Any<CancellationToken>());
        await notifications.DidNotReceive().SendTicketResolvedAsync(
            Arg.Any<Ticket>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IEnumerable<string>?>(),
            Arg.Any<CancellationToken>());
    }
}
