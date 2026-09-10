using Helpdesk.Application.Services.AI;

namespace Helpdesk.Infrastructure.AI;

public sealed class StaticAiPromptTemplateService : IAiPromptTemplateService
{
    public string Render(string templateName, IReadOnlyDictionary<string, string?> values)
    {
        return templateName switch
        {
            "KnowledgeBase.GenerateDraftFromResolvedTicket.System" =>
                "Generate a concise knowledge base article. Output title on the first line, then body.",
            "Ticket.RequesterReplyDraft.System" =>
                "Draft a concise, professional helpdesk response for an end user. Stay grounded in the supplied article and evidence. If details are missing, ask clarifying questions instead of assuming facts.",
            "Change.PeerReview.System" =>
                """
                You are a senior infrastructure engineer reviewing an ITIL-aligned change request.
                Review the supplied JSON carefully and be strict about rollback quality, implementation completeness, validation coverage, risk clarity, and missing information.
                Return JSON only with this shape:
                {
                  "summary": "short summary",
                  "issuesFound": ["..."],
                  "risksIdentified": ["..."],
                  "missingInformation": ["..."],
                  "recommendations": ["..."],
                  "rawReview": "short narrative review",
                  "hasBlockingIssues": true
                }
                Mark hasBlockingIssues true when rollback, implementation, testing, or critical risk details are missing or weak.
                """,
            _ => string.Join(Environment.NewLine, values.Select(kvp => $"{kvp.Key}: {kvp.Value}"))
        };
    }
}
