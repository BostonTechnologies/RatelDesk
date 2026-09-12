using Microsoft.AspNetCore.Mvc;

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
        .WithTags("Setup");

        app.MapPost("/api/v1/setup/storage", async (
            [FromHeader(Name = "X-RatelDesk-Setup-Session")] string? session,
            [FromBody] SelectStorageRequest request,
            [FromServices] IBootstrapStateStore stateStore,
            [FromServices] BootstrapSessionService sessions,
            [FromServices] BootstrapOptions options,
            CancellationToken cancellationToken) =>
        {
            if (!sessions.IsValid(session))
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
                return Results.Problem("PostgreSQL setup preflight is not available yet.", statusCode: StatusCodes.Status501NotImplemented);
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
            CancellationToken cancellationToken) =>
        {
            if (!sessions.IsValid(session))
            {
                return Results.Unauthorized();
            }

            var descriptor = await stateStore.LoadOrCreateAsync(cancellationToken);
            var result = await initializer.InitializeSqliteAsync(descriptor, request, cancellationToken);
            return result.Succeeded
                ? Results.Ok(new BootstrapStatusResponse(result.Descriptor!.State, result.Descriptor.Provider))
                : Results.Problem(result.Error, statusCode: StatusCodes.Status409Conflict);
        })
        .AllowAnonymous()
        .WithTags("Setup");
    }

    private sealed record UnlockSetupRequest(string? SetupCode);

    private sealed record SelectStorageRequest(string? Provider, string? SqlitePath);

    private sealed record SetupSessionResponse(string Session, DateTimeOffset ExpiresAtUtc);

    private sealed record BootstrapStatusResponse(BootstrapState State, string? Provider);
}
