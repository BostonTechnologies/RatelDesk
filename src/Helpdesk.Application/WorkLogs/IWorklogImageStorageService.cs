namespace Helpdesk.Application.WorkLogs;

public interface IWorklogImageStorageService
{
    Task<string> ExtractAndStoreImagesAsync(string worklogId, string html);
}
