namespace Mentora.Core.Entities;

public class AuthOtp
{
    public Guid AuthOtpId { get; set; }
    public string AuthOtpCodeHash { get; set; } = null!;
    public DateTime AuthOtpExpirationDate { get; set; }
    public bool AuthOtpIsUsed { get; set; }
    public DateTime AuthOtpCreatedDate { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
}
