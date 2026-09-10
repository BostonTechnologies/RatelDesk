using Dodo.Primitives;
using Helpdesk.Shared.Enums;

namespace Helpdesk.Shared.Models;

public class SupportGroupMember
{
    public string Id { get; set; } = Uuid.CreateVersion7().ToString();
    public string SupportGroupId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public SupportGroupMemberRole Role { get; set; } = SupportGroupMemberRole.Member;
    public SupportGroupMemberSource Source { get; set; } = SupportGroupMemberSource.Manual;
    public bool IsEnabled { get; set; } = true;
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedUtc { get; set; }
}
