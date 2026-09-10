using Helpdesk.API.Middleware;
using Helpdesk.Application.Events;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Helpdesk.Tests.Api;

public class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_EchoesProvidedCorrelationId()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationConstants.HeaderName] = "corr-test-123";
        var middleware = new CorrelationIdMiddleware(
            _ => Task.CompletedTask,
            NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal("corr-test-123", context.Response.Headers[CorrelationConstants.HeaderName]);
        Assert.Equal("corr-test-123", context.Items[CorrelationConstants.HttpContextItemKey]);
    }

    [Fact]
    public async Task InvokeAsync_GeneratesCorrelationIdWhenHeaderIsMissing()
    {
        var context = new DefaultHttpContext();
        var middleware = new CorrelationIdMiddleware(
            _ => Task.CompletedTask,
            NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        var correlationId = Assert.IsType<string>(context.Items[CorrelationConstants.HttpContextItemKey]);
        Assert.StartsWith("corr-", correlationId);
        Assert.Equal(correlationId, context.Response.Headers[CorrelationConstants.HeaderName]);
    }

    [Fact]
    public async Task InvokeAsync_EchoesSmokeScenarioHeader()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationConstants.SmokeScenarioHeaderName] = "smoke-test-123";
        var middleware = new CorrelationIdMiddleware(
            _ => Task.CompletedTask,
            NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal("smoke-test-123", context.Items[CorrelationConstants.SmokeScenarioHttpContextItemKey]);
        Assert.Equal("smoke-test-123", context.Response.Headers[CorrelationConstants.SmokeScenarioHeaderName]);
    }
}
