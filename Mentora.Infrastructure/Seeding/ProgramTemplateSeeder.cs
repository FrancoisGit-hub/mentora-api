using System.Text.Json;
using Mentora.Core.DTOs.ProgramTemplate.Body;
using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mentora.Infrastructure.Seeding;

public static class ProgramTemplateSeeder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private sealed record SeedTemplate(
        string Name,
        string Description,
        ProgramGoal Goal,
        int DurationWeeks,
        Func<Dictionary<string, Guid>, ProgramTemplateBody> BuildBody);

    private static readonly SeedTemplate[] Templates =
    [
        new("Prise de masse — débutant",
            "Programme 4 semaines pour débuter la prise de masse musculaire, 3 séances par semaine.",
            ProgramGoal.MuscleGain, 4, BuildMuscleGainBody),
        new("Remise en forme — débutant",
            "Programme 4 semaines pour reprendre une activité physique générale, 3 séances par semaine.",
            ProgramGoal.GeneralFitness, 4, BuildGeneralFitnessBody),
    ];

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db     = services.GetRequiredService<MentoraDbContext>();
        var logger = services.GetRequiredService<ILogger<MentoraDbContext>>();

        var existingNames = await db.ProgramTemplates
            .Where(t => t.ProgramTemplateCoachId == null)
            .Select(t => t.ProgramTemplateName)
            .ToListAsync();
        var existingSet = existingNames.ToHashSet();

        var missing = Templates.Where(t => !existingSet.Contains(t.Name)).ToList();
        if (missing.Count == 0)
            return;

        var exerciseIdByName = await db.Exercises
            .Where(e => e.ExerciseCoachId == null)
            .ToDictionaryAsync(e => e.ExerciseName, e => e.ExerciseId);

        var now = DateTime.UtcNow;
        var added = 0;

        foreach (var template in missing)
        {
            ProgramTemplateBody body;
            try
            {
                body = template.BuildBody(exerciseIdByName);
            }
            catch (KeyNotFoundException ex)
            {
                logger.LogWarning(
                    "ProgramTemplateSeeder: skipping '{Name}' — referenced exercise not found ({Message}).",
                    template.Name, ex.Message);
                continue;
            }

            db.ProgramTemplates.Add(new ProgramTemplate
            {
                ProgramTemplateCoachId       = null,
                ProgramTemplateName          = template.Name,
                ProgramTemplateDescription   = template.Description,
                ProgramTemplateGoal          = template.Goal,
                ProgramTemplateDurationWeeks = template.DurationWeeks,
                ProgramTemplateBody          = JsonSerializer.Serialize(body, JsonOptions),
                ProgramTemplateIsActive      = true,
                ProgramTemplateCreatedDate   = now,
                ProgramTemplateUpdatedDate   = now,
            });
            added++;
        }

        if (added == 0)
            return;

        await db.SaveChangesAsync();
        logger.LogInformation("ProgramTemplateSeeder: seeded {Count} Mentora program template(s).", added);
    }

    // ── Body builders ──────────────────────────────────────────────────────────

    private static ProgramTemplateBody BuildMuscleGainBody(Dictionary<string, Guid> ex)
    {
        List<ProgramTemplateSession> BuildSessions() =>
        [
            new ProgramTemplateSession
            {
                Name = "Séance A — Haut du corps", Type = "PRESENTIEL_SOLO", DayOfWeek = 1, Position = 1,
                Circuits =
                [
                    new ProgramTemplateCircuit
                    {
                        Name = "Poussée / tirage", Position = 1, Mode = "STANDARD",
                        Exercises =
                        [
                            Std(ex["Développé couché barre"], 1, 4, 8, "KG", 90),
                            Std(ex["Rowing barre buste penché"], 2, 4, 8, "KG", 90),
                            Std(ex["Développé épaules barre"], 3, 3, 10, "KG", 60),
                            Std(ex["Traction / tirage vertical"], 4, 3, 8, "BODYWEIGHT", 90),
                        ]
                    }
                ]
            },
            new ProgramTemplateSession
            {
                Name = "Séance B — Bas du corps", Type = "PRESENTIEL_SOLO", DayOfWeek = 3, Position = 2,
                Circuits =
                [
                    new ProgramTemplateCircuit
                    {
                        Name = "Jambes", Position = 1, Mode = "STANDARD",
                        Exercises =
                        [
                            Std(ex["Squat"], 1, 4, 8, "KG", 120),
                            Std(ex["Soulevé de terre jambes tendues"], 2, 3, 10, "KG", 90),
                            Std(ex["Presse à cuisses"], 3, 3, 12, "KG", 90),
                            Std(ex["Mollets debout"], 4, 3, 15, "KG", 60),
                        ]
                    }
                ]
            },
            new ProgramTemplateSession
            {
                Name = "Séance C — Bras et abdos", Type = "PRESENTIEL_SOLO", DayOfWeek = 5, Position = 3,
                Circuits =
                [
                    new ProgramTemplateCircuit
                    {
                        Name = "Bras", Position = 1, Mode = "STANDARD",
                        Exercises =
                        [
                            Std(ex["Curl biceps barre"], 1, 3, 10, "KG", 60),
                            Std(ex["Barre au front"], 2, 3, 10, "KG", 60),
                            Std(ex["Dips"], 3, 3, 10, "BODYWEIGHT", 60),
                        ]
                    },
                    new ProgramTemplateCircuit
                    {
                        Name = "Abdos", Position = 2, Mode = "STANDARD",
                        Exercises =
                        [
                            Std(ex["Crunch"], 1, 3, 15, "BODYWEIGHT", 45),
                            Std(ex["Gainage planche"], 2, 3, 1, "BODYWEIGHT", 45),
                        ]
                    }
                ]
            },
        ];

        return BuildFourWeekBody(BuildSessions);
    }

    private static ProgramTemplateBody BuildGeneralFitnessBody(Dictionary<string, Guid> ex)
    {
        List<ProgramTemplateSession> BuildSessions() =>
        [
            new ProgramTemplateSession
            {
                Name = "Séance A — Cardio et renforcement", Type = "PRESENTIEL_SOLO", DayOfWeek = 1, Position = 1,
                Circuits =
                [
                    new ProgramTemplateCircuit
                    {
                        Name = "Renforcement", Position = 1, Mode = "STANDARD",
                        Exercises =
                        [
                            Std(ex["Squat"], 1, 3, 12, "BODYWEIGHT", 60),
                            Std(ex["Pompes"], 2, 3, 10, "BODYWEIGHT", 60),
                            Std(ex["Gainage planche"], 3, 3, 1, "BODYWEIGHT", 45),
                        ]
                    },
                    new ProgramTemplateCircuit
                    {
                        Name = "Tabata Burpees", Position = 2, Mode = "INTERVAL",
                        Rounds = 8, RestBetweenRoundsSeconds = 60,
                        Note = "Format Tabata : 20 s d'effort / 10 s de repos.",
                        Exercises =
                        [
                            Interval(ex["Burpees"], 1, "BODYWEIGHT", 20, 10),
                        ]
                    }
                ]
            },
            new ProgramTemplateSession
            {
                Name = "Séance B — Mobilité et gainage", Type = "PRESENTIEL_SOLO", DayOfWeek = 3, Position = 2,
                Circuits =
                [
                    new ProgramTemplateCircuit
                    {
                        Name = "Mobilité et gainage", Position = 1, Mode = "STANDARD",
                        Exercises =
                        [
                            Std(ex["Fentes avant"], 1, 3, 10, "BODYWEIGHT", 60),
                            Std(ex["Rotation russe"], 2, 3, 15, "BODYWEIGHT", 45),
                        ]
                    }
                ]
            },
            new ProgramTemplateSession
            {
                Name = "Séance C — Full body", Type = "PRESENTIEL_SOLO", DayOfWeek = 5, Position = 3,
                Circuits =
                [
                    new ProgramTemplateCircuit
                    {
                        Name = "Full body", Position = 1, Mode = "STANDARD",
                        Exercises =
                        [
                            Std(ex["Kettlebell swing"], 1, 3, 15, "KG", 60),
                            Std(ex["Rowing haltère unilatéral"], 2, 3, 10, "KG", 60),
                            Std(ex["Mollets assis"], 3, 3, 15, "KG", 45),
                        ]
                    }
                ]
            },
        ];

        return BuildFourWeekBody(BuildSessions);
    }

    private static ProgramTemplateBody BuildFourWeekBody(Func<List<ProgramTemplateSession>> sessionsFactory)
    {
        var microcycles = new List<ProgramTemplateBlock>();
        for (var week = 1; week <= 4; week++)
        {
            microcycles.Add(new ProgramTemplateBlock
            {
                Level      = "MICROCYCLE",
                Name       = $"Semaine {week}",
                Position   = week,
                WeekNumber = week,
                Sessions   = sessionsFactory(),
            });
        }

        return new ProgramTemplateBody
        {
            BodyVersion = 1,
            Blocks =
            [
                new ProgramTemplateBlock
                {
                    Level = "MACROCYCLE",
                    Name = "Cycle principal",
                    Position = 1,
                    Blocks =
                    [
                        new ProgramTemplateBlock
                        {
                            Level = "MESOCYCLE",
                            Name = "Bloc 1",
                            Position = 1,
                            Blocks = microcycles,
                        }
                    ]
                }
            ]
        };
    }

    private static ProgramTemplateExercise Std(
        Guid exerciseId, int position, int sets, int reps, string loadType, int restSeconds) => new()
    {
        ExerciseId     = exerciseId,
        Position       = position,
        LoadType       = loadType,
        PrescribedSets = sets,
        PrescribedReps = reps,
        RestSeconds    = restSeconds,
    };

    private static ProgramTemplateExercise Interval(
        Guid exerciseId, int position, string loadType, int workSeconds, int restWorkSeconds) => new()
    {
        ExerciseId      = exerciseId,
        Position        = position,
        LoadType        = loadType,
        WorkSeconds     = workSeconds,
        RestWorkSeconds = restWorkSeconds,
    };
}
