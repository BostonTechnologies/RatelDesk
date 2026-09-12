namespace Helpdesk.API.Bootstrap;

public sealed record BootstrapDescriptor(
    int Version,
    Guid InstanceId,
    BootstrapState State,
    string SetupCodeHash,
    DateTimeOffset SetupCodeCreatedAtUtc,
    string? Provider,
    string? SqlitePath,
    Guid? OperationId,
    DateTimeOffset? CompletedAtUtc);
