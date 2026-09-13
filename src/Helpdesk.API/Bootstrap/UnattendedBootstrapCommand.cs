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
        await using var lease = await BootstrapOperationLease.AcquireAsync(options.StateDirectory, cancellationToken);
        var currentDescriptor = await stateStore.LoadOrCreateAsync(cancellationToken);
        if (currentDescriptor.InstanceId != descriptor.InstanceId || currentDescriptor.OperationId != descriptor.OperationId)
            return BootstrapInitializationResult.InvalidState;
        descriptor = currentDescriptor;
        if (descriptor.State is BootstrapState.Ready or BootstrapState.RecoveryRequired)
        {
            return BootstrapInitializationResult.InvalidState;
        }

        var reconciled = await BootstrapStartupService.ReconcileSelectedMarkerAsync(
            stateStore, descriptor, dataProtection, cancellationToken);
        if (reconciled is not null) return BootstrapInitializationResult.InvalidState;

        var unattended = options.Unattended;
        if (string.IsNullOrWhiteSpace(unattended.Email) ||
            string.IsNullOrWhiteSpace(unattended.DisplayName) ||
            string.IsNullOrWhiteSpace(unattended.Password) ||
            string.IsNullOrWhiteSpace(unattended.OrganizationName))
        {
            return BootstrapInitializationResult.InvalidUnattendedConfiguration;
        }

        var provider = string.IsNullOrWhiteSpace(unattended.Provider) ? descriptor.Provider ?? "Sqlite" : unattended.Provider.Trim();
        BootstrapDescriptor configured;
        if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            var sqlitePath = Path.GetFullPath(string.IsNullOrWhiteSpace(unattended.SqlitePath)
                ? descriptor.SqlitePath ?? Path.Combine(options.DataDirectory, "rateldesk.db")
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
                    OperationId = current.Provider == "Sqlite" && current.SqlitePath == sqlitePath
                        ? current.OperationId ?? Guid.NewGuid() : Guid.NewGuid()
                },
                _ => current
            }, cancellationToken);
        }
        else if (string.Equals(provider, "PostgreSql", StringComparison.OrdinalIgnoreCase))
        {
            var connection = unattended.PostgreSqlConnectionString;
            string? previousConnection = null;
            try
            {
                if (descriptor.ProtectedPostgreSqlConnection is not null)
                    previousConnection = dataProtection.CreateProtector("RatelDesk.Bootstrap.PostgreSqlConnection.v1")
                        .Unprotect(descriptor.ProtectedPostgreSqlConnection);
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                return BootstrapInitializationResult.InvalidState;
            }
            if (string.IsNullOrWhiteSpace(connection)) connection = previousConnection;
            var preflight = await postgreSqlPreflight.CheckAsync(connection, cancellationToken);
            if (!preflight.Succeeded)
            {
                return new BootstrapInitializationResult(false, preflight.Error, null);
            }

            var protectedConnection = connection == previousConnection ? descriptor.ProtectedPostgreSqlConnection! : dataProtection
                .CreateProtector("RatelDesk.Bootstrap.PostgreSqlConnection.v1")
                .Protect(connection!);
            configured = await stateStore.UpdateAsync(current => current.State switch
            {
                BootstrapState.Unconfigured or BootstrapState.Configuring => current with
                {
                    State = BootstrapState.Configuring,
                    Provider = "PostgreSql",
                    SqlitePath = null,
                    ProtectedPostgreSqlConnection = protectedConnection,
                    OperationId = current.Provider == "PostgreSql" && connection == previousConnection
                        ? current.OperationId ?? Guid.NewGuid() : Guid.NewGuid()
                },
                _ => current
            }, cancellationToken);
        }
        else
        {
            return BootstrapInitializationResult.InvalidUnattendedConfiguration;
        }

        await lease.DisposeAsync();
        var initializer = new BootstrapInitializationService(stateStore, options, dataProtection);
        return await initializer.InitializeAsync(configured, new FirstAdministratorRequest(
            unattended.Email,
            unattended.DisplayName,
            unattended.Password,
            unattended.OrganizationName,
            unattended.ApplicationName,
            unattended.ApplicationUrl,
            unattended.TimeZoneId), cancellationToken);
    }
}
