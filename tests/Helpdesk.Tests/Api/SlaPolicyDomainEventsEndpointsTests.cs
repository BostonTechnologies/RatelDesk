using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Helpdesk.API;
using Helpdesk.Application.Events;
using Helpdesk.Application.Sla;
using Helpdesk.Shared.DTOs.SlaPolicy;
using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Helpdesk.Tests.Api;

public class SlaPolicyDomainEventsEndpointsTests
{
    [Fact]
    public async Task CreatePolicy_RaisesSlaPolicyCreatedDomainEvent()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");
        Environment.SetEnvironmentVariable("RUN_MIGRATIONS", "false");

        var repo = Substitute.For<ISlaPolicyRepository>();
        var validator = Substitute.For<ISlaPolicyValidator>();
        var domainEvents = Substitute.For<IDomainEventPublisher>();
        var correlationContext = Substitute.For<ICorrelationContext>();
        correlationContext.GetCorrelationId().Returns("corr-policy");

        repo.CreateAsync(Arg.Any<SlaPolicy>()).Returns(ci => ci.Arg<SlaPolicy>());
        repo.GetByIdWithEscalationsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((SlaPolicy?)null);

        using var factory = CreateFactory(repo, validator, domainEvents, correlationContext);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", "HelpdeskAdmin");

        var dto = new SlaPolicyDto
        {
            Name = "Tenant Incident Policy",
            ScopeType = SlaScopeType.Tenant,
            TenantId = "tenant-1",
            AppliesTo = TicketType.Incident,
            MatchRank = 1,
            ResponseTimeHours = 4,
            ResolutionTimeHours = 8,
            IsActive = false,
            Escalations =
            [
                new SlaEscalationRuleDto
                {
                    Metric = SlaMetricType.Response,
                    TriggerPercent = 50,
                    IsActive = true,
                    Recipients = ["ops@example.com"]
                }
            ]
        };

        var response = await client.PostAsJsonAsync("/api/v1/slas", dto);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await domainEvents.Received(1).PublishAsync(
            Arg.Is<DomainEvent>(e => e.GetType() == typeof(SlaPolicyCreatedDomainEvent) && e.TenantId == "tenant-1"),
            Arg.Any<CancellationToken>());
    }

    private static WebApplicationFactory<Program> CreateFactory(
        ISlaPolicyRepository repo,
        ISlaPolicyValidator validator,
        IDomainEventPublisher domainEvents,
        ICorrelationContext correlationContext)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting(WebHostDefaults.EnvironmentKey, "Production");
            builder.UseEnvironment("Production");

            builder.ConfigureServices(services =>
            {
                services.AddAuthentication(o =>
                {
                    o.DefaultAuthenticateScheme = "Test";
                    o.DefaultChallengeScheme = "Test";
                }).AddScheme<AuthenticationSchemeOptions, SlaPolicyTestAuthHandler>("Test", _ => { });

                services.AddSingleton(repo);
                services.AddSingleton(validator);
                services.AddSingleton(domainEvents);
                services.AddSingleton(correlationContext);
            });
        });
    }

    private sealed class SlaPolicyTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public SlaPolicyTestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder) : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, "admin-user"),
                new(ClaimTypes.Role, "HelpdeskAdmin")
            };

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
            var ticket = new AuthenticationTicket(principal, "Test");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
