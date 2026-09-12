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

            var descriptor = await stateStore.UpdateAsync(current => current.State switch
            {
                BootstrapState.Unconfigured or BootstrapState.Configuring => current with
                {
                    State = BootstrapState.Configuring,
                    Provider = request.Provider,
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
    }

    private sealed record UnlockSetupRequest(string? SetupCode);

    private sealed record SelectStorageRequest(string? Provider);

    private sealed record SetupSessionResponse(string Session, DateTimeOffset ExpiresAtUtc);

    private sealed record BootstrapStatusResponse(BootstrapState State, string? Provider);
}
