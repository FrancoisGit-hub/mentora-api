using FluentValidation;
using Mentora.Core.DTOs.Coach;

namespace Mentora.Core.Validators.Coach;

/// <summary>Lot 14.1 (14.A2). Rules run on the trimmed values; the email is checked after normalization.</summary>
public class AddMemberRequestValidator : AbstractValidator<AddMemberRequest>
{
    public const int MaxEmailLength = 255;
    public const int MaxNameLength = 100;

    public AddMemberRequestValidator()
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
