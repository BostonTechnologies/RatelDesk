using Helpdesk.Application.Services.EmailTemplates;
using System.Text.Encodings.Web;

namespace Helpdesk.Infrastructure.EmailTemplates;

public sealed class EmailTemplateRenderer(ITemplateEngine templateEngine) : IEmailTemplateRenderer
{
    public string Render(string templateHtml, EmailTemplateContext context)
    {
        context ??= new EmailTemplateContext();
        var updateMessage = ResolveUpdateMessage(context);
        var actionColor = string.IsNullOrWhiteSpace(context.ActionColor) ? "#f59e0b" : context.ActionColor;
        var actionBorderColor = string.IsNullOrWhiteSpace(context.ActionBorderColor) ? "#d97706" : context.ActionBorderColor;
        var actionTextColor = string.IsNullOrWhiteSpace(context.ActionTextColor) ? "#111827" : context.ActionTextColor;

        var templateModel = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            // Backward-compatible uppercase tokens used by existing templates.
            ["USER_NAME"] = HtmlEncoder.Default.Encode(context.UserName ?? string.Empty),
            ["TICKET_REF"] = HtmlEncoder.Default.Encode(context.TicketRef ?? string.Empty),
            ["TICKET_TYPE"] = HtmlEncoder.Default.Encode(context.TicketType ?? "Ticket"),
            ["TICKET_TYPE_LOWER"] = HtmlEncoder.Default.Encode(context.TicketTypeLower ?? "ticket"),
            ["TICKET_LINK"] = HtmlEncoder.Default.Encode(context.TicketLink ?? string.Empty),
            ["UPDATE_MESSAGE"] = updateMessage,
            ["SERVICE_NAME"] = HtmlEncoder.Default.Encode(context.ServiceName ?? string.Empty),
            ["REQUEST_TITLE"] = HtmlEncoder.Default.Encode(context.RequestTitle ?? string.Empty),
            ["REQUEST_DESCRIPTION"] = HtmlEncoder.Default.Encode(context.RequestDescription ?? string.Empty),
            ["REQUEST_REQUESTED_BY"] = HtmlEncoder.Default.Encode(context.RequestRequestedBy ?? string.Empty),
            ["REQUEST_REQUESTED_FOR"] = HtmlEncoder.Default.Encode(context.RequestRequestedFor ?? string.Empty),
            ["REQUEST_APPROVERS"] = HtmlEncoder.Default.Encode(context.RequestApprovers ?? string.Empty),
            ["REQUEST_PAYLOAD_HTML"] = context.RequestPayloadHtml ?? string.Empty,
            ["REQUEST_APPROVAL_DUE_AT"] = HtmlEncoder.Default.Encode(context.RequestApprovalDueAt ?? string.Empty),
            ["REQUEST_APPROVAL_LINK"] = HtmlEncoder.Default.Encode(context.RequestApprovalLink ?? string.Empty),
            ["REQUEST_REJECT_LINK"] = HtmlEncoder.Default.Encode(context.RequestRejectLink ?? string.Empty),
            ["REQUEST_APPROVAL_TASK_NAME"] = HtmlEncoder.Default.Encode(context.RequestApprovalTaskName ?? string.Empty),
            ["REQUEST_APPROVAL_APPROVER"] = HtmlEncoder.Default.Encode(context.RequestApprovalApprover ?? string.Empty),
            ["REQUEST_APPROVAL_REJECTION_REASON"] = HtmlEncoder.Default.Encode(context.RequestApprovalRejectionReason ?? string.Empty),
            ["FAILURE_REASON"] = HtmlEncoder.Default.Encode(context.FailureReason ?? string.Empty),
            ["INCIDENT_REF"] = HtmlEncoder.Default.Encode(context.IncidentRef ?? string.Empty),
            ["INCIDENT_LINK"] = HtmlEncoder.Default.Encode(context.IncidentLink ?? string.Empty),
            ["COMPLETED_AT"] = HtmlEncoder.Default.Encode(context.CompletedAt ?? string.Empty),
            ["RESOLVED_AT"] = HtmlEncoder.Default.Encode(context.ResolvedAt ?? string.Empty),
            ["CHANGE_TITLE"] = HtmlEncoder.Default.Encode(context.ChangeTitle ?? string.Empty),
            ["CHANGE_DESCRIPTION"] = HtmlEncoder.Default.Encode(context.ChangeDescription ?? string.Empty),
            ["CHANGE_TYPE"] = HtmlEncoder.Default.Encode(context.ChangeType ?? string.Empty),
            ["CHANGE_PRIORITY"] = HtmlEncoder.Default.Encode(context.ChangePriority ?? string.Empty),
            ["CHANGE_ORGANIZATION"] = HtmlEncoder.Default.Encode(context.ChangeOrganization ?? string.Empty),
            ["CHANGE_REQUESTED_FOR"] = HtmlEncoder.Default.Encode(context.ChangeRequestedFor ?? string.Empty),
            ["CHANGE_IMPLEMENTOR"] = HtmlEncoder.Default.Encode(context.ChangeImplementor ?? string.Empty),
            ["CHANGE_APPROVERS"] = HtmlEncoder.Default.Encode(context.ChangeApprovers ?? string.Empty),
            ["CHANGE_IMPLEMENTATION_START"] = HtmlEncoder.Default.Encode(context.ChangeImplementationStart ?? string.Empty),
            ["CHANGE_IMPLEMENTATION_END"] = HtmlEncoder.Default.Encode(context.ChangeImplementationEnd ?? string.Empty),
            ["CHANGE_APPROVAL_STATUS"] = HtmlEncoder.Default.Encode(context.ChangeApprovalStatus ?? string.Empty),
            ["CHANGE_COMPLETION_STATE"] = HtmlEncoder.Default.Encode(context.ChangeCompletionState ?? string.Empty),
            ["CHANGE_APPROVAL_LINK"] = HtmlEncoder.Default.Encode(context.ChangeApprovalLink ?? string.Empty),
            ["CHANGE_VIEW_LINK"] = HtmlEncoder.Default.Encode(context.ChangeViewLink ?? string.Empty),
            ["CHANGE_SCOPE_OF_CHANGE"] = HtmlEncoder.Default.Encode(context.ChangeScopeOfChange ?? string.Empty),
            ["CHANGE_AFFECTED_SYSTEMS"] = context.ChangeAffectedSystems ?? string.Empty,
            ["CHANGE_IMPLEMENTATION_STEPS"] = context.ChangeImplementationSteps ?? string.Empty,
            ["CHANGE_VALIDATION_STEPS"] = context.ChangeValidationSteps ?? string.Empty,
            ["CHANGE_ROLLBACK_PLAN"] = HtmlEncoder.Default.Encode(context.ChangeRollbackPlan ?? string.Empty),
            ["CHANGE_ROLLBACK_REFERENCE"] = HtmlEncoder.Default.Encode(context.ChangeRollbackReference ?? string.Empty),
            ["ORGANIZATION_NAME"] = HtmlEncoder.Default.Encode(context.OrganizationName ?? string.Empty),
            ["INVITE_LINK"] = HtmlEncoder.Default.Encode(context.InviteLink ?? string.Empty),
            ["INVITE_EXPIRES_AT"] = HtmlEncoder.Default.Encode(context.InviteExpiresAt ?? string.Empty),
            ["HELPDESK_URL"] = HtmlEncoder.Default.Encode(context.HelpdeskUrl ?? string.Empty),
            ["BRAND_NAME"] = context.BrandName ?? string.Empty,
            ["PRIMARY_COLOR"] = context.PrimaryColor ?? string.Empty,
            ["ACTION_COLOR"] = actionColor,
            ["ACTION_BORDER_COLOR"] = actionBorderColor,
            ["ACTION_TEXT_COLOR"] = actionTextColor,
            ["LOGO_HTML"] = context.LogoHtml ?? string.Empty,
            ["FOOTER_HTML"] = context.FooterHtml ?? string.Empty,

            // Handlebars-lite style names.
            ["UserName"] = context.UserName ?? string.Empty,
            ["TicketRef"] = context.TicketRef ?? string.Empty,
            ["TicketType"] = context.TicketType ?? "Ticket",
            ["TicketTypeLower"] = context.TicketTypeLower ?? "ticket",
            ["TicketLink"] = context.TicketLink ?? string.Empty,
            ["UpdateMessage"] = updateMessage,
            ["UpdateMessageHtml"] = updateMessage,
            ["UpdateMessageText"] = context.UpdateMessageText ?? string.Empty,
            ["ServiceName"] = context.ServiceName ?? string.Empty,
            ["RequestTitle"] = context.RequestTitle ?? string.Empty,
            ["RequestDescription"] = context.RequestDescription ?? string.Empty,
            ["RequestRequestedBy"] = context.RequestRequestedBy ?? string.Empty,
            ["RequestRequestedFor"] = context.RequestRequestedFor ?? string.Empty,
            ["RequestApprovers"] = context.RequestApprovers ?? string.Empty,
            ["RequestPayloadHtml"] = context.RequestPayloadHtml ?? string.Empty,
            ["RequestApprovalDueAt"] = context.RequestApprovalDueAt ?? string.Empty,
            ["RequestApprovalLink"] = context.RequestApprovalLink ?? string.Empty,
            ["RequestRejectLink"] = context.RequestRejectLink ?? string.Empty,
            ["RequestApprovalTaskName"] = context.RequestApprovalTaskName ?? string.Empty,
            ["RequestApprovalApprover"] = context.RequestApprovalApprover ?? string.Empty,
            ["RequestApprovalRejectionReason"] = context.RequestApprovalRejectionReason ?? string.Empty,
            ["FailureReason"] = context.FailureReason ?? string.Empty,
            ["IncidentRef"] = context.IncidentRef ?? string.Empty,
            ["IncidentLink"] = context.IncidentLink ?? string.Empty,
            ["CompletedAt"] = context.CompletedAt ?? string.Empty,
            ["ResolvedAt"] = context.ResolvedAt ?? string.Empty,
            ["ChangeTitle"] = context.ChangeTitle ?? string.Empty,
            ["ChangeDescription"] = context.ChangeDescription ?? string.Empty,
            ["ChangeType"] = context.ChangeType ?? string.Empty,
            ["ChangePriority"] = context.ChangePriority ?? string.Empty,
            ["ChangeOrganization"] = context.ChangeOrganization ?? string.Empty,
            ["ChangeRequestedFor"] = context.ChangeRequestedFor ?? string.Empty,
            ["ChangeImplementor"] = context.ChangeImplementor ?? string.Empty,
            ["ChangeApprovers"] = context.ChangeApprovers ?? string.Empty,
            ["ChangeImplementationStart"] = context.ChangeImplementationStart ?? string.Empty,
            ["ChangeImplementationEnd"] = context.ChangeImplementationEnd ?? string.Empty,
            ["ChangeApprovalStatus"] = context.ChangeApprovalStatus ?? string.Empty,
            ["ChangeCompletionState"] = context.ChangeCompletionState ?? string.Empty,
            ["ChangeApprovalLink"] = context.ChangeApprovalLink ?? string.Empty,
            ["ChangeViewLink"] = context.ChangeViewLink ?? string.Empty,
            ["ChangeScopeOfChange"] = context.ChangeScopeOfChange ?? string.Empty,
            ["ChangeAffectedSystems"] = context.ChangeAffectedSystems ?? string.Empty,
            ["ChangeImplementationSteps"] = context.ChangeImplementationSteps ?? string.Empty,
            ["ChangeValidationSteps"] = context.ChangeValidationSteps ?? string.Empty,
            ["ChangeRollbackPlan"] = context.ChangeRollbackPlan ?? string.Empty,
            ["ChangeRollbackReference"] = context.ChangeRollbackReference ?? string.Empty,
            ["OrganizationName"] = context.OrganizationName ?? string.Empty,
            ["InviteLink"] = context.InviteLink ?? string.Empty,
            ["InviteExpiresAt"] = context.InviteExpiresAt ?? string.Empty,
            ["HelpdeskUrl"] = context.HelpdeskUrl ?? string.Empty,
            ["BrandName"] = context.BrandName ?? string.Empty,
            ["PrimaryColor"] = context.PrimaryColor ?? string.Empty,
            ["ActionColor"] = actionColor,
            ["ActionBorderColor"] = actionBorderColor,
            ["ActionTextColor"] = actionTextColor,
            ["LogoHtml"] = context.LogoHtml ?? string.Empty,
            ["FooterHtml"] = context.FooterHtml ?? string.Empty,
            ["brand"] = context.Brand
        };

