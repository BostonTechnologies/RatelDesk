using System.Threading;
using System.Threading.Tasks;
using Helpdesk.Shared.Models;

namespace Helpdesk.Application.Services.Email;

public interface IGraphEmailProcessor
{
    /// <summary>
    /// Finds a message via Microsoft Graph using its universal Internet Message ID,
    /// processes it into an incident, and then categorizes the original email.
    /// </summary>
    /// <param name="internetMessageId">The unique RFC822 message-id of the email.</param>
    /// <param name="settings">The IMAP settings containing folder names.</param>
    /// <param name="token">A cancellation token.</param>
    Task ProcessEmailByInternetMessageIdAsync(string internetMessageId, ImapEmailSettings settings, CancellationToken token);
}