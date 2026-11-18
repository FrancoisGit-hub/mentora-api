# Lot 13 bis, sub-lot 13bis.1: split the muscle group "Bras" into "Biceps" and "Triceps"

- Date: 2026-11-18
- Branch: `feature/lot_13bis` (base `189a001`, inventory committed as `f9103f7`)
- Reference: `docs/review/lot13bis/13bis.0/inventory.md`
- Environment of every API run: Development, `Email__Provider=Logging`, profile `http` (port 5243), local PostgreSQL `mentora-postgres-local`.
- Raw snapshots: `before.txt` and `after.txt` in this folder (same read-only SQL, `BEGIN READ ONLY ... ROLLBACK`). API logs stay local (ignored by git).

---

## 1. Decisions (validated by François on 18/11)

| Id | Decision |
|---|---|
| 13bis.A | The `MUSCLE_GROUPS` row "Bras" is renamed "Biceps" in place: same `MUSCLE_GROUP_ID`, legacy code `BICEPS`, sort order 4. A new row "Triceps" is created with legacy code `TRICEPS` and sort order 4 (lists ordered by sort order then name: Biceps, Triceps, then Avant-bras). No schema migration. |
| 13bis.B | New data patch `Lot13bis_SplitArms`, run once per database by `DataPatchRunner`, called in `Program.cs` right after `ReferenceNormalizationPatch`. Seeders and CSV files unchanged, except `MuscleGroupSeeder`: it no longer creates "Bras" once the `Lot13bis_SplitArms` marker exists. |
| 13bis.C | Primary group, fixed list: Mentora exercises EX009, EX118 to EX128, LG18, LG19 move to "Triceps" only if their primary group is "Bras"; their legacy `EXERCISE_MUSCLE_GROUP` is set to `TRICEPS`. Every other "Bras" exercise stays on the renamed row (Biceps). An exercise left on Biceps with legacy `TRICEPS` is reported, not changed. |
| 13bis.D | Secondary links, fixed lists, only on Mentora exercises linked to "Bras": 42 codes move their link to "Triceps"; 5 codes (EX013, EX227, EX233, EX237, EX238) keep Biceps and gain Triceps; every other link stays on Biceps. Never a link equal to the primary, never a duplicate, never more than 6 secondary groups: such a change is skipped and reported. |
| 13bis.E | Mirror `EXERCISE_SECONDARY_MUSCLES` rebuilt for every Mentora exercise linked to Biceps or Triceps, with the admin rule (group names ordered by sort order then name, ordinal; NULL when none; `AdminExerciseService.cs:806-809`). No mirror may contain "Bras" afterwards. |
| 13bis.F | Like the Lot12_2a patches: no `ADMIN_AUDIT_LOG` row, `EXERCISE_UPDATED_DATE` untouched, coach-owned exercises never touched (`EXERCISE_COACH_ID IS NULL` filter), `LegacyExerciseColumns` unchanged. |
| 13bis.G | No "Bras" row: nothing changed, the summary says so, the marker is still written. "Bras" together with a "Biceps" or "Triceps" row: the patch throws and the API refuses to start. Everything runs inside the `DataPatchRunner` transaction. |
| 13bis.H | Summary line `[PATCH] Lot13bis_SplitArms applied: renamed= created= primaryToTriceps= secondaryToTriceps= secondaryBoth= skipped= mirrorsRewritten= warnings=`, one line per changed exercise, one line per skip or warning. |

Choices accepted at the 3.3 review: listed codes that are not on "Bras" get an info line "not applicable" and are not counted in `skipped`; a mirror naming "Bras" without a link is rebuilt and counted as a warning; `MUSCLE_GROUP_UPDATED_DATE` is set on the renamed row; the "Biceps"/"Triceps" clash check ignores case; the new Triceps link sets both foreign keys explicitly (`ExerciseSecondaryMuscleGroupExerciseId`, as `AdminExerciseService.cs:456-459`).

## 2. Code

| File | Change |
|---|---|
| `Mentora.Infrastructure/Seeding/SplitArmsPatch.cs` | new: the patch (13bis.A, C to H), code lists as named constants |
| `Mentora.Infrastructure/Seeding/MuscleGroupSeeder.cs` | skips "Bras" when the `Lot13bis_SplitArms` marker exists (13bis.B) |
| `Mentora.API/Program.cs` | `SplitArmsPatch.RunAsync` right after `ReferenceNormalizationPatch` |

