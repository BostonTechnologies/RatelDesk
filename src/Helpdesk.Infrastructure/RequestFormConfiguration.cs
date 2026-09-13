using System.Text.Json;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Helpdesk.Infrastructure.Persistence;

public class RequestFormConfiguration : IEntityTypeConfiguration<RequestForm>
{
    private readonly ITenantContext _tenantContext;
    private readonly bool _isPostgreSql;

    public RequestFormConfiguration(ITenantContext tenantContext, bool isPostgreSql)
    {
        _tenantContext = tenantContext;
        _isPostgreSql = isPostgreSql;
    }

    public void Configure(EntityTypeBuilder<RequestForm> builder)
    {
        var jsonSchema = builder.Property(f => f.JsonSchema)
            .HasConversion(
                v => v.RootElement.GetRawText(),
                v => JsonDocument.Parse(v, new JsonDocumentOptions()));

        jsonSchema.HasColumnType(_isPostgreSql ? "jsonb" : "TEXT");

        builder.HasQueryFilter(f =>
            _tenantContext.IsHelpdeskAdmin || f.OrganizationId == _tenantContext.TenantId);
    }
}
