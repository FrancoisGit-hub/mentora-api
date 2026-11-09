using Mentora.Core.Entities;
using Mentora.Core.Options;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mentora.Infrastructure.Seeding;

/// <summary>
/// Lot 12.1 — AdminAuth:Emails is the reference for ADMIN_USERS at each startup (runs after
/// QuizQuestionSeeder):
/// - a listed address without an account gets one (enabled);
/// - a listed but disabled account is re-enabled;
/// - an enabled account that is no longer listed is disabled, and its live refresh tokens and
///   unused codes are revoked.
/// One transaction, one SaveChanges. The log line carries counts only, never an address.
/// </summary>
public static class AdminUserSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var db      = services.GetRequiredService<MentoraDbContext>();
        var logger  = services.GetRequiredService<ILogger<MentoraDbContext>>();
        var options = services.GetRequiredService<IOptions<AdminAuthOptions>>().Value;

        var listed = options.EmailList.Distinct(StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        var now = DateTime.UtcNow;
        int created = 0, reenabled = 0, disabled = 0;

        await using var tx = await db.Database.BeginTransactionAsync();

        var admins = await db.AdminUsers.ToListAsync();
        var byEmail = admins.ToDictionary(a => a.AdminUserEmail, StringComparer.Ordinal);

        foreach (var email in listed)
        {
            if (!byEmail.TryGetValue(email, out var admin))
            {
                db.AdminUsers.Add(new AdminUser
                {
                    AdminUserEmail       = email,
                    AdminUserIsEnabled   = true,
                    AdminUserCreatedDate = now,
                    AdminUserUpdatedDate = now
                });
                created++;
            }
            else if (!admin.AdminUserIsEnabled)
            {
                admin.AdminUserIsEnabled   = true;
                admin.AdminUserUpdatedDate = now;
                reenabled++;
            }
        }

        var toDisable = admins.Where(a => a.AdminUserIsEnabled && !listed.Contains(a.AdminUserEmail)).ToList();
        if (toDisable.Count > 0)
        {
            var ids = toDisable.Select(a => a.AdminUserId).ToList();
            foreach (var admin in toDisable)
            {
                admin.AdminUserIsEnabled   = false;
                admin.AdminUserUpdatedDate = now;
                disabled++;
            }

            var liveTokens = await db.AdminRefreshTokens
                .Where(t => ids.Contains(t.AdminRefreshTokenAdminUserId) && !t.AdminRefreshTokenIsRevoked)
                .ToListAsync();
            foreach (var token in liveTokens)
            {
                token.AdminRefreshTokenIsRevoked   = true;
                token.AdminRefreshTokenRevokedDate = now;
            }

            var liveCodes = await db.AdminOtps
                .Where(o => ids.Contains(o.AdminOtpAdminUserId) && !o.AdminOtpIsUsed)
                .ToListAsync();
            foreach (var code in liveCodes)
                code.AdminOtpIsUsed = true;
        }

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        logger.LogInformation(
            "[ADMIN] accounts listed={Listed} created={Created} reenabled={Reenabled} disabled={Disabled}",
            listed.Count, created, reenabled, disabled);
    }
}
