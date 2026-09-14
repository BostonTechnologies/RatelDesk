using System.Text.Json;

namespace HelpDesk.NewWeb.Services;

/// <summary>Checks the API's installation state before offering browser sign-in.</summary>
public sealed class InstanceSetupStatusClient(
    IHttpClientFactory clients,
    ILogger<InstanceSetupStatusClient> logger)
{
    public async Task<InstanceEntryState> GetAsync(CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            // Only browser entry routes call this probe. Do not cache installation state:
            // a wizard completion must immediately stop redirecting users back to setup.
            using var client = clients.CreateClient("SystemApiNoAuth");
            using var response = await client.GetAsync("/api/v1/setup/status", timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Could not check installation state: the API returned HTTP {StatusCode}.",
                    (int)response.StatusCode);
                return InstanceEntryState.Unavailable;
            }

            var status = await response.Content.ReadFromJsonAsync<SetupStatusResponse>(timeout.Token);
            return status?.State switch
            {
                "Ready" => InstanceEntryState.Ready,
                "Unconfigured" or "Configuring" or "Restarting" or "RecoveryRequired" => InstanceEntryState.SetupRequired,
                _ => InstanceEntryState.Unavailable
            };
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Could not reach the API to check installation state.");
            return InstanceEntryState.Unavailable;
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "The API returned an invalid installation state.");
            return InstanceEntryState.Unavailable;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("The API installation state check timed out.");
            return InstanceEntryState.Unavailable;
        }
    }

    private sealed record SetupStatusResponse(string? State);
}

public enum InstanceEntryState
{
    Unavailable,
    SetupRequired,
    Ready
}
