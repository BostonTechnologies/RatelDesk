namespace Helpdesk.Shared.DTOs.Attachment;

public record AttachmentUpload(
    string FileName,
    string ContentType,
    byte[] Content
);
