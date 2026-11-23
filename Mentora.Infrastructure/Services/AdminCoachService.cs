using System.Globalization;
using System.Text;
using System.Text.Json;
using FluentValidation;
using Mentora.Core;
using Mentora.Core.DTOs.AdminCoach;
using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Mentora.Core.Exceptions;
using Mentora.Core.Interfaces;
using Mentora.Core.Options;
using Mentora.Infrastructure.Persistence;
using Mentora.Infrastructure.Seeding;
using Mentora.Infrastructure.Services.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Mentora.Infrastructure.Services;

/// <summary>
/// Lot 14.8a: the Mentora team lists and creates coach accounts. A creation is one transaction serialized by the
/// same advisory lock as the member add (UserEmailLock), writes the user, the coach, its parameters, its four
/// default offer programs and one ADMIN_AUDIT_LOG row in a single SaveChanges, then sends the welcome mail after
/// the commit, best effort. Any existing USERS row with the email is a 409: it is the only way to never produce
/// an account that is both coach and member.
/// </summary>
public class AdminCoachService(
    MentoraDbContext db,
    IValidator<AdminCreateCoachRequest> validator,
    IEmailSender emailSender,
    IOptions<MemberInviteOptions> inviteOptions,
    ILogger<AdminCoachService> logger) : IAdminCoachService
{
    // The existing coach rows store the role in upper case, like the member ones.
    private const string CoachUserRole = "COACH";

    // ── List ───────────────────────────────────────────────────────────────────

    public async Task<AdminCoachListResponse> ListAsync(int limit, string? cursor, CancellationToken ct)
    {
        if (limit is < 1 or > 100)
            throw new InvalidOperationException($"limit must be between 1 and 100 (got {limit}).");

        IQueryable<Coach> query = db.Coaches.AsNoTracking();

        if (cursor is not null)
        {
            var (cursorDate, cursorId) = DecodeCursor(cursor);
            query = query.Where(c => c.CoachCreatedDate < cursorDate
                                  || (c.CoachCreatedDate == cursorDate && c.CoachId.CompareTo(cursorId) < 0));
        }

        // One SQL query per page: the member count is a correlated subquery.
        var rows = await query
            .OrderByDescending(c => c.CoachCreatedDate)
            .ThenByDescending(c => c.CoachId)
            .Take(limit + 1)
            .Select(c => new AdminCoachListItem(
                c.CoachId,
                c.UserId,
                c.User.UserEmail,
                c.CoachFirstName,
                c.CoachLastName,
                c.CoachPhone,
                c.CoachCreatedDate,
                c.User.UserIsEnabled,
                c.CoachIsActive,
                c.MemberCoaches.Count(mc => mc.Member.MemberIsActive)))
            .ToListAsync(ct);

        var hasNext = rows.Count > limit;
        var items = hasNext ? rows.Take(limit).ToList() : rows;
        var nextCursor = hasNext ? EncodeCursor(items[^1].CreatedDate, items[^1].CoachId) : null;

        return new AdminCoachListResponse(items, nextCursor);
    }

    // ── Create ─────────────────────────────────────────────────────────────────

    public async Task<AdminCoachListItem> CreateAsync(
        AdminCreateCoachRequest request, Guid adminUserId, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);

        var email = EmailNormalizer.Normalize(request.Email);
        var firstName = request.FirstName!.Trim();
        var lastName = request.LastName!.Trim();
        var phone = PhoneNormalizer.Normalize(request.Phone);

        AdminCoachListItem created;
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            // First statement of the transaction: everything below sees the committed result of any earlier writer.
            await db.Database.ExecuteSqlRawAsync(UserEmailLock.Sql, ct);

            await EnsureAdminAsync(adminUserId, ct);

            var existing = await db.Users
                .AsNoTracking()
                .Where(u => u.UserEmail.ToLower() == email)
                .Select(u => new { u.UserIsEnabled, HasCoach = u.Coach != null, HasMember = u.Member != null })
                .FirstOrDefaultAsync(ct);
            if (existing is not null)
            {
                var account = existing.HasCoach && existing.HasMember ? "BOTH"
                    : existing.HasCoach ? "COACH"
                    : existing.HasMember ? "MEMBER"
                    : "NONE";
                throw new ConflictException("EMAIL_TAKEN", new { existingAccount = account, isEnabled = existing.UserIsEnabled });
            }

            // Whole microseconds, the precision of timestamptz: the 201, the rows and the audit row carry the same value.
            var now = DateTime.UtcNow;
            now = now.AddTicks(-(now.Ticks % 10));
            var userId = Guid.NewGuid();
            var coachId = Guid.NewGuid();

            db.Users.Add(new User
            {
                UserId = userId,
                UserCreatedDate = now,
                UserEmail = email,
                UserIsEnabled = true,
                UserRole = CoachUserRole
            });
            db.Coaches.Add(new Coach
            {
                CoachId = coachId,
                UserId = userId,
                CoachFirstName = firstName,
                CoachLastName = lastName,
                CoachPhone = phone,
                CoachCreatedDate = now,
                CoachIsActive = true
            });
            db.CoachParameters.Add(new CoachParameter
            {
                CoachParameterId = Guid.NewGuid(),
                CoachId = coachId,
                CoachParameterHourlyRateEuros = CoachDefaults.HourlyRateEuros,
                CoachParameterCancellationDelayHours = CoachDefaults.CancellationDelayHours,
                CoachParameterCreatedDate = now,
                CoachParameterUpdatedDate = now
            });
            foreach (var name in CoachDefaults.OfferProgramNames)
            {
                db.OfferPrograms.Add(new OfferProgram
                {
                    OfferProgramId = Guid.NewGuid(),
                    CoachId = coachId,
                    OfferProgramName = name,
                    OfferProgramIsActive = true,
                    OfferProgramCreatedDate = now
                });
            }

            var changes = new Dictionary<string, object?>
            {
                ["userId"] = userId,
                ["coachId"] = coachId,
                ["email"] = email,
                ["firstName"] = firstName,
                ["lastName"] = lastName,
                ["phone"] = phone,
                ["isActive"] = true,
                ["hourlyRateEuros"] = CoachDefaults.HourlyRateEuros,
                ["cancellationDelayHours"] = CoachDefaults.CancellationDelayHours,
                ["offerPrograms"] = CoachDefaults.OfferProgramNames
            };
            db.AdminAuditLogs.Add(NewAudit(adminUserId, AdminAuditAction.CoachCreated, coachId, changes, now));

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            created = new AdminCoachListItem(
                coachId, userId, email, firstName, lastName, phone, now,
                IsEnabled: true, IsActive: true, MemberCount: 0);
        }
        catch (DbUpdateException ex) when (IsEmailViolation(ex))
        {
            // Safety net: a writer that bypassed the lock. The transaction is rolled back on dispose.
            db.ChangeTracker.Clear();
            throw new ConflictException("EMAIL_TAKEN", new { existingAccount = (string?)null, isEnabled = (bool?)null });
        }

        await SendWelcomeMailAsync(created, adminUserId);
        return created;
    }

    // ── Welcome mail ───────────────────────────────────────────────────────────

    /// <summary>
    /// Best effort: a failure here never changes the 201. One warning with ids only, never the address or the body.
    /// RoutingEmailSender applies TEST/PRODUCTION routing.
    /// </summary>
    private async Task SendWelcomeMailAsync(AdminCoachListItem coach, Guid adminUserId)
    {
        try
        {
            var firstName = AuthService.SanitizeFirstName(coach.FirstName);
            var installUrl = inviteOptions.Value.HasInstallUrl ? inviteOptions.Value.InstallUrl!.Trim() : null;

            var sent = await emailSender.SendAsync(
                EmailMessage.ForRecipient(
                    coach.Email,
                    CoachWelcomeEmailBuilder.BuildSubject(firstName),
                    CoachWelcomeEmailBuilder.BuildHtml(firstName, coach.Email, installUrl),
                    CoachWelcomeEmailBuilder.BuildText(firstName, coach.Email, installUrl)),
                CancellationToken.None);

            if (!sent)
                logger.LogWarning("Coach welcome mail was not sent. CoachId={CoachId} AdminUserId={AdminUserId}",
                    coach.CoachId, adminUserId);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Coach welcome mail failed ({ExceptionType}). CoachId={CoachId} AdminUserId={AdminUserId}",
                ex.GetType().Name, coach.CoachId, adminUserId);
        }
    }

    // ── Admin and audit ────────────────────────────────────────────────────────

    /// <summary>The token of a disabled or deleted admin stays valid until it expires: refuse its writes.</summary>
    private async Task EnsureAdminAsync(Guid adminUserId, CancellationToken ct)
    {
        var ok = await db.AdminUsers.AnyAsync(a => a.AdminUserId == adminUserId && a.AdminUserIsEnabled, ct);
        if (!ok)
            throw new UnauthorizedException("Admin account not found or disabled.");
    }

    private static AdminAuditLog NewAudit(
        Guid adminUserId, AdminAuditAction action, Guid coachId, object changes, DateTime now) => new()
    {
        AdminAuditLogId          = Guid.NewGuid(),
        AdminAuditLogAdminUserId = adminUserId,
        AdminAuditLogAction      = action,
        AdminAuditLogEntityType  = AdminAuditEntityType.Coach,
        AdminAuditLogEntityId    = coachId,
        AdminAuditLogChanges     = JsonSerializer.Serialize(changes),
        AdminAuditLogCreatedDate = now,
    };

    private static bool IsEmailViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException pg
        && pg.SqlState == PostgresErrorCodes.UniqueViolation
        && (pg.ConstraintName == "UX_USERS_EMAIL_CI" || pg.ConstraintName == "IX_USERS_USER_EMAIL");

    // ── Cursor ─────────────────────────────────────────────────────────────────

    private sealed record CursorPayload(string CreatedDate, Guid CoachId);

    private static string EncodeCursor(DateTime createdDate, Guid coachId)
    {
        var payload = JsonSerializer.Serialize(
            new CursorPayload(createdDate.ToString("O", CultureInfo.InvariantCulture), coachId));
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
    }

    private static (DateTime CreatedDate, Guid CoachId) DecodeCursor(string cursor)
    {
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var payload = JsonSerializer.Deserialize<CursorPayload>(json);
            if (payload is null || payload.CreatedDate is null
                || !DateTime.TryParse(payload.CreatedDate, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var date))
                throw new InvalidOperationException("Invalid cursor format.");

            // timestamptz parameters need a UTC value.
            date = date.Kind == DateTimeKind.Utc ? date
                : date.Kind == DateTimeKind.Local ? date.ToUniversalTime()
                : DateTime.SpecifyKind(date, DateTimeKind.Utc);
            return (date, payload.CoachId);
        }
        catch (FormatException)   { throw new InvalidOperationException("Invalid cursor format."); }
        catch (JsonException)     { throw new InvalidOperationException("Invalid cursor format."); }
        catch (ArgumentException) { throw new InvalidOperationException("Invalid cursor format."); }
    }
}
