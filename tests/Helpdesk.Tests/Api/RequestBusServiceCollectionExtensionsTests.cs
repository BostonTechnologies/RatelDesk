using Helpdesk.API.DependencyInjection;
using Helpdesk.Application.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Helpdesk.Tests.Api;

public class RequestBusServiceCollectionExtensionsTests
{
    [Fact]
    public async Task RequestSender_ResolvesAndInvokesRegisteredHandler()
    {
        var services = new ServiceCollection();
        services.AddScoped<IRequestSender, RequestSender>();
        services.AddScoped<IRequestHandler<TestPing, string>, TestPingHandler>();

        await using var provider = services.BuildServiceProvider().CreateAsyncScope();
        var sender = provider.ServiceProvider.GetRequiredService<IRequestSender>();

        var response = await sender.Send(new TestPing("hello"));

        Assert.Equal("HELLO", response);
    }

    [Fact]
    public async Task RequestSender_ThrowsClearErrorWhenHandlerMissing()
    {
        var services = new ServiceCollection();
        services.AddScoped<IRequestSender, RequestSender>();

        await using var provider = services.BuildServiceProvider().CreateAsyncScope();
        var sender = provider.ServiceProvider.GetRequiredService<IRequestSender>();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => sender.Send(new MissingHandlerRequest()));
        Assert.Contains(nameof(MissingHandlerRequest), error.Message);
    }

    [Fact]
    public async Task RequestSender_ThrowsClearErrorWhenDuplicateHandlersRegistered()
    {
        var services = new ServiceCollection();
        services.AddScoped<IRequestSender, RequestSender>();
        services.AddScoped<IRequestHandler<DuplicateRequest, string>, DuplicateRequestHandlerA>();
        services.AddScoped<IRequestHandler<DuplicateRequest, string>, DuplicateRequestHandlerB>();

        await using var provider = services.BuildServiceProvider().CreateAsyncScope();
        var sender = provider.ServiceProvider.GetRequiredService<IRequestSender>();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => sender.Send(new DuplicateRequest()));
        Assert.Contains(nameof(DuplicateRequest), error.Message);
    }

    private sealed record TestPing(string Message) : IRequest<string>;

    private sealed class TestPingHandler : IRequestHandler<TestPing, string>
    {
        public Task<string> Handle(TestPing request, CancellationToken cancellationToken)
            => Task.FromResult(request.Message.ToUpperInvariant());
    }

    private sealed record MissingHandlerRequest : IRequest<string>;

    private sealed record DuplicateRequest : IRequest<string>;

    private sealed class DuplicateRequestHandlerA : IRequestHandler<DuplicateRequest, string>
    {
        public Task<string> Handle(DuplicateRequest request, CancellationToken cancellationToken)
            => Task.FromResult("A");
    }

    private sealed class DuplicateRequestHandlerB : IRequestHandler<DuplicateRequest, string>
    {
        public Task<string> Handle(DuplicateRequest request, CancellationToken cancellationToken)
            => Task.FromResult("B");
    }
}
