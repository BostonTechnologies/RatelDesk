using System.Diagnostics.Metrics;
using Helpdesk.Application.Notifications;
using Helpdesk.Application.Observability;
using Helpdesk.Shared.DTOs.Notification;
using NSubstitute;

namespace Helpdesk.Tests.Application.Notifications;

public class NotificationServiceTests
{
    [Fact]
    public async Task CreateNotificationAsync_PersistsNotification()
    {
        var repo = Substitute.For<INotificationRepository>();
        repo.CreateNotificationAsync(Arg.Any<CreateNotificationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new NotificationDto { Id = Guid.NewGuid(), IsRead = false });
        var service = new NotificationService(repo);

        var request = new CreateNotificationRequest
        {
            UserId = "user-1",
            Title = "Integration failure",
            Message = "Zabbix ingestion failed.",
            Severity = NotificationSeverity.Error,
            Category = "Integration",
            Source = "Zabbix"
        };

        await service.CreateNotificationAsync(request, CancellationToken.None);

        await repo.Received(1).CreateNotificationAsync(
            Arg.Is<CreateNotificationRequest>(x =>
                x.UserId == "user-1" &&
                x.Title == "Integration failure" &&
                x.Severity == NotificationSeverity.Error),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateNotificationAsync_RecordsNotificationMetric()
    {
        var measurements = new List<long>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == HelpdeskTelemetry.MeterName &&
                instrument.Name == "helpdesk_notifications_created_total")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == "severity" &&
                    string.Equals(tag.Value?.ToString(), NotificationSeverity.Error.ToString(), StringComparison.Ordinal))
                {
                    measurements.Add(measurement);
                }
            }
        });
        listener.Start();

        var repo = Substitute.For<INotificationRepository>();
        repo.CreateNotificationAsync(Arg.Any<CreateNotificationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new NotificationDto { Id = Guid.NewGuid(), IsRead = false });
        var service = new NotificationService(repo);

        await service.CreateNotificationAsync(new CreateNotificationRequest
        {
            Title = "Integration failure",
            Message = "Zabbix ingestion failed.",
            Severity = NotificationSeverity.Error,
            Category = "Integration",
            Source = "Zabbix",
            TenantId = "tenant-1",
            Reference = "INC-1",
            CorrelationId = "corr-1"
        }, CancellationToken.None);

        Assert.Contains(1, measurements);
    }

    [Fact]
    public async Task GetNotificationsForUserAsync_ReturnsPagedNotifications()
    {
        var repo = Substitute.For<INotificationRepository>();
        repo.GetNotificationsForUserAsync("user-1", 10, 10, null, null, null, null, null, null, null, null, Arg.Any<CancellationToken>())
            .Returns(new List<NotificationDto>
            {
                new() { Title = "N1", CreatedUtc = DateTime.UtcNow },
                new() { Title = "N2", CreatedUtc = DateTime.UtcNow.AddMinutes(-1) }
            });
        repo.CountNotificationsForUserAsync("user-1", null, null, null, null, null, null, Arg.Any<CancellationToken>())
            .Returns(2);

        var service = new NotificationService(repo);

        var result = await service.GetNotificationsForUserAsync("user-1", page: 2, pageSize: 10, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal("N1", result[0].Title);
        await repo.Received(1).GetNotificationsForUserAsync("user-1", 10, 10, null, null, null, null, null, null, null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetNotificationsPageForUserAsync_PassesCreatedRangeToRepository()
    {
        var repo = Substitute.For<INotificationRepository>();
        var from = new DateTimeOffset(2026, 5, 20, 8, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 5, 20, 10, 0, 0, TimeSpan.Zero);
        repo.GetNotificationsForUserAsync(
                "user-1",
                0,
                10,
                null,
                null,
                null,
                null,
                from,
                to,
                "created",
                "desc",
                Arg.Any<CancellationToken>())
            .Returns(new List<NotificationDto>
            {
                new() { Title = "Inside", CreatedUtc = from.UtcDateTime.AddMinutes(30) }
            });
        repo.CountNotificationsForUserAsync("user-1", null, null, null, null, from, to, Arg.Any<CancellationToken>())
            .Returns(1);

        var service = new NotificationService(repo);

        var result = await service.GetNotificationsPageForUserAsync(
            "user-1",
            page: 1,
            pageSize: 10,
            searchTerm: null,
            severity: null,
            source: null,
            category: null,
            from: from,
            to: to,
            sortBy: "created",
            sortDir: "desc",
            CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(1, result.TotalCount);
        await repo.Received(1).GetNotificationsForUserAsync("user-1", 0, 10, null, null, null, null, from, to, "created", "desc", Arg.Any<CancellationToken>());
        await repo.Received(1).CountNotificationsForUserAsync("user-1", null, null, null, null, from, to, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MarkNotificationReadAsync_Throws_WhenNotificationNotAccessible()
    {
        var repo = Substitute.For<INotificationRepository>();
        repo.MarkNotificationReadAsync(Arg.Any<Guid>(), "user-1", Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var service = new NotificationService(repo);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.MarkNotificationReadAsync(Guid.NewGuid(), "user-1", CancellationToken.None));
    }

    [Fact]
    public async Task GetNotificationSummaryAsync_ReturnsCounts()
    {
        var repo = Substitute.For<INotificationRepository>();
        repo.GetNotificationSummaryAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(new NotificationSummaryDto { TotalCount = 12, UnreadCount = 5 });

        var service = new NotificationService(repo);

        var summary = await service.GetNotificationSummaryAsync("user-1", CancellationToken.None);

        Assert.Equal(12, summary.TotalCount);
        Assert.Equal(5, summary.UnreadCount);
    }

    [Fact]
    public async Task GetDomainEventTimelineAsync_ReturnsOrderedItems()
    {
        var repo = Substitute.For<INotificationRepository>();
        repo.GetDomainEventTimelineAsync("user-1", "INC-ABC", null, null, 200, Arg.Any<CancellationToken>())
            .Returns(new List<NotificationDto>
            {
                new() { Title = "TicketCreated", CreatedUtc = DateTime.UtcNow.AddMinutes(-1) },
                new() { Title = "EmailSent", CreatedUtc = DateTime.UtcNow }
            });

        var service = new NotificationService(repo);

        var result = await service.GetDomainEventTimelineAsync("user-1", "INC-ABC", null, null, 200, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal("TicketCreated", result[0].Title);
        await repo.Received(1).GetDomainEventTimelineAsync("user-1", "INC-ABC", null, null, 200, Arg.Any<CancellationToken>());
    }
}
