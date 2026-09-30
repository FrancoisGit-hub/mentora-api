using System.Text.RegularExpressions;
using FluentValidation;
using Mentora.Core.DTOs.Quiz;

namespace Mentora.Core.Validators.Quiz;

public static class QuizRules
{
    /// <summary>16 to 64 characters, ASCII letters, digits and hyphen only.</summary>
    public static readonly Regex DeviceId = new("^[A-Za-z0-9-]{16,64}$", RegexOptions.Compiled);

    public static readonly string[] AnswerKeys = ["A", "B", "C", "D"];

    /// <summary>Key of RootContextData carrying the configured question count (Core has no options package).</summary>
    public const string QuestionCountKey = "QuestionCount";

    public static bool IsValidDeviceId(string? value) => value is not null && DeviceId.IsMatch(value);
}

/// <summary>Device id of a request in device mode (coach mode ignores it).</summary>
public class QuizOwnerValidator : AbstractValidator<QuizOwner>
{
    public QuizOwnerValidator()
    {
        RuleFor(x => x.DeviceId)
            .Must(QuizRules.IsValidDeviceId)
            .WithMessage("X-Device-Id is required in device mode: 16 to 64 characters, ASCII letters, digits and hyphen only.")
            .When(x => !x.IsCoachMode);
    }
}

public class QuizClaimDeviceRequestValidator : AbstractValidator<QuizClaimDeviceRequest>
{
    public QuizClaimDeviceRequestValidator()
    {
        RuleFor(x => x.DeviceId)
            .Must(QuizRules.IsValidDeviceId)
            .WithMessage("DeviceId must be 16 to 64 characters, ASCII letters, digits and hyphen only.");
    }
}

/// <summary>
/// Structure of a submission. The expected item count is the configured question count, passed
/// through <c>RootContextData[QuizRules.QuestionCountKey]</c>.
/// </summary>
public class QuizSubmitRequestValidator : AbstractValidator<QuizSubmitRequest>
{
    public QuizSubmitRequestValidator()
    {
        RuleFor(x => x.Answers)
            .NotNull().WithMessage("Answers is required.")
            .Custom((answers, ctx) =>
            {
                if (answers is null) return;

                if (ctx.RootContextData.TryGetValue(QuizRules.QuestionCountKey, out var raw)
                    && raw is int expected && answers.Count != expected)
                    ctx.AddFailure($"Exactly {expected} answers are required (got {answers.Count}).");

                if (answers.Select(a => a?.QuestionId).Distinct().Count() != answers.Count)
                    ctx.AddFailure("Each questionId must appear only once.");

                if (answers.Any(a => a is null || Array.IndexOf(QuizRules.AnswerKeys, a.ChosenAnswer) < 0))
                    ctx.AddFailure("chosenAnswer must be one of A, B, C, D (uppercase).");
            });
    }
}
