using Helpdesk.Application.Events;
using Helpdesk.Application.Sla;
using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Helpdesk.Tests.Application.Sla;

public class SlaEscalationEvaluatorTests
{
    [Fact]
    public async Task EvaluateAt49Percent_DoesNotSend()
    {
        var policyResolver = Substitute.For<ISlaPolicyResolver>();
        var clock = Substitute.For<ISlaClockService>();
        var eventsRepo = Substitute.For<ITicketSlaEscalationEventRepository>();
        var template = Substitute.For<ISlaEmailTemplate>();
        var sender = Substitute.For<IEmailSender>();
        var logger = Substitute.For<ILogger<SlaEscalationEvaluator>>();

        var policy = CreatePolicy(new SlaEscalationRule { Metric = SlaMetricType.Response, TriggerPercent = 50, IsActive = true, Recipients = ["a@b.com"] });
        policyResolver.ResolveAsync("tenant-1", TicketType.Incident).Returns(policy);
        clock.Compute(Arg.Any<TicketSlaState>(), Arg.Any<DateTimeOffset>()).Returns(CreateSnapshot(responsePercent: 49, resolutionPercent: 0));

        var sut = new SlaEscalationEvaluator(policyResolver, clock, eventsRepo, template, sender, logger);

        await sut.EvaluateAndNotifyAsync(CreateIncident(), CreateSlaState(), DateTimeOffset.UtcNow);

        await sender.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EvaluateAt50Percent_SendsOnceAndMarksSent()
    {
        var policyResolver = Substitute.For<ISlaPolicyResolver>();
        var clock = Substitute.For<ISlaClockService>();
        var eventsRepo = Substitute.For<ITicketSlaEscalationEventRepository>();
        var template = Substitute.For<ISlaEmailTemplate>();
        var sender = Substitute.For<IEmailSender>();
        var logger = Substitute.For<ILogger<SlaEscalationEvaluator>>();
        var domainEvents = Substitute.For<IDomainEventPublisher>();
        var correlation = Substitute.For<ICorrelationContext>();
        correlation.GetCorrelationId().Returns("corr-test");

        var rule = new SlaEscalationRule { Metric = SlaMetricType.Response, TriggerPercent = 50, IsActive = true, Recipients = ["a@b.com"] };
        var policy = CreatePolicy(rule);
        policyResolver.ResolveAsync("tenant-1", TicketType.Incident).Returns(policy);
        clock.Compute(Arg.Any<TicketSlaState>(), Arg.Any<DateTimeOffset>()).Returns(CreateSnapshot(responsePercent: 50, resolutionPercent: 10));

        var pending = new TicketSlaEscalationEvent
        {
            TicketId = "t1",
            Metric = SlaMetricType.Response,
            TriggerPercent = 50,
            SendStatus = EscalationSendStatus.Pending
        };

        eventsRepo.GetOrCreateAsync(Arg.Any<TicketSlaEscalationEvent>(), Arg.Any<CancellationToken>())
            .Returns((pending, true));
        template.BuildWarning(Arg.Any<Ticket>(), Arg.Any<SlaClockSnapshot>(), Arg.Any<SlaEscalationRule>(), Arg.Any<List<string>>())
            .Returns(new EmailMessage { To = ["a@b.com"], Subject = "subject", HtmlBody = "body" });

        var sut = new SlaEscalationEvaluator(policyResolver, clock, eventsRepo, template, sender, logger, null, null, domainEvents, correlation);

        await sut.EvaluateAndNotifyAsync(CreateIncident(), CreateSlaState(), DateTimeOffset.UtcNow);

        await sender.Received(1).SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
        await eventsRepo.Received(1).UpdateAsync(Arg.Is<TicketSlaEscalationEvent>(x => x.SendStatus == EscalationSendStatus.Sent), Arg.Any<CancellationToken>());
        await domainEvents.Received().PublishAsync(
            Arg.Is<DomainEvent>(x => x.GetType() == typeof(TicketSlaEscalationSentDomainEvent) && x.EntityId == "t1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EvaluateAlreadySent_DoesNotSendAgain()
    {
        var policyResolver = Substitute.For<ISlaPolicyResolver>();
        var clock = Substitute.For<ISlaClockService>();
        var eventsRepo = Substitute.For<ITicketSlaEscalationEventRepository>();
        var template = Substitute.For<ISlaEmailTemplate>();
        var sender = Substitute.For<IEmailSender>();
        var logger = Substitute.For<ILogger<SlaEscalationEvaluator>>();

        var rule = new SlaEscalationRule { Metric = SlaMetricType.Response, TriggerPercent = 50, IsActive = true, Recipients = ["a@b.com"] };
        var policy = CreatePolicy(rule);
        policyResolver.ResolveAsync("tenant-1", TicketType.Incident).Returns(policy);
        clock.Compute(Arg.Any<TicketSlaState>(), Arg.Any<DateTimeOffset>()).Returns(CreateSnapshot(responsePercent: 50, resolutionPercent: 10));

        var sent = new TicketSlaEscalationEvent
        {
            TicketId = "t1",
            Metric = SlaMetricType.Response,
            TriggerPercent = 50,
            SendStatus = EscalationSendStatus.Sent
        };
        eventsRepo.GetOrCreateAsync(Arg.Any<TicketSlaEscalationEvent>(), Arg.Any<CancellationToken>())
            .Returns((sent, false));

        var sut = new SlaEscalationEvaluator(policyResolver, clock, eventsRepo, template, sender, logger);

        await sut.EvaluateAndNotifyAsync(CreateIncident(), CreateSlaState(), DateTimeOffset.UtcNow);

        await sender.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EvaluateBothMetrics_CanSendIndependently()
    {
        var policyResolver = Substitute.For<ISlaPolicyResolver>();
        var clock = Substitute.For<ISlaClockService>();
        var eventsRepo = Substitute.For<ITicketSlaEscalationEventRepository>();
        var template = Substitute.For<ISlaEmailTemplate>();
        var sender = Substitute.For<IEmailSender>();
        var logger = Substitute.For<ILogger<SlaEscalationEvaluator>>();

        var responseRule = new SlaEscalationRule { Metric = SlaMetricType.Response, TriggerPercent = 50, IsActive = true, Recipients = ["a@b.com"] };
        var resolutionRule = new SlaEscalationRule { Metric = SlaMetricType.Resolution, TriggerPercent = 80, IsActive = true, Recipients = ["a@b.com"] };
        var policy = CreatePolicy(responseRule, resolutionRule);

        policyResolver.ResolveAsync("tenant-1", TicketType.Incident).Returns(policy);
        clock.Compute(Arg.Any<TicketSlaState>(), Arg.Any<DateTimeOffset>()).Returns(CreateSnapshot(responsePercent: 90, resolutionPercent: 85));

        eventsRepo.GetOrCreateAsync(Arg.Any<TicketSlaEscalationEvent>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var seed = ci.Arg<TicketSlaEscalationEvent>();
                return (new TicketSlaEscalationEvent
                {
                    TicketId = seed.TicketId,
                    Metric = seed.Metric,
                    TriggerPercent = seed.TriggerPercent,
                    SendStatus = EscalationSendStatus.Pending
                }, true);
            });

        template.BuildWarning(Arg.Any<Ticket>(), Arg.Any<SlaClockSnapshot>(), Arg.Any<SlaEscalationRule>(), Arg.Any<List<string>>())
            .Returns(new EmailMessage { To = ["a@b.com"], Subject = "subject", HtmlBody = "body" });

        var sut = new SlaEscalationEvaluator(policyResolver, clock, eventsRepo, template, sender, logger);

        await sut.EvaluateAndNotifyAsync(CreateIncident(), CreateSlaState(), DateTimeOffset.UtcNow);

        await sender.Received(2).SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Evaluate_NoPolicy_DoesNotSend()
    {
        var policyResolver = Substitute.For<ISlaPolicyResolver>();
        var clock = Substitute.For<ISlaClockService>();
        var eventsRepo = Substitute.For<ITicketSlaEscalationEventRepository>();
        var template = Substitute.For<ISlaEmailTemplate>();
        var sender = Substitute.For<IEmailSender>();
        var logger = Substitute.For<ILogger<SlaEscalationEvaluator>>();

        policyResolver.ResolveAsync("tenant-1", TicketType.Incident).Returns((SlaPolicy?)null);

        var sut = new SlaEscalationEvaluator(policyResolver, clock, eventsRepo, template, sender, logger);

        await sut.EvaluateAndNotifyAsync(CreateIncident(), CreateSlaState(), DateTimeOffset.UtcNow);

        await sender.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Evaluate_InactiveRule_Ignored()
    {
        var policyResolver = Substitute.For<ISlaPolicyResolver>();
        var clock = Substitute.For<ISlaClockService>();
        var eventsRepo = Substitute.For<ITicketSlaEscalationEventRepository>();
        var template = Substitute.For<ISlaEmailTemplate>();
        var sender = Substitute.For<IEmailSender>();
        var logger = Substitute.For<ILogger<SlaEscalationEvaluator>>();

        var policy = CreatePolicy(new SlaEscalationRule
        {
            Metric = SlaMetricType.Response,
            TriggerPercent = 50,
            IsActive = false,
            Recipients = ["a@b.com"]
        });

        policyResolver.ResolveAsync("tenant-1", TicketType.Incident).Returns(policy);
        clock.Compute(Arg.Any<TicketSlaState>(), Arg.Any<DateTimeOffset>()).Returns(CreateSnapshot(responsePercent: 90, resolutionPercent: 90));

        var sut = new SlaEscalationEvaluator(policyResolver, clock, eventsRepo, template, sender, logger);

        await sut.EvaluateAndNotifyAsync(CreateIncident(), CreateSlaState(), DateTimeOffset.UtcNow);

        await sender.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    private static Incident CreateIncident()
    {
        return new Incident
        {
            Id = "t1",
            OrganizationId = "tenant-1",
            Title = "Ticket",
            TrackingId = "INC-1",
            State = TicketState.InProgress
        };
    }

    private static TicketSlaState CreateSlaState()
    {
        var start = DateTimeOffset.UtcNow.AddHours(-1);
        return new TicketSlaState
        {
            TicketId = "t1",
            StartedAt = start,
            ResponseDueAt = start.AddHours(4),
            ResolutionDueAt = start.AddHours(8),
            Status = SlaStatus.InProgress
        };
    }

    private static SlaPolicy CreatePolicy(params SlaEscalationRule[] rules)
    {
        return new SlaPolicy
        {
            Id = "p1",
            ScopeType = SlaScopeType.Tenant,
            TenantId = "tenant-1",
            AppliesTo = TicketType.Incident,
            IsActive = true,
            Escalations = rules.ToList()
        };
    }

    private static SlaClockSnapshot CreateSnapshot(int responsePercent, int resolutionPercent)
    {
        var now = DateTimeOffset.UtcNow;
        return new SlaClockSnapshot(
            SlaStatus.InProgress,
            now.AddHours(-1),
            now.AddHours(3),
            now.AddHours(7),
            TimeSpan.Zero,
            null,
            null,
            null,
            TimeSpan.FromHours(3),
            TimeSpan.FromHours(7),
            responsePercent,
            resolutionPercent,
            false,
            false);
    }
}
