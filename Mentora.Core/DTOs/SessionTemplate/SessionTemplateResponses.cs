using Mentora.Core.DTOs.ProgramTemplate.Body;

namespace Mentora.Core.DTOs.SessionTemplate;

/// <summary>Item of the session template list (no circuits).</summary>
public record SessionTemplateItemResponse(
    Guid Id,
    string Name,
    string? Description,
    string Nature,
    string SessionType,
    bool IsPlatform,
    string? SourceCode,
    int CircuitCount,
    int ExerciseCount,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>Cursor page: <c>nextCursor</c> is null on the last page.</summary>
public record SessionTemplateListResponse(List<SessionTemplateItemResponse> Items, string? NextCursor);

/// <summary>Full template: item fields, circuits and a ready-to-insert program tree session node.</summary>
/// <remarks>
/// <c>Circuits</c> are in the tree circuit format. <c>SessionNode</c> is the session node to copy into a
/// program tree: it carries no id and no sessionId, and its <c>dayOfWeek</c> (1) and <c>position</c> (1) are
/// placeholders the app sets when it places the node in the program.
/// </remarks>
public record SessionTemplateResponse(
    Guid Id,
    string Name,
    string? Description,
    string Nature,
    string SessionType,
    bool IsPlatform,
    string? SourceCode,
    int CircuitCount,
    int ExerciseCount,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<ProgramTemplateCircuit> Circuits,
    ProgramTemplateSession SessionNode);
