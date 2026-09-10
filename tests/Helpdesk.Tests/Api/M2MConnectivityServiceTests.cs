using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Helpdesk.API.Application.Connectivity;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Infrastructure.Persistence.Connectivity;
using Helpdesk.Shared.Connectivity;
using Helpdesk.Shared.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

public class M2MConnectivityServiceTests
{
    private sealed class StubTenantContext : ITenantContext
    {
        public string? TenantId { get; init; }
        public string? UserId { get; init; }
        public bool IsHelpdeskAdmin { get; init; }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/internal/health", StringComparison.OrdinalIgnoreCase))
            {
                var json = JsonSerializer.Serialize(new { ok = true, service = "External orchestration provider.API" });
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                });
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    [Fact]
    public async Task RemoteHealth_GoesGreen_On200WithService()
    {
        var options = new DbContextOptionsBuilder<HelpdeskDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var db = new HelpdeskDbContext(options, new StubTenantContext(), new Microsoft.AspNetCore.Http.HttpContextAccessor());
        db.M2MConnectivitySettings.Add(new M2MConnectivitySettings
        {
            Enabled = true,
            RemoteBaseUrl = "http://orchestrator.local",
            RemoteAudience = "orchestrator.api",
            RemoteSystemName = "Orchestrator"
        });
        await db.SaveChangesAsync();

        var factory = Substitute.For<IHttpClientFactory>();
        var http = new HttpClient(new StubHandler()) { BaseAddress = new Uri("http://orchestrator.local") };
        factory.CreateClient("OrchestratorApi").Returns(http);

        var svc = new M2MConnectivityService(factory, db, NullLogger<M2MConnectivityService>.Instance);
        var result = await svc.TestAsync(new M2MConnectivityTestRequestDto(true, true), CancellationToken.None);

        var probe = Assert.Single(result.Probes.Where(p => p.ProbeName == "RemoteHealth"));
        Assert.Equal(TrafficLight.Green, probe.Status);
        Assert.Equal(200, probe.HttpStatus);
        Assert.Equal("External orchestration provider.API", probe.RemoteSystemName);
    }
}

