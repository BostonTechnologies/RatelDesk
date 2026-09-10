using Helpdesk.Shared.Models;

namespace Helpdesk.Application.Services.Branding;

public interface IInstanceBrandingProvider
{
    Task<InstanceBrandingSnapshot> GetEffectiveAsync(CancellationToken cancellationToken = default);
    Task<InstanceBrandingAdministration> GetAdministrationAsync(CancellationToken cancellationToken = default);
    Task<InstanceBrandingAdministration> SaveAsync(InstanceBrandingUpdate update, CancellationToken cancellationToken = default);
}

public sealed record InstanceBrandingSnapshot(
    string ApplicationName,
    string OrganizationName,
    string ApplicationUrl,
    string OrganizationUrl,
    string SupportUrl,
    string SupportEmail,
    string LogoUrl,
    string CompactLogoUrl,
    string FaviconUrl,
    string EmailFromDisplayName,
    string Tagline);

public sealed record InstanceBrandingUpdate(
    string? ApplicationName,
    string? OrganizationName,
    string? ApplicationUrl,
    string? OrganizationUrl,
    string? SupportUrl,
    string? SupportEmail,
    string? LogoUrl,
    string? CompactLogoUrl,
    string? FaviconUrl,
    string? EmailFromDisplayName,
    string? Tagline);

public enum InstanceBrandingValueSource { Default, Database, Environment }

public sealed record InstanceBrandingFieldState(
    string Name,
    string? PersistedValue,
    string EffectiveValue,
    InstanceBrandingValueSource Source,
    bool IsAdminEditable);

public sealed record InstanceBrandingAdministration(
    InstanceBrandingSnapshot Effective,
    IReadOnlyList<InstanceBrandingFieldState> Fields);
