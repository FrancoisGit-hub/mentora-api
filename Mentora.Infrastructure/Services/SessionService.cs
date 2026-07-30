using System.Data;
using FluentValidation;
using Mentora.Core.DTOs.Session;
using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Mentora.Core.Exceptions;
using Mentora.Core.Interfaces;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Mentora.Infrastructure.Services;

public class SessionService(
    MentoraDbContext db,
    IValidator<ReserveSessionRequest> reserveValidator,
    IValidator<CancelSessionRequest> cancelValidator,
    IVisioUrlGenerator visioGenerator,
    ILogger<SessionService> logger) : ISessionService
{
    // ── Member-side ─────────────────────────────────────────────────────────────

    public async Task<SessionResponse> ReserveAsync(
        Guid memberId, ReserveSessionRequest request, CancellationToken ct)
    {
        await reserveValidator.ValidateAndThrowAsync(request, ct);

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, ct);

        try
        {
            // Voucher: ownership + availability
            var voucher = await db.SessionVouchers
                .FirstOrDefaultAsync(v => v.VoucherId == request.VoucherId, ct)
                ?? throw new NotFoundException("Voucher not found.");

            if (voucher.MemberId != memberId)
                throw new NotFoundException("Voucher not found.");

            if (voucher.OfferType == OfferType.PresentielGroupe)
                throw new ConflictException("PresentielGroupe not supported in V1.");

            if (voucher.Status != VoucherStatus.Available)
                throw new ConflictException(
                    "Voucher is not available for reservation.",
                    new {
                        voucherId = voucher.VoucherId,
                        status = EnumMappings.VoucherStatusMapping.ToWire(voucher.Status)
                    });

            // Slot: ownership + availability flag + booking check
            var slot = await db.SessionSlots
                .FirstOrDefaultAsync(s => s.SessionSlotId == request.SlotId, ct)
                ?? throw new NotFoundException("Slot not found.");

            if (slot.CoachId != voucher.CoachId)
                throw new ConflictException(
                    "Slot does not belong to the voucher's coach.",
                    new { slotCoachId = slot.CoachId, voucherCoachId = voucher.CoachId });

            if (!slot.SessionSlotIsAvailable)
                throw new ConflictException("Slot is not available.");

            var slotTaken = await db.Sessions
                .AnyAsync(s => s.SessionSlotId == slot.SessionSlotId
                            && s.SessionStatus != SessionStatus.Cancelled, ct);
            if (slotTaken)
                throw new ConflictException("Slot is already booked.");

            // Compatibility: offerType + duration must match (slot has no Sport field)
            if (voucher.OfferType != slot.SessionSlotOfferType
                || voucher.DurationMinutes != slot.SessionSlotDurationMinutes)
            {
                throw new ConflictException(
                    "Voucher and slot are incompatible.",
                    new {
                        voucher = new {
                            offerType       = EnumMappings.OfferTypeMapping.ToWire(voucher.OfferType),
                            durationMinutes = voucher.DurationMinutes
                        },
                        slot = new {
                            offerType       = EnumMappings.OfferTypeMapping.ToWire(slot.SessionSlotOfferType),
                            durationMinutes = slot.SessionSlotDurationMinutes
                        }
                    });
            }

            // Reject past slots
            if (slot.SessionSlotStartDate < DateTime.UtcNow)
                throw new ConflictException("Cannot reserve a slot in the past.");

            // Visio URL — only for VISIO offer type
            string? visioUrl = voucher.OfferType == OfferType.Visio
                ? visioGenerator.Generate()
                : null;

            var session = new Session
            {
                SessionId              = Guid.NewGuid(),
                SessionVoucherId       = voucher.VoucherId,
                SessionSlotId          = slot.SessionSlotId,
                SessionMemberId        = memberId,
                SessionCoachId         = voucher.CoachId,
                SessionProductId       = voucher.ProductId,
                SessionOfferType       = voucher.OfferType,
                SessionDurationMinutes = voucher.DurationMinutes,
                SessionSport           = voucher.Sport,
                SessionScheduledAt     = slot.SessionSlotStartDate,
                SessionStatus          = SessionStatus.Scheduled,
                SessionVisioUrl        = visioUrl,
                SessionCreatedDate     = DateTime.UtcNow,
                SessionUpdatedDate     = DateTime.UtcNow,
            };
            db.Sessions.Add(session);

            voucher.Status            = VoucherStatus.Reserved;
            voucher.ReservedSessionId = session.SessionId;
            voucher.UpdatedDate       = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            logger.LogInformation(
                "Session {SessionId} reserved by member {MemberId} on slot {SlotId} (voucher {VoucherId}).",
                session.SessionId, memberId, slot.SessionSlotId, voucher.VoucherId);

            return await BuildResponseAsync(session.SessionId, ct);
        }
        catch (PostgresException ex) when (ex.SqlState == "40001")
        {
            // Serialization failure — a concurrent reservation on the same slot won
            logger.LogWarning(
                "Reservation conflict (40001) on slot {SlotId} for member {MemberId}; another reservation won.",
                request.SlotId, memberId);
            throw new ConflictException("Slot is already booked.");
        }
    }

    public async Task<SessionResponse> CancelByMemberAsync(
        Guid memberId, Guid sessionId, CancelSessionRequest request, CancellationToken ct)
    {
        await cancelValidator.ValidateAndThrowAsync(request, ct);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var session = await db.Sessions
            .FirstOrDefaultAsync(s => s.SessionId == sessionId, ct)
            ?? throw new NotFoundException("Session not found.");

        if (session.SessionMemberId != memberId)
            throw new NotFoundException("Session not found.");

        var effective = ComputeEffectiveStatus(session);
        if (effective != SessionStatus.Scheduled)
            throw new ConflictException(
                $"Cannot cancel a session with effective status {EnumMappings.SessionStatusMapping.ToWire(effective)}.",
                new { sessionId, effectiveStatus = EnumMappings.SessionStatusMapping.ToWire(effective) });

        var coachParams = await db.CoachParameters
            .FirstOrDefaultAsync(cp => cp.CoachId == session.SessionCoachId, ct)
            ?? throw new InvalidOperationException(
                $"CoachParameters missing for coach {session.SessionCoachId}. Seeder invariant broken.");

        var cutoff    = session.SessionScheduledAt.AddHours(-coachParams.CoachParameterCancellationDelayHours);
        var inDeadline = DateTime.UtcNow < cutoff;

        var voucher = await db.SessionVouchers
            .FirstAsync(v => v.VoucherId == session.SessionVoucherId, ct);

        // Cancellation matrix (member side):
        //  In deadline  → voucher returns to Available (member may rebook)
        //  Out of deadline → voucher is Consumed (member pays for late cancel)
        voucher.Status            = inDeadline ? VoucherStatus.Available : VoucherStatus.Consumed;
        voucher.ReservedSessionId = null;
        voucher.UpdatedDate       = DateTime.UtcNow;

        session.SessionStatus             = SessionStatus.Cancelled;
        session.SessionCancellationReason = request.Reason.Trim();
        session.SessionCancelledBy        = CancelledBy.Member;
        session.SessionCancelledAt        = DateTime.UtcNow;
        session.SessionUpdatedDate        = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        logger.LogInformation(
            "Session {SessionId} cancelled by member {MemberId}. inDeadline={InDeadline}, voucher new status={VoucherStatus}.",
            sessionId, memberId, inDeadline,
            EnumMappings.VoucherStatusMapping.ToWire(voucher.Status));

        return await BuildResponseAsync(sessionId, ct);
    }

    public async Task<IReadOnlyList<SessionResponse>> ListForMemberAsync(
        Guid memberId, SessionStatusFilter? statusFilter, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        IQueryable<Session> query = db.Sessions
            .Where(s => s.SessionMemberId == memberId)
            .AsNoTracking();

        query = ApplyStatusFilter(query, statusFilter, now);
        query = query.OrderBy(s => s.SessionScheduledAt);

        return await QueryToResponsesAsync(query, ct);
    }

    public async Task<SessionResponse> GetForMemberAsync(
        Guid memberId, Guid sessionId, CancellationToken ct)
    {
        var session = await db.Sessions
            .Include(s => s.Coach)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SessionId == sessionId, ct)
            ?? throw new NotFoundException("Session not found.");

        if (session.SessionMemberId != memberId)
            throw new NotFoundException("Session not found.");

        var product = await db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProductId == session.SessionProductId, ct);

        var memberCoach = await GetMemberCoachAsync(session.SessionMemberId, session.SessionCoachId, ct);

        return ToResponse(session, session.Coach, product, memberCoach);
    }

    // ── Coach-side ──────────────────────────────────────────────────────────────

    public async Task<SessionResponse> CancelByCoachAsync(
        Guid coachId, Guid sessionId, CancelSessionRequest request, CancellationToken ct)
    {
        await cancelValidator.ValidateAndThrowAsync(request, ct);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var session = await db.Sessions
            .FirstOrDefaultAsync(s => s.SessionId == sessionId, ct)
            ?? throw new NotFoundException("Session not found.");

        if (session.SessionCoachId != coachId)
            throw new NotFoundException("Session not found.");

        var effective = ComputeEffectiveStatus(session);
        if (effective != SessionStatus.Scheduled)
            throw new ConflictException(
                $"Cannot cancel a session with effective status {EnumMappings.SessionStatusMapping.ToWire(effective)}.",
                new { sessionId, effectiveStatus = EnumMappings.SessionStatusMapping.ToWire(effective) });

        var voucher = await db.SessionVouchers
            .FirstAsync(v => v.VoucherId == session.SessionVoucherId, ct);

        // Coach cancellation: voucher always returns to Available regardless of deadline
        voucher.Status            = VoucherStatus.Available;
        voucher.ReservedSessionId = null;
        voucher.UpdatedDate       = DateTime.UtcNow;

        session.SessionStatus             = SessionStatus.Cancelled;
        session.SessionCancellationReason = request.Reason.Trim();
        session.SessionCancelledBy        = CancelledBy.Coach;
        session.SessionCancelledAt        = DateTime.UtcNow;
        session.SessionUpdatedDate        = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        logger.LogInformation(
            "Session {SessionId} cancelled by coach {CoachId}. Voucher {VoucherId} returned to Available.",
            sessionId, coachId, voucher.VoucherId);

        return await BuildResponseAsync(sessionId, ct);
    }

    public async Task<IReadOnlyList<SessionResponse>> ListForCoachAsync(
        Guid coachId, SessionStatusFilter? statusFilter, DateTime? fromDate, DateTime? toDate, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        IQueryable<Session> query = db.Sessions
            .Where(s => s.SessionCoachId == coachId)
            .AsNoTracking();

        query = ApplyStatusFilter(query, statusFilter, now);

        if (fromDate.HasValue)
            query = query.Where(s => s.SessionScheduledAt >= fromDate.Value);
        if (toDate.HasValue)
            query = query.Where(s => s.SessionScheduledAt <= toDate.Value);

        query = query.OrderBy(s => s.SessionScheduledAt);

        return await QueryToResponsesAsync(query, ct);
    }

    public async Task<SessionResponse> GetForCoachAsync(
        Guid coachId, Guid sessionId, CancellationToken ct)
    {
        var session = await db.Sessions
            .Include(s => s.Coach)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SessionId == sessionId, ct)
            ?? throw new NotFoundException("Session not found.");

        if (session.SessionCoachId != coachId)
            throw new NotFoundException("Session not found.");

        var product = await db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProductId == session.SessionProductId, ct);

        var memberCoach = await GetMemberCoachAsync(session.SessionMemberId, session.SessionCoachId, ct);

        return ToResponse(session, session.Coach, product, memberCoach);
    }

    // ── Private helpers ─────────────────────────────────────────────────────────

    // Single source of truth for "what status should the user see right now".
    // The DB is never mutated by reads — SCHEDULED → COMPLETED materialization
    // is deferred to a future cron (Lot 5+).
    private static SessionStatus ComputeEffectiveStatus(Session s)
    {
        if (s.SessionStatus == SessionStatus.Cancelled) return SessionStatus.Cancelled;
        if (s.SessionStatus == SessionStatus.Completed) return SessionStatus.Completed;
        var endsAt = s.SessionScheduledAt.AddMinutes(s.SessionDurationMinutes);
        return DateTime.UtcNow > endsAt ? SessionStatus.Completed : SessionStatus.Scheduled;
    }

    private static SessionResponse ToResponse(Session session, Coach coach, Product? product, MemberCoach? memberCoach)
    {
        var effectiveStatus = ComputeEffectiveStatus(session);
        // Visio URL only for VISIO sessions that are not cancelled
        var visioUrl = session.SessionOfferType == OfferType.Visio && effectiveStatus != SessionStatus.Cancelled
            ? session.SessionVisioUrl
            : null;

        // Effective address, resolved at read time (never snapshotted) for in-person sessions:
        // the member's per-coach override, else the booked product's own location. Treated as
        // "in-person" for any non-VISIO offer type (covers PresentielSolo and PresentielGroupe,
        // even though the latter isn't reservable in V1) so this stays correct if that changes.
        var effectiveAddress = session.SessionOfferType != OfferType.Visio
            ? memberCoach?.MemberCoachPresentialAddress ?? product?.ProductLocation
            : null;

        return new SessionResponse(
            SessionId:          session.SessionId,
            VoucherId:          session.SessionVoucherId,
            SlotId:             session.SessionSlotId,
            CoachId:            session.SessionCoachId,
            CoachDisplayName:   $"{coach.CoachFirstName} {coach.CoachLastName}".Trim(),
            ProductId:          session.SessionProductId,
            ProductName:        product?.ProductName ?? "(unavailable)",
            OfferType:          EnumMappings.OfferTypeMapping.ToWire(session.SessionOfferType),
            DurationMinutes:    session.SessionDurationMinutes,
            Sport:              EnumMappings.SportMapping.ToWire(session.SessionSport),
            ScheduledAt:        session.SessionScheduledAt,
            Status:             EnumMappings.SessionStatusMapping.ToWire(effectiveStatus),
            VisioUrl:           visioUrl,
            EffectiveAddress:   effectiveAddress,
            CancellationReason: session.SessionCancellationReason,
            CancelledBy:        session.SessionCancelledBy.HasValue
                                    ? EnumMappings.CancelledByMapping.ToWire(session.SessionCancelledBy.Value)
                                    : null,
            CancelledAt:        session.SessionCancelledAt,
            CreatedDate:        session.SessionCreatedDate,
            UpdatedDate:        session.SessionUpdatedDate);
    }

    private Task<MemberCoach?> GetMemberCoachAsync(Guid memberId, Guid coachId, CancellationToken ct) =>
        db.MemberCoaches
            .AsNoTracking()
            .FirstOrDefaultAsync(mc => mc.MemberId == memberId && mc.CoachId == coachId, ct);

    private async Task<SessionResponse> BuildResponseAsync(Guid sessionId, CancellationToken ct)
    {
        var session = await db.Sessions
            .Include(s => s.Coach)
            .FirstAsync(s => s.SessionId == sessionId, ct);
        var product = await db.Products
            .FirstOrDefaultAsync(p => p.ProductId == session.SessionProductId, ct);
        var memberCoach = await GetMemberCoachAsync(session.SessionMemberId, session.SessionCoachId, ct);
        return ToResponse(session, session.Coach, product, memberCoach);
    }

    private static IQueryable<Session> ApplyStatusFilter(
        IQueryable<Session> query, SessionStatusFilter? filter, DateTime now)
    {
        return filter switch
        {
            SessionStatusFilter.Scheduled => query.Where(s =>
                s.SessionStatus == SessionStatus.Scheduled &&
                now < s.SessionScheduledAt.AddMinutes(s.SessionDurationMinutes)),

            SessionStatusFilter.Completed => query.Where(s =>
                s.SessionStatus == SessionStatus.Completed ||
                (s.SessionStatus == SessionStatus.Scheduled &&
                 now > s.SessionScheduledAt.AddMinutes(s.SessionDurationMinutes))),

            SessionStatusFilter.Cancelled => query.Where(s =>
                s.SessionStatus == SessionStatus.Cancelled),

            SessionStatusFilter.Upcoming => query.Where(s =>
                s.SessionStatus == SessionStatus.Scheduled &&
                s.SessionScheduledAt > now),

            SessionStatusFilter.Past => query.Where(s =>
                s.SessionStatus == SessionStatus.Cancelled ||
                s.SessionStatus == SessionStatus.Completed ||
                (s.SessionStatus == SessionStatus.Scheduled &&
                 now > s.SessionScheduledAt.AddMinutes(s.SessionDurationMinutes))),

            _ => query
        };
    }

    // Single query, single round trip: LEFT JOIN to PRODUCTS (SessionProductId is a weak
    // reference — no FK, product may be archived, so a missing row must yield null rather than
    // drop the session) and LEFT JOIN to MEMBER_COACHES on the session's own (member, coach)
    // pair (so the address override can never leak from a different member's row). No per-row
    // lookups — the join and the mapping both happen once for the whole result set.
    private async Task<IReadOnlyList<SessionResponse>> QueryToResponsesAsync(
        IQueryable<Session> sessionQuery, CancellationToken ct)
    {
        var query =
            from s in sessionQuery
            join p in db.Products on s.SessionProductId equals p.ProductId into productJoin
            from p in productJoin.DefaultIfEmpty()
            join mc in db.MemberCoaches
                on new { MemberId = s.SessionMemberId, CoachId = s.SessionCoachId }
                equals new { MemberId = mc.MemberId, CoachId = mc.CoachId }
                into memberCoachJoin
            from mc in memberCoachJoin.DefaultIfEmpty()
            select new { Session = s, Coach = s.Coach, Product = p, MemberCoach = mc };

        var rows = await query.ToListAsync(ct);

        return rows
            .Select(r => ToResponse(r.Session, r.Coach, r.Product, r.MemberCoach))
            .ToList();
    }
}
