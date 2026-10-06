namespace Mentora.Core.Entities;

public class Coach
{
    public Guid CoachId { get; set; }
    public string CoachFirstName { get; set; } = null!;
    public string CoachLastName { get; set; } = null!;
    public string? CoachPhone { get; set; }
    public DateTime CoachCreatedDate { get; set; }
    public bool CoachIsActive { get; set; }

    // Lot 11.3 - information only, nothing reads these to block an offer or a slot.
    public bool CoachOffersVisio { get; set; } = false;
    public bool CoachOffersInPersonSolo { get; set; } = false;
    public List<string> CoachInPersonLocations { get; set; } = new List<string>();

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public ICollection<MemberCoach> MemberCoaches { get; set; } = [];
    public AgentToken? AgentToken { get; set; }
    public CoachParameter? CoachParameter { get; set; }
    public ICollection<OfferProgram> OfferPrograms { get; set; } = [];
    public ICollection<SessionSlot> SessionSlots { get; set; } = [];
    public ICollection<Product> Products { get; set; } = [];
    public ICollection<ProductPack> ProductPacks { get; set; } = [];
    public ICollection<Exercise> Exercises { get; set; } = [];
    public ICollection<ProgramTemplate> ProgramTemplates { get; set; } = [];
    public ICollection<Program> Programs { get; set; } = [];
}
