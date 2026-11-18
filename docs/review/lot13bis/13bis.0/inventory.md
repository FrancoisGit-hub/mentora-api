# Lot 13 bis, sub-lot 13bis.0: read-only inventory

- Date: 2026-11-18
- Branch: `feature/lot_13bis` (created from `origin/main`, upstream unset)
- Base commit: `189a001` (docs(lot13): VPS deployment evidence, runbook steps 6, 9 and 10.3 corrected)
- Scope: (a) split the muscle group "Bras" into "Biceps" and "Triceps"; (b) duplicate an exercise from the admin.
- Method: code reading and PowerShell `Import-Csv` on the embedded CSV files. No database, no build, no run.
- Every `file:line` below refers to this branch at the base commit.

---

## A. Muscle groups, current model

### A.1 Entity, configuration, migrations

- Entity `MuscleGroupItem`: `MuscleGroupId`, `MuscleGroupName`, `MuscleGroupSortOrder`, `MuscleGroupLegacyCode`, `MuscleGroupCreatedDate`, `MuscleGroupUpdatedDate` (`Mentora.Core/Entities/MuscleGroupItem.cs:9-17`). No active flag, no description, no parent group.
- Configuration (`Mentora.Infrastructure/Persistence/Configurations/MuscleGroupItemConfiguration.cs`):
  - table `MUSCLE_GROUPS` (:11), PK `MUSCLE_GROUP_ID` default `gen_random_uuid()` (:13-16);
  - `MUSCLE_GROUP_NAME` required, 60 chars (:18-21);
  - `MUSCLE_GROUP_SORT_ORDER` required, **no unique index** (:23-25);
  - `MUSCLE_GROUP_LEGACY_CODE` required, 40 chars (:29-32);
  - unique index `UX_MUSCLE_GROUPS_NAME` on the raw name (:42-44): exact, case sensitive at the database level.
- Migration `20260929171450_Lot9_ExerciseCatalog.cs`:
  - creates `MUSCLE_GROUPS` (:178-190);
  - creates `UX_MUSCLE_GROUPS_NAME` (:274-278);
  - FK `FK_EXERCISES_MUSCLE_GROUPS_EXERCISE_PRIMARY_MUSCLE_GROUP_ID`, `ReferentialAction.Restrict` (:302-308);
  - raw SQL `CK_MUSCLE_GROUPS_LEGACY_CODE` with the 13 legacy values, `BICEPS` and `TRICEPS` included (:311-317).
- Migration `20261109113329_Lot12_2a_ExerciseReferences.cs` creates `EXERCISE_SECONDARY_MUSCLE_GROUPS` (:97-116) and its index `IX_EXERCISE_SECONDARY_MUSCLE_GROUPS_MUSCLE_GROUP` (:243-245).
- No other migration touches `MUSCLE_GROUPS` (grep on non-Designer migration files).

### A.2 MuscleGroupSeeder

- Default list of 14 groups, sort order = 1-based position in the list (`Mentora.Infrastructure/Seeding/MuscleGroupSeeder.cs:17-34`). "Bras" is entry 4 with legacy code `BICEPS` (:23); "Avant-bras" is entry 5 (:24).
- Legacy code of each group (:20-33): Pectoraux CHEST, Dos BACK, Épaules SHOULDERS, Bras BICEPS, Avant-bras FOREARMS, Quadriceps QUADRICEPS, Ischio-jambiers HAMSTRINGS, Fessiers GLUTES, Adducteurs GLUTES, Mollets CALVES, Tibial CALVES, Abdominaux ABS, Centre du corps ABS, Hanche GLUTES. No group has `TRICEPS`.
- Matching rule: get-or-create by **exact name** (`HashSet<string>` with the default ordinal comparer) (:41-52). Existing rows are never updated nor deleted (:10-11).
- Consequences at startup:
  - "Bras" renamed in the database while still in the list: the seeder **recreates a new "Bras"** row (name absent from the set) with sort order 4.
  - "Bras" deleted from the database while still in the list: same, recreated.
  - "Bras" removed from the list: the existing row is left untouched (never deleted).
  - New names added to the list: created with sort order = their position, even if that number is already used by another row (no uniqueness on sort order).

### A.3 References to MUSCLE_GROUPS

| Reference | Where | Delete behaviour |
|---|---|---|
| `EXERCISES.EXERCISE_PRIMARY_MUSCLE_GROUP_ID` | `ExerciseConfiguration.cs:94, 139, 146-149`; migration Lot9 :302-308 | Restrict |
| `EXERCISE_SECONDARY_MUSCLE_GROUPS.EXERCISE_SECONDARY_MUSCLE_GROUP_MUSCLE_GROUP_ID` | `ExerciseSecondaryMuscleGroupConfiguration.cs:30-34` | Restrict (exercise side: Cascade, :24-28) |

