using Helpdesk.Application.Services.Branding;
using Microsoft.AspNetCore.Mvc;

namespace Helpdesk.API.Endpoints.Branding;

public static class InstanceBrandingEndpoints
{
    public static void MapInstanceBrandingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/branding", async ([FromServices] IInstanceBrandingProvider branding, CancellationToken ct) =>
            Results.Ok(await branding.GetEffectiveAsync(ct)))
            .WithTags("Branding")
            .WithName("GetEffectiveBranding")
            .WithSummary("Gets safe, effective display branding")
            .AllowAnonymous();

        var admin = app.MapGroup("/api/v1/admin/branding")
            .WithTags("Branding")
            .RequireAuthorization("HelpdeskAdmin");

        admin.MapGet("/", async ([FromServices] IInstanceBrandingProvider branding, CancellationToken ct) =>
            Results.Ok(await branding.GetAdministrationAsync(ct)))
            .WithName("GetInstanceBrandingAdministration");

        admin.MapPut("/", async (
            [FromBody] InstanceBrandingUpdate update,
            [FromServices] IInstanceBrandingProvider branding,
            CancellationToken ct) =>
        {
            var invalid = FindInvalidUrls(update).ToArray();
            return invalid.Length > 0
                ? Results.ValidationProblem(invalid.ToDictionary(x => x, x => new[] { "Must be an absolute HTTP or HTTPS URL." }))
                : Results.Ok(await branding.SaveAsync(update, ct));
        }).WithName("SaveInstanceBrandingAdministration");
    }

    private static IEnumerable<string> FindInvalidUrls(InstanceBrandingUpdate update)
    {
        foreach (var (name, value) in new[]
        {
            (nameof(update.ApplicationUrl), update.ApplicationUrl), (nameof(update.OrganizationUrl), update.OrganizationUrl),
            (nameof(update.SupportUrl), update.SupportUrl), (nameof(update.LogoUrl), update.LogoUrl),
            (nameof(update.CompactLogoUrl), update.CompactLogoUrl), (nameof(update.FaviconUrl), update.FaviconUrl)
        })
        {
            if (!string.IsNullOrWhiteSpace(value) &&
                (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
                 (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                  !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))))
                yield return name;
        }
    }
}
