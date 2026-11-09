namespace Mentora.Core.Entities;

/// <summary>Lot 12.1 — admin refresh token, handed out as "{id}:{secret}", BCrypt hash stored.</summary>
public class AdminRefreshToken
{
    public Guid AdminRefreshTokenId { get; set; }
    public Guid AdminRefreshTokenAdminUserId { get; set; }
    public string AdminRefreshTokenHash { get; set; } = null!;
    public DateTime AdminRefreshTokenExpirationDate { get; set; }
    public bool AdminRefreshTokenIsRevoked { get; set; }
    public DateTime? AdminRefreshTokenRevokedDate { get; set; }
    public DateTime AdminRefreshTokenCreatedDate { get; set; }

    public AdminUser AdminUser { get; set; } = null!;
}
