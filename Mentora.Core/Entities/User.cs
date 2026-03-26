namespace Mentora.Core.Entities;

public class User
{
    public Guid UserId { get; set; }
    public DateTime UserCreatedDate { get; set; }
    public DateTime? UserModificationDate { get; set; }
    public string UserEmail { get; set; } = null!;
    public string? UserPsw { get; set; }
    public string? UserLogin { get; set; }
    public bool UserIsEnabled { get; set; }
    public DateTime? UserDisabledDate { get; set; }
    public string UserRole { get; set; } = null!;

    public Coach? Coach { get; set; }
    public Member? Member { get; set; }
    public ICollection<AuthOtp> AuthOtps { get; set; } = [];
    public ICollection<AuthRefreshToken> AuthRefreshTokens { get; set; } = [];
}
