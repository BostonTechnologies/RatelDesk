using FluentValidation;
using Helpdesk.Shared.DTOs.AI;

namespace Helpdesk.API.Validators;

public class AiProviderCreateDtoValidator : AbstractValidator<AiProviderCreateDto>
{
    public AiProviderCreateDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.BaseUrl).NotEmpty();
        RuleFor(x => x.ApiKey).NotEmpty();
    }
}

