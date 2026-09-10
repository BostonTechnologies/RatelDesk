using Helpdesk.Application.Events;

namespace Helpdesk.Application.AiAssistant.Chat;

public sealed record ChatDomainEvent(string Action, string OrganizationId, string ConversationId, string Correlation)
    : DomainEvent($"DomainEvent.AiAssistant.Chat.{Action}", "AiAssistantChat", OrganizationId, ConversationId, Correlation, ConversationId, DateTime.UtcNow);
