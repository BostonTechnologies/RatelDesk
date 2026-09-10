extern alias NewWeb;

using Helpdesk.Shared.DTOs.Orchestration;
using NewWeb::HelpDesk.NewWeb.Services;

namespace Helpdesk.Tests.NewWeb;

public sealed class OrchestrationCatalogTargetFilterTests
{
    [Fact]
    public void FilterForOrganization_ReturnsMatchingTenantTargets()
    {
        var definitions = new[]
        {
            Definition("alpha", 10, "Alpha Organization"),
            Definition("other", 20, "Other Organization")
        };

        var filtered = OrchestrationCatalogTargetFilter.FilterForOrganization(definitions, 10, "Alpha Organization");

        var target = Assert.Single(filtered);
        Assert.Equal("alpha", target.RequestDefinitionId);
    }

    [Fact]
    public void FilterForOrganization_MatchesByTenantName_WhenIdIsMissing()
    {
        var definitions = new[]
        {
            Definition("alpha", null, "Alpha-Organization"),
            Definition("other", null, "Other Organization")
        };

        var filtered = OrchestrationCatalogTargetFilter.FilterForOrganization(definitions, 10, "Alpha Organization");

        var target = Assert.Single(filtered);
        Assert.Equal("alpha", target.RequestDefinitionId);
    }

    [Fact]
    public void FilterForOrganization_ShowsAllTargets_WhenCatalogHasNoTenantMetadata()
    {
        var definitions = new[]
        {
            Definition("legacy-1", null, null),
            Definition("legacy-2", null, null)
        };

        var filtered = OrchestrationCatalogTargetFilter.FilterForOrganization(definitions, 10, "Alpha Organization");

        Assert.Equal(["legacy-1", "legacy-2"], filtered.Select(x => x.RequestDefinitionId));
    }

    [Fact]
    public void FilterForOrganization_KeepsSelectedTargetVisible_WhenMetadataDoesNotMatch()
    {
        var definitions = new[]
        {
            Definition("stored", 20, "Other Organization"),
            Definition("alpha", 10, "Alpha Organization")
        };

        var filtered = OrchestrationCatalogTargetFilter.FilterForOrganization(definitions, 10, "Alpha Organization", "stored");

        Assert.Contains(filtered, x => x.RequestDefinitionId == "stored");
        Assert.Contains(filtered, x => x.RequestDefinitionId == "alpha");
    }

    private static OrchestrationCatalogRequestDefinitionDto Definition(string id, int? tenantId, string? tenantName)
        => new()
        {
            RequestDefinitionId = id,
            RequestDefinitionName = id,
            FolderPath = "/helpdesk/requests",
            TenantId = tenantId,
            TenantName = tenantName
        };
}
