using FluentValidation;
using Helpdesk.Shared.DTOs.AI;

namespace Helpdesk.API.Validators;

public class AiProviderUpdateDtoValidator : AbstractValidator<AiProviderUpdateDto>
{
    public AiProviderUpdateDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.BaseUrl).NotEmpty();
    }
}

