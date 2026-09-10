using Helpdesk.Application.Services.EmailTemplates;
using System.Text;

namespace Helpdesk.Infrastructure.EmailTemplates.Engine;

internal enum TemplateTokenType
{
    Text,
    MustacheOpen,
    MustacheClose,
    TripleOpen,
    TripleClose,
    BlockStart,
    BlockEnd,
    Variable,
    RawVariable
}

internal sealed record TemplateToken(
    TemplateTokenType Type,
    string Value,
    int Position,
    string? OriginalTag = null);

internal static class TemplateTokenizer
{
    public static IReadOnlyList<TemplateToken> Tokenize(string template)
    {
        var tokens = new List<TemplateToken>();
        var text = new StringBuilder();

        var i = 0;
        while (i < template.Length)
        {
            if (IsEscapedMustache(template, i))
            {
                text.Append("{{");
                i += 3;
                continue;
            }

            if (StartsWith(template, i, "{{{"))
            {
                FlushText(tokens, text, i);
                var close = template.IndexOf("}}}", i + 3, StringComparison.Ordinal);
                if (close < 0)
                    throw new TemplateParseException("Unterminated triple mustache tag.");

                var original = template.Substring(i, (close + 3) - i);
                var inner = template.Substring(i + 3, close - (i + 3)).Trim();

                tokens.Add(new TemplateToken(TemplateTokenType.TripleOpen, "{{{", i));
                tokens.Add(new TemplateToken(TemplateTokenType.RawVariable, inner, i, original));
                tokens.Add(new TemplateToken(TemplateTokenType.TripleClose, "}}}", close));

                i = close + 3;
                continue;
            }

            if (StartsWith(template, i, "{{"))
            {
                FlushText(tokens, text, i);
                var close = template.IndexOf("}}", i + 2, StringComparison.Ordinal);
                if (close < 0)
                    throw new TemplateParseException("Unterminated mustache tag.");

                var original = template.Substring(i, (close + 2) - i);
                var inner = template.Substring(i + 2, close - (i + 2)).Trim();

                tokens.Add(new TemplateToken(TemplateTokenType.MustacheOpen, "{{", i));
                if (inner.StartsWith("#", StringComparison.Ordinal))
                {
                    tokens.Add(new TemplateToken(TemplateTokenType.BlockStart, inner[1..].Trim(), i, original));
                }
                else if (inner.StartsWith("/", StringComparison.Ordinal))
                {
                    tokens.Add(new TemplateToken(TemplateTokenType.BlockEnd, inner[1..].Trim(), i, original));
                }
                else
                {
                    tokens.Add(new TemplateToken(TemplateTokenType.Variable, inner, i, original));
                }

                tokens.Add(new TemplateToken(TemplateTokenType.MustacheClose, "}}", close));
                i = close + 2;
                continue;
            }

            text.Append(template[i]);
            i++;
        }

        FlushText(tokens, text, template.Length);
        return tokens;
    }

    private static bool IsEscapedMustache(string template, int position)
    {
        if (position + 2 >= template.Length)
            return false;

        return template[position] == '\\' && template[position + 1] == '{' && template[position + 2] == '{';
    }

    private static bool StartsWith(string value, int position, string pattern)
    {
        if (position + pattern.Length > value.Length)
            return false;

        for (var i = 0; i < pattern.Length; i++)
        {
            if (value[position + i] != pattern[i])
                return false;
        }

        return true;
    }

    private static void FlushText(List<TemplateToken> tokens, StringBuilder text, int position)
    {
        if (text.Length == 0)
            return;

        tokens.Add(new TemplateToken(TemplateTokenType.Text, text.ToString(), position - text.Length));
        text.Clear();
    }
}
