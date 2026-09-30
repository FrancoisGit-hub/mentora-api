using FluentValidation;
using Mentora.Core.DTOs.Quiz;
using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Mentora.Core.Exceptions;
using Mentora.Core.Interfaces;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Mentora.Infrastructure.Services.Quiz;

public class QuizDeviceClaimService(
    MentoraDbContext db,
    IValidator<QuizClaimDeviceRequest> validator) : IQuizDeviceClaimService
{
    public async Task<QuizClaimDeviceResponse> ClaimAsync(
        Guid coachId, QuizClaimDeviceRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var deviceId = request.DeviceId!;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var existing = await db.QuizDeviceClaims.AsNoTracking()
            .FirstOrDefaultAsync(c => c.QuizDeviceClaimDeviceId == deviceId, ct);
        if (existing is not null)
            return await ExistingClaimAsync(existing, coachId, ct);

        // Lock the device's unclaimed sessions (any status) so a concurrent submission cannot
        // complete one of them between the read and the update (points would then be undercounted).
        var sessions = await db.QuizSessions
            .FromSqlInterpolated($"""
                SELECT * FROM "QUIZ_SESSIONS"
                WHERE "QUIZ_SESSION_DEVICE_ID" = {deviceId} AND "QUIZ_SESSION_COACH_ID" IS NULL
                FOR UPDATE
                """)
            .ToListAsync(ct);

        if (sessions.Count == 0)
        {
            // A concurrent claim of the same device may have committed while we waited for the lock:
            // the rows are then already attributed, which is "claimed", not "nothing to claim".
            existing = await db.QuizDeviceClaims.AsNoTracking()
                .FirstOrDefaultAsync(c => c.QuizDeviceClaimDeviceId == deviceId, ct);
            if (existing is not null)
                return await ExistingClaimAsync(existing, coachId, ct);

            throw new ConflictException("No session to claim for this device.", new { reason = "NOTHING_TO_CLAIM" });
        }

        var now = DateTime.UtcNow;
        var completed = sessions.Where(s => s.QuizSessionStatus == QuizSessionStatus.Completed).ToList();
        var claimedPoints = completed.Sum(s => s.QuizSessionScore ?? 0);

        foreach (var session in sessions)
        {
            session.QuizSessionCoachId     = coachId;   // DEVICE_ID is kept
            session.QuizSessionUpdatedDate = now;
        }

        db.QuizDeviceClaims.Add(new QuizDeviceClaim
        {
            QuizDeviceClaimId          = Guid.NewGuid(),
            QuizDeviceClaimDeviceId    = deviceId,
            QuizDeviceClaimCoachId     = coachId,
            QuizDeviceClaimPoints      = claimedPoints,
            QuizDeviceClaimCreatedDate = now,
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("Device already claimed.", new { reason = "ALREADY_CLAIMED" });
        }

        var totalPoints = await CoachTotalAsync(coachId, ct);
        await transaction.CommitAsync(ct);

        return new QuizClaimDeviceResponse(deviceId, claimedPoints, completed.Count, totalPoints);
    }

    /// <summary>Same coach: idempotent retry with the values of the claim. Another coach: 409.</summary>
    private async Task<QuizClaimDeviceResponse> ExistingClaimAsync(QuizDeviceClaim claim, Guid coachId, CancellationToken ct)
    {
        if (claim.QuizDeviceClaimCoachId != coachId)
            throw new ConflictException("Device already claimed.", new { reason = "ALREADY_CLAIMED" });

        var deviceId = claim.QuizDeviceClaimDeviceId;
        var claimedSessions = await db.QuizSessions.AsNoTracking()
            .CountAsync(s => s.QuizSessionDeviceId == deviceId
                          && s.QuizSessionCoachId == coachId
                          && s.QuizSessionStatus == QuizSessionStatus.Completed, ct);

        return new QuizClaimDeviceResponse(deviceId, claim.QuizDeviceClaimPoints, claimedSessions,
            await CoachTotalAsync(coachId, ct));
    }

    private Task<int> CoachTotalAsync(Guid coachId, CancellationToken ct) =>
        db.QuizSessions.AsNoTracking()
            .Where(s => s.QuizSessionCoachId == coachId && s.QuizSessionStatus == QuizSessionStatus.Completed)
            .SumAsync(s => s.QuizSessionScore ?? 0, ct);
}
