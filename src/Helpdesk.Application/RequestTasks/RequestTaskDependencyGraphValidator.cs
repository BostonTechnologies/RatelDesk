using Helpdesk.Shared.DTOs.RequestForm;

namespace Helpdesk.Application.RequestTasks;

public sealed class RequestTaskDependencyGraphValidator : IRequestTaskDependencyGraphValidator
{
    private const string CircularDependencyError = "Circular task dependency detected in workflow.";
    private const string UnknownDependencyError = "Task dependency references unknown task template ID.";
    private const string DuplicateTemplateIdError = "Task template IDs must be unique within the workflow.";

    public RequestTaskDependencyGraphValidationResult Validate(IReadOnlyCollection<RequestTaskTemplateModel> templates)
    {
        if (templates.Count == 0)
        {
            return new RequestTaskDependencyGraphValidationResult(true, false, null);
        }

        var nonEmptyTemplateIds = templates
            .Where(x => x.Id != Guid.Empty)
            .Select(x => x.Id)
            .ToList();

        var templateIds = nonEmptyTemplateIds.ToHashSet();
        if (templateIds.Count != nonEmptyTemplateIds.Count)
        {
            return new RequestTaskDependencyGraphValidationResult(false, false, DuplicateTemplateIdError);
        }

        var dependencies = templates
            .Where(x => x.Id != Guid.Empty)
            .ToDictionary(
                x => x.Id,
                x => (x.DependsOn ?? new List<Guid>())
                    .Where(dep => dep != Guid.Empty)
                    .Distinct()
                    .ToList());

        foreach (var template in templates.Where(x => x.Id != Guid.Empty))
        {
            foreach (var dependencyId in template.DependsOn ?? new List<Guid>())
            {
                if (dependencyId == Guid.Empty)
                {
                    return new RequestTaskDependencyGraphValidationResult(false, false, UnknownDependencyError);
                }

                if (!templateIds.Contains(dependencyId))
                {
                    return new RequestTaskDependencyGraphValidationResult(false, false, UnknownDependencyError);
                }
            }
        }

        var visitState = new Dictionary<Guid, int>(templateIds.Count);

        foreach (var id in templateIds)
        {
            if (DetectCycle(id, dependencies, visitState))
            {
                return new RequestTaskDependencyGraphValidationResult(false, true, CircularDependencyError);
            }
        }

        return new RequestTaskDependencyGraphValidationResult(true, false, null);
    }

    private static bool DetectCycle(
        Guid id,
        IReadOnlyDictionary<Guid, List<Guid>> dependencies,
        Dictionary<Guid, int> visitState)
    {
        if (visitState.TryGetValue(id, out var state))
        {
            return state == 1;
        }

        visitState[id] = 1;

        if (dependencies.TryGetValue(id, out var children))
        {
            foreach (var child in children)
            {
                if (DetectCycle(child, dependencies, visitState))
                {
                    return true;
                }
            }
        }

        visitState[id] = 2;
        return false;
    }
}
