using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;

namespace Helpdesk.Application.Services.SupportNotifications;

public interface ISupportNotificationRecipientResolver
{
    Task<IReadOnlyList<SupportNotificationRecipient>> ResolveTicketEventRecipientsAsync(
        Ticket ticket,
        SupportNotificationEventType eventType,
        SupportNotificationChannel channel,
        CancellationToken ct = default);

    Task<IReadOnlyList<SupportNotificationRecipient>> ResolveOrganizationEventRecipientsAsync(
        string customerOrganizationId,
        SupportNotificationEventType eventType,
        SupportNotificationChannel channel,
        CancellationToken ct = default);

    Task<IReadOnlyList<SupportNotificationRecipient>> ResolveAssignmentRecipientsAsync(
        Ticket ticket,
        string newAssignedUserId,
        SupportNotificationChannel channel,
        CancellationToken ct = default);
}
