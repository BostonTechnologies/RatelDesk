using Microsoft.AspNetCore.DataProtection;

namespace Helpdesk.API.Bootstrap;

/// <summary>
/// Performs an operator-invoked unattended initialization using exactly the
/// same descriptor and initialization service as the interactive setup flow.
/// This command is deliberately not run automatically at host startup.
/// </summary>
public sealed class UnattendedBootstrapCommand(
    IBootstrapStateStore stateStore,
    BootstrapOptions options,
    IDataProtectionProvider dataProtection,
    PostgreSqlSetupPreflightService postgreSqlPreflight)
{
    public async Task<BootstrapInitializationResult> InitializeAsync(
        BootstrapDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        if (descriptor.State is BootstrapState.Ready or BootstrapState.RecoveryRequired)
        {
            return BootstrapInitializationResult.InvalidState;
        }

        var unattended = options.Unattended;
        if (string.IsNullOrWhiteSpace(unattended.Email) ||
            string.IsNullOrWhiteSpace(unattended.DisplayName) ||
            string.IsNullOrWhiteSpace(unattended.Password) ||
            string.IsNullOrWhiteSpace(unattended.OrganizationName))
        {
            return BootstrapInitializationResult.InvalidUnattendedConfiguration;
        }

        var provider = string.IsNullOrWhiteSpace(unattended.Provider) ? "Sqlite" : unattended.Provider.Trim();
        BootstrapDescriptor configured;
        if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            var sqlitePath = Path.GetFullPath(string.IsNullOrWhiteSpace(unattended.SqlitePath)
                ? Path.Combine(options.DataDirectory, "rateldesk.db")
                : unattended.SqlitePath);
            var allowedDataDirectory = Path.GetFullPath(options.DataDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!sqlitePath.StartsWith(allowedDataDirectory, StringComparison.Ordinal))
            {
                return BootstrapInitializationResult.InvalidUnattendedConfiguration;
            }

            configured = await stateStore.UpdateAsync(current => current.State switch
            {
                BootstrapState.Unconfigured or BootstrapState.Configuring => current with
                {
                    State = BootstrapState.Configuring,
                    Provider = "Sqlite",
                    SqlitePath = sqlitePath,
                    ProtectedPostgreSqlConnection = null,
                    OperationId = current.OperationId ?? Guid.NewGuid()
                },
                _ => current
            }, cancellationToken);
        }
        else if (string.Equals(provider, "PostgreSql", StringComparison.OrdinalIgnoreCase))
        {
            var preflight = await postgreSqlPreflight.CheckAsync(unattended.PostgreSqlConnectionString, cancellationToken);
            if (!preflight.Succeeded)
            {
                return new BootstrapInitializationResult(false, preflight.Error, null);
            }

            var protectedConnection = dataProtection
                .CreateProtector("RatelDesk.Bootstrap.PostgreSqlConnection.v1")
                .Protect(unattended.PostgreSqlConnectionString!);
            configured = await stateStore.UpdateAsync(current => current.State switch
            {
                BootstrapState.Unconfigured or BootstrapState.Configuring => current with
                {
                    State = BootstrapState.Configuring,
                    Provider = "PostgreSql",
                    SqlitePath = null,
                    ProtectedPostgreSqlConnection = protectedConnection,
                    OperationId = current.OperationId ?? Guid.NewGuid()
                },
                _ => current
            }, cancellationToken);
        }
        else
        {
            return BootstrapInitializationResult.InvalidUnattendedConfiguration;
        }

        var initializer = new BootstrapInitializationService(stateStore, options, dataProtection);
        return await initializer.InitializeAsync(configured, new FirstAdministratorRequest(
            unattended.Email,
            unattended.DisplayName,
            unattended.Password,
            unattended.OrganizationName,
            unattended.ApplicationName,
            unattended.ApplicationUrl), cancellationToken);
    }
}
