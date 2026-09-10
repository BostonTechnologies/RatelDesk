using FluentValidation;
using Helpdesk.Shared.DTOs.Orchestration;

namespace Helpdesk.API.Validators;

public sealed class CreateAutomationBindingDtoValidator : AbstractValidator<CreateAutomationBindingDto>
{
    public CreateAutomationBindingDtoValidator()
    {
        RuleFor(x => x.RequestFormId).NotEmpty();
        RuleFor(x => x.TaskTemplateId).NotEmpty();
        RuleFor(x => x.OrchestrationRequestDefinitionId).NotEmpty();
    }
}
