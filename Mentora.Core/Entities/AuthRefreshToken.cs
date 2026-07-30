using Mentora.Core.Enums;

namespace Mentora.Core.Entities;

public class AuthRefreshToken
{
    public Guid AuthRefreshTokenId { get; set; }
    public string AuthRefreshTokenHash { get; set; } = null!;
    public DateTime AuthRefreshTokenExpirationDate { get; set; }
    public bool AuthRefreshTokenIsRevoked { get; set; }
    public UserRole AuthRefreshTokenUserRole { get; set; }
    public DateTime AuthRefreshTokenCreatedDate { get; set; }
    public DateTime? AuthRefreshTokenRevokedDate { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
}
