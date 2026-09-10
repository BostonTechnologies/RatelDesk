using Helpdesk.Shared.Models;
using Helpdesk.Application.Sla;
using Helpdesk.Shared.Services;
using Helpdesk.Application.Messaging;
using Dodo.Primitives;

namespace Helpdesk.Application.Requests;

/// <summary>
/// Represents a command to create a new request with the specified details.
/// </summary>
/// <remarks>This command is used to encapsulate all the necessary information required to create a new request.
/// It includes details such as the title, description, priority, customer and organization identifiers, linked assets,
/// attachments, due date, and category.</remarks>
/// <param name="Title">The title of the request. This value cannot be null or empty.</param>
/// <param name="Description">The description of the request, providing additional context or details. This value cannot be null or empty.</param>
/// <param name="Priority">The priority of the request. Can be null if no priority is specified.</param>
/// <param name="CustomerId">The identifier of the customer associated with the request. Can be null if not applicable.</param>
/// <param name="OrganizationId">The identifier of the organization associated with the request. Can be null if not applicable.</param>
/// <param name="LinkedAssetIds">A collection of identifiers for assets linked to the request. Can be null or empty if no assets are linked.</param>
/// <param name="Attachments">A collection of attachment identifiers associated with the request. Can be null or empty if no attachments are
/// provided.</param>
/// <param name="DueDate">The due date for the request. Can be null if no due date is specified.</param>
public record CreateRequestCommand(
    string Title,
    string Description,
    TicketPriority? Priority,
    string? CustomerId,
    string? OrganizationId,
    IEnumerable<string>? LinkedAssetIds,
    IEnumerable<string>? Attachments,
    DateTime? DueDate,
    string? AssignedToId) : IRequest<Request>;

/// <summary>
/// Handles the creation of a new request by processing a <see cref="CreateRequestCommand"/> and returning the created
/// <see cref="Request"/> entity.
/// </summary>
/// <remarks>This handler processes the input command to create a new request entity, populating its properties
/// based on the command's data. If optional fields such as <see cref="CreateRequestCommand.LinkedAssetIds"/> or <see
/// cref="CreateRequestCommand.Attachments"/> are provided,  they will be included in the created request. Default
/// values are assigned to certain fields if not specified in the command.</remarks>
/// <param name="requests">The repository used to persist the created <see cref="Request"/> entity.</param>
public class CreateRequestCommandHandler(
    IRepository<Request> requests,
    ITicketSlaInitializer? ticketSlaInitializer = null)
    : IRequestHandler<CreateRequestCommand, Request>
{
    private readonly ITicketSlaInitializer? _ticketSlaInitializer = ticketSlaInitializer;

    public async Task<Request> Handle(CreateRequestCommand request, CancellationToken cancellationToken)
    {
        var entity = new Request
        {
            Id = Uuid.CreateVersion7().ToString(),
            Title = request.Title,
            Description = request.Description,
            Priority = request.Priority ?? TicketPriority.Low,
            CustomerId = request.CustomerId ?? "Unknown",
            OrganizationId = request.OrganizationId ?? "Unknown",
            AssignedToId = string.IsNullOrWhiteSpace(request.AssignedToId) ? null : request.AssignedToId,
            DueDate = request.DueDate,
        };
        if (request.LinkedAssetIds is not null)
        {
            entity.LinkedAssetIds.AddRange(request.LinkedAssetIds);
        }
        if (request.Attachments is not null)
        {
            entity.Attachments.AddRange(request.Attachments);
        }

        var created = await requests.CreateAsync(entity);

        try
        {
            if (_ticketSlaInitializer is not null)
            {
                await _ticketSlaInitializer.InitializeAsync(created);
            }
        }
        catch
        {
            // Ticket creation must not fail if SLA initialization fails.
        }

        return created;
    }
}
