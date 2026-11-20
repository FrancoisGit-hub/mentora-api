using FluentValidation;
using Mentora.Core.DTOs.Member;

namespace Mentora.Core.Validators.Parameters;

public class UpdateMemberParameterRequestValidator : AbstractValidator<UpdateMemberParameterRequest>
{
    // Only "FR" is supported today. Add more ISO codes here to open up new languages.
    private static readonly string[] AllowedLanguages = ["FR"];
    private static readonly int[] AllowedReminderHours = [1, 2, 24, 48];

    // Lot 14.1b: shared with AddMemberRequestValidator so both endpoints apply the same rules and messages.
    public const string PhonePattern = @"^[0-9+\-.() ]{8,20}$";
    public const string PhoneMessage = "Phone must be 8-20 characters using digits, spaces, +, -, ., or parentheses only.";
    public const string BirthDateMessage = "BirthDate must be in the past and imply an age between 13 and 120.";

    public UpdateMemberParameterRequestValidator()
    {
        RuleFor(x => x.Phone)
            .Matches(PhonePattern)
            .When(x => !string.IsNullOrWhiteSpace(x.Phone))
            .WithMessage(PhoneMessage);

        RuleFor(x => x.HeightCm)
            .InclusiveBetween((short)50, (short)280)
            .When(x => x.HeightCm.HasValue);

        RuleFor(x => x.BirthDate)
            .Must(BeAPlausibleBirthDate)
            .When(x => x.BirthDate.HasValue)
            .WithMessage(BirthDateMessage);

        RuleFor(x => x.Language)
            .Must(v => AllowedLanguages.Contains(v, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Language must be one of: {string.Join(", ", AllowedLanguages)}");

        RuleFor(x => x.SessionReminderHoursBefore)
            .Must(v => AllowedReminderHours.Contains(v))
            .WithMessage($"SessionReminderHoursBefore must be one of: {string.Join(", ", AllowedReminderHours)}");
    }

    public static bool BeAPlausibleBirthDate(DateOnly? birthDate)
    {
        if (birthDate is not { } value) return true;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (value >= today) return false;

        var age = today.Year - value.Year;
        if (value > today.AddYears(-age)) age--;

        return age is >= 13 and <= 120;
    }
}
