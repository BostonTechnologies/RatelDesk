using Helpdesk.Shared.DTOs.Change;
using Helpdesk.Shared.Models;

namespace Helpdesk.Application.Services.Changes;

public interface IChangeReviewService
{
    string NormalizeChangeType(string? changeType);
    ChangeTemplateDto? DeserializeTemplate(string? templateJson);
    string SerializeTemplate(ChangeTemplateDto? template);
    ChangeTemplateValidationDto ValidateTemplate(string? changeType, ChangeTemplateDto? template);
    ChangeAiReviewDto BuildReviewDto(Change change);
    bool RequiresReviewBeforeProgress(Change change);
    void MarkReviewStale(Change change);
    Task<ChangeAiReviewDto> RunReviewAsync(Change change, CancellationToken token);
}
