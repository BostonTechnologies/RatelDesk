using Helpdesk.Shared.Models;

namespace Helpdesk.Infrastructure.Services;

public interface IEmailSettingsProvider
{
    Task<List<EmailInboxSettings>> GetAllAsync(CancellationToken ct);
    Task<EmailInboxSettings?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<List<EmailInboxSettings>> GetAllEnabledAsync(CancellationToken ct);
}
