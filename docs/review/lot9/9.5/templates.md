# Lot 9.5 — Program templates inventory (read-only)

Local DB only (`mentora-postgres-local`, port 5433). HEAD e93776f. Nothing was modified.

## 2.1 ProgramTemplateSeeder

File: `Mentora.Infrastructure/Seeding/ProgramTemplateSeeder.cs`.

- Seeds 2 Mentora templates (`PROGRAM_TEMPLATE_COACH_ID` NULL): "Prise de masse — débutant" (MUSCLE_GAIN, 4 weeks) and "Remise en forme — débutant" (GENERAL_FITNESS, 4 weeks).
- Exercise references: **by name**. It loads `EXERCISES` where `EXERCISE_COACH_ID IS NULL` into a name → id dictionary, and the body builders look names up with `ex["<name>"]`. The id is then serialized into the body JSON (`exerciseId`).
- Idempotent by template name: templates whose name already exists (coach NULL) are skipped. If a referenced name is missing (`KeyNotFoundException`), the template is skipped with a warning.
- Consequence of the by-name lookup: the names it uses must stay unique among coach-less exercises. It does not filter on `EXERCISE_IS_ACTIVE`.

## 2.2 Mentora templates (coach NULL)

| id | name | goal | weeks | active |
|---|---|---|---|---|
| 24abe8d4-fd8d-4885-8c1b-2fcec1f3343c | Prise de masse — débutant | MUSCLE_GAIN | 4 | true |
| 7f65f76c-70b8-405d-9a89-1c0b97623a0f | Remise en forme — débutant | GENERAL_FITNESS | 4 | true |

Body contents (jsonb, counted with `jsonb_path_query(... 'strict $.**.<key>')`):

| template | blocks (all levels) | root blocks | sessions | circuits | exercises |
|---|---|---|---|---|---|
| Prise de masse — débutant | 6 (MACROCYCLE, MESOCYCLE, 4 MICROCYCLE) | 1 | 12 | 16 | 52 |
| Remise en forme — débutant | 4 (4 MICROCYCLE, no wrapper) | 4 | 12 | 16 | 36 |

These totals match the seeder code (4 weeks x 13 exercises = 52; 4 weeks x 9 = 36).

Note on method: the first attempt used lax-mode `$.**`, which visits arrays twice and gave doubled counts (104 / 72). It was discarded; strict mode gives the figures above.

Referenced exercises (all 22 distinct references, 4 occurrences each, one per week): **every one exists and is active** in `EXERCISES`.

Prise de masse — débutant (13 exercises): Barre au front (EX124), Crunch, Curl biceps barre, Développé couché barre (EX001), Développé épaules barre, Dips, Gainage planche, Mollets debout, Presse à cuisses (EX140), Rowing barre buste penché (EX004), Soulevé de terre jambes tendues, Squat, Traction / tirage vertical.

Remise en forme — débutant (9 exercises): Burpees, Fentes avant, Gainage planche, Kettlebell swing, Mollets assis (EX164), Pompes (EX029), Rotation russe, Rowing haltère unilatéral (EX052), Squat.

Exercises with a code (EXnnn) are imported catalog rows; the ones without are historical.

Stop condition (zero active templates, or a missing / inactive reference): **not triggered**.

## 2.3 The 27 historical exercises not adopted in 9.3

Set: `EXERCISE_SOURCE_CODE IS NULL AND EXERCISE_COACH_ID IS NULL` (27 rows, matches 9.3 V4).

Totals: 2 templates in the table (both Mentora), 0 rows in `PROGRAM_EXERCISES`.

| exercise | template refs | PROGRAM_EXERCISES rows |
|---|---|---|
| Gainage planche | 8 | 0 |
| Squat | 8 | 0 |
| Burpees | 4 | 0 |
| Crunch | 4 | 0 |
| Curl biceps barre | 4 | 0 |
| Développé épaules barre | 4 | 0 |
| Dips | 4 | 0 |
| Fentes avant | 4 | 0 |
| Kettlebell swing | 4 | 0 |
| Mollets debout | 4 | 0 |
| Rotation russe | 4 | 0 |
| Soulevé de terre jambes tendues | 4 | 0 |
| Traction / tirage vertical | 4 | 0 |
| 14 others (Course à pied (tapis), Curl biceps haltères alterné, Curl marteau haltères, Curl poignet barre, Écarté couché haltères, Élévations frontales haltères, Extension poignet haltère, Extension triceps poulie haute, Fentes bulgares, Kickback à la poulie, Oiseau (élévations arrière), Suspension à la barre, Thruster haltères, Tirage horizontal poulie basse) | 0 each | 0 each |

13 of the 27 are referenced, for 60 template references in total (2x8 + 11x4). The other 14 are unreferenced. This is why the historical rows must be kept (not deleted or merged) and why rule 7.6 fills their group instead.
