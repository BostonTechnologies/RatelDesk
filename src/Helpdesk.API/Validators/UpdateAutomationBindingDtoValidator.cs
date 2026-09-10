using FluentValidation;
using Helpdesk.Shared.DTOs.Orchestration;

namespace Helpdesk.API.Validators;

public sealed class UpdateAutomationBindingDtoValidator : AbstractValidator<UpdateAutomationBindingDto>
{
    public UpdateAutomationBindingDtoValidator()
    {
        RuleFor(x => x.OrchestrationRequestDefinitionId)
            .Must(x => x is null || !string.IsNullOrWhiteSpace(x))
            .WithMessage("OrchestrationRequestDefinitionId cannot be empty when provided.");

        RuleFor(x => x.LastSyncDirection)
            .Must(x => x is null || !string.IsNullOrWhiteSpace(x))
            .WithMessage("LastSyncDirection cannot be empty when provided.");
    }
}
