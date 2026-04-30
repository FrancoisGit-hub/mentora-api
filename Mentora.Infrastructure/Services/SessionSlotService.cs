using Mentora.Core.DTOs.Lot2;
using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Mentora.Core.Exceptions;
using Mentora.Core.Interfaces;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mentora.Infrastructure.Services;

public class SessionSlotService(MentoraDbContext db) : ISessionSlotService
{
    public async Task<SessionSlotDto> CreateAsync(Guid coachId, CreateSessionSlotRequest request)
    {
        var offerType = ParseOfferType(request.OfferType);

        if (request.EndDate <= request.StartDate)
            throw new InvalidOperationException("End date must be after start date.");

        var durationMinutes = (int)(request.EndDate - request.StartDate).TotalMinutes;

        var slot = new SessionSlot
        {
            SessionSlotStartDate     = request.StartDate,
            SessionSlotEndDate       = request.EndDate,
            SessionSlotOfferType     = offerType,
            SessionSlotDurationMinutes = durationMinutes,
            SessionSlotIsAvailable   = true,
            SessionSlotCreatedDate   = DateTime.UtcNow,
            CoachId                  = coachId
        };

        db.SessionSlots.Add(slot);
        await db.SaveChangesAsync();

        return ToDto(slot);
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

        slot.SessionSlotStartDate      = request.StartDate;
        slot.SessionSlotEndDate        = request.EndDate;
        slot.SessionSlotOfferType      = offerType;
        slot.SessionSlotDurationMinutes = (int)(request.EndDate - request.StartDate).TotalMinutes;

        await db.SaveChangesAsync();

        return ToDto(slot);
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

    public async Task<List<SessionSlotDto>> GetAvailableSlotsForMemberAsync(Guid memberId, DateTime from, DateTime to)
    {
        if (to <= from)
            throw new InvalidOperationException("'to' must be after 'from'.");

        if ((to - from).TotalDays > 31)
            throw new InvalidOperationException("Date range cannot exceed 31 days.");

        var primaryCoachId = await db.MemberCoaches
            .Where(mc => mc.MemberId == memberId && mc.IsPrimary)
            .Select(mc => (Guid?)mc.CoachId)
            .FirstOrDefaultAsync();

        if (primaryCoachId is null)
            throw new NotFoundException("No primary coach found for this member.");

        return await db.SessionSlots
            .Where(s =>
                s.CoachId == primaryCoachId &&
                s.SessionSlotIsAvailable &&
                s.SessionSlotStartDate >= from &&
                s.SessionSlotStartDate < to)
            .Select(s => ToDto(s))
            .ToListAsync();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses a string offer type. Rejects PresentielGroupe — not exposed in V1.
    /// </summary>
    private static OfferType ParseOfferType(string raw)
    {
        if (!Enum.TryParse<OfferType>(raw, ignoreCase: false, out var offerType))
            throw new InvalidOperationException("Invalid offer type. Accepted values: Visio, PresentielSolo.");

        if (offerType == OfferType.PresentielGroupe)
            throw new InvalidOperationException("PresentielGroupe is not available in V1. Accepted values: Visio, PresentielSolo.");

        return offerType;
    }

    private static SessionSlotDto ToDto(SessionSlot s) => new(
        s.SessionSlotId,
        s.SessionSlotStartDate,
        s.SessionSlotEndDate,
        s.SessionSlotOfferType.ToString(),
        s.SessionSlotDurationMinutes,
        s.SessionSlotIsAvailable,
        s.SessionSlotCreatedDate
    );
}
