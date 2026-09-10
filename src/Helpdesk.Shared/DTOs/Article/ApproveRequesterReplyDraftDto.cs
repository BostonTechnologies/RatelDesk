namespace Helpdesk.Shared.DTOs.Article;

public class ApproveRequesterReplyDraftDto
{
    public string? EditedReply { get; set; }
    public bool IncludeFollowUpQuestions { get; set; } = true;
}
