namespace Helpdesk.Shared.DTOs;

public sealed class PublicTicketTimelineItemDto
{
    public string Id { get; init; } = string.Empty;
    public DateTime CreatedUtc { get; init; }
    public string AuthorName { get; init; } = string.Empty;
    public string AuthorType { get; init; } = string.Empty;
    public bool IsCustomer { get; init; }
    public string NotesHtml { get; init; } = string.Empty;
}
