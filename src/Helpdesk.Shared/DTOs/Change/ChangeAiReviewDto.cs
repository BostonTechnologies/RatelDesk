using Helpdesk.Shared.Models;

namespace Helpdesk.Shared.DTOs.Change;

public class ChangeAiReviewDto
{
    public ChangeReviewStatus Status { get; set; } = ChangeReviewStatus.NotRequested;
    public ChangeReviewGateState GateState { get; set; } = ChangeReviewGateState.NotRequired;
    public string Summary { get; set; } = string.Empty;
    public List<string> IssuesFound { get; set; } = new();
    public List<string> RisksIdentified { get; set; } = new();
    public List<string> MissingInformation { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public string RawReview { get; set; } = string.Empty;
    public bool HasBlockingIssues { get; set; }
    public bool RequiresAcknowledgement { get; set; }
    public string? CorrelationId { get; set; }
    public string? FailureReason { get; set; }
    public string? AcknowledgedByUserId { get; set; }
    public string? AcknowledgedByName { get; set; }
    public string? AcknowledgementNotes { get; set; }
    public DateTimeOffset? RequestedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? AcknowledgedAt { get; set; }
}
