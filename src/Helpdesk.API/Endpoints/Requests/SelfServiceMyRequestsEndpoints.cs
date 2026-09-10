using Helpdesk.Application.Events;
using Helpdesk.API.Services;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.DTOs;
using Helpdesk.Shared.DTOs.Request;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Helpdesk.API.Endpoints.Requests;

public static class SelfServiceMyRequestsEndpoints
{
    public static void MapSelfServiceMyRequestsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/self-service/requests")
            .WithTags("Self Service")
            .RequireAuthorization("SelfService.User");

        group.MapGet("/", GetMyRequests)
            .WithName("GetMySelfServiceRequests")
            .WithSummary("List requests submitted by current user");

        group.MapGet("/{id}", GetMyRequestDetail)
            .WithName("GetMySelfServiceRequestDetail")
            .WithSummary("Get one self-service request submitted by current user");

        group.MapGet("/{id}/tasks", GetMyRequestTasks)
            .WithName("GetMySelfServiceRequestTasks")
            .WithSummary("Get tasks for one self-service request submitted by current user");
    }

    private static async Task<IResult> GetMyRequests(
        ClaimsPrincipal user,
        [FromServices] HelpdeskDbContext db,
        [FromServices] ISelfServiceAudienceService selfServiceAudienceService,
        [FromServices] ITenantContext tenant,
        [FromServices] IDomainEventPublisher domainEvents,
        [FromServices] ICorrelationContext correlationContext,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? q,
        CancellationToken token)
    {
        var resolvedPage = Math.Max(page ?? 1, 1);
        var resolvedPageSize = Math.Clamp(pageSize ?? 10, 1, 100);
        var isAdmin = user.IsInRole("HelpdeskAdmin");
        var isTestUser = await selfServiceAudienceService.IsTestUserAsync(token);
        var currentCustomer = isAdmin ? null : await ResolveCurrentCustomerAsync(db, user, tenant, token);
        var currentCustomerId = currentCustomer?.Id;
        if (!isAdmin && string.IsNullOrWhiteSpace(currentCustomerId))
        {
            return Results.Ok(new PagedResponse<MyRequestListItemDto>
            {
                Page = resolvedPage,
                PageSize = resolvedPageSize,
                TotalCount = 0,
                Items = []
            });
        }

        var effectiveOrganizationId = await ResolveEffectiveOrganizationIdAsync(db, tenant, currentCustomer, token);

        var accessibleForms = BuildAccessibleRequestFormsQuery(
            db,
            selfServiceAudienceService,
            isAdmin,
            isTestUser,
            effectiveOrganizationId);
        var accessibleFormIds = accessibleForms.Select(f => f.Id);

        var query = db.Requests
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => (isAdmin || r.CustomerId == currentCustomerId)
                        && (effectiveOrganizationId == null || r.OrganizationId == effectiveOrganizationId)
                        && (isAdmin
                            || string.IsNullOrWhiteSpace(r.RequestFormId)
                            || accessibleFormIds.Contains(r.RequestFormId!)))
            .Select(r => new
            {
                Request = r,
                ReleaseStatus = string.IsNullOrWhiteSpace(r.RequestFormId)
                    ? (RequestFormReleaseStatus?)null
                    : accessibleForms
                        .Where(f => f.Id == r.RequestFormId)
                        .Select(f => (RequestFormReleaseStatus?)f.ReleaseStatus)
                        .FirstOrDefault()
            });

        if (!string.IsNullOrWhiteSpace(q))
        {
            var like = $"%{q.Trim()}%";
            query = query.Where(r =>
                EF.Functions.Like(r.Request.TrackingId, like) ||
                EF.Functions.Like(r.Request.Title, like));
        }

        var totalCount = await query.CountAsync(token);
        var items = await query
            .OrderByDescending(r => r.Request.CreatedAt)
            .Skip((resolvedPage - 1) * resolvedPageSize)
            .Take(resolvedPageSize)
            .Select(r => new MyRequestListItemDto
            {
                Id = r.Request.Id,
                TrackingId = r.Request.TrackingId,
                Title = r.Request.Title,
                State = r.Request.State,
                ReleaseStatus = r.ReleaseStatus,
                CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(r.Request.CreatedAt, DateTimeKind.Utc)),
                UpdatedAt = r.Request.UpdatedAt.HasValue
                    ? new DateTimeOffset(DateTime.SpecifyKind(r.Request.UpdatedAt.Value, DateTimeKind.Utc))
                    : null
            })
            .ToListAsync(token);

        await domainEvents.PublishAsync(
            new MyRequestsViewedEvent(
                currentCustomerId ?? tenant.UserId,
                resolvedPage,
                resolvedPageSize,
                string.IsNullOrWhiteSpace(q) ? null : q.Trim(),
                items.Count,
                effectiveOrganizationId,
                DateTimeOffset.UtcNow,
                GetCorrelationId(correlationContext)),
            token);

        return Results.Ok(new PagedResponse<MyRequestListItemDto>
        {
            Page = resolvedPage,
            PageSize = resolvedPageSize,
            TotalCount = totalCount,
            Items = items
        });
    }

    private static async Task<IResult> GetMyRequestDetail(
        [FromRoute] string id,
        ClaimsPrincipal user,
        [FromServices] HelpdeskDbContext db,
        [FromServices] ISelfServiceAudienceService selfServiceAudienceService,
        [FromServices] ITenantContext tenant,
        [FromServices] IDomainEventPublisher domainEvents,
        [FromServices] ICorrelationContext correlationContext,
        CancellationToken token)
    {
        var request = await GetOwnedRequestAsync(user, db, selfServiceAudienceService, tenant, id, token);
        if (request is null)
        {
            return Results.NotFound();
        }

        await domainEvents.PublishAsync(
            new MyRequestOpenedEvent(
                tenant.UserId,
                request.Id,
                request.TrackingId,
                tenant.TenantId,
                DateTimeOffset.UtcNow,
                GetCorrelationId(correlationContext)),
            token);

        return Results.Ok(new MyRequestDetailDto
        {
            Id = request.Id,
            TrackingId = request.TrackingId,
            Title = request.Title,
            Description = request.Description,
            State = request.State,
            ReleaseStatus = request.RequestFormReleaseStatus,
            CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(request.CreatedAt, DateTimeKind.Utc)),
            UpdatedAt = request.UpdatedAt.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(request.UpdatedAt.Value, DateTimeKind.Utc))
                : null,
            PayloadJson = string.IsNullOrWhiteSpace(request.PayloadJson) ? "{}" : request.PayloadJson
        });
    }

    private static async Task<IResult> GetMyRequestTasks(
        ClaimsPrincipal user,
        [FromRoute] string id,
        [FromServices] HelpdeskDbContext db,
        [FromServices] ISelfServiceAudienceService selfServiceAudienceService,
        [FromServices] ITenantContext tenant,
        CancellationToken token)
    {
        var request = await GetOwnedRequestAsync(user, db, selfServiceAudienceService, tenant, id, token);
        if (request is null)
        {
            return Results.NotFound();
        }

        var tasks = await db.RequestTasks.AsNoTracking()
            .Where(t => t.RequestId == request.Id)
            .OrderBy(t => t.Order)
            .ThenBy(t => t.CreatedAt)
            .Select(t => new MyRequestTaskDto
            {
                Id = t.Id,
                Name = t.Name,
                Type = t.Type,
                Status = t.Status,
                Order = t.Order,
                StartedAt = t.StartedAt,
                CompletedAt = t.CompletedAt,
                NextRetryAt = t.NextRetryAt,
                DueAt = t.DueAt,
                RetryCount = t.RetryCount,
                FailureReason = t.FailureReason
            })
            .ToListAsync(token);

        var taskIds = tasks.Select(x => x.Id).ToList();
        var pendingApprovals = taskIds.Count == 0
            ? new List<MyRequestPendingApprovalDto>()
            : await db.RequestTaskApprovals.AsNoTracking()
                .Where(x => taskIds.Contains(x.RequestTaskId)
                    && x.Status == RequestTaskApprovalStatus.Pending)
                .Join(
                    db.RequestTasks.AsNoTracking(),
                    approval => approval.RequestTaskId,
                    task => task.Id,
                    (approval, task) => new MyRequestPendingApprovalDto
                    {
                        Id = approval.Id,
                        RequestTaskId = approval.RequestTaskId,
                        TaskName = task.Name,
                        ApproverName = approval.ApproverName,
                        ApproverEmail = approval.ApproverEmail,
                        OrganizationName = approval.OrganizationName,
                        Status = approval.Status,
                        DueAt = task.DueAt,
                        ReviewedAtUtc = approval.ReviewedAtUtc
                    })
                .ToListAsync(token);
        var approvalsByTask = pendingApprovals
            .GroupBy(x => x.RequestTaskId)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);
        foreach (var task in tasks)
        {
            if (approvalsByTask.TryGetValue(task.Id, out var approvals))
            {
                task.PendingApprovals = approvals;
            }
        }

        return Results.Ok(tasks);
    }

    private static async Task<RequestWithReleaseStatus?> GetOwnedRequestAsync(
        ClaimsPrincipal user,
        HelpdeskDbContext db,
        ISelfServiceAudienceService selfServiceAudienceService,
        ITenantContext tenant,
        string requestId,
        CancellationToken token)
    {
        var isAdmin = user.IsInRole("HelpdeskAdmin");
        var isTestUser = await selfServiceAudienceService.IsTestUserAsync(token);
        var currentCustomer = isAdmin ? null : await ResolveCurrentCustomerAsync(db, user, tenant, token);
        var currentCustomerId = currentCustomer?.Id;
        if (!isAdmin && string.IsNullOrWhiteSpace(currentCustomerId))
        {
            return null;
        }

        var effectiveOrganizationId = await ResolveEffectiveOrganizationIdAsync(db, tenant, currentCustomer, token);
        var accessibleForms = BuildAccessibleRequestFormsQuery(
            db,
            selfServiceAudienceService,
            isAdmin,
            isTestUser,
            effectiveOrganizationId);
        var accessibleFormIds = accessibleForms.Select(f => f.Id);

        return await db.Requests
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => r.Id == requestId
                        && (isAdmin || r.CustomerId == currentCustomerId)
                        && (effectiveOrganizationId == null || r.OrganizationId == effectiveOrganizationId)
                        && (isAdmin
                            || string.IsNullOrWhiteSpace(r.RequestFormId)
                            || accessibleFormIds.Contains(r.RequestFormId!)))
            .Select(r => new RequestWithReleaseStatus(
                r.Id,
                r.TrackingId,
                r.Title,
                r.Description,
                r.State,
                r.PayloadJson,
                r.CreatedAt,
                r.UpdatedAt,
                string.IsNullOrWhiteSpace(r.RequestFormId)
                    ? (RequestFormReleaseStatus?)null
                    : accessibleForms
                        .Where(f => f.Id == r.RequestFormId)
                        .Select(f => (RequestFormReleaseStatus?)f.ReleaseStatus)
                        .FirstOrDefault()))
            .FirstOrDefaultAsync(token);
    }

    private static IQueryable<RequestForm> BuildAccessibleRequestFormsQuery(
        HelpdeskDbContext db,
        ISelfServiceAudienceService selfServiceAudienceService,
        bool isAdmin,
        bool isTestUser,
        string? effectiveOrganizationId)
    {
        if (isAdmin)
        {
            return db.RequestForms.IgnoreQueryFilters().AsNoTracking();
        }

        if (!string.IsNullOrWhiteSpace(effectiveOrganizationId))
        {
            return selfServiceAudienceService.ApplyAudienceFilter(
                db.RequestForms.IgnoreQueryFilters().AsNoTracking(),
                isAdmin,
                isTestUser,
                effectiveOrganizationId);
        }

        return db.RequestForms.IgnoreQueryFilters().AsNoTracking()
            .Where(f => f.ReleaseStatus == RequestFormReleaseStatus.Production || isTestUser);
    }

    private static async Task<string?> ResolveEffectiveOrganizationIdAsync(
        HelpdeskDbContext db,
        ITenantContext tenant,
        Customer? currentCustomer,
        CancellationToken token)
    {
        if (!string.IsNullOrWhiteSpace(currentCustomer?.OrganizationId))
        {
            return currentCustomer.OrganizationId;
        }

        if (!string.IsNullOrWhiteSpace(tenant.TenantId))
        {
            return tenant.TenantId;
        }

        if (string.IsNullOrWhiteSpace(tenant.UserId))
        {
            return null;
        }

        return await db.Users.AsNoTracking()
            .Where(x => x.Id == tenant.UserId)
            .Select(x => x.OrganizationId)
            .FirstOrDefaultAsync(token);
    }

    private static async Task<Customer?> ResolveCurrentCustomerAsync(
        HelpdeskDbContext db,
        ClaimsPrincipal user,
        ITenantContext tenant,
        CancellationToken token)
    {
        var email = ResolveUserEmail(user, tenant);
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        return await db.Customers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Email.ToLower() == normalizedEmail, token);
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

    private static string GetCorrelationId(ICorrelationContext correlation)
    {
        return correlation.GetCorrelationId() ?? $"corr-{Guid.NewGuid():N}";
    }

    private sealed record RequestWithReleaseStatus(
        string Id,
        string TrackingId,
        string Title,
        string? Description,
        TicketState State,
        string? PayloadJson,
        DateTime CreatedAt,
        DateTime? UpdatedAt,
        RequestFormReleaseStatus? RequestFormReleaseStatus);
}
