namespace Helpdesk.Application.Services.AI;

public interface IAiPromptTemplateService
{
    string Render(string templateName, IReadOnlyDictionary<string, string?> values);
}
