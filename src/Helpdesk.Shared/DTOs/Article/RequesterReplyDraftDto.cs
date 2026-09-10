namespace Helpdesk.Shared.DTOs.Article;

public class RequesterReplyDraftDto
{
    public bool RequiresClarification { get; set; }
    public string ConfidenceLabel { get; set; } = "Low";
    public string DraftReply { get; set; } = string.Empty;
    public string SuggestedArticleTitle { get; set; } = string.Empty;
    public string SuggestedArticleLink { get; set; } = string.Empty;
    public List<string> FollowUpQuestions { get; set; } = new();
}