- Text copies of group names (no FK): the jsonb mirror `EXERCISES.EXERCISE_SECONDARY_MUSCLES` (`ExerciseConfiguration.cs:95`, `Exercise.cs:55-56`), see A.6.
- Program trees: `PROGRAM_EXERCISES` holds only an exercise pointer (`ProgramExercise.cs:11`); the tree response exposes the legacy enum only (`ProgramService.cs:1011`, `ProgramExerciseResponse.cs:19`). No group id or group name is stored.
- Program templates and session templates (jsonb bodies) reference `exerciseId` only (usage queries `AdminExerciseService.cs:369-381`). `ProgramTemplateSeeder.cs:149` and `:154` contain "Bras" as a **session name and a circuit name** (display text in the template body), not a group reference.
- Quiz: `QUESTIONS.csv` is free text; one line contains the word "Bras" (count only). No quiz code reads `MUSCLE_GROUPS` (grep on `MuscleGroup` in quiz code: none).
- CSV: `MUSCLES.csv` column `groupe_musculaire` (7 rows "Bras"), `REFERENCE_VALUE_MAP.csv` list `secondary_muscle` (5 rows coded "Bras"), see B.
- No view, no trigger, no other table references `MUSCLE_GROUPS` (grep on `MUSCLE_GROUP` in migrations).

### A.4 Legacy enum MuscleGroup and EnumMappings.MuscleGroupMapping

- Enum values include `Biceps` and `Triceps` (`Mentora.Core/Enums/MuscleGroup.cs:8-9`).
- Wire values (13): `CHEST, BACK, SHOULDERS, BICEPS, TRICEPS, FOREARMS, ABS, QUADRICEPS, HAMSTRINGS, GLUTES, CALVES, FULL_BODY, CARDIO` (`EnumMappings.cs:242-246`), `Parse` (:248-264), `ToWire` (:266-282). DB converter in `ExerciseConfiguration.cs:45-53, 196-230`.
- Exposed (written on the wire):
  - `ExerciseResponse.MuscleGroup` (deprecated) on coach and member exercise list and detail (`ExerciseResponse.cs:11, 44`; `ExerciseService.cs:417`);
  - `ProgramExerciseResponse.ExerciseMuscleGroup` in program trees (`ProgramExerciseResponse.cs:19`; `ProgramService.cs:1011`);
  - admin audit mirror field `legacyMuscleGroup` (`AdminExerciseService.cs:937`).
- Parsed (read from the wire):
  - coach list filter `muscleGroup=TRICEPS` (`CoachExercisesController.cs:21, 43`; `ExerciseService.cs:24, 40-41, 361-371`), filter applies to the legacy column `EXERCISE_MUSCLE_GROUP`;
  - coach create and update body `ExerciseRequest.MuscleGroup` (`ExerciseRequest.cs:9-13, 25`; `ExerciseRequestValidator.cs:31-34`; `ExerciseService.cs:95, 126`).

### A.5 LegacyExerciseColumns.MuscleGroupFor

- Rule (`Mentora.Infrastructure/Seeding/LegacyExerciseColumns.cs:16-29`): category label contains "cardio" -> CARDIO; else group named "Bras" (case insensitive) and principal muscle text contains "triceps" -> TRICEPS (:22-24); else the group legacy code (:25-26); else FULL_BODY.
- Callers (only two, grep `MuscleGroupFor(`):
  - `CatalogSeeder.cs:651-652`: `principalMuscle` = the `muscle` column of the MUSCLES.csv row of the principal muscle, set only when the group was derived from MUSCLES.csv (`CatalogSeeder.cs:388-394`); NULL when resolved from the sheet label (:403-406).
  - `AdminExerciseService.cs:839` (create and update, through `BuildState`): `principalMuscle` is always `null`. So an admin write on a "Bras" exercise always gives `BICEPS`, never `TRICEPS`.
- `ExerciseSeeder` does not call it: it writes the legacy enum of its default entry directly (`ExerciseSeeder.cs:308`).
- R2 rule (`AdminExerciseService.cs:829-839`): on update, the legacy muscle group keeps the stored value when `PrimaryMuscleGroupId` and `CategoryId` are both unchanged; otherwise it is recomputed with `principalMuscle = null`. On create (`current` null) it is always computed. Consequence today: a triceps exercise under "Bras" whose category or primary group is changed in the admin flips from TRICEPS to BICEPS.

### A.6 Secondary muscles: links and jsonb mirror

- Links: `EXERCISE_SECONDARY_MUSCLE_GROUPS`, composite PK (`ExerciseSecondaryMuscleGroupConfiguration.cs:14`).
- Mirror `EXERCISE_SECONDARY_MUSCLES` (jsonb list of strings): group **names** of the linked groups, ordered by `MUSCLE_GROUP_SORT_ORDER` then name, NULL when none (`Exercise.cs:55`).
- Writers:
  - `CatalogSeeder.cs:543`: raw CSV split of `muscles_secondaires` (original text, not group names), for new exercises only;
  - `ReferenceNormalizationPatch.cs:105-136`: maps each element through `REFERENCE_VALUE_MAP.csv`, creates the links (distinct groups minus the primary group), rewrites the mirror as group names (:129-134). Runs only on exercises with a non-null mirror and **no link yet** (:108);
  - `AdminExerciseService.cs:806-809, 824, 882`: on create and update, mirror = names of the requested secondary groups.
- Readers: apps receive the mirror as `secondaryMuscles` (`ExerciseService.cs:434`; `ExerciseResponse.cs:26, 59`). The admin reads the links, not the mirror (`AdminExerciseService.cs:333-337`). Admin update compares the mirror in its state (`AdminExerciseService.cs:853`, `:935`): a drifted mirror is corrected and audited under `secondaryMuscles` (:515-517).
- Coach exercises: never touched by the patch (`ReferenceNormalizationPatch.cs:70`).

### A.7 CatalogSeeder primary group resolution

