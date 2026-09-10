using Helpdesk.Shared.DTOs.RequestForm;

namespace Helpdesk.Application.RequestTasks;

public interface IRequestTaskDependencyGraphValidator
{
    RequestTaskDependencyGraphValidationResult Validate(IReadOnlyCollection<RequestTaskTemplateModel> templates);
}

public sealed record RequestTaskDependencyGraphValidationResult(
    bool IsValid,
    bool HasCycle,
    string? ErrorMessage);
