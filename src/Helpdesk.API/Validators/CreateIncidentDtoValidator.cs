using FluentValidation;
using Helpdesk.Shared.DTOs.Incident;

namespace Helpdesk.API.Validators;

public class CreateIncidentDtoValidator : AbstractValidator<CreateIncidentDto>
{
    public CreateIncidentDtoValidator()
    {
        RuleFor(x => x.RequesterEmail)
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.RequesterEmail));

        RuleForEach(x => x.CcRecipients!).EmailAddress().When(x => x.CcRecipients != null);

        RuleFor(x => x.CcRecipients)
            .Must(list => list == null || list.Count <= 20)
            .WithMessage("Maximum 20 CC recipients allowed.");
    }
}