- Groups loaded by name, case insensitive (`CatalogSeeder.cs:356-358`).
- Step 1 (`:366-373`, `:388-394`): the `Principal` row of `EXERCICES_MUSCLES.csv` with the lowest `importance` (file order breaks ties) gives a `muscle_id`; its `MUSCLES.csv` row gives `groupe_musculaire`; that name must exist in `MUSCLE_GROUPS`.
- Step 2 (`:403-406`): otherwise the sheet label `groupe_musculaire_principal` must exist in `MUSCLE_GROUPS`.
- Step 3 (`:408-413`): otherwise the group is left NULL with a `[CATALOG]` warning "no primary muscle group ... left NULL".
- When step 1 succeeds and the sheet label differs, a mismatch line is logged (:399-400). Today all 27 "Bras" exercises log a mismatch (their label is "Biceps", "Triceps" or "Brachio-radial", see B.1).
- On a **new database without a "Bras" group**: step 1 fails for the 27 rows (MUSCLES.csv says "Bras"), step 2 is used. If groups named exactly "Biceps" and "Triceps" (case insensitive) exist, 26 rows resolve by label; EX116 (label "Brachio-radial") is left NULL with a warning unless a group of that name exists. The legacy column of those rows is then the group legacy code (`principalMuscle` NULL on the label path).
- The seeder is create-only: existing exercises are never rewritten (`CatalogSeeder.cs:18-23, 488-489`). It cannot fix an existing database.

### A.8 ReferenceNormalizationPatch and REFERENCE_VALUE_MAP.csv

- Source values coded "Bras" (list `secondary_muscle`, file line numbers counting the header as line 1): `Biceps` (:178), `Triceps brachial` (:180), `Triceps` (:183), `bras` (:197), `Brachial` (:204). Matching is NFC + trim, case insensitive (`ReferenceNormalizationPatch.cs:114`, `SeedingText.cs:26`), so `biceps` and `triceps` also map.
- No other list has the code "Bras" (other lists use reference codes such as `APPUI_AVANT_BRAS`).
- `brachio-radial` maps to "Avant-bras" (line 201) while `MUSCLES.csv` puts `Brachio-radial` (MUS021) in "Bras". The two files disagree for that muscle.
- `ReadMap` throws at startup if a `secondary_muscle` code is not a `MUSCLE_GROUPS` name (`ReferenceNormalizationPatch.cs:201-202`). It runs only when the patch has not been applied yet (`DataPatchRunner.cs:19-23`), so: **on a new database without "Bras", the API refuses to start** while the CSV still maps to "Bras"; on an existing database the patch is skipped and the CSV is not read.
- Original text: the patch overwrites the mirror with group names (:129-134) and the six list texts with the reference label (:91-92). The original values survive only in the startup log lines `[PATCH] ... unmapped ...` (:140-142) for unmapped values; mapped originals are not kept in the database. The original CSV text is still in the embedded `EXERCICES.csv`.

### A.9 DataPatchRunner, LegacyExerciseCodePatch, startup order

- `DataPatchRunner.RunOnceAsync` (`DataPatchRunner.cs:17-32`): skips with a log line when a `DATA_PATCHES` row of that name exists (:19-23); else runs the patch and inserts the marker in one transaction (:25-29); logs `[PATCH] <name> applied: <summary>` (:31). Table `DATA_PATCHES`, PK on the name (migration Lot12_2a :51-59).
- Patches are plain static classes with a `Name` constant and `RunAsync` (`LegacyExerciseCodePatch.cs:14-52`, `ReferenceNormalizationPatch.cs:20-60`). Order = call order in `Program.cs`. No audit row, no `EXERCISE_UPDATED_DATE` change (`LegacyExerciseCodePatch.cs:12`, `ReferenceNormalizationPatch.cs:17-18`).
- Startup order (`Mentora.API/Program.cs:560-577`):
  1. `MigrateAsync` (:563)
  2. `CoachParameterSeeder` (:565), `OfferProgramSeeder` (:566)
  3. `MuscleGroupSeeder` (:568)
  4. `ExerciseReferenceSeeder` (:569)
  5. `LegacyExerciseCodePatch` (:570)
  6. `CatalogSeeder` (:571)
  7. `ExerciseSeeder` (:572)
  8. `ReferenceNormalizationPatch` (:573)
  9. `ProgramTemplateSeeder` (:574), `QuizQuestionSeeder` (:575), `AdminUserSeeder` (:576)
- Same end state on new and existing databases: on a new database, steps 3 to 8 create the "Bras" data (primary FK by steps 6 and 7, secondary links by step 8). A new split patch placed **after step 8** (`Program.cs:573`) sees the same shape on both kinds of database. A patch placed before step 6 would run on an empty catalogue on a new database (no-op) and the seeders would then recreate "Bras" data. Alternatively the seeders and the CSV map can be changed so that a new database never has "Bras", and the patch only has work on existing databases; then the patch must still be harmless on a new database.

### A.10 Coach exercises

- Coach create and update write only the legacy enum and the legacy equipment (`ExerciseService.cs:87-101, 121-129`). They never set `EXERCISE_PRIMARY_MUSCLE_GROUP_ID` nor secondary links.
- `ExerciseRequest` (coach) has `MuscleGroup` as a wire string only (`ExerciseRequest.cs:19-28`); validator accepts the 13 wire values (`ExerciseRequestValidator.cs:31-34`).
- Coach routes return both the legacy `muscleGroup` and `primaryMuscleGroup` (`ExerciseService.cs:417, 431-433`); for coach-owned rows `primaryMuscleGroup` is null.
- Coach list accepts the legacy `muscleGroup` filter and the new `muscleGroupId` filter (`CoachExercisesController.cs:43-45`).
- Coach-owned exercises are excluded from the patches (`ReferenceNormalizationPatch.cs:70`, `LegacyExerciseCodePatch.cs:32`) and from the admin (`AdminExerciseService.cs:121, 295`).

