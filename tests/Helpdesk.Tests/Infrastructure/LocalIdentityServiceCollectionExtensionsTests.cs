using Helpdesk.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Helpdesk.Tests.Infrastructure;

public sealed class LocalIdentityServiceCollectionExtensionsTests
{
    [Fact]
    public void Local_identity_uses_a_passphrase_policy_without_composition_rules()
    {
        var services = new ServiceCollection();
        services.AddDbContext<RatelDeskIdentityDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddRatelDeskLocalIdentity();
        using var provider = services.BuildServiceProvider();

        var password = provider.GetRequiredService<IOptions<IdentityOptions>>().Value.Password;

        Assert.Equal(15, password.RequiredLength);
        Assert.False(password.RequireDigit);
        Assert.False(password.RequireLowercase);
        Assert.False(password.RequireUppercase);
        Assert.False(password.RequireNonAlphanumeric);
    }

    [Fact]
    public async Task Local_identity_persists_a_user_with_the_configured_password_policy()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<RatelDeskIdentityDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddRatelDeskLocalIdentity();

        await using var provider = services.BuildServiceProvider();
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RatelDeskIdentityDbContext>();
            await db.Database.EnsureCreatedAsync();

            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var create = await users.CreateAsync(
                new ApplicationUser { UserName = "admin@example.test", Email = "admin@example.test", DisplayName = "Instance Admin" },
                "correct horse battery staple");

            Assert.True(create.Succeeded, string.Join(", ", create.Errors.Select(error => error.Description)));
            var user = await users.FindByEmailAsync("admin@example.test");
            Assert.NotNull(user);
            Assert.True(await users.CheckPasswordAsync(user, "correct horse battery staple"));
            Assert.NotEqual("correct horse battery staple", user.PasswordHash);
        }
    }
}
