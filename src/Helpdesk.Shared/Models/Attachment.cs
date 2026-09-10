namespace Helpdesk.Shared.Models;

public class Attachment
{
    public Guid Id { get; set; }
    public string TicketId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }
    public string UploadedById { get; set; } = string.Empty;
}
