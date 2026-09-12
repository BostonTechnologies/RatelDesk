using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Helpdesk.API.Endpoints.Presence;
using Helpdesk.API.Services;
using Helpdesk.Shared.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Helpdesk.Tests.Api;

public sealed class PresenceEndpointsAuthorizationTests
{
    [Fact]
    public async Task Authenticated_non_administrator_cannot_enumerate_online_users()
    {
        await using var harness = await PresenceEndpointsHarness.CreateAsync();

        var response = await harness.Client.GetAsync("/api/v1/presence/online-users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private sealed class PresenceEndpointsHarness : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private PresenceEndpointsHarness(WebApplication app, HttpClient client)
        {
            _app = app;
            Client = client;
        }

        public HttpClient Client { get; }

        public static async Task<PresenceEndpointsHarness> CreateAsync()
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
            builder.WebHost.UseTestServer();
            builder.Services.AddSingleton<IUserPresenceService>(Substitute.For<IUserPresenceService>());
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "Test";
                options.DefaultChallengeScheme = "Test";
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("HelpdeskAdmin", policy =>
                {
                    policy.AddAuthenticationSchemes("Test");
                    policy.RequireAuthenticatedUser();
                    policy.RequireRole(HelpdeskPermissions.HelpdeskAdmin);
                });
            });

            var app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapPresenceEndpoints();
            await app.StartAsync();

            var client = app.GetTestClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", "ordinary-user");
            return new PresenceEndpointsHarness(app, client);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.DisposeAsync();
        }
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "ordinary-user"),
                new Claim(ClaimTypes.Name, "Ordinary User")
            ],
            Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
