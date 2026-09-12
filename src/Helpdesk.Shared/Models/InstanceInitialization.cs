namespace Helpdesk.Shared.Models;

/// <summary>
/// Durable evidence that the application database belongs to an initialized
/// RatelDesk instance. This is deliberately distinct from migration history:
/// a fresh database may be fully migrated before its first administrator is
/// created.
/// </summary>
public sealed class InstanceInitialization
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    public Guid InstanceId { get; set; }

    public Guid OperationId { get; set; }

    public string SetupVersion { get; set; } = string.Empty;

    public DateTimeOffset CompletedAtUtc { get; set; }
}
