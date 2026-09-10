using Helpdesk.Shared.DTOs.Attachment;

namespace Helpdesk.Application.Services.Tickets;

public interface ITicketAttachmentService
{
    Task<IEnumerable<AttachmentDto>> SaveAsync(string ticketId, IEnumerable<AttachmentUpload> uploads, string? uploadedById, CancellationToken token);
}
