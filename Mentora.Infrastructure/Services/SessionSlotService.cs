using FluentValidation;
using Mentora.Core.DTOs.Coach;
using Mentora.Core.DTOs.Lot2;
using Mentora.Core.DTOs.Member;
using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Mentora.Core.Exceptions;
using Mentora.Core.Interfaces;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mentora.Infrastructure.Services;

public class SessionSlotService(
    MentoraDbContext db,
    IValidator<ListSessionSlotsRequest> listSlotsValidator) : ISessionSlotService
{
    // ── Coach-facing mutations ─────────────────────────────────────────────────

    public async Task<SessionSlotDto> CreateAsync(Guid coachId, CreateSessionSlotRequest request)
    {
        var offerType = ParseOfferType(request.OfferType);

        if (request.EndDate <= request.StartDate)
            throw new InvalidOperationException("End date must be after start date.");

        var durationMinutes = (int)(request.EndDate - request.StartDate).TotalMinutes;

        var slot = new SessionSlot
        {
            SessionSlotStartDate       = request.StartDate,
            SessionSlotEndDate         = request.EndDate,
            SessionSlotOfferType       = offerType,
            SessionSlotDurationMinutes = durationMinutes,
            SessionSlotIsAvailable     = true,
            SessionSlotCreatedDate     = DateTime.UtcNow,
            CoachId                    = coachId
        };

        db.SessionSlots.Add(slot);
        await db.SaveChangesAsync();

        return ToSlotDto(slot);
    }

    public async Task<SessionSlotDto> UpdateAsync(Guid coachId, Guid slotId, UpdateSessionSlotRequest request)
    {
        var slot = await db.SessionSlots
            .FirstOrDefaultAsync(s => s.SessionSlotId == slotId && s.CoachId == coachId)
            ?? throw new NotFoundException("Session slot not found.");

        if (!slot.SessionSlotIsAvailable)
            throw new ConflictException("Cannot modify a slot that is no longer available.");

        var offerType = ParseOfferType(request.OfferType);

        if (request.EndDate <= request.StartDate)
            throw new InvalidOperationException("End date must be after start date.");

        slot.SessionSlotStartDate       = request.StartDate;
        slot.SessionSlotEndDate         = request.EndDate;
        slot.SessionSlotOfferType       = offerType;
        slot.SessionSlotDurationMinutes = (int)(request.EndDate - request.StartDate).TotalMinutes;

        await db.SaveChangesAsync();

        return ToSlotDto(slot);
    }

    public async Task DeleteAsync(Guid coachId, Guid slotId)
    {
        var slot = await db.SessionSlots
            .FirstOrDefaultAsync(s => s.SessionSlotId == slotId && s.CoachId == coachId)
            ?? throw new NotFoundException("Session slot not found.");

        if (!slot.SessionSlotIsAvailable)
            throw new ConflictException("Cannot delete a slot that is no longer available.");

        db.SessionSlots.Remove(slot);
        await db.SaveChangesAsync();
    }

    public async Task<List<CoachSessionSlotDto>> ListForCoachAsync(
        Guid coachId, ListSessionSlotsRequest request, CancellationToken ct)
    {
        await listSlotsValidator.ValidateAndThrowAsync(request, ct);

        // SessionSlotStartDate is TIMESTAMPTZ — Npgsql requires DateTimeKind.Utc, and DateOnly
        // has no timezone of its own, so the range boundaries are treated as UTC calendar days.
        var fromDate = DateTime.SpecifyKind(request.From!.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var toDateExclusive = DateTime.SpecifyKind(
            request.To!.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);

        IQueryable<SessionSlot> slotsQuery = db.SessionSlots
            .Where(s => s.CoachId == coachId
                     && s.SessionSlotStartDate >= fromDate
                     && s.SessionSlotStartDate < toDateExclusive);

        if (request.IsAvailable.HasValue)
            slotsQuery = slotsQuery.Where(s => s.SessionSlotIsAvailable == request.IsAvailable.Value);

        // Single query: LEFT JOIN to SESSIONS (excluding cancelled bookings, which free the slot
        // back up conceptually) and LEFT JOIN to MEMBERS on the booking session's member. No
        // per-row lookups — booked-member identity is resolved in the same round trip as the slots.
        var rows = await (
            from s in slotsQuery
            join sess in db.Sessions.Where(x => x.SessionStatus != SessionStatus.Cancelled)
                on s.SessionSlotId equals sess.SessionSlotId into sessJoin
            from sess in sessJoin.DefaultIfEmpty()
            join m in db.Members
                on sess.SessionMemberId equals m.MemberId into memberJoin
            from m in memberJoin.DefaultIfEmpty()
            orderby s.SessionSlotStartDate
            select new { Slot = s, Session = sess, Member = m })
            .AsNoTracking()
            .ToListAsync(ct);

        return rows.Select(r => new CoachSessionSlotDto(
            r.Slot.SessionSlotId,
            r.Slot.SessionSlotStartDate,
            r.Slot.SessionSlotEndDate,
            EnumMappings.OfferTypeMapping.ToWire(r.Slot.SessionSlotOfferType),
            r.Slot.SessionSlotDurationMinutes,
            r.Slot.SessionSlotIsAvailable,
            r.Session?.SessionId,
            r.Session?.SessionMemberId,
            r.Member?.MemberFirstName,
            r.Member?.MemberLastName)).ToList();
    }

    // ── Member-facing read ─────────────────────────────────────────────────────

    public async Task<List<MemberSessionSlotDto>> GetAvailableSlotsForMemberAsync(
        Guid memberId,
        Guid? coachId,
        Guid? voucherId,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken ct)
    {
        if (!coachId.HasValue)
            throw new InvalidOperationException("coachId is required.");

        var now           = DateTime.UtcNow;
        var effectiveFrom = fromDate ?? now;
        var effectiveTo   = toDate   ?? now.AddDays(60);

        if (effectiveFrom >= effectiveTo)
            throw new InvalidOperationException("fromDate must be before toDate.");

        if ((effectiveTo - effectiveFrom).TotalDays > 90)
            throw new InvalidOperationException("Date range cannot exceed 90 days.");

        // ── Voucher validation (when provided) ─────────────────────────────────
        // Single query on memberId + voucherId: missing OR foreign-member → same 404, no info leak.
        SessionVoucher? voucher = null;
        if (voucherId.HasValue)
        {
            voucher = await db.SessionVouchers
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    v => v.VoucherId == voucherId.Value && v.MemberId == memberId, ct)
                ?? throw new NotFoundException("Voucher not found.");

            if (voucher.CoachId != coachId.Value)
                throw new InvalidOperationException("Voucher does not belong to this coach.");

            if (voucher.Status != VoucherStatus.Available)
            {
                var wire = EnumMappings.VoucherStatusMapping.ToWire(voucher.Status);
                throw new InvalidOperationException(
                    $"Voucher is not available for reservation (current status: {wire}).");
            }
        }

        // ── Slot query ─────────────────────────────────────────────────────────
        IQueryable<SessionSlot> query = voucher is not null
            // Strict mode: filter by voucher's exact offer type and duration
            ? db.SessionSlots.Where(s =>
                s.CoachId                    == voucher.CoachId &&
                s.SessionSlotOfferType       == voucher.OfferType &&
                s.SessionSlotDurationMinutes == voucher.DurationMinutes &&
                s.SessionSlotIsAvailable     &&
                s.SessionSlotStartDate       >  now &&
                s.SessionSlotStartDate       >= effectiveFrom &&
                s.SessionSlotStartDate       <  effectiveTo)
            // Plain mode: any available slot for the requested coach in the window
            : db.SessionSlots.Where(s =>
                s.CoachId                == coachId.Value &&
                s.SessionSlotIsAvailable &&
                s.SessionSlotStartDate   >= effectiveFrom &&
                s.SessionSlotStartDate   <  effectiveTo);

        var slots = await query
            .OrderBy(s => s.SessionSlotStartDate)
            .AsNoTracking()
            .ToListAsync(ct);

        if (slots.Count == 0)
            return [];

        // ── Auxiliary: resolve productLocation AND product CTA fields per (offerType, durationMinutes) ─
        // Single query for all published products of the coach, ordered by PRODUCT_CREATED_DATE ASC
        // so GroupBy.First() always picks the earliest-created product per pair.
        // No ProductLocation != null filter here — we need all published products for the CTA lookup;
        // the location dictionary filters in memory on the non-null subset.
        var productRows = await db.Products
            .Where(p =>
                p.CoachId       == coachId.Value &&
                p.ProductStatus == ProductStatus.Published)
            .OrderBy(p => p.ProductCreatedDate)
            .Select(p => new
            {
                p.ProductId,
                p.ProductName,
                p.ProductPriceEuros,
                p.ProductOfferType,
                p.ProductDurationMinutes,
                p.ProductLocation
            })
            .AsNoTracking()
            .ToListAsync(ct);

        // First product with a non-null location per (offerType, duration)
        var locationDict = productRows
            .Where(p => p.ProductLocation != null)
            .GroupBy(p => (p.ProductOfferType, p.ProductDurationMinutes))
            .ToDictionary(g => g.Key, g => g.First().ProductLocation);

        // First published product per (offerType, duration) for the "Buy this slot" CTA fields
        var productLookup = productRows
            .GroupBy(p => (p.ProductOfferType, p.ProductDurationMinutes))
            .ToDictionary(g => g.Key, g => g.First());

        // Effective address = this member's per-coach override, if set, else the product's own
        // location. Scoped to (memberId, coachId) so it can never leak another member's override.
        var memberOverrideAddress = await db.MemberCoaches
            .Where(mc => mc.MemberId == memberId && mc.CoachId == coachId.Value)
            .Select(mc => mc.MemberCoachPresentialAddress)
            .FirstOrDefaultAsync(ct);

        bool? compatibleFlag = voucherId.HasValue ? true : null;

        return slots.Select(s =>
        {
            var pair = (s.SessionSlotOfferType, s.SessionSlotDurationMinutes);
            locationDict.TryGetValue(pair, out var loc);
            productLookup.TryGetValue(pair, out var prod);

            return new MemberSessionSlotDto(
                SlotId:                  s.SessionSlotId,
                CoachId:                 s.CoachId,
                StartDate:               s.SessionSlotStartDate,
                EndDate:                 s.SessionSlotEndDate,
                OfferType:               EnumMappings.OfferTypeMapping.ToWire(s.SessionSlotOfferType),
                DurationMinutes:         s.SessionSlotDurationMinutes,
                ProductLocation:         memberOverrideAddress ?? loc,
                CompatibleWithVoucherId: compatibleFlag,
                ProductId:               prod?.ProductId,
                ProductName:             prod?.ProductName,
                ProductPriceEuros:       prod?.ProductPriceEuros);
        }).ToList();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static OfferType ParseOfferType(string raw)
    {
        if (!Enum.TryParse<OfferType>(raw, ignoreCase: false, out var offerType))
            throw new InvalidOperationException("Invalid offer type. Accepted values: Visio, PresentielSolo.");

        if (offerType == OfferType.PresentielGroupe)
            throw new InvalidOperationException(
                "PresentielGroupe is not available in V1. Accepted values: Visio, PresentielSolo.");

        return offerType;
    }

    private static SessionSlotDto ToSlotDto(SessionSlot s) => new(
        s.SessionSlotId,
        s.SessionSlotStartDate,
        s.SessionSlotEndDate,
        s.SessionSlotOfferType.ToString(),
        s.SessionSlotDurationMinutes,
        s.SessionSlotIsAvailable,
        s.SessionSlotCreatedDate);
}
