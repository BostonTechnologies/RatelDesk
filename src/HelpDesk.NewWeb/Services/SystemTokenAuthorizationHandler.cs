using System.Net.Http.Headers;

namespace HelpDesk.NewWeb.Services;

public class SystemTokenAuthorizationHandler(ISystemTokenService tokens) : DelegatingHandler
{
    private readonly ISystemTokenService _tokens = tokens;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _tokens.GetTokenAsync();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, cancellationToken);
    }
}
