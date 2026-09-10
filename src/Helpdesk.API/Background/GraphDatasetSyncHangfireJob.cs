using Hangfire;
using Helpdesk.Application.Resources;
using Helpdesk.Application.Services.AI;

namespace Helpdesk.API.Background;

public sealed class GraphDatasetSyncHangfireJob(
    IDataManagementService dataManagementService,
    IGraphDatasetSyncScheduleService graphDatasetSyncScheduleService,
    ILogger<GraphDatasetSyncHangfireJob> logger)
{
    [DisableConcurrentExecution(timeoutInSeconds: 60 * 30)]
    public async Task RunAsync(string organizationId, CancellationToken ct)
    {
        logger.LogInformation("Starting Graph dataset sync for organization {OrganizationId}.", organizationId);
        IReadOnlyList<Helpdesk.Shared.DTOs.Resources.DatasetDefinitionDto> synced;
        try
        {
            synced = await dataManagementService.SyncBuiltInDatasetsAsync(organizationId, ct);
        }
        catch (SecretReentryRequiredException ex)
        {
            logger.LogWarning(
                ex,
                "Skipping Graph dataset sync retries for organization {OrganizationId} until the stored secret is re-entered.",
                organizationId);
            try
            {
                await graphDatasetSyncScheduleService.ReconcileAsync(organizationId, CancellationToken.None);
            }
            catch (Exception reconcileException)
            {
                logger.LogError(
                    reconcileException,
                    "Failed to remove Graph dataset sync job after secret re-entry was required for organization {OrganizationId}.",
                    organizationId);
            }
            return;
        }

        logger.LogInformation(
            "Completed Graph dataset sync for organization {OrganizationId}. DatasetCount={DatasetCount}.",
            organizationId,
            synced.Count);
    }
}
