using FluentValidation;
using Mentora.Core.DTOs.Coach;
using Mentora.Core.Exceptions;
using Mentora.Core.Interfaces;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mentora.Infrastructure.Services;

public class CoachSessionPreferencesService(
    MentoraDbContext db,
    IValidator<UpdateSessionPreferencesRequest> validator) : ICoachSessionPreferencesService
{
    public async Task<SessionPreferencesDto> GetAsync(Guid coachId, CancellationToken ct)
    {
        var coach = await db.Coaches.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CoachId == coachId, ct)
            ?? throw new NotFoundException($"Coach {coachId} not found.");
        return new(coach.CoachOffersVisio, coach.CoachOffersInPersonSolo, coach.CoachInPersonLocations);
    }

    public async Task<SessionPreferencesDto> UpdateAsync(
        Guid coachId, UpdateSessionPreferencesRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);

        var coach = await db.Coaches.FirstOrDefaultAsync(c => c.CoachId == coachId, ct)
            ?? throw new NotFoundException($"Coach {coachId} not found.");

        coach.CoachOffersVisio         = request.OffersVisio!.Value;
        coach.CoachOffersInPersonSolo  = request.OffersInPersonSolo!.Value;
        coach.CoachInPersonLocations   = request.InPersonLocations!.Select(l => l.Trim()).ToList();
        await db.SaveChangesAsync(ct);

        return new(coach.CoachOffersVisio, coach.CoachOffersInPersonSolo, coach.CoachInPersonLocations);
    }
}