Build: `dotnet build --no-incremental` exit 0, 19 distinct warnings before the change and 19 after, 0 new (none in `Seeding` or `Program.cs`). No NuGet package, no migration, model unchanged.

## 3. Before and after (local database)

| Item | Before | After run 1 |
|---|---|---|
| `MUSCLE_GROUPS` rows | 14 | 15 |
| Row "Bras" | 1 (sort 4, BICEPS, `08d8cdd3-...`) | 0 |
| Row "Biceps" | 0 | 1 (sort 4, BICEPS, `08d8cdd3-...`, former "Bras" id) |
| Row "Triceps" | 0 | 1 (sort 4, TRICEPS, `a7c1c4aa-...`) |
| Mentora exercises, primary "Bras" | 32 (27 EX + 5 LG) | 0 |
| Mentora exercises, primary "Biceps" | 0 | 18 |
| Mentora exercises, primary "Triceps" | 0 | 14 |
| Secondary links to "Bras" | 71 | 0 |
| Secondary links to "Biceps" | 0 | 29 (24 unchanged + 5 both) |
| Secondary links to "Triceps" | 0 | 47 (42 moved + 5 both) |
| `EXERCISE_SECONDARY_MUSCLE_GROUPS` rows | 381 | 386 |
| Mentora mirrors containing "Bras" / "Biceps" / "Triceps" | 71 / 0 / 0 | 0 / 29 / 47 |
| `DATA_PATCHES` rows | 2 | 3 (+ `Lot13bis_SplitArms`) |
| `ADMIN_AUDIT_LOG` rows | 22 | 22 |

Fingerprints (count / md5 of the ordered rows):

| Fingerprint | Before | After run 1 | After run 2 |
|---|---|---|---|
| `MUSCLE_GROUPS` (id, name, sort, legacy) | 14 / `0ff23ffde5b365b3f2789c02a8211eb4` | 15 / `4495fbdec575c777539d4568c4b1f7d2` | 15 / `4495fbdec575c777539d4568c4b1f7d2` |
| `EXERCISE_SECONDARY_MUSCLE_GROUPS` | 381 / `f01fb0fd8bb7097190647e2796f4600d` | 386 / `7b514cdaa52cd099c0fb843eaf48fb72` | 386 / `7b514cdaa52cd099c0fb843eaf48fb72` |
| `EXERCISES` (id, primary id, legacy, mirror) | 275 / `7eac67415d840559da9357ed24e7086f` | 275 / `c41c3be6f6fb408bc57dbd06e7e06606` | 275 / `c41c3be6f6fb408bc57dbd06e7e06606` |
| Coach exercises (whole row) | 0 / `d41d8cd98f00b204e9800998ecf8427e` | identical | identical |
| Mentora `EXERCISE_UPDATED_DATE` | 275 / `f991c6f2dac46dc44a64380832b04d45` | identical | identical |

## 4. Run 1 (patch applied on the existing local database)

```
MuscleGroupSeeder: created 0 muscle group(s).
[PATCH] Lot12_2a_LegacyExerciseCodes skipped (already applied)
CatalogSeeder exercises: created 0, already present 248, skipped 2, ...
ExerciseSeeder: created 0, existing 27, superseded 18, skipped 0.
[PATCH] Lot12_2a_ReferenceNormalization skipped (already applied)
[PATCH] Lot13bis_SplitArms <code> primary Bras -> Triceps         (14 lines, see section 6)
[PATCH] Lot13bis_SplitArms <code> secondary Bras -> Triceps       (42 lines)
[PATCH] Lot13bis_SplitArms <code> both Bras -> Biceps+Triceps     (5 lines)
[PATCH] Lot13bis_SplitArms applied: renamed=1 created=1 primaryToTriceps=14 secondaryToTriceps=42 secondaryBoth=5 skipped=0 mirrorsRewritten=71 warnings=0
```

No `fail:`, `crit:` or `Exception` line. API stopped, port 5243 free (0 listener, 0 `Mentora.API` process).

## 5. Checks after run 1 (3.6)

