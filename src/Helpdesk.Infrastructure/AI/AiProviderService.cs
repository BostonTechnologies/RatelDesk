using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Helpdesk.Application.Services.AI;

public class AiProviderService(
    HelpdeskDbContext db,
    ISecretProtector protector,
    IAiClient client,
    ILogger<AiProviderService> logger) : IAiProviderService
{
    private readonly HelpdeskDbContext _db = db;
    private readonly ISecretProtector _protector = protector;
    private readonly IAiClient _client = client;
    private readonly ILogger<AiProviderService> _logger = logger;

    public async Task<IEnumerable<AiProvider>> ListAsync(CancellationToken token) =>
        await _db.AiProviders.Include(p => p.Models).AsNoTracking().ToListAsync(token);

    public async Task<AiProvider?> GetAsync(Guid id, CancellationToken token) =>
        await _db.AiProviders.Include(p => p.Models).FirstOrDefaultAsync(p => p.Id == id, token);

    public async Task<AiProvider> CreateAsync(AiProvider provider, string apiKey, CancellationToken token)
    {
        provider.Id = provider.Id == Guid.Empty ? Guid.NewGuid() : provider.Id;
        provider.ApiKeyEncrypted = _protector.Protect(apiKey);
        provider.CreatedAt = DateTime.UtcNow;
        _db.AiProviders.Add(provider);
        await _db.SaveChangesAsync(token);
        _logger.LogInformation("AI provider {ProviderId} created", provider.Id);
        return provider;
    }

    public async Task<AiProvider?> UpdateAsync(AiProvider provider, string? apiKey, CancellationToken token)
    {
        var existing = await _db.AiProviders.FirstOrDefaultAsync(p => p.Id == provider.Id, token);
        if (existing is null) return null;

        existing.Name = provider.Name;
        existing.ProviderType = provider.ProviderType;
        existing.BaseUrl = provider.BaseUrl;
        existing.IsEnabled = provider.IsEnabled;
        existing.DefaultModel = provider.DefaultModel;
        existing.ExtraHeadersJson = provider.ExtraHeadersJson;
        existing.UpdatedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(apiKey))
            existing.ApiKeyEncrypted = _protector.Protect(apiKey);

        await _db.SaveChangesAsync(token);
        _logger.LogInformation("AI provider {ProviderId} updated", provider.Id);
        return existing;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken token)
    {
        var provider = await _db.AiProviders.Include(p => p.Models).FirstOrDefaultAsync(p => p.Id == id, token);
        if (provider is null) return false;
        _db.AiProviders.Remove(provider);
        await _db.SaveChangesAsync(token);
        _logger.LogInformation("AI provider {ProviderId} deleted", id);
        return true;
    }

    public async Task<IEnumerable<string>> ListModelsAsync(Guid providerId, CancellationToken token)
    {
        var provider = await _db.AiProviders
            .Include(p => p.Models)
            .FirstOrDefaultAsync(p => p.Id == providerId, token);
        if (provider is null) return Enumerable.Empty<string>();

        var raw = await _client.ListModelsAsync(provider, token) ?? Enumerable.Empty<string>();

        // 1) Normalize & validate
        var now = DateTime.UtcNow;
        var modelNames = raw
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (modelNames.Count == 0)
        {
            // Nothing valid returned — nothing to do
            return Array.Empty<string>();
        }

        // 2) Add only missing models (no touching existing rows -> avoids concurrency/rowcount issues)
        var existingNames = provider.Models.Select(m => m.Name).ToHashSet(StringComparer.Ordinal);
        var toAdd = modelNames
            .Where(n => !existingNames.Contains(n))
            .Select(n => new AiModel
            {
                Id = Guid.NewGuid(),
                AiProviderId = provider.Id,
                Name = n,
                IsEnabled = true,
                CreatedAt = now
            })
            .ToList();

        if (toAdd.Count > 0)
            _db.Set<AiModel>().AddRange(toAdd);

        // 3) (Optional) prune stale rows (models no longer advertised by provider)
        //    If you prefer to keep historical rows, comment this out.
        var toRemove = provider.Models
            .Where(m => !modelNames.Contains(m.Name, StringComparer.Ordinal))
            .ToList();
        if (toRemove.Count > 0)
            _db.Set<AiModel>().RemoveRange(toRemove);

        // 4) Save
        if (toAdd.Count > 0 || toRemove.Count > 0)
            await _db.SaveChangesAsync(token);

        return modelNames;
    }

    public async Task<AiProviderConnectionTestResult> TestAsync(Guid providerId, CancellationToken token)
    {
        var provider = await _db.AiProviders.FirstOrDefaultAsync(p => p.Id == providerId, token);
        if (provider is null)
        {
            return new AiProviderConnectionTestResult(
                false,
                Message: "Provider not found.");
        }

        var result = await _client.TestAsync(provider, token);
        _logger.LogInformation(
            "AI provider {ProviderId} test result success={Success} status={StatusCode} url={Url}",
            providerId,
            result.Success,
            result.StatusCode,
            result.AttemptedUrl);
        return result;
    }

    public async Task<string> ChatTestAsync(Guid providerId, string prompt, string? model, CancellationToken token)
    {
        var provider = await _db.AiProviders.FirstOrDefaultAsync(p => p.Id == providerId, token);
        if (provider is null) return string.Empty;
        return await _client.ChatAsync(provider, prompt, model, token);
    }
}
