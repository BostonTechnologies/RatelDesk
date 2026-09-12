using System.Text.Json;
using Helpdesk.Application.Events;
using Helpdesk.Application.Orchestration;
using Helpdesk.Application.RequestTasks;
using Helpdesk.API.Services;
using AppTicketServices = Helpdesk.Application.Services.Tickets;
using Helpdesk.Application.Services.Notifications;
using Helpdesk.Application.Services.Tenants;
using Helpdesk.Application.Sla;
using Helpdesk.Application.Workflow;
using Helpdesk.Application.Resources;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.Auth;
using Helpdesk.Shared.DTOs.Request;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Helpdesk.API.Endpoints.Requests;

public static class SelfServiceRequestEndpoints
{
    private const int MaxPayloadBytes = 64 * 1024;

    public static void MapSelfServiceRequestEndpoints(this IEndpointRouteBuilder app)
    {
        var selfService = app.MapGroup("/api/v1/self-service")
            .WithTags("Self Service")
            .RequireAuthorization("SelfService.User");

        selfService.MapGet("/request-users", SearchRequestUsers)
            .WithName("SearchSelfServiceRequestUsers")
            .WithSummary("Search users and customer contacts available for submit-on-behalf self-service requests.");

        var group = app.MapGroup("/api/v1/self-service/requests")
            .WithTags("Self Service")
            .RequireAuthorization("SelfService.User");

        group.MapPost("/", SubmitRequest)
            .WithName("SubmitSelfServiceRequest")
            .WithSummary("Submit request from self-service portal")
            .WithDescription("Creates a Request from a RequestForm schema payload and generates RequestTasks.");
    }