| Check | Expected | Actual |
|---|---|---|
| Rows named "Bras" | 0 | 0 |
| "Biceps" has the former "Bras" id | yes | yes (BICEPS, sort 4) |
| "Triceps" exists with TRICEPS and sort 4 | yes | yes |
| Listed primary codes on Triceps with legacy TRICEPS | 14 | 14 |
| Unlisted exercises on Triceps | 0 | 0 |
| Exercises left on Biceps | 18 | 18 (EX008, EX104 to EX117, LG15, LG16, LG17) |
| Exercises on Biceps with legacy TRICEPS (warning) | 0 | 0 |
| Secondary "moved" codes: Triceps only | 42 | 42, 0 mismatch |
| Secondary "both" codes: Biceps and Triceps | 5 | 5, 0 mismatch |
| Secondary links unchanged (Biceps only) | 24 | 24, 0 mismatch |
| Skipped changes | 0 | 0 |
| Mirror equals the ordered names of the linked groups, all 275 Mentora exercises | 0 mismatch | 0 mismatch |
| Mentora mirrors containing "Bras" | 0 | 0 |
| Secondary link equal to the primary group | 0 | 0 |
| Exercises with more than 6 secondary groups | 0 | 0 (max 5: EX227, EX233) |
| Coach exercises fingerprint | identical | identical (see the limitation below) |
| `EXERCISE_UPDATED_DATE` of Mentora exercises | identical | identical |
| `ADMIN_AUDIT_LOG` rows | unchanged | 22 and 22 |

**Coach exercises, limitation.** The local database has 0 coach-owned exercises, so the coach fingerprint check is trivially identical and proves nothing by itself. Coach rows are protected by the `EXERCISE_COACH_ID IS NULL` filter of the patch (the only exercise query of `SplitArmsPatch`), as in the Lot12_2a patches. The PROD read of 18/11 showed no coach exercise on "Bras" (coach exercises carry only the legacy enum and no group id, inventory A.10).

## 6. Per-exercise list (for Federico)

Rename: the muscle group "Bras" is now called **"Biceps"** (same group, so every exercise and link not listed below simply shows "Biceps" instead of "Bras"). A new muscle group **"Triceps"** appears right after it.

### 6.1 Primary muscle group changed: Bras -> Triceps (14)

| Code | Exercise | Change | From | To |
|---|---|---|---|---|
| EX009 | Extension triceps à la poulie | primary | Bras | Triceps |
| EX118 | Dips triceps | primary | Bras | Triceps |
| EX119 | Dips assistés | primary | Bras | Triceps |
| EX120 | Extension triceps au-dessus de la tête haltère | primary | Bras | Triceps |
| EX121 | Extension triceps au-dessus de la tête corde | primary | Bras | Triceps |
| EX122 | Extension triceps poulie unilatérale | primary | Bras | Triceps |
| EX123 | Extension triceps poulie prise inversée | primary | Bras | Triceps |
| EX124 | Barre au front | primary | Bras | Triceps |
| EX125 | Extension triceps haltères couchée | primary | Bras | Triceps |
| EX126 | Kickback triceps haltère | primary | Bras | Triceps |
| EX127 | Kickback triceps poulie | primary | Bras | Triceps |
| EX128 | Développé couché prise serrée triceps | primary | Bras | Triceps |
| LG18 | Dips | primary | Bras | Triceps |
| LG19 | Extension triceps poulie haute | primary | Bras | Triceps |

### 6.2 Primary muscle group renamed only: Bras -> Biceps (18, no other change)

EX008 Curl haltères, EX104 Curl barre, EX105 Curl barre EZ, EX106 Curl incliné haltères, EX107 Curl marteau, EX108 Curl marteau alterné, EX109 Curl concentration, EX110 Curl pupitre barre EZ, EX111 Curl pupitre haltère, EX112 Curl poulie basse, EX113 Curl poulie unilatéral, EX114 Curl Bayesian, EX115 Curl spider, EX116 Curl inversé barre, EX117 Curl Zottman, LG15 Curl biceps barre, LG16 Curl biceps haltères alterné, LG17 Curl marteau haltères.

### 6.3 Secondary muscle group changed: Bras -> Triceps (42)

