using Mentora.Core.Entities;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mentora.Infrastructure.Seeding;

public static class CoachParameterSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var db     = services.GetRequiredService<MentoraDbContext>();
        var logger = services.GetRequiredService<ILogger<MentoraDbContext>>();

        var coachesWithoutParams = await db.Coaches
            .Where(c => !db.CoachParameters.Any(p => p.CoachId == c.CoachId))
            .ToListAsync();

        if (coachesWithoutParams.Count == 0)
            return;

        foreach (var coach in coachesWithoutParams)
        {
            db.CoachParameters.Add(new CoachParameter
            {
                CoachParameterHourlyRateEuros       = 50.00m,
                CoachParameterCancellationDelayHours = 24,
                CoachParameterCreatedDate            = DateTime.UtcNow,
                CoachParameterUpdatedDate            = DateTime.UtcNow,
                CoachId                             = coach.CoachId
            });
        }

        await db.SaveChangesAsync();
        logger.LogInformation("CoachParameterSeeder: created default parameters for {Count} coach(es).", coachesWithoutParams.Count);
    }
}
