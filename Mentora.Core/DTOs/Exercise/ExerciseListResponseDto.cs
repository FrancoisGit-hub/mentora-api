namespace Mentora.Core.DTOs.Exercise;

/// <summary>
/// Paginated list of catalog exercises, as returned by <c>GET /api/v1/member/exercises</c>.
/// </summary>
/// <param name="Items">Exercises on the current page, sorted by name then id ascending.</param>
/// <param name="NextCursor">
/// Opaque cursor to supply as the <c>cursor</c> query parameter to retrieve the next page,
/// or <c>null</c> when this is the last page.
/// </param>
public record ExerciseListResponseDto(
    IReadOnlyList<ExerciseSummaryDto> Items,
    string? NextCursor);

/// <summary>Light representation of an exercise in a member list.</summary>
/// <param name="ExerciseId">Unique identifier of the exercise.</param>
/// <param name="Name">Display name of the exercise.</param>
/// <param name="Level">Minimum level: <c>BEGINNER</c>, <c>INTERMEDIATE</c> or <c>ADVANCED</c>; <c>null</c> when unknown.</param>
/// <param name="PrimaryEquipment">Primary equipment, or <c>null</c>.</param>
/// <param name="SecondaryEquipment">Secondary equipment, or <c>null</c>.</param>
public record ExerciseSummaryDto(
    Guid ExerciseId,
    string Name,
    string? Level,
    ExerciseEquipmentRef? PrimaryEquipment,
    ExerciseEquipmentRef? SecondaryEquipment);
