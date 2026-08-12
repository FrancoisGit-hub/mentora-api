using Mentora.Core.DTOs.ProgramTemplate.Body;

namespace Mentora.Core.DTOs.Program;

/// <summary>Payload for assigning a program to a member.</summary>
/// <param name="TemplateId">
/// When present, the program is a deep copy of this program template. <paramref name="Name"/>,
/// <paramref name="Goal"/>, <paramref name="DurationWeeks"/> and <paramref name="Body"/> are then
/// taken from the template and ignored if supplied. The template must be visible to the coach
/// (Mentora catalogue or owned by the coach) and active, else 404.
/// </param>
/// <param name="Name">Required when <paramref name="TemplateId"/> is absent. Max 120 characters.</param>
/// <param name="Goal">Required when <paramref name="TemplateId"/> is absent.</param>
/// <param name="StartDate">First day the program takes effect. Always required.</param>
/// <param name="DurationWeeks">Required when <paramref name="TemplateId"/> is absent.</param>
/// <param name="Body">Required when <paramref name="TemplateId"/> is absent.</param>
public record AssignProgramRequest(
    Guid? TemplateId,
    string? Name,
    string? Goal,
    DateOnly StartDate,
    int? DurationWeeks,
    ProgramTemplateBody? Body
);
