using Hangfire;
using Hangfire.Storage;
using Helpdesk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.API.Background;

public interface IGraphDatasetSyncScheduleService
{
    Task ReconcileAsync(string organizationId, CancellationToken cancellationToken = default);
    Task ReconcileAllAsync(CancellationToken cancellationToken = default);
    Task ApplyDiagnosticsAsync(Helpdesk.Shared.DTOs.Resources.TenantGraphDatasetSettingsDto dto, CancellationToken cancellationToken = default);
}

public sealed class GraphDatasetSyncScheduleService(
    HelpdeskDbContext db,
    IRecurringJobManager recurringJobManager,
    JobStorage jobStorage,
    HangfireRuntimeStatus runtimeStatus,
    ILogger<GraphDatasetSyncScheduleService> logger) : IGraphDatasetSyncScheduleService
{
    private readonly HelpdeskDbContext _db = db;
    private readonly IRecurringJobManager _recurringJobManager = recurringJobManager;
    private readonly JobStorage _jobStorage = jobStorage;
    private readonly HangfireRuntimeStatus _runtimeStatus = runtimeStatus;
    private readonly ILogger<GraphDatasetSyncScheduleService> _logger = logger;

    public async Task ReconcileAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        var settings = await _db.TenantGraphDatasetSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId, cancellationToken);

        if (settings is null)
        {
            RemoveJob(organizationId, "Graph dataset settings are missing.");
            return;
        }

        if (!await IsGlobalRuntimeEnabledAsync(cancellationToken))
        {
            RemoveJob(organizationId, "Hangfire runtime is disabled in Ops settings.");
            return;
        }

        if (IsActionRequired(settings))
        {
            RemoveJob(organizationId, "Stored Graph secret requires re-entry.");
            return;
        }

        if (!ShouldSchedule(settings))
        {
            RemoveJob(organizationId, "Background sync is disabled or incomplete.");
            return;
        }

        var cron = string.IsNullOrWhiteSpace(settings.BackgroundSyncCronExpression)
            ? "0 */6 * * *"
            : settings.BackgroundSyncCronExpression.Trim();

        _recurringJobManager.AddOrUpdate<GraphDatasetSyncHangfireJob>(
            recurringJobId: BuildJobId(organizationId),
            queue: _runtimeStatus.QueueName,
            methodCall: job => job.RunAsync(organizationId, CancellationToken.None),
            cronExpression: cron,
            options: new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });

        _logger.LogInformation(
            "Configured Hangfire Graph dataset sync. OrganizationId={OrganizationId}, Cron={Cron}, Queue={Queue}.",
            organizationId,
            cron,
            _runtimeStatus.QueueName);
    }

    public async Task ReconcileAllAsync(CancellationToken cancellationToken = default)
    {
        var organizationIds = await _db.TenantGraphDatasetSettings
            .AsNoTracking()
            .Select(x => x.OrganizationId)
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        foreach (var organizationId in organizationIds)
        {
            await ReconcileAsync(organizationId, cancellationToken);
        }
    }

    public async Task ApplyDiagnosticsAsync(Helpdesk.Shared.DTOs.Resources.TenantGraphDatasetSettingsDto dto, CancellationToken cancellationToken = default)
    {
        dto.HangfireEnabled = await IsGlobalRuntimeEnabledAsync(cancellationToken);
        dto.BackgroundSyncJobId = BuildJobId(dto.OrganizationId);
        dto.BackgroundSyncQueueName = _runtimeStatus.QueueName;
        dto.HangfireRuntimeSource = _runtimeStatus.RuntimeSource;
        dto.HangfireStorageProvider = _runtimeStatus.StorageProvider;

        if (!dto.HangfireEnabled)
        {
            dto.BackgroundSyncRegistered = false;
            dto.BackgroundSyncNextExecutionUtc = null;
            dto.BackgroundSyncLastExecutionUtc = null;
            return;
        }

        using var connection = _jobStorage.GetConnection();
        var recurringJob = connection.GetRecurringJobs()
            .FirstOrDefault(x => string.Equals(x.Id, dto.BackgroundSyncJobId, StringComparison.Ordinal));

        dto.BackgroundSyncRegistered = recurringJob is not null && !recurringJob.Removed;
        dto.BackgroundSyncLastExecutionUtc = recurringJob?.LastExecution;
        dto.BackgroundSyncNextExecutionUtc = recurringJob?.NextExecution;
        dto.BackgroundSyncQueueName = recurringJob?.Queue ?? _runtimeStatus.QueueName;
    }

    private void RemoveJob(string organizationId, string reason)
    {
        var jobId = BuildJobId(organizationId);
        _recurringJobManager.RemoveIfExists(jobId);
        _logger.LogInformation(
            "Removed Hangfire Graph dataset sync. OrganizationId={OrganizationId}, Reason={Reason}.",
            organizationId,
            reason);
    }

    private static bool ShouldSchedule(Helpdesk.Shared.Models.TenantGraphDatasetSettings settings)
    {
        return settings.BackgroundSyncEnabled
            && !string.IsNullOrWhiteSpace(settings.BackgroundSyncCronExpression)
            && !string.IsNullOrWhiteSpace(settings.TenantId)
            && !string.IsNullOrWhiteSpace(settings.ClientId)
            && !string.IsNullOrWhiteSpace(settings.ClientSecretProtected)
            && (settings.EnableUsers || settings.EnableDevices || settings.EnableGroups || settings.EnableSharePointSites);
    }

    private static bool IsActionRequired(Helpdesk.Shared.Models.TenantGraphDatasetSettings settings)
        => string.Equals(settings.LastSyncStatus, "ActionRequired", StringComparison.OrdinalIgnoreCase);

    private async Task<bool> IsGlobalRuntimeEnabledAsync(CancellationToken cancellationToken)
    {
        var enabled = await _db.HangfireRuntimeSettings
            .AsNoTracking()
            .Where(x => x.Id == 1)
            .Select(x => (bool?)x.Enabled)
            .FirstOrDefaultAsync(cancellationToken);

        return enabled ?? false;
    }

    private static string BuildJobId(string organizationId) => $"graph-dataset-sync:{organizationId}";
}
