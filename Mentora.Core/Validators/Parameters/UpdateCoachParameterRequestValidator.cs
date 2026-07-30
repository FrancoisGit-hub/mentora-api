using FluentValidation;
using Mentora.Core.DTOs.Lot2;

namespace Mentora.Core.Validators.Parameters;

public class UpdateCoachParameterRequestValidator : AbstractValidator<UpdateCoachParameterRequest>
{
    // Only "FR" is supported today. Add more ISO codes here to open up new languages.
    private static readonly string[] AllowedLanguages = ["FR"];

    public UpdateCoachParameterRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Phone)
            .Matches(@"^[0-9+\-.() ]{8,20}$")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone))
            .WithMessage("Phone must be 8-20 characters using digits, spaces, +, -, ., or parentheses only.");

        RuleFor(x => x.HourlyRateEuros)
            .GreaterThan(0)
            .LessThanOrEqualTo(10000);

        RuleFor(x => x.CancellationDelayHours)
            .InclusiveBetween(0, 168);

        RuleFor(x => x.MinBookingNoticeHours)
            .InclusiveBetween(0, 168);

        RuleFor(x => x.MaxBookingHorizonDays)
            .InclusiveBetween(1, 365);

        RuleFor(x => x.DefaultSessionDurationMinutes)
            .InclusiveBetween(15, 480);

        RuleFor(x => x.PresentialAddress)
            .MaximumLength(500)
            .When(x => x.PresentialAddress is not null);

        RuleFor(x => x.CustomVisioUrl)
            .Must(BeAbsoluteHttpsUrl)
            .When(x => x.CustomVisioUrl is not null)
            .WithMessage("CustomVisioUrl must be an absolute https:// URL.");

        RuleFor(x => x.Language)
            .Must(v => AllowedLanguages.Contains(v))
            .WithMessage($"Language must be one of: {string.Join(", ", AllowedLanguages)}");
    }

    private static bool BeAbsoluteHttpsUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        return Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
    }
}
