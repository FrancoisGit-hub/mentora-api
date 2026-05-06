using FluentValidation;
using Mentora.Core.DTOs.Session;

namespace Mentora.Core.Validators.Session;

public class CancelSessionRequestValidator : AbstractValidator<CancelSessionRequest>
{
    public CancelSessionRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty()
            .MaximumLength(500);
    }
}
