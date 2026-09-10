using System.Threading;
using System.Threading.Tasks;
using Helpdesk.Application.Services.Email;
using Helpdesk.Shared.Models;

namespace Helpdesk.Tests.Mocks;

public class DummyEmailIngestionService : IEmailIngestionService
{
    public Task<Ticket?> ConvertEmailToTicketAsync(string rawEmail) => Task.FromResult<Ticket?>(null);
    public Task<int> IngestEmailsAsync(CancellationToken token) => Task.FromResult(0);
}