| Code | Exercise | Change | From | To |
|---|---|---|---|---|
| EX001 | Développé couché barre | secondary | Bras | Triceps |
| EX012 | Burpee | secondary | Bras | Triceps |
| EX016 | Développé couché haltères | secondary | Bras | Triceps |
| EX017 | Développé incliné barre | secondary | Bras | Triceps |
| EX018 | Développé incliné haltères | secondary | Bras | Triceps |
| EX019 | Développé décliné barre | secondary | Bras | Triceps |
| EX020 | Développé décliné haltères | secondary | Bras | Triceps |
| EX021 | Développé couché prise serrée | secondary | Bras | Triceps |
| EX022 | Développé haltères prise neutre | secondary | Bras | Triceps |
| EX023 | Développé alterné haltères | secondary | Bras | Triceps |
| EX024 | Développé unilatéral haltère | secondary | Bras | Triceps |
| EX025 | Chest press machine | secondary | Bras | Triceps |
| EX026 | Chest press unilatérale | secondary | Bras | Triceps |
| EX027 | Smith machine développé couché | secondary | Bras | Triceps |
| EX028 | Smith machine développé incliné | secondary | Bras | Triceps |
| EX029 | Pompes | secondary | Bras | Triceps |
| EX030 | Pompes sur genoux | secondary | Bras | Triceps |
| EX031 | Pompes inclinées | secondary | Bras | Triceps |
| EX032 | Pompes déclinées | secondary | Bras | Triceps |
| EX033 | Pompes prise serrée | secondary | Bras | Triceps |
| EX034 | Pompes larges | secondary | Bras | Triceps |
| EX035 | Pompes avec élastique | secondary | Bras | Triceps |
| EX036 | Pompes lestées | secondary | Bras | Triceps |
| EX037 | Pompes pliométriques | secondary | Bras | Triceps |
| EX065 | Pull-over poulie haute bras tendus | secondary | Bras | Triceps |
| EX066 | Pull-over haltère | secondary | Bras | Triceps |
| EX067 | Pull-over machine | secondary | Bras | Triceps |
| EX075 | Développé militaire barre debout | secondary | Bras | Triceps |
| EX076 | Développé militaire barre assis | secondary | Bras | Triceps |
| EX077 | Développé épaules haltères assis | secondary | Bras | Triceps |
| EX078 | Développé épaules haltères debout | secondary | Bras | Triceps |
| EX079 | Développé épaules unilatéral haltère | secondary | Bras | Triceps |
| EX080 | Arnold press | secondary | Bras | Triceps |
| EX081 | Shoulder press machine | secondary | Bras | Triceps |
| EX082 | Shoulder press machine unilatérale | secondary | Bras | Triceps |
| EX083 | Smith machine développé épaules | secondary | Bras | Triceps |
| EX084 | Landmine press unilatéral | secondary | Bras | Triceps |
| EX085 | Landmine press à deux mains | secondary | Bras | Triceps |
| EX213 | Planche haute | secondary | Bras | Triceps |
| EX220 | L-sit | secondary | Bras | Triceps |
| EX228 | SkiErg | secondary | Bras | Triceps |
| EX245 | Crab walk | secondary | Bras | Triceps |

### 6.4 Secondary muscle group split: Bras -> Biceps and Triceps (5)

| Code | Exercise | Change | From | To |
|---|---|---|---|---|
| EX013 | Rameur | both | Bras | Biceps + Triceps |
| EX227 | Air bike | both | Bras | Biceps + Triceps |
| EX233 | Elliptique | both | Bras | Biceps + Triceps |
| EX237 | Battle rope vagues alternées | both | Bras | Biceps + Triceps |
| EX238 | Battle rope vagues simultanées | both | Bras | Biceps + Triceps |

### 6.5 Secondary muscle group renamed only: Bras -> Biceps (24, no other change)

EX004 Rowing barre buste penché, EX005 Tractions pronation, EX045 Tirage vertical poulie haute, EX046 Tirage vertical prise neutre, EX047 Tirage vertical prise supination, EX048 Tirage vertical unilatéral, EX049 Tractions supination, EX050 Tractions prise neutre, EX051 Tractions assistées, EX052 Rowing haltère unilatéral, EX053 Rowing haltères buste penché, EX054 Rowing assis poulie basse, EX055 Rowing assis prise large, EX056 Rowing poulie basse unilatéral, EX057 Rowing poitrine appuyée haltères, EX058 Rowing T-bar, EX059 Rowing landmine unilatéral, EX060 Rowing machine assise, EX061 Rowing machine unilatéral, EX062 Rowing inversé, EX063 Rowing inversé aux anneaux, EX064 Tirage TRX, EX097 Rowing coudes ouverts haltères, EX240 Sled pull.

