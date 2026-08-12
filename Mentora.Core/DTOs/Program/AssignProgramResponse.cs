namespace Mentora.Core.DTOs.Program;

/// <param name="Program">The newly created program, full tree included.</param>
/// <param name="ArchivedProgramId">
/// The member's previous ACTIVE program, if any, now ARCHIVED as a side effect of this
/// assignment — so the coach UI can say what was replaced. Null when there was none.
/// </param>
public record AssignProgramResponse(
    ProgramResponse Program,
    Guid? ArchivedProgramId
);
