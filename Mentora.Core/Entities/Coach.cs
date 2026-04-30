namespace Mentora.Core.Entities;

public class Coach
{
    public Guid CoachId { get; set; }
    public string CoachFirstName { get; set; } = null!;
    public string CoachLastName { get; set; } = null!;
    public string? CoachPhone { get; set; }
    public DateTime CoachCreatedDate { get; set; }
    public bool CoachIsActive { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public ICollection<MemberCoach> MemberCoaches { get; set; } = [];
    public AgentToken? AgentToken { get; set; }
    public CoachParameter? CoachParameter { get; set; }
    public ICollection<OfferProgram> OfferPrograms { get; set; } = [];
    public ICollection<SessionSlot> SessionSlots { get; set; } = [];
}
