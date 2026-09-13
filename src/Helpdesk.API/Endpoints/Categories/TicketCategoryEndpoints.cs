using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.Auth;
using Helpdesk.Shared.DTOs.Category;
using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.API.Endpoints.Categories;

public static class TicketCategoryEndpoints
{
    public static void MapTicketCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/categories")
            .WithTags("Ticket Categories")
            .RequireAuthorization("TicketReadAccess");

        group.MapGet("/", GetCategories);

        group.MapPost("/", CreateCategory)
            .RequireAuthorization("HelpdeskAdmin");

        group.MapPut("/{id:guid}", UpdateCategory)
            .RequireAuthorization("HelpdeskAdmin");

        group.MapDelete("/{id:guid}", DeleteCategory)
            .RequireAuthorization("HelpdeskAdmin");
    }

    private static async Task<IResult> GetCategories(
        [FromServices] HelpdeskDbContext db,
        [FromServices] ITenantContext tenant,
        [FromQuery] TicketCategoryType? type,
        [FromQuery] string? search)
    {
        var tenantId = ParseTenantId(tenant.TenantId);

        var query = db.TicketCategories
            .AsNoTracking()
            .Where(x => x.IsActive &&
                        (x.TenantId == null || (tenantId.HasValue && x.TenantId == tenantId.Value)));

        if (type.HasValue)
        {
            var requestedType = type.Value;
            query = query.Where(x => x.Type == requestedType || x.Type == TicketCategoryType.Service);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();

            query = query
                .Where(x => x.Name.ToLower().Contains(term))
                .OrderBy(x => x.Name)
                .Take(50);
        }

        var items = await query
            .OrderBy(x => x.Type)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new TicketCategoryDto
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                Type = x.Type,
                TenantId = x.TenantId,
                ParentCategoryId = x.ParentCategoryId,
                SortOrder = x.SortOrder,
                IsActive = x.IsActive,
                IsSystem = x.IsSystem,
                CreatedUtc = x.CreatedUtc,
                UpdatedUtc = x.UpdatedUtc
            })
            .ToListAsync();

        return Results.Ok(items);
    }

    private static async Task<IResult> CreateCategory(
        [FromBody] CreateTicketCategoryDto dto,
        [FromServices] HelpdeskDbContext db,
        [FromServices] ITenantContext tenant)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return Results.BadRequest("Name is required.");
        }

        Guid? targetTenantId;
        var isSystem = dto.TenantId is null;

        if (isSystem)
        {
            if (!tenant.IsHelpdeskAdmin)
            {
                return Results.Forbid();
            }

            targetTenantId = null;
        }
        else
        {
            var exists = await db.Organizations.AnyAsync(x => x.Id == dto.TenantId!.Value.ToString());
            if (!exists)
            {
                return Results.BadRequest("Tenant does not exist.");
            }

            targetTenantId = dto.TenantId;
        }

        var entity = new TicketCategory
        {
            Id = Guid.NewGuid(),
            Name = dto.Name.Trim(),
            Description = dto.Description,
            Type = dto.Type,
            TenantId = targetTenantId,
            ParentCategoryId = dto.ParentCategoryId,
            SortOrder = dto.SortOrder,
            IsActive = dto.IsActive,
            IsSystem = isSystem,
            CreatedUtc = DateTime.UtcNow
        };

        db.TicketCategories.Add(entity);
        await db.SaveChangesAsync();

        return Results.Created($"/api/v1/categories/{entity.Id}", new TicketCategoryDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            Type = entity.Type,
            TenantId = entity.TenantId,
            ParentCategoryId = entity.ParentCategoryId,
            SortOrder = entity.SortOrder,
            IsActive = entity.IsActive,
            IsSystem = entity.IsSystem,
            CreatedUtc = entity.CreatedUtc,
            UpdatedUtc = entity.UpdatedUtc
        });
    }

    private static async Task<IResult> UpdateCategory(
        [FromRoute] Guid id,
        [FromBody] UpdateTicketCategoryDto dto,
        [FromServices] HelpdeskDbContext db,
        [FromServices] ITenantContext tenant)
    {
        var tenantId = ParseTenantId(tenant.TenantId);
        var entity = await db.TicketCategories.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null)
        {
            return Results.NotFound();
        }

        if (entity.IsSystem)
        {
            return Results.BadRequest("System categories are read-only.");
        }

        if (tenant.IsHelpdeskAdmin)
        {
            entity.Name = dto.Name.Trim();
            entity.Description = dto.Description;
            entity.Type = dto.Type;
            entity.ParentCategoryId = dto.ParentCategoryId;
            entity.SortOrder = dto.SortOrder;
            entity.IsActive = dto.IsActive;
            entity.UpdatedUtc = DateTime.UtcNow;

            await db.SaveChangesAsync();
            return Results.Ok();
        }

        if (!tenantId.HasValue)
        {
            return Results.BadRequest("Tenant context required to manage categories.");
        }

        if (!tenantId.HasValue || entity.TenantId != tenantId.Value)
        {
            return Results.Forbid();
        }

        entity.Name = dto.Name.Trim();
        entity.Description = dto.Description;
        entity.Type = dto.Type;
        entity.ParentCategoryId = dto.ParentCategoryId;
        entity.SortOrder = dto.SortOrder;
        entity.IsActive = dto.IsActive;
        entity.UpdatedUtc = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Results.Ok();
    }

    private static async Task<IResult> DeleteCategory(
        [FromRoute] Guid id,
        [FromServices] HelpdeskDbContext db,
        [FromServices] ITenantContext tenant)
    {
        var tenantId = ParseTenantId(tenant.TenantId);
        var entity = await db.TicketCategories.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null)
        {
            return Results.NotFound();
        }

        if (entity.IsSystem)
        {
            return Results.BadRequest("System categories are read-only.");
        }

        if (tenant.IsHelpdeskAdmin)
        {
            db.TicketCategories.Remove(entity);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }

        if (!tenantId.HasValue)
        {
            return Results.BadRequest("Tenant context required to manage categories.");
        }

        if (!tenantId.HasValue || entity.TenantId != tenantId.Value)
        {
            return Results.Forbid();
        }

        db.TicketCategories.Remove(entity);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static Guid? ParseTenantId(string? tenantId)
    {
        return Guid.TryParse(tenantId, out var parsed) ? parsed : null;
    }
}
