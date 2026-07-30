namespace Mentora.Core.Entities;

public class MemberCoach
{
    public Guid MemberCoachId { get; set; }
    public Guid MemberId { get; set; }
    public Guid CoachId { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime StartedAt { get; set; }
    public string? MemberCoachPresentialAddress { get; set; }

    public Member Member { get; set; } = null!;
    public Coach Coach { get; set; } = null!;
}
