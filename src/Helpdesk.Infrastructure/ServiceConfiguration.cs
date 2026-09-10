using System.Text.Json;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Helpdesk.Infrastructure.Persistence;

public class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    private readonly ITenantContext _tenantContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ServiceConfiguration(ITenantContext tenantContext, IHttpContextAccessor httpContextAccessor)
    {
        _tenantContext = tenantContext;
        _httpContextAccessor = httpContextAccessor;
    }

    public void Configure(EntityTypeBuilder<Service> builder)
    {
        // ----- converters & comparers for List<string> -----
        var listToString = new ValueConverter<List<string>, string>(
            v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
            v => string.IsNullOrWhiteSpace(v)
                    ? new List<string>()
                    : (JsonSerializer.Deserialize<List<string>>(v, JsonSerializerOptions.Default) ?? new List<string>()));

        var listComparer = new ValueComparer<List<string>>(
            (a, b) =>
                ReferenceEquals(a, b) ||
                (a != null && b != null && a.SequenceEqual(b)),
            v => v == null
                ? 0
                : v.Aggregate(0, (h, s) => HashCode.Combine(h, (s == null ? 0 : s.GetHashCode()))),
            v => v == null ? new List<string>() : v.ToList());

        // AllowedCustomerIds
        var custProp = builder.Property(s => s.AllowedCustomerIds);
        custProp.HasConversion(listToString);
        custProp.Metadata.SetValueComparer(listComparer);
        custProp.HasColumnType("TEXT"); // SQLite

        // AllowedOrganizationIds
        var orgProp = builder.Property(s => s.AllowedOrganizationIds);
        orgProp.HasConversion(listToString);
        orgProp.Metadata.SetValueComparer(listComparer);
        orgProp.HasColumnType("TEXT");

        // ----- query filter: admin sees all; else user must be allowed -----
        // Avoid null-propagation (?.) inside the expression tree.
        var httpContext = _httpContextAccessor.HttpContext;

        builder.HasQueryFilter(service =>
            httpContext == null                                      // design-time / background
            || _tenantContext.IsHelpdeskAdmin                         // admins see all
            || (_tenantContext.UserId != null                         // else user must be in AllowedCustomerIds
                && service.AllowedCustomerIds.Contains(_tenantContext.UserId)));
    }
}
