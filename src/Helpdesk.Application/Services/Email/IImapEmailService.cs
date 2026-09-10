using System.Threading;
using System.Threading.Tasks;

namespace Helpdesk.Application.Services.Email;

public interface IImapEmailService
{
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
    Task<bool> TestConnectionAsync(Helpdesk.Shared.Models.ImapEmailSettings settings, CancellationToken token);
}
