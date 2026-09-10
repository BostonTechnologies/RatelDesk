using Helpdesk.API.Services;
using HtmlAgilityPack;

namespace Helpdesk.Tests.Api;

public class TicketMessageLinkifierTests
{
    private const string TrendMicroUrl =
        "https://wfbs-svc-emea.trendmicro.com/wfbs-svc/portal/en/view/index?verify_did=1236087&ref=email#/logs/filter/log.type=400&log.subtypes%5B0%5D=200&period=custom&timeRange.from=1782277012&timeRange.to=1782280612";

    [Fact]
    public void LinkifyPlainUrls_CreatesLinkForTrendMicroUrl()
    {
        var html = $"<p>Open {TrendMicroUrl}</p>";

        var result = TicketMessageLinkifier.LinkifyPlainUrls(html);
        var anchor = GetSingleAnchor(result);

        Assert.Equal(TrendMicroUrl, anchor.GetAttributeValue("href", string.Empty));
        Assert.Equal("_blank", anchor.GetAttributeValue("target", string.Empty));
        Assert.Equal("noopener noreferrer", anchor.GetAttributeValue("rel", string.Empty));
        Assert.Equal(TrendMicroUrl, anchor.InnerText);
    }

    [Fact]
    public void LinkifyPlainUrls_DoesNotDoubleWrapExistingAnchors()
    {
        var html = $"<p><a href=\"{TrendMicroUrl}\">{TrendMicroUrl}</a></p>";

        var result = TicketMessageLinkifier.LinkifyPlainUrls(html);
        var anchor = GetSingleAnchor(result);

        Assert.Equal(TrendMicroUrl, anchor.GetAttributeValue("href", string.Empty));
        Assert.Equal(TrendMicroUrl, anchor.InnerText);
    }

    [Fact]
    public void LinkifyPlainUrls_LeavesTrailingSentencePunctuationOutsideAnchor()
    {
        var html = "<p>See https://example.com/path?x=1.</p>";

        var result = TicketMessageLinkifier.LinkifyPlainUrls(html);
        var anchor = GetSingleAnchor(result);

        Assert.Equal("https://example.com/path?x=1", anchor.GetAttributeValue("href", string.Empty));
        Assert.EndsWith("</a>.</p>", result);
    }

    [Fact]
    public void LinkifyPlainUrls_KeepsBalancedSquareBracketsInUrl()
    {
        var url = "https://example.com/logs/filter/log.subtypes[0]=200";
        var html = $"<p>{url}</p>";

        var result = TicketMessageLinkifier.LinkifyPlainUrls(html);
        var anchor = GetSingleAnchor(result);

        Assert.Equal(url, anchor.GetAttributeValue("href", string.Empty));
    }

    [Fact]
    public void LinkifyPlainUrls_PreservesExistingFormatting()
    {
        var html = "<table><tbody><tr><td><strong>Alert</strong> https://example.com/a?b=1&amp;c=2</td></tr></tbody></table>";

        var result = TicketMessageLinkifier.LinkifyPlainUrls(html);
        var anchor = GetSingleAnchor(result);

        Assert.Contains("<table>", result);
        Assert.Contains("<strong>Alert</strong>", result);
        Assert.Equal("https://example.com/a?b=1&c=2", anchor.GetAttributeValue("href", string.Empty));
    }

    private static HtmlNode GetSingleAnchor(string html)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);
        return Assert.Single(document.DocumentNode.Descendants("a"));
    }
}
