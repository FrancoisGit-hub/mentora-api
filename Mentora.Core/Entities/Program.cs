using Mentora.Core.Enums;

namespace Mentora.Core.Entities;

public class Program
{
    public Guid ProgramId { get; set; }

    public Guid ProgramCoachId { get; set; }
    public Coach Coach { get; set; } = null!;

    public Guid ProgramMemberId { get; set; }
    public Member Member { get; set; } = null!;

    // Weak reference — no FK constraint; origin trace only, same pattern as SessionVoucher.ProductId
    public Guid? ProgramTemplateId { get; set; }

    public string ProgramName { get; set; } = null!;
    public ProgramGoal ProgramGoal { get; set; }
    public DateOnly ProgramStartDate { get; set; }
    public int ProgramDurationWeeks { get; set; }

    // Unused until Lot 6.5
    public int ProgramWeekOffset { get; set; } = 0;

    public ProgramStatus ProgramStatus { get; set; } = ProgramStatus.Active;

    public DateTime ProgramCreatedDate { get; set; }
    public DateTime ProgramUpdatedDate { get; set; }
}