        var body = templateEngine.Render(templateHtml ?? string.Empty, templateModel);

        if (string.IsNullOrWhiteSpace(context.LayoutHtml))
            return body;

        var layoutModel = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["BODY"] = body,
            ["BRAND_NAME"] = context.BrandName ?? string.Empty,
            ["PRIMARY_COLOR"] = context.PrimaryColor ?? string.Empty,
            ["ACTION_COLOR"] = actionColor,
            ["ACTION_BORDER_COLOR"] = actionBorderColor,
            ["ACTION_TEXT_COLOR"] = actionTextColor,
            ["LOGO_HTML"] = context.LogoHtml ?? string.Empty,
            ["FOOTER_HTML"] = context.FooterHtml ?? string.Empty,
            ["Body"] = body,
            ["BrandName"] = context.BrandName ?? string.Empty,
            ["PrimaryColor"] = context.PrimaryColor ?? string.Empty,
            ["ActionColor"] = actionColor,
            ["ActionBorderColor"] = actionBorderColor,
            ["ActionTextColor"] = actionTextColor,
            ["LogoHtml"] = context.LogoHtml ?? string.Empty,
            ["FooterHtml"] = context.FooterHtml ?? string.Empty,
            ["brand"] = context.Brand
        };

        return templateEngine.Render(context.LayoutHtml, layoutModel);
    }

    private static string ResolveUpdateMessage(EmailTemplateContext context) =>
        !string.IsNullOrWhiteSpace(context.UpdateMessageHtml)
            ? context.UpdateMessageHtml
            : (context.UpdateMessageText ?? string.Empty)
                .Replace("\r\n", "<br />", StringComparison.Ordinal)
                .Replace("\n", "<br />", StringComparison.Ordinal);
}
