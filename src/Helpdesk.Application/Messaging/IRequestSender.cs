namespace Helpdesk.Application.Messaging;

public interface IRequestSender
{
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
}
