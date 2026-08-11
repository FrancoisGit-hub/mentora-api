using Mentora.Core.DTOs.ProgramTemplate.Body;

namespace Mentora.Core.DTOs.ProgramTemplate;

/// <summary>Payload for creating or fully replacing a program template in the coach's library.</summary>
/// <param name="Name">Display name of the template. Required; max 120 characters.</param>
/// <param name="Description">Optional description. Max 2 000 characters.</param>
/// <param name="Goal">
/// Training goal. Accepted values: <c>MUSCLE_GAIN</c>, <c>FAT_LOSS</c>, <c>STRENGTH</c>,
/// <c>ENDURANCE</c>, <c>MOBILITY</c>, <c>REHAB</c>, <c>GENERAL_FITNESS</c>.
/// </param>
/// <param name="DurationWeeks">Number of weeks the template spans. Must be a positive integer.</param>
/// <param name="Body">The recursive block tree (macrocycles/mesocycles/microcycles/sessions/circuits/exercises).</param>
public record ProgramTemplateRequest(
    string Name,
    string? Description,
    string Goal,
    int DurationWeeks,
    ProgramTemplateBody Body
);