### A.11 Admin audit and data patches

- `ADMIN_AUDIT_LOG_ADMIN_USER_ID` is required with FK Restrict to the admin user (`AdminAuditLogConfiguration.cs:24, 52-56`).
- `CK_ADMIN_AUDIT_LOG_ACTION` allows `EXERCISE_CREATED, EXERCISE_UPDATED, EXERCISE_ACTIVATED, EXERCISE_DEACTIVATED, EXERCISE_VIDEO_UPLOAD_CREATED, EXERCISE_VIDEO_REMOVED` (migration `20261110104601_Lot12_3a_MuxVideos.cs:105-110`; first version `20261109160428_Lot12_2b_AdminAuditLog.cs:55-56`). Enum and mappings: `AdminAuditAction.cs:4-43`.
- Precedent: the Lot12_2a patches were not audited. They write only the `DATA_PATCHES` marker (`DataPatchRunner.cs:25-29`), and the audit table was created after them (Lot12_2b migration is later than Lot12_2a).
- A data patch has no admin user id. Auditing it would need an admin row to attribute it to, or a schema change (nullable admin id or a new action value plus a CHECK migration). Not auditing needs nothing.

---

## B. Data size from the CSV (no database access)

> **These counts describe the seeded catalogue only (embedded CSV files and ExerciseSeeder), not PROD.** Federico has edited the PROD catalogue since; the real numbers must be read from PROD before any data correction.

Inputs: `EXERCICES.csv` 251 rows, 250 active, 1 duplicated active id (`EX135`, 2 rows, both skipped by the seeder), so 248 eligible rows. All 248 resolve to a group (0 unresolved). Primary group distribution: Abdominaux 41, Quadriceps 37, Dos 32, Épaules 30, Pectoraux 30, **Bras 27**, Fessiers 24, Ischio-jambiers 10, Avant-bras 7, Mollets 6, Adducteurs 3, Tibial 1.

`MUSCLES.csv` rows in "Bras" (7): MUS018 and MUS019 Biceps brachial, MUS020 Brachial antérieur, MUS021 Brachio-radial, MUS022 to MUS024 Triceps brachial.

### B.1 Primary group "Bras" (CatalogSeeder rule): 27 rows, all resolved from MUSCLES.csv

| Principal muscle | Count | Source codes | Sheet label |
|---|---|---|---|
| Biceps brachial | 12 | EX008, EX104, EX105, EX106, EX109, EX110, EX111, EX112, EX113, EX114, EX115, EX117 | Biceps |
| Triceps brachial | 12 | EX009, EX118, EX119, EX120, EX121, EX122, EX123, EX124, EX125, EX126, EX127, EX128 | Triceps |
| Brachial antérieur | 2 | EX107 (Curl marteau), EX108 (Curl marteau alterné) | Biceps |
| Brachio-radial | 1 | EX116 (Curl inversé barre) | Brachio-radial |
| Other | 0 | | |

Note: the sheet label (`groupe_musculaire_principal`) already says "Biceps" or "Triceps" for 26 of the 27 rows. With the current legacy rule, the 12 triceps rows get `TRICEPS` and the 15 others get `BICEPS`.

### B.2 Secondary values mapped to "Bras": 74 elements on 74 exercises (no exercise has two)

| Original text | Count | Source codes [primary group] |
|---|---|---|
| `Biceps` | 25 | EX004, EX005, EX045 to EX064 (20 codes) [Dos]; EX097 [Épaules]; EX116 [Bras]; EX240 [Abdominaux] |
| `Triceps` | 20 | EX001 [Pectoraux]; EX012, EX228 [Quadriceps]; EX065, EX066, EX067 [Dos]; EX075 to EX085 [Épaules]; EX213, EX220, EX245 [Abdominaux] |
| `Triceps brachial` | 22 | EX016 to EX037 [Pectoraux] |
| `Brachial` | 2 | EX107, EX108 [Bras] |
| `bras` | 5 | EX013, EX227 [Quadriceps]; EX233, EX237, EX238 [Abdominaux] |
| other | 0 | |

Case-sensitive detail: `Biceps` 25 counts both `Biceps` and `biceps` (EX055, EX057, EX058, EX097, EX240 are lower case); `Triceps` 20 counts both `Triceps` and `triceps` (EX012, EX065, EX066, EX067, EX085, EX213, EX220, EX228 are lower case). The map is case insensitive (A.8).

- 3 of the 74 have "Bras" as primary group (EX107, EX108, EX116): the patch removed "Bras" from their secondary links (`ReferenceNormalizationPatch.cs:122-123`), so on a seeded database **71 exercises** carry a secondary link to "Bras".
- The 5 `bras` rows (Rameur, Air bike, Elliptique, two Battle rope) do not say whether biceps or triceps is meant.

### B.3 Legacy exercises LG01 to LG45 (ExerciseSeeder)

