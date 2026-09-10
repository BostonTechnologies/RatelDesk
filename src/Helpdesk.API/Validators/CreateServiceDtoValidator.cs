using FluentValidation;
using Helpdesk.Shared.DTOs.Service;

namespace Helpdesk.API.Validators;

public class CreateServiceDtoValidator : AbstractValidator<CreateServiceDto>
{
    public CreateServiceDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
    }
}
