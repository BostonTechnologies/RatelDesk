using System.Threading;
using System.Threading.Tasks;
using Helpdesk.Shared.Models;

namespace Helpdesk.Application.Services.Email;

/// <summary>
/// Converts raw email data into <see cref="Ticket"/> instances.
/// </summary>
[Obsolete("This service has been replaced by the hybrid IImapEmailService and IGraphEmailProcessor.")]
public interface IEmailIngestionService
{
    /// <summary>
    /// Parses the provided raw email string and constructs a ticket instance.
    /// </summary>
    /// <param name="rawEmail">Full contents of the email.</param>
    /// <returns>A new ticket or <c>null</c> if the email could not be parsed.</returns>
    [Obsolete("Use GraphEmailProcessor for ingestion instead.")]
    Task<Ticket?> ConvertEmailToTicketAsync(string rawEmail);

    /// <summary>
    /// Polls the configured email inbox and converts unread messages into
    /// <see cref="Incident"/> tickets.
    /// </summary>
    /// <param name="token">Cancellation token for aborting the operation.</param>
    /// <returns>The number of emails that were ingested.</returns>
    [Obsolete("Use the IMAP/Graph hybrid services instead.")]
    Task<int> IngestEmailsAsync(CancellationToken token);
}