| Code | Name | Legacy enum | Primary group |
|---|---|---|---|
| LG15 | Curl biceps barre | BICEPS (`ExerciseSeeder.cs:94-97`) | Bras |
| LG16 | Curl biceps haltères alterné | BICEPS (:98-101) | Bras |
| LG17 | Curl marteau haltères | BICEPS (:102-105) | Bras |
| LG18 | Dips | TRICEPS (:108-111) | Bras |
| LG19 | Extension triceps poulie haute | TRICEPS (:112-115) | Bras |
| LG20 | Barre au front | TRICEPS (:116-119) | superseded by EX124 (`ExerciseSeeder.cs:246`), never created while EX124 exists |

- New database: `ExerciseSeeder.cs:300, 311` maps BICEPS and TRICEPS to "Bras" through `LegacyMuscleGroupMapping.GroupName` (`SeedingText.cs:49-50`). If no "Bras" exists, the primary group is silently left NULL (no warning, `ExerciseSeeder.cs:311`).
- Existing databases: these rows predate 12.2a. Their primary group was filled from the legacy code at lot 9 (commit `c88c6ad`, "fill primary group of historical exercises from legacy code"); 12.2a only gave them their LG codes (`LegacyExerciseCodePatch.cs:23-51`). LG15 to LG19 have no category (12.2a normalization report, `docs/review/lot12/12.2a/normalization-report.md:184-188`).
- Historical descriptions mention "triceps" or "biceps" as text (`ExerciseSeeder.cs:45, 103`): text only.

### B.4 Scope warning

The tables above count the **seeded catalogue**. PROD can differ: exercises created, renamed, re-grouped or deactivated by Federico in the admin, secondary links edited, and coach-owned exercises (legacy enum only). The split must start from a PROD read.

---

## C. Contract and screens affected by the split

### C.1 App routes

| Route | Muscle group fields | Id or name | Order |
|---|---|---|---|
| `GET /api/v1/member/exercise-filters` (`MemberExerciseFiltersController.cs:9`) and `GET /api/v1/coach/exercise-filters` (`CoachExerciseFiltersController.cs:9`) | `muscleGroups[]`: `muscleGroupId`, `name`, `sortOrder` (`ExerciseFiltersResponse.cs:21`) | both | sort order then name (`ExerciseService.cs:332-336`); only groups used as a **primary** group by a visible active exercise (:333) |
| `GET /api/v1/member/exercises` (`MemberExercisesController.cs:9, 37`) | filter `muscleGroupId` (primary only, `ExerciseService.cs:305-306`); list items carry no group (`ExerciseService.cs:229-236`) | id | n/a |
| `GET /api/v1/member/exercises/{id}`, `GET /api/v1/coach/exercises`, `GET /api/v1/coach/exercises/{id}` | `primaryMuscleGroup {muscleGroupId, name}`, `secondaryMuscles[]` (names from the mirror), deprecated `muscleGroup` (wire value) (`ExerciseService.cs:417, 431-434`) | id + name; secondaries by name only | secondaries in mirror order (sort order) |
| Program tree (`MemberProgramsController.cs:17, 34`; `CoachProgramsController.cs:42`) | `exerciseMuscleGroup` (legacy wire value) (`ProgramService.cs:1011`) | legacy code only | n/a |

Effect of the split: groups appear in the filters automatically once used as a primary group and "Bras" disappears when no visible exercise uses it (`ExerciseService.cs:333`). Apps that hard-code "Bras" or a group id are outside this repo (not checked).

### C.2 Admin API

- `GET /api/v1/admin/exercise-references` (`AdminExercisesController.cs:27`): `muscleGroups[]` `{id, name, sortOrder}`, all groups, sort order then name (`AdminExerciseService.cs:92-95`).
- `GET /api/v1/admin/exercises`: filter `muscleGroupId` on the primary group only (`AdminExercisesController.cs:60`; `AdminExerciseService.cs:127-128`); list item `primaryMuscleGroup` = name (`AdminExerciseService.cs:171, 215`; `AdminExerciseDtos.cs:94`).
- `GET /api/v1/admin/exercises/{id}`: `primaryMuscleGroup {id, name}` and `secondaryMuscleGroups[] {id, name}` ordered by sort order then name (`AdminExerciseService.cs:332-337`; `AdminExerciseDtos.cs:134-135`).
- Write body: `primaryMuscleGroupId` optional (`AdminExerciseValidators.cs:43`), `secondaryMuscleGroupIds` 0 to 6, distinct, never the primary (`AdminExerciseValidators.cs:52-60`). Any existing group id is accepted (no active flag, `AdminExerciseService.cs:706-730`).
- There is **no admin route to create, rename or delete a muscle group**.

### C.3 Mentora.Admin

- List filter "Groupe musculaire" built from the references (`Mentora.Admin/Pages/Exercices/List.razor:35-47`), query parameter `groupe` (`List.razor:231, 282`), column "Groupe musculaire" showing the name (`List.razor:133, 150`).
- Form: primary group select (`Form.razor:139-140`), secondary chips excluding the primary (`Form.razor:264-269, 489-495`), at most 6 (`Form.razor:744-748`).
- References are cached once per session (`Mentora.Admin/Services/ReferencesCache.cs:6, 15-22`): new groups appear only after a reload of the admin app.
- No hard-coded group name in Mentora.Admin (grep "Bras" case insensitive: no hit). No badge per group.

### C.4 Swagger

- `ExerciseListOperationFilter` marks the coach `muscleGroup` and `equipment` filters as deprecated (`Mentora.API/Swagger/ExerciseListOperationFilter.cs:35-40`); it contains no BICEPS, TRICEPS or "Bras".
- `ExerciseRequest` XML doc lists `BICEPS` and `TRICEPS` among the accepted values (`ExerciseRequest.cs:9-13`).
- Coach list XML doc example is `CHEST` (`CoachExercisesController.cs:21`).
- No Swagger text mentions "Bras".

