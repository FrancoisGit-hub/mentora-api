using Mentora.Core.DTOs.ProgramTemplate.Body;

namespace Mentora.Core.Validators.ProgramTemplate;

/// <summary>
/// The circuit / exercise rules of the program tree, shared by <see cref="ProgramTemplateBodyValidator"/>
/// (program templates and programs) and the session template validator. Single source: do not duplicate
/// these rules elsewhere. Failure keys are <c>{path}[{Position}]...</c>, Position being the 1-based
/// position value of the node (not the array index).
/// </summary>
public static class ProgramCircuitRules
{
    public const int MaxRestAfterSeconds = 600;

    private static readonly HashSet<string> ValidLoadTypes = ["KG", "BODYWEIGHT"];

    /// <summary>
    /// Validates a list of circuits (an empty list is ignored here: callers decide whether it is allowed).
    /// <paramref name="onExercise"/> receives the failure path prefix (<c>{path}[p].Exercises[q]</c>) and the
    /// referenced exercise id of every exercise node, so callers can check visibility.
    /// </summary>
    public static void ValidateCircuits(
        List<ProgramTemplateCircuit> circuits, string path,
        Action<string, string> fail, Action<string, Guid> onExercise)
    {
        if (circuits is not { Count: > 0 })
            return;

        ValidatePositions(circuits, c => c.Position, path, fail);

        foreach (var circuit in circuits)
        {
            var itemPath = $"{path}[{circuit.Position}]";

            if (string.IsNullOrWhiteSpace(circuit.Name))
                fail($"{itemPath}.Name", "Name is required.");

            var isStandard = circuit.Mode == "STANDARD";
            var isInterval = circuit.Mode == "INTERVAL";

            if (!isStandard && !isInterval)
                fail($"{itemPath}.Mode", "Mode must be one of: STANDARD, INTERVAL.");

            if (isInterval && circuit.Rounds is null)
                fail($"{itemPath}.Rounds", "Rounds is required when Mode is INTERVAL.");

            if (circuit.RestAfterSeconds is < 0 or > MaxRestAfterSeconds)
                fail($"{itemPath}.RestAfterSeconds",
                    $"RestAfterSeconds must be between 0 and {MaxRestAfterSeconds}.");

            ValidateExercises(circuit.Exercises, circuit.Mode, $"{itemPath}.Exercises", fail, onExercise);
        }
    }

    private static void ValidateExercises(
        List<ProgramTemplateExercise> exercises, string? mode, string path,
        Action<string, string> fail, Action<string, Guid> onExercise)
    {
        if (exercises is not { Count: > 0 })
        {
            fail(path, "A circuit must contain at least one exercise.");
            return;
        }

        ValidatePositions(exercises, e => e.Position, path, fail);

        foreach (var exercise in exercises)
        {
            var itemPath = $"{path}[{exercise.Position}]";

            onExercise(itemPath, exercise.ExerciseId);

            if (exercise.LoadType == "ELASTIC")
                fail($"{itemPath}.LoadType", "ELASTIC is not supported in V1.");
            else if (exercise.LoadType is null || !ValidLoadTypes.Contains(exercise.LoadType))
                fail($"{itemPath}.LoadType", $"LoadType must be one of: {string.Join(", ", ValidLoadTypes)}.");

            if (mode == "STANDARD")
            {
                if (exercise.PrescribedSets is null || exercise.PrescribedReps is null)
                    fail(itemPath, "STANDARD circuits require PrescribedSets and PrescribedReps on every exercise.");

                if (exercise.WorkSeconds is not null || exercise.RestWorkSeconds is not null)
                    fail(itemPath, "STANDARD circuits must not carry WorkSeconds or RestWorkSeconds.");
            }
            else if (mode == "INTERVAL")
            {
                if (exercise.WorkSeconds is null || exercise.RestWorkSeconds is null)
                    fail(itemPath, "INTERVAL circuits require WorkSeconds and RestWorkSeconds on every exercise.");

                if (exercise.PrescribedSets is not null || exercise.PrescribedReps is not null)
                    fail(itemPath, "INTERVAL circuits must not carry PrescribedSets or PrescribedReps.");
            }
        }
    }

    /// <summary>Positions must be unique and contiguous starting from 1.</summary>
    public static void ValidatePositions<T>(
        List<T> items, Func<T, int> selector, string path, Action<string, string> fail)
    {
        var positions = items.Select(selector).OrderBy(p => p).ToList();
        var expected = Enumerable.Range(1, items.Count);

        if (!positions.SequenceEqual(expected))
            fail(path, $"Positions must be unique and contiguous starting from 1 (found: {string.Join(", ", positions)}).");
    }
}
