extern alias NewWeb;

using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NewWeb::HelpDesk.NewWeb.Services;
using Xunit;

namespace Helpdesk.Tests.NewWeb;

public sealed class FirstRunEntryMiddlewareTests
{
    [Theory]
    [InlineData("/", "Unconfigured")]
    [InlineData("/login", "Unconfigured")]
    [InlineData("/login", "Configuring")]
    [InlineData("/login", "Restarting")]
    [InlineData("/login", "RecoveryRequired")]
    [InlineData("/login/two-factor", "Unconfigured")]
    [InlineData("/LOGIN/", "Unconfigured")]
    public async Task Uninitialized_browser_entry_redirects_to_setup_before_showing_login(string path, string state)
    {
        using var api = new SetupApi(_ => Json(HttpStatusCode.OK, $$"""{"state":"{{state}}"}"""));
        var context = Context(path);
        context.Request.QueryString = new QueryString("?status=Sign-in%20failed");
        var reachedLogin = false;
        var middleware = new FirstRunEntryMiddleware(_ => { reachedLogin = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, api.Client);

        Assert.False(reachedLogin);
        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Equal("/setup", context.Response.Headers.Location.ToString());
        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
        Assert.Equal("/api/v1/setup/status", Assert.Single(api.RequestPaths));
    }

    [Theory]
    [InlineData("/local-login")]
    [InlineData("/local-login/two-factor")]
    public async Task Pre_setup_credential_posts_redirect_as_get_without_reading_or_forwarding_credentials(string path)
    {
        using var api = new SetupApi(_ => Json(HttpStatusCode.OK, "{\"state\":\"Unconfigured\"}"));
        var context = Context(path, "POST");
        context.Request.PathBase = "/desk";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("email=admin%40example.test&password=never-forward-this"));
        var reachedLogin = false;
        var middleware = new FirstRunEntryMiddleware(_ => { reachedLogin = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, api.Client);

        Assert.False(reachedLogin);
        Assert.Equal(StatusCodes.Status303SeeOther, context.Response.StatusCode);
        Assert.Equal("/desk/setup", context.Response.Headers.Location.ToString());
        Assert.Equal(0, context.Request.Body.Position);
        Assert.Equal("/api/v1/setup/status", Assert.Single(api.RequestPaths));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/login")]
    [InlineData("/login/two-factor")]
    [InlineData("/local-login")]
    [InlineData("/local-login/two-factor")]
    public async Task Ready_installation_preserves_existing_local_or_oidc_route_and_query(string path)
    {
        using var api = new SetupApi(_ => Json(HttpStatusCode.OK, "{\"state\":\"Ready\"}"));
        var context = Context(path);
        context.Request.QueryString = new QueryString("?ReturnUrl=%2Fincidents&status=Sign-in%20failed");
        var reachedLogin = false;
        var middleware = new FirstRunEntryMiddleware(current =>
        {
            Assert.Same(context, current);
            Assert.Equal(path, current.Request.Path.Value);
            Assert.Equal("?ReturnUrl=%2Fincidents&status=Sign-in%20failed", current.Request.QueryString.Value);
            reachedLogin = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, api.Client);

        Assert.True(reachedLogin);
        Assert.False(context.Response.Headers.ContainsKey("Location"));
    }

    [Theory]
    [InlineData("/setup")]
    [InlineData("/api/v1/setup/status")]
    [InlineData("/api/v1/setup/initialize")]
    [InlineData("/api/v1/local-auth/login")]
    [InlineData("/login-authentik")]
    [InlineData("/signin-authentik")]
    [InlineData("/css/helpdesk-design-system.css")]
    [InlineData("/_blazor/negotiate")]
    [InlineData("/health")]
    public async Task Setup_proxy_assets_and_identity_callbacks_do_not_wait_for_entry_probe(string path)
    {
        using var api = new SetupApi(_ => throw new InvalidOperationException("The setup API must not be called for this route."));
        var reachedRoute = false;
        var middleware = new FirstRunEntryMiddleware(_ => { reachedRoute = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(Context(path), api.Client);

        Assert.True(reachedRoute);
        Assert.Empty(api.RequestPaths);
    }

    [Theory]
    [InlineData(404, "")]
    [InlineData(401, "")]
    [InlineData(503, "")]
    [InlineData(200, "not json")]
    [InlineData(200, "{}")]
    [InlineData(200, "[]")]
    [InlineData(200, "{\"state\":\"Unknown\"}")]
    public async Task Unknown_api_state_shows_retry_page_instead_of_credentials(int status, string body)
    {
        using var api = new SetupApi(_ => Json((HttpStatusCode)status, body));
        var context = Context("/login");
        var reachedLogin = false;
        var middleware = new FirstRunEntryMiddleware(_ => { reachedLogin = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, api.Client);

        Assert.False(reachedLogin);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
        Assert.Equal("5", context.Response.Headers.RetryAfter.ToString());
        var html = Body(context);
        Assert.Contains("RatelDesk is temporarily unavailable", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/login\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<form", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<input", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sign-in failed", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("GET", "/login?ReturnUrl=%2Fincidents&amp;status=&quot;&gt;&lt;script&gt;")]
    [InlineData("POST", "/login")]
    public async Task Retry_link_is_encoded_and_never_reposts_credentials(string method, string expected)
    {
        using var api = new SetupApi(_ => Json(HttpStatusCode.ServiceUnavailable, ""));
        var context = Context("/login", method);
        context.Request.QueryString = new QueryString("?ReturnUrl=%2Fincidents&status=\"><script>");
        var middleware = new FirstRunEntryMiddleware(_ => throw new InvalidOperationException());

        await middleware.InvokeAsync(context, api.Client);

        var html = Body(context);
        Assert.Contains($"href=\"{expected}\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Setup_completion_is_visible_on_the_next_entry_request()
    {
        var state = "Unconfigured";
        using var api = new SetupApi(_ => Json(HttpStatusCode.OK, $$"""{"state":"{{state}}"}"""));
        var entered = 0;
        var middleware = new FirstRunEntryMiddleware(_ => { entered++; return Task.CompletedTask; });

        var initial = Context("/login");
        await middleware.InvokeAsync(initial, api.Client);
        state = "Ready";
        await middleware.InvokeAsync(Context("/login"), api.Client);
        state = "RecoveryRequired";
        var recovery = Context("/");
        await middleware.InvokeAsync(recovery, api.Client);

        Assert.Equal(1, entered);
        Assert.Equal("/setup", initial.Response.Headers.Location.ToString());
        Assert.Equal("/setup", recovery.Response.Headers.Location.ToString());
        Assert.Equal(3, api.RequestPaths.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Network_failure_or_probe_timeout_is_unavailable(bool timeout)
    {
        using var api = new SetupApi(_ => timeout
            ? throw new TaskCanceledException("Synthetic API timeout")
            : throw new HttpRequestException("Synthetic connection failure"));

        Assert.Equal(InstanceEntryState.Unavailable, await api.Client.GetAsync());
    }

    [Fact]
    public async Task Browser_cancellation_is_propagated_instead_of_rendering_an_error()
    {
        using var api = new SetupApi(_ => Json(HttpStatusCode.OK, "{\"state\":\"Ready\"}"));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => api.Client.GetAsync(cancelled.Token));
    }

    [Fact]
    public async Task Head_entry_reports_unavailability_without_an_html_body()
    {
        using var api = new SetupApi(_ => Json(HttpStatusCode.ServiceUnavailable, ""));
        var context = Context("/login", "HEAD");
        var middleware = new FirstRunEntryMiddleware(_ => throw new InvalidOperationException());

        await middleware.InvokeAsync(context, api.Client);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        Assert.Empty(Body(context));
    }

    private static DefaultHttpContext Context(string path, string method = "GET")
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = method;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static string Body(HttpContext context) => Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray());

    private static HttpResponseMessage Json(HttpStatusCode status, string body) => new(status)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private sealed class SetupApi : HttpMessageHandler, IHttpClientFactory
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> respond;

        public SetupApi(Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            this.respond = respond;
            Client = new InstanceSetupStatusClient(this, NullLogger<InstanceSetupStatusClient>.Instance);
        }

        public InstanceSetupStatusClient Client { get; }
        public List<string> RequestPaths { get; } = [];

        public HttpClient CreateClient(string name)
        {
            Assert.Equal("SystemApiNoAuth", name);
            return new HttpClient(this, disposeHandler: false) { BaseAddress = new Uri("http://127.0.0.1:9/") };
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Null(request.Content);
            RequestPaths.Add(request.RequestUri!.AbsolutePath);
            return Task.FromResult(respond(request));
        }
    }
}
