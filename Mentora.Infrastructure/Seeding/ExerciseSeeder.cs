using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mentora.Infrastructure.Seeding;

public static class ExerciseSeeder
{
    private sealed record SeedExercise(
        string Name,
        string Description,
        string Instructions,
        MuscleGroup MuscleGroup,
        Equipment Equipment,
        bool IsPolyarticular);

    private static readonly SeedExercise[] DefaultExercises =
    [
        // Pectoraux
        new("Développé couché barre",
            "Exercice polyarticulaire de référence pour les pectoraux.",
            "Allongé sur un banc, saisir la barre à largeur épaules, descendre jusqu'à effleurer la poitrine puis pousser jusqu'à extension complète des bras.",
            MuscleGroup.Chest, Equipment.Barbell, true),
        new("Développé couché haltères",
            "Variante avec haltères pour une amplitude accrue.",
            "Allongé sur un banc, un haltère dans chaque main au niveau de la poitrine, pousser vers le haut jusqu'à extension complète.",
            MuscleGroup.Chest, Equipment.Dumbbell, false),
        new("Écarté couché haltères",
            "Mouvement d'isolation des pectoraux.",
            "Allongé sur un banc, bras légèrement fléchis, écarter les haltères sur les côtés puis les ramener au-dessus de la poitrine.",
            MuscleGroup.Chest, Equipment.Dumbbell, false),
        new("Pompes",
            "Exercice au poids du corps pour les pectoraux, épaules et triceps.",
            "Corps gainé en position de planche, descendre la poitrine vers le sol puis repousser jusqu'à extension des bras.",
            MuscleGroup.Chest, Equipment.Bodyweight, false),
        new("Développé incliné haltères",
            "Cible la portion haute des pectoraux.",
            "Sur un banc incliné, pousser les haltères depuis les épaules jusqu'à extension complète des bras.",
            MuscleGroup.Chest, Equipment.Dumbbell, false),

        // Dos
        new("Soulevé de terre",
            "Exercice polyarticulaire majeur pour la chaîne postérieure.",
            "Pieds sous la barre, dos droit, saisir la barre et se redresser en poussant dans le sol jusqu'à extension complète des hanches et des genoux.",
            MuscleGroup.Back, Equipment.Barbell, true),
        new("Traction / tirage vertical",
            "Exercice polyarticulaire pour le grand dorsal, en traction au poids du corps ou à la poulie haute.",
            "Suspendu à la barre (ou poulie haute), tirer le corps vers le haut jusqu'à ce que le menton dépasse la barre, puis redescendre en contrôlant.",
            MuscleGroup.Back, Equipment.Cable, true),
        new("Rowing barre buste penché",
            "Exercice polyarticulaire pour l'épaisseur du dos.",
            "Buste penché en avant, dos droit, tirer la barre vers le bas de l'abdomen en resserrant les omoplates.",
            MuscleGroup.Back, Equipment.Barbell, true),
        new("Rowing haltère unilatéral",
            "Travaille chaque côté du dos indépendamment.",
            "Un genou et une main en appui sur un banc, tirer l'haltère vers la hanche en resserrant l'omoplate.",
            MuscleGroup.Back, Equipment.Dumbbell, false),
        new("Tirage horizontal poulie basse",
            "Exercice à la poulie pour le milieu du dos.",
            "Assis face à la poulie basse, tirer la poignée vers l'abdomen en gardant le dos droit et les épaules basses.",
            MuscleGroup.Back, Equipment.Cable, false),

        // Épaules
        new("Développé épaules barre",
            "Exercice polyarticulaire de référence pour les épaules.",
            "Debout ou assis, pousser la barre depuis les épaules jusqu'à extension complète des bras au-dessus de la tête.",
            MuscleGroup.Shoulders, Equipment.Barbell, true),
        new("Élévations latérales haltères",
            "Mouvement d'isolation du deltoïde moyen.",
            "Debout, un haltère dans chaque main, lever les bras sur les côtés jusqu'à hauteur d'épaule puis redescendre.",
            MuscleGroup.Shoulders, Equipment.Dumbbell, false),
        new("Élévations frontales haltères",
            "Cible le deltoïde antérieur.",
            "Debout, lever un haltère devant soi jusqu'à hauteur d'épaule puis redescendre en contrôlant.",
            MuscleGroup.Shoulders, Equipment.Dumbbell, false),
        new("Oiseau (élévations arrière)",
            "Cible le deltoïde postérieur.",
            "Buste penché en avant, écarter les haltères sur les côtés en resserrant les omoplates.",
            MuscleGroup.Shoulders, Equipment.Dumbbell, false),

        // Biceps
        new("Curl biceps barre",
            "Exercice de base pour les biceps.",
            "Debout, bras tendus, fléchir les coudes pour amener la barre vers les épaules puis redescendre en contrôlant.",
            MuscleGroup.Biceps, Equipment.Barbell, false),
        new("Curl biceps haltères alterné",
            "Travaille chaque bras indépendamment.",
            "Debout, fléchir un coude à la fois pour amener l'haltère vers l'épaule en supinant l'avant-bras.",
            MuscleGroup.Biceps, Equipment.Dumbbell, false),
        new("Curl marteau haltères",
            "Cible le biceps et le brachial.",
            "Debout, paumes face à face, fléchir les coudes pour lever les haltères sans tourner les poignets.",
            MuscleGroup.Biceps, Equipment.Dumbbell, false),

        // Triceps
        new("Dips",
            "Exercice polyarticulaire pour les triceps et le bas des pectoraux.",
            "En appui sur des barres parallèles, descendre en fléchissant les coudes puis repousser jusqu'à extension complète des bras.",
            MuscleGroup.Triceps, Equipment.Bodyweight, true),
        new("Extension triceps poulie haute",
            "Exercice d'isolation des triceps à la poulie.",
            "Face à la poulie haute, coudes fixes le long du corps, pousser la barre vers le bas jusqu'à extension complète.",
            MuscleGroup.Triceps, Equipment.Cable, false),
        new("Barre au front",
            "Exercice d'isolation des triceps allongé sur un banc.",
            "Allongé sur un banc, barre au-dessus du front, fléchir puis étendre les coudes sans bouger les bras.",
            MuscleGroup.Triceps, Equipment.Barbell, false),

        // Avant-bras
        new("Curl poignet barre",
            "Isolation des fléchisseurs de l'avant-bras.",
            "Avant-bras posés sur les cuisses ou un banc, fléchir les poignets pour lever la barre puis redescendre.",
            MuscleGroup.Forearms, Equipment.Barbell, false),
        new("Extension poignet haltère",
            "Isolation des extenseurs de l'avant-bras.",
            "Avant-bras posé sur une surface, paume vers le bas, lever puis abaisser l'haltère en pliant le poignet.",
            MuscleGroup.Forearms, Equipment.Dumbbell, false),
        new("Suspension à la barre",
            "Renforce la force de préhension.",
            "Suspendu à une barre, bras tendus, maintenir la position le plus longtemps possible sans lâcher.",
            MuscleGroup.Forearms, Equipment.Bodyweight, false),

        // Abdominaux
        new("Crunch",
            "Exercice de base pour le grand droit de l'abdomen.",
            "Allongé sur le dos, genoux fléchis, enrouler le buste vers les genoux en contractant les abdominaux.",
            MuscleGroup.Abs, Equipment.Bodyweight, false),
        new("Gainage planche",
            "Exercice de gainage statique pour la sangle abdominale.",
            "En appui sur les avant-bras et les pointes de pieds, maintenir le corps aligné et gainé.",
            MuscleGroup.Abs, Equipment.Bodyweight, false),
        new("Relevé de jambes suspendu",
            "Cible le bas des abdominaux.",
            "Suspendu à une barre, jambes tendues, relever les jambes jusqu'à l'horizontale puis redescendre en contrôlant.",
            MuscleGroup.Abs, Equipment.Bodyweight, false),
        new("Rotation russe",
            "Cible les obliques.",
            "Assis, buste légèrement incliné en arrière, faire pivoter le buste d'un côté à l'autre en gardant les abdominaux gainés.",
            MuscleGroup.Abs, Equipment.Elastic, false),

        // Quadriceps
        new("Squat",
            "Exercice polyarticulaire de référence pour les jambes.",
            "Barre sur les trapèzes, pieds largeur épaules, descendre en fléchissant hanches et genoux jusqu'à cuisses parallèles au sol puis remonter.",
            MuscleGroup.Quadriceps, Equipment.Barbell, true),
        new("Fentes avant",
            "Exercice unilatéral pour les quadriceps et les fessiers.",
            "Faire un grand pas en avant, descendre le genou arrière vers le sol puis revenir en position debout.",
            MuscleGroup.Quadriceps, Equipment.Dumbbell, false),
        new("Presse à cuisses",
            "Exercice guidé pour les quadriceps.",
            "Assis à la machine, pousser la plateforme en tendant les jambes sans verrouiller les genoux.",
            MuscleGroup.Quadriceps, Equipment.Machine, false),
        new("Leg extension",
            "Isolation des quadriceps à la machine.",
            "Assis à la machine, tendre les jambes contre la résistance puis redescendre en contrôlant.",
            MuscleGroup.Quadriceps, Equipment.Machine, false),

        // Ischio-jambiers
        new("Leg curl allongé",
            "Isolation des ischio-jambiers à la machine.",
            "Allongé à la machine, fléchir les genoux pour amener les talons vers les fessiers puis redescendre.",
            MuscleGroup.Hamstrings, Equipment.Machine, false),
        new("Soulevé de terre jambes tendues",
            "Cible les ischio-jambiers et les lombaires.",
            "Jambes quasi tendues, descendre la barre le long des jambes en gardant le dos droit puis remonter.",
            MuscleGroup.Hamstrings, Equipment.Barbell, false),
        new("Good morning barre",
            "Renforce les ischio-jambiers et les lombaires.",
            "Barre sur les trapèzes, pencher le buste en avant en gardant le dos droit puis revenir en position debout.",
            MuscleGroup.Hamstrings, Equipment.Barbell, false),

        // Fessiers
        new("Hip thrust barre",
            "Exercice de référence pour les fessiers.",
            "Dos appuyé sur un banc, barre sur les hanches, pousser les hanches vers le haut jusqu'à extension complète.",
            MuscleGroup.Glutes, Equipment.Barbell, false),
        new("Fentes bulgares",
            "Exercice unilatéral intense pour les fessiers et quadriceps.",
            "Pied arrière surélevé sur un banc, descendre le genou avant vers le sol puis remonter.",
            MuscleGroup.Glutes, Equipment.Dumbbell, false),
        new("Kickback à la poulie",
            "Isolation des fessiers à la poulie.",
            "Face à la poulie basse, sangle à la cheville, tendre la jambe vers l'arrière en contractant le fessier.",
            MuscleGroup.Glutes, Equipment.Cable, false),

        // Mollets
        new("Mollets debout",
            "Exercice de référence pour les mollets.",
            "Debout à la machine, monter sur la pointe des pieds puis redescendre en étirant les mollets.",
            MuscleGroup.Calves, Equipment.Machine, false),
        new("Mollets assis",
            "Cible le muscle soléaire.",
            "Assis à la machine, genoux sous les coussinets, monter sur la pointe des pieds puis redescendre.",
            MuscleGroup.Calves, Equipment.Machine, false),

        // Corps entier
        new("Burpees",
            "Exercice cardio-renforcement complet.",
            "Depuis la position debout, descendre en squat, poser les mains au sol, envoyer les jambes en planche, revenir puis sauter.",
            MuscleGroup.FullBody, Equipment.Bodyweight, false),
        new("Kettlebell swing",
            "Exercice balistique pour la chaîne postérieure et le cardio.",
            "Kettlebell tenue à deux mains, faire un mouvement de hanches pour projeter le kettlebell jusqu'à hauteur d'épaule.",
            MuscleGroup.FullBody, Equipment.Kettlebell, false),
        new("Thruster haltères",
            "Combine squat et développé épaules.",
            "Haltères aux épaules, descendre en squat puis remonter en poussant les haltères au-dessus de la tête.",
            MuscleGroup.FullBody, Equipment.Dumbbell, false),

        // Cardio
        new("Corde à sauter",
            "Exercice cardiovasculaire simple et efficace.",
            "Sauter à la corde en gardant un rythme régulier, coudes proches du corps.",
            MuscleGroup.Cardio, Equipment.Other, false),
        new("Course à pied (tapis)",
            "Exercice cardiovasculaire d'endurance.",
            "Courir à allure régulière sur tapis en maintenant une posture droite et une respiration contrôlée.",
            MuscleGroup.Cardio, Equipment.Machine, false),
        new("Rameur",
            "Exercice cardiovasculaire complet sollicitant jambes, dos et bras.",
            "Pousser avec les jambes, puis tirer la poignée vers l'abdomen en gardant le dos droit, et revenir en position de départ.",
            MuscleGroup.Cardio, Equipment.Machine, false),
    ];

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db     = services.GetRequiredService<MentoraDbContext>();
        var logger = services.GetRequiredService<ILogger<MentoraDbContext>>();

        var existingNames = await db.Exercises
            .Where(e => e.ExerciseCoachId == null)
            .Select(e => e.ExerciseName)
            .ToListAsync();

        var existingSet = existingNames.ToHashSet();

        var missing = DefaultExercises
            .Where(x => !existingSet.Contains(x.Name))
            .ToList();

        if (missing.Count == 0)
            return;

        var now = DateTime.UtcNow;

        foreach (var item in missing)
        {
            db.Exercises.Add(new Exercise
            {
                ExerciseCoachId         = null,
                ExerciseName            = item.Name,
                ExerciseDescription     = item.Description,
                ExerciseInstructions    = item.Instructions,
                ExerciseMuscleGroup     = item.MuscleGroup,
                ExerciseEquipment       = item.Equipment,
                ExerciseIsPolyarticular = item.IsPolyarticular,
                ExerciseIsActive        = true,
                ExerciseCreatedDate     = now,
                ExerciseUpdatedDate     = now,
            });
        }

        await db.SaveChangesAsync();
        logger.LogInformation("ExerciseSeeder: seeded {Count} Mentora catalogue exercise(s).", missing.Count);
    }
}
