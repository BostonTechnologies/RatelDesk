using Helpdesk.Application.Services.SupportNotifications;
using Helpdesk.Shared.DTOs.SupportNotifications;
using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Mvc;

namespace Helpdesk.API.Endpoints.SupportNotifications;

public static class OrganizationSupportCoverageEndpoints
{
    public static void MapOrganizationSupportCoverageEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/support/coverage")
            .WithTags("Support Coverage")
            .RequireAuthorization("HelpdeskAdmin");

        group.MapGet("/", async (
            [FromServices] IRepository<OrganizationSupportCoverage> repo,
            string? customerOrganizationId = null) =>
        {
            var items = (await repo.GetAllAsync())
                .Where(x => string.IsNullOrWhiteSpace(customerOrganizationId) || string.Equals(x.CustomerOrganizationId, customerOrganizationId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.CustomerOrganizationId)
                .ThenBy(x => x.ProviderOrganizationId)
                .Select(ToDto)
                .ToList();
            return Results.Ok(items);
        });

        group.MapPost("/", async ([FromBody] CreateOrganizationSupportCoverageDto dto, [FromServices] IRepository<OrganizationSupportCoverage> repo) =>
        {
            var validation = Validate(dto.CustomerOrganizationId, dto.ProviderOrganizationId, dto.SupportGroupId);
            if (validation is not null) return validation;

            var created = await repo.CreateAsync(new OrganizationSupportCoverage
            {
                CustomerOrganizationId = dto.CustomerOrganizationId.Trim(),
                ProviderOrganizationId = dto.ProviderOrganizationId.Trim(),
                SupportGroupId = dto.SupportGroupId.Trim(),
                Role = dto.Role,
                IsEnabled = dto.IsEnabled
            });
            return Results.Created($"/api/v1/support/coverage/{created.Id}", ToDto(created));
        });

        group.MapPut("/{id}", async (string id, [FromBody] UpdateOrganizationSupportCoverageDto dto, [FromServices] IRepository<OrganizationSupportCoverage> repo) =>
        {
            var existing = await repo.GetAsync(id);
            if (existing is null) return Results.NotFound();
            var validation = Validate(dto.CustomerOrganizationId, dto.ProviderOrganizationId, dto.SupportGroupId);
            if (validation is not null) return validation;

            existing.CustomerOrganizationId = dto.CustomerOrganizationId.Trim();
            existing.ProviderOrganizationId = dto.ProviderOrganizationId.Trim();
            existing.SupportGroupId = dto.SupportGroupId.Trim();
            existing.Role = dto.Role;
            existing.IsEnabled = dto.IsEnabled;
            existing.UpdatedUtc = DateTimeOffset.UtcNow;
            var updated = await repo.UpdateAsync(existing);
            return Results.Ok(ToDto(updated!));
        });

        group.MapDelete("/{id}", async (string id, [FromServices] IRepository<OrganizationSupportCoverage> repo) =>
            await repo.DeleteAsync(id) ? Results.NoContent() : Results.NotFound());

        app.MapPost("/api/v1/support/bootstrap/defaults", async (
            [FromServices] ISupportNotificationBootstrapper bootstrapper,
            CancellationToken ct) =>
        {
            var result = await bootstrapper.EnsureDefaultSupportConfigurationAsync(ct);
            return Results.Ok(result);
        })
        .WithTags("Support Coverage")
        .RequireAuthorization("HelpdeskAdmin");

        app.MapGet("/api/v1/support/organizations/{organizationId}/recipient-preview", async (
            string organizationId,
            [FromQuery] SupportNotificationEventType eventType,
            [FromServices] ISupportNotificationRecipientResolver resolver,
            CancellationToken ct) =>
        {
            var recipients = await resolver.ResolveOrganizationEventRecipientsAsync(
                organizationId,
                eventType,
                SupportNotificationChannel.Email,
                ct);
            return Results.Ok(new SupportNotificationRecipientPreviewDto
            {
                CustomerOrganizationId = organizationId,
                EventType = eventType,
                Channel = SupportNotificationChannel.Email,
                Recipients = recipients.Select(x => new SupportNotificationRecipientDto
                {
                    UserId = x.UserId,
                    UserName = x.UserName,
                    Email = x.Email,
                    SupportGroupId = x.SupportGroupId,
                    SupportGroupName = x.SupportGroupName
                }).ToList()
            });
        })
        .WithTags("Support Coverage")
        .RequireAuthorization("HelpdeskAdmin");
    }

    private static IResult? Validate(string customerOrganizationId, string providerOrganizationId, string supportGroupId)
    {
        if (string.IsNullOrWhiteSpace(customerOrganizationId) ||
            string.IsNullOrWhiteSpace(providerOrganizationId) ||
            string.IsNullOrWhiteSpace(supportGroupId))
        {
            return Results.BadRequest("Customer organization, provider organization, and support group are required.");
        }

        return null;
    }

    private static OrganizationSupportCoverageDto ToDto(OrganizationSupportCoverage source) => new()
    {
        Id = source.Id,
        CustomerOrganizationId = source.CustomerOrganizationId,
        ProviderOrganizationId = source.ProviderOrganizationId,
        SupportGroupId = source.SupportGroupId,
        Role = source.Role,
        IsEnabled = source.IsEnabled,
        CreatedUtc = source.CreatedUtc,
        UpdatedUtc = source.UpdatedUtc
    };
}
