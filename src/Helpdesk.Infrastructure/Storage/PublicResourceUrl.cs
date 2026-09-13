using Helpdesk.Application.Services.Branding;

namespace Helpdesk.Infrastructure.Storage;

/// <summary>Uses an explicit public API origin, otherwise the configured instance URL served by Web.</summary>
public static class PublicResourceUrl
{
    public static async Task<string> BuildAsync(
        string relativePath,
        string? publicApiBaseUrl,
        IInstanceBrandingProvider? brandingProvider,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = publicApiBaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl) && brandingProvider is not null)
            baseUrl = (await brandingProvider.GetEffectiveAsync(cancellationToken)).ApplicationUrl;
        return string.IsNullOrWhiteSpace(baseUrl)
            ? relativePath
            : $"{baseUrl.TrimEnd('/')}{relativePath}";
    }
}
