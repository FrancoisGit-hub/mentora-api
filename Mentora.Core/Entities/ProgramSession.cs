using Mentora.Core.Enums;

namespace Mentora.Core.Entities;

public class ProgramSession
{
    public Guid ProgramSessionId { get; set; }

    public Guid ProgramSessionProgramId { get; set; }
    public Guid ProgramSessionBlockId { get; set; }

    // Denormalized — see WHY PROGRAM_ID IS DENORMALIZED ON ALL FIVE (ProgramConfiguration.cs)
    public Guid ProgramSessionCoachId { get; set; }
    public Guid ProgramSessionMemberId { get; set; }

    // Unused until Lot 6.5 — links to the actual booked Session once the member books it.
    // SetNull is correct: if the booking disappears, losing the link is the right outcome.
    public Guid? ProgramSessionSessionId { get; set; }

    public string ProgramSessionName { get; set; } = null!;
    public string ProgramSessionType { get; set; } = null!;   // PRESENTIEL_SOLO | PRESENTIEL_GROUPE | VISIO | A_DISTANCE
    public int ProgramSessionDayOfWeek { get; set; }          // 1..7
    public int ProgramSessionPosition { get; set; }

    public ProgramSessionStatus ProgramSessionStatus { get; set; } = ProgramSessionStatus.Planned;

    public DateTime? ProgramSessionCompletedDate { get; set; }
    public string? ProgramSessionMemberFeedback { get; set; }
    public string? ProgramSessionCoachNote { get; set; }
}
