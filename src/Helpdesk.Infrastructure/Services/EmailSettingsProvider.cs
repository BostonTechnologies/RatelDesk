using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Helpdesk.Infrastructure.Services;

public class EmailSettingsProvider : IEmailSettingsProvider
{
    private readonly HelpdeskDbContext _db;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    public EmailSettingsProvider(HelpdeskDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<List<EmailInboxSettings>> GetAllAsync(CancellationToken ct)
    {
        return await _cache.GetOrCreateAsync("email_all", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await _db.EmailInboxSettings.AsNoTracking().ToListAsync(ct);
        }) ?? [];
    }

    public async Task<EmailInboxSettings?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var all = await GetAllAsync(ct);
        return all.FirstOrDefault(x => x.Id == id);
    }

    public async Task<List<EmailInboxSettings>> GetAllEnabledAsync(CancellationToken ct)
    {
        var all = await GetAllAsync(ct);
        return all.Where(x => x.Enabled).ToList();
    }
}
