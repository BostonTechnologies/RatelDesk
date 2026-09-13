using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.DTOs.Attachment;
using Helpdesk.Shared.Models;
using Helpdesk.Infrastructure.Storage;

namespace Helpdesk.Application.Services.Tickets;

public class TicketAttachmentService(HelpdeskDbContext db, TicketAttachmentFileStore fileStore) : ITicketAttachmentService
{
    private readonly HelpdeskDbContext _db = db;
    private readonly TicketAttachmentFileStore _fileStore = fileStore;

    public async Task<IEnumerable<AttachmentDto>> SaveAsync(string ticketId, IEnumerable<AttachmentUpload> uploads, string? uploadedById, CancellationToken token)
    {
        var results = new List<AttachmentDto>();

        foreach (var upload in uploads)
        {
            var uniqueName = $"{Guid.NewGuid()}{Path.GetExtension(upload.FileName)}";
            var filePath = _fileStore.GetWritePath(uniqueName);
            await File.WriteAllBytesAsync(filePath, upload.Content, token);

            var entity = new Attachment
            {
                Id = Guid.NewGuid(),
                TicketId = ticketId,
                FileName = upload.FileName,
                FilePath = uniqueName,
                ContentType = upload.ContentType,
                SizeBytes = upload.Content.LongLength,
                CreatedAt = DateTime.UtcNow,
                UploadedById = uploadedById ?? string.Empty
            };

            _db.Attachments.Add(entity);
            results.Add(new AttachmentDto(entity.Id, entity.TicketId, entity.FileName, entity.ContentType, entity.SizeBytes, entity.CreatedAt));
        }

        await _db.SaveChangesAsync(token);
        return results;
    }
}
