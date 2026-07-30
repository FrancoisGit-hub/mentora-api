using Mentora.Core.Enums;

namespace Mentora.Core.Entities;

public class Member
{
    public Guid MemberId { get; set; }
    public string MemberFirstName { get; set; } = null!;
    public string MemberLastName { get; set; } = null!;
    public string? MemberPhone { get; set; }
    public DateTime MemberCreatedDate { get; set; }
    public bool MemberIsActive { get; set; }
    public bool MemberHasActivated { get; set; }
    public DateTime? MemberActivationDate { get; set; }
    public Gender? MemberGender { get; set; }
    public short? MemberHeightCm { get; set; }
    public DateOnly? MemberBirthDate { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public ICollection<MemberCoach> MemberCoaches { get; set; } = [];
    public MemberParameter? MemberParameter { get; set; }
}
