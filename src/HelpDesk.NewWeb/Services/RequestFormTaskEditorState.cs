using Helpdesk.Shared.DTOs.RequestForm;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HelpDesk.NewWeb.Services;

public static class RequestFormTaskEditorState
{
    public const string ManualType = "manual";
    public const string AutomationType = "automation";
    public const string ApprovalType = "approval";

    public static JsonSerializerOptions CreateJsonOptions(bool writeIndented = false)
        => new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = writeIndented
        };

    public static string NormalizeType(string? type)
    {
        if (string.Equals(type, AutomationType, StringComparison.OrdinalIgnoreCase))
        {
            return AutomationType;
        }

        if (string.Equals(type, ApprovalType, StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "get approval", StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "get-approval", StringComparison.OrdinalIgnoreCase))
        {
            return ApprovalType;
        }

        return ManualType;
    }

    public static RequestTaskTemplateModel NormalizeTask(RequestTaskTemplateModel task, IReadOnlySet<Guid>? knownTaskIds = null)
    {
        task.DependsOn ??= new List<Guid>();
        task.ApprovalApprovers ??= new List<RequestTaskApprovalApproverModel>();
        task.Type = NormalizeType(task.Type);
        task.OrchestratorJobName = null;

        if (task.Type == AutomationType)
        {
            task.GraceRuntimeMinutes ??= 10;
        }

        if (task.Type == ApprovalType)
        {
            task.AutoStart = true;
            task.ApprovalAllowedDays = Math.Clamp(task.ApprovalAllowedDays ?? 7, 1, 14);
            task.ApprovalApprovers = task.ApprovalApprovers
                .Where(x => !string.IsNullOrWhiteSpace(x.Email))
                .Select(NormalizeApprover)
                .DistinctBy(x => x.Email, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        task.DependsOn = task.DependsOn
            .Where(x => x != Guid.Empty && x != task.Id && (knownTaskIds is null || knownTaskIds.Contains(x)))
            .Distinct()
            .ToList();

        task.ConditionExpression = string.IsNullOrWhiteSpace(task.ConditionExpression)
            ? null
            : task.ConditionExpression.Trim();

        return task;
    }

    public static List<RequestTaskTemplateModel> NormalizeTaskOrder(IEnumerable<RequestTaskTemplateModel> tasks)
    {
        var ordered = tasks
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return NormalizeTaskOrderInCurrentSequence(ordered);
    }

    public static List<RequestTaskTemplateModel> MoveTaskToIndex(
        IEnumerable<RequestTaskTemplateModel> tasks,
        Guid taskId,
        int destinationIndex)
    {
        var ordered = NormalizeTaskOrder(tasks);
        var currentIndex = ordered.FindIndex(x => x.Id == taskId);
        if (currentIndex < 0)
        {
            return ordered;
        }

        var task = ordered[currentIndex];
        ordered.RemoveAt(currentIndex);
        var boundedIndex = Math.Clamp(destinationIndex, 0, ordered.Count);
        ordered.Insert(boundedIndex, task);

        return NormalizeTaskOrderInCurrentSequence(ordered);
    }

    private static List<RequestTaskTemplateModel> NormalizeTaskOrderInCurrentSequence(List<RequestTaskTemplateModel> tasks)
    {
        var knownIds = tasks.Select(x => x.Id).ToHashSet();

        for (var i = 0; i < tasks.Count; i++)
        {
            tasks[i].Order = i + 1;
            NormalizeTask(tasks[i], knownIds);
        }

        return tasks;
    }

    private static RequestTaskApprovalApproverModel NormalizeApprover(RequestTaskApprovalApproverModel approver)
        => new()
        {
            Source = string.Equals(approver.Source, "User", StringComparison.OrdinalIgnoreCase) ? "User" : "Customer",
            Id = approver.Id?.Trim() ?? string.Empty,
            Name = approver.Name?.Trim() ?? string.Empty,
            Email = approver.Email.Trim(),
            OrganizationId = string.IsNullOrWhiteSpace(approver.OrganizationId) ? null : approver.OrganizationId.Trim(),
            OrganizationName = string.IsNullOrWhiteSpace(approver.OrganizationName) ? null : approver.OrganizationName.Trim()
        };
}
