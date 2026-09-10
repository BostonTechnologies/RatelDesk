using Helpdesk.Application.Incidents;
using Helpdesk.Application.Sla;
using Helpdesk.Application.Services.SupportNotifications;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using NSubstitute;
using Xunit;

namespace Helpdesk.Tests.Application.Incidents;

public class CreateIncidentCommandHandlerTests
{
    [Fact]
    public async Task Handle_CallsRepositoryAddOnce()
    {
        var repo = Substitute.For<IRepository<Incident>>();
        repo.CreateAsync(Arg.Any<Incident>()).Returns(ci => ci.Arg<Incident>());
        var refGen = Substitute.For<Helpdesk.Application.Services.Tickets.ITicketRefGeneratorService>();
        refGen.NextReferenceAsync(Arg.Any<string>()).Returns("INC-XYZ-123-456");
        var orgRepo = Substitute.For<IRepository<Organization>>();
        var understanding = Substitute.For<Helpdesk.Application.Services.AI.ITicketUnderstandingService>();
        var suggestions = Substitute.For<Helpdesk.Application.Services.KB.IKnowledgeSuggestionService>();
        var suggRepo = Substitute.For<IRepository<TicketKnowledgeSuggestion>>();
        var supportNotifications = Substitute.For<ISupportNotificationService>();
        var handler = new CreateIncidentCommandHandler(repo, refGen, orgRepo, understanding, suggestions, suggRepo, supportNotifications);
        var command = new CreateIncidentCommand(
            "Test",
            "Desc",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

        await handler.Handle(command, CancellationToken.None);

        await repo.Received(1).CreateAsync(Arg.Any<Incident>());
    }

    [Fact]
    public async Task Handle_InitializesTicketSlaState_WhenInitializerProvided()
    {
        var repo = Substitute.For<IRepository<Incident>>();
        repo.CreateAsync(Arg.Any<Incident>()).Returns(ci => ci.Arg<Incident>());
        var refGen = Substitute.For<Helpdesk.Application.Services.Tickets.ITicketRefGeneratorService>();
        refGen.NextReferenceAsync(Arg.Any<string>()).Returns("INC-XYZ-123-456");
        var orgRepo = Substitute.For<IRepository<Organization>>();
        var understanding = Substitute.For<Helpdesk.Application.Services.AI.ITicketUnderstandingService>();
        var suggestions = Substitute.For<Helpdesk.Application.Services.KB.IKnowledgeSuggestionService>();
        var suggRepo = Substitute.For<IRepository<TicketKnowledgeSuggestion>>();
        var supportNotifications = Substitute.For<ISupportNotificationService>();
        var slaInitializer = Substitute.For<ITicketSlaInitializer>();

        var handler = new CreateIncidentCommandHandler(
            repo,
            refGen,
            orgRepo,
            understanding,
            suggestions,
            suggRepo,
            supportNotifications,
            slaInitializer);

        var command = new CreateIncidentCommand(
            "Test",
            "Desc",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

        await handler.Handle(command, CancellationToken.None);

        await slaInitializer.Received(1).InitializeAsync(Arg.Any<Incident>());
    }

    [Fact]
    public async Task Handle_Persists_AssignedToId_WhenProvided()
    {
        var repo = Substitute.For<IRepository<Incident>>();
        Incident? captured = null;
        repo.CreateAsync(Arg.Do<Incident>(x => captured = x)).Returns(ci => ci.Arg<Incident>());
        var refGen = Substitute.For<Helpdesk.Application.Services.Tickets.ITicketRefGeneratorService>();
        refGen.NextReferenceAsync(Arg.Any<string>()).Returns("INC-XYZ-123-456");
        var orgRepo = Substitute.For<IRepository<Organization>>();
        var understanding = Substitute.For<Helpdesk.Application.Services.AI.ITicketUnderstandingService>();
        var suggestions = Substitute.For<Helpdesk.Application.Services.KB.IKnowledgeSuggestionService>();
        var suggRepo = Substitute.For<IRepository<TicketKnowledgeSuggestion>>();
        var supportNotifications = Substitute.For<ISupportNotificationService>();
        var handler = new CreateIncidentCommandHandler(repo, refGen, orgRepo, understanding, suggestions, suggRepo, supportNotifications);
        var command = new CreateIncidentCommand(
            "Test",
            "Desc",
            null,
            null,
            "org-1",
            null,
            null,
            null,
            null,
            null,
            null,
            "user-1");

        await handler.Handle(command, CancellationToken.None);

        Assert.Equal("user-1", captured!.AssignedToId);
    }

    [Fact]
    public async Task Handle_UnassignedTicket_NotifiesSupport()
    {
        var repo = Substitute.For<IRepository<Incident>>();
        repo.CreateAsync(Arg.Any<Incident>()).Returns(ci => ci.Arg<Incident>());
        var refGen = Substitute.For<Helpdesk.Application.Services.Tickets.ITicketRefGeneratorService>();
        refGen.NextReferenceAsync(Arg.Any<string>()).Returns("INC-XYZ-123-456");
        var orgRepo = Substitute.For<IRepository<Organization>>();
        var understanding = Substitute.For<Helpdesk.Application.Services.AI.ITicketUnderstandingService>();
        var suggestions = Substitute.For<Helpdesk.Application.Services.KB.IKnowledgeSuggestionService>();
        var suggRepo = Substitute.For<IRepository<TicketKnowledgeSuggestion>>();
        var supportNotifications = Substitute.For<ISupportNotificationService>();
        var handler = new CreateIncidentCommandHandler(repo, refGen, orgRepo, understanding, suggestions, suggRepo, supportNotifications);

        await handler.Handle(new CreateIncidentCommand(
            "Test",
            "Desc",
            null,
            null,
            "org-1",
            null,
            null,
            null,
            null,
            null,
            null,
            null), CancellationToken.None);

        await supportNotifications.Received(1).NotifyTicketCreatedUnassignedAsync(
            Arg.Is<Incident>(x => x.TrackingId == "INC-XYZ-123-456"),
            Arg.Any<CancellationToken>());
    }
}
