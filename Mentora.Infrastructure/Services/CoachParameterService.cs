using Mentora.Core.DTOs.Coach;
using Mentora.Core.Interfaces;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mentora.Infrastructure.Services;

public class CoachParameterService(MentoraDbContext db) : ICoachParameterService
{
    public async Task<CoachParameterResponse> GetParametersAsync(Guid coachId)
    {
        var parameters = await db.CoachParameters
            .FirstOrDefaultAsync(p => p.CoachId == coachId)
            ?? throw new InvalidOperationException("Coach parameters not found.");

        return new CoachParameterResponse(
            CreditValueEuros: parameters.CoachCreditValueEuros,
            CancellationDelayHours: parameters.CoachCancellationDelayHours,
            UpdatedAt: parameters.CoachParameterUpdatedDate
        );
    }

    public async Task<CoachParameterResponse> UpdateParametersAsync(Guid coachId, UpdateCoachParameterRequest request)
    {
        var parameters = await db.CoachParameters
            .FirstOrDefaultAsync(p => p.CoachId == coachId)
            ?? throw new InvalidOperationException("Coach parameters not found.");

        parameters.CoachCreditValueEuros = request.CreditValueEuros;
        parameters.CoachCancellationDelayHours = request.CancellationDelayHours;
        parameters.CoachParameterUpdatedDate = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return new CoachParameterResponse(
            CreditValueEuros: parameters.CoachCreditValueEuros,
            CancellationDelayHours: parameters.CoachCancellationDelayHours,
            UpdatedAt: parameters.CoachParameterUpdatedDate
        );
    }
}
