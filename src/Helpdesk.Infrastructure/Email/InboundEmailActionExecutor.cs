using Helpdesk.Application.Incidents;
using Helpdesk.Application.Services.Tickets;
using Helpdesk.Shared.DTOs.Attachment;
using Helpdesk.Application.Messaging;
using Helpdesk.Application.Services.Email;
using Helpdesk.Application.Services.Tenants;
using Helpdesk.Shared.Auth;
using Helpdesk.Shared.DTOs.EmailRules;
using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Helpdesk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Helpdesk.Infrastructure.Email;

public sealed class InboundEmailActionExecutor(
    HelpdeskDbContext db,
    IRequestSender requestSender,
    ITenantProvisioningService tenantProvisioningService,
    IRepository<Incident> incidentRepo,
    IRepository<TicketTimelineEvent> timelineRepo,
    ILogger<InboundEmailActionExecutor> logger,
    IInboundInlineImageResolver inlineImageResolver,
    ITicketAttachmentService attachmentService)
    : IInboundEmailActionExecutor
{
    private static readonly string[] SupportRoles =
    [
        HelpdeskPermissions.HelpdeskAdmin,
        HelpdeskRoleBundles.Technical,
        HelpdeskPermissions.IncidentManager,
        HelpdeskPermissions.RequestManager,
        HelpdeskPermissions.ChangeManager,
        "Technician"
    ];

    public async Task<InboundEmailRuleProcessingResult> ExecuteAsync(
        InboundEmailRule rule,
        InboundEmailRuleActionConfig action,
        InboundEmailContext context,
        ForwardedEmailParseResult? forwarded,
        CancellationToken ct = default)
    {
        var actionKey = NormalizeActionKey(action);
        if (await HasProcessingLogAsync(context, rule.Id, actionKey, ct))
        {
            await TryLogAsync(context, rule, actionKey, true, InboundEmailProcessingStatus.Duplicate, null, "Duplicate message/rule/action.", ct);
            return new InboundEmailRuleProcessingResult(true, true, null);
        }

        await TryLogAsync(context, rule, actionKey, true, InboundEmailProcessingStatus.Matched, null, null, ct);

        var forwarder = await ResolveForwarderAsync(context.FromEmail, ct);
        if (forwarder is null || !IsSupportUser(forwarder))
        {
            await TryLogAsync(context, rule, actionKey, true, InboundEmailProcessingStatus.UnauthorizedSender, null, "Forwarding sender is not an authorized support user.", ct);
            return new InboundEmailRuleProcessingResult(false, false, null);
        }

        if (forwarded?.HasConfidentRequester != true)
        {
            await TryLogAsync(context, rule, actionKey, true, InboundEmailProcessingStatus.ParserFailed, null, "Original forwarded requester could not be confidently extracted.", ct);
            return new InboundEmailRuleProcessingResult(true, true, null);
        }

        var tenantResult = await ResolveTenantAsync(rule, context, forwarder, forwarded.OriginalFromEmail!, ct);
        if (tenantResult.Ambiguous)
        {
            await TryLogAsync(context, rule, actionKey, true, InboundEmailProcessingStatus.TenantResolutionAmbiguous, null, "Tenant resolution was ambiguous.", ct);
            return new InboundEmailRuleProcessingResult(true, true, null);
        }

        if (tenantResult.Organization is null)
        {
            await TryLogAsync(context, rule, actionKey, true, InboundEmailProcessingStatus.Failed, null, "Tenant could not be resolved.", ct);
            return new InboundEmailRuleProcessingResult(true, true, null);
        }

        try
        {
            var domain = forwarded.OriginalFromEmail!.Split('@').Last();
            var (customer, _) = await tenantProvisioningService.GetOrCreateCustomerAsync(
                forwarded.OriginalFromEmail!,
                forwarded.OriginalFromDisplayName ?? forwarded.OriginalFromEmail!,
                domain);

            if (!string.Equals(customer.OrganizationId, tenantResult.Organization.Id, StringComparison.OrdinalIgnoreCase))
            {
                customer.OrganizationId = tenantResult.Organization.Id;
                db.Customers.Update(customer);
                await db.SaveChangesAsync(ct);
            }

            var incident = await requestSender.Send(new CreateIncidentCommand(
                string.IsNullOrWhiteSpace(forwarded.OriginalSubject) ? context.Subject : forwarded.OriginalSubject!,
                forwarded.OriginalBodyHtml ?? forwarded.OriginalBodyText ?? context.HtmlBody,
                null,
                customer.Id,
                tenantResult.Organization.Id,
                null,
                null,
                null,
                null,
                customer.Email,
                [],
                null), ct);

            incident.State = TicketState.New;
            incident.AssignedToId = null;
            var resolution = await inlineImageResolver.ResolveAsync(
                incident.Id, forwarded.OriginalBodyHtml ?? context.HtmlBody, context.Attachments,
                context.GraphMessageId, context.InternetMessageId, ct);
            incident.Description = forwarded.OriginalBodyHtml is null && forwarded.OriginalBodyText is not null
                ? forwarded.OriginalBodyText
                : resolution.Html;
            incident.OriginalEmailHtml = resolution.Html;
            incident.OriginalEmailText = forwarded.OriginalBodyText ?? context.TextBody;
            incident.EmailFrom = forwarded.OriginalFromEmail;
            incident.EmailReceivedUtc = context.ReceivedUtc;
            await incidentRepo.UpdateAsync(incident);

            var uploads = context.Attachments
                .Where((attachment, index) => attachment.ContentBytes is not null && !resolution.ConsumedAttachmentIndexes.Contains(index))
                .Select(attachment => new AttachmentUpload(attachment.Name, attachment.ContentType, attachment.ContentBytes!))
                .ToList();
            if (uploads.Count > 0)
            {
                await attachmentService.SaveAsync(incident.Id, uploads, null, ct);
            }

            await timelineRepo.CreateAsync(new TicketTimelineEvent
            {
                TicketId = incident.Id,
                EventType = TimelineEventType.InternalNote,
                CreatedByUserId = forwarder.Id,
                CreatedByUserName = forwarder.Email,
                MessageText = $"Forwarded email processed by inbound rule. Forwarder: {forwarder.Email}. Source Message-ID: {context.InternetMessageId}."
            });

            await TryLogAsync(context, rule, actionKey, true, InboundEmailProcessingStatus.Succeeded, incident.Id, null, ct);
            return new InboundEmailRuleProcessingResult(true, rule.StopProcessing, incident);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Inbound forwarded email action failed for MessageId={MessageId}", context.InternetMessageId);
            await TryLogAsync(context, rule, actionKey, true, InboundEmailProcessingStatus.Failed, null, ex.Message, ct);
            return new InboundEmailRuleProcessingResult(true, true, null);
        }
    }

    internal async Task<User?> ResolveForwarderAsync(string email, CancellationToken ct) =>
        await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Email.ToLower() == email.Trim().ToLowerInvariant(), ct);

    internal static bool IsSupportUser(User user) =>
        SupportRoles.Any(role => string.Equals(user.Role, role, StringComparison.OrdinalIgnoreCase));

    private async Task<TenantResolutionResult> ResolveTenantAsync(
        InboundEmailRule rule,
        InboundEmailContext context,
        User forwarder,
        string originalRequesterEmail,
        CancellationToken ct)
    {
        var candidates = new List<Organization>();
        if (!string.IsNullOrWhiteSpace(context.MailboxTenantId))
        {
            await AddOrganizationAsync(candidates, context.MailboxTenantId, ct);
        }

        if (rule.ScopeType == InboundEmailRuleScopeType.Tenant && !string.IsNullOrWhiteSpace(rule.TenantId))
        {
            await AddOrganizationAsync(candidates, rule.TenantId, ct);
        }

        var managed = await ResolveManagedOrganizationsAsync(forwarder, ct);
        if (managed.Count == 1)
        {
            AddDistinct(candidates, managed[0]);
        }
        else if (candidates.Count == 0 && managed.Count > 1)
        {
            return new TenantResolutionResult(null, true);
        }

        if (candidates.Count == 0)
        {
            var domain = originalRequesterEmail.Split('@').Last();
            var organization = await tenantProvisioningService.GetOrCreateOrganizationByDomainAsync(domain);
            AddDistinct(candidates, organization);
        }

        return candidates.Select(x => x.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1
            ? new TenantResolutionResult(null, true)
            : new TenantResolutionResult(candidates.FirstOrDefault(), false);
    }

    private async Task<List<Organization>> ResolveManagedOrganizationsAsync(User forwarder, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(forwarder.OrganizationId))
        {
            return [];
        }

        return await db.Organizations.AsNoTracking()
            .Where(x => x.ItSupportOrganizationId == forwarder.OrganizationId || x.Id == forwarder.OrganizationId)
            .ToListAsync(ct);
    }

    private async Task AddOrganizationAsync(List<Organization> organizations, string? id, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        var organization = await db.Organizations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (organization is not null)
        {
            AddDistinct(organizations, organization);
        }
    }

    private static void AddDistinct(List<Organization> organizations, Organization organization)
    {
        if (!organizations.Any(x => string.Equals(x.Id, organization.Id, StringComparison.OrdinalIgnoreCase)))
        {
            organizations.Add(organization);
        }
    }

    private async Task<bool> HasProcessingLogAsync(InboundEmailContext context, string ruleId, string actionKey, CancellationToken ct)
    {
        var mailboxKey = MailboxKey(context.MailboxId);
        return await db.InboundEmailProcessingLogs.AsNoTracking().AnyAsync(x =>
            x.MessageId == context.InternetMessageId &&
            x.MailboxKey == mailboxKey &&
            x.RuleId == ruleId &&
            x.ActionKey == actionKey &&
            (x.Status == InboundEmailProcessingStatus.Succeeded || x.Status == InboundEmailProcessingStatus.Duplicate),
            ct);
    }

    private async Task TryLogAsync(
        InboundEmailContext context,
        InboundEmailRule rule,
        string actionKey,
        bool matched,
        InboundEmailProcessingStatus status,
        string? ticketId,
        string? error,
        CancellationToken ct)
    {
        db.InboundEmailProcessingLogs.Add(new InboundEmailProcessingLog
        {
            MessageId = context.InternetMessageId,
            MailboxId = context.MailboxId,
            MailboxKey = MailboxKey(context.MailboxId),
            TenantId = rule.TenantId ?? context.MailboxTenantId,
            RuleId = rule.Id,
            ActionKey = actionKey,
            Matched = matched,
            Status = status,
            TicketId = ticketId,
            Error = error
        });
        await db.SaveChangesAsync(ct);
    }

    private static string NormalizeActionKey(InboundEmailRuleActionConfig action) =>
        string.IsNullOrWhiteSpace(action.ActionKey)
            ? action.Type.ToString()
            : action.ActionKey.Trim();

    private static string MailboxKey(Guid? mailboxId) => mailboxId?.ToString("D") ?? "default";

    private sealed record TenantResolutionResult(Organization? Organization, bool Ambiguous);
}