    private static async Task<IResult> SubmitRequest(
        [FromBody] SubmitSelfServiceRequestDto dto,
        [FromServices] HelpdeskDbContext db,
        [FromServices] IRepository<RequestForm> requestForms,
        [FromServices] IRepository<Request> requests,
        [FromServices] IAutomationBindingPayloadContractService automationPayloadContractService,
        [FromServices] IRequestFormSchemaParser schemaParser,
        [FromServices] ISelfServiceDatasetBindingResolver datasetBindingResolver,
        [FromServices] ISelfServiceAudienceService selfServiceAudienceService,
        [FromServices] IRequestTaskGenerationService requestTaskGeneration,
        [FromServices] IWorkflowEngine workflowEngine,
        [FromServices] ITicketSlaInitializer ticketSlaInitializer,
        [FromServices] AppTicketServices.ITicketRefGeneratorService refs,
        [FromServices] ITicketNotificationService ticketNotificationService,
        [FromServices] ITenantProvisioningService tenantProvisioningService,
        [FromServices] ITenantContext tenant,
        [FromServices] ICurrentUserAccessService accessService,
        [FromServices] IDomainEventPublisher domainEvents,
        [FromServices] ICorrelationContext correlation,
        [FromServices] ILoggerFactory loggerFactory,
        ClaimsPrincipal user,
        CancellationToken token)
    {
        var logger = loggerFactory.CreateLogger("SelfServiceRequestEndpoints");
        var requesterEmail = FirstNonEmpty(ResolveUserEmail(user, tenant), LooksLikeEmail(dto.RequesterEmail) ? dto.RequesterEmail : null);
        if (string.IsNullOrWhiteSpace(requesterEmail))
        {
            return Results.BadRequest("Authenticated self-service requests require an email address.");
        }

        var requesterName = FirstNonEmpty(dto.RequesterName, ResolveUserName(user), requesterEmail) ?? requesterEmail;
        var requesterDomain = requesterEmail.Split('@', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        if (string.IsNullOrWhiteSpace(requesterDomain))
        {
            return Results.BadRequest("Authenticated self-service requests require a valid email address.");
        }

        if (string.IsNullOrWhiteSpace(dto.RequestFormId))
        {
            return Results.BadRequest("RequestFormId is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.PayloadJson))
        {
            return Results.BadRequest("PayloadJson is required.");
        }

        if (global::System.Text.Encoding.UTF8.GetByteCount(dto.PayloadJson) > MaxPayloadBytes)
        {
            return Results.BadRequest($"PayloadJson exceeds maximum size of {MaxPayloadBytes} bytes.");
        }

        try
        {
            using var _ = JsonDocument.Parse(dto.PayloadJson);
        }
        catch (JsonException)
        {
            return Results.BadRequest("PayloadJson must be valid JSON.");
        }

        var requestForm = await requestForms.GetAsync(dto.RequestFormId);
        if (requestForm is null)
        {
            return Results.NotFound("RequestForm not found.");
        }

        if (!await selfServiceAudienceService.CanAccessRequestFormAsync(requestForm, token))
        {
            return Results.Forbid();
        }

        var access = await accessService.ResolveAsync(user, token);
        var submitterEmail = requesterEmail;

        var (customer, requestedForNameResult) = await ResolveRequestedForCustomerAsync(
            dto,
            db,
            access,
            tenant,
            tenantProvisioningService,
            requesterEmail,
            requesterName,
            requesterDomain,
            token);
        if (customer is null)
        {
            return Results.BadRequest("Requested-for user was not found or is outside your organization scope.");
        }

        var requestedForName = requestedForNameResult ?? customer.Name;

        var parsedSchema = schemaParser.Parse(requestForm.JsonSchema);
        var normalizedPayload = await datasetBindingResolver.NormalizePayloadAsync(
            customer.OrganizationId,
            parsedSchema.Fields,
            dto.PayloadJson,
            token);
        if (!normalizedPayload.Success)
        {
            return Results.BadRequest(string.Join(Environment.NewLine, normalizedPayload.Errors));
        }

        var automationPayloadValidation = await automationPayloadContractService.ValidateBoundRequestPayloadAsync(
            requestForm,
            normalizedPayload.PayloadJson,
            token);
        if (!automationPayloadValidation.Success)
        {
            return Results.BadRequest(string.Join(Environment.NewLine, automationPayloadValidation.Errors));
        }

        var schema = ExtractSchemaMetadata(requestForm.JsonSchema);
        var request = new Request
        {
            Title = string.IsNullOrWhiteSpace(dto.Title) ? schema.Title : dto.Title,
            Description = string.IsNullOrWhiteSpace(dto.Description) ? schema.Description : dto.Description,
            RequestFormId = requestForm.Id,
            ServiceId = requestForm.ServiceId,
            PayloadJson = normalizedPayload.PayloadJson,
            OrganizationId = customer.OrganizationId,
            CustomerId = customer.Id,
            RequesterEmail = customer.Email,
            TrackingId = await refs.NextReferenceAsync("REQ"),
            Priority = TicketPriority.Low,
            State = TicketState.New
        };
        if (!string.IsNullOrWhiteSpace(dto.RequestedForPersonId))
        {
            request.CcRecipients = BuildListeners(customer.Email, submitterEmail);
        }

        await using var tx = await db.Database.BeginTransactionAsync(token);
        logger.LogInformation("SubmitRequest DbContext instance hash: {Hash}", db.GetHashCode());

        var created = await requests.CreateAsync(request);
        try
        {
            await ticketSlaInitializer.InitializeAsync(created);
        }
        catch
        {
            // Request submission must not fail if SLA initialization fails.
        }

        await requestTaskGeneration.GenerateForRequestAsync(created, token);
        await db.SaveChangesAsync(token);
        await tx.CommitAsync(token);

        await workflowEngine.RunAsync(created.Id, WorkflowRunReason.RequestCreated, token);

        await domainEvents.PublishAsync(
            new SelfServiceRequestSubmittedEvent(
                created.Id,
                requestForm.Id,
                requestForm.ServiceId,
                created.TrackingId,
                customer.Id,
                customer.OrganizationId,
                DateTimeOffset.UtcNow,
                GetCorrelationId(correlation)),
            token);

        _ = await ticketNotificationService.SendSelfServiceRequestCreatedAsync(
            created,
            requestForm,
            customer.Email,
            requestedForName,
            created.CcRecipients,
            token);

        return Results.Ok(new SelfServiceRequestSubmittedDto
        {
            RequestId = created.Id,
            TrackingId = created.TrackingId
        });
    }

    private static async Task<IResult> SearchRequestUsers(
        [FromQuery] string? query,
        [FromQuery] int? pageSize,
        [FromServices] HelpdeskDbContext db,
        [FromServices] ITenantContext tenant,
        [FromServices] ICurrentUserAccessService accessService,
        ClaimsPrincipal user,
        CancellationToken token)
    {
        var access = await accessService.ResolveAsync(user, token);
        var visibleOrganizationIds = await ResolveVisibleOrganizationIdsAsync(db, tenant, access, token);
        var isAdmin = access.IsHelpdeskAdmin || tenant.IsHelpdeskAdmin;
        var take = Math.Clamp(pageSize ?? 25, 1, 100);
        var search = query?.Trim();

        var organizationNames = await db.Organizations
            .AsNoTracking()
            .Where(x => isAdmin || visibleOrganizationIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, token);

        var usersQuery = db.Users.AsNoTracking();
        if (!isAdmin)
        {
            usersQuery = usersQuery.Where(x => x.OrganizationId != null && visibleOrganizationIds.Contains(x.OrganizationId));
        }

        var customersQuery = db.Customers.AsNoTracking()
            .Where(x => x.State == Helpdesk.Shared.Models.EntityState.Enabled);
        if (!isAdmin)
        {
            customersQuery = customersQuery.Where(x => visibleOrganizationIds.Contains(x.OrganizationId));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search}%";
            usersQuery = usersQuery.Where(x =>
                EF.Functions.Like(x.Name, pattern) ||
                EF.Functions.Like(x.Email, pattern));
            customersQuery = customersQuery.Where(x =>
                EF.Functions.Like(x.Name, pattern) ||
                EF.Functions.Like(x.Email, pattern));
        }

        var users = await usersQuery
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Email)
            .Take(take)
            .Select(x => new SelfServiceRequestUserDto
            {
                Id = x.Id,
                Name = x.Name,
                Email = x.Email,
                Source = SelfServiceRequestPersonSource.User,
                OrganizationId = x.OrganizationId,
                Role = x.Role
            })
            .ToListAsync(token);

        var remaining = Math.Max(0, take - users.Count);
        var customers = remaining == 0
            ? new List<SelfServiceRequestUserDto>()
            : await customersQuery
                .OrderBy(x => x.Name)
                .ThenBy(x => x.Email)
                .Take(remaining)
                .Select(x => new SelfServiceRequestUserDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    Email = x.Email,
                    Source = SelfServiceRequestPersonSource.Customer,
                    OrganizationId = x.OrganizationId,
                    Role = "Customer"
                })
                .ToListAsync(token);

        var results = users
            .Concat(customers)
            .Where(x => !string.IsNullOrWhiteSpace(x.Email))
            .DistinctBy(x => $"{x.Source}:{x.Id}", StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Email, StringComparer.OrdinalIgnoreCase)
            .Take(take)
            .ToList();

        foreach (var result in results)
        {
            if (!string.IsNullOrWhiteSpace(result.OrganizationId)
                && organizationNames.TryGetValue(result.OrganizationId, out var organizationName))
            {
                result.OrganizationName = organizationName;
            }
        }

        return Results.Ok(results);
    }

