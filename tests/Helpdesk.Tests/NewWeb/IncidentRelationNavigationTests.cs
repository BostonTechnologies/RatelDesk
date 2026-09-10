namespace Helpdesk.Tests.NewWeb;

public class IncidentRelationNavigationTests
{
    [Fact]
    public void IncidentDetail_ReloadsWhenRouteParameterChanges()
    {
        var detailPath = Path.Combine(
            TestEnvironment.RepositoryRoot,
            "src",
            "HelpDesk.NewWeb",
            "Components",
            "Pages",
            "Incidents",
            "IncidentDetail.razor");
        var contents = File.ReadAllText(detailPath);

        Assert.Contains("OnParametersSetAsync", contents, StringComparison.Ordinal);
        Assert.Contains("loadedIncidentId", contents, StringComparison.Ordinal);
        Assert.Contains("activityTabsKey++", contents, StringComparison.Ordinal);
    }

    [Fact]
    public void RelationsPanel_ExposesOpenAndDeleteActions()
    {
        var relationsPath = Path.Combine(
            TestEnvironment.RepositoryRoot,
            "src",
            "HelpDesk.NewWeb",
            "Components",
            "Shared",
            "TicketRelations.razor");
        var contents = File.ReadAllText(relationsPath);

        Assert.Contains("Target=\"_blank\"", contents, StringComparison.Ordinal);
        Assert.Contains("Icons.Material.Filled.OpenInNew", contents, StringComparison.Ordinal);
        Assert.Contains("Icons.Material.Filled.Close", contents, StringComparison.Ordinal);
        Assert.Contains("DeleteRelationAsync", contents, StringComparison.Ordinal);
        Assert.Contains("HelpdeskApi.DeleteAsync", contents, StringComparison.Ordinal);
    }
}
