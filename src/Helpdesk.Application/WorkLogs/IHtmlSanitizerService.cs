namespace Helpdesk.Application.WorkLogs;

public interface IHtmlSanitizerService
{
    string Sanitize(string html);
}
