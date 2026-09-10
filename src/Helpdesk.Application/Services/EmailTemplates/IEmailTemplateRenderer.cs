namespace Helpdesk.Application.Services.EmailTemplates;

public interface IEmailTemplateRenderer
{
    string Render(string templateHtml, EmailTemplateContext context);
}
