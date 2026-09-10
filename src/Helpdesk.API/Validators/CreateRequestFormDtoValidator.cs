using FluentValidation;
using Helpdesk.Shared.DTOs.RequestForm;

namespace Helpdesk.API.Validators;

public class CreateRequestFormDtoValidator : AbstractValidator<CreateRequestFormDto>
{
    public CreateRequestFormDtoValidator()
    {
        RuleFor(x => x.ServiceId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty();
        RuleFor(x => x.JsonSchema).NotEmpty();
    }
}
