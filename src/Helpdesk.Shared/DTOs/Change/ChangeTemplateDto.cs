namespace Helpdesk.Shared.DTOs.Change;

public class ChangeTemplateDto
{
    public bool? IsPreApproved { get; set; }
    public string? ExistingRunbookReference { get; set; }
    public string? ScopeOfChange { get; set; }
    public List<string> AffectedSystems { get; set; } = new();
    public List<string> ImplementationSteps { get; set; } = new();
    public List<string> ValidationSteps { get; set; } = new();
    public string? RollbackReference { get; set; }
    public string? RiskClassification { get; set; }
    public string? ChangeDescription { get; set; }
    public string? BusinessJustification { get; set; }
    public string? ImpactAssessment { get; set; }
    public string? RiskAssessment { get; set; }
    public List<string> PreChangeChecks { get; set; } = new();
    public string? TestingPlan { get; set; }
    public string? RollbackPlan { get; set; }
    public List<string> Dependencies { get; set; } = new();
    public string? EmergencyReason { get; set; }
    public string? BusinessImpactIfNotImplemented { get; set; }
    public string? ImmediateRiskAssessment { get; set; }
    public string? IncidentReference { get; set; }
    public string? PostChangeValidation { get; set; }
}
