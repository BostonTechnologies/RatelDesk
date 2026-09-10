namespace Helpdesk.Shared.DTOs.User;

public record UpdateUserRequest(string Name, string Email, string? Password, string Role, bool IsTestUser = false, string? OrganizationId = null);
