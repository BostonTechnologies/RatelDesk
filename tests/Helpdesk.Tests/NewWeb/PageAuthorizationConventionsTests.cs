using Xunit;

namespace Helpdesk.Tests.NewWeb;

public class PageAuthorizationConventionsTests
{
    [Fact]
    public void RoutablePages_DeclareAuthorizationExplicitly()
    {
        var repoRoot = TestEnvironment.RepositoryRoot;
        var pageRoot = Path.Combine(repoRoot, "src", "HelpDesk.NewWeb", "Components", "Pages");

        var pageFiles = Directory.EnumerateFiles(pageRoot, "*.razor", SearchOption.AllDirectories)
            .Where(path => File.ReadLines(path).Any(line => line.StartsWith("@page", StringComparison.Ordinal)))
            .OrderBy(path => path)
            .ToList();

        Assert.NotEmpty(pageFiles);

        var undecoratedPages = pageFiles
            .Where(path =>
            {
                var contents = File.ReadAllText(path);
                return !contents.Contains("@attribute [Authorize", StringComparison.Ordinal)
                    && !contents.Contains("@attribute [Microsoft.AspNetCore.Authorization.Authorize", StringComparison.Ordinal)
                    && !contents.Contains("@attribute [AllowAnonymous", StringComparison.Ordinal)
                    && !contents.Contains("@attribute [Microsoft.AspNetCore.Authorization.AllowAnonymous", StringComparison.Ordinal);
            })
            .Select(path => Path.GetRelativePath(repoRoot, path))
            .ToList();

        Assert.True(
            undecoratedPages.Count == 0,
            $"Routable pages must declare auth explicitly. Missing: {string.Join(", ", undecoratedPages)}");
    }

    [Fact]
    public void DataManagementPage_AllowsHelpdeskAdminAndDataManagementAdmin()
    {
        var pagePath = Path.Combine(TestEnvironment.RepositoryRoot, "src", "HelpDesk.NewWeb", "Components", "Pages", "Admin", "Resources", "DataManagement.razor");
        var contents = File.ReadAllText(pagePath);

        Assert.Contains("@attribute [Authorize(Roles = \"HelpdeskAdmin,DataManagement.Admin\")]", contents, StringComparison.Ordinal);
    }

    [Fact]
    public void NavMenu_ShowsDataManagementForDataManagementAdmin()
    {
        var navPath = Path.Combine(TestEnvironment.RepositoryRoot, "src", "HelpDesk.NewWeb", "Components", "Layout", "NavMenu.razor");
        var contents = File.ReadAllText(navPath);

        Assert.Contains("Roles=\"HelpdeskAdmin,DataManagement.Admin\"", contents, StringComparison.Ordinal);
        Assert.Contains("Href=\"/admin/resources/data-management\"", contents, StringComparison.Ordinal);
    }

    [Fact]
    public void TenantMembershipPage_UsesApiResolvedTenantAccess()
    {
        var repoRoot = TestEnvironment.RepositoryRoot;
        var page = File.ReadAllText(Path.Combine(repoRoot, "src", "HelpDesk.NewWeb", "Components", "Pages", "Admin", "User", "TenantAdministration.razor"));
        var navigation = File.ReadAllText(Path.Combine(repoRoot, "src", "HelpDesk.NewWeb", "Components", "Layout", "TenantAdministrationNavigation.razor"));
        var navMenu = File.ReadAllText(Path.Combine(repoRoot, "src", "HelpDesk.NewWeb", "Components", "Layout", "NavMenu.razor"));

        Assert.Contains("@page \"/tenant-administration\"", page, StringComparison.Ordinal);
        Assert.Contains("@attribute [Authorize(Roles = \"Tenant.Roles.Assign,HelpdeskAdmin\")]", page, StringComparison.Ordinal);
        Assert.Contains("api/v1/tenant-admin/organizations", page, StringComparison.Ordinal);
        Assert.Contains("Roles=\"Tenant.Roles.Assign,HelpdeskAdmin\"", navigation, StringComparison.Ordinal);
        Assert.Contains("Roles=\"Tenant.Settings.Manage,HelpdeskAdmin\"", navigation, StringComparison.Ordinal);
        var settingsPage = File.ReadAllText(Path.Combine(repoRoot, "src", "HelpDesk.NewWeb", "Components", "Pages", "Admin", "User", "TenantSettings.razor"));
        Assert.Contains("api/v1/tenant-admin/organizations?permission=Tenant.Settings.Manage", settingsPage, StringComparison.Ordinal);
        Assert.Contains("profile.HasPermission(HelpdeskPermissions.TenantSettingsManage, organization.Id)", settingsPage, StringComparison.Ordinal);
        Assert.Contains("Href=\"/admin/roles\"", navigation, StringComparison.Ordinal);
        Assert.Contains("<TenantAdministrationNavigation />", navMenu, StringComparison.Ordinal);
    }
}
