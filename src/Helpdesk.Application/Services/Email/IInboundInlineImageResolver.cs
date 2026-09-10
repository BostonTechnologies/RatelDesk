namespace Helpdesk.Application.Services.Email;

public interface IInboundInlineImageResolver
{
    Task<InboundInlineImageResult> ResolveAsync(
        string incidentId,
        string html,
        IReadOnlyList<InboundEmailAttachmentContext> attachments,
        string? graphMessageId = null,
        string? internetMessageId = null,
        CancellationToken cancellationToken = default);
}

// Indexes identify attachment entries even when Graph IDs are missing or duplicated.
public sealed record InboundInlineImageResult(string Html, IReadOnlySet<int> ConsumedAttachmentIndexes);
