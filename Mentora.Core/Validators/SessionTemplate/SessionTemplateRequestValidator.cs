using FluentValidation;
using Mentora.Core.DTOs.SessionTemplate;
using Mentora.Core.Enums;
using Mentora.Core.Interfaces;
using Mentora.Core.Validators.ProgramTemplate;

namespace Mentora.Core.Validators.SessionTemplate;

/// <summary>
/// Validates the body of POST / PUT of a session template. The circuit and exercise rules are the program
/// tree's (<see cref="ProgramCircuitRules"/>), so the 422 keys point into the template body with the
/// 1-based Position of each node, e.g. <c>Circuits[1].Exercises[2].ExerciseId</c>.
/// </summary>
/// <remarks>
/// Requires <c>ValidationContext.RootContextData["CoachId"]</c> (Guid), set by the service: fail-fast when
/// the body references exercises, never a silent skip of the exercise-visibility check.
/// </remarks>
public class SessionTemplateRequestValidator : AbstractValidator<SessionTemplateRequest>
{
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 1000;
    public const string UnknownExerciseMessage = "Unknown or inaccessible exercise id.";

    private readonly IExerciseService exerciseService;

    public SessionTemplateRequestValidator(IExerciseService exerciseService)
    {
        this.exerciseService = exerciseService;

        RuleFor(x => x.Name)
            .Must(v => v is not null && v.Trim().Length is >= 1 and <= NameMaxLength)
            .WithMessage($"Name is required and must be at most {NameMaxLength} characters.");

        RuleFor(x => x.Description)
            .Must(v => v is null || v.Trim().Length <= DescriptionMaxLength)
            .WithMessage($"Description must be at most {DescriptionMaxLength} characters.");

        RuleFor(x => x.Nature)
            .Must(v => v is not null && ProgramSessionNatures.All.Contains(v))
            .WithMessage($"Nature must be one of: {string.Join(", ", ProgramSessionNatures.All)}.");

        RuleFor(x => x.SessionType)
            .Must(v => v is not null && ProgramSessionTypes.All.Contains(v))
            .WithMessage($"SessionType must be one of: {string.Join(", ", ProgramSessionTypes.All)}.");

        RuleFor(x => x).CustomAsync(ValidateCircuitsAsync);
    }

    private async Task ValidateCircuitsAsync(
        SessionTemplateRequest request, ValidationContext<SessionTemplateRequest> context, CancellationToken ct)
    {
        if (request.Circuits is not { Count: > 0 })
        {
            context.AddFailure("Circuits", "At least one circuit is required.");
            return;
        }

        var exerciseRefs = new List<(string Path, Guid ExerciseId)>();

        ProgramCircuitRules.ValidateCircuits(
            request.Circuits, "Circuits",
            (key, message) => context.AddFailure(key, message),
            (path, exerciseId) => exerciseRefs.Add((path, exerciseId)));

        if (exerciseRefs.Count == 0)
            return;

        if (!context.RootContextData.TryGetValue("CoachId", out var coachIdValue) || coachIdValue is not Guid coachId)
            throw new InvalidOperationException(
                "SessionTemplateRequestValidator requires ValidationContext.RootContextData[\"CoachId\"] " +
                "to be set to a Guid before validation runs — refusing to skip the exercise-visibility check.");

        // One query for every referenced exercise; unknown, inactive and another coach's exercise
        // get the same message on their own path, so nothing leaks about their existence.
        var visibleIds = await exerciseService.ResolveVisibleActiveIdsAsync(
            exerciseRefs.Select(r => r.ExerciseId).Distinct().ToList(), coachId, ct);

        foreach (var (path, exerciseId) in exerciseRefs)
            if (!visibleIds.Contains(exerciseId))
                context.AddFailure($"{path}.ExerciseId", UnknownExerciseMessage);
    }
}
