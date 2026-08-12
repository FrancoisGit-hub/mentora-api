using Mentora.Core.DTOs.ProgramTemplate.Body;

namespace Mentora.Core.DTOs.Program;

/// <summary>Payload for fully replacing an existing program: name, goal, startDate and the whole tree.</summary>
public record UpdateProgramRequest(
    string Name,
    string Goal,
    DateOnly StartDate,
    int DurationWeeks,
    ProgramTemplateBody Body
);
