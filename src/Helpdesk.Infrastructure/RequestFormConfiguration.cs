using System.Text.Json;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Helpdesk.Infrastructure.Persistence;

public class RequestFormConfiguration : IEntityTypeConfiguration<RequestForm>
{
    private readonly ITenantContext _tenantContext;

    public RequestFormConfiguration(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public void Configure(EntityTypeBuilder<RequestForm> builder)
    {
        builder.Property(f => f.JsonSchema)
            .HasColumnType("jsonb")
            .HasConversion(
                v => v.RootElement.GetRawText(),
                v => JsonDocument.Parse(v, new JsonDocumentOptions()));

        builder.HasQueryFilter(f =>
            _tenantContext.IsHelpdeskAdmin || f.OrganizationId == _tenantContext.TenantId);
    }
}
