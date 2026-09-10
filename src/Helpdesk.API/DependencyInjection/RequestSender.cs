using Helpdesk.Application.Messaging;

namespace Helpdesk.API.DependencyInjection;

public sealed class RequestSender(IServiceProvider serviceProvider) : IRequestSender
{
    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestType = request.GetType();
        var handlerType = typeof(IRequestHandler<,>).MakeGenericType(requestType, typeof(TResponse));
        var handlers = serviceProvider.GetServices(handlerType).ToArray();

        if (handlers.Length == 0)
        {
            throw new InvalidOperationException($"No request handler registered for {requestType.FullName}.");
        }

        if (handlers.Length > 1)
        {
            throw new InvalidOperationException($"Multiple request handlers registered for {requestType.FullName}.");
        }

        var handleMethod = handlerType.GetMethod("Handle")
            ?? throw new InvalidOperationException($"Request handler {handlerType.FullName} does not expose a Handle method.");

        var task = handleMethod.Invoke(handlers[0], [request, cancellationToken]) as Task<TResponse>;
        return task ?? throw new InvalidOperationException($"Request handler {handlerType.FullName} returned an unexpected result.");
    }
}
