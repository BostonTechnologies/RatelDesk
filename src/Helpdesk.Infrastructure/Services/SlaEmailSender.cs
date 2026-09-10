using Helpdesk.Application.Services.Email;
using Helpdesk.Application.Sla;

namespace Helpdesk.Infrastructure.Services;

public class SlaEmailSender(IEmailService emailService) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        if (message.To.Count == 0)
        {
            return;
        }

        var attachments = message.Attachments
            .Select(x => new EmailAttachmentData
            {
                FileName = x.FileName,
                ContentType = x.ContentType,
                ContentBytes = x.ContentBytes
            })
            .ToList();

        await emailService.SendEmailAsync(
            message.To,
            message.Subject,
            message.HtmlBody,
            null,
            ct,
            null,
            attachments);
    }
}
