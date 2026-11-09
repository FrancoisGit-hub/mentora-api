namespace Mentora.Core.Entities;

/// <summary>
/// Lot 12.1 — a Mentora associate allowed on the admin back-office. Never linked to USERS.
/// The list of accounts is driven by AdminAuth:Emails at each startup (AdminUserSeeder).
/// </summary>
public class AdminUser
{
    public Guid AdminUserId { get; set; }
    public string AdminUserEmail { get; set; } = null!;
    public bool AdminUserIsEnabled { get; set; }
    public DateTime? AdminUserLastLoginDate { get; set; }
    public DateTime AdminUserCreatedDate { get; set; }
    public DateTime AdminUserUpdatedDate { get; set; }

    public ICollection<AdminOtp> AdminOtps { get; set; } = new List<AdminOtp>();
    public ICollection<AdminRefreshToken> AdminRefreshTokens { get; set; } = new List<AdminRefreshToken>();
}
