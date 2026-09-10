namespace Helpdesk.Shared.Services;

public interface ITenantContext
{
    string? TenantId { get; }
    string? UserId { get; }
    bool IsHelpdeskAdmin { get; }
}
