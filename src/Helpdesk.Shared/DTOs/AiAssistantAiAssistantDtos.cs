using Helpdesk.Shared.Models;

namespace Helpdesk.Shared.DTOs;

public sealed record AiAssistantWebhookConfigurationDto(Guid Id, string Name, bool IsEnabled, bool IsArchived, List<AiAssistantTicketArea> TicketAreas, string Endpoint, string? RouteName, string? PromptTemplate, string? PermittedInputsSchemaJson, int TimeoutSeconds, int MaxBodyBytes, bool HasSigningSecret, DateTimeOffset? LastDispatchUtc, string? LastHealthMessage);
public sealed record UpsertAiAssistantWebhookConfigurationDto(string OrganizationId, string Name, List<AiAssistantTicketArea> TicketAreas, string Endpoint, string? RouteName, string? PromptTemplate, string? PermittedInputsSchemaJson, int TimeoutSeconds = 30, int MaxBodyBytes = 65536, string? SigningSecret = null, bool IsEnabled = true);
public sealed record DispatchAiInvestigationDto(Guid ConfigurationId, string? OperatorAssistanceRequest, string IdempotencyKey);
public sealed record AiInvestigationInvocationDto(Guid Id, AiInvestigationStatus Status, string CorrelationId, string? AiAssistantRunReference, string? FailureMessage, DateTimeOffset CreatedUtc);
public sealed record AiInvestigationWorklogEntryDto(Guid Id, Guid InvocationId, AiInvestigationSource Source, AiInvestigationStatus Status, string? Severity, string Message, string? MetadataJson, string? ArtifactReferencesJson, string CorrelationId, DateTimeOffset OccurredUtc);
public sealed record AppendAiInvestigationWorklogDto(string EventId, string TicketId, string TicketType, string CorrelationId, AiInvestigationStatus Status, string? Severity, string Message, string? MetadataJson, string? ArtifactReferencesJson, string? RunReference, DateTimeOffset OccurredUtc);
public sealed record AiAssistantWebhookStateDto(string OrganizationId, bool Enabled, bool Archived);