    private static async Task<(Customer? Customer, string? Name)> ResolveRequestedForCustomerAsync(
        SubmitSelfServiceRequestDto dto,
        HelpdeskDbContext db,
        CurrentUserAccessProfile access,
        ITenantContext tenant,
        ITenantProvisioningService tenantProvisioningService,
        string requesterEmail,
        string requesterName,
        string requesterDomain,
        CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(dto.RequestedForPersonId))
        {
            var scopedOrganizationId = access.PrimaryOrganizationId;
            if (!string.IsNullOrWhiteSpace(scopedOrganizationId)
                && access.ScopedPermissionGrants.Contains(
                    new ScopedPermissionGrant(HelpdeskPermissions.SelfServiceUser, scopedOrganizationId)))
            {
                var normalizedEmail = requesterEmail.Trim().ToLowerInvariant();
                var existingCustomer = await db.Customers
                    .FirstOrDefaultAsync(x => x.Email.ToLower() == normalizedEmail, token);
                if (existingCustomer is not null)
                {
                    return existingCustomer.State == Helpdesk.Shared.Models.EntityState.Enabled
                           && string.Equals(existingCustomer.OrganizationId, scopedOrganizationId, StringComparison.OrdinalIgnoreCase)
                        ? (existingCustomer, existingCustomer.Name)
                        : (null, null);
                }

                var provisionedCustomer = new Customer
                {
                    Name = requesterName,
                    Email = normalizedEmail,
                    OrganizationId = scopedOrganizationId,
                    State = Helpdesk.Shared.Models.EntityState.Enabled
                };
                db.Customers.Add(provisionedCustomer);
                try
                {
                    await db.SaveChangesAsync(token);
                    return (provisionedCustomer, provisionedCustomer.Name);
                }
                catch (DbUpdateException)
                {
                    db.Entry(provisionedCustomer).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
                    var racingCustomer = await db.Customers
                        .FirstOrDefaultAsync(x => x.Email.ToLower() == normalizedEmail, token);
                    return racingCustomer?.State == Helpdesk.Shared.Models.EntityState.Enabled
                           && string.Equals(racingCustomer.OrganizationId, scopedOrganizationId, StringComparison.OrdinalIgnoreCase)
                        ? (racingCustomer, racingCustomer.Name)
                        : (null, null);
                }
            }

            var (customer, _) = await tenantProvisioningService.GetOrCreateCustomerAsync(
                requesterEmail,
                requesterName,
                requesterDomain);
            return (customer, customer.Name);
        }

