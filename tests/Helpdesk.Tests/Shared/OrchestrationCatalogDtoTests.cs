using System.Text.Json;
using Helpdesk.Shared.DTOs.Orchestration;

namespace Helpdesk.Tests.Shared;

public sealed class OrchestrationCatalogDtoTests
{
    [Fact]
    public void RequestDefinitionDto_DeserializesTenantMetadata_WhenProvided()
    {
        var dto = JsonSerializer.Deserialize<OrchestrationCatalogRequestDefinitionDto>(
            """
            {
              "requestDefinitionId": "req-1",
              "requestDefinitionName": "restart-service",
              "tenantId": 42,
              "tenantName": "Example Organization"
            }
            """,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(dto);
        Assert.Equal(42, dto!.TenantId);
        Assert.Equal("Example Organization", dto.TenantName);
    }

    [Fact]
    public void RequestDefinitionDto_AllowsOlderResponses_WithoutTenantMetadata()
    {
        var dto = JsonSerializer.Deserialize<OrchestrationCatalogRequestDefinitionDto>(
            """
            {
              "requestDefinitionId": "req-1",
              "requestDefinitionName": "restart-service"
            }
            """,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(dto);
        Assert.Null(dto!.TenantId);
        Assert.Null(dto.TenantName);
    }

    [Fact]
    public void RequestDefinitionDto_DeserializesTenantMetadata_Aliases()
    {
        var dto = JsonSerializer.Deserialize<OrchestrationCatalogRequestDefinitionDto>(
            """
            {
              "requestDefinitionId": "req-1",
              "requestDefinitionName": "restart-service",
              "tenant_id": 10,
              "tenant_name": "Example Organization"
            }
            """,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(dto);
        Assert.Equal(10, dto!.TenantId);
        Assert.Equal("Example Organization", dto.TenantName);
    }
}
