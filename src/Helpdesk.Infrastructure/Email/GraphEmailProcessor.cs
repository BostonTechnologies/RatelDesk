using Azure.Identity;
using Helpdesk.Application.Events;
using Helpdesk.Application.Incidents;
using Helpdesk.Application.Messaging;
using Helpdesk.Application.Services.AI;
using Helpdesk.Application.Services.Tickets;
using Helpdesk.Application.Services.Notifications;
using Helpdesk.Application.Services.Tenants;
using Helpdesk.Application.Services.EmailTemplates;
using Helpdesk.Application.Tickets;
using Helpdesk.Application.WorkLogs;
using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Helpdesk.Shared.DTOs.Attachment;
using Helpdesk.Infrastructure.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Pgvector;

namespace Helpdesk.Application.Services.Email;

public class GraphEmailProcessor : IGraphEmailProcessor
{
    private static readonly Regex TrackingReferenceRegex = new(@"\b(?:INC|REQ)-[A-Z0-9-]+\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DsnStatusRegex = new(@"\bStatus:\s*5(?:\.\d{1,3}){0,2}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IRequestSender _requestSender;
    private readonly ILogger<GraphEmailProcessor> _logger;
    private readonly ITenantProvisioningService _tenantProvisioningService;
    private readonly ITicketNotificationService _ticketNotificationService;
    private readonly IRepository<BlockedEntity> _blockedRepo;
    private readonly IRepository<OrganizationAiKbSettings> _aiKbRepo;
    private readonly IEmailService _emailService;
    private readonly IRepository<EmailTemplate> _templateRepo;
    private readonly IRepository<Incident> _incidentRepo;
    private readonly IRepository<Ticket> _ticketRepo;
    private readonly IRepository<TicketTimelineEvent> _timelineRepo;
    private readonly ITicketAttachmentService _attachmentService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IRepository<KnowledgeEmbedding> _embeddingRepo;
    private readonly IInboundInlineImageResolver _inlineImageResolver;
    private readonly IEmailTemplateRenderer _templateRenderer;
    private readonly IEmailLayoutResolver _layoutResolver;
    private readonly ITenantBrandingResolver _tenantBrandingResolver;
    private readonly IDomainEventPublisher _domainEvents;
    private readonly ICorrelationContext? _correlationContext;
    private readonly IInboundEmailRuleProcessor? _inboundEmailRuleProcessor;

    public GraphEmailProcessor(
        IRequestSender requestSender,
        ILogger<GraphEmailProcessor> logger,
        ITenantProvisioningService tenantProvisioningService,
        ITicketNotificationService ticketNotificationService,
        IRepository<BlockedEntity> blockedRepo,
        IRepository<OrganizationAiKbSettings> aiKbRepo,
        IEmailService emailService,
        IRepository<EmailTemplate> templateRepo,
        IRepository<Incident> incidentRepo,
        IRepository<Ticket> ticketRepo,
        IRepository<TicketTimelineEvent> timelineRepo,
        ITicketAttachmentService attachmentService,
        IEmbeddingService embeddingService,
        IRepository<KnowledgeEmbedding> embeddingRepo,
        IInboundInlineImageResolver inlineImageResolver,
        IEmailTemplateRenderer templateRenderer,
        IEmailLayoutResolver layoutResolver,
        ITenantBrandingResolver tenantBrandingResolver,
        IDomainEventPublisher? domainEvents = null,
        ICorrelationContext? correlationContext = null,
        IInboundEmailRuleProcessor? inboundEmailRuleProcessor = null)
    {
        _requestSender = requestSender;
        _logger = logger;
        _tenantProvisioningService = tenantProvisioningService;
        _ticketNotificationService = ticketNotificationService;
        _blockedRepo = blockedRepo;
        _aiKbRepo = aiKbRepo;
        _emailService = emailService;
        _templateRepo = templateRepo;
        _incidentRepo = incidentRepo;
        _ticketRepo = ticketRepo;
        _timelineRepo = timelineRepo;
        _attachmentService = attachmentService;
        _embeddingService = embeddingService;
        _embeddingRepo = embeddingRepo;
        _inlineImageResolver = inlineImageResolver;
        _templateRenderer = templateRenderer;
        _layoutResolver = layoutResolver;
        _tenantBrandingResolver = tenantBrandingResolver;
        _domainEvents = domainEvents ?? NoopDomainEventPublisher.Instance;
        _correlationContext = correlationContext;
        _inboundEmailRuleProcessor = inboundEmailRuleProcessor;
    }

    public async Task ProcessEmailByInternetMessageIdAsync(string internetMessageId, ImapEmailSettings settings, CancellationToken token)
    {
        var credential = new ClientSecretCredential(settings.TenantId, settings.ClientId, settings.ClientSecret);
        var graphClient = new GraphServiceClient(credential);
        var mailboxAddress = settings.UserEmail;

        var processedFolderId = await GetOrCreateMailFolderIdAsync(graphClient, mailboxAddress, settings.ProcessedFolder, token);
        var blockedFolderId = await GetOrCreateMailFolderIdAsync(graphClient, mailboxAddress, settings.BlockedFolder, token);

        if (string.IsNullOrEmpty(processedFolderId) || string.IsNullOrEmpty(blockedFolderId))
        {
            _logger.LogError("Could not find or create required mail folders. Halting processing for this message.");
            return;
        }

        var filter = $"internetMessageId eq '<{internetMessageId}>'";
        var messages = await graphClient.Users[mailboxAddress].Messages.GetAsync(req =>
        {
            req.QueryParameters.Filter = filter;
            req.QueryParameters.Select =
            [
                "id",
                "subject",
                "body",
                "from",
                "toRecipients",
                "ccRecipients",
                "receivedDateTime",
                "internetMessageId",
                "internetMessageHeaders"
            ];
        }, cancellationToken: token);

        var graphMessage = messages?.Value?.FirstOrDefault();
        if (graphMessage is null)
        {
            _logger.LogWarning("Could not find message in Graph with Internet-Message-ID: {MessageId}", internetMessageId);
            return;
        }

        var fromAddress = graphMessage.From?.EmailAddress?.Address;
        if (EmailAddressGuard.IsSameAddress(fromAddress, mailboxAddress))
        {
            _logger.LogWarning(
                "Skipping self-originated message {GraphId} from mailbox {MailboxAddress}; marking it read and moving it to the processed folder.",
                graphMessage.Id,
                mailboxAddress);
            await FinalizeGraphMessageAsync(graphClient, mailboxAddress, graphMessage, processedFolderId, null, token);
            return;
        }

        if (IsDeliveryFailureMessage(graphMessage))
        {
            var bouncedTicket = await ResolveDeliveryFailureTicketAsync(graphMessage, internetMessageId, token);
            await FinalizeGraphMessageAsync(graphClient, mailboxAddress, graphMessage, processedFolderId, bouncedTicket, token);
            return;
        }

        if (string.IsNullOrWhiteSpace(fromAddress) || !fromAddress.Contains('@'))
        {
            _logger.LogWarning("Message {GraphId} has no valid sender address. Skipping.", graphMessage.Id);
            return;
        }

        var fromName = graphMessage.From?.EmailAddress?.Name ?? string.Empty;
        var attachmentsResponse = await graphClient.Users[mailboxAddress].Messages[graphMessage.Id].Attachments.GetAsync(cancellationToken: token);
        var attachments = attachmentsResponse?.Value ?? Enumerable.Empty<Microsoft.Graph.Models.Attachment>();

        if (_inboundEmailRuleProcessor is not null)
        {
            var ruleResult = await _inboundEmailRuleProcessor.ProcessAsync(
                BuildInboundEmailContext(graphMessage, attachments, mailboxAddress, internetMessageId),
                token);
            if (ruleResult.Handled && ruleResult.StopDefaultProcessing)
            {
                await FinalizeGraphMessageAsync(graphClient, mailboxAddress, graphMessage, processedFolderId, ruleResult.Ticket, token);
                return;
            }
        }

        var domain = fromAddress.Split('@').Last();
        var organization = await _tenantProvisioningService.GetOrCreateOrganizationByDomainAsync(domain);
        var (customer, isNewCustomer) = await _tenantProvisioningService.GetOrCreateCustomerAsync(fromAddress, fromName, domain);

        if (customer.State == EntityState.Blocked || organization.State == EntityState.Blocked)
        {
            _logger.LogWarning("Sender {SenderEmail} is blocked.", fromAddress);
            await SendNotificationAsync("AccessDenied", fromAddress, tenantId: organization.Id);
            await MoveGraphMessageAsync(graphClient, mailboxAddress, graphMessage.Id, blockedFolderId); // use ID
            return;
        }

        if (isNewCustomer)
        {
            await SendNotificationAsync("WelcomeNewUser", customer.Email, userName: customer.Name, tenantId: customer.OrganizationId);
        }

        var ticket = await ProcessTicketEmailAsync(
            graphMessage,
            attachments,
            customer,
            token,
            internetMessageId);

        await FinalizeGraphMessageAsync(graphClient, mailboxAddress, graphMessage, processedFolderId, ticket, token);
    }

    internal async Task<Incident> ProcessIncidentAsync(
        Message graphMessage,
        IEnumerable<Microsoft.Graph.Models.Attachment> attachments,
        Customer customer,
        CancellationToken token,
        string? internetMessageId = null)
    {
        var ticket = await ProcessTicketEmailAsync(graphMessage, attachments, customer, token, internetMessageId);
        if (ticket is Incident incident)
        {
            return incident;
        }

        if (ticket is null)
        {
            throw new InvalidOperationException("Inbound email was ignored and did not create or update an incident.");
        }

        throw new InvalidOperationException($"Inbound email resolved to non-incident ticket {ticket.TrackingId}.");
    }

    internal async Task<Ticket?> ProcessTicketEmailAsync(
        Message graphMessage,
        IEnumerable<Microsoft.Graph.Models.Attachment> attachments,
        Customer customer,
        CancellationToken token,
        string? internetMessageId = null)
    {
        Ticket? ticket = null;
        var subject = graphMessage.Subject ?? string.Empty;
        var match = TrackingReferenceRegex.Match(subject);
        if (match.Success)
        {
            var trackingId = match.Value;
            ticket = (await _ticketRepo.GetAllAsync())
                .FirstOrDefault(i => i.TrackingId.Equals(trackingId, StringComparison.OrdinalIgnoreCase));

            if (ticket is null && trackingId.StartsWith("INC-", StringComparison.OrdinalIgnoreCase))
            {
                ticket = (await _incidentRepo.GetAllAsync())
                    .FirstOrDefault(i => i.TrackingId.Equals(trackingId, StringComparison.OrdinalIgnoreCase));
            }
        }

        if (IsDeliveryFailureMessage(graphMessage))
        {
            return await ResolveDeliveryFailureTicketAsync(graphMessage, internetMessageId, token);
        }

        if (ticket is not null && ticket.EmailExclusionReason != TicketEmailExclusionReason.None)
        {
            await RecordInboundMessageMarkerAsync(
                ticket.Id,
                internetMessageId,
                graphMessage.From?.EmailAddress?.Address,
                graphMessage.From?.EmailAddress?.Name ?? customer.Name);
            _logger.LogInformation(
                "Skipping inbound email update for excluded ticket {TrackingId}. Reason={Reason}",
                ticket.TrackingId,
                ticket.EmailExclusionReason);
            return ticket;
        }

        if (ticket is null && !string.IsNullOrWhiteSpace(internetMessageId))
        {
            ticket = await FindTicketByInboundMessageIdAsync(internetMessageId);
            if (ticket is not null)
            {
                _logger.LogInformation(
                    "Skipping duplicate inbound email ticket creation for MessageId={MessageId}. Existing ticket={TrackingId}",
                    internetMessageId,
                    ticket.TrackingId);
                return ticket;
            }
        }

        var incomingCc = NormalizeEmails(
            graphMessage.CcRecipients?.Select(r => r.EmailAddress?.Address) ?? Enumerable.Empty<string>(),
            customer.Email);

        var bodyContent = graphMessage.Body?.Content ?? string.Empty;
        var htmlBody = graphMessage.Body?.Content ?? string.Empty;
        if (graphMessage.Body?.ContentType == BodyType.Html)
        {
            htmlBody = graphMessage.Body.Content ?? string.Empty;
        }
        else
        {
            htmlBody = HtmlEncoder.Default.Encode(graphMessage.Body?.Content ?? string.Empty);
        }

        var inlineAttachments = attachments.OfType<FileAttachment>().Select(ToAttachmentContext).ToList();
        var resolution = new InboundInlineImageResult(htmlBody, new HashSet<int>());
        var updatedHtml = htmlBody;
        if (ticket is not null)
        {
            resolution = await _inlineImageResolver.ResolveAsync(ticket.Id, htmlBody, inlineAttachments, graphMessage.Id, internetMessageId, token);
            updatedHtml = resolution.Html;
        }

        var skipFinalUpdate = false;
        if (ticket is null)
        {
            var incident = await _requestSender.Send(new CreateIncidentCommand(
                graphMessage.Subject ?? "Email Ticket",
                updatedHtml,
                null, customer.Id, customer.OrganizationId, null, null, null, null,
                customer.Email,
                incomingCc,
                null), token);

            resolution = await _inlineImageResolver.ResolveAsync(incident.Id, htmlBody, inlineAttachments, graphMessage.Id, internetMessageId, token);
            updatedHtml = resolution.Html;
            incident.Description = updatedHtml;
            incident.OriginalEmailHtml = updatedHtml;
            incident.OriginalEmailText = bodyContent;
            incident.EmailFrom = graphMessage.From?.EmailAddress?.Address;
            incident.EmailReceivedUtc = graphMessage.ReceivedDateTime;
            // Apply final state changes up-front so we only update once for creation path
            incident.State = TicketState.WaitingReply;
            incident.UpdatedAt = DateTime.UtcNow;
            incident.LastReplierName = customer.Name;
            await _incidentRepo.UpdateAsync(incident);
            await RecordInboundMessageMarkerAsync(
                incident.Id,
                internetMessageId,
                graphMessage.From?.EmailAddress?.Address,
                graphMessage.From?.EmailAddress?.Name ?? customer.Name);
            skipFinalUpdate = true;

            var sent = await _ticketNotificationService.SendNewTicketConfirmationAsync(
                incident,
                customer.Email,
                customer.Name,
                incident.CcRecipients,
                token);

            if (!sent)
            {
                _logger.LogWarning("Failed to send new ticket confirmation for incident {TrackingId}", incident.TrackingId);
            }

            await TryCreateEmailEmbeddingsAsync(customer.OrganizationId, incident.Id, graphMessage.Body?.Content ?? string.Empty, token);

            _logger.LogInformation("Successfully created Incident {TrackingId}", incident.TrackingId);
            ticket = incident;
        }
        else
        {
            if (incomingCc.Count > 0)
            {
                ticket.CcRecipients = ticket.CcRecipients
                    .Concat(incomingCc)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            if (string.IsNullOrWhiteSpace(ticket.RequesterEmail))
            {
                ticket.RequesterEmail = customer.Email;
            }

            var senderAddress = graphMessage.From?.EmailAddress?.Address?.Trim();
            var senderName = graphMessage.From?.EmailAddress?.Name?.Trim();
            var timelineCreatedByUserId = !string.IsNullOrWhiteSpace(internetMessageId)
                ? internetMessageId
                : (!string.IsNullOrWhiteSpace(senderAddress) ? senderAddress : customer.Email);
            var timelineCreatedByUserName = !string.IsNullOrWhiteSpace(senderAddress)
                ? senderAddress
                : (!string.IsNullOrWhiteSpace(senderName) ? senderName : customer.Name);

            var alreadyProcessed = (await _timelineRepo.GetAllAsync())
                .Any(e =>
                    e.TicketId == ticket.Id &&
                    e.EventType == TimelineEventType.CustomerReply &&
                    string.Equals(e.CreatedByUserId, timelineCreatedByUserId, StringComparison.OrdinalIgnoreCase));

            if (alreadyProcessed)
            {
                _logger.LogInformation(
                    "Skipping duplicate inbound email worklog for ticket {TicketId}. MessageId={MessageId}",
                    ticket.Id,
                    timelineCreatedByUserId);
            }
            else
            {
                await _requestSender.Send(new CreateWorkLogCommand(
                    ticket.Id,
                    0,
                    updatedHtml,
                    null,
                    customer.Name,
                    false,
                    TimelineEventType.CustomerReply,
                    timelineCreatedByUserId,
                    timelineCreatedByUserName), token);
            }
        }

        var tracked = await _ticketRepo.GetAsync(ticket.Id);
        tracked ??= ticket is Incident incidentForLookup
            ? await _incidentRepo.GetAsync(incidentForLookup.Id) ?? ticket
            : ticket;
        if (!skipFinalUpdate)
        {
            tracked.CcRecipients = ticket.CcRecipients;
            tracked.RequesterEmail = ticket.RequesterEmail;
            tracked.State = TicketState.WaitingReply;
            tracked.UpdatedAt = DateTime.UtcNow;
            tracked.LastReplierName = customer.Name;
            if (tracked is Incident trackedIncident && ticket is Incident sourceIncident)
            {
                trackedIncident.OriginalEmailHtml = sourceIncident.OriginalEmailHtml;
                trackedIncident.OriginalEmailText = sourceIncident.OriginalEmailText;
                trackedIncident.EmailFrom = sourceIncident.EmailFrom;
                trackedIncident.EmailReceivedUtc = sourceIncident.EmailReceivedUtc;
                await _incidentRepo.UpdateAsync(trackedIncident);
            }
            else
            {
                await _ticketRepo.UpdateAsync(tracked);
            }
        }

        var uploads = inlineAttachments
            .Where((a, index) => a.ContentBytes is not null && !resolution.ConsumedAttachmentIndexes.Contains(index))
            .Select(a => new AttachmentUpload(
                a.Name ?? "attachment",
                a.ContentType ?? "application/octet-stream",
                a.ContentBytes!))
            .ToList();

        if (uploads.Count > 0)
        {
            await _attachmentService.SaveAsync(tracked.Id, uploads, null, token);
        }

        return tracked;
    }

    private async Task<Ticket?> ResolveDeliveryFailureTicketAsync(
        Message graphMessage,
        string? internetMessageId,
        CancellationToken token)
    {
        var references = ExtractTrackingReferences(graphMessage).ToList();
        if (references.Count != 1)
        {
            _logger.LogInformation(
                "Ignoring inbound delivery failure MessageId={MessageId}. ReferenceCount={ReferenceCount}",
                internetMessageId,
                references.Count);
            return null;
        }

        var trackingId = references[0];
        var ticket = (await _ticketRepo.GetAllAsync())
            .FirstOrDefault(i => string.Equals(i.TrackingId, trackingId, StringComparison.OrdinalIgnoreCase));

        if (ticket is null && trackingId.StartsWith("INC-", StringComparison.OrdinalIgnoreCase))
        {
            ticket = (await _incidentRepo.GetAllAsync())
                .FirstOrDefault(i => string.Equals(i.TrackingId, trackingId, StringComparison.OrdinalIgnoreCase));
        }

        _logger.LogInformation(
            "Ignoring inbound delivery failure MessageId={MessageId} for TrackingId={TrackingId}. TicketFound={TicketFound}",
            internetMessageId,
            trackingId,
            ticket is not null);

        return ticket;
    }

    internal static bool IsDeliveryFailureMessage(Message graphMessage)
    {
        var headers = GetHeaders(graphMessage);
        var body = graphMessage.Body?.Content ?? string.Empty;
        var sender = graphMessage.From?.EmailAddress?.Address ?? string.Empty;
        var subject = graphMessage.Subject ?? string.Empty;

        if (HasDsnMimeProof(headers))
        {
            return true;
        }

        if (!IsKnownSystemSender(sender) && !HeadersContainKnownSystemSender(headers))
        {
            return false;
        }

        var markerCount = CountDsnBodyHeaderMarkers(headers, body);
        if (markerCount >= 2)
        {
            return true;
        }

        return HasDeliveryFailureSubject(subject) && HasSupportingDsnSignal(headers, body);
    }

    private static bool HasDsnMimeProof(IReadOnlyDictionary<string, List<string>> headers)
    {
        var contentType = HeaderValues(headers, "content-type");
        if (contentType.Any(value =>
                value.Contains("multipart/report", StringComparison.OrdinalIgnoreCase) &&
                value.Contains("delivery-status", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (contentType.Any(value => value.Contains("message/delivery-status", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }

    private static bool HasSupportingDsnSignal(IReadOnlyDictionary<string, List<string>> headers, string body)
    {
        return HasEmptyReturnPath(headers) ||
               HasAutoSubmittedHeader(headers) ||
               CountDsnBodyHeaderMarkers(headers, body) > 0;
    }

    private static bool HasEmptyReturnPath(IReadOnlyDictionary<string, List<string>> headers)
    {
        return HeaderValues(headers, "return-path").Any(value => value.Trim() == "<>");
    }

    private static bool HasAutoSubmittedHeader(IReadOnlyDictionary<string, List<string>> headers)
    {
        return HeaderValues(headers, "auto-submitted").Any(value =>
            value.Contains("auto-replied", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("auto-generated", StringComparison.OrdinalIgnoreCase));
    }

    private static int CountDsnBodyHeaderMarkers(IReadOnlyDictionary<string, List<string>> headers, string body)
    {
        var count = 0;

        if (HeaderValues(headers, "x-failed-recipients").Any(value => !string.IsNullOrWhiteSpace(value)))
        {
            count++;
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return count;
        }

        if (body.Contains("Final-Recipient:", StringComparison.OrdinalIgnoreCase))
        {
            count++;
        }

        if (body.Contains("Diagnostic-Code:", StringComparison.OrdinalIgnoreCase))
        {
            count++;
        }

        if (DsnStatusRegex.IsMatch(body))
        {
            count++;
        }

        if (body.Contains("Action: failed", StringComparison.OrdinalIgnoreCase))
        {
            count++;
        }

        return count;
    }

    private static bool HasDeliveryFailureSubject(string subject)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            return false;
        }

        return subject.Contains("undeliverable", StringComparison.OrdinalIgnoreCase) ||
               subject.Contains("delivery status notification", StringComparison.OrdinalIgnoreCase) ||
               subject.Contains("delivery has failed", StringComparison.OrdinalIgnoreCase) ||
               subject.Contains("delivery failure", StringComparison.OrdinalIgnoreCase) ||
               subject.Contains("message not delivered", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HeadersContainKnownSystemSender(IReadOnlyDictionary<string, List<string>> headers)
    {
        return HeaderValues(headers, "from")
            .Concat(HeaderValues(headers, "sender"))
            .Any(IsKnownSystemSender);
    }

    private static bool IsKnownSystemSender(string sender)
    {
        if (string.IsNullOrWhiteSpace(sender))
        {
            return false;
        }

        var address = NormalizeSenderAddress(sender);
        var atIndex = address.LastIndexOf('@');
        var localPart = atIndex >= 0 ? address[..atIndex] : address;
        var domain = atIndex >= 0 ? address[(atIndex + 1)..] : string.Empty;

        return localPart is "postmaster" or "mailer-daemon" or "mail-daemon" or "mdaemon" ||
               localPart.StartsWith("microsoftexchange", StringComparison.OrdinalIgnoreCase) ||
               IsTrustedMicrosoftProtectionDomain(domain);
    }

    private static bool IsTrustedMicrosoftProtectionDomain(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            return false;
        }

        return domain.Equals("protection.outlook.com", StringComparison.OrdinalIgnoreCase) ||
               domain.EndsWith(".protection.outlook.com", StringComparison.OrdinalIgnoreCase) ||
               domain.Equals("mail.protection.outlook.com", StringComparison.OrdinalIgnoreCase) ||
               domain.EndsWith(".mail.protection.outlook.com", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeSenderAddress(string sender)
    {
        var value = sender.Trim().ToLowerInvariant();
        var start = value.IndexOf('<');
        var end = value.IndexOf('>');
        if (start >= 0 && end > start)
        {
            value = value[(start + 1)..end].Trim();
        }

        return value.Trim('"', '\'');
    }

    private static IEnumerable<string> ExtractTrackingReferences(Message graphMessage)
    {
        var values = new[]
        {
            graphMessage.Subject ?? string.Empty,
            graphMessage.Body?.Content ?? string.Empty
        };

        return values
            .SelectMany(value => TrackingReferenceRegex.Matches(value).Select(match => match.Value.ToUpperInvariant()))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<string, List<string>> GetHeaders(Message graphMessage)
    {
        var headers = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in graphMessage.InternetMessageHeaders ?? [])
        {
            if (string.IsNullOrWhiteSpace(header.Name))
            {
                continue;
            }

            if (!headers.TryGetValue(header.Name, out var values))
            {
                values = [];
                headers[header.Name] = values;
            }

            values.Add(header.Value ?? string.Empty);
        }

        return headers;
    }

    private static IReadOnlyList<string> HeaderValues(IReadOnlyDictionary<string, List<string>> headers, string name) =>
        headers.TryGetValue(name, out var values) ? values : [];

    private async Task<Ticket?> FindTicketByInboundMessageIdAsync(string internetMessageId)
    {
        var marker = (await _timelineRepo.GetAllAsync())
            .Where(e =>
                e.EventType == TimelineEventType.CustomerReply &&
                string.Equals(e.CreatedByUserId, internetMessageId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.CreatedUtc)
            .FirstOrDefault();

        if (marker is null)
        {
            return null;
        }

        var ticket = await _ticketRepo.GetAsync(marker.TicketId);
        if (ticket is not null)
        {
            return ticket;
        }

        return await _incidentRepo.GetAsync(marker.TicketId);
    }

    private async Task FinalizeGraphMessageAsync(
        GraphServiceClient graphClient,
        string mailboxAddress,
        Message graphMessage,
        string processedFolderId,
        Ticket? ticket,
        CancellationToken token)
    {
        var update = new Message { IsRead = true };
        if (ticket is not null &&
            !string.IsNullOrWhiteSpace(ticket.TrackingId) &&
            !(graphMessage.Subject ?? string.Empty).Contains(ticket.TrackingId, StringComparison.OrdinalIgnoreCase))
        {
            update.Subject = $"[{ticket.TrackingId}] {graphMessage.Subject}";
        }

        await graphClient.Users[mailboxAddress].Messages[graphMessage.Id].PatchAsync(update, cancellationToken: token);
        await MoveGraphMessageAsync(graphClient, mailboxAddress, graphMessage.Id, processedFolderId);

        _logger.LogInformation("Patched and moved Graph Message ID {MessageId}", graphMessage.Id);
    }

    private static InboundEmailContext BuildInboundEmailContext(
        Message graphMessage,
        IEnumerable<Microsoft.Graph.Models.Attachment> attachments,
        string mailboxAddress,
        string internetMessageId)
    {
        var bodyContent = graphMessage.Body?.Content ?? string.Empty;
        var htmlBody = graphMessage.Body?.ContentType == BodyType.Html
            ? bodyContent
            : HtmlEncoder.Default.Encode(bodyContent);
        var textBody = graphMessage.Body?.ContentType == BodyType.Text
            ? bodyContent
            : string.Empty;

        return new InboundEmailContext(
            internetMessageId,
            graphMessage.Id,
            null,
            null,
            mailboxAddress,
            graphMessage.From?.EmailAddress?.Address ?? string.Empty,
            graphMessage.From?.EmailAddress?.Name,
            NormalizeEmails(graphMessage.ToRecipients?.Select(r => r.EmailAddress?.Address) ?? Enumerable.Empty<string>(), null),
            NormalizeEmails(graphMessage.CcRecipients?.Select(r => r.EmailAddress?.Address) ?? Enumerable.Empty<string>(), null),
            graphMessage.Subject ?? string.Empty,
            htmlBody,
            textBody,
            graphMessage.ReceivedDateTime,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            attachments.OfType<FileAttachment>()
                .Select(ToAttachmentContext)
                .ToList());
    }

    private async Task RecordInboundMessageMarkerAsync(
        string incidentId,
        string? internetMessageId,
        string? senderAddress,
        string? senderName)
    {
        if (string.IsNullOrWhiteSpace(internetMessageId))
        {
            return;
        }

        var alreadyRecorded = (await _timelineRepo.GetAllAsync())
            .Any(e =>
                e.TicketId == incidentId &&
                e.EventType == TimelineEventType.CustomerReply &&
                string.Equals(e.CreatedByUserId, internetMessageId, StringComparison.OrdinalIgnoreCase));

        if (alreadyRecorded)
        {
            return;
        }

        await _timelineRepo.CreateAsync(new TicketTimelineEvent
        {
            TicketId = incidentId,
            EventType = TimelineEventType.CustomerReply,
            CreatedByUserId = internetMessageId,
            CreatedByUserName = !string.IsNullOrWhiteSpace(senderAddress)
                ? senderAddress
                : (!string.IsNullOrWhiteSpace(senderName) ? senderName : "Inbound email"),
            MessageText = "Inbound email received."
        });
    }

    private async Task TryCreateEmailEmbeddingsAsync(string orgId, string sourceId, string text, CancellationToken token)
    {
        try
        {
            var settings = (await _aiKbRepo.GetAllAsync())
                .FirstOrDefault(s => s.OrganizationId == orgId);
            if (settings is null || (!settings.EnableAiSearch && !settings.EnableAiAnswers))
            {
                return;
            }

            var chunks = TextChunker.Split(text).ToList();
            if (chunks.Count == 0)
            {
                return;
            }

            var inputs = chunks.Select(c => c.Text).ToList();
            var vectors = await _embeddingService.CreateEmbeddingsAsync(orgId, inputs, token);

            if (vectors.Count != inputs.Count)
            {
                _logger.LogWarning(
                    "Embedding count mismatch for source {SourceId}: inputs={Inputs}, vectors={Vectors}. Falling back to single vector.",
                    sourceId, inputs.Count, vectors.Count);

                var combined = string.Join("\n\n", inputs);
                var single = await _embeddingService.CreateEmbeddingAsync(orgId, combined, token);

                await _embeddingRepo.CreateAsync(new KnowledgeEmbedding
                {
                    OrganizationId = orgId,
                    SourceType = "Email",
                    SourceId = sourceId,
                    DocumentTitle = "Email Message",
                    ChunkIndex = 0,
                    ChunkId = "0",
                    Text = combined,
                    MetadataJson = "{\"source\":\"email\"}",
                    Vector = new Vector(single),
                    CreatedAt = DateTime.UtcNow
                });

                return;
            }

            for (var i = 0; i < Math.Min(inputs.Count, vectors.Count); i++)
            {
                await _embeddingRepo.CreateAsync(new KnowledgeEmbedding
                {
                    OrganizationId = orgId,
                    SourceType = "Email",
                    SourceId = sourceId,
                    DocumentTitle = "Email Message",
                    ChunkIndex = i,
                    ChunkId = chunks[i].ChunkId,
                    Text = inputs[i],
                    MetadataJson = "{\"source\":\"email\"}",
                    Vector = new Vector(vectors[i]),
                    CreatedAt = DateTime.UtcNow
                });
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Email embedding generation failed for source {SourceId}. Inbound email processing will continue.",
                sourceId);
        }
    }

    private async Task<string?> GetOrCreateMailFolderIdAsync(GraphServiceClient graphClient, string mailbox, string folderName, CancellationToken token)
    {
        if (string.IsNullOrEmpty(folderName)) return null;

        try
        {
            var filter = $"displayName eq '{folderName}'";
            var folders = await graphClient.Users[mailbox].MailFolders.GetAsync(req => { req.QueryParameters.Filter = filter; }, token);
            var existingFolder = folders?.Value?.FirstOrDefault();
            if (existingFolder != null) return existingFolder.Id;

            _logger.LogInformation("Folder '{FolderName}' not found, creating it with Graph API.", folderName);
            var newFolder = new MailFolder { DisplayName = folderName };
            var createdFolder = await graphClient.Users[mailbox].MailFolders.PostAsync(newFolder, cancellationToken: token);
            return createdFolder?.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get or create mail folder '{FolderName}'.", folderName);
            return null;
        }
    }

    private async Task<bool> SendNotificationAsync(
        string templateName,
        string to,
        IEnumerable<string>? cc = null,
        string? userName = null,
        string? ticketRef = null,
        string? ticketLink = null,
        string? tenantId = null)
    {
        var template = (await _templateRepo.GetAllAsync()).FirstOrDefault(t => t.Name == templateName);
        if (template is null)
        {
            _logger.LogWarning("Email template '{TemplateName}' not found. Cannot send notification.", templateName);
            return false;
        }

        var safeUser = userName ?? string.Empty;
        var safeRef = ticketRef ?? string.Empty;
        var safeLink = ticketLink ?? string.Empty;
        var layout = await _layoutResolver.ResolveAsync(template.LayoutId);
        var branding = await _tenantBrandingResolver.ResolveAsync(tenantId);

        var subject = (template.Subject ?? string.Empty).Replace("{{{TICKET_REF}}}", safeRef);
        var body = _templateRenderer.Render(template.HtmlContent ?? string.Empty, new EmailTemplateContext
        {
            UserName = safeUser,
            TicketRef = safeRef,
            TicketLink = safeLink,
            LayoutHtml = layout?.HtmlContent ?? string.Empty,
            BrandName = branding.BrandName,
            LogoHtml = branding.LogoHtml,
            FooterHtml = branding.FooterHtml,
            PrimaryColor = branding.PrimaryColor
        });

        var sent = await _emailService.SendEmailAsync(
            new[] { to },
            subject,
            body,
            cc,
            fromName: branding.FromName,
            replyTo: branding.ReplyTo);
        if (sent)
        {
            _logger.LogInformation("Sent '{TemplateName}' notification to {Recipient}", templateName, to);
        }

        await _domainEvents.PublishAsync(
            new EmailSentEvent(
                Recipient: to,
                TemplateName: templateName,
                TenantId: tenantId,
                Reference: ticketRef,
                CorrelationId: GetCorrelationId(),
                Success: sent,
                Details: sent ? "Email sent successfully." : "Email send returned false."),
            CancellationToken.None);

        return sent;
    }

    private string GetCorrelationId()
    {
        return _correlationContext?.GetCorrelationId() ?? $"corr-{Guid.NewGuid():N}";
    }

    private async Task MoveGraphMessageAsync(GraphServiceClient graphClient, string mailbox, string graphMessageId, string destinationFolderId)
    {
        if (string.IsNullOrEmpty(destinationFolderId)) return;
        try
        {
            await graphClient.Users[mailbox].Messages[graphMessageId].Move
                .PostAsync(new Microsoft.Graph.Users.Item.Messages.Item.Move.MovePostRequestBody
                {
                    DestinationId = destinationFolderId
                });
        }
        catch (ODataError odataError) when (odataError.Error?.Code?.Equals("ErrorItemNotFound", StringComparison.OrdinalIgnoreCase) == true)
        {
            _logger.LogWarning("Destination folder id '{Folder}' not found in mailbox.", destinationFolderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to move Graph message {MessageId} to folder id {Folder}", graphMessageId, destinationFolderId);
        }
    }

    private static List<string> NormalizeEmails(IEnumerable<string?> emails, string? requester)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in emails)
        {
            var e = (raw ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(e)) continue;
            set.Add(e);
        }
        if (!string.IsNullOrWhiteSpace(requester))
        {
            set.Remove(requester.Trim().ToLowerInvariant());
        }
        return set.ToList();
    }

    private static InboundEmailAttachmentContext ToAttachmentContext(FileAttachment attachment) => new(
        attachment.Name ?? "attachment",
        attachment.ContentType ?? "application/octet-stream",
        attachment.ContentId,
        attachment.ContentBytes,
        attachment.Id,
        attachment.IsInline);
}
