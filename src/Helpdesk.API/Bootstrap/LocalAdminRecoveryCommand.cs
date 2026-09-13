using Helpdesk.Infrastructure;
using Helpdesk.Infrastructure.Identity;
using System.Data.Common;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Helpdesk.API.Bootstrap;

public static class LocalAdminRecoveryCommand
{
    public static async Task<string?> GenerateActivationTokenAsync(
        IConfiguration configuration,
        string email,
        CancellationToken cancellationToken = default)
    {
        var keyRingPath = configuration["DataProtection:KeyRingPath"]
                          ?? Path.Combine(Path.GetTempPath(), "rateldesk", "keys");
        var applicationName = configuration["DataProtection:ApplicationName"] ?? "Helpdesk-Keyring";
        Directory.CreateDirectory(keyRingPath);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath))
            .SetApplicationName(applicationName);
        services.AddHelpdeskInfrastructure(configuration);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        try
        {
            var identityDb = scope.ServiceProvider.GetRequiredService<RatelDeskIdentityDbContext>();
            if (identityDb.Database.IsSqlite())
            {
                var source = identityDb.Database.GetDbConnection().DataSource;
                if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
                {
                    return null;
                }
            }

            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var administrator = await users.Users.SingleOrDefaultAsync(user =>
                user.Email == email && user.IsInstanceAdministrator, cancellationToken);
            if (administrator is null)
            {
                return null;
            }

            administrator.IsEnabled = true;
            administrator.DisabledAtUtc = null;
            administrator.AuthorizationRevision++;
            var update = await users.UpdateAsync(administrator);
            if (!update.Succeeded)
            {
                return null;
            }

            return await users.GeneratePasswordResetTokenAsync(administrator);
        }
        catch (DbException)
        {
            return null;
        }
        catch (InvalidOperationException exception) when (exception.InnerException is DbException)
        {
            return null;
        }
    }
}
