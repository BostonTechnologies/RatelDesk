using FluentValidation;
using Helpdesk.Shared.DTOs.Organization;

namespace Helpdesk.API.Validators;

public class OrganizationAiKbSettingsDtoValidator : AbstractValidator<OrganizationAiKbSettingsDto>
{
    public OrganizationAiKbSettingsDtoValidator()
    {
        RuleFor(x => x.SearchThreshold).InclusiveBetween(0, 1);
        RuleFor(x => x.AnswerThreshold).InclusiveBetween(0, 1);
        RuleFor(x => x.MaxProviderAttempts).InclusiveBetween(1, 5);
        RuleFor(x => x.MinimumSuggestionHelpfulRate).InclusiveBetween(0, 1);
        RuleFor(x => x.MinimumAutomationResolvedRate).InclusiveBetween(0, 1);
        RuleFor(x => x.MinimumSuggestionFeedbackCount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MinimumAutomationFeedbackCount).GreaterThanOrEqualTo(0);
    }
}
