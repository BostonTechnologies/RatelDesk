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
    DateTimeOffset? CompletedAtUtc)
{
    // This value is protected with the durable bootstrap data-protection key ring.
    // It is never included in setup status responses or logs.
    public string? ProtectedPostgreSqlConnection { get; init; }
    public bool AdoptedLegacy { get; init; }
    public string? ProtectedKeyRingProof { get; init; }
}
