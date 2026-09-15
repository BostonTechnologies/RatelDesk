using System.Text.Json;
using Helpdesk.Application.Services.Changes;
using Helpdesk.Shared.DTOs.Change;

namespace Helpdesk.Infrastructure.Changes;

public sealed class ChangeTemplateService : IChangeTemplateService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string NormalizeChangeType(string? changeType) => changeType?.Trim().ToLowerInvariant() switch
    {
        "standard" => "Standard",
        "normal" => "Normal",
        "emergency" => "Emergency",
        _ => string.Empty
    };

    public ChangeTemplateDto? DeserializeTemplate(string? templateJson) =>
        string.IsNullOrWhiteSpace(templateJson) ? null : JsonSerializer.Deserialize<ChangeTemplateDto>(templateJson, JsonOptions);

    public string SerializeTemplate(ChangeTemplateDto? template) =>
        JsonSerializer.Serialize(NormalizeTemplate(template ?? new ChangeTemplateDto()), JsonOptions);

    public ChangeTemplateValidationDto ValidateTemplate(string? changeType, ChangeTemplateDto? template)
    {
        var normalizedType = NormalizeChangeType(changeType);
        var normalizedTemplate = NormalizeTemplate(template ?? new ChangeTemplateDto());
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(normalizedType)) errors.Add("Change type must be Standard, Normal, or Emergency.");
        if (string.IsNullOrWhiteSpace(normalizedTemplate.ScopeOfChange)) errors.Add("Scope of change is required.");
        if (normalizedTemplate.AffectedSystems.Count == 0) errors.Add("At least one affected system or service is required.");
        if (normalizedTemplate.ImplementationSteps.Count == 0) errors.Add("Implementation steps are required.");
        if (normalizedTemplate.ValidationSteps.Count == 0) errors.Add("Validation steps are required.");
        if (string.IsNullOrWhiteSpace(normalizedTemplate.RollbackPlan) && string.IsNullOrWhiteSpace(normalizedTemplate.RollbackReference)) errors.Add("A rollback plan or rollback reference is required.");

        switch (normalizedType)
        {
            case "Standard":
                if (normalizedTemplate.IsPreApproved != true) errors.Add("Standard changes must confirm pre-approved change status.");
                if (string.IsNullOrWhiteSpace(normalizedTemplate.ExistingRunbookReference)) errors.Add("Standard changes require an SOP or runbook reference.");
                break;
            case "Normal":
                if (string.IsNullOrWhiteSpace(normalizedTemplate.BusinessJustification)) errors.Add("Business justification is required for normal changes.");
                if (string.IsNullOrWhiteSpace(normalizedTemplate.ImpactAssessment)) errors.Add("Impact assessment is required for normal changes.");
                if (string.IsNullOrWhiteSpace(normalizedTemplate.RiskAssessment)) errors.Add("Risk assessment is required for normal changes.");
                if (normalizedTemplate.PreChangeChecks.Count == 0) errors.Add("Pre-change checks are required for normal changes.");
                if (string.IsNullOrWhiteSpace(normalizedTemplate.TestingPlan)) errors.Add("Validation or testing plan is required for normal changes.");
                break;
            case "Emergency":
                if (string.IsNullOrWhiteSpace(normalizedTemplate.EmergencyReason)) errors.Add("Emergency reason is required for emergency changes.");
                if (string.IsNullOrWhiteSpace(normalizedTemplate.BusinessImpactIfNotImplemented)) errors.Add("Business impact if not implemented is required for emergency changes.");
                if (string.IsNullOrWhiteSpace(normalizedTemplate.ImmediateRiskAssessment)) errors.Add("Immediate risk assessment is required for emergency changes.");
                if (string.IsNullOrWhiteSpace(normalizedTemplate.PostChangeValidation)) errors.Add("Post-change validation is required for emergency changes.");
                break;
        }

        return new ChangeTemplateValidationDto { IsComplete = errors.Count == 0, Errors = errors };
    }

    private static ChangeTemplateDto NormalizeTemplate(ChangeTemplateDto template) => new()
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

    private static List<string> NormalizeList(IEnumerable<string>? items) => items?
        .Select(NormalizeText).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>()
        .Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? [];

    private static string? NormalizeText(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