### C.5 Hard-coded "Bras" in the solution (excluding CSV, docs, migrations)

| File:line | Use |
|---|---|
| `Mentora.Infrastructure/Seeding/MuscleGroupSeeder.cs:23` | default group "Bras", legacy BICEPS |
| `Mentora.Infrastructure/Seeding/LegacyExerciseColumns.cs:13, 22` | triceps rule keyed on the name "Bras" |
| `Mentora.Infrastructure/Seeding/SeedingText.cs:49, 50` | legacy BICEPS and TRICEPS -> "Bras" (used by ExerciseSeeder) |
| `Mentora.Infrastructure/Seeding/ProgramTemplateSeeder.cs:149, 154` | session name "Séance C ... Bras et abdos" and circuit name "Bras" (display text) |

Case-insensitive grep also hits "bras" and "avant-bras" inside exercise descriptions and instructions (`ExerciseSeeder.cs:34-233`, text only) and "Avant-bras" (`MuscleGroupSeeder.cs:24`, `SeedingText.cs:51`). CSV hits (excluded from the table): `MUSCLES.csv` 7 rows, `REFERENCE_VALUE_MAP.csv` lines 178, 180, 183, 197, 204, `QUESTIONS.csv` 1 line (quiz text).

---

## D. Exercise duplication in the admin

### D.1 Form.razor

- Routes: `@page "/exercices/nouveau"` and `@page "/exercices/{ExerciseId:guid}"` (`Form.razor:1-2`).
- Mode detection: `new Uri(Nav.Uri).AbsolutePath` ends with `/nouveau` (`Form.razor:501-503`). The query string is not part of `AbsolutePath`, so `/exercices/nouveau?depuis=...` is still the create mode.
- Reload guard: `OnParametersSetAsync` returns early when the mode and the id are unchanged (`Form.razor:508-509`). Going from `/exercices/nouveau` to `/exercices/nouveau?depuis=X` (or between two `depuis` values) would **not** reload with the current key.
- Load: references from `ReferencesCache` and detail from `Api.GetAsync` in parallel (`Form.razor:520-566`); create mode calls `InitCreate` (empty model, "Sans matériel" preselected, `Form.razor:569-578`); edit mode calls `Fill` (`Form.razor:582-618`).
- Mapping `ExerciseDetail` -> form (`Form.razor:585-610`): name, English name, description, instructions, the six reference ids, primary group id, primary and secondary equipment ids, level, breathing, mistakes (joined by new lines), precautions, coach tip, search tags, secondary group ids, objective ids. `Fill` also sets `_detail`, `_held` (ids of the six lists, so an inactive held value stays in the drop-down, `Form.razor:612-614, 718-722`) and `_incomplete` (`Form.razor:617`).
- Save: local required checks (`Form.razor:859-861`); in create mode every required field blocks (name, category, level, primary group, primary equipment, short instruction, `Form.razor:620-628`); `BuildRequest` (`Form.razor:688-714`); `_currentIsCreate ? Api.CreateAsync : Api.UpdateAsync` (`Form.razor:879-881`). After a create: flash with the new code, then `Nav.NavigateTo("exercices/" + id, replace: true)` (`Form.razor:892-898`).
- Dirty check: `IsDirty` = `Snapshot() != _baseline` (`Form.razor:483-484`); `Snapshot` concatenates all form fields (`Form.razor:667-675`); `_baseline` is taken right after load (`Form.razor:566`) and after a save (`Form.razor:891`). `NavigationLock` (`Form.razor:13`) calls `OnBeforeNavigation` (`Form.razor:796-815`), which opens the "Quitter sans enregistrer ?" dialog (`Form.razor:362-365`); `ConfirmLeave` and `CancelLeave` (`Form.razor:832-844`). A form prefilled from a source would have its baseline equal to the copy, so leaving it unchanged would not prompt.
- Video block is hidden in create mode ("Enregistrez d'abord l'exercice", `Form.razor:161-164`).

### D.2 Field availability for a copy

Admin client request `ExerciseRequest` (`Mentora.Admin/Api/ExerciseModels.cs:110-131`) mirrors the API `AdminExerciseRequest` (`AdminExerciseDtos.cs:33-54`). Every request field is present in `ExerciseDetail` (`ExerciseModels.cs:77-107`):

| Request field | From the detail |
|---|---|
| name, nameEn, description, instructions, level, breathing, precautions, coachTip, searchTags | same name |
| categoryId, typeId, lateralityId, movementPatternId, startingPositionId, gripTypeId | `.Id` of the `CodedRef` (null when the list holds only a legacy text) |
| primaryMuscleGroupId, primaryEquipmentId, secondaryEquipmentId | `.Id` of the `NamedRef` |
| secondaryMuscleGroupIds, objectiveIds | ids of the lists |
| commonMistakes | list |

