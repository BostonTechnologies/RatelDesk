using System.Text.Json;
using Helpdesk.Application.Services.AI;
using Helpdesk.Application.Services.Changes;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.DTOs.Change;
using Helpdesk.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Helpdesk.Infrastructure.Changes;

public sealed class ChangeReviewService(
    IAiRuntime aiRuntime,
    IAiPromptTemplateService promptTemplates,
    IAiOperationAuditService auditService,
    HelpdeskDbContext db,
    ILogger<ChangeReviewService> logger) : IChangeReviewService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly IAiRuntime _aiRuntime = aiRuntime;
    private readonly IAiPromptTemplateService _promptTemplates = promptTemplates;
    private readonly IAiOperationAuditService _auditService = auditService;
    private readonly HelpdeskDbContext _db = db;
    private readonly ILogger<ChangeReviewService> _logger = logger;

    public string NormalizeChangeType(string? changeType)
    {
        return changeType?.Trim().ToLowerInvariant() switch
        {
            "standard" => "Standard",
            "normal" => "Normal",
            "emergency" => "Emergency",
            _ => string.Empty
        };
    }

    public ChangeTemplateDto? DeserializeTemplate(string? templateJson)
    {
        if (string.IsNullOrWhiteSpace(templateJson))
        {
            return null;
        }

        return JsonSerializer.Deserialize<ChangeTemplateDto>(templateJson, JsonOptions);
    }

    public string SerializeTemplate(ChangeTemplateDto? template)
    {
        return JsonSerializer.Serialize(NormalizeTemplate(template ?? new ChangeTemplateDto()), JsonOptions);
    }

    public ChangeTemplateValidationDto ValidateTemplate(string? changeType, ChangeTemplateDto? template)
    {
        var normalizedType = NormalizeChangeType(changeType);
        var normalizedTemplate = NormalizeTemplate(template ?? new ChangeTemplateDto());
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(normalizedType))
        {
            errors.Add("Change type must be Standard, Normal, or Emergency.");
        }

        if (string.IsNullOrWhiteSpace(normalizedTemplate.ScopeOfChange))
        {
            errors.Add("Scope of change is required.");
        }

        if (normalizedTemplate.AffectedSystems.Count == 0)
        {
            errors.Add("At least one affected system or service is required.");
        }

        if (normalizedTemplate.ImplementationSteps.Count == 0)
        {
            errors.Add("Implementation steps are required.");
        }

        if (normalizedTemplate.ValidationSteps.Count == 0)
        {
            errors.Add("Validation steps are required.");
        }

        if (string.IsNullOrWhiteSpace(normalizedTemplate.RollbackPlan)
            && string.IsNullOrWhiteSpace(normalizedTemplate.RollbackReference))
        {
            errors.Add("A rollback plan or rollback reference is required.");
        }

        switch (normalizedType)
        {
            case "Standard":
                if (normalizedTemplate.IsPreApproved != true)
                {
                    errors.Add("Standard changes must confirm pre-approved change status.");
                }

                if (string.IsNullOrWhiteSpace(normalizedTemplate.ExistingRunbookReference))
                {
                    errors.Add("Standard changes require an SOP or runbook reference.");
                }

                break;
            case "Normal":
                if (string.IsNullOrWhiteSpace(normalizedTemplate.BusinessJustification))
                {
                    errors.Add("Business justification is required for normal changes.");
                }

                if (string.IsNullOrWhiteSpace(normalizedTemplate.ImpactAssessment))
                {
                    errors.Add("Impact assessment is required for normal changes.");
                }

                if (string.IsNullOrWhiteSpace(normalizedTemplate.RiskAssessment))
                {
                    errors.Add("Risk assessment is required for normal changes.");
                }

                if (normalizedTemplate.PreChangeChecks.Count == 0)
                {
                    errors.Add("Pre-change checks are required for normal changes.");
                }

                if (string.IsNullOrWhiteSpace(normalizedTemplate.TestingPlan))
                {
                    errors.Add("Validation or testing plan is required for normal changes.");
                }

                break;
            case "Emergency":
                if (string.IsNullOrWhiteSpace(normalizedTemplate.EmergencyReason))
                {
                    errors.Add("Emergency reason is required for emergency changes.");
                }

                if (string.IsNullOrWhiteSpace(normalizedTemplate.BusinessImpactIfNotImplemented))
                {
                    errors.Add("Business impact if not implemented is required for emergency changes.");
                }

                if (string.IsNullOrWhiteSpace(normalizedTemplate.ImmediateRiskAssessment))
                {
                    errors.Add("Immediate risk assessment is required for emergency changes.");
                }

                if (string.IsNullOrWhiteSpace(normalizedTemplate.PostChangeValidation))
                {
                    errors.Add("Post-change validation is required for emergency changes.");
                }

                break;
        }

        return new ChangeTemplateValidationDto
        {
            IsComplete = errors.Count == 0,
            Errors = errors
        };
    }

    public ChangeAiReviewDto BuildReviewDto(Change change)
    {
        ChangeAiReviewDto? stored = null;
        if (!string.IsNullOrWhiteSpace(change.AiReviewOutputJson))
        {
            stored = JsonSerializer.Deserialize<ChangeAiReviewDto>(change.AiReviewOutputJson, JsonOptions);
        }

        stored ??= new ChangeAiReviewDto();
        stored.Status = change.AiReviewStatus;
        stored.GateState = change.AiReviewGateState;
        stored.CorrelationId ??= change.AiReviewCorrelationId;
        stored.FailureReason ??= change.AiReviewFailureReason;
        stored.AcknowledgementNotes ??= change.AiReviewAcknowledgementNotes;
        stored.AcknowledgedByUserId ??= change.AiReviewAcknowledgedByUserId;
        stored.AcknowledgedByName ??= change.AiReviewAcknowledgedByName;
        stored.RequestedAt ??= change.AiReviewRequestedAt;
        stored.CompletedAt ??= change.AiReviewCompletedAt;
        stored.AcknowledgedAt ??= change.AiReviewAcknowledgedAt;
        stored.RequiresAcknowledgement = change.AiReviewGateState == ChangeReviewGateState.Warning;
        return stored;
    }

    public bool RequiresReviewBeforeProgress(Change change)
    {
        return change.AiReviewStatus is not ChangeReviewStatus.Complete
            || change.AiReviewGateState == ChangeReviewGateState.Warning;
    }

    public void MarkReviewStale(Change change)
    {
        if (change.AiReviewStatus == ChangeReviewStatus.NotRequested)
        {
            return;
        }

        change.AiReviewStatus = ChangeReviewStatus.Stale;
        change.AiReviewGateState = ChangeReviewGateState.Warning;
        change.AiReviewAcknowledgedAt = null;
        change.AiReviewAcknowledgedByName = null;
        change.AiReviewAcknowledgedByUserId = null;
        change.AiReviewAcknowledgementNotes = null;
    }

    public async Task<ChangeAiReviewDto> RunReviewAsync(Change change, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(change.OrganizationId))
        {
            if (!string.IsNullOrWhiteSpace(change.CustomerId))
            {
                var organizationId = await _db.Customers
                    .AsNoTracking()
                    .Where(x => x.Id == change.CustomerId)
                    .Select(x => x.OrganizationId)
                    .FirstOrDefaultAsync(token);

                if (!string.IsNullOrWhiteSpace(organizationId))
                {
                    change.OrganizationId = organizationId;
                    await _db.SaveChangesAsync(token);
                }
            }

            if (string.IsNullOrWhiteSpace(change.OrganizationId))
            {
                throw new InvalidOperationException("Select an organization before AI review can run.");
            }
        }

        var template = DeserializeTemplate(change.ChangeTemplateJson) ?? new ChangeTemplateDto();
        var validation = ValidateTemplate(change.ChangeType, template);
        if (!validation.IsComplete)
        {
            throw new InvalidOperationException("Change template is incomplete. Complete the required ITIL fields before AI review.");
        }

        change.AiReviewStatus = ChangeReviewStatus.Pending;
        change.AiReviewGateState = ChangeReviewGateState.NotRequired;
        change.AiReviewRequestedAt = DateTimeOffset.UtcNow;
        change.AiReviewCompletedAt = null;
        change.AiReviewFailureReason = null;
        change.AiReviewAcknowledgedAt = null;
        change.AiReviewAcknowledgedByName = null;
        change.AiReviewAcknowledgedByUserId = null;
        change.AiReviewAcknowledgementNotes = null;

        var correlationId = Guid.NewGuid().ToString("N");
        change.AiReviewCorrelationId = correlationId;
        await _db.SaveChangesAsync(token);

        await _auditService.RecordAsync(
            new AiOperationAuditEntry(
                "change-ai-review-requested",
                change.OrganizationId,
                "pending",
                "pending",
                correlationId,
                change.Id,
                $"Requested AI peer review for {NormalizeChangeType(change.ChangeType)} change {change.TrackingId}."),
            token);

        try
        {
            var runtime = await _aiRuntime.CreateChatClientAsync(
                new AiChatRuntimeRequest(
                    change.OrganizationId,
                    CorrelationId: correlationId,
                    SubjectId: change.Id,
                    Scenario: "change-ai-review"),
                token);

            var systemPrompt = _promptTemplates.Render(
                "Change.PeerReview.System",
                new Dictionary<string, string?>());
            var payload = JsonSerializer.Serialize(
                new
                {
                    change.TrackingId,
                    ChangeType = NormalizeChangeType(change.ChangeType),
                    change.Title,
                    change.Description,
                    Template = template
                },
                JsonOptions);

            var response = await runtime.Client.GetResponseAsync(
                [
                    new ChatMessage(ChatRole.System, systemPrompt),
                    new ChatMessage(ChatRole.User, payload)
                ],
                cancellationToken: token);

            var review = ParseReviewResponse(response.Text ?? string.Empty);
            review.Status = ChangeReviewStatus.Complete;
            review.CorrelationId = correlationId;
            review.RequestedAt = change.AiReviewRequestedAt;
            review.CompletedAt = DateTimeOffset.UtcNow;
            review.GateState = review.HasBlockingIssues ? ChangeReviewGateState.Warning : ChangeReviewGateState.Pass;
            review.RequiresAcknowledgement = review.HasBlockingIssues;

            change.AiReviewStatus = review.Status;
            change.AiReviewGateState = review.GateState;
            change.AiReviewCompletedAt = review.CompletedAt;
            change.AiReviewOutputJson = JsonSerializer.Serialize(review, JsonOptions);
            await _db.SaveChangesAsync(token);

            await _auditService.RecordAsync(
                new AiOperationAuditEntry(
                    "change-ai-review-completed",
                    change.OrganizationId,
                    runtime.ProviderName,
                    runtime.ModelId,
                    correlationId,
                    change.Id,
                    review.Summary),
                token);

            _logger.LogInformation("Completed AI peer review for change {ChangeId}", change.Id);
            return review;
        }
        catch (Exception ex)
        {
            change.AiReviewStatus = ChangeReviewStatus.Failed;
            change.AiReviewGateState = ChangeReviewGateState.Warning;
            change.AiReviewFailureReason = ex.Message;
            change.AiReviewCompletedAt = DateTimeOffset.UtcNow;
            change.AiReviewOutputJson = JsonSerializer.Serialize(
                new ChangeAiReviewDto
                {
                    Status = ChangeReviewStatus.Failed,
                    GateState = ChangeReviewGateState.Warning,
                    FailureReason = ex.Message,
                    Summary = "AI peer review failed.",
                    RequestedAt = change.AiReviewRequestedAt,
                    CompletedAt = change.AiReviewCompletedAt,
                    CorrelationId = correlationId
                },
                JsonOptions);
            await _db.SaveChangesAsync(token);

            await _auditService.RecordAsync(
                new AiOperationAuditEntry(
                    "change-ai-review-failed",
                    change.OrganizationId,
                    "failed",
                    "failed",
                    correlationId,
                    change.Id,
                    ex.Message),
                token);

            throw;
        }
    }

    private static ChangeTemplateDto NormalizeTemplate(ChangeTemplateDto template)
    {
        return new ChangeTemplateDto
        {
            IsPreApproved = template.IsPreApproved,
            ExistingRunbookReference = NormalizeText(template.ExistingRunbookReference),
            ScopeOfChange = NormalizeText(template.ScopeOfChange),
            AffectedSystems = NormalizeList(template.AffectedSystems),
            ImplementationSteps = NormalizeList(template.ImplementationSteps),
            ValidationSteps = NormalizeList(template.ValidationSteps),
            RollbackReference = NormalizeText(template.RollbackReference),
            RiskClassification = NormalizeText(template.RiskClassification),
            ChangeDescription = NormalizeText(template.ChangeDescription),
            BusinessJustification = NormalizeText(template.BusinessJustification),
            ImpactAssessment = NormalizeText(template.ImpactAssessment),
            RiskAssessment = NormalizeText(template.RiskAssessment),
            PreChangeChecks = NormalizeList(template.PreChangeChecks),
            TestingPlan = NormalizeText(template.TestingPlan),
            RollbackPlan = NormalizeText(template.RollbackPlan),
            Dependencies = NormalizeList(template.Dependencies),
            EmergencyReason = NormalizeText(template.EmergencyReason),
            BusinessImpactIfNotImplemented = NormalizeText(template.BusinessImpactIfNotImplemented),
            ImmediateRiskAssessment = NormalizeText(template.ImmediateRiskAssessment),
            IncidentReference = NormalizeText(template.IncidentReference),
            PostChangeValidation = NormalizeText(template.PostChangeValidation)
        };
    }

    private static List<string> NormalizeList(IEnumerable<string>? items)
    {
        return items?
            .Select(NormalizeText)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
            ?? new List<string>();
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static ChangeAiReviewDto ParseReviewResponse(string rawResponse)
    {
        var cleaned = ExtractJson(rawResponse);
        ChangeAiReviewDto? parsed = null;
        if (!string.IsNullOrWhiteSpace(cleaned))
        {
            try
            {
                parsed = JsonSerializer.Deserialize<ChangeAiReviewDto>(cleaned, JsonOptions);
            }
            catch
            {
                // Fall through to a raw-text fallback.
            }
        }

        parsed ??= new ChangeAiReviewDto
        {
            Summary = "AI peer review completed.",
            RawReview = rawResponse,
            Recommendations = rawResponse
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Take(10)
                .ToList()
        };
        parsed.RawReview = string.IsNullOrWhiteSpace(parsed.RawReview) ? rawResponse : parsed.RawReview;
        parsed.IssuesFound = NormalizeList(parsed.IssuesFound);
        parsed.RisksIdentified = NormalizeList(parsed.RisksIdentified);
        parsed.MissingInformation = NormalizeList(parsed.MissingInformation);
        parsed.Recommendations = NormalizeList(parsed.Recommendations);
        parsed.HasBlockingIssues = parsed.HasBlockingIssues
            || parsed.IssuesFound.Count > 0
            || parsed.MissingInformation.Count > 0;
        if (string.IsNullOrWhiteSpace(parsed.Summary))
        {
            parsed.Summary = parsed.HasBlockingIssues
                ? "AI review found issues that require reviewer acknowledgement."
                : "AI review did not find blocking issues.";
        }

        return parsed;
    }

    private static string ExtractJson(string rawResponse)
    {
        var trimmed = rawResponse.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstBreak = trimmed.IndexOf('\n');
            var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (firstBreak >= 0 && lastFence > firstBreak)
            {
                trimmed = trimmed.Substring(firstBreak + 1, lastFence - firstBreak - 1).Trim();
            }
        }

        var firstBrace = trimmed.IndexOf('{');
        var lastBrace = trimmed.LastIndexOf('}');
        return firstBrace >= 0 && lastBrace > firstBrace
            ? trimmed.Substring(firstBrace, lastBrace - firstBrace + 1)
            : trimmed;
    }
}
