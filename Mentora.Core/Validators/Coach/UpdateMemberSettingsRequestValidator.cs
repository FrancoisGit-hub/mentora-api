using FluentValidation;
using Mentora.Core.DTOs.Coach;

namespace Mentora.Core.Validators.Coach;

public class UpdateMemberSettingsRequestValidator : AbstractValidator<UpdateMemberSettingsRequest>
{
    public UpdateMemberSettingsRequestValidator()
    {
        RuleFor(x => x.PresentialAddress)
            .MaximumLength(500)
            .When(x => x.PresentialAddress is not null);
    }
}
