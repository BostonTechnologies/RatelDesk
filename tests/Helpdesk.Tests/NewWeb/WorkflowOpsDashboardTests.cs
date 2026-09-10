namespace Helpdesk.Tests.NewWeb;

public sealed class WorkflowOpsDashboardTests
{
    [Fact]
    public void Dashboard_LoadsRetriesPendingOnlyForHelpdeskAdmin()
    {
        var source = ReadDashboardSource();

        Assert.Contains("var retriesTask = isAdmin", source, StringComparison.Ordinal);
        Assert.Contains("api/v1/ops/tasks/retries-pending?page=1&pageSize=25", source, StringComparison.Ordinal);
        Assert.Contains("Task.FromResult(new PagedResponse<TaskOpsRowDto>())", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Dashboard_RendersRetriesPendingOnlyForHelpdeskAdmin()
    {
        var source = ReadDashboardSource();
        var retryHeadingIndex = source.IndexOf("Retries Pending", StringComparison.Ordinal);
        var firstAdminGuardBeforeRetry = source.LastIndexOf("@if (isAdmin)", retryHeadingIndex, StringComparison.Ordinal);

        Assert.True(retryHeadingIndex > 0);
        Assert.True(firstAdminGuardBeforeRetry >= 0);
    }

    [Fact]
    public void Dashboard_UsesSafePagedLoaderForWorkflowWidgets()
    {
        var source = ReadDashboardSource();

        Assert.Contains("LoadPagedOrEmptyAsync<T>", source, StringComparison.Ordinal);
        Assert.Contains("catch (HttpRequestException ex)", source, StringComparison.Ordinal);
        Assert.Contains("could not be loaded right now", source, StringComparison.Ordinal);
    }

    private static string ReadDashboardSource()
    {
        var path = Path.Combine(
            TestEnvironment.RepositoryRoot,
            "src",
            "HelpDesk.NewWeb",
            "Components",
            "Pages",
            "Ops",
            "WorkflowOpsDashboard.razor");
        return File.ReadAllText(path);
    }
}
