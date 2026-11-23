using FluentValidation;
using Mentora.Core.DTOs.AdminCoach;
using Mentora.Core.Validators.Parameters;

namespace Mentora.Core.Validators.AdminCoach;

/// <summary>
/// Lot 14.8a (14.G4). Rules copied from AddMemberRequestValidator: they run on the trimmed values and the
/// email is checked after normalization. The phone pattern (8 to 20 characters) already caps the length at
/// the 20 of COACH_PHONE, so no separate length rule is needed.
/// </summary>
public class AdminCreateCoachRequestValidator : AbstractValidator<AdminCreateCoachRequest>
{
    public const int MaxEmailLength = 255;
    public const int MaxNameLength = 100;

    public AdminCreateCoachRequestValidator()
    {
        RuleFor(x => x.Email)
            .Cascade(CascadeMode.Stop)
            .Must(e => !string.IsNullOrWhiteSpace(e)).WithMessage("'Email' must not be empty.")
            .Must(e => EmailNormalizer.Normalize(e).Length <= MaxEmailLength)
                .WithMessage($"'Email' must be {MaxEmailLength} characters or fewer.")
            .Must(e => IsValidAddress(EmailNormalizer.Normalize(e)))
                .WithMessage("'Email' is not a valid email address.");

        RuleFor(x => x.FirstName)
            .Cascade(CascadeMode.Stop)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("'First Name' must not be empty.")
            .Must(n => n!.Trim().Length <= MaxNameLength)
                .WithMessage($"'First Name' must be {MaxNameLength} characters or fewer.");

        RuleFor(x => x.LastName)
            .Cascade(CascadeMode.Stop)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("'Last Name' must not be empty.")
            .Must(n => n!.Trim().Length <= MaxNameLength)
                .WithMessage($"'Last Name' must be {MaxNameLength} characters or fewer.");

        RuleFor(x => x.Phone)
            .Matches(UpdateMemberParameterRequestValidator.PhonePattern)
            .When(x => !string.IsNullOrWhiteSpace(x.Phone))
            .WithMessage(UpdateMemberParameterRequestValidator.PhoneMessage);
    }

    /// <summary>One "@", non-empty local part, a dotted domain, no whitespace or control character anywhere.</summary>
    private static bool IsValidAddress(string email)
    {
        if (email.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || c is ',' or ';' or '<' or '>' or '"'))
            return false;
        var at = email.IndexOf('@');
        if (at < 1 || at != email.LastIndexOf('@') || at == email.Length - 1)
            return false;
        var domain = email[(at + 1)..];
        return domain.Contains('.')
            && !domain.StartsWith('.') && !domain.EndsWith('.') && !domain.Contains("..");
    }
}
