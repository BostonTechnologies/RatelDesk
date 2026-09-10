namespace Helpdesk.Shared.DTOs.Ops;

public class HangfireRuntimeSettingsDto
{
    public bool Enabled { get; set; }
    public string QueueName { get; set; } = "default";
    public string DashboardPath { get; set; } = "/hangfire";
    public string RuntimeSource { get; set; } = "Ops UI / database";
    public string StorageProvider { get; set; } = "Application PostgreSQL database";
    public int RecurringJobCount { get; set; }
    public int GraphDatasetSyncJobCount { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
}
