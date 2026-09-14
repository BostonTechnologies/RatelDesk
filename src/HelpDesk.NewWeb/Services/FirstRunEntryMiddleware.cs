using System.Text.Encodings.Web;

namespace HelpDesk.NewWeb.Services;

/// <summary>Routes first-time visitors to setup before credentials can be requested or submitted.</summary>
public sealed class FirstRunEntryMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, InstanceSetupStatusClient installation)
    {
        if (!IsEntryPath(context.Request.Path))
        {
            await next(context);
            return;
        }

        var state = await installation.GetAsync(context.RequestAborted);
        if (state == InstanceEntryState.Ready)
        {
            await next(context);
            return;
        }

        context.Response.Headers.CacheControl = "no-store";
        if (state == InstanceEntryState.SetupRequired)
        {
            // A credential POST must turn into a GET, never be replayed to setup.
            context.Response.StatusCode = HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method)
                ? StatusCodes.Status302Found
                : StatusCodes.Status303SeeOther;
            context.Response.Headers.Location = context.Request.PathBase.Add("/setup").Value;
            return;
        }

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.Headers.RetryAfter = "5";
        context.Response.ContentType = "text/html; charset=utf-8";
        if (HttpMethods.IsHead(context.Request.Method))
            return;

        var retryPath = HttpMethods.IsGet(context.Request.Method)
            ? context.Request.PathBase.Add(context.Request.Path).Value + context.Request.QueryString.Value
            : context.Request.PathBase.Add("/login").Value;
        var retryUrl = HtmlEncoder.Default.Encode(retryPath ?? "/");
        await context.Response.WriteAsync($$"""
            <!doctype html>
            <html lang="en">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>RatelDesk is temporarily unavailable</title>
                <style>
                    :root { color-scheme: light dark; font-family: system-ui, sans-serif; }
                    body { margin: 0; padding: 1.5rem; min-height: 100dvh; box-sizing: border-box; display: grid; place-items: center; background: #f4f6fa; color: #182536; }
                    main { box-sizing: border-box; width: min(100%, 34rem); padding: 2rem; border-radius: 1rem; background: white; box-shadow: 0 0.5rem 2rem #18253612; }
                    h1 { margin: 0 0 1rem; font-size: 1.5rem; line-height: 1.3; }
                    p { line-height: 1.6; }
                    a { display: inline-block; margin-top: 0.5rem; padding: 0.75rem 1.25rem; border-radius: 0.5rem; background: #155ca2; color: white; text-decoration: none; font-weight: 600; }
                    a:focus-visible { outline: 3px solid #182536; outline-offset: 3px; }
                    @media (prefers-color-scheme: dark) { body { background: #131b27; color: #f1f5fa; } main { background: #202c3c; } a:focus-visible { outline-color: #f1f5fa; } }
                </style>
            </head>
            <body>
                <main>
                    <h1>RatelDesk is temporarily unavailable</h1>
                    <p>We couldn’t check whether this instance is ready. If it has just started, give it a moment and try again.</p>
                    <p>If this continues, ask your administrator to check the application services.</p>
                    <a href="{{retryUrl}}">Try again</a>
                </main>
            </body>
            </html>
            """, context.RequestAborted);
    }

    private static bool IsEntryPath(PathString requestPath)
    {
        var path = requestPath.Value?.TrimEnd('/') ?? string.Empty;
        return path.Length == 0 ||
               path.Equals("/login", StringComparison.OrdinalIgnoreCase) ||
               path.Equals("/local-login", StringComparison.OrdinalIgnoreCase) ||
               path.Equals("/login/two-factor", StringComparison.OrdinalIgnoreCase) ||
               path.Equals("/local-login/two-factor", StringComparison.OrdinalIgnoreCase);
    }
}
