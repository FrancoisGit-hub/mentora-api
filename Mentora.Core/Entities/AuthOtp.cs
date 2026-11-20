namespace Mentora.Core.Entities;

public class AuthOtp
{
    public Guid AuthOtpId { get; set; }
    public string AuthOtpCodeHash { get; set; } = null!;
    public DateTime AuthOtpExpirationDate { get; set; }
    public bool AuthOtpIsUsed { get; set; }
    public DateTime AuthOtpCreatedDate { get; set; }

    /// <summary>Lot 14.2: failed verifications; the code stops being a candidate at 5.</summary>
    public int AuthOtpAttemptCount { get; set; } = 0;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
}
