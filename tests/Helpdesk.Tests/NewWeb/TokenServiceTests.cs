extern alias NewWeb;

using NewWeb::HelpDesk.NewWeb.Services;
using Helpdesk.Shared.Auth;
using Helpdesk.Shared.DTOs.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Xunit;

namespace Helpdesk.Tests.NewWeb;

public class TokenServiceTests
{
    private static (HttpContext context, IAuthenticationService authService) CreateContext(
        string accessToken,
        DateTime expiresAtUtc,
        string? refreshToken = null,
        params Claim[] claims)
        => CreateContextWithTicketExpiry(accessToken, expiresAtUtc, expiresAtUtc, refreshToken, claims);

    private static (HttpContext context, IAuthenticationService authService) CreateContextWithTicketExpiry(
        string accessToken,
        DateTime tokenExpiresAtUtc,
        DateTime ticketExpiresAtUtc,
        string? refreshToken = null,
        params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, "test");
        var principal = new ClaimsPrincipal(identity);

        var ticket = new AuthenticationTicket(
            principal,
            new AuthenticationProperties
            {
                ExpiresUtc = new DateTimeOffset(ticketExpiresAtUtc, TimeSpan.Zero)
            },
            CookieAuthenticationDefaults.AuthenticationScheme);

        var tokens = new List<AuthenticationToken>
        {
            new() { Name = "access_token", Value = accessToken },
            new() { Name = "expires_at", Value = tokenExpiresAtUtc.ToString("o") }
        };

        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            tokens.Add(new AuthenticationToken { Name = "refresh_token", Value = refreshToken });
        }

        ticket.Properties.StoreTokens(tokens);

        var authService = Substitute.For<IAuthenticationService>();
        authService.AuthenticateAsync(Arg.Any<HttpContext>(), Arg.Any<string?>())
            .Returns(AuthenticateResult.Success(ticket));

        var services = new ServiceCollection();
        services.AddAuthentication("test");
        services.AddSingleton(authService);

        var provider = services.BuildServiceProvider();
        var context = new DefaultHttpContext
        {
            RequestServices = provider,
            User = principal
        };

        return (context, authService);
    }

    [Fact]
    public async Task Token_Persists_Across_Requests()
    {
        var jwt = string.Join('.', "eyJhbGciOiAibm9uZSJ9", "eyJleHAiOiA0MTAyNDQ0ODAwfQ", string.Empty);
        var (context, _) = CreateContext(jwt, DateTime.UtcNow.AddHours(1));
        var accessor = new HttpContextAccessor { HttpContext = context };
        var factory = Substitute.For<IHttpClientFactory>();
        var configuration = new ConfigurationBuilder().Build();
        var systemTokenService = Substitute.For<ISystemTokenService>();

        var svc = new TokenService(accessor, factory, configuration, systemTokenService);

        var first = await svc.GetValidAccessTokenAsync();
        Assert.Equal(jwt, first);

        accessor.HttpContext = null;
        var second = await svc.GetValidAccessTokenAsync();
        Assert.Null(second);
    }

    [Fact]
    public async Task Expiring_Authentik_Token_Is_Refreshed_And_Reissued_To_Cookie()
    {
        const string currentToken = "current-access-token-rotated";
        const string nextToken = "next-access-token";
        const string currentRefreshToken = "refresh-token-rotated";
        const string nextRefreshToken = "refresh-token-2";
        var ticketExpiresAt = DateTime.UtcNow.AddHours(8);

        var (context, authService) = CreateContextWithTicketExpiry(
            currentToken,
            DateTime.UtcNow.AddMinutes(2),
            ticketExpiresAt,
            currentRefreshToken,
            new Claim(ClaimTypes.Name, "operator@example.com"));

        var accessor = new HttpContextAccessor { HttpContext = context };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AUTHENTIK_CLIENT_SECRET"] = "client-secret",
                ["Authentication:Authentik:Authority"] = "https://id.example.com/application/o/rateldesk/",
                ["Authentication:Authentik:ClientId"] = "client-id",
                ["Authentication:Authentik:ApiScope"] = "helpdesk-api"
            })
            .Build();

        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient().Returns(new HttpClient(new StubMessageHandler(_ =>
        {
            var json = """
                {
                  "access_token": "next-access-token",
                  "refresh_token": "refresh-token-2",
                  "expires_in": 3600
                }
                """;

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        })));

        var systemTokenService = Substitute.For<ISystemTokenService>();
        var svc = new TokenService(accessor, factory, configuration, systemTokenService);

        var refreshed = await svc.GetValidAccessTokenAsync();

        Assert.Equal(nextToken, refreshed);
        await authService.Received().SignInAsync(
            context,
            CookieAuthenticationDefaults.AuthenticationScheme,
            Arg.Any<ClaimsPrincipal>(),
            Arg.Is<AuthenticationProperties>(props =>
                props.GetTokenValue("access_token") == nextToken &&
                props.GetTokenValue("refresh_token") == nextRefreshToken &&
                props.ExpiresUtc.HasValue &&
                props.ExpiresUtc.Value.UtcDateTime > DateTime.UtcNow.AddHours(7)));
    }

    [Fact]
    public async Task Expiring_Authentik_Token_With_Bad_Ticket_Expiry_Restores_Helpdesk_Session_Lifetime()
    {
        const string currentToken = "current-access-token-bad-ticket";
        const string nextToken = "next-access-token";
        const string currentRefreshToken = "refresh-token-bad-ticket";
        var coupledExpiry = DateTime.UtcNow.AddMinutes(2);

        var (context, authService) = CreateContextWithTicketExpiry(
            currentToken,
            tokenExpiresAtUtc: coupledExpiry,
            ticketExpiresAtUtc: coupledExpiry,
            refreshToken: currentRefreshToken,
            new Claim(ClaimTypes.Name, "operator@example.com"));

        var accessor = new HttpContextAccessor { HttpContext = context };
        var configuration = CreateAuthentikConfiguration();
        var factory = CreateRefreshFactory("""
            {
              "access_token": "next-access-token",
              "refresh_token": "refresh-token-2",
              "expires_in": "3600"
            }
            """);
        var systemTokenService = Substitute.For<ISystemTokenService>();
        var svc = new TokenService(accessor, factory, configuration, systemTokenService);

        var refreshed = await svc.GetValidAccessTokenAsync();

        Assert.Equal(nextToken, refreshed);
        await authService.Received().SignInAsync(
            context,
            CookieAuthenticationDefaults.AuthenticationScheme,
            Arg.Any<ClaimsPrincipal>(),
            Arg.Is<AuthenticationProperties>(props =>
                props.GetTokenValue("access_token") == nextToken &&
                props.ExpiresUtc.HasValue &&
                props.ExpiresUtc.Value.UtcDateTime > DateTime.UtcNow.AddHours(7)));
    }

    [Fact]
    public async Task Expiring_Authentik_Token_Preserves_Refresh_Token_When_No_Replacement_Returned()
    {
        const string currentToken = "current-access-token-no-replacement";
        const string nextToken = "next-access-token";
        const string currentRefreshToken = "refresh-token-no-replacement";

        var (context, authService) = CreateContextWithTicketExpiry(
            currentToken,
            DateTime.UtcNow.AddMinutes(2),
            DateTime.UtcNow.AddHours(8),
            currentRefreshToken,
            new Claim(ClaimTypes.Name, "operator@example.com"));

        var accessor = new HttpContextAccessor { HttpContext = context };
        var configuration = CreateAuthentikConfiguration();
        var factory = CreateRefreshFactory("""
            {
              "access_token": "next-access-token",
              "expires_in": "3600"
            }
            """);
        var systemTokenService = Substitute.For<ISystemTokenService>();
        var svc = new TokenService(accessor, factory, configuration, systemTokenService);

        var refreshed = await svc.GetValidAccessTokenAsync();

        Assert.Equal(nextToken, refreshed);
        await authService.Received().SignInAsync(
            context,
            CookieAuthenticationDefaults.AuthenticationScheme,
            Arg.Any<ClaimsPrincipal>(),
            Arg.Is<AuthenticationProperties>(props =>
                props.GetTokenValue("access_token") == nextToken &&
                props.GetTokenValue("refresh_token") == currentRefreshToken));
    }

    [Fact]
    public async Task Expiring_Token_Uses_Authentik_Token_Endpoint_And_Deduplicates_Scopes()
    {
        const string currentToken = "current-access-token-scope";
        const string nextToken = "next-access-token";
        const string currentRefreshToken = "refresh-token-scope";
        Uri? requestedUri = null;
        string? requestBody = null;

        var (context, _) = CreateContext(
            currentToken,
            DateTime.UtcNow.AddMinutes(2),
            currentRefreshToken,
            new Claim(ClaimTypes.Name, "operator@example.com"));

        var accessor = new HttpContextAccessor { HttpContext = context };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AUTHENTIK_CLIENT_SECRET"] = "authentik-secret",
                ["Authentication:Authentik:Authority"] = "https://id.example.com/application/o/rateldesk/",
                ["Authentication:Authentik:ClientId"] = "helpdesk-dev",
                ["Authentication:Authentik:ApiScope"] = "openid helpdesk-api"
            })
            .Build();

        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient().Returns(new HttpClient(new StubMessageHandler(request =>
        {
            requestedUri = request.RequestUri;
            requestBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            var json = """
                {
                  "access_token": "next-access-token",
                  "refresh_token": "refresh-token",
                  "expires_in": "3600"
                }
                """;

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        })));

        var systemTokenService = Substitute.For<ISystemTokenService>();
        var svc = new TokenService(accessor, factory, configuration, systemTokenService);

        var refreshed = await svc.GetValidAccessTokenAsync();

        Assert.Equal(nextToken, refreshed);
        Assert.Equal("https://id.example.com/application/o/rateldesk/token/", requestedUri?.ToString());
        Assert.Contains("scope=openid+profile+email+offline_access+helpdesk-api", requestBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Expired_Token_Without_Refresh_Token_Signs_Out_Session()
    {
        var (context, authService) = CreateContext(
            "expired-access-token",
            DateTime.UtcNow.AddMinutes(-1),
            refreshToken: null,
            claims: new Claim(ClaimTypes.Name, "operator@example.com"));

        var accessor = new HttpContextAccessor { HttpContext = context };
        var factory = Substitute.For<IHttpClientFactory>();
        var configuration = new ConfigurationBuilder().Build();
        var systemTokenService = Substitute.For<ISystemTokenService>();

        var svc = new TokenService(accessor, factory, configuration, systemTokenService);

        var token = await svc.GetValidAccessTokenAsync();

        Assert.Null(token);
        await authService.Received().SignOutAsync(
            context,
            CookieAuthenticationDefaults.AuthenticationScheme,
            Arg.Any<AuthenticationProperties?>());
    }

    [Fact]
    public async Task Failed_Refresh_Grant_Signs_Out_Without_Reissuing_Cookie()
    {
        var (context, authService) = CreateContextWithTicketExpiry(
            "expired-access-token",
            DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddHours(8),
            "refresh-token-failed-grant",
            new Claim(ClaimTypes.Name, "operator@example.com"));

        var accessor = new HttpContextAccessor { HttpContext = context };
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient().Returns(new HttpClient(new StubMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.BadRequest))));
        var configuration = CreateAuthentikConfiguration();
        var systemTokenService = Substitute.For<ISystemTokenService>();

        var svc = new TokenService(accessor, factory, configuration, systemTokenService);

        var token = await svc.GetValidAccessTokenAsync();

        Assert.Null(token);
        await authService.Received().SignOutAsync(
            context,
            CookieAuthenticationDefaults.AuthenticationScheme,
            Arg.Any<AuthenticationProperties?>());
        await authService.DidNotReceive().SignInAsync(
            context,
            CookieAuthenticationDefaults.AuthenticationScheme,
            Arg.Any<ClaimsPrincipal>(),
            Arg.Any<AuthenticationProperties>());
    }

    [Fact]
    public async Task Concurrent_Refreshes_For_Same_Token_Perform_One_Authentik_Request()
    {
        const string currentRefreshToken = "refresh-token-concurrent";
        var requestCount = 0;
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient().Returns(new HttpClient(new StubMessageHandler(_ =>
        {
            Interlocked.Increment(ref requestCount);
            Thread.Sleep(100);
            var json = """
                {
                  "access_token": "next-access-token",
                  "refresh_token": "refresh-token-2",
                  "expires_in": "3600"
                }
                """;

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        })));

        var (firstContext, firstAuthService) = CreateContextWithTicketExpiry(
            "current-access-token",
            DateTime.UtcNow.AddMinutes(2),
            DateTime.UtcNow.AddHours(8),
            currentRefreshToken,
            new Claim(ClaimTypes.Name, "operator@example.com"));
        var (secondContext, secondAuthService) = CreateContextWithTicketExpiry(
            "current-access-token",
            DateTime.UtcNow.AddMinutes(2),
            DateTime.UtcNow.AddHours(8),
            currentRefreshToken,
            new Claim(ClaimTypes.Name, "operator@example.com"));

        var configuration = CreateAuthentikConfiguration();
        var systemTokenService = Substitute.For<ISystemTokenService>();
        var svc = new TokenService(new HttpContextAccessor(), factory, configuration, systemTokenService);
        var firstAuth = await firstContext.AuthenticateAsync();
        var secondAuth = await secondContext.AuthenticateAsync();

        var results = await Task.WhenAll(
            svc.TryRefreshSessionAsync(firstContext, firstAuth),
            svc.TryRefreshSessionAsync(secondContext, secondAuth));

        Assert.All(results, Assert.True);
        Assert.Equal(1, requestCount);
        await firstAuthService.Received().SignInAsync(
            firstContext,
            CookieAuthenticationDefaults.AuthenticationScheme,
            Arg.Any<ClaimsPrincipal>(),
            Arg.Is<AuthenticationProperties>(props => props.GetTokenValue("access_token") == "next-access-token"));
        await secondAuthService.Received().SignInAsync(
            secondContext,
            CookieAuthenticationDefaults.AuthenticationScheme,
            Arg.Any<ClaimsPrincipal>(),
            Arg.Is<AuthenticationProperties>(props => props.GetTokenValue("access_token") == "next-access-token"));
    }

    [Fact]
    public async Task DevelopmentOperator_Refresh_Uses_Ticket_Principal_Without_Persisting_System_Token()
    {
        const string accessToken = "old-development-access-token";
        const string nextToken = "new-development-access-token";
        var (context, authService) = CreateContextWithTicketExpiry(
            accessToken,
            DateTime.UtcNow.AddMinutes(1),
            DateTime.UtcNow.AddHours(8),
            refreshToken: null,
            claims: new[]
            {
                new Claim(ClaimTypes.Name, "local-operator"),
                new Claim("auth_mode", "development")
            });

        var accessor = new HttpContextAccessor { HttpContext = context };
        var factory = Substitute.For<IHttpClientFactory>();
        var configuration = new ConfigurationBuilder().Build();
        var systemTokenService = Substitute.For<ISystemTokenService>();
        systemTokenService.GetTokenAsync().Returns(nextToken);
        var svc = new TokenService(accessor, factory, configuration, systemTokenService);
        context.User = new ClaimsPrincipal(new ClaimsIdentity());

        var ticket = await authService.AuthenticateAsync(context, CookieAuthenticationDefaults.AuthenticationScheme);
        var refreshed = await svc.TryRefreshSessionAsync(context, ticket);

        Assert.True(refreshed);
        Assert.Equal(nextToken, Assert.IsType<string>(context.Items["Helpdesk.RefreshedAccessToken"]));
        await authService.DidNotReceive().SignInAsync(
            context,
            CookieAuthenticationDefaults.AuthenticationScheme,
            Arg.Any<ClaimsPrincipal>(),
            Arg.Any<AuthenticationProperties>());
    }

    [Fact]
    public async Task CookieValidation_Sets_ShouldRenew_After_TokenService_Refresh()
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "operator@example.com")], "test");
        var principal = new ClaimsPrincipal(identity);
        var properties = new AuthenticationProperties
        {
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
        };
        var ticket = new AuthenticationTicket(
            principal,
            properties,
            CookieAuthenticationDefaults.AuthenticationScheme);
        var httpContext = new DefaultHttpContext
        {
            User = principal
        };
        var scheme = new AuthenticationScheme(
            CookieAuthenticationDefaults.AuthenticationScheme,
            CookieAuthenticationDefaults.AuthenticationScheme,
            typeof(CookieAuthenticationHandler));
        var validateContext = new CookieValidatePrincipalContext(
            httpContext,
            scheme,
            new CookieAuthenticationOptions(),
            ticket);
        var events = new CookieOidcSessionEvents(
            new RefreshingTokenService(),
            NullLogger<CookieOidcSessionEvents>.Instance);

        await events.ValidatePrincipal(validateContext);

        Assert.True(validateContext.ShouldRenew);
    }

    [Fact]
    public async Task CookieValidation_Reprojects_Oidc_access_from_the_api()
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "operator@example.test"), new Claim(ClaimTypes.Role, HelpdeskPermissions.ChangeManager),
             new Claim(ClaimTypes.Role, HelpdeskPermissions.ChangeWrite), new Claim("roles", AuthentikRbacGroups.HelpdeskAdmin),
             new Claim("scoped_permission", "Change.Write|org-old")],
            "test");
        var principal = new ClaimsPrincipal(identity);
        var properties = new AuthenticationProperties();
        properties.StoreTokens([new AuthenticationToken { Name = "access_token", Value = "access-token" }, new AuthenticationToken { Name = "expires_at", Value = DateTime.UtcNow.AddHours(1).ToString("O") }]);
        var ticket = new AuthenticationTicket(principal, properties, CookieAuthenticationDefaults.AuthenticationScheme);
        var context = new DefaultHttpContext { User = principal };
        var scheme = new AuthenticationScheme(CookieAuthenticationDefaults.AuthenticationScheme, CookieAuthenticationDefaults.AuthenticationScheme, typeof(CookieAuthenticationHandler));
        var validation = new CookieValidatePrincipalContext(context, scheme, new CookieAuthenticationOptions(), ticket);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("SystemApiNoAuth").Returns(new HttpClient(new StubMessageHandler(request =>
        {
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("access-token", request.Headers.Authorization?.Parameter);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new CurrentUserAccessDto(true, "Operator", "operator@example.test", "org-a", "Organization A", null, false,
                    [HelpdeskRoleBundles.User], [HelpdeskPermissions.IncidentRead], ["org-a"], [])
                { UsesScopedPermissions = true, ScopedPermissionGrants = [new Helpdesk.Shared.Services.ScopedPermissionGrant(HelpdeskPermissions.IncidentRead, "org-a")] })
            };
        }))
        { BaseAddress = new Uri("https://api.example.test") });
        var events = new CookieOidcSessionEvents(
            new RefreshingTokenService(),
            NullLogger<CookieOidcSessionEvents>.Instance,
            factory);

        await events.ValidatePrincipal(validation);

        Assert.True(validation.ShouldRenew);
        Assert.True(principal.IsInRole(HelpdeskPermissions.IncidentRead));
        Assert.False(principal.IsInRole(HelpdeskPermissions.ChangeManager));
        Assert.False(principal.IsInRole(HelpdeskPermissions.ChangeWrite));
        Assert.False(Helpdesk.Shared.Services.CurrentUserAccessProfile.FromClaims(principal).IsHelpdeskAdmin);
        Assert.True(principal.HasClaim("permission_scope_mode", "scoped"));
        Assert.DoesNotContain(principal.Claims, claim => claim.Type == "scoped_permission" && claim.Value.Contains("org-old", StringComparison.Ordinal));
        Assert.Contains(principal.Claims, claim => claim.Type == "organization_id" && claim.Value == "org-a");
    }

    [Fact]
    public async Task Local_cookie_validation_rejects_a_session_rejected_by_the_API()
    {
        const string cookieName = "__Host-RatelDesk.Local";
        const string cookieValue = "protected-local-session";
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "local-user"), new Claim("auth_mode", "local")],
            "RatelDeskLocal"));
        var properties = new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8) };
        var ticket = new AuthenticationTicket(principal, properties, "RatelDeskLocal");
        var authentication = Substitute.For<IAuthenticationService>();
        var services = new ServiceCollection();
        services.AddSingleton(authentication);
        var context = new DefaultHttpContext { RequestServices = services.BuildServiceProvider(), User = principal };
        context.Request.Headers.Cookie = $"{cookieName}={cookieValue}";
        var scheme = new AuthenticationScheme("RatelDeskLocal", "RatelDeskLocal", typeof(CookieAuthenticationHandler));
        var validation = new CookieValidatePrincipalContext(context, scheme, new CookieAuthenticationOptions(), ticket);
        var factory = Substitute.For<IHttpClientFactory>();
        string? forwardedCookie = null;
        factory.CreateClient("SystemApiNoAuth").Returns(new HttpClient(new StubMessageHandler(request =>
        {
            forwardedCookie = request.Headers.GetValues("Cookie").Single();
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }))
        { BaseAddress = new Uri("https://api.example.test") });
        var events = new CookieLocalSessionEvents(
            factory,
            new ConfigurationBuilder().Build(),
            NullLogger<CookieLocalSessionEvents>.Instance);

        await events.ValidatePrincipal(validation);

        Assert.Equal($"{cookieName}={cookieValue}", forwardedCookie);
        Assert.True(validation.Principal is null);
        await authentication.Received().SignOutAsync(context, "RatelDeskLocal", Arg.Any<AuthenticationProperties?>());
    }

    [Fact]
    public async Task AiAgent_Session_Remains_Valid_Without_Azure_Refresh()
    {
        const string accessToken = "ai-agent-access-token";
        var (context, authService) = CreateContext(
            accessToken,
            DateTime.UtcNow.AddMinutes(10),
            refreshToken: null,
            claims: new[]
            {
                new Claim(ClaimTypes.Name, "svc-aiagent-dev"),
                new Claim("auth_mode", "ai_agent")
            });

        var accessor = new HttpContextAccessor { HttpContext = context };
        var factory = Substitute.For<IHttpClientFactory>();
        var configuration = new ConfigurationBuilder().Build();
        var systemTokenService = Substitute.For<ISystemTokenService>();

        var svc = new TokenService(accessor, factory, configuration, systemTokenService);

        var token = await svc.GetValidAccessTokenAsync();

        Assert.Equal(accessToken, token);
        await authService.DidNotReceive().SignOutAsync(
            context,
            CookieAuthenticationDefaults.AuthenticationScheme,
            Arg.Any<AuthenticationProperties?>());
    }

    [Fact]
    public async Task Local_Session_Does_Not_Require_An_Oidc_Access_Token()
    {
        var (context, authService) = CreateContext(
            accessToken: string.Empty,
            expiresAtUtc: DateTime.UtcNow.AddHours(1),
            claims: [new Claim("auth_mode", "local")]);
        var accessor = new HttpContextAccessor { HttpContext = context };
        var service = new TokenService(
            accessor,
            Substitute.For<IHttpClientFactory>(),
            new ConfigurationBuilder().Build(),
            Substitute.For<ISystemTokenService>());

        var token = await service.GetValidAccessTokenAsync();

        Assert.Null(token);
        await authService.DidNotReceive().SignOutAsync(
            context,
            CookieAuthenticationDefaults.AuthenticationScheme,
            Arg.Any<AuthenticationProperties?>());
    }

    [Fact]
    public async Task Local_Session_Cookie_Is_Relayed_To_The_Api()
    {
        const string cookieValue = "protected-local-session";
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("auth_mode", "local")],
            "RatelDeskLocal"));
        context.Request.Headers.Cookie = $"__Host-RatelDesk.Local={cookieValue}";
        var accessor = new HttpContextAccessor { HttpContext = context };
        var tokenService = Substitute.For<ITokenService>();
        tokenService.GetValidAccessTokenAsync().Returns((string?)null);
        string? forwardedCookie = null;
        var handler = new TokenAuthorizationHandler(
            tokenService,
            accessor,
            new ConfigurationBuilder().Build())
        {
            InnerHandler = new StubMessageHandler(request =>
            {
                forwardedCookie = request.Headers.GetValues("Cookie").Single();
                return new HttpResponseMessage(HttpStatusCode.OK);
            })
        };

        using var client = new HttpClient(handler);
        await client.GetAsync("https://api.example.test/api/v1/auth/me");

        Assert.Equal($"__Host-RatelDesk.Local={cookieValue}", forwardedCookie);
    }

    [Fact]
    public void Local_cookie_forwarding_includes_ticket_chunks_and_excludes_other_browser_cookies()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = "RatelDesk.Local=chunks-2; RatelDesk.LocalC1=first; RatelDesk.LocalC2=second; unrelated=private; RatelDesk.LocalExtra=excluded";
        var header = LocalSessionCookieForwarder.GetHeader(context, "RatelDesk.Local");
        Assert.Equal("RatelDesk.Local=chunks-2; RatelDesk.LocalC1=first; RatelDesk.LocalC2=second", header);
    }

    [Fact]
    public async Task Web_local_validation_relays_only_the_api_renewal_and_does_not_renew_a_stale_ticket()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("auth_mode", "local"), new Claim(ClaimTypes.Role, HelpdeskPermissions.IncidentWrite)], "RatelDeskLocal"));
        var context = new DefaultHttpContext { User = principal };
        context.Request.Headers.Cookie = "__Host-RatelDesk.Local=old-ticket";
        var scheme = new AuthenticationScheme("RatelDeskLocal", "RatelDeskLocal", typeof(CookieAuthenticationHandler));
        var ticket = new AuthenticationTicket(principal, new AuthenticationProperties(), "RatelDeskLocal");
        var validation = new CookieValidatePrincipalContext(context, scheme, new CookieAuthenticationOptions(), ticket) { ShouldRenew = true };
        using var client = new HttpClient(new StubMessageHandler(request =>
        {
            Assert.Equal("__Host-RatelDesk.Local=old-ticket", request.Headers.GetValues("Cookie").Single());
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new CurrentUserAccessDto(true, "Reader", "reader@example.test", "org", "Org", null, false, [], [HelpdeskPermissions.IncidentRead], ["org"], [])
                { UsesScopedPermissions = true })
            };
            response.Headers.TryAddWithoutValidation("Set-Cookie", "__Host-RatelDesk.Local=renewed-ticket; path=/; secure; httponly");
            return response;
        })) { BaseAddress = new Uri("https://api.example.test") };
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("SystemApiNoAuth").Returns(client);
        var events = new CookieLocalSessionEvents(factory, new ConfigurationBuilder().Build(), NullLogger<CookieLocalSessionEvents>.Instance);

        await events.ValidatePrincipal(validation);

        Assert.False(validation.ShouldRenew);
        Assert.NotNull(validation.Principal);
        Assert.False(validation.Principal.IsInRole(HelpdeskPermissions.IncidentWrite));
        Assert.True(validation.Principal.IsInRole(HelpdeskPermissions.IncidentRead));
        Assert.Contains("renewed-ticket", context.Response.Headers.SetCookie.ToString(), StringComparison.Ordinal);
    }

    private sealed class StubMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_handler(request));
    }

    private sealed class RefreshingTokenService : ITokenService
    {
        public Task<string?> GetValidAccessTokenAsync() => Task.FromResult<string?>("refreshed-access-token");

        public Task<bool> TryRefreshSessionAsync(HttpContext ctx, AuthenticateResult auth, CancellationToken cancellationToken = default)
        {
            ctx.Items["Helpdesk.SessionRefreshed"] = true;
            return Task.FromResult(true);
        }
    }

    private static IConfiguration CreateAuthentikConfiguration()
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AUTHENTIK_CLIENT_SECRET"] = "client-secret",
                ["Authentication:Authentik:Authority"] = "https://id.example.com/application/o/rateldesk/",
                ["Authentication:Authentik:ClientId"] = "client-id",
                ["Authentication:Authentik:ApiScope"] = "helpdesk-api"
            })
            .Build();

    private static IHttpClientFactory CreateRefreshFactory(string json)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient().Returns(new HttpClient(new StubMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            })));
        return factory;
    }
}
