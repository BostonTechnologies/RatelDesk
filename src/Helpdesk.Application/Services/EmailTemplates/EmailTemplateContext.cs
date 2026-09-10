namespace Helpdesk.Application.Services.EmailTemplates;

public class EmailTemplateContext
{
    public string UserName { get; set; } = string.Empty;
    public string TicketRef { get; set; } = string.Empty;
    public string TicketType { get; set; } = "Ticket";
    public string TicketTypeLower { get; set; } = "ticket";
    public string TicketLink { get; set; } = string.Empty;
    public string UpdateMessageHtml { get; set; } = string.Empty;
    public string UpdateMessageText { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string RequestTitle { get; set; } = string.Empty;
    public string RequestDescription { get; set; } = string.Empty;
    public string RequestRequestedBy { get; set; } = string.Empty;
    public string RequestRequestedFor { get; set; } = string.Empty;
    public string RequestApprovers { get; set; } = string.Empty;
    public string RequestPayloadHtml { get; set; } = string.Empty;
    public string RequestApprovalDueAt { get; set; } = string.Empty;
    public string RequestApprovalLink { get; set; } = string.Empty;
    public string RequestRejectLink { get; set; } = string.Empty;
    public string RequestApprovalTaskName { get; set; } = string.Empty;
    public string RequestApprovalApprover { get; set; } = string.Empty;
    public string RequestApprovalRejectionReason { get; set; } = string.Empty;
    public string FailureReason { get; set; } = string.Empty;
    public string IncidentRef { get; set; } = string.Empty;
    public string IncidentLink { get; set; } = string.Empty;
    public string CompletedAt { get; set; } = string.Empty;
    public string ResolvedAt { get; set; } = string.Empty;
    public string ChangeTitle { get; set; } = string.Empty;
    public string ChangeDescription { get; set; } = string.Empty;
    public string ChangeType { get; set; } = string.Empty;
    public string ChangePriority { get; set; } = string.Empty;
    public string ChangeOrganization { get; set; } = string.Empty;
    public string ChangeRequestedFor { get; set; } = string.Empty;
    public string ChangeImplementor { get; set; } = string.Empty;
    public string ChangeApprovers { get; set; } = string.Empty;
    public string ChangeImplementationStart { get; set; } = string.Empty;
    public string ChangeImplementationEnd { get; set; } = string.Empty;
    public string ChangeApprovalStatus { get; set; } = string.Empty;
    public string ChangeCompletionState { get; set; } = string.Empty;
    public string ChangeApprovalLink { get; set; } = string.Empty;
    public string ChangeViewLink { get; set; } = string.Empty;
    public string ChangeScopeOfChange { get; set; } = string.Empty;
    public string ChangeAffectedSystems { get; set; } = string.Empty;
    public string ChangeImplementationSteps { get; set; } = string.Empty;
    public string ChangeValidationSteps { get; set; } = string.Empty;
    public string ChangeRollbackPlan { get; set; } = string.Empty;
    public string ChangeRollbackReference { get; set; } = string.Empty;
    public string OrganizationName { get; set; } = string.Empty;
    public string InviteLink { get; set; } = string.Empty;
    public string InviteExpiresAt { get; set; } = string.Empty;
    public string HelpdeskUrl { get; set; } = string.Empty;

    public string LayoutHtml { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public string LogoHtml { get; set; } = string.Empty;
    public string FooterHtml { get; set; } = string.Empty;
    public string PrimaryColor { get; set; } = string.Empty;
    public string ActionColor { get; set; } = "#f59e0b";
    public string ActionBorderColor { get; set; } = "#d97706";
    public string ActionTextColor { get; set; } = "#111827";
    public EmailBrandingTemplateContext Brand { get; set; } = new();
}

public sealed class EmailBrandingTemplateContext
{
    public string ApplicationName { get; set; } = "RatelDesk";
    public string OrganizationName { get; set; } = string.Empty;
    public string ApplicationUrl { get; set; } = string.Empty;
    public string OrganizationUrl { get; set; } = string.Empty;
    public string SupportUrl { get; set; } = string.Empty;
    public string SupportEmail { get; set; } = string.Empty;
    public string LogoUrl { get; set; } = string.Empty;
    public string EmailFromDisplayName { get; set; } = "RatelDesk";
    public string Tagline { get; set; } = string.Empty;
}
