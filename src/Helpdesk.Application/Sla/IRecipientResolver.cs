using Helpdesk.Shared.Models;

namespace Helpdesk.Application.Sla;

public interface IRecipientResolver
{
    Task<List<string>> ResolveEmailsAsync(string tenantId, List<RecipientTarget> targets, CancellationToken ct = default);
}
