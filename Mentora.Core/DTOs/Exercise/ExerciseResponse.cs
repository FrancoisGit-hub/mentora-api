namespace Mentora.Core.DTOs.Exercise;

/// <summary>Representation of an exercise returned by the API.</summary>
/// <param name="ExerciseId">Unique identifier of the exercise.</param>
/// <param name="CoachId">Owning coach, or <c>null</c> when this is a shared Mentora catalogue entry.</param>
/// <param name="Name">Display name of the exercise.</param>
/// <param name="Description">Optional short description.</param>
/// <param name="Instructions">Optional step-by-step execution instructions.</param>
/// <param name="VideoUrl">Optional demonstration video URL.</param>
/// <param name="ImageUrl">Optional illustration image URL.</param>
/// <param name="MuscleGroup">Primary muscle group targeted.</param>
/// <param name="Equipment">Equipment required.</param>
/// <param name="IsPolyarticular">Whether the exercise recruits multiple joints/muscle groups at once.</param>
/// <param name="IsActive">Whether the exercise is active (soft-delete flag).</param>
/// <param name="CreatedDate">Timestamp when the exercise was created (UTC).</param>
/// <param name="UpdatedDate">Timestamp of the last modification (UTC).</param>
public record ExerciseResponse(
    Guid ExerciseId,
    Guid? CoachId,
    string Name,
    string? Description,
    string? Instructions,
    string? VideoUrl,
    string? ImageUrl,
    string MuscleGroup,
    string Equipment,
    bool IsPolyarticular,
    bool IsActive,
    DateTime CreatedDate,
    DateTime UpdatedDate
);
