using Dodo.Primitives;

namespace Helpdesk.Shared.Models;

public class ChangeApproval
{
    public string Id { get; set; } = Uuid.CreateVersion7().ToString();
    public string ChangeId { get; set; } = string.Empty;
    public Change? Change { get; set; }
    public string? ApproverId { get; set; }
    public string ApproverName { get; set; } = string.Empty;
    public string ApproverEmail { get; set; } = string.Empty;
    public ChangeApprovalStatus Status { get; set; } = ChangeApprovalStatus.Pending;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReviewedAtUtc { get; set; }
    public DateTimeOffset? ViewedAtUtc { get; set; }
    public DateTimeOffset? TokenSentAtUtc { get; set; }
}
