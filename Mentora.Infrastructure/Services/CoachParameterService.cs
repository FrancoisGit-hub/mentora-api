using Mentora.Core.DTOs.Lot2;
using Mentora.Core.Exceptions;
using Mentora.Core.Interfaces;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mentora.Infrastructure.Services;

public class CoachParameterService(MentoraDbContext db) : ICoachParameterService
{
    public async Task<CoachParameterDto> GetAsync(Guid coachId)
    {
        var param = await db.CoachParameters
            .FirstOrDefaultAsync(p => p.CoachId == coachId)
            ?? throw new NotFoundException("No parameters found for this coach.");

        return ToDto(param);
    }

    public async Task<CoachParameterDto> UpdateAsync(Guid coachId, UpdateCoachParameterRequest request)
    {
        if (request.HourlyRateEuros <= 0)
            throw new InvalidOperationException("Hourly rate must be greater than zero.");

        if (request.CancellationDelayHours < 0)
            throw new InvalidOperationException("Cancellation delay cannot be negative.");

        var param = await db.CoachParameters
            .FirstOrDefaultAsync(p => p.CoachId == coachId)
            ?? throw new NotFoundException("No parameters found for this coach.");

        param.CoachParameterHourlyRateEuros = request.HourlyRateEuros;
        param.CoachParameterCancellationDelayHours = request.CancellationDelayHours;
        param.CoachParameterUpdatedDate = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return ToDto(param);
    }

    private static CoachParameterDto ToDto(Core.Entities.CoachParameter p) =>
        new(p.CoachParameterHourlyRateEuros, p.CoachParameterCancellationDelayHours, p.CoachParameterUpdatedDate);
}
