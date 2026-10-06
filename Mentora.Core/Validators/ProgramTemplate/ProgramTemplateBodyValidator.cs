using FluentValidation;
using Mentora.Core.DTOs.ProgramTemplate.Body;
using Mentora.Core.Enums;
using Mentora.Core.Interfaces;

namespace Mentora.Core.Validators.ProgramTemplate;

/// <summary>
/// Validates a recursive program-template block tree. Reused as-is by Lot 6.3's program
/// assignment (POST body without a templateId) — do not duplicate these rules elsewhere.
/// </summary>
/// <remarks>
/// Requires two entries in <see cref="ValidationContext{T}.RootContextData"/>, set by the caller
/// before validation runs:
///   - "DurationWeeks" (int)  — always required.
///   - "CoachId" (Guid)       — required only when the tree references at least one exercise.
/// Both are guarded with a fail-fast throw rather than a silent skip, for the same reason the
/// exercise-visibility check itself must never be silently skipped.
/// </remarks>
public class ProgramTemplateBodyValidator : AbstractValidator<ProgramTemplateBody>
{
    // MACROCYCLE > MESOCYCLE > MICROCYCLE — a level may be skipped, never inverted or repeated.
    private static readonly Dictionary<string, int> LevelRank = new()
    {
        ["MACROCYCLE"] = 1,
        ["MESOCYCLE"] = 2,
        ["MICROCYCLE"] = 3,
    };

    // Exact decimal comparison. Allowed only on MESOCYCLE blocks.
    private static readonly decimal[] ValidLoadPercents = [100m, 102.5m, 105m, 110m];

    private readonly IExerciseService exerciseService;

    public ProgramTemplateBodyValidator(IExerciseService exerciseService)
    {
        this.exerciseService = exerciseService;

        RuleFor(x => x).CustomAsync(ValidateAsync);
    }

    private async Task ValidateAsync(
        ProgramTemplateBody body, ValidationContext<ProgramTemplateBody> context, CancellationToken ct)
    {
        if (body?.Blocks is not { Count: > 0 })
        {
            context.AddFailure("Blocks", "At least one block is required.");
            return;
        }

        if (!context.RootContextData.TryGetValue("DurationWeeks", out var durationWeeksValue)
            || durationWeeksValue is not int durationWeeks)
            throw new InvalidOperationException(
                "ProgramTemplateBodyValidator requires ValidationContext.RootContextData[\"DurationWeeks\"] " +
                "to be set to an int before validation runs.");

        var weekNumbers = new List<int>();
        var exerciseIds = new HashSet<Guid>();

        ValidateBlockList(body.Blocks, parentRank: 0, depth: 1, "Blocks", context, weekNumbers, exerciseIds);
        ValidateWeekNumbers(weekNumbers, durationWeeks, context);

        if (exerciseIds.Count == 0)
            return;

        // Fail fast, never silently skip: a missing/malformed CoachId must not be interpreted
        // as "no exercises to check" — that would let a coach reference another coach's private
        // exercises undetected.
        if (!context.RootContextData.TryGetValue("CoachId", out var coachIdValue) || coachIdValue is not Guid coachId)
            throw new InvalidOperationException(
                "ProgramTemplateBodyValidator requires ValidationContext.RootContextData[\"CoachId\"] " +
                "to be set to a Guid before validation runs — refusing to skip the exercise-visibility check.");

        var visibleIds = await exerciseService.ResolveVisibleActiveIdsAsync(exerciseIds, coachId, ct);
        var missing = exerciseIds.Where(id => !visibleIds.Contains(id)).ToList();

        if (missing.Count > 0)
            context.AddFailure(string.Empty, $"Unknown or inaccessible exercise id(s): {string.Join(", ", missing)}.");
    }

