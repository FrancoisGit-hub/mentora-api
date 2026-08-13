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

            // Permanent, not a V1 gap: this is the individual-reservation path. Group courses are
            // never self-booked — the coach registers each participant via
            // POST /coach/sessions/{sessionId}/participants (Lot 6.4), which consumes the
            // voucher through a capacity check this single-slot flow doesn't perform.
            if (voucher.OfferType == OfferType.PresentielGroupe || voucher.OfferType == OfferType.VisioGroupe)
                throw new ConflictException(
                    "Group offer types cannot be reserved directly — the coach registers participants " +
                    "for group sessions.",
                    new { offerType = EnumMappings.OfferTypeMapping.ToWire(voucher.OfferType) });

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

            // Best-effort convenience link — never blocks the booking (see LinkToProgramSessionAsync).
            await LinkToProgramSessionAsync(memberId, session, ct);

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

        // Deliberately a direct SessionMemberId comparison, not the V_SESSION_MEMBERS view: this
        // endpoint cancels an individual booking with its own voucher-refund matrix. A group
        // registration is unregistered via DELETE .../participants/{memberId} instead — a null
        // SessionMemberId (group session) never equals memberId, so it 404s here as intended.
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

        // Guaranteed present — the SessionMemberId match above proves this is an individual
        // session, and CK_SESSIONS_GROUP_HAS_NO_MEMBER guarantees the voucher travels with it.
        var voucherId = session.SessionVoucherId
            ?? throw new InvalidOperationException(
                $"Session {sessionId} has a member but no voucher — CK_SESSIONS_GROUP_HAS_NO_MEMBER should have prevented this.");
        var voucher = await db.SessionVouchers
            .FirstAsync(v => v.VoucherId == voucherId, ct);

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

        // Routed through V_SESSION_MEMBERS so a member's group-course registrations show up
        // alongside their individual bookings — not just SESSION_MEMBER_ID matches.
        var memberSessionIds = db.SessionMembers.Where(v => v.MemberId == memberId).Select(v => v.SessionId);

        IQueryable<Session> query = db.Sessions
            .Where(s => memberSessionIds.Contains(s.SessionId))
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
            .Include(s => s.Member)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SessionId == sessionId, ct)
            ?? throw new NotFoundException("Session not found.");

        // Ownership via V_SESSION_MEMBERS — covers both an individual booking and a group
        // registration, unlike a direct SessionMemberId comparison.
        var owns = await db.SessionMembers.AnyAsync(v => v.SessionId == sessionId && v.MemberId == memberId, ct);
        if (!owns)
            throw new NotFoundException("Session not found.");

        var product = session.SessionProductId is { } productId
            ? await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.ProductId == productId, ct)
            : null;

        // The caller's own memberId is used here, not session.SessionMemberId — the latter is
        // null for a group session this member is merely a participant of, but "my address
        // override with this coach" is still resolvable from the caller's identity.
        var memberCoach = await GetMemberCoachAsync(memberId, session.SessionCoachId, ct);

        return ToResponse(session, session.Coach, session.Member, product, memberCoach);
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

        // Group sessions are not cancellable through this endpoint: cancelling a full course
        // means returning every registered participant's voucher to Available, which this
        // single-voucher flow cannot express. Not implemented in Lot 6.4 — a future lot will add
        // a group-cancel flow that walks SESSION_PARTICIPANTS and refunds each one.
        if (session.SessionMemberId is null)
            throw new InvalidOperationException(
                "Group sessions cannot be cancelled through this endpoint. Unregister participants " +
                "individually via DELETE .../participants/{memberId}; a bulk group-cancel flow that " +
                "returns every participant's voucher to Available is not implemented yet.");

        var effective = ComputeEffectiveStatus(session);
        if (effective != SessionStatus.Scheduled)
            throw new ConflictException(
                $"Cannot cancel a session with effective status {EnumMappings.SessionStatusMapping.ToWire(effective)}.",
                new { sessionId, effectiveStatus = EnumMappings.SessionStatusMapping.ToWire(effective) });

        // Guaranteed present — the group-session guard above proves this is individual, and
        // CK_SESSIONS_GROUP_HAS_NO_MEMBER guarantees the voucher travels with the member.
        var voucherId = session.SessionVoucherId
            ?? throw new InvalidOperationException(
                $"Session {sessionId} has a member but no voucher — CK_SESSIONS_GROUP_HAS_NO_MEMBER should have prevented this.");
        var voucher = await db.SessionVouchers
            .FirstAsync(v => v.VoucherId == voucherId, ct);

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
        Guid coachId, SessionStatusFilter? statusFilter, DateTime? fromDate, DateTime? toDate,
        Guid? memberId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // coachId always comes from the JWT — memberId here is a filter, not an identity. Scoping
        // stays on SessionCoachId first, so a coach passing another coach's member id gets an
        // empty list rather than that member's sessions.
        IQueryable<Session> query = db.Sessions
            .Where(s => s.SessionCoachId == coachId)
            .AsNoTracking();

        query = ApplyStatusFilter(query, statusFilter, now);

        if (fromDate.HasValue)
            query = query.Where(s => s.SessionScheduledAt >= fromDate.Value);
        if (toDate.HasValue)
            query = query.Where(s => s.SessionScheduledAt <= toDate.Value);
        if (memberId.HasValue)
        {
            // Routed through V_SESSION_MEMBERS (UNION ALL runs in the database, not in memory —
            // this is a correlated subquery, not a client-evaluated list) so a member registered
            // to a group course shows up here too, not just their individual bookings.
            var memberSessionIds = db.SessionMembers
                .Where(v => v.MemberId == memberId.Value)
                .Select(v => v.SessionId);
            query = query.Where(s => memberSessionIds.Contains(s.SessionId));
        }

        query = query.OrderBy(s => s.SessionScheduledAt);

        return await QueryToResponsesAsync(query, ct);
    }

    public async Task<SessionResponse> GetForCoachAsync(
        Guid coachId, Guid sessionId, CancellationToken ct)
    {
        var session = await db.Sessions
            .Include(s => s.Coach)
            .Include(s => s.Member)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SessionId == sessionId, ct)
            ?? throw new NotFoundException("Session not found.");

        if (session.SessionCoachId != coachId)
            throw new NotFoundException("Session not found.");

        var product = session.SessionProductId is { } productId
            ? await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.ProductId == productId, ct)
            : null;

        // A group session has no single member to resolve an address override for — memberCoach
        // stays null, and ToResponse's EffectiveAddress falls back to the product's own location,
        // which is already null-safe (?. throughout).
        var memberCoach = session.SessionMemberId is { } sessionMemberId
            ? await GetMemberCoachAsync(sessionMemberId, session.SessionCoachId, ct)
            : null;

        return ToResponse(session, session.Coach, session.Member, product, memberCoach);
    }

    // ── Coach-side — group sessions (Lot 6.4) ─────────────────────────────────────

    private static readonly HashSet<OfferType> GroupOfferTypes = [OfferType.PresentielGroupe, OfferType.VisioGroupe];

    public async Task<SessionResponse> CreateGroupSessionAsync(
        Guid coachId, CreateGroupSessionRequest request, CancellationToken ct)
    {
        // Ownership folded into the query filter, not a separate check — never 403.
        var slot = await db.SessionSlots
            .FirstOrDefaultAsync(s => s.SessionSlotId == request.SlotId && s.CoachId == coachId, ct)
            ?? throw new NotFoundException("Slot not found.");

        if (!slot.SessionSlotIsAvailable)
            throw new ConflictException("Slot is not available.");

        if (!GroupOfferTypes.Contains(slot.SessionSlotOfferType))
            throw new ConflictException(
                "Slot is not a group offer type.",
                new { slotOfferType = EnumMappings.OfferTypeMapping.ToWire(slot.SessionSlotOfferType) });

        var slotTaken = await db.Sessions
            .AnyAsync(s => s.SessionSlotId == slot.SessionSlotId && s.SessionStatus != SessionStatus.Cancelled, ct);
        if (slotTaken)
            throw new ConflictException("Slot is already booked.");

        if (slot.SessionSlotStartDate < DateTime.UtcNow)
            throw new ConflictException("Cannot create a group session on a slot in the past.");

        var product = await db.Products
            .FirstOrDefaultAsync(p => p.ProductId == request.ProductId && p.CoachId == coachId, ct)
            ?? throw new NotFoundException("Product not found.");

        if (product.ProductStatus != ProductStatus.Published)
            throw new ConflictException(
                "Product must be published to create a group session from it.",
                new { productStatus = EnumMappings.ProductStatusMapping.ToWire(product.ProductStatus) });

        if (!GroupOfferTypes.Contains(product.ProductOfferType))
            throw new ConflictException(
                "Product is not a group offer type.",
                new { productOfferType = EnumMappings.OfferTypeMapping.ToWire(product.ProductOfferType) });

        if (product.ProductOfferType != slot.SessionSlotOfferType || product.ProductDurationMinutes != slot.SessionSlotDurationMinutes)
            throw new ConflictException(
                "Product and slot are incompatible.",
                new {
                    product = new {
                        offerType       = EnumMappings.OfferTypeMapping.ToWire(product.ProductOfferType),
                        durationMinutes = product.ProductDurationMinutes
                    },
                    slot = new {
                        offerType       = EnumMappings.OfferTypeMapping.ToWire(slot.SessionSlotOfferType),
                        durationMinutes = slot.SessionSlotDurationMinutes
                    }
                });

        var now = DateTime.UtcNow;
        var session = new Session
        {
            SessionId              = Guid.NewGuid(),
            SessionVoucherId       = null,
            SessionSlotId          = slot.SessionSlotId,
            SessionMemberId        = null,
            SessionCoachId         = coachId,
            SessionProductId       = product.ProductId,
            SessionOfferType       = slot.SessionSlotOfferType,
            SessionDurationMinutes = slot.SessionSlotDurationMinutes,
            SessionSport           = product.ProductSport,
            SessionScheduledAt     = slot.SessionSlotStartDate,
            SessionMaxParticipants = product.ProductMaxParticipants,
            SessionStatus          = SessionStatus.Scheduled,
            SessionVisioUrl        = null, // VisioGroupe cannot exist yet — see OfferType.VisioGroupe
            SessionCreatedDate     = now,
            SessionUpdatedDate     = now,
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Group session {SessionId} created by coach {CoachId} on slot {SlotId} (product {ProductId}).",
            session.SessionId, coachId, slot.SessionSlotId, product.ProductId);

        return await BuildGroupResponseAsync(session.SessionId, ct);
    }

    public async Task<IReadOnlyList<SessionParticipantResponse>> ListParticipantsAsync(
        Guid coachId, Guid sessionId, CancellationToken ct)
    {
        await GetOwnedGroupSessionAsync(coachId, sessionId, ct);

        var participants = await db.SessionParticipants
            .Include(p => p.Member)
            .Where(p => p.SessionParticipantSessionId == sessionId)
            .OrderBy(p => p.SessionParticipantCreatedDate)
            .AsNoTracking()
            .ToListAsync(ct);

        // One extra query for the whole list, not per row — IX_PROGRAM_SESSIONS_BOOKING_MEMBER
        // guarantees at most one row per (session, member).
        var programSessionIdByMember = await db.ProgramSessions
            .Where(ps => ps.ProgramSessionSessionId == sessionId)
            .ToDictionaryAsync(ps => ps.ProgramSessionMemberId, ps => (Guid?)ps.ProgramSessionId, ct);

        return participants
            .Select(p => ToParticipantResponse(p, programSessionIdByMember.GetValueOrDefault(p.SessionParticipantMemberId)))
            .ToList();
    }

    public async Task<SessionParticipantResponse> RegisterParticipantAsync(
        Guid coachId, Guid sessionId, RegisterParticipantRequest request, CancellationToken ct)
    {
        // a. Session belongs to the coach and is a group type, else 404.
        var session = await GetOwnedGroupSessionAsync(coachId, sessionId, ct);

        // b. The MEMBER_COACHES link exists, else 404 — never 403.
        var isLinked = await db.MemberCoaches
            .AnyAsync(mc => mc.MemberId == request.MemberId && mc.CoachId == coachId, ct);
        if (!isLinked)
            throw new NotFoundException($"Member {request.MemberId} not found.");

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        // c. Voucher belongs to that member, on this coach, is Available, and matches the
        // session's offer type and duration. Otherwise 409 with details.
        var voucher = await db.SessionVouchers
            .FirstOrDefaultAsync(v => v.VoucherId == request.VoucherId, ct)
            ?? throw new NotFoundException("Voucher not found.");

        if (voucher.MemberId != request.MemberId)
            throw new NotFoundException("Voucher not found.");

        if (voucher.CoachId != coachId)
            throw new ConflictException(
                "Voucher does not belong to this coach.",
                new { voucherCoachId = voucher.CoachId, coachId });

        if (voucher.Status != VoucherStatus.Available)
            throw new ConflictException(
                "Voucher is not available for reservation.",
                new {
                    voucherId = voucher.VoucherId,
                    status = EnumMappings.VoucherStatusMapping.ToWire(voucher.Status)
                });

        if (voucher.OfferType != session.SessionOfferType || voucher.DurationMinutes != session.SessionDurationMinutes)
            throw new ConflictException(
                "Voucher and session are incompatible.",
                new {
                    voucher = new {
                        offerType       = EnumMappings.OfferTypeMapping.ToWire(voucher.OfferType),
                        durationMinutes = voucher.DurationMinutes
                    },
                    session = new {
                        offerType       = EnumMappings.OfferTypeMapping.ToWire(session.SessionOfferType),
                        durationMinutes = session.SessionDurationMinutes
                    }
                });

        var alreadyRegistered = await db.SessionParticipants.AnyAsync(p =>
            p.SessionParticipantSessionId == sessionId &&
            p.SessionParticipantMemberId == request.MemberId &&
            p.SessionParticipantStatus != SessionParticipantStatus.Cancelled, ct);
        if (alreadyRegistered)
            throw new ConflictException("Member is already registered for this session.");

        // d. Capacity: count participants with status <> CANCELLED. NULL max means no limit.
        if (session.SessionMaxParticipants is { } max)
        {
            var seatsTaken = await db.SessionParticipants.CountAsync(p =>
                p.SessionParticipantSessionId == sessionId &&
                p.SessionParticipantStatus != SessionParticipantStatus.Cancelled, ct);
            if (seatsTaken >= max)
                throw new ConflictException(
                    "Session is at capacity.",
                    new { maxParticipants = max, seatsTaken });
        }

        // e. Insert the participant, set the voucher to Reserved.
        var now = DateTime.UtcNow;
        var participant = new SessionParticipant
        {
            SessionParticipantId          = Guid.NewGuid(),
            SessionParticipantSessionId   = sessionId,
            SessionParticipantMemberId    = request.MemberId,
            SessionParticipantVoucherId   = voucher.VoucherId,
            SessionParticipantStatus      = SessionParticipantStatus.Registered,
            SessionParticipantCreatedDate = now,
            SessionParticipantUpdatedDate = now,
        };
        db.SessionParticipants.Add(participant);

        voucher.Status            = VoucherStatus.Reserved;
        voucher.ReservedSessionId = sessionId;
        voucher.UpdatedDate       = now;

        // Best-effort convenience link — never blocks the registration (see LinkToProgramSessionAsync).
        await LinkToProgramSessionAsync(request.MemberId, session, ct);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        logger.LogInformation(
            "Member {MemberId} registered for group session {SessionId} by coach {CoachId} (voucher {VoucherId}).",
            request.MemberId, sessionId, coachId, voucher.VoucherId);

        return await BuildParticipantResponseAsync(participant.SessionParticipantId, ct);
    }

    public async Task UnregisterParticipantAsync(Guid coachId, Guid sessionId, Guid memberId, CancellationToken ct)
    {
        await GetOwnedGroupSessionAsync(coachId, sessionId, ct);

        var participant = await db.SessionParticipants.FirstOrDefaultAsync(p =>
            p.SessionParticipantSessionId == sessionId &&
            p.SessionParticipantMemberId == memberId &&
            p.SessionParticipantStatus != SessionParticipantStatus.Cancelled, ct)
            ?? throw new NotFoundException("Registration not found.");

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var now = DateTime.UtcNow;
        participant.SessionParticipantStatus      = SessionParticipantStatus.Cancelled;
        participant.SessionParticipantUpdatedDate = now;

        if (participant.SessionParticipantVoucherId is { } voucherId)
        {
            var voucher = await db.SessionVouchers.FirstAsync(v => v.VoucherId == voucherId, ct);
            voucher.Status            = VoucherStatus.Available;
            voucher.ReservedSessionId = null;
            voucher.UpdatedDate       = now;
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        logger.LogInformation(
            "Member {MemberId} unregistered from group session {SessionId} by coach {CoachId}.",
            memberId, sessionId, coachId);
    }

    // ── Program-session linking (Lot 6.5) ─────────────────────────────────────────

    // Called from ReserveAsync and RegisterParticipantAsync, inside their existing transaction.
    // A booking must NEVER fail because of program matching: "no active program" and "no
    // candidate" are both normal, silent no-ops here, never exceptions. Only a genuine
    // infrastructure failure (DB down, etc.) would propagate past this method, same as any other
    // query in the surrounding transaction.
    private async Task LinkToProgramSessionAsync(Guid memberId, Session session, CancellationToken ct)
    {
        // a. The member must have an ACTIVE program, else do nothing.
        var program = await db.Programs
            .FirstOrDefaultAsync(p => p.ProgramMemberId == memberId && p.ProgramStatus == ProgramStatus.Active, ct);
        if (program is null)
            return;

        var offerTypeWire = EnumMappings.OfferTypeMapping.ToWire(session.SessionOfferType);

        // b. Candidates: PLANNED, unlinked, this member, matching type.
        var candidates = await (
            from ps in db.ProgramSessions
            join pb in db.ProgramBlocks on ps.ProgramSessionBlockId equals pb.ProgramBlockId
            where ps.ProgramSessionProgramId == program.ProgramId
               && ps.ProgramSessionMemberId == memberId
               && ps.ProgramSessionStatus == ProgramSessionStatus.Planned
               && ps.ProgramSessionSessionId == null
               && ps.ProgramSessionType == offerTypeWire
            select new { ProgramSession = ps, pb.ProgramBlockWeekNumber })
            .ToListAsync(ct);

        if (candidates.Count == 0)
            return;

        // c. Earliest computed date, then position — same formula as
        // ProgramService.ComputeSessionDate (StartDate + (week-1+offset)*7 + (dayOfWeek-1)).
        var best = candidates
            .OrderBy(c => program.ProgramStartDate.AddDays(
                (c.ProgramBlockWeekNumber!.Value - 1 + program.ProgramWeekOffset) * 7 +
                (c.ProgramSession.ProgramSessionDayOfWeek - 1)))
            .ThenBy(c => c.ProgramSession.ProgramSessionPosition)
            .First();

        // d. Link. IX_PROGRAM_SESSIONS_BOOKING_MEMBER is unique on (SESSION_ID, MEMBER_ID), not
        // on SESSION_ID alone — several participants of the same group session each link their
        // own program session to it.
        best.ProgramSession.ProgramSessionSessionId = session.SessionId;
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

    // member is null for a group session — it carries no single member, only participants
    // (see GET .../participants). MemberId/MemberFirstName/MemberLastName/ProductId/VoucherId
    // are nullable on the wire for exactly this reason.
    private static SessionResponse ToResponse(
        Session session, Coach coach, Member? member, Product? product, MemberCoach? memberCoach)
    {
        var effectiveStatus = ComputeEffectiveStatus(session);
        // Visio URL only for VISIO sessions that are not cancelled
        var visioUrl = session.SessionOfferType == OfferType.Visio && effectiveStatus != SessionStatus.Cancelled
            ? session.SessionVisioUrl
            : null;

        // Effective address, resolved at read time (never snapshotted) for in-person sessions:
        // the member's per-coach override, else the booked product's own location. Treated as
        // "in-person" for any non-VISIO offer type (covers PresentielSolo and both group types)
        // so this stays correct as those roll out. memberCoach is null for a group session (no
        // single member), so this falls back to the product's own location — still correct.
        var effectiveAddress = session.SessionOfferType != OfferType.Visio
            ? memberCoach?.MemberCoachPresentialAddress ?? product?.ProductLocation
            : null;

        return new SessionResponse(
            SessionId:          session.SessionId,
            VoucherId:          session.SessionVoucherId,
            SlotId:             session.SessionSlotId,
            CoachId:            session.SessionCoachId,
            CoachDisplayName:   $"{coach.CoachFirstName} {coach.CoachLastName}".Trim(),
            MemberId:           session.SessionMemberId,
            MemberFirstName:    member?.MemberFirstName,
            MemberLastName:     member?.MemberLastName,
            ProductId:          session.SessionProductId,
            ProductName:        product?.ProductName ?? "(unavailable)",
            OfferType:          EnumMappings.OfferTypeMapping.ToWire(session.SessionOfferType),
            DurationMinutes:    session.SessionDurationMinutes,
            Sport:              EnumMappings.SportMapping.ToWire(session.SessionSport),
            ScheduledAt:        session.SessionScheduledAt,
            Status:             EnumMappings.SessionStatusMapping.ToWire(effectiveStatus),
            VisioUrl:           visioUrl,
            EffectiveAddress:   effectiveAddress,
            MaxParticipants:    session.SessionMaxParticipants,
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

    // Individual-session-only: called after ReserveAsync, CancelByMemberAsync, and
    // CancelByCoachAsync — the latter now guards against group sessions before reaching here, so
    // SessionMemberId is always present by the time this runs. Asserted, not silenced.
    private async Task<SessionResponse> BuildResponseAsync(Guid sessionId, CancellationToken ct)
    {
        var session = await db.Sessions
            .Include(s => s.Coach)
            .Include(s => s.Member)
            .FirstAsync(s => s.SessionId == sessionId, ct);

        var memberId = session.SessionMemberId
            ?? throw new InvalidOperationException(
                $"Session {sessionId} has no member — BuildResponseAsync is only used for " +
                "individual-session flows (reserve/cancel), never for group sessions.");

        var product = session.SessionProductId is { } productId
            ? await db.Products.FirstOrDefaultAsync(p => p.ProductId == productId, ct)
            : null;
        var memberCoach = await GetMemberCoachAsync(memberId, session.SessionCoachId, ct);
        return ToResponse(session, session.Coach, session.Member, product, memberCoach);
    }

    // Group-session-only counterpart to BuildResponseAsync: no member, no memberCoach, ever.
    private async Task<SessionResponse> BuildGroupResponseAsync(Guid sessionId, CancellationToken ct)
    {
        var session = await db.Sessions
            .Include(s => s.Coach)
            .FirstAsync(s => s.SessionId == sessionId, ct);

        var product = session.SessionProductId is { } productId
            ? await db.Products.FirstOrDefaultAsync(p => p.ProductId == productId, ct)
            : null;

        return ToResponse(session, session.Coach, null, product, null);
    }

    // Ownership + group-type both folded into one query filter — never 403, and registering a
    // participant onto an individual session 404s the same way a foreign coach's session does.
    private async Task<Session> GetOwnedGroupSessionAsync(Guid coachId, Guid sessionId, CancellationToken ct) =>
        await db.Sessions
            .FirstOrDefaultAsync(s =>
                s.SessionId == sessionId &&
                s.SessionCoachId == coachId &&
                (s.SessionOfferType == OfferType.PresentielGroupe || s.SessionOfferType == OfferType.VisioGroupe), ct)
            ?? throw new NotFoundException("Group session not found.");

    private static SessionParticipantResponse ToParticipantResponse(SessionParticipant p, Guid? programSessionId) => new(
        p.SessionParticipantId,
        p.SessionParticipantSessionId,
        p.SessionParticipantMemberId,
        p.Member.MemberFirstName,
        p.Member.MemberLastName,
        p.SessionParticipantVoucherId,
        EnumMappings.SessionParticipantStatusMapping.ToWire(p.SessionParticipantStatus),
        programSessionId,
        p.SessionParticipantCreatedDate,
        p.SessionParticipantUpdatedDate);

    private async Task<SessionParticipantResponse> BuildParticipantResponseAsync(Guid participantId, CancellationToken ct)
    {
        var participant = await db.SessionParticipants
            .Include(p => p.Member)
            .AsNoTracking()
            .FirstAsync(p => p.SessionParticipantId == participantId, ct);

        var programSessionId = await db.ProgramSessions
            .Where(ps => ps.ProgramSessionSessionId == participant.SessionParticipantSessionId
                      && ps.ProgramSessionMemberId == participant.SessionParticipantMemberId)
            .Select(ps => (Guid?)ps.ProgramSessionId)
            .FirstOrDefaultAsync(ct);

        return ToParticipantResponse(participant, programSessionId);
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
        // SessionProductId/SessionMemberId are nullable now, so the join keys are cast to match —
        // a group session's SessionMemberId is null and never matches a MEMBER_COACHES row, so mc
        // comes back null for it, which is the correct "no address override for a group session"
        // outcome (ToResponse already falls back to the product's own location).
        var query =
            from s in sessionQuery
            join p in db.Products on s.SessionProductId equals (Guid?)p.ProductId into productJoin
            from p in productJoin.DefaultIfEmpty()
            join mc in db.MemberCoaches
                on new { MemberId = s.SessionMemberId, CoachId = s.SessionCoachId }
                equals new { MemberId = (Guid?)mc.MemberId, CoachId = mc.CoachId }
                into memberCoachJoin
            from mc in memberCoachJoin.DefaultIfEmpty()
            select new { Session = s, Coach = s.Coach, Member = s.Member, Product = p, MemberCoach = mc };

        var rows = await query.ToListAsync(ct);

        return rows
            .Select(r => ToResponse(r.Session, r.Coach, r.Member, r.Product, r.MemberCoach))
            .ToList();
    }
}
