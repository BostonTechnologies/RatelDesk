namespace Helpdesk.Shared.Enums;

public enum InboundEmailRuleScopeType
{
    Global = 0,
    Tenant = 1
}

public enum InboundEmailRuleConditionType
{
    SenderIsInternalSupportUser = 0,
    SenderHasAnySupportPermission = 1,
    IsForwardedEmail = 2,
    OriginalForwardedSenderExists = 3,
    MailboxEquals = 4,
    MessageNotAlreadyProcessed = 5
}

public enum InboundEmailRuleActionType
{
    CreateIncidentForOriginalForwardedSender = 0
}

public enum InboundEmailProcessingStatus
{
    Matched = 0,
    Succeeded = 1,
    Failed = 2,
    Duplicate = 3,
    TenantResolutionAmbiguous = 4,
    ParserFailed = 5,
    UnauthorizedSender = 6
}

public enum ForwardedEmailParseStatus
{
    NotForwarded = 0,
    Parsed = 1,
    MissingOriginalSender = 2,
    LowConfidence = 3
}
