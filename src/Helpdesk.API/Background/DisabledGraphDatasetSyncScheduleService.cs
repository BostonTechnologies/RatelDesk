namespace Helpdesk.API.Background;

public sealed class DisabledGraphDatasetSyncScheduleService(HangfireRuntimeStatus runtimeStatus) : IGraphDatasetSyncScheduleService
{
    private readonly HangfireRuntimeStatus _runtimeStatus = runtimeStatus;

    public Task ReconcileAsync(string organizationId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task ReconcileAllAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task ApplyDiagnosticsAsync(Helpdesk.Shared.DTOs.Resources.TenantGraphDatasetSettingsDto dto, CancellationToken cancellationToken = default)
    {
        dto.HangfireEnabled = false;
        dto.BackgroundSyncRegistered = false;
        dto.BackgroundSyncJobId = $"graph-dataset-sync:{dto.OrganizationId}";
        dto.BackgroundSyncQueueName = _runtimeStatus.QueueName;
        dto.BackgroundSyncNextExecutionUtc = null;
        dto.BackgroundSyncLastExecutionUtc = null;
        dto.HangfireRuntimeSource = _runtimeStatus.RuntimeSource;
        dto.HangfireStorageProvider = _runtimeStatus.StorageProvider;
        return Task.CompletedTask;
    }
}

public sealed class DisabledHangfireRuntimeAdminService(HangfireRuntimeStatus runtimeStatus) : IHangfireRuntimeAdminService
{
    private readonly HangfireRuntimeStatus _runtimeStatus = runtimeStatus;

    public Task<Helpdesk.Shared.DTOs.Ops.HangfireRuntimeSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(BuildDto());

    public Task<Helpdesk.Shared.DTOs.Ops.HangfireRuntimeSettingsDto> UpdateSettingsAsync(Helpdesk.Shared.DTOs.Ops.UpdateHangfireRuntimeSettingsDto dto, string? updatedBy, CancellationToken cancellationToken = default) =>
        Task.FromResult(BuildDto(updatedBy));

    private Helpdesk.Shared.DTOs.Ops.HangfireRuntimeSettingsDto BuildDto(string? updatedBy = null) => new()
    {
        Enabled = false,
        QueueName = _runtimeStatus.QueueName,
        DashboardPath = _runtimeStatus.DashboardPath,
        RuntimeSource = _runtimeStatus.RuntimeSource,
        StorageProvider = _runtimeStatus.StorageProvider,
        RecurringJobCount = 0,
        GraphDatasetSyncJobCount = 0,
        UpdatedAtUtc = DateTimeOffset.UtcNow,
        UpdatedBy = updatedBy
    };
}
