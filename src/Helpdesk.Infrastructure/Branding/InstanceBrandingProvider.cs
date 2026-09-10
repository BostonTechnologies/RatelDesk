using Helpdesk.Application.Services.Branding;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.Extensions.Configuration;

namespace Helpdesk.Infrastructure.Branding;

public sealed class InstanceBrandingProvider(
    IRepository<InstanceBranding> repository,
    IConfiguration configuration) : IInstanceBrandingProvider
{
    private static readonly InstanceBrandingSnapshot Defaults = new(
        "RatelDesk", "", "", "", "", "", "/branding/rateldesk-wordmark.webp",
        "/branding/rateldesk-mark.webp", "/favicon.ico", "RatelDesk", "Service management");

    public async Task<InstanceBrandingSnapshot> GetEffectiveAsync(CancellationToken cancellationToken = default)
    {
        var persisted = await repository.GetAsync("1");
        return BuildEffective(persisted);
    }

    public async Task<InstanceBrandingAdministration> GetAdministrationAsync(CancellationToken cancellationToken = default)
    {
        var persisted = await repository.GetAsync("1");
        var effective = BuildEffective(persisted);
        return new InstanceBrandingAdministration(effective,
        [
            State(nameof(InstanceBranding.ApplicationName), persisted?.ApplicationName, effective.ApplicationName),
            State(nameof(InstanceBranding.OrganizationName), persisted?.OrganizationName, effective.OrganizationName),
            State(nameof(InstanceBranding.ApplicationUrl), persisted?.ApplicationUrl, effective.ApplicationUrl),
            State(nameof(InstanceBranding.OrganizationUrl), persisted?.OrganizationUrl, effective.OrganizationUrl),
            State(nameof(InstanceBranding.SupportUrl), persisted?.SupportUrl, effective.SupportUrl),
            State(nameof(InstanceBranding.SupportEmail), persisted?.SupportEmail, effective.SupportEmail),
            State(nameof(InstanceBranding.LogoUrl), persisted?.LogoUrl, effective.LogoUrl),
            State(nameof(InstanceBranding.CompactLogoUrl), persisted?.CompactLogoUrl, effective.CompactLogoUrl),
            State(nameof(InstanceBranding.FaviconUrl), persisted?.FaviconUrl, effective.FaviconUrl),
            State(nameof(InstanceBranding.EmailFromDisplayName), persisted?.EmailFromDisplayName, effective.EmailFromDisplayName),
            State(nameof(InstanceBranding.Tagline), persisted?.Tagline, effective.Tagline)
        ]);
    }

    public async Task<InstanceBrandingAdministration> SaveAsync(InstanceBrandingUpdate update, CancellationToken cancellationToken = default)
    {
        var current = await repository.GetAsync("1") ?? new InstanceBranding();
        ApplyWhenManaged(nameof(InstanceBranding.ApplicationName), update.ApplicationName, value => current.ApplicationName = value);
        ApplyWhenManaged(nameof(InstanceBranding.OrganizationName), update.OrganizationName, value => current.OrganizationName = value);
        ApplyWhenManaged(nameof(InstanceBranding.ApplicationUrl), update.ApplicationUrl, value => current.ApplicationUrl = value);
        ApplyWhenManaged(nameof(InstanceBranding.OrganizationUrl), update.OrganizationUrl, value => current.OrganizationUrl = value);
        ApplyWhenManaged(nameof(InstanceBranding.SupportUrl), update.SupportUrl, value => current.SupportUrl = value);
        ApplyWhenManaged(nameof(InstanceBranding.SupportEmail), update.SupportEmail, value => current.SupportEmail = value);
        ApplyWhenManaged(nameof(InstanceBranding.LogoUrl), update.LogoUrl, value => current.LogoUrl = value);
        ApplyWhenManaged(nameof(InstanceBranding.CompactLogoUrl), update.CompactLogoUrl, value => current.CompactLogoUrl = value);
        ApplyWhenManaged(nameof(InstanceBranding.FaviconUrl), update.FaviconUrl, value => current.FaviconUrl = value);
        ApplyWhenManaged(nameof(InstanceBranding.EmailFromDisplayName), update.EmailFromDisplayName, value => current.EmailFromDisplayName = value);
        ApplyWhenManaged(nameof(InstanceBranding.Tagline), update.Tagline, value => current.Tagline = value);
        current.UpdatedUtc = DateTime.UtcNow;

        if (await repository.GetAsync("1") is null)
            await repository.CreateAsync(current);
        else
            await repository.UpdateAsync(current);

        return await GetAdministrationAsync(cancellationToken);
    }

    private InstanceBrandingSnapshot BuildEffective(InstanceBranding? persisted) => new(
        Resolve(nameof(InstanceBranding.ApplicationName), persisted?.ApplicationName, Defaults.ApplicationName),
        Resolve(nameof(InstanceBranding.OrganizationName), persisted?.OrganizationName, Defaults.OrganizationName),
        Resolve(nameof(InstanceBranding.ApplicationUrl), persisted?.ApplicationUrl, Defaults.ApplicationUrl),
        Resolve(nameof(InstanceBranding.OrganizationUrl), persisted?.OrganizationUrl, Defaults.OrganizationUrl),
        Resolve(nameof(InstanceBranding.SupportUrl), persisted?.SupportUrl, Defaults.SupportUrl),
        Resolve(nameof(InstanceBranding.SupportEmail), persisted?.SupportEmail, Defaults.SupportEmail),
        Resolve(nameof(InstanceBranding.LogoUrl), persisted?.LogoUrl, Defaults.LogoUrl),
        Resolve(nameof(InstanceBranding.CompactLogoUrl), persisted?.CompactLogoUrl, Defaults.CompactLogoUrl),
        Resolve(nameof(InstanceBranding.FaviconUrl), persisted?.FaviconUrl, Defaults.FaviconUrl),
        Resolve(nameof(InstanceBranding.EmailFromDisplayName), persisted?.EmailFromDisplayName, Defaults.EmailFromDisplayName),
        Resolve(nameof(InstanceBranding.Tagline), persisted?.Tagline, Defaults.Tagline));

    private InstanceBrandingFieldState State(string name, string? persisted, string effective) => new(
        name, persisted, effective, Source(name, persisted), !HasDeploymentValue(name));

    private string Resolve(string name, string? persisted, string fallback) =>
        Normalize(configuration[$"Branding:{name}"]) ?? Normalize(persisted) ?? fallback;

    private InstanceBrandingValueSource Source(string name, string? persisted) => HasDeploymentValue(name)
        ? InstanceBrandingValueSource.Environment
        : Normalize(persisted) is not null ? InstanceBrandingValueSource.Database : InstanceBrandingValueSource.Default;

    private bool HasDeploymentValue(string name) => Normalize(configuration[$"Branding:{name}"]) is not null;

    private void ApplyWhenManaged(string name, string? value, Action<string?> assign)
    {
        if (!HasDeploymentValue(name))
            assign(Normalize(value));
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
