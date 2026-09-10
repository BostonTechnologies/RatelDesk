using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Encodings.Web;
using Helpdesk.Application.Services.EmailTemplates;
using Helpdesk.Infrastructure.EmailTemplates.Engine;

namespace Helpdesk.Infrastructure.EmailTemplates;

public sealed class TemplateEngine : ITemplateEngine
{
    public string Render(string template, object model, TemplateEngineOptions? options = null)
    {
        options ??= new TemplateEngineOptions();
        template ??= string.Empty;

        var tokens = TemplateTokenizer.Tokenize(template);
        var ast = TemplateParser.Parse(tokens, options.MaxDepth);

        var writer = new OutputWriter(options.MaxOutputChars);
        var frames = new List<RenderFrame> { new(model, null, null) };

        RenderNodes(ast, frames, writer, options);
        return writer.ToString();
    }

    private static void RenderNodes(
        IReadOnlyList<TemplateNode> nodes,
        List<RenderFrame> frames,
        OutputWriter writer,
        TemplateEngineOptions options)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case TextNode textNode:
                    writer.Append(textNode.Text);
                    break;

                case VariableNode variableNode:
                    RenderVariable(variableNode, frames, writer, options);
                    break;

                case IfBlockNode ifNode:
                    RenderIf(ifNode, frames, writer, options);
                    break;

