using Helpdesk.Application.Services.EmailTemplates;

namespace Helpdesk.Infrastructure.EmailTemplates.Engine;

internal static class TemplateParser
{
    public static IReadOnlyList<TemplateNode> Parse(IReadOnlyList<TemplateToken> tokens, int maxDepth)
    {
        if (maxDepth < 1)
            throw new TemplateParseException("MaxDepth must be at least 1.");

        var cursor = 0;
        var nodes = ParseNodes(tokens, ref cursor, null, 0, maxDepth);
        return nodes;
    }

    private static IReadOnlyList<TemplateNode> ParseNodes(
        IReadOnlyList<TemplateToken> tokens,
        ref int cursor,
        string? expectedBlockEnd,
        int depth,
        int maxDepth)
    {
        if (depth > maxDepth)
            throw new TemplateParseException($"Template nesting exceeds MaxDepth ({maxDepth}).");

        var nodes = new List<TemplateNode>();

        while (cursor < tokens.Count)
        {
            var token = tokens[cursor];
            switch (token.Type)
            {
                case TemplateTokenType.Text:
                    nodes.Add(new TextNode(token.Value));
                    cursor++;
                    break;

                case TemplateTokenType.Variable:
                    nodes.Add(new VariableNode(token.Value, false, token.OriginalTag ?? $"{{{{{token.Value}}}}}"));
                    cursor++;
                    break;

                case TemplateTokenType.RawVariable:
                    nodes.Add(new VariableNode(token.Value, true, token.OriginalTag ?? $"{{{{{{{token.Value}}}}}}}"));
                    cursor++;
                    break;

                case TemplateTokenType.BlockStart:
                    {
                        var (name, argument) = SplitBlockDefinition(token.Value);
                        if (!string.Equals(name, "if", StringComparison.Ordinal) &&
                            !string.Equals(name, "each", StringComparison.Ordinal))
                        {
                            throw new TemplateParseException($"Unsupported block '{{{{#{token.Value}}}}}'.");
                        }

                        cursor++;
                        var children = ParseNodes(tokens, ref cursor, name, depth + 1, maxDepth);

                        if (string.Equals(name, "if", StringComparison.Ordinal))
                        {
                            nodes.Add(new IfBlockNode(argument, token.OriginalTag ?? $"{{{{#{token.Value}}}}}", children));
                        }
                        else
                        {
                            nodes.Add(new EachBlockNode(argument, token.OriginalTag ?? $"{{{{#{token.Value}}}}}", children));
                        }

                        break;
                    }

                case TemplateTokenType.BlockEnd:
                    {
                        var endName = token.Value.Trim();
                        if (string.IsNullOrWhiteSpace(expectedBlockEnd))
                        {
                            throw new TemplateParseException($"Unexpected closing block '{{{{/{endName}}}}}'.");
                        }

                        if (!string.Equals(expectedBlockEnd, endName, StringComparison.Ordinal))
                        {
                            throw new TemplateParseException($"Mismatched closing block '{{{{/{endName}}}}}', expected '{{{{/{expectedBlockEnd}}}}}'.");
                        }

                        cursor++;
                        return nodes;
                    }

                default:
                    cursor++;
                    break;
            }
        }

        if (!string.IsNullOrWhiteSpace(expectedBlockEnd))
            throw new TemplateParseException($"Missing closing block '{{{{/{expectedBlockEnd}}}}}'.");

        return nodes;
    }

    private static (string Name, string Argument) SplitBlockDefinition(string value)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new TemplateParseException("Block declaration cannot be empty.");

        var firstWhitespace = trimmed.IndexOfAny([' ', '\t', '\r', '\n']);
        if (firstWhitespace < 0)
            throw new TemplateParseException($"Block declaration '{{{{#{value}}}}}' is missing an argument.");

        var name = trimmed[..firstWhitespace].Trim();
        var argument = trimmed[(firstWhitespace + 1)..].Trim();

        if (string.IsNullOrWhiteSpace(argument))
            throw new TemplateParseException($"Block declaration '{{{{#{value}}}}}' is missing an argument.");

        return (name, argument);
    }
}
