using FluentValidation;
using Mentora.Core.DTOs.Member;

namespace Mentora.Core.Validators.Parameters;

public class UpdateMemberParameterRequestValidator : AbstractValidator<UpdateMemberParameterRequest>
{
    // Only "FR" is supported today. Add more ISO codes here to open up new languages.
    private static readonly string[] AllowedLanguages = ["FR"];
    private static readonly int[] AllowedReminderHours = [1, 2, 24, 48];

    public UpdateMemberParameterRequestValidator()
    {
        RuleFor(x => x.Phone)
            .Matches(@"^[0-9+\-.() ]{8,20}$")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone))
            .WithMessage("Phone must be 8-20 characters using digits, spaces, +, -, ., or parentheses only.");

        RuleFor(x => x.HeightCm)
            .InclusiveBetween((short)50, (short)280)
            .When(x => x.HeightCm.HasValue);

        RuleFor(x => x.BirthDate)
            .Must(BeAPlausibleBirthDate)
            .When(x => x.BirthDate.HasValue)
            .WithMessage("BirthDate must be in the past and imply an age between 13 and 120.");

        RuleFor(x => x.Language)
            .Must(v => AllowedLanguages.Contains(v, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Language must be one of: {string.Join(", ", AllowedLanguages)}");

        RuleFor(x => x.SessionReminderHoursBefore)
            .Must(v => AllowedReminderHours.Contains(v))
            .WithMessage($"SessionReminderHoursBefore must be one of: {string.Join(", ", AllowedReminderHours)}");
    }

    private static bool BeAPlausibleBirthDate(DateOnly? birthDate)
    {
        if (birthDate is not { } value) return true;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (value >= today) return false;

        var age = today.Year - value.Year;
        if (value > today.AddYears(-age)) age--;

        return age is >= 13 and <= 120;
    }
}
