using Helpdesk.Shared.Enums;

namespace Helpdesk.Shared.DTOs.EmailRules;

public sealed record InboundEmailRuleDto(
    string Id,
    InboundEmailRuleScopeType ScopeType,
    string? TenantId,
    Guid? MailboxId,
    string Name,
    string? Description,
    bool Enabled,
    int Priority,
    List<InboundEmailRuleConditionConfig> Conditions,
    List<InboundEmailRuleActionConfig> Actions,
    bool StopProcessing,
    string? CreatedBy,
    string? UpdatedBy,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record CreateInboundEmailRuleRequest(
    InboundEmailRuleScopeType ScopeType,
    string? TenantId,
    Guid? MailboxId,
    string Name,
    string? Description,
    bool Enabled,
    int Priority,
    List<InboundEmailRuleConditionConfig>? Conditions,
    List<InboundEmailRuleActionConfig>? Actions,
    bool StopProcessing);

public sealed record UpdateInboundEmailRuleRequest(
    InboundEmailRuleScopeType ScopeType,
    string? TenantId,
    Guid? MailboxId,
    string Name,
    string? Description,
    bool Enabled,
    int Priority,
    List<InboundEmailRuleConditionConfig>? Conditions,
    List<InboundEmailRuleActionConfig>? Actions,
    bool StopProcessing);

public sealed record ReorderInboundEmailRulesRequest(List<InboundEmailRulePriorityDto> Rules);

public sealed record InboundEmailRulePriorityDto(string Id, int Priority);

public sealed record InboundEmailRuleAuditDto(
    string Id,
    string MessageId,
    Guid? MailboxId,
    string? TenantId,
    string? RuleId,
    string ActionKey,
    bool Matched,
    InboundEmailProcessingStatus Status,
    string? TicketId,
    string? Error,
    DateTimeOffset CreatedAtUtc);

public sealed record InboundEmailRuleConditionConfig(
    InboundEmailRuleConditionType Type,
    string? MailboxId = null,
    List<string>? Permissions = null);

public sealed record InboundEmailRuleActionConfig(
    InboundEmailRuleActionType Type,
    string? ActionKey = null);
