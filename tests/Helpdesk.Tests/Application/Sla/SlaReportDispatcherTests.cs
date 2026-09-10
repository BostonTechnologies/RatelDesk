using Helpdesk.Application.Sla;
using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Helpdesk.Tests.Application.Sla;

public class SlaReportDispatcherTests
{
    [Fact]
    public async Task RunAsync_DoesNotResend_WhenAlreadySentForPeriod()
    {
        var subscriptions = Substitute.For<IRepository<SlaReportSubscription>>();
        subscriptions.GetAllAsync().Returns(new List<SlaReportSubscription>
        {
            new()
            {
                Id = "sub-1",
                TenantId = "tenant-1",
                IsActive = true,
                Frequency = ReportFrequency.Daily,
                SendTimeLocal = DateTimeOffset.UtcNow.TimeOfDay,
                TimeZoneId = "UTC",
                Targets = new List<RecipientTarget> { new() { Type = RecipientTargetType.Email, Value = "a@b.com" } }
            }
        });

        var reportGenerator = Substitute.For<ISlaReportGenerator>();
        reportGenerator.GenerateAsync(Arg.Any<Helpdesk.Shared.DTOs.Sla.SlaComplianceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new GeneratedReport { Subject = "s", HtmlBody = "h", CsvBytes = new byte[] { 1 } });

        var recipientResolver = Substitute.For<IRecipientResolver>();
        recipientResolver.ResolveEmailsAsync("tenant-1", Arg.Any<List<RecipientTarget>>(), Arg.Any<CancellationToken>())
            .Returns(new List<string> { "a@b.com" });

        var sendEvents = Substitute.For<ISlaReportSendEventRepository>();
        sendEvents.GetOrCreateAsync(Arg.Any<SlaReportSendEvent>(), Arg.Any<CancellationToken>())
            .Returns((new SlaReportSendEvent { Id = "e1", Status = ReportSendStatus.Sent }, false));

        var emailSender = Substitute.For<IEmailSender>();
        var logger = Substitute.For<ILogger<SlaReportDispatcher>>();

        var sut = new SlaReportDispatcher(subscriptions, reportGenerator, recipientResolver, sendEvents, emailSender, logger);

        await sut.RunAsync();

        await emailSender.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }
}
