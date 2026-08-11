namespace Mentora.Core.DTOs.ProgramTemplate;

/// <summary>
/// Header-only representation of a program template, without its body — used by the list
/// endpoint since the body can be large.
/// </summary>
/// <param name="ProgramTemplateId">Unique identifier of the template.</param>
/// <param name="CoachId">Owning coach, or <c>null</c> when this is a shared Mentora template.</param>
/// <param name="Name">Display name of the template.</param>
/// <param name="Description">Optional description.</param>
/// <param name="Goal">Training goal.</param>
/// <param name="DurationWeeks">Number of weeks the template spans.</param>
/// <param name="IsActive">Whether the template is active (soft-delete flag).</param>
/// <param name="CreatedDate">Timestamp when the template was created (UTC).</param>
/// <param name="UpdatedDate">Timestamp of the last modification (UTC).</param>
public record ProgramTemplateHeaderResponse(
    Guid ProgramTemplateId,
    Guid? CoachId,
    string Name,
    string? Description,
    string Goal,
    int DurationWeeks,
    bool IsActive,
    DateTime CreatedDate,
    DateTime UpdatedDate
);
