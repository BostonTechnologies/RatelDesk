using Helpdesk.Shared.Models;

namespace Helpdesk.Shared.Services;

public interface IErrorLogRepository
{
    Task SaveAsync(ErrorLog entry);
}
