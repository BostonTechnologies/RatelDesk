namespace Helpdesk.API.Middleware;

/// <summary>Protects browser cookie mutations while leaving bearer-token clients unchanged.</summary>
public sealed class LocalCookieCsrfMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        var isLogin = string.Equals(request.Path.Value?.TrimEnd('/'), "/api/v1/local-auth/login", StringComparison.OrdinalIgnoreCase);
        if ((!context.User.HasClaim("auth_mode", "local") && !isLogin) ||
            HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method))
        {
            await next(context);
            return;
        }

        // Fetch Metadata is browser-controlled and cannot be forged by a hostile page.
        // Browser callers without it, and server-side Web clients, use a non-simple
        // header: cross-origin pages cannot send it without an allowed CORS preflight.
        // The API does not enable credentialed cross-origin access.
        var fetchSite = request.Headers["Sec-Fetch-Site"].ToString();
        var accepted = string.Equals(fetchSite, "same-origin", StringComparison.OrdinalIgnoreCase) ||
            (string.IsNullOrEmpty(fetchSite) &&
             string.Equals(request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.Ordinal));
        if (!accepted || string.Equals(request.Headers.Origin, "null", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "cross_origin_cookie_request" }, context.RequestAborted);
            return;
        }

        await next(context);
    }
}
