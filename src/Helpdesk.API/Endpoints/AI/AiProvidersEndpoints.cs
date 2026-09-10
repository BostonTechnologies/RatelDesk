using FluentValidation;
using Helpdesk.Application.Services.AI;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.DTOs;
using Helpdesk.Shared.DTOs.AI;
using Microsoft.AspNetCore.Mvc;

namespace Helpdesk.API.Endpoints.AI;

public static class AiProvidersEndpoints
{
    public static void MapAiProviderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/ai/providers")
            .WithTags("AI Providers")
            .RequireAuthorization("HelpdeskAdmin");

        group.MapGet("/", async ([FromServices] IAiProviderService service, int page, int pageSize, CancellationToken token) =>
        {
            var all = (await service.ListAsync(token)).Select(ToDto).ToList();
            var paged = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return Results.Ok(new PagedResponse<AiProviderSummaryDto>
            {
                Items = paged,
                Page = page,
                PageSize = pageSize,
                TotalCount = all.Count
            });
        })
        .WithName("GetAiProviders")
        .WithSummary("List AI providers")
        .WithDescription("Gets a paginated list of AI providers.");

        group.MapGet("/{id}", async (Guid id, IAiProviderService service, CancellationToken token) =>
        {
            var provider = await service.GetAsync(id, token);
            return provider is not null
                ? Results.Ok(ToDto(provider))
                : Results.Problem("Provider not found", statusCode: 404);
        })
        .WithName("GetAiProvider")
        .WithSummary("Get AI provider")
        .WithDescription("Gets details for an AI provider.");

        group.MapPost("/", async (AiProviderCreateDto dto, IAiProviderService service, IValidator<AiProviderCreateDto> validator, CancellationToken token) =>
        {
            var validation = await validator.ValidateAsync(dto, token);
            if (!validation.IsValid)
                return Results.ValidationProblem(validation.ToDictionary());
            var entity = new AiProvider
            {
                Name = dto.Name,
                ProviderType = dto.ProviderType,
                BaseUrl = dto.BaseUrl,
                IsEnabled = dto.IsEnabled,
                DefaultModel = dto.DefaultModel,
                ExtraHeadersJson = dto.ExtraHeadersJson
            };
            var created = await service.CreateAsync(entity, dto.ApiKey, token);
            return Results.Created($"/api/v1/ai/providers/{created.Id}", ToDto(created));
        })
        .WithName("CreateAiProvider")
        .WithSummary("Create AI provider")
        .WithDescription("Creates a new AI provider.");

        group.MapPut("/{id}", async (Guid id, AiProviderUpdateDto dto, IAiProviderService service, IValidator<AiProviderUpdateDto> validator, CancellationToken token) =>
        {
            var validation = await validator.ValidateAsync(dto, token);
            if (!validation.IsValid)
                return Results.ValidationProblem(validation.ToDictionary());
            var entity = new AiProvider
            {
                Id = id,
                Name = dto.Name,
                ProviderType = dto.ProviderType,
                BaseUrl = dto.BaseUrl,
                IsEnabled = dto.IsEnabled,
                DefaultModel = dto.DefaultModel,
                ExtraHeadersJson = dto.ExtraHeadersJson
            };
            var updated = await service.UpdateAsync(entity, dto.ApiKey, token);
            return updated is not null
                ? Results.Ok(ToDto(updated))
                : Results.Problem("Provider not found", statusCode: 404);
        })
        .WithName("UpdateAiProvider")
        .WithSummary("Update AI provider")
        .WithDescription("Updates an existing AI provider.");

        group.MapDelete("/{id}", async (Guid id, IAiProviderService service, CancellationToken token) =>
            await service.DeleteAsync(id, token)
                ? Results.NoContent()
                : Results.Problem("Provider not found", statusCode: 404))
        .WithName("DeleteAiProvider")
        .WithSummary("Delete AI provider")
        .WithDescription("Deletes an AI provider.");

        group.MapPost("/{id}/test", async (Guid id, IAiProviderService service, CancellationToken token) =>
        {
            var provider = await service.GetAsync(id, token);
            if (provider is null) return Results.Problem("Provider not found", statusCode: 404);
            var result = await service.TestAsync(id, token);
            return Results.Ok(new AiProviderTestResultDto
            {
                Success = result.Success,
                AttemptedUrl = result.AttemptedUrl,
                StatusCode = result.StatusCode,
                Message = result.Message,
                ErrorBody = result.ErrorBody
            });
        })
        .WithName("TestAiProvider")
        .WithSummary("Test AI provider connectivity")
        .WithDescription("Tests connectivity to the AI provider.");

        group.MapGet("/{id}/models", async (Guid id, IAiProviderService service, CancellationToken token) =>
        {
            var provider = await service.GetAsync(id, token);
            if (provider is null) return Results.Problem("Provider not found", statusCode: 404);

            // Refresh models from upstream + persist new ones
            await service.ListModelsAsync(id, token);

            // Re-load to get up-to-date Models (IDs, IsEnabled, etc.)
            provider = await service.GetAsync(id, token);
            if (provider is null) return Results.Problem("Provider not found after refresh", statusCode: 404);

            var dtos = provider.Models
                .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .Select(m => new AiModelDto
                {
                    Id = m.Id.ToString(),
                    Name = m.Name,
                    IsEnabled = m.IsEnabled,
                    ExtraHeadersJson = m.ExtraHeadersJson
                })
                .ToList();

            return Results.Ok(dtos);
        })
        .WithName("ListAiProviderModels")
        .WithSummary("List AI provider models")
        .WithDescription("Lists available models for the AI provider.");

        group.MapPost("/{id}/chat-test", async (Guid id, AiChatRequestDto dto, IAiProviderService service, IValidator<AiChatRequestDto> validator, CancellationToken token) =>
        {
            var validation = await validator.ValidateAsync(dto, token);
            if (!validation.IsValid)
                return Results.ValidationProblem(validation.ToDictionary());
            var provider = await service.GetAsync(id, token);
            if (provider is null) return Results.Problem("Provider not found", statusCode: 404);
            try
            {
                var response = await service.ChatTestAsync(id, dto.Prompt, dto.Model, token);
                return Results.Ok(new { Response = response });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        })
        .WithName("ChatTestAiProvider")
        .WithSummary("Chat test AI provider")
        .WithDescription("Sends a prompt to the AI provider to test chat functionality.");
    }

    private static AiProviderSummaryDto ToDto(AiProvider provider) => new()
    {
        Id = provider.Id.ToString(),
        Name = provider.Name,
        ProviderType = provider.ProviderType,
        BaseUrl = provider.BaseUrl,
        IsEnabled = provider.IsEnabled,
        DefaultModel = provider.DefaultModel,
        Models = provider.Models.Select(m => new AiModelDto
        {
            Id = m.Id.ToString(),
            Name = m.Name,
            IsEnabled = m.IsEnabled,
            ExtraHeadersJson = m.ExtraHeadersJson
        }).ToList()
    };
}
