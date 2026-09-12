using Helpdesk.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Helpdesk.Tests.Infrastructure;

public sealed class LocalIdentityServiceCollectionExtensionsTests
{
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
                "Strong!Passw0rd");

            Assert.True(create.Succeeded, string.Join(", ", create.Errors.Select(error => error.Description)));
            var user = await users.FindByEmailAsync("admin@example.test");
            Assert.NotNull(user);
            Assert.True(await users.CheckPasswordAsync(user, "Strong!Passw0rd"));
            Assert.NotEqual("Strong!Passw0rd", user.PasswordHash);
        }
    }
}
