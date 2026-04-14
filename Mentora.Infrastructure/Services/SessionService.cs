using Mentora.Core.DTOs.Sessions;
using Mentora.Core.Entities;
using Mentora.Core.Interfaces;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mentora.Infrastructure.Services;

public class SessionService(MentoraDbContext db) : ISessionService
{
    public async Task<BookSessionResponse> BookSessionAsync(Guid userId, Guid coachId, Guid slotId)
    {
        var slot = await db.SessionSlots
            .Include(s => s.Session)
            .FirstOrDefaultAsync(s => s.SessionSlotId == slotId && s.CoachId == coachId)
            ?? throw new InvalidOperationException("Session slot not found.");

        if (!slot.SessionSlotIsAvailable)
            throw new InvalidOperationException("Slot already booked.");

        var balance = await db.CreditBalances
            .FirstOrDefaultAsync(b => b.UserId == userId && b.CoachId == coachId);

        if (balance == null)
        {
            balance = new CreditBalance
            {
                CreditBalanceAmount = 0,
                CreditBalanceUpdatedDate = DateTime.UtcNow,
                UserId = userId,
                CoachId = coachId
            };
            db.CreditBalances.Add(balance);
            await db.SaveChangesAsync();
        }

        if (balance.CreditBalanceAmount < slot.SessionSlotCreditsRequired)
            throw new InvalidOperationException("Insufficient credits.");

        var session = new Session
        {
            SessionStatus = "CONFIRMED",
            SessionCreditsConsumed = slot.SessionSlotCreditsRequired,
            SessionCreatedDate = DateTime.UtcNow,
            SessionSlotId = slot.SessionSlotId,
            UserId = userId,
            CoachId = coachId
        };
        db.Sessions.Add(session);

        var transaction = new CreditTransaction
        {
            CreditTransactionType = "CONSUMED",
            CreditTransactionAmount = -slot.SessionSlotCreditsRequired,
            CreditTransactionDate = DateTime.UtcNow,
            UserId = userId,
            CoachId = coachId
        };
        db.CreditTransactions.Add(transaction);

        balance.CreditBalanceAmount -= slot.SessionSlotCreditsRequired;
        balance.CreditBalanceUpdatedDate = DateTime.UtcNow;

        slot.SessionSlotIsAvailable = false;

        await db.SaveChangesAsync();

        transaction.SessionId = session.SessionId;
        await db.SaveChangesAsync();

        return new BookSessionResponse(
            SessionId: session.SessionId,
            CreditsConsumed: session.SessionCreditsConsumed,
            RemainingBalance: balance.CreditBalanceAmount,
            StartDate: slot.SessionSlotStartDate,
            EndDate: slot.SessionSlotEndDate,
            Type: slot.SessionSlotType
        );
    }

    public async Task<CancelSessionResponse> CancelSessionAsync(Guid userId, Guid coachId, Guid sessionId, string? reason)
    {
        var session = await db.Sessions
            .Include(s => s.SessionSlot)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.UserId == userId && s.CoachId == coachId)
            ?? throw new InvalidOperationException("Session not found.");

        if (session.SessionStatus != "CONFIRMED")
            throw new InvalidOperationException("Only confirmed sessions can be cancelled.");

        var parameters = await db.CoachParameters
            .FirstOrDefaultAsync(p => p.CoachId == coachId)
            ?? throw new InvalidOperationException("Coach parameters not found.");

        var timeUntilStart = session.SessionSlot.SessionSlotStartDate - DateTime.UtcNow;
        if (timeUntilStart < TimeSpan.FromHours(parameters.CoachCancellationDelayHours))
            throw new InvalidOperationException("Cancellation deadline passed.");

        session.SessionStatus = "CANCELLED";
        session.SessionCancelReason = reason;
        session.SessionCancelledDate = DateTime.UtcNow;
        session.SessionSlot.SessionSlotIsAvailable = true;

        var balance = await db.CreditBalances
            .FirstOrDefaultAsync(b => b.UserId == userId && b.CoachId == coachId)
            ?? throw new InvalidOperationException("Credit balance not found.");

        db.CreditTransactions.Add(new CreditTransaction
        {
            CreditTransactionType = "REFUNDED",
            CreditTransactionAmount = session.SessionCreditsConsumed,
            CreditTransactionDate = DateTime.UtcNow,
            SessionId = session.SessionId,
            UserId = userId,
            CoachId = coachId
        });

        balance.CreditBalanceAmount += session.SessionCreditsConsumed;
        balance.CreditBalanceUpdatedDate = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return new CancelSessionResponse(
            SessionId: session.SessionId,
            Status: session.SessionStatus,
            CreditsRefunded: session.SessionCreditsConsumed,
            RemainingBalance: balance.CreditBalanceAmount
        );
    }

    public async Task<List<SessionResponse>> GetSessionsAsync(Guid userId, Guid coachId, string? status)
    {
        var query = db.Sessions
            .Include(s => s.SessionSlot)
            .Where(s => s.UserId == userId && s.CoachId == coachId);

        if (status != null)
            query = query.Where(s => s.SessionStatus == status);

        var sessions = await query
            .OrderByDescending(s => s.SessionSlot.SessionSlotStartDate)
            .ToListAsync();

        return sessions.Select(MapToResponse).ToList();
    }

    public async Task<SessionDetailResponse> GetSessionByIdAsync(Guid userId, Guid coachId, Guid sessionId)
    {
        var session = await db.Sessions
            .Include(s => s.SessionSlot)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.UserId == userId && s.CoachId == coachId)
            ?? throw new InvalidOperationException("Session not found.");

        var coach = await db.Coaches
            .FirstOrDefaultAsync(c => c.CoachId == coachId)
            ?? throw new InvalidOperationException("Coach not found.");

        return new SessionDetailResponse(
            SessionId: session.SessionId,
            Status: session.SessionStatus,
            CreditsConsumed: session.SessionCreditsConsumed,
            Type: session.SessionSlot.SessionSlotType,
            StartDate: session.SessionSlot.SessionSlotStartDate,
            EndDate: session.SessionSlot.SessionSlotEndDate,
            CancelReason: session.SessionCancelReason,
            CancelledAt: session.SessionCancelledDate,
            BookedAt: session.SessionCreatedDate,
            CoachFirstName: coach.CoachFirstName,
            CoachLastName: coach.CoachLastName
        );
    }

    private static SessionResponse MapToResponse(Session session) =>
        new(
            SessionId: session.SessionId,
            Status: session.SessionStatus,
            CreditsConsumed: session.SessionCreditsConsumed,
            Type: session.SessionSlot.SessionSlotType,
            StartDate: session.SessionSlot.SessionSlotStartDate,
            EndDate: session.SessionSlot.SessionSlotEndDate,
            CancelReason: session.SessionCancelReason,
            CancelledAt: session.SessionCancelledDate,
            BookedAt: session.SessionCreatedDate
        );
}
