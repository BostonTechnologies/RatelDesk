using System.Threading;

namespace Helpdesk.Shared.Services;

/// <summary>
/// Simple in-memory implementation of <see cref="ITicketRefGeneratorService"/>.
/// Generates sequential references like "INC-000001".
/// </summary>
public class TicketRefGeneratorService : ITicketRefGeneratorService
{
    private int _counter;

    /// <inheritdoc />
    public string GenerateIncidentRef()
    {
        var next = Interlocked.Increment(ref _counter);
        return $"INC-{next:D6}";
    }
}
