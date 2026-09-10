using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Helpdesk.Application.Messaging;
using Dodo.Primitives;

namespace Helpdesk.Application.RequestTasks;

/// <summary>
/// Represents a command to create a new request task with the specified details.
/// </summary>
/// <param name="Title">The title of the task. This value cannot be null or empty.</param>
/// <param name="Description">The description of the task, providing additional details about its purpose. This value cannot be null or empty.</param>
/// <param name="RequestId">The unique identifier of the associated request. This value cannot be null or empty.</param>
/// <param name="Priority">The priority level of the task. If null, the default priority will be used.</param>
/// <param name="CustomerId">The identifier of the customer associated with the task. This value is optional and can be null.</param>
/// <param name="OrganizationId">The identifier of the organization associated with the task. This value is optional and can be null.</param>
/// <param name="LinkedAssetIds">A collection of asset identifiers linked to the task. This value is optional and can be null or empty.</param>
/// <param name="Attachments">A collection of attachment identifiers associated with the task. This value is optional and can be null or empty.</param>
/// <param name="DueDate">The due date for the task. If null, no specific due date is assigned.</param>
public record CreateRequestTaskCommand(
    string Title,
    string Description,
    string RequestId,
    TicketPriority? Priority,
    string? CustomerId,
    string? OrganizationId,
    IEnumerable<string>? LinkedAssetIds,
    IEnumerable<string>? Attachments,
    DateTime? DueDate) : IRequest<RequestTask>;

/// <summary>
/// Handles the creation of a new <see cref="RequestTask"/> based on the provided command.
/// </summary>
/// <remarks>This handler processes a <see cref="CreateRequestTaskCommand"/> to create a new <see
/// cref="RequestTask"/> entity and persists it to the repository. The created task includes details such as title,
/// description, priority,  and optional linked assets or attachments.</remarks>
/// <param name="tasks"></param>
public class CreateRequestTaskCommandHandler(IRepository<RequestTask> tasks)
    : IRequestHandler<CreateRequestTaskCommand, RequestTask>
{
    public async Task<RequestTask> Handle(CreateRequestTaskCommand request, CancellationToken cancellationToken)
    {
        var entity = new RequestTask
        {
            Id = Uuid.CreateVersion7().ToString(),
            Title = request.Title,
            Description = request.Description,
            RequestId = request.RequestId,
            TemplateId = Uuid.CreateVersion7().ToString(),
            Type = RequestTaskType.Manual,
            Status = RequestTaskStatus.Pending,
            State = TicketState.New,
            Priority = request.Priority ?? TicketPriority.Low,
            CustomerId = request.CustomerId ?? "Unknown",
            OrganizationId = request.OrganizationId ?? "Unknown",
            DueDate = request.DueDate,
            AutomationBindingId = null,
            OrchestrationRequestDefinitionId = null,
            OrchestrationJobDefinitionId = null,
            OrchestrationExternalRequestId = null,
            OrchestrationExternalRunId = null,
            LastAutomationStatus = null,
            LastAutomationUpdatedAt = null
        };
        if (request.LinkedAssetIds is not null)
        {
            entity.LinkedAssetIds.AddRange(request.LinkedAssetIds);
        }
        if (request.Attachments is not null)
        {
            entity.Attachments.AddRange(request.Attachments);
        }
        return await tasks.CreateAsync(entity);
    }
}
