using System.Security.Claims;
using Helpdesk.API.Endpoints.Authentication;
using Helpdesk.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Helpdesk.Tests.Api;

public sealed class LocalSessionValidatorTests
{
    [Fact]
    public async Task Bearer_stream_cannot_outlive_its_original_access_token()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("exp", DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds().ToString())], "oidc"))
        };
        Assert.False(await LocalSessionValidator.IsValidAsync(context));
    }

    [Fact]
    public async Task Existing_stream_session_detects_updated_stamp_even_when_the_user_was_already_tracked()
    {
        var authentication = Substitute.For<IAuthenticationService>();
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton(authentication);
        var databaseName = Guid.NewGuid().ToString();
        services.AddDbContext<RatelDeskIdentityDbContext>(options => options.UseInMemoryDatabase(databaseName));
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RatelDeskIdentityDbContext>();
        var user = new ApplicationUser { Id = "local", SecurityStamp = "old-stamp", AuthorizationRevision = 1 };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "local"), new Claim("auth_mode", "local"),
            new Claim("security_stamp", "old-stamp"), new Claim("authorization_revision", "1")], LocalAuthenticationOptions.Scheme));
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider, User = principal };
        authentication.AuthenticateAsync(context, LocalAuthenticationOptions.Scheme).Returns(AuthenticateResult.Success(
            new AuthenticationTicket(principal, new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(5) }, LocalAuthenticationOptions.Scheme)));
        Assert.True(await LocalSessionValidator.IsValidAsync(context));

        await using (var otherScope = provider.CreateAsyncScope())
        {
            var otherDb = otherScope.ServiceProvider.GetRequiredService<RatelDeskIdentityDbContext>();
            var changed = await otherDb.Users.SingleAsync();
            changed.SecurityStamp = "reset-stamp";
            await otherDb.SaveChangesAsync();
        }

        Assert.Equal("old-stamp", user.SecurityStamp);
        Assert.False(await LocalSessionValidator.IsValidAsync(context));
    }

    [Fact]
    public async Task Stream_session_expires_without_waiting_for_a_new_http_request()
    {
        var authentication = Substitute.For<IAuthenticationService>();
        await using var services = new ServiceCollection().AddSingleton(authentication).BuildServiceProvider();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("auth_mode", "local")], LocalAuthenticationOptions.Scheme));
        var context = new DefaultHttpContext { RequestServices = services, User = principal };
        authentication.AuthenticateAsync(context, LocalAuthenticationOptions.Scheme).Returns(AuthenticateResult.Success(
            new AuthenticationTicket(principal, new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(-1) }, LocalAuthenticationOptions.Scheme)));
        Assert.False(await LocalSessionValidator.IsValidAsync(context));
    }
}
