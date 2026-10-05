using Mentora.Core.DTOs.Program.Body;

namespace Mentora.Core.DTOs.Program;

/// <summary>
/// Payload for replacing an existing program: name, goal, startDate and the whole tree. Nodes
/// carrying an id are updated in place (history kept), nodes without id are inserted, existing
/// nodes absent from the tree are deleted unless they carry history.
/// </summary>
public record UpdateProgramRequest(
    string Name,
    string Goal,
    DateOnly StartDate,
    int DurationWeeks,
    UpdateProgramBody Body
);
