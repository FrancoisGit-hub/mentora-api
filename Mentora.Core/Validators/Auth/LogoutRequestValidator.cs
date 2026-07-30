using FluentValidation;
using Mentora.Core.DTOs.Auth;

namespace Mentora.Core.Validators.Auth;

public class LogoutRequestValidator : AbstractValidator<LogoutRequest>
{
    public LogoutRequestValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty();
    }
}
