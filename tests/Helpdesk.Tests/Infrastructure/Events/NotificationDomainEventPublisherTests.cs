using Helpdesk.Application.Events;
using Helpdesk.Application.Notifications;
using Helpdesk.Infrastructure.Events;
using Helpdesk.Shared.DTOs.Notification;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Helpdesk.Tests.Infrastructure.Events;

public class NotificationDomainEventPublisherTests
{
    [Fact]
    public async Task PublishAsync_SlaEvent_UsesDomainEventSlaCategory()
    {
        var notifications = Substitute.For<INotificationService>();
        var logger = Substitute.For<ILogger<NotificationDomainEventPublisher>>();
        var sut = new NotificationDomainEventPublisher(notifications, logger);

        var evt = new TicketSlaPausedDomainEvent(
            TicketId: "t1",
            TenantId: "tenant-1",
            Metric: null,
            Status: Helpdesk.Shared.Enums.SlaStatus.Paused,
            Timestamp: DateTimeOffset.UtcNow,
            TriggerSource: SlaTriggerSources.Manual,
            CorrelationId: "corr-test");

        await sut.PublishAsync(evt, CancellationToken.None);

        await notifications.Received(1).CreateNotificationAsync(
            Arg.Is<CreateNotificationRequest>(x =>
                x.Category == "DomainEvent.SLA" &&
                x.Source == "SLA" &&
                x.Title == nameof(TicketSlaPausedDomainEvent)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_HangfireEvent_UsesDomainEventHangfireCategory()
    {
        var notifications = Substitute.For<INotificationService>();
        var logger = Substitute.For<ILogger<NotificationDomainEventPublisher>>();
        var sut = new NotificationDomainEventPublisher(notifications, logger);

        var started = DateTimeOffset.UtcNow;
        var evt = new SlaEvaluationJobStartedDomainEvent(
            JobId: "job-1",
            StartedAt: started,
            DurationMs: 0,
            TicketsProcessed: 0,
            EscalationsSent: 0,
            ErrorsCount: 0,
            CorrelationId: "corr-job");

        await sut.PublishAsync(evt, CancellationToken.None);

        await notifications.Received(1).CreateNotificationAsync(
            Arg.Is<CreateNotificationRequest>(x =>
                x.Category == "DomainEvent.Hangfire" &&
                x.Source == "Hangfire" &&
                x.Title == nameof(SlaEvaluationJobStartedDomainEvent)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_RequestTaskEvent_UsesDomainEventRequestTaskCategory()
    {
        var notifications = Substitute.For<INotificationService>();
        var logger = Substitute.For<ILogger<NotificationDomainEventPublisher>>();
        var sut = new NotificationDomainEventPublisher(notifications, logger);

        var evt = new RequestTaskCompletedEvent(
            TaskId: "task-1",
            RequestId: "req-1",
            TenantId: "tenant-1",
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: "corr-task");

        await sut.PublishAsync(evt, CancellationToken.None);

        await notifications.Received(1).CreateNotificationAsync(
            Arg.Is<CreateNotificationRequest>(x =>
                x.Category == "DomainEvent.RequestTask" &&
                x.Source == "RequestTask" &&
                x.Title == RequestTaskDomainEventTypes.Completed),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_RequestFormEvent_UsesDomainEventRequestFormCategory()
    {
        var notifications = Substitute.For<INotificationService>();
        var logger = Substitute.For<ILogger<NotificationDomainEventPublisher>>();
        var sut = new NotificationDomainEventPublisher(notifications, logger);

        var evt = new RequestFormUpdatedDomainEvent(
            RequestFormId: "form-1",
            ServiceId: "svc-1",
            Title: "Onboarding",
            TaskCount: 3,
            TenantId: "tenant-1",
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: "corr-form");

        await sut.PublishAsync(evt, CancellationToken.None);

        await notifications.Received(1).CreateNotificationAsync(
            Arg.Is<CreateNotificationRequest>(x =>
                x.Category == "DomainEvent.RequestForm" &&
                x.Source == "RequestForm" &&
                x.Title == RequestFormDomainEventTypes.Updated),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_SelfServiceEvent_UsesDomainEventSelfServiceCategory()
    {
        var notifications = Substitute.For<INotificationService>();
        var logger = Substitute.For<ILogger<NotificationDomainEventPublisher>>();
        var sut = new NotificationDomainEventPublisher(notifications, logger);

        var evt = new SelfServiceRequestSubmittedEvent(
            RequestId: "req-1",
            RequestFormId: "form-1",
            ServiceId: "svc-1",
            TrackingId: "REQ-1001",
            SubmittedByUserId: "user-1",
            TenantId: "tenant-1",
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: "corr-self");

        await sut.PublishAsync(evt, CancellationToken.None);

        await notifications.Received(1).CreateNotificationAsync(
            Arg.Is<CreateNotificationRequest>(x =>
                x.Category == "DomainEvent.SelfService" &&
                x.Source == "SelfService" &&
                x.Title == SelfServiceDomainEventTypes.RequestSubmitted),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_TaskDashboardEvent_UsesDomainEventTaskDashboardCategory()
    {
        var notifications = Substitute.For<INotificationService>();
        var logger = Substitute.For<ILogger<NotificationDomainEventPublisher>>();
        var sut = new NotificationDomainEventPublisher(notifications, logger);

        var evt = new TasksDashboardViewedEvent(
            UserId: "tech-1",
            AssignedToMe: true,
            AssignedToId: null,
            Status: "InProgress",
            Type: null,
            RequestId: null,
            ServiceId: null,
            Query: "mail",
            ResultCount: 10,
            TenantId: "tenant-1",
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: "corr-dashboard");

        await sut.PublishAsync(evt, CancellationToken.None);

        await notifications.Received(1).CreateNotificationAsync(
            Arg.Is<CreateNotificationRequest>(x =>
                x.Category == "DomainEvent.TaskDashboard" &&
                x.Source == "TaskDashboard" &&
                x.Title == TaskDashboardDomainEventTypes.Viewed),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_OrchestrationEvent_UsesDomainEventOrchestrationCategory()
    {
        var notifications = Substitute.For<INotificationService>();
        var logger = Substitute.For<ILogger<NotificationDomainEventPublisher>>();
        var sut = new NotificationDomainEventPublisher(notifications, logger);

        var evt = new OrchestrationSettingsUpdatedEvent(
            Enabled: true,
            BaseUrl: "https://orchestration.local",
            Audience: "orchestrator.api",
            TokenEndpoint: "https://orchestration.local/connect/token",
            TenantId: "tenant-1",
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: "corr-orchestration");

        await sut.PublishAsync(evt, CancellationToken.None);

        await notifications.Received(1).CreateNotificationAsync(
            Arg.Is<CreateNotificationRequest>(x =>
                x.Category == "DomainEvent.Orchestration" &&
                x.Source == "Orchestration" &&
                x.Title == OrchestrationDomainEventTypes.OrchestrationSettingsUpdated),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_OrchestrationCallbackRejected_UsesWarningSeverity()
    {
        var notifications = Substitute.For<INotificationService>();
        var logger = Substitute.For<ILogger<NotificationDomainEventPublisher>>();
        var sut = new NotificationDomainEventPublisher(notifications, logger);

        var evt = new OrchestrationCallbackRejectedEvent(
            ReasonCode: "IdentifierMismatch",
            CallerClientId: "orchestration.api",
            TenantId: "tenant-1",
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: "corr-reject");

        await sut.PublishAsync(evt, CancellationToken.None);

        await notifications.Received(1).CreateNotificationAsync(
            Arg.Is<CreateNotificationRequest>(x =>
                x.Category == "DomainEvent.Orchestration" &&
                x.Severity == NotificationSeverity.Warning &&
                x.Title == OrchestrationDomainEventTypes.OrchestrationCallbackRejected),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_WhenNotificationFails_DoesNotThrow()
    {
        var notifications = Substitute.For<INotificationService>();
        notifications.CreateNotificationAsync(Arg.Any<CreateNotificationRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("store failed"));
        var logger = Substitute.For<ILogger<NotificationDomainEventPublisher>>();
        var sut = new NotificationDomainEventPublisher(notifications, logger);

        var evt = new TicketSlaPausedDomainEvent(
            TicketId: "t1",
            TenantId: "tenant-1",
            Metric: null,
            Status: Helpdesk.Shared.Enums.SlaStatus.Paused,
            Timestamp: DateTimeOffset.UtcNow,
            TriggerSource: SlaTriggerSources.Manual,
            CorrelationId: "corr-test");

        await sut.PublishAsync(evt, CancellationToken.None);
    }
}
