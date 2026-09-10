namespace Helpdesk.Infrastructure.EmailTemplates.Engine;

internal abstract record TemplateNode;

internal sealed record TextNode(string Text) : TemplateNode;

internal sealed record VariableNode(string Path, bool IsRaw, string OriginalTag) : TemplateNode;

internal sealed record IfBlockNode(string ConditionPath, string OriginalTag, IReadOnlyList<TemplateNode> Children) : TemplateNode;

internal sealed record EachBlockNode(string ListPath, string OriginalTag, IReadOnlyList<TemplateNode> Children) : TemplateNode;
