using Helpdesk.Application.Events;
using Helpdesk.Application.Timeline;
using Helpdesk.Infrastructure.Configuration;
using Helpdesk.Infrastructure.Services;
using Helpdesk.Shared.DTOs.Worklog;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph.Users.Item.SendMail;
using NSubstitute;
using Xunit;

namespace Helpdesk.Tests.Infrastructure.Services;

public class GraphEmailServiceTests
{
    private const string MailboxAddress = "helpdesk@example.com";

    [Fact]
    public async Task SendEmailAsync_SelfOnly_DoesNotCallGraph()
    {
        var graphCallCount = 0;
        var service = CreateService((_, _, _) =>
        {
            graphCallCount++;
            return Task.CompletedTask;
        });

        var result = await service.SendEmailAsync(
            [" HELPDESK@EXAMPLE.COM "],
            "Loop notification",
            "<p>Do not send</p>",
            [MailboxAddress],
            suppressTimeline: true);

        Assert.False(result);
        Assert.Equal(0, graphCallCount);
    }

    [Fact]
    public async Task SendEmailAsync_MixedRecipients_RemovesSelfAndDeliversExternalRecipients()
    {
        string? senderMailbox = null;
        SendMailPostRequestBody? capturedRequest = null;
        var service = CreateService((mailbox, request, _) =>
        {
            senderMailbox = mailbox;
            capturedRequest = request;
            return Task.CompletedTask;
        });

        var result = await service.SendEmailAsync(
            [MailboxAddress, " customer@example.com ", "CUSTOMER@example.com"],
            "Ticket update",
            "<p>External delivery</p>",
            [" HELPDESK@EXAMPLE.COM ", "copy@example.com"],
            suppressTimeline: true);

        Assert.True(result);
        Assert.Equal(MailboxAddress, senderMailbox);
        Assert.NotNull(capturedRequest?.Message);
        var toRecipient = Assert.Single(capturedRequest.Message.ToRecipients!);
        var ccRecipient = Assert.Single(capturedRequest.Message.CcRecipients!);
        Assert.Equal("customer@example.com", toRecipient.EmailAddress?.Address);
        Assert.Equal("copy@example.com", ccRecipient.EmailAddress?.Address);
    }

    private static GraphEmailService CreateService(
        Func<string, SendMailPostRequestBody, CancellationToken, Task> sendMailAsync)
    {
        var domainEvents = Substitute.For<IDomainEventPublisher>();
        domainEvents
            .PublishAsync(Arg.Any<DomainEvent>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var emailSettingsProvider = Substitute.For<IEmailSettingsProvider>();
        emailSettingsProvider
            .GetAllEnabledAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<EmailInboxSettings>()));

        return new GraphEmailService(
            Options.Create(new ExchangeEmailOptions
            {
                Enabled = true,
                TenantId = Guid.NewGuid().ToString(),
                ClientId = Guid.NewGuid().ToString(),
                ClientSecret = "test-secret",
                MailboxAddress = MailboxAddress
            }),
            Substitute.For<ILogger<GraphEmailService>>(),
            domainEvents,
            Substitute.For<ICorrelationContext>(),
            Substitute.For<IRepository<TicketTimelineEvent>>(),
            Substitute.For<ITimelineEventBus>(),
            emailSettingsProvider,
            sendMailAsync);
    }
}
