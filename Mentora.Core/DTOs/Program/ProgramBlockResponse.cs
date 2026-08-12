namespace Mentora.Core.DTOs.Program;

public record ProgramBlockResponse(
    Guid ProgramBlockId,
    Guid? ParentId,
    string Level,
    string Name,
    int Position,
    int? WeekNumber,
    List<ProgramBlockResponse> Blocks,
    List<ProgramSessionResponse> Sessions
);