                case EachBlockNode eachNode:
                    RenderEach(eachNode, frames, writer, options);
                    break;
            }
        }
    }

    private static void RenderVariable(
        VariableNode node,
        IReadOnlyList<RenderFrame> frames,
        OutputWriter writer,
        TemplateEngineOptions options)
    {
        if (!TryResolvePath(node.Path, frames, out var value))
        {
            switch (options.UnknownTokenBehavior)
            {
                case UnknownTokenBehavior.Ignore:
                    return;

                case UnknownTokenBehavior.Keep:
                    writer.Append(node.OriginalTag);
                    return;

                case UnknownTokenBehavior.Throw:
                    throw new TemplateRenderException($"Unknown token path '{node.Path}'.");
            }
        }

        var text = ToInvariantString(value);
        if (node.IsRaw)
        {
            writer.Append(text);
        }
        else
        {
            writer.Append(HtmlEncoder.Default.Encode(text));
        }
    }

    private static void RenderIf(
        IfBlockNode node,
        List<RenderFrame> frames,
        OutputWriter writer,
        TemplateEngineOptions options)
    {
        if (!TryResolvePath(node.ConditionPath, frames, out var value))
        {
            if (options.UnknownTokenBehavior == UnknownTokenBehavior.Throw)
                throw new TemplateRenderException($"Unknown token path '{node.ConditionPath}'.");

            return;
        }

        if (IsTruthy(value))
        {
            RenderNodes(node.Children, frames, writer, options);
        }
    }

    private static void RenderEach(
        EachBlockNode node,
        List<RenderFrame> frames,
        OutputWriter writer,
        TemplateEngineOptions options)
    {
        if (!TryResolvePath(node.ListPath, frames, out var value))
        {
            if (options.UnknownTokenBehavior == UnknownTokenBehavior.Throw)
                throw new TemplateRenderException($"Unknown token path '{node.ListPath}'.");

            return;
        }

        if (value is null)
            return;

        if (value is string)
            throw new TemplateRenderException($"Path '{node.ListPath}' is not an iterable collection.");

        if (value is not IEnumerable enumerable)
            throw new TemplateRenderException($"Path '{node.ListPath}' is not an iterable collection.");

        var index = 0;
        foreach (var item in enumerable)
        {
            frames.Add(new RenderFrame(item, item, index));
            RenderNodes(node.Children, frames, writer, options);
            frames.RemoveAt(frames.Count - 1);
            index++;
        }
    }

    private static bool TryResolvePath(string path, IReadOnlyList<RenderFrame> frames, out object? value)
    {
        value = null;
        var trimmed = path.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return false;

        if (string.Equals(trimmed, "@index", StringComparison.Ordinal))
        {
            for (var i = frames.Count - 1; i >= 0; i--)
            {
                var index = frames[i].Index;
                if (index.HasValue)
                {
                    value = index.Value;
                    return true;
                }
            }

            return false;
        }

        if (string.Equals(trimmed, "this", StringComparison.Ordinal))
        {
            for (var i = frames.Count - 1; i >= 0; i--)
            {
                if (frames[i].This is not null)
                {
                    value = frames[i].This;
                    return true;
                }
            }

            return false;
        }

        if (trimmed.StartsWith("this.", StringComparison.Ordinal))
        {
            var thisPath = trimmed[5..];
            for (var i = frames.Count - 1; i >= 0; i--)
            {
                if (frames[i].This is not null && TryResolveFromObject(frames[i].This, thisPath, out value))
                {
                    return true;
                }
            }

            return false;
        }

        for (var i = frames.Count - 1; i >= 0; i--)
        {
            if (TryResolveFromObject(frames[i].Value, trimmed, out value))
                return true;
        }

        return false;
    }

    private static bool TryResolveFromObject(object? source, string path, out object? value)
    {
        value = source;
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
            return false;

        foreach (var segment in segments)
        {
            if (!TryResolveSegment(value, segment, out value))
                return false;
        }

        return true;
    }

    private static bool TryResolveSegment(object? source, string segment, out object? value)
    {
        value = null;
        if (source is null)
            return false;

        if (source is IDictionary<string, object?> dictionary)
        {
            if (dictionary.TryGetValue(segment, out value))
                return true;

            var key = dictionary.Keys.FirstOrDefault(k => string.Equals(k, segment, StringComparison.OrdinalIgnoreCase));
            if (key is null)
                return false;

            value = dictionary[key];
            return true;
        }

        if (source is IDictionary legacyDictionary)
        {
            foreach (DictionaryEntry entry in legacyDictionary)
            {
                if (entry.Key is string key && string.Equals(key, segment, StringComparison.OrdinalIgnoreCase))
                {
                    value = entry.Value;
                    return true;
                }
            }

            return false;
        }

        var type = source.GetType();
        var property = type.GetProperty(segment, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (property is not null)
        {
            value = property.GetValue(source);
            return true;
        }

        var field = type.GetField(segment, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (field is not null)
        {
            value = field.GetValue(source);
            return true;
        }

        return false;
    }

    private static string ToInvariantString(object? value)
    {
        if (value is null)
            return string.Empty;

        if (value is string text)
            return text;

        if (value is IFormattable formattable)
            return formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty;

        return value.ToString() ?? string.Empty;
    }

    private static bool IsTruthy(object? value)
    {
        if (value is null)
            return false;

        if (value is bool b)
            return b;

        if (value is string s)
            return !string.IsNullOrEmpty(s);

        if (value is sbyte sb)
            return sb != 0;
        if (value is byte bt)
            return bt != 0;
        if (value is short sh)
            return sh != 0;
        if (value is ushort ush)
            return ush != 0;
        if (value is int i)
            return i != 0;
        if (value is uint ui)
            return ui != 0;
        if (value is long l)
            return l != 0;
        if (value is ulong ul)
            return ul != 0;
        if (value is float f)
            return Math.Abs(f) > float.Epsilon;
        if (value is double d)
            return Math.Abs(d) > double.Epsilon;
        if (value is decimal m)
            return m != 0m;

        if (value is IEnumerable enumerable && value is not string)
        {
            var enumerator = enumerable.GetEnumerator();
            using (enumerator as IDisposable)
            {
                return enumerator.MoveNext();
            }
        }

        return true;
    }

    private sealed record RenderFrame(object? Value, object? This, int? Index);

    private sealed class OutputWriter
    {
        private readonly int _maxChars;
        private readonly System.Text.StringBuilder _builder = new();

        public OutputWriter(int maxChars)
        {
            _maxChars = maxChars > 0 ? maxChars : throw new TemplateRenderException("MaxOutputChars must be greater than 0.");
        }

        public void Append(string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            if (_builder.Length + value.Length > _maxChars)
                throw new TemplateRenderException($"Rendered output exceeds MaxOutputChars ({_maxChars}).");

            _builder.Append(value);
        }

        public override string ToString() => _builder.ToString();
    }
}
