using Helpdesk.Shared.DTOs.RequestForm;
using Helpdesk.Shared.Models;

namespace Helpdesk.Application.RequestTasks;

public static class RequestTaskApprovalGate
{
    public static List<Guid> GetEffectiveDependencies(
        RequestTaskTemplateModel template,
        IReadOnlyList<RequestTaskTemplateModel> orderedTemplates)
    {
        var dependencies = (template.DependsOn ?? new List<Guid>())
            .Where(x => x != Guid.Empty && x != template.Id)
            .ToList();

        foreach (var earlierApproval in orderedTemplates
            .Where(x => x.Order < template.Order
                && x.Id != Guid.Empty
                && string.Equals(x.Type, "approval", StringComparison.OrdinalIgnoreCase)))
        {
            dependencies.Add(earlierApproval.Id);
        }

        return dependencies
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();
    }

    public static bool IsBlockedByEarlierApproval(RequestTask task, IEnumerable<RequestTask> requestTasks)
        => task.Type != RequestTaskType.Approval
            && requestTasks.Any(x => x.RequestId == task.RequestId
                && x.Type == RequestTaskType.Approval
                && x.Order < task.Order
                && x.Status is not (RequestTaskStatus.Completed or RequestTaskStatus.Skipped));
}
