using System.Net.Http.Json;

namespace HelpDesk.NewWeb.Services;

public interface IAppBarVersionApiClient
{
    Task<AppBarApiVersionInfo?> GetApiVersionAsync(CancellationToken cancellationToken = default);
}

public sealed class AppBarVersionApiClient(
    IHttpClientFactory httpClientFactory,
    ILogger<AppBarVersionApiClient> logger) : IAppBarVersionApiClient
{
    private readonly HttpClient _http = httpClientFactory.CreateClient("HelpdeskApi");

    public async Task<AppBarApiVersionInfo?> GetApiVersionAsync(CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            return await _http.GetFromJsonAsync<AppBarApiVersionInfo>("/api/v1/system/version", cts.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            logger.LogWarning(ex, "Could not load Helpdesk API version information for the app bar.");
            return null;
        }
    }
}

public sealed record AppBarApiVersionInfo(
    string ServiceName,
    string DisplayVersion,
    string InformationalVersion,
    string AssemblyVersion,
    string Environment);
