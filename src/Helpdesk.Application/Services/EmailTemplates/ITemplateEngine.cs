namespace Helpdesk.Application.Services.EmailTemplates;

public interface ITemplateEngine
{
    string Render(string template, object model, TemplateEngineOptions? options = null);
}
