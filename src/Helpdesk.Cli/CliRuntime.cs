using System.Net.Http.Headers;
using System.Text.Json;
using Helpdesk.AgentClient;

internal sealed class CliRuntime
{
    private readonly Func<HttpMessageHandler>? _handlerFactory;
    private readonly Dictionary<string, HelpdeskAgentClient> _agentClients = new(StringComparer.Ordinal);

    public CliRuntime(Func<HttpMessageHandler>? handlerFactory = null)
    {
        _handlerFactory = handlerFactory;
    }

    public TextWriter Out { get; init; } = Console.Out;
    public TextWriter Error { get; init; } = Console.Error;

    public HttpClient CreateHttpClient(Uri baseAddress)
    {
        var client = _handlerFactory is null ? new HttpClient() : new HttpClient(_handlerFactory(), disposeHandler: true);
        client.BaseAddress = baseAddress;
        client.Timeout = TimeSpan.FromSeconds(60);
        return client;
    }

    public async Task<string> GetAccessTokenAsync(ResolvedCliConfig config, CancellationToken ct = default)
    {
        var key = $"{config.ApiBaseUrl}|{config.AuthentikTokenUrl}|{config.AuthentikClientId}|{config.AuthentikUsername}|{config.AuthentikScope}";
        if (!_agentClients.TryGetValue(key, out var agentClient))
        {
            agentClient = new HelpdeskAgentClient(new AgentClientConfiguration(config.ApiBaseUrl.ToString(), config.AuthentikTokenUrl, config.AuthentikClientId, config.AuthentikUsername, config.AuthentikAppPassword, config.AuthentikScope, config.AgentUserEmail), _handlerFactory);
            _agentClients[key] = agentClient;
        }
        try { return await agentClient.GetAccessTokenAsync(ct).ConfigureAwait(false); }
        catch (AgentClientRemoteException ex) { throw new CliRemoteException(ex.Code, ex.Message, ex.StatusCode, ex.ResponseBody); }
        catch (AgentClientValidationException ex) { throw new CliValidationException(ex.Message); }
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync(ResolvedCliConfig config, CancellationToken ct = default)
    {
        var client = CreateHttpClient(config.ApiBaseUrl);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(config, ct).ConfigureAwait(false));
        return client;
    }
}
