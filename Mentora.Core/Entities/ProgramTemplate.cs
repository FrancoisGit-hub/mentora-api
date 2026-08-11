using Mentora.Core.Enums;

namespace Mentora.Core.Entities;

public class ProgramTemplate
{
    public Guid ProgramTemplateId { get; set; }

    // NULL means "Mentora template"
    public Guid? ProgramTemplateCoachId { get; set; }
    public Coach? Coach { get; set; }

    public string ProgramTemplateName { get; set; } = null!;
    public string? ProgramTemplateDescription { get; set; }
    public ProgramGoal ProgramTemplateGoal { get; set; }
    public int ProgramTemplateDurationWeeks { get; set; }

    // Recursive block tree — cannot use OwnsOne/OwnsMany/ToJson (EF owned types
    // cannot be recursive). Stored as a plain jsonb string; the service owns
    // serialization via System.Text.Json.
    public string ProgramTemplateBody { get; set; } = null!;

    public bool ProgramTemplateIsActive { get; set; } = true;

    public DateTime ProgramTemplateCreatedDate { get; set; }
    public DateTime ProgramTemplateUpdatedDate { get; set; }
}
