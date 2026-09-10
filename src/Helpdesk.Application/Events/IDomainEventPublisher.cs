namespace Helpdesk.Application.Events;

public interface IDomainEventPublisher
{
    Task PublishAsync(DomainEvent domainEvent, CancellationToken ct);
}
