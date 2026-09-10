namespace Helpdesk.Application.Services.EmailTemplates;

public sealed class TemplateRenderException : Exception
{
    public TemplateRenderException(string message) : base(message)
    {
    }
}
