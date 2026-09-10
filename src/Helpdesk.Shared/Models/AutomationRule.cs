using Dodo.Primitives;

namespace Helpdesk.Shared.Models;

public class AutomationRule
{
    public string Id { get; set; } = Uuid.CreateVersion7().ToString();
    public string Name { get; set; } = string.Empty;
    public string Trigger { get; set; } = string.Empty;
    public string? Conditions { get; set; }
    public string? Actions { get; set; }
}
