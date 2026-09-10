using FluentValidation;
using Helpdesk.Shared.DTOs.AI;

namespace Helpdesk.API.Validators;

public class AiChatRequestDtoValidator : AbstractValidator<AiChatRequestDto>
{
    public AiChatRequestDtoValidator()
    {
        RuleFor(x => x.Prompt).NotEmpty();
    }
}

