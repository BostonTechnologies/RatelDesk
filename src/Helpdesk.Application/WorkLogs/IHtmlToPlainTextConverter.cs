namespace Helpdesk.Application.WorkLogs;

public interface IHtmlToPlainTextConverter
{
    string Convert(string html);
}
