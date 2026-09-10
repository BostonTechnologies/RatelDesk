extern alias NewWeb;

using NewWeb::HelpDesk.NewWeb.Components.Layout;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Helpdesk.Tests.NewWeb;

public class AppBarVersionResolverTests
{
    [Fact]
    public void ResolveDetails_PrefersConfiguredAppBarVersion()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AppBar:BuildVersion"] = "0.0.148",
                ["OTEL_SERVICE_VERSION"] = "9.0.13"
            })
            .Build();

        var details = AppBarVersionResolver.ResolveDetails(configuration, typeof(AppBarVersionResolver).Assembly);

        Assert.Equal("v0.0.148", details.DisplayVersion);
        Assert.Equal("0.0.148", details.FullVersion);
        Assert.Equal("AppBar:BuildVersion", details.Source);
    }
}
