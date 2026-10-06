namespace Mentora.Core.DTOs.Program;

public record ProgramCircuitResponse(
    Guid ProgramCircuitId,
    string Name,
    int Position,
    string Mode,
    int? Rounds,
    int? RestBetweenRoundsSeconds,
    int? RestSecondsAfterExercices,
    string? Note,
    List<ProgramExerciseResponse> Exercises
);
