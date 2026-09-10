namespace Helpdesk.Application.Events;

public sealed class NoopDomainEventPublisher : IDomainEventPublisher
{
    public static readonly NoopDomainEventPublisher Instance = new();

    private NoopDomainEventPublisher()
    {
    }

    public Task PublishAsync(DomainEvent domainEvent, CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}
