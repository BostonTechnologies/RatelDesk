using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace Helpdesk.API.Services;

public static partial class TicketMessageLinkifier
{
    private static readonly HashSet<string> SkippedAncestorNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "a",
        "script",
        "style",
        "textarea",
        "title"
    };

    public static string LinkifyPlainUrls(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return html ?? string.Empty;
        }

        var document = new HtmlDocument();
        document.LoadHtml(html);

        var textNodes = document.DocumentNode
            .Descendants()
            .Where(node => node.NodeType == HtmlNodeType.Text && !HasSkippedAncestor(node))
            .ToList();

        foreach (var textNode in textNodes)
        {
            ReplaceTextNodeUrls(document, textNode);
        }

        return document.DocumentNode.InnerHtml;
    }

    private static void ReplaceTextNodeUrls(HtmlDocument document, HtmlNode textNode)
    {
        var text = HtmlEntity.DeEntitize(textNode.InnerText);
        var matches = UrlRegex().Matches(text);
        if (matches.Count == 0)
        {
            return;
        }

        var replacementNodes = new List<HtmlNode>();
        var currentIndex = 0;

        foreach (Match match in matches)
        {
            if (match.Index > currentIndex)
            {
                replacementNodes.Add(document.CreateTextNode(text[currentIndex..match.Index]));
            }

            var matchedUrl = match.Value;
            var url = TrimTrailingPunctuation(matchedUrl, out var trailingText);

            if (Uri.TryCreate(url, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                var anchor = document.CreateElement("a");
                anchor.SetAttributeValue("href", url);
                anchor.SetAttributeValue("target", "_blank");
                anchor.SetAttributeValue("rel", "noopener noreferrer");
                anchor.InnerHtml = HtmlEntity.Entitize(url);
                replacementNodes.Add(anchor);
            }
            else
            {
                replacementNodes.Add(document.CreateTextNode(url));
            }

            if (!string.IsNullOrEmpty(trailingText))
            {
                replacementNodes.Add(document.CreateTextNode(trailingText));
            }

            currentIndex = match.Index + match.Length;
        }

        if (currentIndex < text.Length)
        {
            replacementNodes.Add(document.CreateTextNode(text[currentIndex..]));
        }

        foreach (var replacementNode in replacementNodes)
        {
            textNode.ParentNode.InsertBefore(replacementNode, textNode);
        }

        textNode.ParentNode.RemoveChild(textNode);
    }

    private static bool HasSkippedAncestor(HtmlNode node)
    {
        for (var current = node.ParentNode; current is not null; current = current.ParentNode)
        {
            if (SkippedAncestorNames.Contains(current.Name))
            {
                return true;
            }
        }

        return false;
    }

    private static string TrimTrailingPunctuation(string value, out string trailingText)
    {
        var end = value.Length;

        while (end > 0 && IsTrailingSentencePunctuation(value[end - 1]))
        {
            end--;
        }

        while (end > 0 && IsUnmatchedClosingDelimiter(value[..end], value[end - 1]))
        {
            end--;
        }

        trailingText = value[end..];
        return value[..end];
    }

    private static bool IsTrailingSentencePunctuation(char value)
        => value is '.' or ',' or ';' or ':' or '!' or '?';

    private static bool IsUnmatchedClosingDelimiter(string value, char closing)
    {
        var opening = closing switch
        {
            ')' => '(',
            ']' => '[',
            '}' => '{',
            _ => '\0'
        };

        return opening != '\0'
            && value.Count(character => character == closing) > value.Count(character => character == opening);
    }

    [GeneratedRegex(@"https?://[^\s<>""]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UrlRegex();
}
