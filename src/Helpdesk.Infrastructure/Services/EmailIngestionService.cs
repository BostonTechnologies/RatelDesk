using Helpdesk.Application.Services.Email;
using Helpdesk.Shared.Models;
using Microsoft.Extensions.Logging;

namespace Helpdesk.Infrastructure.Services;

public class EmailIngestionService : IEmailIngestionService
{
    private readonly ILogger<EmailIngestionService> _logger;

    public EmailIngestionService(ILogger<EmailIngestionService> logger)
    {
        _logger = logger;
    }

    public Task<Ticket?> ConvertEmailToTicketAsync(string rawEmail)
    {
        _logger.LogDebug("Legacy ConvertEmailToTicketAsync invoked. Returning no ticket.");
        return Task.FromResult<Ticket?>(null);
    }

    public Task<int> IngestEmailsAsync(CancellationToken token)
    {
        _logger.LogDebug("Legacy IngestEmailsAsync invoked. IMAP/Graph background ingestion remains authoritative.");
        return Task.FromResult(0);
    }
}
