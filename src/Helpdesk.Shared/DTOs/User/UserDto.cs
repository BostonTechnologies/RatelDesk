namespace Helpdesk.Shared.DTOs.User;

public record UserDto(string Id, string Name, string Email, string Role, bool IsTestUser, string? OrganizationId = null);
