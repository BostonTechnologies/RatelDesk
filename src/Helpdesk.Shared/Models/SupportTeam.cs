using Dodo.Primitives;

namespace Helpdesk.Shared.Models;

public class SupportTeam
{
    public string Id { get; set; } = Uuid.CreateVersion7().ToString();
    public string Name { get; set; } = string.Empty;
}
