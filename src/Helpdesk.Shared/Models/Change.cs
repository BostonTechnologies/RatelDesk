namespace Helpdesk.Shared.Models;

public class Change : Ticket
{
    public ICollection<ChangeCategoryLink> CategoryLinks { get; set; } = new List<ChangeCategoryLink>();
    public ICollection<ChangeApproval> Approvals { get; set; } = new List<ChangeApproval>();

    public string? ChangeType { get; set; }
    public ChangeLifecycleState? LifecycleState { get; set; } = ChangeLifecycleState.Draft;
    public string? RequestedForUserId { get; set; }
    public string? ImplementorUserId { get; set; }
    public List<string> ApproverUserIds { get; set; } = new();
    public DateTime? ImplementationStartAt { get; set; }
    public DateTime? ImplementationEndAt { get; set; }
    public string? ChangeTemplateJson { get; set; }
    public ChangeReviewStatus AiReviewStatus { get; set; } = ChangeReviewStatus.NotRequested;
    public ChangeReviewGateState AiReviewGateState { get; set; } = ChangeReviewGateState.NotRequired;
    public string? AiReviewOutputJson { get; set; }
    public string? AiReviewCorrelationId { get; set; }
    public string? AiReviewFailureReason { get; set; }
    public string? AiReviewAcknowledgementNotes { get; set; }
    public DateTimeOffset? AiReviewRequestedAt { get; set; }
    public DateTimeOffset? AiReviewCompletedAt { get; set; }
    public DateTimeOffset? AiReviewAcknowledgedAt { get; set; }
    public string? AiReviewAcknowledgedByUserId { get; set; }
    public string? AiReviewAcknowledgedByName { get; set; }
}
