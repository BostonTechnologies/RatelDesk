extern alias NewWeb;

using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Helpdesk.Shared.DTOs.Auth;
using Microsoft.Extensions.Logging.Abstractions;
using NewWeb::HelpDesk.NewWeb.Services;
using NSubstitute;

namespace Helpdesk.Tests.NewWeb;

public sealed class UserProvisioningServiceTests
{
    [Fact]
    public async Task Provisioning_sends_the_verified_user_access_token_without_a_claims_body_or_system_credentials()
    {
        string? authorization = null;
        HttpContent? body = null;
        using var client = new HttpClient(new CaptureHandler(request =>
        {
            authorization = request.Headers.Authorization?.ToString();
            body = request.Content;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new CurrentUserAccessDto(true, "Customer", "customer@example.test", "tenant", "Tenant", "contact", false, [], [], [], []))
            };
        })) { BaseAddress = new Uri("https://api.example.test") };
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("SystemApiNoAuth").Returns(client);
        var service = new UserProvisioningService(factory, NullLogger<UserProvisioningService>.Instance);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("email", "customer@example.test")], "oidc"));

        var access = await service.EnsureUserAccessAsync(principal, "verified-user-access-token", CancellationToken.None);

        Assert.Equal("Bearer verified-user-access-token", authorization);
        Assert.Null(body);
        Assert.Equal("contact", access!.CustomerId);
        factory.DidNotReceive().CreateClient("SystemApi");
    }

    [Fact]
    public async Task Provisioning_never_substitutes_a_system_token_when_the_user_token_is_missing()
    {
        var factory = Substitute.For<IHttpClientFactory>();
        var service = new UserProvisioningService(factory, NullLogger<UserProvisioningService>.Instance);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("email", "customer@example.test")], "oidc"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.EnsureUserAccessAsync(principal, "", CancellationToken.None));
        factory.DidNotReceiveWithAnyArgs().CreateClient(default!);
    }

    private sealed class CaptureHandler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(send(request));
    }
}
