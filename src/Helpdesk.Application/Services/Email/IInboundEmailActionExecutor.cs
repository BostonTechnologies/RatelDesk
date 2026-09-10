using Helpdesk.Shared.DTOs.EmailRules;
using Helpdesk.Shared.Models;

namespace Helpdesk.Application.Services.Email;

public interface IInboundEmailActionExecutor
{
    Task<InboundEmailRuleProcessingResult> ExecuteAsync(
        InboundEmailRule rule,
        InboundEmailRuleActionConfig action,
        InboundEmailContext context,
        ForwardedEmailParseResult? forwarded,
        CancellationToken ct = default);
}
