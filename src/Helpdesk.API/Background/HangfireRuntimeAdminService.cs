using Hangfire;
using Hangfire.Storage;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.DTOs.Ops;
using Helpdesk.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.API.Background;

public interface IHangfireRuntimeAdminService
{
    Task<HangfireRuntimeSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task<HangfireRuntimeSettingsDto> UpdateSettingsAsync(UpdateHangfireRuntimeSettingsDto dto, string? updatedBy, CancellationToken cancellationToken = default);
}

public sealed class HangfireRuntimeAdminService(
    HelpdeskDbContext db,
    JobStorage jobStorage,
    HangfireRuntimeStatus runtimeStatus,
    IGraphDatasetSyncScheduleService graphDatasetSyncScheduleService) : IHangfireRuntimeAdminService
{
    private readonly HelpdeskDbContext _db = db;
    private readonly JobStorage _jobStorage = jobStorage;
    private readonly HangfireRuntimeStatus _runtimeStatus = runtimeStatus;
    private readonly IGraphDatasetSyncScheduleService _graphDatasetSyncScheduleService = graphDatasetSyncScheduleService;

    public async Task<HangfireRuntimeSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetOrCreateAsync(cancellationToken);
        return BuildDto(settings);
    }

    public async Task<HangfireRuntimeSettingsDto> UpdateSettingsAsync(UpdateHangfireRuntimeSettingsDto dto, string? updatedBy, CancellationToken cancellationToken = default)
    {
        var settings = await GetOrCreateAsync(cancellationToken);
        settings.Enabled = dto.Enabled;
        settings.UpdatedAtUtc = DateTimeOffset.UtcNow;
        settings.UpdatedBy = string.IsNullOrWhiteSpace(updatedBy) ? null : updatedBy.Trim();

        await _db.SaveChangesAsync(cancellationToken);
        await _graphDatasetSyncScheduleService.ReconcileAllAsync(cancellationToken);

        return BuildDto(settings);
    }

    private async Task<HangfireRuntimeSettings> GetOrCreateAsync(CancellationToken cancellationToken)
    {
        var settings = await _db.HangfireRuntimeSettings
            .FirstOrDefaultAsync(x => x.Id == 1, cancellationToken);

        if (settings is not null)
        {
            return settings;
        }

        settings = new HangfireRuntimeSettings
        {
            Id = 1,
            Enabled = false,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        _db.HangfireRuntimeSettings.Add(settings);
        await _db.SaveChangesAsync(cancellationToken);
        return settings;
    }

    private HangfireRuntimeSettingsDto BuildDto(HangfireRuntimeSettings settings)
    {
        using var connection = _jobStorage.GetConnection();
        var recurringJobs = connection.GetRecurringJobs();
        var activeJobs = recurringJobs.Where(x => !x.Removed).ToList();

        return new HangfireRuntimeSettingsDto
        {
            Enabled = settings.Enabled,
            QueueName = _runtimeStatus.QueueName,
            DashboardPath = _runtimeStatus.DashboardPath,
            RuntimeSource = _runtimeStatus.RuntimeSource,
            StorageProvider = _runtimeStatus.StorageProvider,
            RecurringJobCount = activeJobs.Count,
            GraphDatasetSyncJobCount = activeJobs.Count(x => x.Id.StartsWith("graph-dataset-sync:", StringComparison.Ordinal)),
            UpdatedAtUtc = settings.UpdatedAtUtc,
            UpdatedBy = settings.UpdatedBy
        };
    }
}
