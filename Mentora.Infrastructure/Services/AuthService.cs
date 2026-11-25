using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using Mentora.Core;
using Mentora.Core.DTOs.Auth;
using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Mentora.Core.Exceptions;
using Mentora.Core.Interfaces;
using Mentora.Core.Settings;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Mentora.Infrastructure.Services;

public class AuthService(
    MentoraDbContext db,
    IOptions<JwtSettings> jwtOptions,
    IEmailSender emailSender,
    IValidator<LogoutRequest> logoutValidator,
    ILogger<AuthService> logger,
    IHostEnvironment environment,
    INotificationComposer notifications) : IAuthService
{
    // Single source for the OTP expiry and for the "expires in N minutes" text of the OTP mail.
    private const int OtpLifetimeMinutes = 10;

    // Lot 14.2 (14.C2): failed verifications after which a code is no longer a candidate.
    internal const int OtpMaxAttempts = 5;

    private readonly JwtSettings _jwt = jwtOptions.Value;

    public async Task RequestOtpAsync(string email, bool? isCoach)
    {
        // Lot 14.1 (14.A3): trim + lower invariant; the lookup compares lower("USER_EMAIL"), so rows
        // stored with capitals still match.
        email = EmailNormalizer.Normalize(email);

        // Any enabled user (member or coach) can request an OTP
        var user = await db.Users
            .Include(u => u.Coach)
            .Include(u => u.Member)
            .FirstOrDefaultAsync(u => u.UserEmail.ToLower() == email && u.UserIsEnabled)
            ?? throw new InvalidOperationException("User not found or account is disabled.");

        var effectiveIsCoach = ResolveEffectiveIsCoach(user, isCoach, email);

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
            AuthOtpExpirationDate = DateTime.UtcNow.AddMinutes(OtpLifetimeMinutes),
            AuthOtpIsUsed = false,
            UserId = user.UserId
        });
        await db.SaveChangesAsync();

        // Lot 14.3: the login code is written to the console only in Development; nowhere else.
        if (environment.IsDevelopment())
            Console.WriteLine($"[OTP] {email} → {plainOtp}");

        var firstName = SanitizeFirstName(effectiveIsCoach
            ? user.Coach?.CoachFirstName
            : user.Member?.MemberFirstName);
        var subject = firstName == null
            ? "Votre code Mentora"
            : $"{firstName}, votre code Mentora";
        _ = await emailSender.SendAsync(
            EmailMessage.ForRecipient(
                user.UserEmail,
                subject,
                BuildOtpHtmlBody(plainOtp, OtpLifetimeMinutes),
                BuildOtpTextBody(plainOtp, OtpLifetimeMinutes)),
            CancellationToken.None);
    }

    /// <summary>
    /// Trims, drops control characters (CR, LF, tab, any char.IsControl), caps at 50 characters.
    /// Returns null when nothing is left (first name unknown).
    /// </summary>
    internal static string? SanitizeFirstName(string? firstName)
    {
        if (firstName == null)
            return null;
        var cleaned = new string(firstName.Trim().Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (cleaned.Length > 50)
            cleaned = cleaned[..50].TrimEnd();
        return cleaned.Length == 0 ? null : cleaned;
    }

    public async Task<AuthResponse> VerifyOtpAsync(string email, string code, bool? isCoach)
    {
        email = EmailNormalizer.Normalize(email); // Lot 14.1 (14.A3), same rule as the request

        var user = await db.Users
            .Include(u => u.Member)
            .Include(u => u.Coach)
            .FirstOrDefaultAsync(u => u.UserEmail.ToLower() == email && u.UserIsEnabled)
            ?? throw new InvalidOperationException("User not found or account is disabled.");

        // Load candidates into memory: BCrypt.Verify cannot be translated to SQL.
        // Lot 14.2 (14.C2): a code that reached OtpMaxAttempts failures is not a candidate any more.
        var candidates = await db.AuthOtps
            .AsNoTracking()
            .Where(o => o.UserId == user.UserId && !o.AuthOtpIsUsed && o.AuthOtpExpirationDate > DateTime.UtcNow
                        && o.AuthOtpAttemptCount < OtpMaxAttempts)
            .OrderByDescending(o => o.AuthOtpCreatedDate)
            .ToListAsync();

        // Review wave 1 (W2): an attempt is claimed BEFORE the guess is checked, so parallel requests cannot
        // evaluate more than OtpMaxAttempts guesses per code, whatever their result. Each claim is one
        // conditional statement, committed on its own (no transaction is open here): a wrong guess keeps
        // its attempt. The count only grows and never passes OtpMaxAttempts (CK_AUTH_OTPS_ATTEMPT_COUNT is >= 0).
        AuthOtp? validOtp = null;
        foreach (var candidate in candidates)
        {
            var claimed = await db.AuthOtps
                .Where(o => o.AuthOtpId == candidate.AuthOtpId && !o.AuthOtpIsUsed
                            && o.AuthOtpAttemptCount < OtpMaxAttempts && o.AuthOtpExpirationDate > DateTime.UtcNow)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.AuthOtpAttemptCount, o => o.AuthOtpAttemptCount + 1));
            if (claimed == 0)
                continue; // no attempt left (or consumed, or expired meanwhile): not evaluated

            if (BCrypt.Net.BCrypt.Verify(code, candidate.AuthOtpCodeHash))
            {
                validOtp = candidate;
                break;
            }
        }
        if (validOtp == null)
            throw new InvalidOperationException("Invalid or expired OTP.");

        var effectiveIsCoach = ResolveEffectiveIsCoach(user, isCoach, email);
        var role = effectiveIsCoach ? UserRole.Coach : UserRole.Member;

        // Lot 14.2 (14.C4): consumption, activation and refresh token in ONE transaction. Disposed without
        // commit (exception), everything rolls back: the code is not consumed if the save fails.
        await using var tx = await db.Database.BeginTransactionAsync();

        // W2: this attempt is already counted, so the bound is <= (the claimed 5th attempt may still succeed).
        var consumed = await db.AuthOtps
            .Where(o => o.AuthOtpId == validOtp.AuthOtpId && !o.AuthOtpIsUsed
                        && o.AuthOtpAttemptCount <= OtpMaxAttempts && o.AuthOtpExpirationDate > DateTime.UtcNow)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.AuthOtpIsUsed, true));
        if (consumed == 0)
            throw new InvalidOperationException("Invalid or expired OTP."); // lost the race: same answer as a wrong code

        // Lot 14.1 (14.A10): first member login marks the account as activated, in the same save as the
        // OTP consumption (CreateRefreshTokenAsync flushes every tracked change). Coach login unchanged.
        if (!effectiveIsCoach && !user.Member!.MemberHasActivated)
        {
            user.Member.MemberHasActivated = true;
            user.Member.MemberActivationDate = DateTime.UtcNow;

            // Lot 14.9b: MEMBER_JOINED rows for every linked coach, saved with the activation (same transaction).
            // The flag flips once, so the event fires once per member.
            await notifications.AddMemberJoinedAsync(
                user.Member.MemberId, user.Member.MemberFirstName, user.Member.MemberLastName, CancellationToken.None);
        }

        var (clientToken, _) = await CreateRefreshTokenAsync(user.UserId, role);
        await db.SaveChangesAsync();
        await tx.CommitAsync();

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
                r.AuthRefreshTokenExpirationDate > DateTime.UtcNow &&
                r.User.UserIsEnabled) // Lot 12.1 (12.D) — a disabled user cannot renew
            ?? throw new UnauthorizedException("Invalid or expired refresh token.");

        if (!BCrypt.Net.BCrypt.Verify(rawSecret, record.AuthRefreshTokenHash))
            throw new UnauthorizedException("Invalid or expired refresh token.");

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
            ExpiresIn: _jwt.AccessTokenExpirationMinutes * 60,
            IsCoach: record.AuthRefreshTokenUserRole == UserRole.Coach
        );
    }

    public async Task LogoutAsync(Guid userId, LogoutRequest request, CancellationToken ct)
    {
        await logoutValidator.ValidateAndThrowAsync(request, ct);

        // Never throw for an unknown/foreign/already-revoked token — that would be an oracle.
        // Silently no-op instead; only revoke when the token both parses AND is proven to
        // belong to the calling user AND its secret verifies.
        if (TryParseClientToken(request.RefreshToken, out var tokenId, out var rawSecret))
        {
            var record = await db.AuthRefreshTokens
                .FirstOrDefaultAsync(r => r.AuthRefreshTokenId == tokenId && r.UserId == userId, ct);

            if (record is not null
                && !record.AuthRefreshTokenIsRevoked
                && BCrypt.Net.BCrypt.Verify(rawSecret, record.AuthRefreshTokenHash))
            {
                record.AuthRefreshTokenIsRevoked = true;
                record.AuthRefreshTokenRevokedDate = DateTime.UtcNow;
            }
        }

        if (!string.IsNullOrWhiteSpace(request.DeviceToken))
        {
            await db.UserDevices
                .Where(d => d.UserDeviceToken == request.DeviceToken && d.UserId == userId)
                .ExecuteDeleteAsync(ct);
        }

        await db.SaveChangesAsync(ct);
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
        if (!TryParseClientToken(token, out var tokenId, out var rawSecret))
            throw new InvalidOperationException("Invalid or expired refresh token.");
        return (tokenId, rawSecret);
    }

    private static bool TryParseClientToken(string token, out Guid tokenId, out string rawSecret)
    {
        tokenId = Guid.Empty;
        rawSecret = "";

        var sep = token.IndexOf(':');
        if (sep < 1 || !Guid.TryParse(token[..sep], out tokenId))
            return false;

        rawSecret = token[(sep + 1)..];
        return true;
    }

    // Lot 12.1 — uniform over [100000, 999999]; the former Math.Abs(int.MinValue) path could overflow.
    private static string GenerateOtp()
        => RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString();

    private static string GenerateSecureRandom()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static string BuildOtpHtmlBody(string otpCode, int lifetimeMinutes) => $@"
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
  <p>Ce code expire dans {lifetimeMinutes} minutes.</p>
  <p style=""color: #666; font-size: 13px; margin-top: 32px;"">
    Si vous n'avez pas demandé ce code, ignorez ce message.
  </p>
</body>
</html>";

    private static string BuildOtpTextBody(string otpCode, int lifetimeMinutes) => $@"Mentora

Bonjour,

Voici votre code de connexion : {otpCode}

Ce code expire dans {lifetimeMinutes} minutes.

Si vous n'avez pas demandé ce code, ignorez ce message.";
}
