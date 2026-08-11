namespace Mentora.Core.DTOs.Exercise;

/// <summary>Payload for creating or fully replacing an exercise in the coach's catalogue.</summary>
/// <param name="Name">Display name of the exercise. Required; max 120 characters.</param>
/// <param name="Description">Optional short description. Max 2 000 characters.</param>
/// <param name="Instructions">Optional step-by-step execution instructions. Max 4 000 characters.</param>
/// <param name="VideoUrl">Optional demonstration video URL. Max 500 characters.</param>
/// <param name="ImageUrl">Optional illustration image URL. Max 500 characters.</param>
/// <param name="MuscleGroup">
/// Primary muscle group targeted. Accepted values: <c>CHEST</c>, <c>BACK</c>, <c>SHOULDERS</c>,
/// <c>BICEPS</c>, <c>TRICEPS</c>, <c>FOREARMS</c>, <c>ABS</c>, <c>QUADRICEPS</c>, <c>HAMSTRINGS</c>,
/// <c>GLUTES</c>, <c>CALVES</c>, <c>FULL_BODY</c>, <c>CARDIO</c>.
/// </param>
/// <param name="Equipment">
/// Equipment required. Accepted values: <c>BODYWEIGHT</c>, <c>BARBELL</c>, <c>DUMBBELL</c>,
/// <c>KETTLEBELL</c>, <c>MACHINE</c>, <c>CABLE</c>, <c>ELASTIC</c>, <c>OTHER</c>.
/// </param>
/// <param name="IsPolyarticular">Whether the exercise recruits multiple joints/muscle groups at once.</param>
public record ExerciseRequest(
    string Name,
    string? Description,
    string? Instructions,
    string? VideoUrl,
    string? ImageUrl,
    string MuscleGroup,
    string Equipment,
    bool IsPolyarticular
);
