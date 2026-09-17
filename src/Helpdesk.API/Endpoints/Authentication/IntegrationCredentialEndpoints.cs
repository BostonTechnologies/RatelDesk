using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Helpdesk.API.Authentication;
using Helpdesk.Infrastructure.Identity;
using Helpdesk.Shared.Auth;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.API.Endpoints.Authentication;

public static class IntegrationCredentialEndpoints
{
    public const string CredentialManagementPolicy = "IntegrationCredentialManagementSession";
    private const int DefaultLifetimeDays = 30;
    private const int MaximumLifetimeDays = 90;

    public static void MapIntegrationCredentialEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/integration-credentials")
            .WithTags("Integration Credentials")
            .RequireAuthorization(CredentialManagementPolicy);

        group.MapGet("/", async (ClaimsPrincipal principal, IIntegrationCredentialOwnerResolver ownerResolver, RatelDeskIdentityDbContext identityDb, CancellationToken ct) =>
        {
            var owner = await ownerResolver.ResolveAsync(principal, ct);
            if (owner is null) return Results.Forbid();
            var credentials = await identityDb.IntegrationCredentials.AsNoTracking()
                .Where(credential => credential.OwnerUserId == owner.UserId)
                .OrderByDescending(credential => credential.CreatedAtUnixMilliseconds)
                .ThenByDescending(credential => credential.Id)
                .Select(credential => new IntegrationCredentialMetadata(
                    credential.Id, credential.Name, credential.Prefix, credential.Purpose,
                    credential.OrganizationId, credential.Permissions.Split(' ', StringSplitOptions.RemoveEmptyEntries),
                    credential.ExpiresAtUtc, credential.CreatedAtUtc, credential.LastUsedAtUtc, credential.RevokedAtUtc)
                {
                    McpResourceUri = credential.McpResourceUri
                })
                .ToListAsync(ct);
            return Results.Ok(credentials);
        }).WithSummary("List integration credentials");

