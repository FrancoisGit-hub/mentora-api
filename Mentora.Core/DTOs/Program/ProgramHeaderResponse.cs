namespace Mentora.Core.DTOs.Program;

/// <summary>Header-only representation of a program, without its tree — used by the list endpoint.</summary>
public record ProgramHeaderResponse(
    Guid ProgramId,
    Guid CoachId,
    Guid MemberId,
    Guid? TemplateId,
    string Name,
    string Goal,
    DateOnly StartDate,
    int DurationWeeks,
    string Status,
    DateTime CreatedDate,
    DateTime UpdatedDate
);
