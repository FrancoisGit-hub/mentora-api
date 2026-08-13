using FluentValidation;
using Mentora.Core.DTOs.Program;
using Mentora.Core.Enums;

namespace Mentora.Core.Validators.Program;

/// <summary>
/// Shape-level checks only (status membership, RPE range). DB-dependent rules — ProgramExerciseId
/// membership in the session, and actualWeightKg vs LoadType — need the session's own exercises
/// loaded, so they run inside ProgramService.UpdateCompletionAsync instead, raising the same
/// FluentValidation.ValidationException / 422 shape by hand.
/// </summary>
public class UpdateProgramSessionCompletionRequestValidator : AbstractValidator<UpdateProgramSessionCompletionRequest>
{
    public UpdateProgramSessionCompletionRequestValidator()
    {
        RuleFor(x => x.Status)
            .Must(v => EnumMappings.ProgramSessionStatusMapping.WireValues.Contains(v))
            .WithMessage($"Status must be one of: {string.Join(", ", EnumMappings.ProgramSessionStatusMapping.WireValues)}");

        RuleForEach(x => x.Exercises).ChildRules(exercise =>
        {
            exercise.RuleFor(e => e.ActualRpe)
                .InclusiveBetween(1, 10)
                .When(e => e.ActualRpe.HasValue)
                .WithMessage("actualRpe must be between 1 and 10.");
        });
    }
}
