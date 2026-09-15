extern alias NewWeb;

using NewWeb::HelpDesk.NewWeb.Components.Pages.Admin.User;

namespace Helpdesk.Tests.NewWeb;

public sealed class TenantOrganizationSelectionTests
{
    [Fact]
    public void RouteChanges_RefreshTheOrganizationUsedForSubsequentTenantRequests()
    {
        var selection = new TenantOrganizationSelection();
        var organizations = new[] { "organization-a", "organization-b" };

        Assert.True(selection.ApplyRouteParameter("organization-a", organizations));
        Assert.Equal("api/v1/tenant-admin/organizations/organization-a/users/", selection.Path("users/"));

        Assert.True(selection.ApplyRouteParameter(null, organizations));
        selection.Select("organization-b");
        Assert.Equal("api/v1/tenant-admin/organizations/organization-b/users/member-1/assignments", selection.Path("users/member-1/assignments"));

        Assert.True(selection.ApplyRouteParameter("organization-a", organizations));
        Assert.Equal("api/v1/tenant-admin/organizations/organization-a/users/", selection.Path("users/"));
        Assert.Equal("api/v1/tenant-admin/organizations/organization-a/settings", selection.Path("settings"));
    }
}
