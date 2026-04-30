using Mentora.Core.Entities;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mentora.Infrastructure.Seeding;

public static class OfferProgramSeeder
{
    private static readonly string[] DefaultPrograms =
    [
        "Prise de masse",
        "Perte de poids",
        "Préparation Marathon",
        "Préparation Hyrox"
    ];

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db     = services.GetRequiredService<MentoraDbContext>();
        var logger = services.GetRequiredService<ILogger<MentoraDbContext>>();

        // Only seed coaches that have zero active programs
        var coachesWithoutPrograms = await db.Coaches
            .Where(c => !db.OfferPrograms.Any(p => p.CoachId == c.CoachId && p.OfferProgramIsActive))
            .ToListAsync();

        if (coachesWithoutPrograms.Count == 0)
            return;

        foreach (var coach in coachesWithoutPrograms)
        {
            foreach (var name in DefaultPrograms)
            {
                db.OfferPrograms.Add(new OfferProgram
                {
                    OfferProgramName        = name,
                    OfferProgramIsActive    = true,
                    OfferProgramCreatedDate = DateTime.UtcNow,
                    CoachId                 = coach.CoachId
                });
            }
        }

        await db.SaveChangesAsync();
        logger.LogInformation("OfferProgramSeeder: seeded {Programs} program(s) for {Count} coach(es).",
            DefaultPrograms.Length * coachesWithoutPrograms.Count, coachesWithoutPrograms.Count);
    }
}
