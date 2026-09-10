using Dodo.Primitives;

namespace Helpdesk.Shared.Models;

public class RequestTaskApproval
{
    public string Id { get; set; } = Uuid.CreateVersion7().ToString();
    public string RequestId { get; set; } = string.Empty;
    public Request? Request { get; set; }
    public string RequestTaskId { get; set; } = string.Empty;
    public RequestTask? RequestTask { get; set; }
    public string ApproverSource { get; set; } = "Customer";
    public string ApproverId { get; set; } = string.Empty;
    public string ApproverName { get; set; } = string.Empty;
    public string ApproverEmail { get; set; } = string.Empty;
    public string? OrganizationId { get; set; }
    public string? OrganizationName { get; set; }
    public RequestTaskApprovalStatus Status { get; set; } = RequestTaskApprovalStatus.Pending;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ViewedAtUtc { get; set; }
    public DateTimeOffset? ReviewedAtUtc { get; set; }
    public DateTimeOffset? TokenSentAtUtc { get; set; }
}
