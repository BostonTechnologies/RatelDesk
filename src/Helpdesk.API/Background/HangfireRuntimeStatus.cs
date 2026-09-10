namespace Helpdesk.API.Background;

public sealed record HangfireRuntimeStatus(
    string QueueName,
    string DashboardPath,
    string RuntimeSource,
    string StorageProvider);