No request field is missing from the detail. Fields that must not be copied: `exerciseId`, `sourceCode` (allocated on create), `video` (a MUX asset owned by one exercise, `AdminExerciseVideosController.cs:29`), `isActive` (not in the request, a create is always active), `usage`, `createdDate`, `updatedDate`, `legacyValues` (not in the request), `isPolyarticular` (derived from the type, `AdminExerciseService.cs:832-834`). Entity columns outside the admin contract and not copyable through the form: `EXERCISE_VIDEO_URL`, `EXERCISE_IMAGE_URL` (excluded from the request, `AdminExerciseDtos.cs:9-10`) and the legacy `EXERCISE_MUSCLE_GROUP` / `EXERCISE_EQUIPMENT` (recomputed, `AdminExerciseService.cs:835-842`).

Value that may be refused when copied: a reference row that is now **inactive**. On create there is no held value (`AdminExerciseService.cs:686-687`, `heldId` null) so the API answers 422; the form also hides inactive rows in create mode because `_held` is empty (`Form.razor:572, 718-722`).

### D.3 POST /api/v1/admin/exercises

- Name uniqueness: `EnsureNameFreeAsync` compares `SeedingText.FoldedKey` (accents, case and spaces ignored) against every Mentora exercise, inactive included (`AdminExerciseService.cs:636-653`; `SeedingText.cs:19-20`), and answers 409 `EXERCISE_NAME_TAKEN` with `{exerciseId, sourceCode, name}` of the existing one. Backstop index `UX_EXERCISES_MENTORA_NAME_CI` on `lower(btrim(name))` for Mentora rows (migration Lot12_2a :365-367), caught at `AdminExerciseService.cs:478-483, 655-658`. A copy with the same name is always refused; the form shows a link to the existing exercise (`Form.razor:117-122, 920-927`).
- Code: next `EX` number under advisory lock 7122002 (`AdminExerciseService.cs:32, 432, 436-447`).
- Required: `categoryId` and `primaryEquipmentId` (`AdminExerciseValidators.cs:30-36`).
- New exercise is active (`AdminExerciseService.cs:448`).
- Audit: one `EXERCISE_CREATED` row in the same transaction, changes = `sourceCode`, `isActive` and all request fields (`AdminExerciseService.cs:468-473`). Nothing records the source exercise of a copy.

### D.4 Exercises with legacy values

- `hasLegacyValues` = at least one of the six lists has text with a NULL FK (`AdminExerciseService.cs:47-53, 177-182`); list badge "À reprendre" (`List.razor:180-183`); the form shows "Valeur d'origine : ..." under the select (`Form.razor:785-788`) and "À compléter" badges for required fields empty at opening (`Form.razor:652-653`).
- On create, `Mirror` with no current exercise gives `(null, null)` for a list without id (`AdminExerciseService.cs:790-795`): the legacy text of the source is **not carried** into the copy. Copy and source would then differ.
- The 27 historical LG exercises without category (`docs/review/lot12/12.2a/normalization-report.md:172-204`) cannot be copied without choosing a category: `categoryId` is required by the API and every required field blocks in create mode (`Form.razor:859-861`). Same for an empty level, primary group or short instruction.

### D.5 Query parameters in Mentora.Admin

- Pattern in `List.razor`: `[SupplyParameterFromQuery(Name = "...")] public string? X` read as text, parsed with `ParseGuid` (malformed value ignored, never an exception) (`List.razor:225-243`), applied in `OnParametersSetAsync` with a key of all parameters (`List.razor:277-310`); URL built with `Nav.GetUriWithQueryParameters` (`List.razor:390-403`).
- `Form.razor` has no query parameter today. Reusing the pattern for `/exercices/nouveau?depuis={id}` needs the `depuis` value in the reload key (see D.1 guard).

### D.6 Where a "Dupliquer" button would sit

- Detail page header `pagehead`: "Retour aux exercices" and "Enregistrer l'exercice" (`Form.razor:37-40`).
- Edit bar (detail only): usage line, state badge, "Désactiver" / "Activer" (`Form.razor:70-89`).
- List row actions: "Modifier", "Désactiver" / "Activer" (`List.razor:185-197`).
- No "Dupliquer" exists anywhere in the admin or the API for exercises (grep "dupli", "copie").
- POC: the repo mentions "the POC" (`Mentora.Admin/wwwroot/css/app.css:1`; `docs/review/lot12/12.3.0/inventory.md:329`; `docs/review/lot12/12.4b/runtime-evidence.md:43`) but holds no POC file or link. Not opened.

### D.7 Alternative: server-side duplicate route (trade-off only, no design)

What it would require:
- Controller action on `AdminExercisesController` (`Mentora.API/Controllers/Admin/AdminExercisesController.cs`), for example a POST on `exercises/{id}/...`, with Swagger doc.
- Service method in `IAdminExerciseService` / `AdminExerciseService` reusing the create path (lock, EX code, name check, links).
- A name rule, since a copy cannot keep the name (D.3). Precedent: session templates use `" (copie)"` truncated to the column length (`SessionTemplateService.cs:31, 183, 238`; `CoachSessionTemplatesController.cs:128-141`).
- Audit: either reuse `EXERCISE_CREATED` (no migration, source recorded only in the changes jsonb), or a new action value: enum and mappings (`AdminAuditAction.cs:4-43`) plus a migration that drops and re-adds `CK_ADMIN_AUDIT_LOG_ACTION` (precedent `20261110104601_Lot12_3a_MuxVideos.cs:105-110`, down :135-140).
- Rules to decide server side: legacy texts, inactive references, video (never copied), polyarticular (derived).
- Admin client: a method in `ExercisesApi` (`Mentora.Admin/Api/ExercisesApi.cs`) and a button.

Client-side prefill (D.5) needs no API change, no migration, and shows the copy before it exists; server side gives an atomic copy in one call but needs a name rule and API work.

