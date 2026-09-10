using Helpdesk.Shared.Models;

namespace Helpdesk.Application.Services.AI;

public interface IAiProviderService
{
    Task<IEnumerable<AiProvider>> ListAsync(CancellationToken token);
    Task<AiProvider?> GetAsync(Guid id, CancellationToken token);
    Task<AiProvider> CreateAsync(AiProvider provider, string apiKey, CancellationToken token);
    Task<AiProvider?> UpdateAsync(AiProvider provider, string? apiKey, CancellationToken token);
    Task<bool> DeleteAsync(Guid id, CancellationToken token);
    Task<IEnumerable<string>> ListModelsAsync(Guid providerId, CancellationToken token);
    Task<AiProviderConnectionTestResult> TestAsync(Guid providerId, CancellationToken token);
    Task<string> ChatTestAsync(Guid providerId, string prompt, string? model, CancellationToken token);
}
