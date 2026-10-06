namespace Mentora.Core.Entities;

/// <summary>
/// A reusable session (circuits and exercises) a coach inserts, by copy, into a program tree.
/// COACH_ID NULL = Mentora platform template (written only by the 11.6 seeder, never through the API);
/// set = the coach's own template.
/// </summary>
/// <remarks>
/// Rule for the 11.6 seeder: a platform template may only reference Mentora catalog exercises
/// (EXERCISES.COACH_ID IS NULL). Duplicating a template never re-validates its exercises for that reason.
/// </remarks>
public class SessionTemplate
{
    public Guid SessionTemplateId { get; set; }

    public Guid? SessionTemplateCoachId { get; set; }
    public Coach? Coach { get; set; }

    /// <summary>Required for platform templates, forbidden for coach templates (CK_SESSION_TEMPLATES_SOURCE_CODE).</summary>
    public string? SessionTemplateSourceCode { get; set; }

    public string SessionTemplateName { get; set; } = null!;
    public string? SessionTemplateDescription { get; set; }

    /// <summary>STANDARD | EVALUATION (<c>ProgramSessionNatures</c>).</summary>
    public string SessionTemplateNature { get; set; } = null!;

    /// <summary>PRESENTIEL_SOLO | PRESENTIEL_GROUPE | VISIO | AUTONOME | PRESENTIEL_OU_VISIO (<c>ProgramSessionTypes</c>).</summary>
    public string SessionTemplateSessionType { get; set; } = null!;

    /// <summary>
    /// jsonb: a bare array of circuits in the tree circuit format (<c>ProgramTemplateCircuit</c>), camelCase.
    /// No node ids, no realised values, no session-level fields. Mapped as a string, serialized by the service.
    /// </summary>
    public string SessionTemplateContent { get; set; } = null!;

    /// <summary>Hides a platform template. Never settable through the API.</summary>
    public bool SessionTemplateIsActive { get; set; } = true;

    public DateTime SessionTemplateCreatedDate { get; set; }
    public DateTime SessionTemplateUpdatedDate { get; set; }
}