---

## Facts that constrain the design

1. `MUSCLE_GROUPS` has no active flag; "Bras" cannot be hidden, only deleted, and deletion is blocked by two Restrict FKs while any exercise uses it (`MuscleGroupItem.cs:9-17`, `ExerciseConfiguration.cs:146-149`, `ExerciseSecondaryMuscleGroupConfiguration.cs:30-34`).
2. `MuscleGroupSeeder` is get-or-create by exact name: keeping "Bras" in its list recreates it after any rename or delete; removing it from the list never deletes the row (`MuscleGroupSeeder.cs:41-52`).
3. Sort order is not unique and is the list position; inserting two groups shifts nothing in an existing database (`MuscleGroupItemConfiguration.cs:23-25`, `MuscleGroupSeeder.cs:57`).
4. `CK_MUSCLE_GROUPS_LEGACY_CODE` already allows `BICEPS` and `TRICEPS`: new groups need no CHECK migration (Lot9 migration :311-317).
5. There is no admin route to manage muscle groups; the split can only be done by code (seeder plus data patch) or SQL (`AdminExercisesController.cs:27-143`).
6. On a new database, `REFERENCE_VALUE_MAP.csv` must not name a group that does not exist, or `ReferenceNormalizationPatch` throws at startup (`ReferenceNormalizationPatch.cs:201-202`).
7. Without a "Bras" group, `ExerciseSeeder` silently leaves LG15 to LG19 with no primary group (`SeedingText.cs:49-50`, `ExerciseSeeder.cs:311`), and `CatalogSeeder` falls back to the sheet label, which already says Biceps or Triceps for 26 of 27 rows; EX116 "Brachio-radial" would be NULL (`CatalogSeeder.cs:403-413`).
8. A split patch must run after `ReferenceNormalizationPatch` (`Program.cs:573`) to see the same data on new and existing databases, through `DataPatchRunner` (once per database, marker in `DATA_PATCHES`).
9. The patch must update the primary FK, the secondary links (no duplicate, never the primary) and the jsonb mirror together, or the admin update will report a drifted mirror (`AdminExerciseService.cs:515-517`, `:853`).
10. The legacy enum is still on the wire (coach `muscleGroup`, program trees `exerciseMuscleGroup`) and the triceps rule is keyed on the name "Bras" (`LegacyExerciseColumns.cs:22-24`); the admin path never passes a principal muscle (`AdminExerciseService.cs:839`).
11. Patches have no admin user id; the Lot12_2a precedent wrote no audit row and did not touch `EXERCISE_UPDATED_DATE` (`DataPatchRunner.cs:25-29`, `ReferenceNormalizationPatch.cs:17-18`).
12. The PROD state is unknown from the repo: the CSV counts (27 primary, 71 secondary links, 5 LG) describe the seeded catalogue only.
13. Duplication: same name is always refused (folded key) (`AdminExerciseService.cs:636-653`); the copy needs a new name chosen by the user or by a rule.
14. Duplication through the create form drops legacy texts and inactive references, and requires category, level, primary group, primary equipment and short instruction (`AdminExerciseService.cs:686-687, 790-795`; `Form.razor:620-628, 859-861`).
15. The form reload guard ignores the query string; a `depuis` parameter must be part of the reload key (`Form.razor:501-509`).
16. Admin references are cached per session; new groups need an admin reload (`ReferencesCache.cs:15-22`).

## Open questions for François

1. "Brachial antérieur" (curl marteau EX107, EX108) and "Brachio-radial" (curl inversé EX116): Biceps, Triceps, or Avant-bras?
2. The 5 exercises with secondary text "bras" (Rameur, Air bike, Elliptique, two Battle rope): link to Biceps, Triceps, both, or none?
3. Do we remove "Bras" for good (patch moves everything, then the row is deleted), or keep the row unused?
4. Where do Biceps and Triceps sit in the order? Two new numbers, or renumber the list (Bras is 4, Avant-bras is 5)?
5. Should the patch also fix the legacy enum (BICEPS or TRICEPS) to match the new group, for the coach app and the program trees?
6. Do we write an audit row for the data patch (needs an admin to attribute it to, or a schema change), or follow the Lot12_2a precedent (no audit)?
7. The patch must start from the real PROD state: do we freeze Federico's edits during the deployment window, and who reads the PROD counts first?
8. Coach-owned exercises keep only BICEPS or TRICEPS (no group): leave them as they are?
9. Duplicate: copy in the form before saving (no API change) or a server button that creates the copy at once?
10. Name of the copy: empty, "(copie)" suffix, or left to the user with the 409 message?
11. Duplicate button: on the detail page only, or also in the list rows?
12. Should the copy start active or deactivated (today every create is active)?

## Not checked

- PROD and demo database contents (no database access by rule): real counts of "Bras" primary and secondary use, coach exercises with BICEPS or TRICEPS, groups possibly created by SQL.
- Mobile app and coach app code (not in this repo): hard-coded group names, ids or the legacy enum.
- The POC artifact itself (not opened, no link in the repo).
- `ExercisesApi.cs` beyond the lines read (create and list calls, `ExercisesApi.cs:12-41`).
- Runtime behaviour of the startup order on a fresh database (no `dotnet run`).
- Whether `UX_MUSCLE_GROUPS_NAME` collation in PROD differs from the local default (index definition read from code only).
- Program template bodies stored in PROD (only the seeder text was read).
