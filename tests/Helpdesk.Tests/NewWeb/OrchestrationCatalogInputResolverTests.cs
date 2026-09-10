extern alias NewWeb;

using Helpdesk.Shared.DTOs.Orchestration;
using NewWeb::HelpDesk.NewWeb.Services;

namespace Helpdesk.Tests.NewWeb;

public sealed class OrchestrationCatalogInputResolverTests
{
    [Fact]
    public void ResolveInputs_UsesRequestDefinitionInputs_First()
    {
        var requestDefinition = RequestDefinition(
            requestDefinitionId: "req-1",
            jobDefinitionId: "job-1",
            inputs: [Input("request-path")]);
        var jobs = new[]
        {
            Job("job-1", "Get Childitem pathparam", [Input("job-path")])
        };

        var inputs = OrchestrationCatalogInputResolver.ResolveInputs(requestDefinition, jobs);

        var input = Assert.Single(inputs);
        Assert.Equal("request-path", input.Key);
    }

    [Fact]
    public void ResolveInputs_FallsBackToJobInputs_WhenRequestDefinitionInputsAreEmpty()
    {
        var requestDefinition = RequestDefinition(
            requestDefinitionId: "req-1",
            jobDefinitionId: "job-1",
            inputs: []);
        var jobs = new[]
        {
            Job("job-1", "Get Childitem pathparam", [Input("path")])
        };

        var inputs = OrchestrationCatalogInputResolver.ResolveInputs(requestDefinition, jobs);

        var input = Assert.Single(inputs);
        Assert.Equal("path", input.Key);
    }

    [Fact]
    public void ResolveInputs_FallsBackToJobNameAndFolder_WhenJobIdIsMissing()
    {
        var requestDefinition = RequestDefinition(
            requestDefinitionId: "req-1",
            jobDefinitionId: null,
            inputs: [],
            displayName: "Get Childitem pathparam",
            folderPath: "/Example-Organization");
        var jobs = new[]
        {
            Job("job-1", "Get Childitem pathparam", [Input("path")], "/Example-Organization")
        };

        var inputs = OrchestrationCatalogInputResolver.ResolveInputs(requestDefinition, jobs);

        var input = Assert.Single(inputs);
        Assert.Equal("path", input.Key);
    }

    [Fact]
    public void ResolveInputs_MatchesJob_WhenRequestDisplayNameIncludesFolder()
    {
        var requestDefinition = RequestDefinition(
            requestDefinitionId: "req-1",
            jobDefinitionId: null,
            inputs: [],
            displayName: "/Example-Organization/Get Childitem pathparam",
            folderPath: "/Example-Organization");
        var jobs = new[]
        {
            Job("job-1", "Get Childitem pathparam", [Input("path")], "/Example-Organization")
        };

        var inputs = OrchestrationCatalogInputResolver.ResolveInputs(requestDefinition, jobs);

        var input = Assert.Single(inputs);
        Assert.Equal("path", input.Key);
    }

    [Fact]
    public void ResolveInputs_ReturnsEmpty_WhenCatalogInputsAreUnavailable()
    {
        var requestDefinition = RequestDefinition(
            requestDefinitionId: "req-1",
            jobDefinitionId: null,
            inputs: []);

        var inputs = OrchestrationCatalogInputResolver.ResolveInputs(requestDefinition, []);

        Assert.Empty(inputs);
    }

    [Fact]
    public void JoinTargetLabel_DoesNotDuplicateFolderPath()
    {
        var label = OrchestrationCatalogInputResolver.JoinTargetLabel(
            "Example-Organization/",
            "/Example-Organization/Get-Childitem pathparam");

        Assert.Equal("Example-Organization/Get-Childitem pathparam", label);
    }

    [Fact]
    public void FirstReadableName_SuppressesOpaqueIdentifiers()
    {
        var name = OrchestrationCatalogInputResolver.FirstReadableName(
            "C200145AF30C99E98DAB6FCF3F15A8DC9A7546F4719754A834013409AA170A58",
            "Get-Childitem pathparam");

        Assert.Equal("Get-Childitem pathparam", name);
    }

    private static OrchestrationCatalogRequestDefinitionDto RequestDefinition(
        string requestDefinitionId,
        string? jobDefinitionId,
        IReadOnlyList<OrchestrationCatalogInputDefinitionDto> inputs,
        string displayName = "Get Childitem pathparam",
        string folderPath = "/")
        => new()
        {
            RequestDefinitionId = requestDefinitionId,
            RequestDefinitionName = displayName,
            DisplayName = displayName,
            FolderPath = folderPath,
            OrchestrationJobDefinitionId = jobDefinitionId,
            Inputs = inputs
        };

    private static OrchestrationCatalogJobDto Job(
        string id,
        string name,
        IReadOnlyList<OrchestrationCatalogInputDefinitionDto> inputs,
        string folderPath = "/")
        => new()
        {
            Id = id,
            Name = name,
            DisplayName = name,
            FolderPath = folderPath,
            Inputs = inputs
        };

    private static OrchestrationCatalogInputDefinitionDto Input(string key)
        => new()
        {
            Key = key,
            Label = key,
            Type = "text"
        };
}