        if (dto.RequestedForPersonSource is null)
        {
            return (null, null);
        }

        var visibleOrganizationIds = await ResolveVisibleOrganizationIdsAsync(db, tenant, access, token);
        var isAdmin = access.IsHelpdeskAdmin || tenant.IsHelpdeskAdmin;
        return dto.RequestedForPersonSource.Value switch
        {
            SelfServiceRequestPersonSource.Customer => await ResolveRequestedCustomerAsync(
                db,
                dto.RequestedForPersonId,
                visibleOrganizationIds,
                isAdmin,
                token),
            SelfServiceRequestPersonSource.User => await ResolveRequestedUserAsCustomerAsync(
                db,
                dto.RequestedForPersonId,
                visibleOrganizationIds,
                isAdmin,
                token),
            _ => (null, null)
        };
    }

    private static async Task<(Customer? Customer, string? Name)> ResolveRequestedCustomerAsync(
        HelpdeskDbContext db,
        string id,
        IReadOnlySet<string> visibleOrganizationIds,
        bool isAdmin,
        CancellationToken token)
    {
        var customer = await db.Customers
            .FirstOrDefaultAsync(x => x.Id == id && x.State == Helpdesk.Shared.Models.EntityState.Enabled, token);
        if (customer is null || !CanUseOrganization(customer.OrganizationId, visibleOrganizationIds, isAdmin))
        {
            return (null, null);
        }

        return (customer, customer.Name);
    }

    private static async Task<(Customer? Customer, string? Name)> ResolveRequestedUserAsCustomerAsync(
        HelpdeskDbContext db,
        string id,
        IReadOnlySet<string> visibleOrganizationIds,
        bool isAdmin,
        CancellationToken token)
    {
        var selectedUser = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, token);
        if (selectedUser is null
            || string.IsNullOrWhiteSpace(selectedUser.OrganizationId)
            || string.IsNullOrWhiteSpace(selectedUser.Email)
            || !CanUseOrganization(selectedUser.OrganizationId, visibleOrganizationIds, isAdmin))
        {
            return (null, null);
        }

        var email = selectedUser.Email.Trim().ToLowerInvariant();
        var customer = await db.Customers
            .FirstOrDefaultAsync(x =>
                x.OrganizationId == selectedUser.OrganizationId &&
                x.Email.ToLower() == email &&
                x.State == Helpdesk.Shared.Models.EntityState.Enabled,
                token);
        if (customer is null)
        {
            customer = new Customer
            {
                Name = string.IsNullOrWhiteSpace(selectedUser.Name) ? selectedUser.Email : selectedUser.Name,
                Email = email,
                OrganizationId = selectedUser.OrganizationId,
                State = Helpdesk.Shared.Models.EntityState.Enabled
            };
            db.Customers.Add(customer);
            await db.SaveChangesAsync(token);
        }

        return (customer, selectedUser.Name);
    }

    private static async Task<HashSet<string>> ResolveVisibleOrganizationIdsAsync(
        HelpdeskDbContext db,
        ITenantContext? tenant,
        CurrentUserAccessProfile access,
        CancellationToken token)
    {
        var organizationIds = access.AllowedOrganizationIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(access.PrimaryOrganizationId))
        {
            organizationIds.Add(access.PrimaryOrganizationId);
        }

        if (!string.IsNullOrWhiteSpace(tenant?.TenantId))
        {
            organizationIds.Add(tenant.TenantId);
        }

        if (access.IsHelpdeskAdmin || tenant?.IsHelpdeskAdmin == true)
        {
            var allEnabled = await db.Organizations
                .AsNoTracking()
                .Where(x => x.State == Helpdesk.Shared.Models.EntityState.Enabled)
                .Select(x => x.Id)
                .ToListAsync(token);
            foreach (var organizationId in allEnabled)
            {
                organizationIds.Add(organizationId);
            }
        }

        return organizationIds;
    }

    private static bool CanUseOrganization(
        string? organizationId,
        IReadOnlySet<string> visibleOrganizationIds,
        bool isAdmin) =>
        isAdmin || !string.IsNullOrWhiteSpace(organizationId) && visibleOrganizationIds.Contains(organizationId);

    private static List<string> BuildListeners(params string?[] emails)
    {
        return emails
            .Where(LooksLikeEmail)
            .Select(email => email!.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static (string Title, string Description) ExtractSchemaMetadata(JsonDocument schema)
    {
        var title = "Request";
        var description = string.Empty;

        if (schema.RootElement.ValueKind == JsonValueKind.Object)
        {
            if (schema.RootElement.TryGetProperty("title", out var titleNode) && titleNode.ValueKind == JsonValueKind.String)
            {
                title = titleNode.GetString() ?? title;
            }

            if (schema.RootElement.TryGetProperty("description", out var descriptionNode) && descriptionNode.ValueKind == JsonValueKind.String)
            {
                description = descriptionNode.GetString() ?? description;
            }
        }

        return (string.IsNullOrWhiteSpace(title) ? "Request" : title, description);
    }

    private static string GetCorrelationId(ICorrelationContext correlation)
    {
        return correlation.GetCorrelationId() ?? $"corr-{Guid.NewGuid():N}";
    }

    private static string? ResolveUserEmail(ClaimsPrincipal user, ITenantContext tenant)
    {
        var identityName = user.Identity?.Name;
        return FirstNonEmpty(
            user.FindFirstValue(ClaimTypes.Email),
            user.FindFirstValue("email"),
            user.FindFirstValue("preferred_username"),
            user.FindFirstValue("upn"),
            user.FindFirstValue("unique_name"),
            LooksLikeEmail(identityName) ? identityName : null,
            LooksLikeEmail(tenant.UserId) ? tenant.UserId : null);
    }

    private static string? ResolveUserName(ClaimsPrincipal user) =>
        FirstNonEmpty(
            user.FindFirstValue("name"),
            user.FindFirstValue(ClaimTypes.Name),
            LooksLikeEmail(user.Identity?.Name) ? null : user.Identity?.Name);

    private static bool LooksLikeEmail(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) && value.Contains('@', StringComparison.Ordinal);
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

}
