using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using Helpdesk.API.Endpoints.Orchestration;
using Helpdesk.API.Middleware;
using Helpdesk.Application.Events;
using Helpdesk.Application.Messaging;
using Helpdesk.Application.RequestTasks;
using Helpdesk.Application.WorkLogs;
using Helpdesk.Application.Workflow;
using Helpdesk.Infrastructure.Configuration;
using Helpdesk.Infrastructure.Events;
using Helpdesk.Infrastructure.Identity;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.DTOs.Orchestration;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Helpdesk.Tests.Api;

public class OrchestrationCallbackEndpointsTests
{
    [Theory]
    [InlineData("provider")]
    public async Task Callback_WithValidToken_UpdatesStateAndReturnsNoContent(string route)
    {
        await using var harness = await OrchestrationCallbackTestHarness.CreateAsync(route);

        await harness.SeedTaskAsync("abc123");
        var token = harness.CreateToken(audience: harness.Audience, expires: DateTimeOffset.UtcNow.AddMinutes(5), clientId: "orchestration.api");
        var response = await harness.PostAsync(token, new OrchestrationCallbackDto
        {
            RequestTaskId = harness.TaskId,
            ExecutionId = "abc123",
            Status = "succeeded"
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Correlation-Id"));

        var task = await harness.GetTaskAsync();
        Assert.Equal(RequestTaskStatus.Completed, task!.Status);
        Assert.NotNull(task.CompletedAt);
        Assert.True(harness.RequestStateService.Invoked);
        Assert.Single(harness.Sender.Worklogs);
    }

    [Theory]
    [InlineData("provider")]
    public async Task Callback_WithWrongAudience_ReturnsUnauthorized(string route)
    {
        await using var harness = await OrchestrationCallbackTestHarness.CreateAsync(route);
        await harness.SeedTaskAsync("abc123");

        var token = harness.CreateToken(audience: "wrong-aud", expires: DateTimeOffset.UtcNow.AddMinutes(5), clientId: "orchestration.api");
        var response = await harness.PostAsync(token, new OrchestrationCallbackDto
        {
            RequestTaskId = harness.TaskId,
            ExecutionId = "abc123",
            Status = "running"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("provider")]
    public async Task Callback_WithExpiredToken_ReturnsUnauthorized(string route)
    {
        await using var harness = await OrchestrationCallbackTestHarness.CreateAsync(route);
        await harness.SeedTaskAsync("abc123");

        var token = harness.CreateToken(audience: harness.Audience, expires: DateTimeOffset.UtcNow.AddMinutes(-5), clientId: "orchestration.api");
        var response = await harness.PostAsync(token, new OrchestrationCallbackDto
        {
            RequestTaskId = harness.TaskId,
            ExecutionId = "abc123",
            Status = "running"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("provider")]
    public async Task Callback_WithClientNotAllowed_ReturnsForbidden(string route)
    {
        await using var harness = await OrchestrationCallbackTestHarness.CreateAsync(route);
        await harness.SeedTaskAsync("abc123");

        var token = harness.CreateToken(audience: harness.Audience, expires: DateTimeOffset.UtcNow.AddMinutes(5), clientId: "unknown-client");
        var response = await harness.PostAsync(token, new OrchestrationCallbackDto
        {
            RequestTaskId = harness.TaskId,
            ExecutionId = "abc123",
            Status = "running"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("provider")]
    public async Task Callback_WithExecutionIdMismatch_ReturnsBadRequest(string route)
    {
        await using var harness = await OrchestrationCallbackTestHarness.CreateAsync(route);
        await harness.SeedTaskAsync("expected-exec");

        var token = harness.CreateToken(audience: harness.Audience, expires: DateTimeOffset.UtcNow.AddMinutes(5), clientId: "orchestration.api");
        var response = await harness.PostAsync(token, new OrchestrationCallbackDto
        {
            RequestTaskId = harness.TaskId,
            ExecutionId = "different-exec",
            Status = "succeeded"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(harness.DomainEvents.Events, x =>
            x.EventType == OrchestrationDomainEventTypes.OrchestrationCallbackRejected &&
            x.Reference == "orchestration.api");
    }

    [Theory]
    [InlineData("provider")]
    public async Task Callback_DuplicateSucceeded_IsIdempotent(string route)
    {
        await using var harness = await OrchestrationCallbackTestHarness.CreateAsync(route);
        await harness.SeedTaskAsync("abc123");
        var token = harness.CreateToken(audience: harness.Audience, expires: DateTimeOffset.UtcNow.AddMinutes(5), clientId: "orchestration.api");

        var first = await harness.PostAsync(token, new OrchestrationCallbackDto
        {
            RequestTaskId = harness.TaskId,
            ExecutionId = "abc123",
            Status = "succeeded"
        });
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        var completedAt = (await harness.GetTaskAsync())!.CompletedAt;
        var second = await harness.PostAsync(token, new OrchestrationCallbackDto
        {
            RequestTaskId = harness.TaskId,
            ExecutionId = "abc123",
            Status = "succeeded"
        });

        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        var task = await harness.GetTaskAsync();
        Assert.Equal(completedAt, task!.CompletedAt);

        var completedEvents = harness.DomainEvents.Events.Count(e => e.EventType == RequestTaskDomainEventTypes.AutomationCompleted);
        Assert.Equal(1, completedEvents);
        Assert.Single(harness.Sender.Worklogs);
    }

    [Theory]
    [InlineData("provider")]
    public async Task Callback_WithOrchestrationRunIdOnly_ResolvesTaskAndUpdatesIdentifiers(string route)
    {
        await using var harness = await OrchestrationCallbackTestHarness.CreateAsync(route);
        await harness.SeedTaskAsync("legacy-exec", orchestrationRequestId: "orchestration-req-1", orchestrationRunId: "orchestration-run-1");

        var token = harness.CreateToken(audience: harness.Audience, expires: DateTimeOffset.UtcNow.AddMinutes(5), clientId: "orchestration.api");
        var response = await harness.PostAsync(token, new OrchestrationCallbackDto
        {
            OrchestrationRunId = "orchestration-run-1",
            OrchestrationRequestId = "orchestration-req-1",
            Status = "failed",
            Message = "Job failed in External orchestration"
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var task = await harness.GetTaskAsync();
        Assert.Equal(RequestTaskStatus.Failed, task!.Status);
        Assert.Equal("orchestration-req-1", task.OrchestrationExternalRequestId);
        Assert.Equal("orchestration-run-1", task.OrchestrationExternalRunId);
        Assert.Equal("failed", task.LastAutomationStatus);
        Assert.Single(harness.Sender.Worklogs);
    }

    [Theory]
    [InlineData("provider")]
    public async Task Callback_ExpectedRuntimeExceeded_RecordsWorklogWithoutFailingTask(string route)
    {
        await using var harness = await OrchestrationCallbackTestHarness.CreateAsync(route);
        await harness.SeedTaskAsync("abc123");

        var token = harness.CreateToken(audience: harness.Audience, expires: DateTimeOffset.UtcNow.AddMinutes(5), clientId: "orchestration.api");
        var response = await harness.PostAsync(token, new OrchestrationCallbackDto
        {
            RequestTaskId = harness.TaskId,
            ExecutionId = "abc123",
            Status = "running",
            ExpectedRuntimeExceeded = true,
            ExpectedRuntimeSeconds = 1800,
            GraceSeconds = 600,
            HardTimeoutSeconds = 2400
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var task = await harness.GetTaskAsync();
        Assert.Equal(RequestTaskStatus.InProgress, task!.Status);
        Assert.Equal("running", task.LastAutomationStatus);
        Assert.Single(harness.Sender.Worklogs);
        Assert.Contains("Expected runtime exceeded", harness.Sender.Worklogs[0].Notes);
        Assert.Equal(0, harness.FailurePolicyEngine.Invocations);
    }

    [Theory]
    [InlineData("provider")]
    public async Task Ping_RequiresValidAudienceLifetimeAndAllowedCaller(string route)
    {
        await using var harness = await OrchestrationCallbackTestHarness.CreateAsync(route);
        using var missing = await PingAsync(null);
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        using var wrongAudience = await PingAsync(harness.CreateToken("wrong-aud", DateTimeOffset.UtcNow.AddMinutes(5), "orchestration.api"));
        Assert.Equal(HttpStatusCode.Unauthorized, wrongAudience.StatusCode);
        using var expired = await PingAsync(harness.CreateToken(harness.Audience, DateTimeOffset.UtcNow.AddMinutes(-5), "orchestration.api"));
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        using var forbidden = await PingAsync(harness.CreateToken(harness.Audience, DateTimeOffset.UtcNow.AddMinutes(5), "unknown-client"));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var allowed = await PingAsync(harness.CreateToken(harness.Audience, DateTimeOffset.UtcNow.AddMinutes(5), "orchestration.api"));
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        var result = await allowed.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("Helpdesk.API", result.GetProperty("service").GetString());
        Assert.Equal(harness.Audience, result.GetProperty("audience").GetString());
        Assert.Empty(harness.Sender.Worklogs);

        async Task<HttpResponseMessage> PingAsync(string? token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "m2m/ping");
            if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return await harness.Client.SendAsync(request);
        }
    }

    private sealed class OrchestrationCallbackTestHarness : IAsyncDisposable
    {
        private readonly ECDsa _signingKey;
        private readonly WebApplication _app;
        private readonly IServiceScopeFactory _scopeFactory;

        private OrchestrationCallbackTestHarness(
            ECDsa signingKey,
            WebApplication app,
            IServiceScopeFactory scopeFactory,
            HttpClient client,
            string issuer,
            string audience,
            TestDomainEventPublisher domainEvents,
            TestRequestTaskStateService requestStateService,
            TestFailurePolicyEngine failurePolicyEngine,
            TestSender sender,
            string taskId)
        {
            _signingKey = signingKey;
            _app = app;
            _scopeFactory = scopeFactory;
            Client = client;
            Issuer = issuer;
            Audience = audience;
            DomainEvents = domainEvents;
            RequestStateService = requestStateService;
            FailurePolicyEngine = failurePolicyEngine;
            Sender = sender;
            TaskId = taskId;
        }

        public HttpClient Client { get; }
        public string Issuer { get; }
        public string Audience { get; }
        public string TaskId { get; }
        public TestDomainEventPublisher DomainEvents { get; }
        public TestRequestTaskStateService RequestStateService { get; }
        public TestFailurePolicyEngine FailurePolicyEngine { get; }
        public TestSender Sender { get; }

        public static async Task<OrchestrationCallbackTestHarness> CreateAsync(string route)
        {
            var issuer = "https://orchestration.example.com";
            var audience = "helpdesk.api";
            var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var dbName = $"orchestration-callback-tests-{Guid.NewGuid():N}";
            var domainEvents = new TestDomainEventPublisher();
            var requestStateService = new TestRequestTaskStateService();
            var failurePolicyEngine = new TestFailurePolicyEngine();
            var sender = new TestSender();
            var taskId = Guid.NewGuid().ToString("N");

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = "Development"
            });
            builder.WebHost.UseTestServer();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddDbContext<HelpdeskDbContext>(options =>
                options.UseInMemoryDatabase(dbName));
            builder.Services.AddScoped<ITenantContext, TestTenantContext>();
            builder.Services.AddScoped<ICorrelationContext, HttpCorrelationContext>();
            builder.Services.AddSingleton<IDomainEventPublisher>(domainEvents);
            builder.Services.AddSingleton<IRequestTaskStateService>(requestStateService);
            builder.Services.AddSingleton<IFailurePolicyEngine>(failurePolicyEngine);
            builder.Services.AddSingleton<IRequestSender>(sender);
            builder.Services.Configure<OrchestrationM2MOptions>(o =>
            {
                o.AllowedCallerClientIds = ["orchestration.api", "orchestration.worker"];
            });

            builder.Services.AddAuthentication()
                .AddJwtBearer("OrchestrationM2M", options =>
                {
                    options.RequireHttpsMetadata = false;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = issuer,
                        ValidateAudience = true,
                        ValidAudience = audience,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new ECDsaSecurityKey(signingKey),
                        ClockSkew = TimeSpan.FromMinutes(2),
                        NameClaimType = "azp"
                    };
                });

            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("OrchestrationM2MOnly", policy =>
                {
                    policy.AddAuthenticationSchemes("OrchestrationM2M");
                    policy.RequireAuthenticatedUser();
                });
            });

            var app = builder.Build();
            app.UseMiddleware<CorrelationIdMiddleware>();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapExternalOrchestrationCallbackEndpoints();
            await app.StartAsync();

            var client = app.GetTestClient();
            client.BaseAddress = new Uri($"http://localhost/api/v1/orchestration/{route}/");
            return new OrchestrationCallbackTestHarness(
                signingKey,
                app,
                app.Services.GetRequiredService<IServiceScopeFactory>(),
                client,
                issuer,
                audience,
                domainEvents,
                requestStateService,
                failurePolicyEngine,
                sender,
                taskId);
        }

        public string CreateToken(string audience, DateTimeOffset expires, string clientId)
        {
            var credentials = new SigningCredentials(new ECDsaSecurityKey(_signingKey), SecurityAlgorithms.EcdsaSha256);
            var now = DateTimeOffset.UtcNow;
            var notBefore = now.AddMinutes(-1);
            if (expires <= notBefore)
            {
                notBefore = expires.AddMinutes(-1);
            }
            var token = new JwtSecurityToken(
                issuer: Issuer,
                audience: audience,
                claims:
                [
                    new Claim("azp", clientId),
                    new Claim("client_id", clientId)
                ],
                notBefore: notBefore.UtcDateTime,
                expires: expires.UtcDateTime,
                signingCredentials: credentials);
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public async Task SeedTaskAsync(string executionId, string? orchestrationRequestId = null, string? orchestrationRunId = null)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();

            var request = new Request
            {
                Id = Guid.NewGuid().ToString("D"),
                Title = "Request",
                OrganizationId = "tenant-1"
            };

            var task = new RequestTask
            {
                Id = TaskId,
                RequestId = request.Id,
                Request = request,
                Title = "Automation Task",
                OrganizationId = "tenant-1",
                Status = RequestTaskStatus.InProgress,
                State = TicketState.InProgress,
                Type = RequestTaskType.Automation,
                OrchestratorExecutionId = executionId,
                OrchestrationExternalRequestId = orchestrationRequestId,
                OrchestrationExternalRunId = orchestrationRunId
            };

            db.Requests.Add(request);
            db.RequestTasks.Add(task);
            await db.SaveChangesAsync();
        }

        public async Task<RequestTask?> GetTaskAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();
            return await db.RequestTasks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == TaskId);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.DisposeAsync();
            _signingKey.Dispose();
        }

        public async Task<HttpResponseMessage> PostAsync(string token, OrchestrationCallbackDto dto)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "callback")
            {
                Content = JsonContent.Create(dto)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return await Client.SendAsync(request);
        }
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public string? TenantId => null;
        public string? UserId => "test-user";
        public bool IsHelpdeskAdmin => true;
    }

    private sealed class TestRequestTaskStateService : IRequestTaskStateService
    {
        public bool Invoked { get; private set; }

        public Task<bool> EvaluateParentRequestState(string requestId, CancellationToken cancellationToken = default)
        {
            Invoked = true;
            return Task.FromResult(true);
        }
    }

    private sealed class TestFailurePolicyEngine : IFailurePolicyEngine
    {
        public int Invocations { get; private set; }

        public Task<FailurePolicyResult> OnTaskFailedAsync(string requestId, string taskId, CancellationToken ct)
        {
            Invocations++;
            return Task.FromResult(new FailurePolicyResult
            {
                Decision = "BlockRequest",
                RetryScheduled = false,
                RequestStateChanged = true
            });
        }
    }

    private sealed class TestDomainEventPublisher : IDomainEventPublisher
    {
        public List<DomainEvent> Events { get; } = new();

        public Task PublishAsync(DomainEvent domainEvent, CancellationToken ct)
        {
            Events.Add(domainEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class TestSender : IRequestSender
    {
        public List<CreateWorkLogCommand> Worklogs { get; } = new();

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is CreateWorkLogCommand command)
            {
                Worklogs.Add(command);
                return Task.FromResult((TResponse)(object)new WorkLog { TicketId = command.TicketId });
            }

            return Task.FromResult(default(TResponse)!);
        }
    }
}
