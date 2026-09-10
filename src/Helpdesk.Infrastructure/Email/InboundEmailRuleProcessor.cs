using System.Text.Json;
using Helpdesk.Application.Services.Email;
using Helpdesk.Shared.DTOs.EmailRules;
using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;
using Helpdesk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Helpdesk.Infrastructure.Email;

public sealed class InboundEmailRuleProcessor(
    HelpdeskDbContext db,
    IForwardedEmailParser forwardedEmailParser,
    IInboundEmailActionExecutor actionExecutor,
    ILogger<InboundEmailRuleProcessor> logger)
    : IInboundEmailRuleProcessor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<InboundEmailRuleProcessingResult> ProcessAsync(InboundEmailContext context, CancellationToken ct = default)
    {
        logger.LogDebug("Evaluating inbound email rules for MessageId={MessageId}", context.InternetMessageId);
        var rules = await LoadRulesAsync(context, ct);
        if (rules.Count == 0)
        {
            return new InboundEmailRuleProcessingResult(false, false, null);
        }

        ForwardedEmailParseResult? forwarded = null;
        foreach (var rule in rules)
        {
            var conditions = Deserialize<List<InboundEmailRuleConditionConfig>>(rule.ConditionsJson) ?? [];
            var actions = Deserialize<List<InboundEmailRuleActionConfig>>(rule.ActionsJson) ?? [];
            if (conditions.Any(c => NeedsForwardedParse(c.Type)) || actions.Any(a => a.Type == InboundEmailRuleActionType.CreateIncidentForOriginalForwardedSender))
            {
                forwarded ??= forwardedEmailParser.Parse(context.HtmlBody, context.TextBody);
            }

            if (!await MatchesAsync(rule, conditions, context, forwarded, ct))
            {
                continue;
            }

            foreach (var action in actions)
            {
                var result = await actionExecutor.ExecuteAsync(rule, action, context, forwarded, ct);
                if (result.Handled && (rule.StopProcessing || result.StopDefaultProcessing))
                {
                    return new InboundEmailRuleProcessingResult(true, true, result.Ticket);
                }
            }
        }

        return new InboundEmailRuleProcessingResult(false, false, null);
    }

    private async Task<List<InboundEmailRule>> LoadRulesAsync(InboundEmailContext context, CancellationToken ct)
    {
        var query = db.InboundEmailRules.AsNoTracking()
            .Where(x => x.Enabled)
            .Where(x => x.MailboxId == null || x.MailboxId == context.MailboxId);

        if (!string.IsNullOrWhiteSpace(context.MailboxTenantId))
        {
            query = query.Where(x => x.ScopeType == InboundEmailRuleScopeType.Global || x.TenantId == context.MailboxTenantId);
        }

        return await query
            .OrderByDescending(x => x.ScopeType == InboundEmailRuleScopeType.Tenant)
            .ThenBy(x => x.Priority)
            .ThenBy(x => x.CreatedAtUtc)
            .ToListAsync(ct);
    }

    private async Task<bool> MatchesAsync(
        InboundEmailRule rule,
        List<InboundEmailRuleConditionConfig> conditions,
        InboundEmailContext context,
        ForwardedEmailParseResult? forwarded,
        CancellationToken ct)
    {
        foreach (var condition in conditions)
        {
            switch (condition.Type)
            {
                case InboundEmailRuleConditionType.SenderIsInternalSupportUser:
                case InboundEmailRuleConditionType.SenderHasAnySupportPermission:
                    var forwarder = await db.Users.AsNoTracking()
                        .FirstOrDefaultAsync(x => x.Email.ToLower() == context.FromEmail.Trim().ToLowerInvariant(), ct);
                    if (forwarder is null || !InboundEmailActionExecutor.IsSupportUser(forwarder))
                    {
                        return false;
                    }
                    break;

                case InboundEmailRuleConditionType.IsForwardedEmail:
                    if (forwarded?.Status is not ForwardedEmailParseStatus.Parsed and not ForwardedEmailParseStatus.MissingOriginalSender)
                    {
                        return false;
                    }
                    break;

                case InboundEmailRuleConditionType.OriginalForwardedSenderExists:
                    if (forwarded?.HasConfidentRequester == true)
                    {
                        break;
                    }
                    if (forwarded?.Status is ForwardedEmailParseStatus.MissingOriginalSender or ForwardedEmailParseStatus.LowConfidence)
                    {
                        break;
                    }
                    if (forwarded?.Status == ForwardedEmailParseStatus.Parsed)
                    {
                        break;
                    }
                    else
                    {
                        return false;
                    }

                case InboundEmailRuleConditionType.MailboxEquals:
                    if (!MatchesMailbox(condition, context))
                    {
                        return false;
                    }
                    break;

                case InboundEmailRuleConditionType.MessageNotAlreadyProcessed:
                    if (await AlreadyProcessedAsync(rule, context, ct))
                    {
                        return false;
                    }
                    break;
            }
        }

        return true;
    }

    private async Task<bool> AlreadyProcessedAsync(InboundEmailRule rule, InboundEmailContext context, CancellationToken ct)
    {
        var mailboxKey = context.MailboxId?.ToString("D") ?? "default";
        return await db.InboundEmailProcessingLogs.AsNoTracking().AnyAsync(x =>
            x.MessageId == context.InternetMessageId &&
            x.MailboxKey == mailboxKey &&
            x.RuleId == rule.Id &&
            (x.Status == InboundEmailProcessingStatus.Succeeded || x.Status == InboundEmailProcessingStatus.Duplicate),
            ct);
    }

    private static bool MatchesMailbox(InboundEmailRuleConditionConfig condition, InboundEmailContext context)
    {
        if (string.IsNullOrWhiteSpace(condition.MailboxId))
        {
            return true;
        }

        return Guid.TryParse(condition.MailboxId, out var mailboxId) &&
               context.MailboxId.HasValue &&
               context.MailboxId.Value == mailboxId;
    }

    private static bool NeedsForwardedParse(InboundEmailRuleConditionType type) =>
        type is InboundEmailRuleConditionType.IsForwardedEmail or InboundEmailRuleConditionType.OriginalForwardedSenderExists;

    private static T? Deserialize<T>(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(string.IsNullOrWhiteSpace(json) ? "[]" : json, JsonOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }
}
