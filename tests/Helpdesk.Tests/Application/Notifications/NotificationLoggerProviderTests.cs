using Helpdesk.Application.Notifications;
using Helpdesk.Infrastructure.Logging;
using Helpdesk.Shared.DTOs.Notification;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Helpdesk.Tests.Application.Notifications;

public class NotificationLoggerProviderTests
{
    [Fact]
    public async Task Warning_CreatesNotification()
    {
        var (provider, notificationService) = CreateProvider();
        var logger = provider.CreateLogger("Helpdesk.Infrastructure.Email.GraphEmailService");

        logger.LogWarning("Graph API credentials are missing or invalid");

        await WaitForCallAsync(async () =>
        {
            await notificationService.Received(1).CreateNotificationAsync(
                Arg.Is<CreateNotificationRequest>(r =>
                    r.Severity == NotificationSeverity.Warning &&
                    r.Category == "System" &&
                    r.Source != null &&
                    r.Message.Contains("Graph API credentials", StringComparison.Ordinal)),
                Arg.Any<CancellationToken>());
        });

        provider.Dispose();
    }

    [Fact]
    public async Task Error_CreatesNotification()
    {
        var (provider, notificationService) = CreateProvider();
        var logger = provider.CreateLogger("Helpdesk.API.Middleware.ExceptionNotificationMiddleware");

        logger.LogError("Unhandled failure while processing request");

        await WaitForCallAsync(async () =>
        {
            await notificationService.Received(1).CreateNotificationAsync(
                Arg.Is<CreateNotificationRequest>(r => r.Severity == NotificationSeverity.Error),
                Arg.Any<CancellationToken>());
        });

        provider.Dispose();
    }

    [Fact]
    public async Task Info_DoesNotCreateNotification()
    {
        var (provider, notificationService) = CreateProvider();
        var logger = provider.CreateLogger("Helpdesk.API.SomeCategory");

        logger.LogInformation("This is informational and should be ignored");
        await Task.Delay(150);

        await notificationService.DidNotReceive().CreateNotificationAsync(Arg.Any<CreateNotificationRequest>(), Arg.Any<CancellationToken>());

        provider.Dispose();
    }

    [Fact]
    public async Task OptionalIncidentPostProcessWarning_DoesNotCreateNotification()
    {
        var (provider, notificationService) = CreateProvider();
        var logger = provider.CreateLogger("IncidentPostProcess");

        logger.LogWarning("Optional incident post-processing skipped because AI providers are unavailable. TicketId={TicketId}", "t1");
        await Task.Delay(150);

        await notificationService.DidNotReceive().CreateNotificationAsync(Arg.Any<CreateNotificationRequest>(), Arg.Any<CancellationToken>());

        provider.Dispose();
    }

    private static (NotificationLoggerProvider provider, INotificationService notificationService) CreateProvider()
    {
        var notificationService = Substitute.For<INotificationService>();
        notificationService.CreateNotificationAsync(Arg.Any<CreateNotificationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(INotificationService)).Returns(notificationService);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        var provider = new NotificationLoggerProvider(
            scopeFactory,
            "Test",
            "Helpdesk.API",
            "Helpdesk.API");

        return (provider, notificationService);
    }

    private static async Task WaitForCallAsync(Func<Task> assertion)
    {
        var timeout = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < timeout)
        {
            try
            {
                await assertion();
                return;
            }
            catch
            {
                await Task.Delay(50);
            }
        }

        await assertion();
    }
}
