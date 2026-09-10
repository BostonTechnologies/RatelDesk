using System.Net;
using System.Text.Json;
using Helpdesk.Application.AiAssistant;
using Helpdesk.Application.Services.AI;
using Helpdesk.Infrastructure.AiAssistant;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.DTOs;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Helpdesk.Tests.Infrastructure.AiAssistant;

public class AiAssistantAiAssistantServiceTests
{
    [Fact]
    public async Task DispatchAsync_PersistsAndSendsOperatorAssistanceRequest()
    {
        var options = new DbContextOptionsBuilder<HelpdeskDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var tenant = Substitute.For<ITenantContext>();
        tenant.TenantId.Returns("org-example");
        await using var db = new HelpdeskDbContext(options, tenant, new HttpContextAccessor());
        db.Incidents.Add(new Incident { Id = "incident-1", OrganizationId = "org-example", Title = "Printer", Description = "Queued labels" });
        var configuration = new AiAssistantWebhookConfiguration
        {
            OrganizationId = "org-example",
            Name = "Example incident webhook",
            Endpoint = "https://ai-assistant.example/webhook",
            SigningSecretProtected = "protected-secret",
            IsEnabled = true,
            TicketAreas = [AiAssistantTicketArea.Incidents]
        };
        db.AiAssistantWebhookConfigurations.Add(configuration);
        await db.SaveChangesAsync();
        string? body = null;
        var client = new HttpClient(new RecordingHandler(async request =>
        {
            body = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.Accepted);
        }));
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("AiAssistantWebhook").Returns(client);
        var protector = Substitute.For<ISecretProtector>();
        protector.Unprotect("protected-secret").Returns("signing-secret");
        var service = new AiAssistantAiAssistantService(db, tenant, Substitute.For<IAiInvestigationEventBus>(), factory, protector);

        await service.DispatchAsync("incident-1", "incidents", new DispatchAiInvestigationDto(configuration.Id, "  Check the printer queue and report the findings.  ", "key-685"), "operator-1", CancellationToken.None);

        var invocation = Assert.Single(db.AiInvestigationInvocations);
        Assert.Equal("Check the printer queue and report the findings.", invocation.OperatorAssistanceRequest);
        using var document = JsonDocument.Parse(body!);
        Assert.Equal("Check the printer queue and report the findings.", document.RootElement.GetProperty("operatorAssistanceRequest").GetString());
    }

    [Fact]
    public async Task DispatchAsync_RejectsBlankOperatorAssistanceRequest()
    {
        var options = new DbContextOptionsBuilder<HelpdeskDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var tenant = Substitute.For<ITenantContext>();
        tenant.TenantId.Returns("org-example");
        await using var db = new HelpdeskDbContext(options, tenant, new HttpContextAccessor());
        var service = new AiAssistantAiAssistantService(db, tenant, Substitute.For<IAiInvestigationEventBus>(), Substitute.For<IHttpClientFactory>(), Substitute.For<ISecretProtector>());

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.DispatchAsync("incident-1", "incidents", new DispatchAiInvestigationDto(Guid.NewGuid(), "   ", "key-685"), "operator-1", CancellationToken.None));

        Assert.Equal("Operator assistance request is required.", exception.Message);
    }

    [Fact]
    public async Task AppendMcpWorklogAsync_ExistingMcpIdentityWithoutTenantClaim_UsesInvocationScope()
    {
        var options = new DbContextOptionsBuilder<HelpdeskDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var tenant = Substitute.For<ITenantContext>();
        tenant.IsHelpdeskAdmin.Returns(false);
        tenant.TenantId.Returns((string?)null);
        await using var db = new HelpdeskDbContext(options, tenant, new HttpContextAccessor());
        var invocation = new AiInvestigationInvocation
        {
            OrganizationId = "org-example",
            TicketId = "incident-1",
            TicketArea = AiAssistantTicketArea.Incidents,
            CorrelationId = "corr-683",
            IdempotencyKey = "key-683"
        };
        db.AiInvestigationInvocations.Add(invocation);
        await db.SaveChangesAsync();

        var service = new AiAssistantAiAssistantService(
            db,
            tenant,
            Substitute.For<IAiInvestigationEventBus>(),
            Substitute.For<IHttpClientFactory>(),
            Substitute.For<ISecretProtector>());

        var appended = await service.AppendMcpWorklogAsync(
            invocation.Id,
            new AppendAiInvestigationWorklogDto("event-683", "incident-1", "incidents", "corr-683", AiInvestigationStatus.Progress, null, "Investigating the printer queue.", null, null, "run-683", DateTimeOffset.UtcNow),
            CancellationToken.None);

        Assert.True(appended);
        var worklog = Assert.Single(db.AiInvestigationWorklogEntries);
        Assert.Equal("org-example", worklog.OrganizationId);
        Assert.Equal(AiInvestigationStatus.Progress, worklog.Status);
    }

    [Fact]
    public async Task GetEligibleAsync_HelpdeskAdminWithoutTenantClaim_UsesTicketOrganization()
    {
        var options = new DbContextOptionsBuilder<HelpdeskDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var tenant = Substitute.For<ITenantContext>();
        tenant.IsHelpdeskAdmin.Returns(true);
        tenant.TenantId.Returns((string?)null);
        await using var db = new HelpdeskDbContext(options, tenant, new HttpContextAccessor());
        db.Incidents.Add(new Incident { Id = "incident-1", OrganizationId = "org-example", Title = "Printer", Description = "Queued labels" });
        db.AiAssistantWebhookConfigurations.AddRange(
            new AiAssistantWebhookConfiguration { OrganizationId = "org-example", Name = "Example incident webhook", Endpoint = "https://ai-assistant.example/webhook", IsEnabled = true, TicketAreas = [AiAssistantTicketArea.Incidents] },
            new AiAssistantWebhookConfiguration { OrganizationId = "org-other", Name = "Other tenant webhook", Endpoint = "https://ai-assistant.example/webhook", IsEnabled = true, TicketAreas = [AiAssistantTicketArea.Incidents] });
        await db.SaveChangesAsync();

        var service = new AiAssistantAiAssistantService(
            db,
            tenant,
            Substitute.For<IAiInvestigationEventBus>(),
            Substitute.For<IHttpClientFactory>(),
            Substitute.For<ISecretProtector>());

        var configurations = await service.GetEligibleAsync("incident-1", "incidents", CancellationToken.None);

        var configuration = Assert.Single(configurations);
        Assert.Equal("Example incident webhook", configuration.Name);
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => responder(request);
    }
}
