using System.ComponentModel.DataAnnotations;
using Helpdesk.Shared.Enums;

namespace Helpdesk.Shared.DTOs.SupportNotifications;

public class SupportGroupMemberDto
{
    public string Id { get; set; } = string.Empty;
    public string SupportGroupId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string? UserEmail { get; set; }
    public SupportGroupMemberRole Role { get; set; }
    public SupportGroupMemberSource Source { get; set; }
    public bool IsEnabled { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset? UpdatedUtc { get; set; }
}

public class CreateSupportGroupMemberDto
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    public SupportGroupMemberRole Role { get; set; } = SupportGroupMemberRole.Member;
    public SupportGroupMemberSource Source { get; set; } = SupportGroupMemberSource.Manual;
    public bool IsEnabled { get; set; } = true;
}

public class UpdateSupportGroupMemberDto
{
    public SupportGroupMemberRole Role { get; set; } = SupportGroupMemberRole.Member;
    public SupportGroupMemberSource Source { get; set; } = SupportGroupMemberSource.Manual;
    public bool IsEnabled { get; set; } = true;
}
