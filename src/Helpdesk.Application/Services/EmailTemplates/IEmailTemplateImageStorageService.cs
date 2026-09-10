namespace Helpdesk.Application.Services.EmailTemplates;

public interface IEmailTemplateImageStorageService
{
    Task<string> SaveTemplateInlineImageAsync(
        string templateName,
        string filename,
        byte[] content);
}
