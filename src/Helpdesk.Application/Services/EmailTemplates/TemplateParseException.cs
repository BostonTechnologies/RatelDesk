namespace Helpdesk.Application.Services.EmailTemplates;

public sealed class TemplateParseException : Exception
{
    public TemplateParseException(string message) : base(message)
    {
    }
}
