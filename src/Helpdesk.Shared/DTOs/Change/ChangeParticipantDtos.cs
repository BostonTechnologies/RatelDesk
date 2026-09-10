namespace Helpdesk.Shared.DTOs.Change;

public record ChangeParticipantUserDto(
    string Id,
    string Name,
    string Email,
    string Role,
    string? OrganizationId);

public class ChangeParticipantsDto
{
    public string OrganizationId { get; set; } = string.Empty;
    public string? ItSupportOrganizationId { get; set; }
    public List<ChangeParticipantUserDto> RequestedForUsers { get; set; } = new();
    public List<ChangeParticipantUserDto> ImplementorUsers { get; set; } = new();
    public List<ChangeParticipantUserDto> ApproverUsers { get; set; } = new();
}
