using System.Threading.Tasks;

namespace Helpdesk.Shared.Services;

/// <summary>
/// Generates unique ticket reference strings.
/// </summary>
public interface ITicketRefGeneratorService
{
    /// <summary>
    /// Generates the next reference for an incident ticket.
    /// </summary>
    /// <returns>A ticket reference string.</returns>
    string GenerateIncidentRef();
}
