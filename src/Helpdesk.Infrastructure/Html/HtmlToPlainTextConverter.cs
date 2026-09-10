using System.Net;
using Helpdesk.Application.WorkLogs;
using HtmlAgilityPack;

namespace Helpdesk.Infrastructure.Html;

public sealed class HtmlToPlainTextConverter : IHtmlToPlainTextConverter
{
    public string Convert(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var text = doc.DocumentNode.InnerText;
        text = WebUtility.HtmlDecode(text);

        var lines = text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x));

        return string.Join('\n', lines);
    }
}
