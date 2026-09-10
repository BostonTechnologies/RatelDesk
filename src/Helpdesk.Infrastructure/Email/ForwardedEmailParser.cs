using System.Net;
using System.Text.RegularExpressions;
using Helpdesk.Application.Services.Email;
using Helpdesk.Shared.Enums;
using HtmlAgilityPack;

namespace Helpdesk.Infrastructure.Email;

public sealed class ForwardedEmailParser : IForwardedEmailParser
{
    private static readonly Regex EmailRegex = new(
        @"(?<name>[^<\r\n]*)<(?<email>[^<>\s@]+@[^<>\s@]+)>|(?<email2>[A-Z0-9._%+\-']+@[A-Z0-9.\-]+\.[A-Z]{2,})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public ForwardedEmailParseResult Parse(string? htmlBody, string? textBody)
    {
        var text = NormalizeText(!string.IsNullOrWhiteSpace(textBody) ? textBody : HtmlToText(htmlBody));
        if (string.IsNullOrWhiteSpace(text))
        {
            return NotForwarded();
        }

        var markerIndex = FindForwardedMarker(text);
        if (markerIndex < 0)
        {
            return NotForwarded();
        }

        var forwarded = text[markerIndex..];
        var fromLine = FindHeaderValue(forwarded, "From");
        if (string.IsNullOrWhiteSpace(fromLine))
        {
            return new ForwardedEmailParseResult(
                ForwardedEmailParseStatus.MissingOriginalSender,
                null,
                null,
                null,
                null,
                FindHeaderValue(forwarded, "Subject"),
                null,
                null,
                0.3);
        }

        var emailMatch = EmailRegex.Match(fromLine);
        var email = emailMatch.Groups["email"].Success
            ? emailMatch.Groups["email"].Value
            : emailMatch.Groups["email2"].Value;
        if (string.IsNullOrWhiteSpace(email))
        {
            return new ForwardedEmailParseResult(
                ForwardedEmailParseStatus.MissingOriginalSender,
                null,
                null,
                null,
                null,
                FindHeaderValue(forwarded, "Subject"),
                null,
                null,
                0.4);
        }

        var name = emailMatch.Groups["name"].Success
            ? emailMatch.Groups["name"].Value.Trim().Trim('"')
            : fromLine.Replace(email, string.Empty, StringComparison.OrdinalIgnoreCase).Trim().Trim('<', '>', '"');
        var subject = FindHeaderValue(forwarded, "Subject");
        var to = FindHeaderValue(forwarded, "To");
        var date = TryParseDate(FindHeaderValue(forwarded, "Sent") ?? FindHeaderValue(forwarded, "Date"));
        var bodyText = ExtractForwardedBody(forwarded);

        return new ForwardedEmailParseResult(
            ForwardedEmailParseStatus.Parsed,
            email.Trim().ToLowerInvariant(),
            string.IsNullOrWhiteSpace(name) ? null : name,
            to,
            date,
            string.IsNullOrWhiteSpace(subject) ? null : subject,
            WebUtility.HtmlEncode(bodyText).Replace("\n", "<br />", StringComparison.Ordinal),
            bodyText,
            string.IsNullOrWhiteSpace(bodyText) ? 0.75 : 0.9);
    }

    private static ForwardedEmailParseResult NotForwarded() => new(
        ForwardedEmailParseStatus.NotForwarded,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        0);

    private static int FindForwardedMarker(string text)
    {
        var markers = new[]
        {
            "-----Original Message-----",
            "From:",
            "Begin forwarded message:",
            "Forwarded message"
        };

        return markers
            .Select(marker => text.IndexOf(marker, StringComparison.OrdinalIgnoreCase))
            .Where(index => index >= 0)
            .DefaultIfEmpty(-1)
            .Min();
    }

    private static string? FindHeaderValue(string text, string header)
    {
        var pattern = @"(?im)^\s*" + Regex.Escape(header) + @":\s*(?<value>.+?)\s*$";
        var match = Regex.Match(text, pattern);
        return match.Success ? match.Groups["value"].Value.Trim() : null;
    }

    private static string ExtractForwardedBody(string forwarded)
    {
        var lines = forwarded.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var lastHeaderLine = -1;
        for (var i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"^\s*(From|Sent|Date|To|Cc|Subject):", RegexOptions.IgnoreCase))
            {
                lastHeaderLine = i;
            }
            else if (lastHeaderLine >= 0 && i > lastHeaderLine && !string.IsNullOrWhiteSpace(lines[i]))
            {
                return string.Join('\n', lines.Skip(i)).Trim();
            }
        }

        return string.Empty;
    }

    private static DateTimeOffset? TryParseDate(string? value) =>
        DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;

    private static string NormalizeText(string? value) =>
        WebUtility.HtmlDecode(value ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();

    private static string HtmlToText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        foreach (var br in doc.DocumentNode.SelectNodes("//br") ?? Enumerable.Empty<HtmlNode>())
        {
            br.ParentNode.ReplaceChild(doc.CreateTextNode("\n"), br);
        }

        foreach (var block in doc.DocumentNode.SelectNodes("//p|//div|//tr") ?? Enumerable.Empty<HtmlNode>())
        {
            block.AppendChild(doc.CreateTextNode("\n"));
        }

        return doc.DocumentNode.InnerText;
    }
}