Skipped changes: none. Warnings: none.

## 7. Run 2 (idempotence)

```
MuscleGroupSeeder: created 0 muscle group(s).
[PATCH] Lot12_2a_LegacyExerciseCodes skipped (already applied)
CatalogSeeder primary groups: from muscles 221, from label 26, unresolved 1, mismatches 111, warnings 1.
CatalogSeeder exercises: created 0, already present 248, skipped 2, ...
ExerciseSeeder: created 0, existing 27, superseded 18, skipped 0.
[PATCH] Lot12_2a_ReferenceNormalization skipped (already applied)
[PATCH] Lot13bis_SplitArms skipped (already applied)
```

No "Bras" row recreated (15 groups, 0 named "Bras"), no `fail:`, `crit:` or `Exception` line. All fingerprints identical to after run 1 (section 3). API stopped, port 5243 free.

**EX116, accepted log noise (backlog).** On every startup of an already split database, `CatalogSeeder` logs `[CATALOG] exercise EX116 'Curl inversé barre': no primary muscle group (label 'Brachio-radial'), left NULL`. `CatalogSeeder` resolves the CSV before its create-only step: MUSCLES.csv puts MUS021 Brachio-radial in "Bras", which no longer exists, and the sheet label "Brachio-radial" is not a group name. It writes nothing (created 0, run 2 fingerprints identical); EX116 stays on Biceps in the database. On a new database the marker is absent when `MuscleGroupSeeder` runs, so "Bras" exists when `CatalogSeeder` resolves EX116 (through MUSCLES.csv), and the patch then leaves it on the renamed row: EX116 ends on Biceps there too (confirmed by the parity run, section 8). Decision: accepted, no code change, backlog item to silence or re-map this line.

## 8. Parity on a new database (3.8)

- `CREATE DATABASE mentora_parity_13bis` in `mentora-postgres-local`.
- One API start against it, database name overridden for that process only (`ConnectionStrings__DefaultConnection` built from the Development settings by replacing only the database name; `database replaced=True`; never printed, no file, removed from the shell right after the start).
- First start: 26 "Applying migration" lines, `MuscleGroupSeeder: created 14`, `CatalogSeeder exercises: created 248`, `ExerciseSeeder: created 27`, `Lot12_2a_ReferenceNormalization` applied (secondary links created=381), then:

```
[PATCH] Lot13bis_SplitArms applied: renamed=1 created=1 primaryToTriceps=14 secondaryToTriceps=42 secondaryBoth=5 skipped=0 mirrorsRewritten=71 warnings=0
```

  with the same 61 per-exercise lines as run 1. API stopped, port 5243 free.
- Parity database: 15 groups, 0 named "Bras", `DATA_PATCHES` = `Lot12_2a_LegacyExerciseCodes, Lot12_2a_ReferenceNormalization, Lot13bis_SplitArms`.
- Comparison per source code (EX and LG codes, Mentora exercises): primary group name and secondary group names (ordered by sort order then name).

```
local codes=275 parity codes=275
codes only in one side=0
differing lines=0
EX116 local:  EX116 | Biceps | Avant-bras
EX116 parity: EX116 | Biceps | Avant-bras
```

  No difference, so no local edit to explain. EX116 is on Biceps in both databases.
- `DROP DATABASE mentora_parity_13bis`; `mentora_parity_13bis_exists: 0` (databases left: mentora_db, postgres, template0, template1).

## 9. Not covered here

- PROD and api-demo: not touched. The patch will run at their next deployment; the fixed lists come from the PROD read of 18/11, and any listed code that is not on "Bras" there is logged as "not applicable".
- Mentora.Admin, API routes and DTOs, CSV files, `ProgramTemplateSeeder` texts: out of scope, unchanged. The admin reference cache needs a reload to show the new group (inventory C.3).
