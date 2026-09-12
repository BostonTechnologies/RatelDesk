using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Helpdesk.API;
using Helpdesk.API.Endpoints.Authentication;
using Helpdesk.Infrastructure.Identity;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.DTOs.Auth;
using Helpdesk.Shared.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Helpdesk.Tests.Api;

public sealed class LocalAuthenticationEndpointsTests : IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly InMemoryDatabaseRoot _applicationDatabaseRoot = new();
    private readonly ServiceProvider _applicationDatabaseProvider = new ServiceCollection()
        .AddEntityFrameworkInMemoryDatabase()
        .BuildServiceProvider();
    private readonly InMemoryDatabaseRoot _identityDatabaseRoot = new();
    private readonly ServiceProvider _identityDatabaseProvider = new ServiceCollection()
        .AddEntityFrameworkInMemoryDatabase()
        .BuildServiceProvider();

    public LocalAuthenticationEndpointsTests()
    {
        var identityDatabaseName = $"local-auth-identity-{Guid.NewGuid():N}";
        var applicationDatabaseName = $"local-auth-application-{Guid.NewGuid():N}";
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting(WebHostDefaults.EnvironmentKey, "Development");
            builder.UseSetting("Authentication:Mode", "Local");
            builder.UseSetting("Authentication:AllowInsecureLocalhost", "true");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Mode"] = "Local",
                ["Authentication:AllowInsecureLocalhost"] = "true"
            }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<HelpdeskDbContext>>();
                services.AddDbContext<HelpdeskDbContext>(options => options
                    .UseInMemoryDatabase(applicationDatabaseName, _applicationDatabaseRoot)
                    .UseInternalServiceProvider(_applicationDatabaseProvider));
                services.RemoveAll<DbContextOptions<RatelDeskIdentityDbContext>>();
                services.AddDbContext<RatelDeskIdentityDbContext>(options => options
                    .UseInMemoryDatabase(identityDatabaseName, _identityDatabaseRoot)
                    .UseInternalServiceProvider(_identityDatabaseProvider));
            });
        });
    }

    public async Task InitializeAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RatelDeskIdentityDbContext>();
        await db.Database.EnsureCreatedAsync();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var result = await users.CreateAsync(
            new ApplicationUser { UserName = "admin@example.test", Email = "admin@example.test", DisplayName = "Instance Admin", IsInstanceAdministrator = true },
            "correct horse battery staple");
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Description)));
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _applicationDatabaseProvider.DisposeAsync();
        await _identityDatabaseProvider.DisposeAsync();
    }

    [Fact]
    public async Task Local_login_issues_a_cookie_that_authenticates_subsequent_api_requests()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

        var login = await client.PostAsJsonAsync("/api/v1/local-auth/login", new LocalAuthenticationEndpoints.LocalLoginRequest(
            "admin@example.test",
            "correct horse battery staple"));

        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        Assert.Contains(login.Headers, header => string.Equals(header.Key, "Set-Cookie", StringComparison.OrdinalIgnoreCase));

        var currentUser = await client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.OK, currentUser.StatusCode);
        var access = await currentUser.Content.ReadFromJsonAsync<CurrentUserAccessDto>();
        Assert.True(access!.IsHelpdeskAdmin);
    }

    [Fact]
    public async Task Last_enabled_instance_administrator_cannot_be_disabled()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var login = await client.PostAsJsonAsync("/api/v1/local-auth/login", new LocalAuthenticationEndpoints.LocalLoginRequest(
            "admin@example.test",
            "correct horse battery staple"));
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var administrator = await users.FindByEmailAsync("admin@example.test");

        var disable = await client.PostAsync($"/api/v1/local-auth/users/{administrator!.Id}/disable", content: null);

        Assert.Equal(HttpStatusCode.Conflict, disable.StatusCode);
        Assert.True((await users.FindByIdAsync(administrator.Id))!.IsEnabled);
    }

    [Fact]
    public async Task Disabled_local_account_loses_an_existing_cookie_session()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var operatorCreate = await users.CreateAsync(
            new ApplicationUser { UserName = "operator@example.test", Email = "operator@example.test", DisplayName = "Operator" },
            "correct horse battery staple");
        Assert.True(operatorCreate.Succeeded, string.Join(", ", operatorCreate.Errors.Select(error => error.Description)));
        var operatorUser = await users.FindByEmailAsync("operator@example.test");

        using var administratorClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        using var operatorClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        Assert.Equal(HttpStatusCode.NoContent, (await administratorClient.PostAsJsonAsync("/api/v1/local-auth/login", new LocalAuthenticationEndpoints.LocalLoginRequest(
            "admin@example.test", "correct horse battery staple"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await operatorClient.PostAsJsonAsync("/api/v1/local-auth/login", new LocalAuthenticationEndpoints.LocalLoginRequest(
            "operator@example.test", "correct horse battery staple"))).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await administratorClient.PostAsync($"/api/v1/local-auth/users/{operatorUser!.Id}/disable", content: null)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await operatorClient.GetAsync("/api/v1/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Administrator_can_reenable_a_disabled_local_account()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var operatorCreate = await users.CreateAsync(
            new ApplicationUser { UserName = "reenable.operator@example.test", Email = "reenable.operator@example.test", DisplayName = "Reenable Operator" },
            "correct horse battery staple");
        Assert.True(operatorCreate.Succeeded, string.Join(", ", operatorCreate.Errors.Select(error => error.Description)));
        var operatorUser = await users.FindByEmailAsync("reenable.operator@example.test");

        using var administratorClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        Assert.Equal(HttpStatusCode.NoContent, (await administratorClient.PostAsJsonAsync("/api/v1/local-auth/login", new LocalAuthenticationEndpoints.LocalLoginRequest(
            "admin@example.test", "correct horse battery staple"))).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await administratorClient.PostAsync($"/api/v1/local-auth/users/{operatorUser!.Id}/disable", content: null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await administratorClient.PostAsync($"/api/v1/local-auth/users/{operatorUser.Id}/enable", content: null)).StatusCode);

        using var operatorClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var login = await operatorClient.PostAsJsonAsync("/api/v1/local-auth/login", new LocalAuthenticationEndpoints.LocalLoginRequest(
            "reenable.operator@example.test", "correct horse battery staple"));

        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        Assert.True((await users.FindByIdAsync(operatorUser.Id))!.IsEnabled);
    }

    [Fact]
    public async Task Administrator_can_create_and_activate_a_local_account_with_a_single_use_token()
    {
        const string organizationId = "local-account-organization";
        await using (var setupScope = _factory.Services.CreateAsyncScope())
        {
            var setupDb = setupScope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();
            setupDb.Organizations.Add(new Organization { Id = organizationId, Name = "Local account organization" });
            await setupDb.SaveChangesAsync();
        }

        using var administratorClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        Assert.Equal(HttpStatusCode.NoContent, (await administratorClient.PostAsJsonAsync("/api/v1/local-auth/login", new LocalAuthenticationEndpoints.LocalLoginRequest(
            "admin@example.test", "correct horse battery staple"))).StatusCode);

        var create = await administratorClient.PostAsJsonAsync("/api/v1/local-auth/users", new LocalAuthenticationEndpoints.CreateLocalAccountRequest(
            "New Operator", "new.operator@example.test")
        {
            OrganizationId = organizationId,
            IsTestUser = true
        });
        var activation = await create.Content.ReadFromJsonAsync<LocalAuthenticationEndpoints.LocalAccountActivationResponse>();

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.NotNull(activation);
        Assert.False(string.IsNullOrWhiteSpace(activation.ActivationToken));
        await using var scope = _factory.Services.CreateAsyncScope();
        var domainDb = scope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();
        var domainUser = await domainDb.Users.SingleAsync(user => user.Id == activation.UserId && user.Email == activation.Email);
        Assert.Equal(organizationId, domainUser.OrganizationId);
        Assert.True(domainUser.IsTestUser);

        var activate = await administratorClient.PostAsJsonAsync("/api/v1/local-auth/activate", new LocalAuthenticationEndpoints.ActivateLocalAccountRequest(
            activation.Email, activation.ActivationToken, "another secure passphrase"));
        var replay = await administratorClient.PostAsJsonAsync("/api/v1/local-auth/activate", new LocalAuthenticationEndpoints.ActivateLocalAccountRequest(
            activation.Email, activation.ActivationToken, "a different secure passphrase"));
        using var accountClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var login = await accountClient.PostAsJsonAsync("/api/v1/local-auth/login", new LocalAuthenticationEndpoints.LocalLoginRequest(
            activation.Email, "another secure passphrase"));
        var access = await accountClient.GetFromJsonAsync<CurrentUserAccessDto>("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.NoContent, activate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        Assert.NotNull(access);
        Assert.Equal(organizationId, access.PrimaryOrganizationId);
        Assert.Contains(organizationId, access.AllowedOrganizationIds);
        Assert.Contains(Helpdesk.Shared.Auth.HelpdeskPermissions.SelfServiceUser, access.Permissions);
    }

    [Fact]
    public async Task Administrator_cannot_create_a_local_account_for_an_unknown_or_disabled_organization()
    {
        await using (var setupScope = _factory.Services.CreateAsyncScope())
        {
            var setupDb = setupScope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();
            setupDb.Organizations.Add(new Organization { Id = "disabled-local-account-organization", Name = "Disabled organization", IsEnabled = false });
            await setupDb.SaveChangesAsync();
        }

        using var administratorClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        Assert.Equal(HttpStatusCode.NoContent, (await administratorClient.PostAsJsonAsync("/api/v1/local-auth/login", new LocalAuthenticationEndpoints.LocalLoginRequest(
            "admin@example.test", "correct horse battery staple"))).StatusCode);

        var unknownOrganization = await administratorClient.PostAsJsonAsync("/api/v1/local-auth/users", new LocalAuthenticationEndpoints.CreateLocalAccountRequest(
            "Unknown Organization Operator", "unknown.organization.operator@example.test")
        {
            OrganizationId = "missing-organization"
        });
        var disabledOrganization = await administratorClient.PostAsJsonAsync("/api/v1/local-auth/users", new LocalAuthenticationEndpoints.CreateLocalAccountRequest(
            "Disabled Organization Operator", "disabled.organization.operator@example.test")
        {
            OrganizationId = "disabled-local-account-organization"
        });

        Assert.Equal(HttpStatusCode.BadRequest, unknownOrganization.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, disabledOrganization.StatusCode);
        await using var scope = _factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Null(await users.FindByEmailAsync("unknown.organization.operator@example.test"));
        Assert.Null(await users.FindByEmailAsync("disabled.organization.operator@example.test"));
    }

    [Fact]
    public async Task Administrator_can_assign_the_technician_bundle_to_a_local_account()
    {
        const string organizationId = "local-technician-organization";
        await using (var setupScope = _factory.Services.CreateAsyncScope())
        {
            var setupDb = setupScope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();
            setupDb.Organizations.Add(new Organization { Id = organizationId, Name = "Local technician organization" });
            await setupDb.SaveChangesAsync();
        }

        using var administratorClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        Assert.Equal(HttpStatusCode.NoContent, (await administratorClient.PostAsJsonAsync("/api/v1/local-auth/login", new LocalAuthenticationEndpoints.LocalLoginRequest(
            "admin@example.test", "correct horse battery staple"))).StatusCode);

        var create = await administratorClient.PostAsJsonAsync("/api/v1/local-auth/users", new LocalAuthenticationEndpoints.CreateLocalAccountRequest(
            "Local Technician", "local.technician@example.test")
        {
            OrganizationId = organizationId,
            Role = "Technician"
        });
        var activation = await create.Content.ReadFromJsonAsync<LocalAuthenticationEndpoints.LocalAccountActivationResponse>();
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.NotNull(activation);

        Assert.Equal(HttpStatusCode.NoContent, (await administratorClient.PostAsJsonAsync("/api/v1/local-auth/activate", new LocalAuthenticationEndpoints.ActivateLocalAccountRequest(
            activation!.Email, activation.ActivationToken, "another secure passphrase"))).StatusCode);
        using var technicianClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        Assert.Equal(HttpStatusCode.NoContent, (await technicianClient.PostAsJsonAsync("/api/v1/local-auth/login", new LocalAuthenticationEndpoints.LocalLoginRequest(
            activation.Email, "another secure passphrase"))).StatusCode);

        var access = await technicianClient.GetFromJsonAsync<CurrentUserAccessDto>("/api/v1/auth/me");

        Assert.NotNull(access);
        Assert.Equal(organizationId, access.PrimaryOrganizationId);
        Assert.Contains(Helpdesk.Shared.Auth.HelpdeskPermissions.IncidentManager, access.Permissions);
        Assert.Contains(Helpdesk.Shared.Auth.HelpdeskPermissions.RequestManager, access.Permissions);
        Assert.Contains(Helpdesk.Shared.Auth.HelpdeskPermissions.ChangeManager, access.Permissions);
    }

    [Fact]
    public async Task Local_login_requires_an_authenticator_code_after_two_factor_is_enabled()
    {
        using var setupClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        Assert.Equal(HttpStatusCode.NoContent, (await setupClient.PostAsJsonAsync("/api/v1/local-auth/login", new LocalAuthenticationEndpoints.LocalLoginRequest(
            "admin@example.test", "correct horse battery staple"))).StatusCode);

        var setup = await setupClient.PostAsync("/api/v1/local-auth/two-factor/setup", content: null);
        Assert.Equal(HttpStatusCode.OK, setup.StatusCode);
        var setupResult = await setup.Content.ReadFromJsonAsync<LocalAuthenticationEndpoints.AuthenticatorSetupResponse>();
        Assert.False(string.IsNullOrWhiteSpace(setupResult?.SharedKey));

        var authenticatorCode = CreateTotp(setupResult!.SharedKey);
        Assert.Equal(HttpStatusCode.NoContent, (await setupClient.PostAsJsonAsync("/api/v1/local-auth/login", new LocalAuthenticationEndpoints.LocalLoginRequest(
            "admin@example.test", "correct horse battery staple"))).StatusCode);

        var enable = await setupClient.PostAsJsonAsync("/api/v1/local-auth/two-factor/enable", new LocalAuthenticationEndpoints.EnableTwoFactorRequest(authenticatorCode));
        Assert.Equal(HttpStatusCode.OK, enable.StatusCode);
        var recoveryCodes = await enable.Content.ReadFromJsonAsync<LocalAuthenticationEndpoints.TwoFactorRecoveryCodesResponse>();
        Assert.Equal(10, recoveryCodes!.RecoveryCodes.Count);

        using var loginClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var missingCode = await loginClient.PostAsJsonAsync("/api/v1/local-auth/login", new LocalAuthenticationEndpoints.LocalLoginRequest(
            "admin@example.test", "correct horse battery staple"));
        var withAuthenticator = await loginClient.PostAsJsonAsync("/api/v1/local-auth/login", new LocalAuthenticationEndpoints.LocalLoginRequest(
            "admin@example.test", "correct horse battery staple", TwoFactorCode: authenticatorCode));
        using var recoveryClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var withRecoveryCode = await recoveryClient.PostAsJsonAsync("/api/v1/local-auth/login", new LocalAuthenticationEndpoints.LocalLoginRequest(
            "admin@example.test", "correct horse battery staple", TwoFactorCode: recoveryCodes.RecoveryCodes[0]));

        Assert.Equal(HttpStatusCode.Unauthorized, missingCode.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, withAuthenticator.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, withRecoveryCode.StatusCode);
    }

    private static string CreateTotp(string sharedKey)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bits = 0;
        var bitCount = 0;
        var bytes = new List<byte>();
        foreach (var character in sharedKey.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant())
        {
            var value = alphabet.IndexOf(character);
            if (value < 0)
            {
                throw new ArgumentException("The authenticator key is not valid base32.", nameof(sharedKey));
            }

            bits = (bits << 5) | value;
            bitCount += 5;
            if (bitCount < 8)
            {
                continue;
            }

            bitCount -= 8;
            bytes.Add((byte)(bits >> bitCount));
            bits &= (1 << bitCount) - 1;
        }

        var counter = (ulong)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30);
        Span<byte> counterBytes = stackalloc byte[8];
        for (var index = counterBytes.Length - 1; index >= 0; index--)
        {
            counterBytes[index] = (byte)counter;
            counter >>= 8;
        }

        var hash = HMACSHA1.HashData(bytes.ToArray(), counterBytes);
        var offset = hash[^1] & 0x0f;
        var value32 = ((hash[offset] & 0x7f) << 24) |
                      (hash[offset + 1] << 16) |
                      (hash[offset + 2] << 8) |
                      hash[offset + 3];
        return (value32 % 1_000_000).ToString("D6", global::System.Globalization.CultureInfo.InvariantCulture);
    }
}
