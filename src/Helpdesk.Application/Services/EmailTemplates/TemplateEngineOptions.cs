namespace Helpdesk.Application.Services.EmailTemplates;

public sealed class TemplateEngineOptions
{
    public UnknownTokenBehavior UnknownTokenBehavior { get; set; } = UnknownTokenBehavior.Ignore;

    public int MaxDepth { get; set; } = 16;

    public int MaxOutputChars { get; set; } = 500_000;
}
