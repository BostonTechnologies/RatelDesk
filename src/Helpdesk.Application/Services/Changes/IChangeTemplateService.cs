using Helpdesk.Shared.DTOs.Change;

namespace Helpdesk.Application.Services.Changes;

/// <summary>Validates and serializes the deterministic ITIL change template.</summary>
public interface IChangeTemplateService
{
    string NormalizeChangeType(string? changeType);
    ChangeTemplateDto? DeserializeTemplate(string? templateJson);
    string SerializeTemplate(ChangeTemplateDto? template);
    ChangeTemplateValidationDto ValidateTemplate(string? changeType, ChangeTemplateDto? template);
}
