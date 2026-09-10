using Xunit;

namespace Helpdesk.Tests.NewWeb;

public class TicketHeaderResponsiveTests
{
    [Fact]
    public void TicketHeader_UsesResponsiveMetadataClasses()
    {
        var headerPath = Path.Combine(TestEnvironment.RepositoryRoot, "src", "HelpDesk.NewWeb", "Components", "Shared", "TicketHeader.razor");
        var header = File.ReadAllText(headerPath);

        Assert.Contains("ticket-header-card", header, StringComparison.Ordinal);
        Assert.Contains("ticket-header-metadata-row", header, StringComparison.Ordinal);
        Assert.Contains("ticket-header-tracking-id", header, StringComparison.Ordinal);
        Assert.Contains("ticket-header-updated-at", header, StringComparison.Ordinal);
        Assert.Contains("ticket-header-requester-chip", header, StringComparison.Ordinal);
        Assert.Contains("ticket-header-metadata-slot", header, StringComparison.Ordinal);
        Assert.Contains("ticket-header-category-chip-set", header, StringComparison.Ordinal);
    }

    [Fact]
    public void AppCss_AllowsTicketHeaderMetadataToWrapWithinCard()
    {
        var cssPath = Path.Combine(TestEnvironment.RepositoryRoot, "src", "HelpDesk.NewWeb", "wwwroot", "css", "app.css");
        var css = File.ReadAllText(cssPath);

        Assert.Contains(".ticket-header-card", css, StringComparison.Ordinal);
        Assert.Contains(".ticket-header-metadata-row", css, StringComparison.Ordinal);
        Assert.Contains("flex-wrap: wrap", css, StringComparison.Ordinal);
        Assert.Contains("overflow-wrap: anywhere", css, StringComparison.Ordinal);
        Assert.Contains("text-overflow: ellipsis", css, StringComparison.Ordinal);
        Assert.Contains("@media (orientation: portrait) and (max-width: 1024px)", css, StringComparison.Ordinal);
    }
}
