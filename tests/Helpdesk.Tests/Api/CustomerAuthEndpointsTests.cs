using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json.Nodes;
using Helpdesk.API;
using Helpdesk.Infrastructure.Auth.Authentik;
using Helpdesk.Shared.DTOs.Customer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Helpdesk.Tests.Api;

public sealed class CustomerAuthEndpointsTests
{
    [Fact]
    public async Task ResendInvite_ReturnsServiceUnavailable_WhenAuthentikAdminIsNotConfigured()
    {
        using var factory = CreateFactory(new ThrowingInvitationService(
            new AuthentikAdminConfigurationException("Customer invitation is not configured. Set Authentication:AuthentikAdmin:ApiToken for this environment.")));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", "HelpdeskAdmin");

        var response = await client.PostAsync("/api/v1/customers/customer-1/auth/resend-invite", null);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("Customer invitation is not configured", body, StringComparison.Ordinal);
        Assert.Contains("Authentication:AuthentikAdmin:ApiToken", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invite_ReturnsBadRequest_WhenAuthentikRejectsClientRequest()
    {
        using var factory = CreateFactory(new ThrowingInvitationService(
            new AuthentikAdminRequestException(
                HttpStatusCode.BadRequest,
                "create user",
                "api/v3/core/users/",
                "username already exists")));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", "HelpdeskAdmin");

        var response = await client.PostAsync("/api/v1/customers/customer-1/auth/invite", null);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Customer invitation request could not be completed", body, StringComparison.Ordinal);
        Assert.Contains("Authentik rejected create user (400 BadRequest): username already exists", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invite_ReturnsBadGateway_WhenAuthentikFailsServerRequest()
    {
        using var factory = CreateFactory(new ThrowingInvitationService(
            new AuthentikAdminRequestException(
                HttpStatusCode.InternalServerError,
                "create recovery link",
                "api/v3/core/users/123/recovery/",
                "upstream failure")));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", "HelpdeskAdmin");

        var response = await client.PostAsync("/api/v1/customers/customer-1/auth/invite", null);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Contains("Customer invitation request could not be completed", body, StringComparison.Ordinal);
        Assert.Contains("Authentik rejected create recovery link (500 InternalServerError): upstream failure", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateUserAsync_ThrowsAuthentikRequestException_WithSanitizedResponseBody()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"username\":[\"already exists\"]}\n", Encoding.UTF8, "application/json")
            }));
        var client = new AuthentikAdminClient(
            httpClient,
            Options.Create(new AuthentikOptions
            {
                BaseUrl = "https://auth.example.test/",
                ApiToken = "test-token"
            }));

        var ex = await Assert.ThrowsAsync<AuthentikAdminRequestException>(
            () => client.CreateUserAsync("Test User", "test@example.test"));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Equal("create user", ex.Operation);
        Assert.Equal("api/v3/core/users/", ex.Path);
        Assert.Equal("{\"username\":[\"already exists\"]}", ex.ResponseBody);
        Assert.Contains("Authentik rejected create user (400 BadRequest): {\"username\":[\"already exists\"]}", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateUserAsync_SendsEmailAsUsername()
    {
        string? requestBody = null;
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            requestBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(
                    "{\"pk\":123,\"username\":\"security@example.com\",\"name\":\"Example User\",\"email\":\"security@example.com\",\"is_active\":true}",
                    Encoding.UTF8,
                    "application/json")
            };
        }));
        var client = new AuthentikAdminClient(
            httpClient,
            Options.Create(new AuthentikOptions
            {
                BaseUrl = "https://auth.example.test/",
                ApiToken = "test-token"
            }));

        await client.CreateUserAsync(" Example User ", " security@example.com ");

        Assert.NotNull(requestBody);
        var payload = JsonNode.Parse(requestBody!)!;
        Assert.Equal("security@example.com", payload["username"]?.GetValue<string>());
        Assert.Equal("Example User", payload["name"]?.GetValue<string>());
        Assert.Equal("security@example.com", payload["email"]?.GetValue<string>());
        Assert.Equal(true, payload["is_active"]?.GetValue<bool>());
        Assert.Equal("users", payload["path"]?.GetValue<string>());
        Assert.Equal("internal", payload["type"]?.GetValue<string>());
    }

    private static WebApplicationFactory<Program> CreateFactory(ICustomerInvitationService invitationService)
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting(WebHostDefaults.EnvironmentKey, "Development");
            builder.UseEnvironment("Development");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ICustomerInvitationService>();
                services.AddSingleton(invitationService);

                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "Test";
                    options.DefaultChallengeScheme = "Test";
                }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
            });
        });

    private sealed class ThrowingInvitationService(Exception exception) : ICustomerInvitationService
    {
        public Task<CustomerAuthStatusDto> GetStatusAsync(string customerId, CancellationToken ct = default)
            => throw exception;

        public Task<CustomerAuthStatusDto> InviteAsync(string customerId, string invitedByUserId, CancellationToken ct = default)
            => throw exception;

        public Task<CustomerAuthStatusDto> ResendInviteAsync(string customerId, string invitedByUserId, CancellationToken ct = default)
            => throw exception;

        public Task<CustomerAuthStatusDto> DisableLoginAsync(string customerId, string disabledByUserId, CancellationToken ct = default)
            => throw exception;

        public Task<CustomerAuthStatusDto> SyncAuthentikAsync(string customerId, CancellationToken ct = default)
            => throw exception;
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("Authorization", out var header))
            {
                return Task.FromResult(AuthenticateResult.Fail("No authorization header"));
            }

            var parts = header.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var role = parts.Length > 1 ? parts[1] : string.Empty;
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, "Test"),
                new Claim(ClaimTypes.Role, role)
            };
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(handler(request));
    }
}
