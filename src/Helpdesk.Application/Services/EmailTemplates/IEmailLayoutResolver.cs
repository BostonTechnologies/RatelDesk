using Helpdesk.Shared.Models;

namespace Helpdesk.Application.Services.EmailTemplates;

public interface IEmailLayoutResolver
{
    Task<EmailLayout?> ResolveAsync(int? layoutId, CancellationToken cancellationToken = default);
}
