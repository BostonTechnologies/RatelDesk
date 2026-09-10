using Helpdesk.Shared.DTOs.Customer;

namespace Helpdesk.Infrastructure.Auth.Authentik;

public interface ICustomerInvitationService
{
    Task<CustomerAuthStatusDto> GetStatusAsync(string customerId, CancellationToken ct = default);
    Task<CustomerAuthStatusDto> InviteAsync(string customerId, string invitedByUserId, CancellationToken ct = default);
    Task<CustomerAuthStatusDto> ResendInviteAsync(string customerId, string invitedByUserId, CancellationToken ct = default);
    Task<CustomerAuthStatusDto> DisableLoginAsync(string customerId, string disabledByUserId, CancellationToken ct = default);
    Task<CustomerAuthStatusDto> SyncAuthentikAsync(string customerId, CancellationToken ct = default);
}
