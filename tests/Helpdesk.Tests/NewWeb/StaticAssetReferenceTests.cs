namespace Helpdesk.Tests.NewWeb;

public class StaticAssetReferenceTests
{
    [Fact]
    public void AppReferencesExistingCodeBeamStaticAssets()
    {
        var appPath = Path.Combine(TestEnvironment.RepositoryRoot, "src", "HelpDesk.NewWeb", "Components", "App.razor");
        var app = File.ReadAllText(appPath);

        Assert.DoesNotContain("mudblazor-extensions.css", app, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mudblazor-extensions.js", app, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MudBlazor.Extensions", app, StringComparison.Ordinal);
        Assert.DoesNotContain("CodeBeam.MudBlazor.Extensions", app, StringComparison.Ordinal);
    }
}
