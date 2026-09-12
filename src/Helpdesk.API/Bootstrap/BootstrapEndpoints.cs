using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Helpdesk.API.Bootstrap;

public static class BootstrapEndpoints
{
    public static void MapBootstrapEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/setup/status", async (
            [FromServices] IBootstrapStateStore stateStore,
            CancellationToken cancellationToken) =>
        {
            var descriptor = await stateStore.LoadOrCreateAsync(cancellationToken);
            return Results.Ok(new BootstrapStatusResponse(descriptor.State, descriptor.Provider));
        })
        .AllowAnonymous()
        .RequireRateLimiting("SetupUnlock")
        .WithTags("Setup");

        app.MapPost("/api/v1/setup/session", async (
            [FromBody] UnlockSetupRequest request,
            [FromServices] IBootstrapStateStore stateStore,
            [FromServices] BootstrapSessionService sessions,
            CancellationToken cancellationToken) =>
        {
            var descriptor = await stateStore.LoadOrCreateAsync(cancellationToken);
            return sessions.TryCreate(descriptor, request.SetupCode, out var session)
                ? Results.Ok(new SetupSessionResponse(session!, DateTimeOffset.UtcNow.AddMinutes(10)))
                : Results.Unauthorized();
        })
        .AllowAnonymous()
        .RequireRateLimiting("SetupUnlock")
        .WithTags("Setup");

        app.MapPost("/api/v1/setup/storage", async (
            [FromHeader(Name = "X-RatelDesk-Setup-Session")] string? session,
            [FromBody] SelectStorageRequest request,
            [FromServices] IBootstrapStateStore stateStore,
            [FromServices] BootstrapSessionService sessions,
            [FromServices] BootstrapOptions options,
            [FromServices] PostgreSqlSetupPreflightService postgreSqlPreflight,
            [FromServices] IDataProtectionProvider dataProtection,
            CancellationToken cancellationToken) =>
        {
            var currentDescriptor = await stateStore.LoadOrCreateAsync(cancellationToken);
            if (!sessions.IsValid(session, currentDescriptor))
            {
                return Results.Unauthorized();
            }

            if (request.Provider is not ("Sqlite" or "PostgreSql"))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["provider"] = ["Provider must be Sqlite or PostgreSql."]
                });
            }

            if (string.Equals(request.Provider, "PostgreSql", StringComparison.OrdinalIgnoreCase))
            {
                var connectionString = request.PostgreSqlConnectionString;
                if (string.IsNullOrWhiteSpace(connectionString) && !string.IsNullOrWhiteSpace(request.PostgreSqlHost))
                {
                    connectionString = new NpgsqlConnectionStringBuilder
                    {
                        Host = request.PostgreSqlHost.Trim(),
                        Port = request.PostgreSqlPort.GetValueOrDefault(5432),
                        Database = request.PostgreSqlDatabase?.Trim(),
                        Username = request.PostgreSqlUsername?.Trim(),
                        Password = request.PostgreSqlPassword,
                        SslMode = request.PostgreSqlUseTls ? SslMode.Require : SslMode.Prefer
                    }.ConnectionString;
                }

                var preflight = await postgreSqlPreflight.CheckAsync(connectionString, cancellationToken);
                if (!preflight.Succeeded)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["postgreSqlConnectionString"] = [preflight.Error!]
                    });
                }

                var protectedConnection = dataProtection
                    .CreateProtector("RatelDesk.Bootstrap.PostgreSqlConnection.v1")
                    .Protect(connectionString!);
                var postgreSqlDescriptor = await stateStore.UpdateAsync(current => current.State switch
                {
                    BootstrapState.Unconfigured or BootstrapState.Configuring => current with
                    {
                        State = BootstrapState.Configuring,
                        Provider = request.Provider,
                        SqlitePath = null,
                        ProtectedPostgreSqlConnection = protectedConnection,
                        OperationId = current.OperationId ?? Guid.NewGuid()
                    },
                    _ => current
                }, cancellationToken);

                return postgreSqlDescriptor.State == BootstrapState.Configuring
                    ? Results.Ok(new BootstrapStatusResponse(postgreSqlDescriptor.State, postgreSqlDescriptor.Provider))
                    : Results.Conflict(new BootstrapStatusResponse(postgreSqlDescriptor.State, postgreSqlDescriptor.Provider));
            }

            var sqlitePath = Path.GetFullPath(string.IsNullOrWhiteSpace(request.SqlitePath)
                ? Path.Combine(options.DataDirectory, "rateldesk.db")
                : request.SqlitePath);
            var allowedDataDirectory = Path.GetFullPath(options.DataDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!sqlitePath.StartsWith(allowedDataDirectory, StringComparison.Ordinal))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["sqlitePath"] = ["SQLite data must be stored under the configured bootstrap data directory."]
                });
            }

            var descriptor = await stateStore.UpdateAsync(current => current.State switch
            {
                BootstrapState.Unconfigured or BootstrapState.Configuring => current with
                {
                    State = BootstrapState.Configuring,
                    Provider = request.Provider,
                    SqlitePath = sqlitePath,
                    ProtectedPostgreSqlConnection = null,
                    OperationId = current.OperationId ?? Guid.NewGuid()
                },
                _ => current
            }, cancellationToken);

            return descriptor.State == BootstrapState.Configuring
                ? Results.Ok(new BootstrapStatusResponse(descriptor.State, descriptor.Provider))
                : Results.Conflict(new BootstrapStatusResponse(descriptor.State, descriptor.Provider));
        })
        .AllowAnonymous()
        .WithTags("Setup");

        app.MapPost("/api/v1/setup/initialize", async (
            [FromHeader(Name = "X-RatelDesk-Setup-Session")] string? session,
            [FromBody] FirstAdministratorRequest request,
            [FromServices] IBootstrapStateStore stateStore,
            [FromServices] BootstrapSessionService sessions,
            [FromServices] BootstrapInitializationService initializer,
            [FromServices] IHostApplicationLifetime applicationLifetime,
            CancellationToken cancellationToken) =>
        {
            var descriptor = await stateStore.LoadOrCreateAsync(cancellationToken);
            if (!sessions.IsValid(session, descriptor))
            {
                return Results.Unauthorized();
            }

            var result = await initializer.InitializeAsync(descriptor, request, cancellationToken);
            if (!result.Succeeded)
            {
                return Results.Problem(result.Error, statusCode: StatusCodes.Status409Conflict);
            }

            _ = StopBootstrapHostAfterResponseAsync(applicationLifetime);
            return Results.Ok(new BootstrapStatusResponse(result.Descriptor!.State, result.Descriptor.Provider));
        })
        .AllowAnonymous()
        .WithTags("Setup");
    }

    private sealed record UnlockSetupRequest(string? SetupCode);

    private sealed record SelectStorageRequest(
        string? Provider,
        string? SqlitePath,
        string? PostgreSqlConnectionString,
        string? PostgreSqlHost,
        int? PostgreSqlPort,
        string? PostgreSqlDatabase,
        string? PostgreSqlUsername,
        string? PostgreSqlPassword,
        bool PostgreSqlUseTls = true);

    private sealed record SetupSessionResponse(string Session, DateTimeOffset ExpiresAtUtc);

    private sealed record BootstrapStatusResponse(BootstrapState State, string? Provider);

    private static async Task StopBootstrapHostAfterResponseAsync(IHostApplicationLifetime applicationLifetime)
    {
        await Task.Delay(TimeSpan.FromSeconds(1));
        applicationLifetime.StopApplication();
    }
}