    private void ValidateBlockList(
        List<ProgramTemplateBlock> blocks, int parentRank, int depth, string path,
        ValidationContext<ProgramTemplateBody> context, List<int> weekNumbers, HashSet<Guid> exerciseIds)
    {
        if (blocks is not { Count: > 0 })
            return;

        if (depth > 3)
        {
            context.AddFailure(path, "Block tree depth cannot exceed 3.");
            return;
        }

        if (!ValidatePositions(blocks, b => b.Position, path, context))
            return;

        foreach (var block in blocks)
        {
            var itemPath = $"{path}[{block.Position}]";

            if (!LevelRank.TryGetValue(block.Level ?? "", out var rank))
            {
                context.AddFailure($"{itemPath}.Level", "Level must be one of: MACROCYCLE, MESOCYCLE, MICROCYCLE.");
                continue;
            }

            if (rank <= parentRank)
                context.AddFailure($"{itemPath}.Level",
                    "Block levels must descend MACROCYCLE > MESOCYCLE > MICROCYCLE; a level may be skipped but never inverted or repeated.");

            if (string.IsNullOrWhiteSpace(block.Name))
                context.AddFailure($"{itemPath}.Name", "Name is required.");

            if (block.LoadPercent is { } loadPercent
                && (block.Level != "MESOCYCLE" || !ValidLoadPercents.Contains(loadPercent)))
                context.AddFailure($"{itemPath}.LoadPercent",
                    "LoadPercent is only allowed on MESOCYCLE blocks and must be one of: 100, 102.5, 105, 110.");

            var isMicrocycle = block.Level == "MICROCYCLE";

            if (isMicrocycle)
            {
                if (block.WeekNumber is null)
                    context.AddFailure($"{itemPath}.WeekNumber", "WeekNumber is required for MICROCYCLE blocks.");
                else
                    weekNumbers.Add(block.WeekNumber.Value);

                if (block.Blocks is { Count: > 0 })
                    context.AddFailure($"{itemPath}.Blocks", "MICROCYCLE blocks cannot contain nested blocks.");

                ValidateSessions(block.Sessions, $"{itemPath}.Sessions", context, exerciseIds);
            }
            else
            {
                if (block.WeekNumber is not null)
                    context.AddFailure($"{itemPath}.WeekNumber", "WeekNumber is only allowed on MICROCYCLE blocks.");

                if (block.Sessions is { Count: > 0 })
                    context.AddFailure($"{itemPath}.Sessions", "Sessions are only allowed on MICROCYCLE blocks.");

                ValidateBlockList(block.Blocks, rank, depth + 1, $"{itemPath}.Blocks", context, weekNumbers, exerciseIds);
            }
        }
    }

    private static void ValidateWeekNumbers(
        List<int> weekNumbers, int durationWeeks, ValidationContext<ProgramTemplateBody> context)
    {
        var distinct = weekNumbers.Distinct().OrderBy(w => w).ToList();

        if (distinct.Count != weekNumbers.Count)
            context.AddFailure(string.Empty, "WeekNumber values must be unique across all MICROCYCLE blocks.");

        var expected = Enumerable.Range(1, durationWeeks).ToList();
        if (!distinct.SequenceEqual(expected))
            context.AddFailure(string.Empty,
                $"WeekNumber values must be contiguous from 1 to DurationWeeks ({durationWeeks}); found: {string.Join(", ", distinct)}.");
    }

    // ── Sessions / circuits / exercises ───────────────────────────────────────

    private void ValidateSessions(
        List<ProgramTemplateSession> sessions, string path,
        ValidationContext<ProgramTemplateBody> context, HashSet<Guid> exerciseIds)
    {
        if (sessions is not { Count: > 0 })
            return;

        if (!ValidatePositions(sessions, s => s.Position, path, context))
            return;

        foreach (var session in sessions)
        {
            var itemPath = $"{path}[{session.Position}]";

            if (string.IsNullOrWhiteSpace(session.Name))
                context.AddFailure($"{itemPath}.Name", "Name is required.");

            if (session.DayOfWeek is < 1 or > 7)
                context.AddFailure($"{itemPath}.DayOfWeek", "DayOfWeek must be between 1 and 7.");

            ValidateSessionType(session.Type, $"{itemPath}.Type", context);

            if (session.Nature is not null && !ProgramSessionNatures.All.Contains(session.Nature))
                context.AddFailure($"{itemPath}.Nature",
                    $"Nature must be one of: {string.Join(", ", ProgramSessionNatures.All)}.");

            ValidateCircuits(session.Circuits, $"{itemPath}.Circuits", context, exerciseIds);
        }
    }

    private static void ValidateSessionType(
        string? type, string path, ValidationContext<ProgramTemplateBody> context)
    {
        if (type == "VISIO_GROUPE")
        {
            context.AddFailure(path, "VISIO_GROUPE is not supported in V1.");
            return;
        }

        if (type is null || !ProgramSessionTypes.All.Contains(type))
            context.AddFailure(path, $"Type must be one of: {string.Join(", ", ProgramSessionTypes.All)}.");
    }

    // Circuit and exercise rules live in ProgramCircuitRules (shared with the session template validator).
    private static void ValidateCircuits(
        List<ProgramTemplateCircuit> circuits, string path,
        ValidationContext<ProgramTemplateBody> context, HashSet<Guid> exerciseIds) =>
        ProgramCircuitRules.ValidateCircuits(
            circuits, path,
            (key, message) => context.AddFailure(key, message),
            (_, exerciseId) => exerciseIds.Add(exerciseId));

    // ── Shared ─────────────────────────────────────────────────────────────────

    private static bool ValidatePositions<T>(
        List<T> items, Func<T, int> selector, string path, ValidationContext<ProgramTemplateBody> context) =>
        ProgramCircuitRules.ValidatePositions(items, selector, path, (key, message) => context.AddFailure(key, message));
}
