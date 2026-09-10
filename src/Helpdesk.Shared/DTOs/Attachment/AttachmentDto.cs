namespace Helpdesk.Shared.DTOs.Attachment;

public record AttachmentDto(
    Guid Id,
    string TicketId,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTime CreatedAt
);
