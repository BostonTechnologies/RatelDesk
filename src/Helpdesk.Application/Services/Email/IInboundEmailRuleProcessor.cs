namespace Helpdesk.Application.Services.Email;

public interface IInboundEmailRuleProcessor
{
    Task<InboundEmailRuleProcessingResult> ProcessAsync(InboundEmailContext context, CancellationToken ct = default);
}
