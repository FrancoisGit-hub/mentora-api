using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Mentora.Core.DTOs.Auth;
using Mentora.Core.Entities;
using Mentora.Core.Interfaces;
using Mentora.Core.Settings;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Mentora.Infrastructure.Services;

public class AuthService(MentoraDbContext db, IOptions<JwtSettings> jwtOptions) : IAuthService
{
    private readonly JwtSettings _jwt = jwtOptions.Value;

    public async Task RequestOtpAsync(string email)
    {
        // Any enabled user (member or coach) can request an OTP
        var user = await db.Users
            .FirstOrDefaultAsync(u => u.UserEmail == email && u.UserIsEnabled)
            ?? throw new InvalidOperationException("User not found or account is disabled.");

        // Invalidate any previous unused OTPs for this user
        var previousOtps = await db.AuthOtps
            .Where(o => o.UserId == user.UserId && !o.AuthOtpIsUsed && o.AuthOtpExpirationDate > DateTime.UtcNow)
            .ToListAsync();
        foreach (var old in previousOtps)
            old.AuthOtpIsUsed = true;

        var plainOtp = GenerateOtp();
        db.AuthOtps.Add(new AuthOtp
        {
            AuthOtpCodeHash = BCrypt.Net.BCrypt.HashPassword(plainOtp),
            AuthOtpCreatedDate = DateTime.UtcNow,
            AuthOtpExpirationDate = DateTime.UtcNow.AddMinutes(10),
            AuthOtpIsUsed = false,
            UserId = user.UserId
        });
        await db.SaveChangesAsync();

        Console.WriteLine($"[OTP] {email} → {plainOtp}");
    }

    public async Task<AuthResponse> VerifyOtpAsync(string email, string code)
    {
        var user = await db.Users
            .Include(u => u.Member)
            .Include(u => u.Coach)
            .FirstOrDefaultAsync(u => u.UserEmail == email && u.UserIsEnabled)
            ?? throw new InvalidOperationException("User not found or account is disabled.");

        // Load candidates into memory — BCrypt.Verify cannot be translated to SQL
        var candidates = await db.AuthOtps
            .Where(o => o.UserId == user.UserId && !o.AuthOtpIsUsed && o.AuthOtpExpirationDate > DateTime.UtcNow)
            .OrderByDescending(o => o.AuthOtpCreatedDate)
            .ToListAsync();

        var validOtp = candidates.FirstOrDefault(o => BCrypt.Net.BCrypt.Verify(code, o.AuthOtpCodeHash))
            ?? throw new InvalidOperationException("Invalid or expired OTP.");

        validOtp.AuthOtpIsUsed = true;

        var (clientToken, _) = await CreateRefreshTokenAsync(user.UserId);
        await db.SaveChangesAsync();

        return new AuthResponse(
            AccessToken: GenerateAccessToken(user, user.Member?.MemberId, user.Coach?.CoachId),
            RefreshToken: clientToken,
            ExpiresIn: _jwt.AccessTokenExpirationMinutes * 60
        );
    }

    public async Task<AuthResponse> RefreshTokenAsync(string refreshToken)
    {
        var (tokenId, rawSecret) = ParseClientToken(refreshToken);

        var record = await db.AuthRefreshTokens
            .Include(r => r.User).ThenInclude(u => u.Member)
            .Include(r => r.User).ThenInclude(u => u.Coach)
            .FirstOrDefaultAsync(r =>
                r.AuthRefreshTokenId == tokenId &&
                !r.AuthRefreshTokenIsRevoked &&
                r.AuthRefreshTokenExpirationDate > DateTime.UtcNow)
            ?? throw new InvalidOperationException("Invalid or expired refresh token.");

        if (!BCrypt.Net.BCrypt.Verify(rawSecret, record.AuthRefreshTokenHash))
            throw new InvalidOperationException("Invalid or expired refresh token.");

        // Revoke the consumed token
        record.AuthRefreshTokenIsRevoked = true;
        record.AuthRefreshTokenRevokedDate = DateTime.UtcNow;

        var (newClientToken, _) = await CreateRefreshTokenAsync(record.UserId);
        await db.SaveChangesAsync();

        return new AuthResponse(
            AccessToken: GenerateAccessToken(record.User, record.User.Member?.MemberId, record.User.Coach?.CoachId),
            RefreshToken: newClientToken,
            ExpiresIn: _jwt.AccessTokenExpirationMinutes * 60
        );
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the access token.
    ///
    /// Claims always present:
    ///   sub       — UserId (UUID)
    ///   email     — user email
    ///   userType  — "MEMBER" | "COACH" | "BOTH"
    ///   jti       — unique token id
    ///
    /// Claims added when the corresponding profile exists:
    ///   memberId  — MemberId (UUID), present for MEMBER and BOTH
    ///   coachId   — CoachId  (UUID), present for COACH and BOTH
    /// </summary>
    private string GenerateAccessToken(User user, Guid? memberId, Guid? coachId)
    {
        var userType = (memberId.HasValue, coachId.HasValue) switch
        {
            (true,  true)  => "BOTH",
            (false, true)  => "COACH",
            (true,  false) => "MEMBER",
            _              => "UNKNOWN"
        };

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,   user.UserId.ToString()),
            new(JwtRegisteredClaimNames.Email, user.UserEmail),
            new("userType",                    userType),
            new(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString())
        };

        if (memberId.HasValue)
            claims.Add(new Claim("memberId", memberId.Value.ToString()));

        if (coachId.HasValue)
            claims.Add(new Claim("coachId", coachId.Value.ToString()));

        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
        var token = new JwtSecurityToken(
            issuer:             _jwt.Issuer,
            audience:           _jwt.Audience,
            claims:             claims,
            expires:            DateTime.UtcNow.AddMinutes(_jwt.AccessTokenExpirationMinutes),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // Returns (clientToken, entity). Flushes to DB to obtain the generated Guid PK.
    private async Task<(string clientToken, AuthRefreshToken entity)> CreateRefreshTokenAsync(Guid userId)
    {
        var rawSecret = GenerateSecureRandom();
        var entity = new AuthRefreshToken
        {
            AuthRefreshTokenHash           = BCrypt.Net.BCrypt.HashPassword(rawSecret),
            AuthRefreshTokenCreatedDate    = DateTime.UtcNow,
            AuthRefreshTokenExpirationDate = DateTime.UtcNow.AddDays(_jwt.RefreshTokenExpirationDays),
            AuthRefreshTokenIsRevoked      = false,
            UserId                         = userId
        };
        db.AuthRefreshTokens.Add(entity);
        // Flush to get the DB-generated Guid before encoding it in the token
        await db.SaveChangesAsync();

        return ($"{entity.AuthRefreshTokenId}:{rawSecret}", entity);
    }

    private static (Guid tokenId, string rawSecret) ParseClientToken(string token)
    {
        var sep = token.IndexOf(':');
        if (sep < 1 || !Guid.TryParse(token[..sep], out var id))
            throw new InvalidOperationException("Invalid or expired refresh token.");
        return (id, token[(sep + 1)..]);
    }

    private static string GenerateOtp()
    {
        Span<byte> bytes = stackalloc byte[4];
        RandomNumberGenerator.Fill(bytes);
        var value = Math.Abs(BitConverter.ToInt32(bytes)) % 900_000 + 100_000;
        return value.ToString();
    }

    private static string GenerateSecureRandom()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }
}
