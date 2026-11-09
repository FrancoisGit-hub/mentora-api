using FluentValidation;
using Mentora.Core.DTOs.Admin.Auth;

namespace Mentora.Core.Validators.Admin;

/// <summary>Lot 12.1 — called inside AdminAuthService (422 through GlobalExceptionMiddleware).</summary>
public class AdminRequestCodeRequestValidator : AbstractValidator<AdminRequestCodeRequest>
{
    public AdminRequestCodeRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(255)
            .EmailAddress();
    }
}

/// <summary>Lot 12.1 — called inside AdminAuthService (422 through GlobalExceptionMiddleware).</summary>
public class AdminVerifyCodeRequestValidator : AbstractValidator<AdminVerifyCodeRequest>
{
    public AdminVerifyCodeRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(255)
            .EmailAddress();

        RuleFor(x => x.Code)
            .NotEmpty()
            .Matches("^[0-9]{6}$")
            .WithMessage("Code must be exactly 6 digits.");
    }
}