        group.MapPost("/", async (
            [FromBody] CreateIntegrationCredentialRequest? request,
            ClaimsPrincipal principal,
            HttpContext context,
            IIntegrationCredentialOwnerResolver ownerResolver,
            ICurrentUserAccessService accessService,
            RatelDeskIdentityDbContext identityDb,
            CancellationToken ct) =>
        {
            var owner = await ownerResolver.ResolveAsync(principal, ct);
            if (owner is null) return Results.Forbid();
            if (request is null) return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = ["A credential request is required."] });
            if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 128) return Results.ValidationProblem(new Dictionary<string, string[]> { ["name"] = ["A credential name up to 128 characters is required."] });
            if (request.Purpose is not ("api" or "mcp")) return Results.ValidationProblem(new Dictionary<string, string[]> { ["purpose"] = ["Purpose must be api or mcp."] });
            var mcpResourceUri = request.Purpose == "mcp"
                ? CanonicalMcpResourceUri(request.McpResourceUri)
                : null;
            if (request.Purpose == "mcp" && mcpResourceUri is null)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["mcpResourceUri"] = ["MCP credentials require an absolute HTTPS resource URI ending in /mcp."] });
            if (request.Purpose == "api" && !string.IsNullOrWhiteSpace(request.McpResourceUri))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["mcpResourceUri"] = ["MCP resource URIs can only be configured for MCP credentials."] });

            var access = await accessService.ResolveAsync(principal, ct);
            var requestedPermissions = request.Permissions?
                .Where(permission => !string.IsNullOrWhiteSpace(permission))
                .Select(permission => permission.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? [];
            if (requestedPermissions.Length == 0 || requestedPermissions.Except(access.Permissions, StringComparer.OrdinalIgnoreCase).Any() ||
                requestedPermissions.Any(permission => !HelpdeskPermissions.AssignablePermissions.Contains(permission, StringComparer.OrdinalIgnoreCase)))
            {
                return Results.Forbid();
            }
            if (string.IsNullOrWhiteSpace(request.OrganizationId) || !access.AllowedOrganizationIds.Contains(request.OrganizationId)) return Results.Forbid();

            var lifetimeDays = Math.Clamp(request.LifetimeDays ?? DefaultLifetimeDays, 1, MaximumLifetimeDays);
            var id = Guid.NewGuid();
            var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
            var prefix = $"rdk_{id:N}"[..16];
            var createdAtUtc = DateTimeOffset.UtcNow;
            var credential = new IntegrationCredential
            {
                Id = id,
                OwnerUserId = owner.UserId,
                Name = request.Name.Trim(),
                Prefix = prefix,
                SecretHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret))),
                Purpose = request.Purpose,
                McpResourceUri = mcpResourceUri,
                OrganizationId = request.OrganizationId,
                Permissions = string.Join(' ', requestedPermissions.Order(StringComparer.OrdinalIgnoreCase)),
                CreatedAtUtc = createdAtUtc,
                CreatedAtUnixMilliseconds = createdAtUtc.ToUnixTimeMilliseconds(),
                ExpiresAtUtc = createdAtUtc.AddDays(lifetimeDays)
            };
            identityDb.IntegrationCredentials.Add(credential);
            await identityDb.SaveChangesAsync(ct);
            context.Response.Headers.CacheControl = "no-store";
            return Results.Created($"/api/v1/integration-credentials/{credential.Id:N}", new CreatedIntegrationCredential(
                credential.Id, credential.Prefix, $"rdk_{credential.Id:N}_{secret}", credential.Purpose,
                credential.OrganizationId, requestedPermissions, credential.ExpiresAtUtc)
            {
                McpResourceUri = credential.McpResourceUri
            });
        }).WithSummary("Create an API integration credential");

        group.MapDelete("/{credentialId:guid}", async (Guid credentialId, ClaimsPrincipal principal, IIntegrationCredentialOwnerResolver ownerResolver, RatelDeskIdentityDbContext identityDb, CancellationToken ct) =>
        {
            var owner = await ownerResolver.ResolveAsync(principal, ct);
            if (owner is null) return Results.Forbid();
            var credential = await identityDb.IntegrationCredentials.SingleOrDefaultAsync(candidate => candidate.Id == credentialId && candidate.OwnerUserId == owner.UserId, ct);
            if (credential is null) return Results.NotFound();
            if (credential.RevokedAtUtc is null)
            {
                credential.RevokedAtUtc = DateTimeOffset.UtcNow;
                await identityDb.SaveChangesAsync(ct);
            }
            return Results.NoContent();
        }).WithSummary("Revoke an integration credential");
    }

    private static string? CanonicalMcpResourceUri(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
            !string.Equals(uri.AbsolutePath.TrimEnd('/'), "/mcp", StringComparison.Ordinal))
        {
            return null;
        }

        return uri.AbsoluteUri.TrimEnd('/');
    }

    public sealed record CreateIntegrationCredentialRequest(string Name, string Purpose, string OrganizationId, IReadOnlyList<string> Permissions, int? LifetimeDays)
    {
        public string? McpResourceUri { get; init; }
    }

    public sealed record IntegrationCredentialMetadata(Guid Id, string Name, string Prefix, string Purpose, string? OrganizationId, IReadOnlyList<string> Permissions, DateTimeOffset ExpiresAtUtc, DateTimeOffset CreatedAtUtc, DateTimeOffset? LastUsedAtUtc, DateTimeOffset? RevokedAtUtc)
    {
        public string? McpResourceUri { get; init; }
    }

    public sealed record CreatedIntegrationCredential(Guid Id, string Prefix, string Secret, string Purpose, string? OrganizationId, IReadOnlyList<string> Permissions, DateTimeOffset ExpiresAtUtc)
    {
        public string? McpResourceUri { get; init; }
    }
}
