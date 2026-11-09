namespace Mentora.Core.Entities;

/// <summary>Lot 12.1 — one-time admin login code (BCrypt hash only).</summary>
public class AdminOtp
{
    public Guid AdminOtpId { get; set; }
    public Guid AdminOtpAdminUserId { get; set; }
    public string AdminOtpCodeHash { get; set; } = null!;
    public DateTime AdminOtpExpirationDate { get; set; }
    public bool AdminOtpIsUsed { get; set; }
    public int AdminOtpFailedAttempts { get; set; }
    public DateTime AdminOtpCreatedDate { get; set; }

    public AdminUser AdminUser { get; set; } = null!;
}
