namespace Helpdesk.Application.WorkLogs;

public interface IInlineImageStorageService
{
    Task<string> SaveIncidentInlineImageAsync(
        string incidentId,
        string filename,
        byte[] content);

    Task<string> SaveWorklogInlineImageAsync(
        string worklogId,
        string filename,
        byte[] content);
}
