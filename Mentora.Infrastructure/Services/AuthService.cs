using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Mentora.Core.DTOs.Auth;
using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Mentora.Core.Exceptions;
using Mentora.Core.Interfaces;
using Mentora.Core.Settings;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Mentora.Infrastructure.Services;

public class AuthService(
    MentoraDbContext db,
    IOptions<JwtSettings> jwtOptions,
    IEmailSender emailSender,
    ILogger<AuthService> logger) : IAuthService
{
    private readonly JwtSettings _jwt = jwtOptions.Value;

    public async Task RequestOtpAsync(string email, bool? isCoach)
    {
        // Any enabled user (member or coach) can request an OTP
        var user = await db.Users
            .Include(u => u.Coach)
            .Include(u => u.Member)
            .FirstOrDefaultAsync(u => u.UserEmail == email && u.UserIsEnabled)
            ?? throw new InvalidOperationException("User not found or account is disabled.");

        ResolveEffectiveIsCoach(user, isCoach, email);

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

        var subject = $"Votre code Mentora : {plainOtp}";
        _ = await emailSender.SendAsync(
            user.UserEmail,
            subject,
            BuildOtpHtmlBody(plainOtp),
            BuildOtpTextBody(plainOtp),
            CancellationToken.None);
    }

    public async Task<AuthResponse> VerifyOtpAsync(string email, string code, bool? isCoach)
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

        var effectiveIsCoach = ResolveEffectiveIsCoach(user, isCoach, email);
        var role = effectiveIsCoach ? UserRole.Coach : UserRole.Member;

        validOtp.AuthOtpIsUsed = true;

        var (clientToken, _) = await CreateRefreshTokenAsync(user.UserId, role);
        await db.SaveChangesAsync();

        return new AuthResponse(
            AccessToken: effectiveIsCoach
                ? GenerateAccessToken(user, memberId: null, coachId: user.Coach!.CoachId)
                : GenerateAccessToken(user, memberId: user.Member!.MemberId, coachId: null),
            RefreshToken: clientToken,
            ExpiresIn: _jwt.AccessTokenExpirationMinutes * 60,
            IsCoach: effectiveIsCoach
        );
    }

    /// <summary>
    /// Resolves whether the caller should be treated as coach (true) or member (false).
    /// When <paramref name="isCoach"/> is provided, it is honored but still validated against
    /// the user's actual profiles (backward-compat). When absent, the role is auto-detected from
    /// which profile(s) exist. An email with BOTH a Coach and a Member profile cannot be
    /// auto-resolved — that is a business-rule violation, surfaced as a 409 rather than guessed.
    /// </summary>
    private bool ResolveEffectiveIsCoach(User user, bool? isCoach, string email)
    {
        if (isCoach.HasValue)
        {
            var value = isCoach.Value;
            if (value && user.Coach == null)
                throw new InvalidOperationException("No coach account for this email.");
            if (!value && user.Member == null)
                throw new InvalidOperationException("No member account for this email.");
            return value;
        }

        if (user.Coach != null && user.Member != null)
        {
            logger.LogError(
                "Ambiguous role for {Email}: user has both a Coach and a Member profile and no isCoach was provided.",
                email);
            throw new ConflictException(
                "This email has both a coach and a member account. Please specify isCoach explicitly.");
        }

        if (user.Coach != null)
            return true;
        if (user.Member != null)
            return false;

        throw new InvalidOperationException("No coach or member account for this email.");
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

        // The role is fixed at the token's original issuance (VerifyOtpAsync) — never
        // re-derived from whichever profiles the user currently has, so a dual-profile
        // user who logged in as MEMBER stays MEMBER across refreshes, not "BOTH".
        Guid? memberId = null;
        Guid? coachId  = null;

        if (record.AuthRefreshTokenUserRole == UserRole.Member)
        {
            memberId = record.User.Member?.MemberId
                ?? throw new InvalidOperationException("Refresh token role is MEMBER but the user has no member profile.");
        }
        else
        {
            coachId = record.User.Coach?.CoachId
                ?? throw new InvalidOperationException("Refresh token role is COACH but the user has no coach profile.");
        }

        var (newClientToken, _) = await CreateRefreshTokenAsync(record.UserId, record.AuthRefreshTokenUserRole);
        await db.SaveChangesAsync();

        return new AuthResponse(
            AccessToken: GenerateAccessToken(record.User, memberId, coachId),
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
    private async Task<(string clientToken, AuthRefreshToken entity)> CreateRefreshTokenAsync(Guid userId, UserRole role)
    {
        var rawSecret = GenerateSecureRandom();
        var entity = new AuthRefreshToken
        {
            AuthRefreshTokenHash           = BCrypt.Net.BCrypt.HashPassword(rawSecret),
            AuthRefreshTokenCreatedDate    = DateTime.UtcNow,
            AuthRefreshTokenExpirationDate = DateTime.UtcNow.AddDays(_jwt.RefreshTokenExpirationDays),
            AuthRefreshTokenIsRevoked      = false,
            AuthRefreshTokenUserRole       = role,
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

    private static string BuildOtpHtmlBody(string otpCode) => $@"
<!DOCTYPE html>
<html>
<head><meta charset=""utf-8""></head>
<body style=""font-family: Arial, sans-serif; color: #1a1a1a; max-width: 560px; margin: 0 auto; padding: 24px;"">
  <h1 style=""color: #1a1a1a; font-size: 20px;"">Mentora</h1>
  <p>Bonjour,</p>
  <p>Voici votre code de connexion :</p>
  <p style=""font-size: 32px; font-weight: bold; letter-spacing: 4px; background: #f4f4f4; padding: 16px 24px; display: inline-block; border-radius: 8px;"">
    {otpCode}
  </p>
  <p>Ce code expire dans 15 minutes.</p>
  <p style=""color: #666; font-size: 13px; margin-top: 32px;"">
    Si vous n'avez pas demandé ce code, ignorez ce message.
  </p>
</body>
</html>";

    private static string BuildOtpTextBody(string otpCode) => $@"Mentora

Bonjour,

Voici votre code de connexion : {otpCode}

Ce code expire dans 15 minutes.

Si vous n'avez pas demandé ce code, ignorez ce message.";
}
