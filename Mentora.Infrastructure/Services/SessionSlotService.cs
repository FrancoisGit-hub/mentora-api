using Mentora.Core.DTOs.Sessions;
using Mentora.Core.Entities;
using Mentora.Core.Interfaces;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mentora.Infrastructure.Services;

public class SessionSlotService(MentoraDbContext db) : ISessionSlotService
{
    private static readonly HashSet<string> ValidTypes = ["VISIO", "PRESENTIEL", "SOLO"];

    public async Task<SessionSlotResponse> CreateSlotAsync(Guid coachId, CreateSessionSlotRequest request)
    {
        if (request.StartDate <= DateTime.UtcNow)
            throw new InvalidOperationException("Start date must be in the future.");

        if (request.EndDate <= request.StartDate)
            throw new InvalidOperationException("End date must be after start date.");

        if (!ValidTypes.Contains(request.Type))
            throw new InvalidOperationException("Type must be VISIO, PRESENTIEL or SOLO.");

        var parameters = await db.CoachParameters
            .FirstOrDefaultAsync(p => p.CoachId == coachId)
            ?? throw new InvalidOperationException("Coach parameters not found.");

        var creditsRequired = (int)Math.Ceiling(request.PriceEuros / parameters.CoachCreditValueEuros);

        var slot = new SessionSlot
        {
            SessionSlotStartDate = request.StartDate,
            SessionSlotEndDate = request.EndDate,
            SessionSlotPriceEuros = request.PriceEuros,
            SessionSlotCreditsRequired = creditsRequired,
            SessionSlotType = request.Type,
            SessionSlotIsAvailable = true,
            SessionSlotCreatedDate = DateTime.UtcNow,
            CoachId = coachId
        };

        db.SessionSlots.Add(slot);
        await db.SaveChangesAsync();

        return MapToResponse(slot);
    }

    public async Task<SessionSlotResponse> UpdateSlotAsync(Guid coachId, Guid slotId, CreateSessionSlotRequest request)
    {
        var slot = await db.SessionSlots
            .Include(s => s.Session)
            .FirstOrDefaultAsync(s => s.SessionSlotId == slotId && s.CoachId == coachId)
            ?? throw new InvalidOperationException("Session slot not found.");

        if (slot.Session != null && slot.Session.SessionStatus == "CONFIRMED")
            throw new InvalidOperationException("Cannot update a slot that is already booked.");

        if (request.StartDate <= DateTime.UtcNow)
            throw new InvalidOperationException("Start date must be in the future.");

        if (request.EndDate <= request.StartDate)
            throw new InvalidOperationException("End date must be after start date.");

        if (!ValidTypes.Contains(request.Type))
            throw new InvalidOperationException("Type must be VISIO, PRESENTIEL or SOLO.");

        var parameters = await db.CoachParameters
            .FirstOrDefaultAsync(p => p.CoachId == coachId)
            ?? throw new InvalidOperationException("Coach parameters not found.");

        slot.SessionSlotStartDate = request.StartDate;
        slot.SessionSlotEndDate = request.EndDate;
        slot.SessionSlotPriceEuros = request.PriceEuros;
        slot.SessionSlotCreditsRequired = (int)Math.Ceiling(request.PriceEuros / parameters.CoachCreditValueEuros);
        slot.SessionSlotType = request.Type;

        await db.SaveChangesAsync();

        return MapToResponse(slot);
    }

    public async Task DeleteSlotAsync(Guid coachId, Guid slotId)
    {
        var slot = await db.SessionSlots
            .Include(s => s.Session)
            .FirstOrDefaultAsync(s => s.SessionSlotId == slotId && s.CoachId == coachId)
            ?? throw new InvalidOperationException("Session slot not found.");

        if (slot.Session != null && slot.Session.SessionStatus == "CONFIRMED")
            throw new InvalidOperationException("Cannot delete a slot that is already booked.");

        db.SessionSlots.Remove(slot);
        await db.SaveChangesAsync();
    }

    public async Task<List<SessionSlotResponse>> GetAvailableSlotsAsync(Guid coachId, DateTime from, DateTime to)
    {
        var slots = await db.SessionSlots
            .Where(s => s.CoachId == coachId
                     && s.SessionSlotIsAvailable
                     && s.SessionSlotStartDate >= from
                     && s.SessionSlotStartDate <= to)
            .OrderBy(s => s.SessionSlotStartDate)
            .ToListAsync();

        return slots.Select(MapToResponse).ToList();
    }

    private static SessionSlotResponse MapToResponse(SessionSlot slot) =>
        new(
            SessionSlotId: slot.SessionSlotId,
            StartDate: slot.SessionSlotStartDate,
            EndDate: slot.SessionSlotEndDate,
            CreditsRequired: slot.SessionSlotCreditsRequired,
            Type: slot.SessionSlotType,
            IsAvailable: slot.SessionSlotIsAvailable
        );
}
