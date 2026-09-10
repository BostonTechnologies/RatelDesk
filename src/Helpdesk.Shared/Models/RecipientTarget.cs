using Helpdesk.Shared.Enums;

namespace Helpdesk.Shared.Models;

public class RecipientTarget
{
    public RecipientTargetType Type { get; set; } = RecipientTargetType.Email;
    public string Value { get; set; } = string.Empty;
}
