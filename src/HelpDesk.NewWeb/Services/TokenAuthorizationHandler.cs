using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;


namespace HelpDesk.NewWeb.Services;

public class TokenAuthorizationHandler : DelegatingHandler
{
    private readonly ITokenService _tokenService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly string _localCookieName;

    public TokenAuthorizationHandler(
        ITokenService tokenService,
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration)
    {
        _tokenService = tokenService;
        _httpContextAccessor = httpContextAccessor;
        _localCookieName = configuration.GetValue<bool>("Authentication:AllowInsecureLocalhost")
            ? "RatelDesk.Local"
            : "__Host-RatelDesk.Local";
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _tokenService.GetValidAccessTokenAsync();

        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        else
        {
            ForwardLocalSessionCookie(request);
        }

        try
        {
            return await base.SendAsync(request, cancellationToken);
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            return new HttpResponseMessage(System.Net.HttpStatusCode.GatewayTimeout)
            {
                RequestMessage = request,
                ReasonPhrase = "Request timed out"
            };
        }
    }

    private void ForwardLocalSessionCookie(HttpRequestMessage request)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context?.User.Identity?.IsAuthenticated != true ||
            !context.User.HasClaim("auth_mode", "local") ||
            request.Headers.Contains("Cookie"))
        {
            return;
        }

        var cookie = LocalSessionCookieForwarder.GetHeader(context, _localCookieName);
        if (!string.IsNullOrWhiteSpace(cookie))
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
            request.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
        }
    }
}
