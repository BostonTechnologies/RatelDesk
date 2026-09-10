namespace Helpdesk.Application.Events;

public interface ICorrelationContext
{
    string GetCorrelationId();
}
