using System.Threading;
using System.Threading.Tasks;
using Helpdesk.Shared.Models;

namespace Helpdesk.Application.Services.AI;

public interface IAiClient
{
    Task<IEnumerable<string>> ListModelsAsync(AiProvider provider, CancellationToken token);
    Task<AiProviderConnectionTestResult> TestAsync(AiProvider provider, CancellationToken token);
    Task<string> ChatAsync(AiProvider provider, string prompt, string? model, CancellationToken token);
}
