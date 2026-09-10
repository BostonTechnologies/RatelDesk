using Helpdesk.Shared.DTOs.EmailRules;
using Helpdesk.Shared.Enums;
using MudBlazor;

namespace HelpDesk.NewWeb.Components.Pages.Admin.EmailRules;

internal static class InboundEmailRuleLabels
{
    public static string Scope(InboundEmailRuleScopeType value) => value switch
    {
        InboundEmailRuleScopeType.Global => "Global fallback",
        InboundEmailRuleScopeType.Tenant => "Tenant-specific",
        _ => value.ToString()
    };

    public static string Condition(InboundEmailRuleConditionConfig condition) => condition.Type switch
    {
        InboundEmailRuleConditionType.SenderIsInternalSupportUser => "Sender is an internal support user",
        InboundEmailRuleConditionType.SenderHasAnySupportPermission => "Sender has a support permission",
        InboundEmailRuleConditionType.IsForwardedEmail => "Message looks like a forwarded email",
        InboundEmailRuleConditionType.OriginalForwardedSenderExists => "Original forwarded sender can be extracted",
        InboundEmailRuleConditionType.MailboxEquals => string.IsNullOrWhiteSpace(condition.MailboxId)
            ? "Mailbox matches configured rule mailbox"
            : $"Mailbox is {condition.MailboxId}",
        InboundEmailRuleConditionType.MessageNotAlreadyProcessed => "Message has not already been processed by this rule action",
        _ => condition.Type.ToString()
    };

    public static string Action(InboundEmailRuleActionConfig action) => action.Type switch
    {
        InboundEmailRuleActionType.CreateIncidentForOriginalForwardedSender => "Create incident from forwarded support email",
        _ => action.Type.ToString()
    };

    public static string ActionDescription(InboundEmailRuleActionConfig action) => action.Type switch
    {
        InboundEmailRuleActionType.CreateIncidentForOriginalForwardedSender =>
            "Creates a New / Unassigned incident for the original requester when an authorized support user forwards a customer email into the helpdesk mailbox.",
        _ => string.Empty
    };

    public static string Status(InboundEmailProcessingStatus value) => value switch
    {
        InboundEmailProcessingStatus.Matched => "Matched",
        InboundEmailProcessingStatus.Succeeded => "Succeeded",
        InboundEmailProcessingStatus.Failed => "Failed",
        InboundEmailProcessingStatus.Duplicate => "Duplicate",
        InboundEmailProcessingStatus.TenantResolutionAmbiguous => "Ambiguous tenant",
        InboundEmailProcessingStatus.ParserFailed => "Parser failed",
        InboundEmailProcessingStatus.UnauthorizedSender => "Unauthorized sender",
        _ => value.ToString()
    };

    public static Color StatusColor(InboundEmailProcessingStatus value) => value switch
    {
        InboundEmailProcessingStatus.Succeeded => Color.Success,
        InboundEmailProcessingStatus.Matched => Color.Info,
        InboundEmailProcessingStatus.Duplicate => Color.Secondary,
        InboundEmailProcessingStatus.TenantResolutionAmbiguous => Color.Warning,
        InboundEmailProcessingStatus.ParserFailed => Color.Warning,
        InboundEmailProcessingStatus.UnauthorizedSender => Color.Warning,
        InboundEmailProcessingStatus.Failed => Color.Error,
        _ => Color.Default
    };
}
