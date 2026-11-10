using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using Mentora.Core.DTOs.Admin.Auth;
using Mentora.Core.Entities;
using Mentora.Core.Exceptions;
using Mentora.Core.Interfaces;
using Mentora.Core.Options;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Mentora.Infrastructure.Services.Admin;

/// <summary>
/// Lot 12.1 — admin back-office login: a 6-digit code sent by mail (IEmailSender, so the 11.4
/// routing applies), a short JWT signed with AdminAuth:JwtSecret, an opaque rotating refresh token.
/// Never writes a code, a token or an address to the console or the logs.
/// Independent from AuthService on purpose (small helpers are duplicated).
/// </summary>
public class AdminAuthService(
    MentoraDbContext db,
    IOptions<AdminAuthOptions> options,
    IEmailSender emailSender,
    IValidator<AdminRequestCodeRequest> requestCodeValidator,
    IValidator<AdminVerifyCodeRequest> verifyCodeValidator) : IAdminAuthService
{
    public const string InvalidCodeMessage = "Invalid or expired code.";
    public const string InvalidRefreshMessage = "Invalid or expired refresh token.";
    public const string AdminIdClaim = "adminId";

    // Hash compared against when no admin matches, so that a miss costs one BCrypt verify too.
    private static readonly Lazy<string> DummyHash = new(() => BCrypt.Net.BCrypt.HashPassword(GenerateCode()));

    private readonly AdminAuthOptions _opts = options.Value;

    public async Task RequestCodeAsync(AdminRequestCodeRequest request, CancellationToken ct)
    {
        await requestCodeValidator.ValidateAndThrowAsync(request, ct);
        var email = Normalize(request.Email);
        var now = DateTime.UtcNow;

        var admin = await db.AdminUsers
            .FirstOrDefaultAsync(a => a.AdminUserEmail == email && a.AdminUserIsEnabled, ct);

        if (admin is null)
        {
            // Same BCrypt cost as the real path; the response never tells the two cases apart.
            _ = BCrypt.Net.BCrypt.HashPassword(GenerateCode());
            return;
        }

        var previous = await db.AdminOtps
            .Where(o => o.AdminOtpAdminUserId == admin.AdminUserId && !o.AdminOtpIsUsed && o.AdminOtpExpirationDate > now)
            .ToListAsync(ct);
        foreach (var old in previous)
            old.AdminOtpIsUsed = true;

        var code = GenerateCode();
        db.AdminOtps.Add(new AdminOtp
        {
            AdminOtpAdminUserId    = admin.AdminUserId,
            AdminOtpCodeHash       = BCrypt.Net.BCrypt.HashPassword(code),
            AdminOtpExpirationDate = now.AddMinutes(_opts.OtpLifetimeMinutes),
            AdminOtpIsUsed         = false,
            AdminOtpFailedAttempts = 0,
            AdminOtpCreatedDate    = now
        });
        await db.SaveChangesAsync(ct);

        _ = await emailSender.SendAsync(
            EmailMessage.ForRecipient(
                admin.AdminUserEmail,
                "Votre code admin Mentora",
                BuildCodeHtmlBody(code, _opts.OtpLifetimeMinutes),
                BuildCodeTextBody(code, _opts.OtpLifetimeMinutes)),
            ct);
    }

    public async Task<AdminAuthResponse> VerifyCodeAsync(AdminVerifyCodeRequest request, CancellationToken ct)
    {
        await verifyCodeValidator.ValidateAndThrowAsync(request, ct);
        var email = Normalize(request.Email);
        var now = DateTime.UtcNow;

        var admin = await db.AdminUsers
            .FirstOrDefaultAsync(a => a.AdminUserEmail == email && a.AdminUserIsEnabled, ct);
        if (admin is null)
        {
            _ = BCrypt.Net.BCrypt.Verify(request.Code, DummyHash.Value);
            throw new InvalidOperationException(InvalidCodeMessage);
        }

        // BCrypt.Verify cannot be translated to SQL: load the live candidates, newest first.
        var max = _opts.OtpMaxAttempts;
        var candidates = await db.AdminOtps
            .Where(o => o.AdminOtpAdminUserId == admin.AdminUserId
                        && !o.AdminOtpIsUsed
                        && o.AdminOtpExpirationDate > now
                        && o.AdminOtpFailedAttempts < max)
            .OrderByDescending(o => o.AdminOtpCreatedDate)
            .AsNoTracking()
            .ToListAsync(ct);

        var valid = candidates.FirstOrDefault(o => BCrypt.Net.BCrypt.Verify(request.Code, o.AdminOtpCodeHash));
        if (valid is null)
        {
            if (candidates.Count > 0)
            {
                var ids = candidates.Select(o => o.AdminOtpId).ToList();
                // Single UPDATE statements: concurrent wrong attempts cannot lose an increment.
                await db.AdminOtps
                    .Where(o => ids.Contains(o.AdminOtpId) && !o.AdminOtpIsUsed)
                    .ExecuteUpdateAsync(s => s.SetProperty(o => o.AdminOtpFailedAttempts, o => o.AdminOtpFailedAttempts + 1), ct);
                await db.AdminOtps
                    .Where(o => ids.Contains(o.AdminOtpId) && !o.AdminOtpIsUsed && o.AdminOtpFailedAttempts >= max)
                    .ExecuteUpdateAsync(s => s.SetProperty(o => o.AdminOtpIsUsed, true), ct);
            }
            else
            {
                _ = BCrypt.Net.BCrypt.Verify(request.Code, DummyHash.Value);
            }
            throw new InvalidOperationException(InvalidCodeMessage);
        }

        // Consume the code atomically: of two concurrent verifies of the same code, only one wins.
        var consumed = await db.AdminOtps
            .Where(o => o.AdminOtpId == valid.AdminOtpId && !o.AdminOtpIsUsed && o.AdminOtpFailedAttempts < max)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.AdminOtpIsUsed, true), ct);
        if (consumed != 1)
            throw new InvalidOperationException(InvalidCodeMessage);

        admin.AdminUserLastLoginDate = now;
        var refreshToken = await CreateRefreshTokenAsync(admin.AdminUserId, now, ct);
        return BuildResponse(admin, refreshToken, now);
    }

    public async Task<AdminAuthResponse> RefreshAsync(AdminRefreshRequest request, CancellationToken ct)
    {
        if (!TryParseClientToken(request.RefreshToken, out var tokenId, out var rawSecret))
            throw new UnauthorizedException(InvalidRefreshMessage);

        var now = DateTime.UtcNow;
        var record = await db.AdminRefreshTokens
            .Include(r => r.AdminUser)
            .FirstOrDefaultAsync(r => r.AdminRefreshTokenId == tokenId, ct);

        if (record is null || !BCrypt.Net.BCrypt.Verify(rawSecret, record.AdminRefreshTokenHash))
            throw new UnauthorizedException(InvalidRefreshMessage);

        if (record.AdminRefreshTokenIsRevoked)
        {
            // Reuse of a consumed token proven by its secret: assume theft, end every session of that admin.
            await RevokeAllActiveAsync(record.AdminRefreshTokenAdminUserId, now, ct);
            throw new UnauthorizedException(InvalidRefreshMessage);
        }

        if (record.AdminRefreshTokenExpirationDate <= now || !record.AdminUser.AdminUserIsEnabled)
            throw new UnauthorizedException(InvalidRefreshMessage);

        // Consume atomically: a concurrent refresh with the same token cannot rotate twice.
        var consumed = await db.AdminRefreshTokens
            .Where(r => r.AdminRefreshTokenId == record.AdminRefreshTokenId && !r.AdminRefreshTokenIsRevoked)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.AdminRefreshTokenIsRevoked, true)
                .SetProperty(r => r.AdminRefreshTokenRevokedDate, now), ct);
        if (consumed != 1)
        {
            await RevokeAllActiveAsync(record.AdminRefreshTokenAdminUserId, now, ct);
            throw new UnauthorizedException(InvalidRefreshMessage);
        }

        var newToken = await CreateRefreshTokenAsync(record.AdminRefreshTokenAdminUserId, now, ct);
        return BuildResponse(record.AdminUser, newToken, now);
    }

    public async Task LogoutAsync(AdminLogoutRequest request, CancellationToken ct)
    {
        // Never throws for a missing, malformed, unknown or already revoked token (no oracle).
        if (!TryParseClientToken(request.RefreshToken, out var tokenId, out var rawSecret))
            return;

        var record = await db.AdminRefreshTokens
            .FirstOrDefaultAsync(r => r.AdminRefreshTokenId == tokenId, ct);
        if (record is null
            || record.AdminRefreshTokenIsRevoked
            || !BCrypt.Net.BCrypt.Verify(rawSecret, record.AdminRefreshTokenHash))
            return;

        record.AdminRefreshTokenIsRevoked = true;
        record.AdminRefreshTokenRevokedDate = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<AdminMeResponse> GetMeAsync(Guid adminUserId, CancellationToken ct)
    {
        var admin = await db.AdminUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.AdminUserId == adminUserId && a.AdminUserIsEnabled, ct)
            ?? throw new UnauthorizedException("Admin account not found or disabled.");
        return new AdminMeResponse(admin.AdminUserId, admin.AdminUserEmail);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();

    private Task<int> RevokeAllActiveAsync(Guid adminUserId, DateTime now, CancellationToken ct)
        => db.AdminRefreshTokens
            .Where(r => r.AdminRefreshTokenAdminUserId == adminUserId && !r.AdminRefreshTokenIsRevoked)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.AdminRefreshTokenIsRevoked, true)
                .SetProperty(r => r.AdminRefreshTokenRevokedDate, now), ct);

    private AdminAuthResponse BuildResponse(AdminUser admin, string refreshToken, DateTime now)
        => new(
            AccessToken: GenerateAccessToken(admin, now),
            ExpiresIn: _opts.AccessTokenMinutes * 60,
            RefreshToken: refreshToken,
            RefreshExpiresIn: _opts.RefreshTokenHours * 3600,
            Admin: new AdminMeResponse(admin.AdminUserId, admin.AdminUserEmail));

    /// <summary>
    /// HS256, AdminAuth issuer/audience/key. Claims: sub and adminId (admin user id), email, jti, iat.
    /// </summary>
    private string GenerateAccessToken(AdminUser admin, DateTime now)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,   admin.AdminUserId.ToString()),
            new(AdminIdClaim,                  admin.AdminUserId.ToString()),
            new(JwtRegisteredClaimNames.Email, admin.AdminUserEmail),
            new(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat,   new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opts.JwtSecret));
        var token = new JwtSecurityToken(
            issuer:             _opts.JwtIssuer,
            audience:           _opts.JwtAudience,
            claims:             claims,
            notBefore:          now,
            expires:            now.AddMinutes(_opts.AccessTokenMinutes),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // Saves pending changes (LastLoginDate included) and returns "{id}:{secret}".
    private async Task<string> CreateRefreshTokenAsync(Guid adminUserId, DateTime now, CancellationToken ct)
    {
        var rawSecret = GenerateSecureRandom();
        var entity = new AdminRefreshToken
        {
            AdminRefreshTokenAdminUserId    = adminUserId,
            AdminRefreshTokenHash           = BCrypt.Net.BCrypt.HashPassword(rawSecret),
            AdminRefreshTokenExpirationDate = now.AddHours(_opts.RefreshTokenHours),
            AdminRefreshTokenIsRevoked      = false,
            AdminRefreshTokenCreatedDate    = now
        };
        db.AdminRefreshTokens.Add(entity);
        await db.SaveChangesAsync(ct);
        return $"{entity.AdminRefreshTokenId}:{rawSecret}";
    }

    private static bool TryParseClientToken(string? token, out Guid tokenId, out string rawSecret)
    {
        tokenId = Guid.Empty;
        rawSecret = "";
        if (string.IsNullOrEmpty(token))
            return false;

        var sep = token.IndexOf(':');
        if (sep < 1 || sep == token.Length - 1 || !Guid.TryParse(token[..sep], out tokenId))
            return false;

        rawSecret = token[(sep + 1)..];
        return true;
    }

    private static string GenerateCode()
        => RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString();

    private static string GenerateSecureRandom()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static string BuildCodeHtmlBody(string code, int lifetimeMinutes) => $@"
<!DOCTYPE html>
<html>
<head><meta charset=""utf-8""></head>
<body style=""font-family: Arial, sans-serif; color: #1a1a1a; max-width: 560px; margin: 0 auto; padding: 24px;"">
  <h1 style=""color: #1a1a1a; font-size: 20px;"">Mentora: Administration</h1>
  <p>Bonjour,</p>
  <p>Voici votre code de connexion à l'administration Mentora :</p>
  <p style=""font-size: 32px; font-weight: bold; letter-spacing: 4px; background: #f4f4f4; padding: 16px 24px; display: inline-block; border-radius: 8px;"">
    {code}
  </p>
  <p>Ce code expire dans {lifetimeMinutes} minutes et ne peut servir qu'une seule fois.</p>
  <p style=""color: #666; font-size: 13px; margin-top: 32px;"">
    Si vous n'avez pas demandé ce code, ignorez ce message et prévenez les autres associés.
  </p>
</body>
</html>";

    private static string BuildCodeTextBody(string code, int lifetimeMinutes) => $@"Mentora: Administration

Bonjour,

Voici votre code de connexion à l'administration Mentora : {code}

Ce code expire dans {lifetimeMinutes} minutes et ne peut servir qu'une seule fois.

Si vous n'avez pas demandé ce code, ignorez ce message et prévenez les autres associés.";
}
