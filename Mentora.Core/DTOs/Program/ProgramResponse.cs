namespace Mentora.Core.DTOs.Program;

/// <summary>Full representation of a program, including its tree.</summary>
public record ProgramResponse(
    Guid ProgramId,
    Guid CoachId,
    Guid MemberId,
    Guid? TemplateId,
    string Name,
    string Goal,
    DateOnly StartDate,
    int DurationWeeks,
    int WeekOffset,
    string Status,
    DateTime CreatedDate,
    DateTime UpdatedDate,
    List<ProgramBlockResponse> Blocks
);
