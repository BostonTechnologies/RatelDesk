using Helpdesk.Shared.Enums;

namespace Helpdesk.Shared.DTOs.SlaPolicy;

public class RecipientTargetDto
{
    public RecipientTargetType Type { get; set; } = RecipientTargetType.Email;
    public string Value { get; set; } = string.Empty;
}
