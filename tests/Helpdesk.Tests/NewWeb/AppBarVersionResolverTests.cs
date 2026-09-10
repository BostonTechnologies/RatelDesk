extern alias NewWeb;

using NewWeb::HelpDesk.NewWeb.Components.Layout;
using Xunit;

namespace Helpdesk.Tests.NewWeb;

public class AppBarVersionResolverTests
{
    [Fact]
    public void ResolveDetails_UsesAssemblyMetadataInsteadOfDeploymentConfiguration()
    {
        var details = AppBarVersionResolver.ResolveDetails(typeof(AppBarVersionResolver).Assembly, "Production");

        Assert.Equal("v0.1.0", details.DisplayVersion);
        Assert.Equal("0.1.0", details.Version);
        Assert.Equal("HelpDesk.NewWeb", details.AssemblyName);
        Assert.Equal("Production", details.Environment);
        Assert.NotEqual("unknown", details.BuildTimestamp);
    }
}
