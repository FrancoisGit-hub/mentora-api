# Lot 12, sub-lot 12.2a: closed reference lists, one-time normalization, create-only catalog seeders (12.F, 12.G, 12.H)

Branch `feature/lot_12`, base `d900e4f`. Local run only (Docker PostgreSQL 16 on 5433, API on 5243, `ASPNETCORE_ENVIRONMENT=Development`, `Email__Provider=Logging`). No token, code, secret, connection string or address appears below: the app test accounts are "the member" and "the coach". Every output was produced by a command; secrets stayed in variables, only lengths and booleans were printed. API logs, preflight dumps and comparison outputs are under `docs/review/lot12/12.2a/*.log` (gitignored by `*.log`, not committed). The JSON captures of the mobile contract (`mobile-before/`, `mobile-after/`) were deleted after R4.

The PC was restarted between the R2 "before" fingerprint and the R2 second start. After the restart, read-only checks confirmed HEAD `d900e4f` with the uncommitted work, last migration `20261109113329_Lot12_2a_ExerciseReferences`, `DATA_PATCHES` 2 rows, port 5243 free, and the four fingerprint lines equal to those printed before the restart (`line 1..4 equal: True`).

---

## 1. Preflight

| Step | Output |
|---|---|
| `git status` / `log --oneline -2` | clean; `d900e4f docs(lot12): 12.1 runtime evidence`, `cdf9e08 feat(lot12): 12.1 admin authentication and disabled user refresh check` |
| `docker ps` | `mentora-postgres-local Up … 0.0.0.0:5433->5432/tcp` |
| last migration | `20261108223153_Lot12_1_AdminAuth` |
| `dotnet build --no-incremental` | `19 Avertissement(s)`, `0 Erreur(s)` (all CS1573) |
| `has-pending-model-changes` | `No changes have been made to the model since the last migration.`, 32 `warn` lines |

### 1.1 Data (Mentora rows = `EXERCISE_COACH_ID` null)

- Inventory 5.3 block: active with source code 248, active without source code 27, inactive 0; duplicate name groups after `lower(trim())` 0; `EXERCISE_VIDEOS` 0.
- By prefix: `EX` 248, null 27, other 0; total 275; coach-owned exercises 0.
- Distinct values (counts per value saved in `preflight-data.log`): category 4 values, type 9, laterality 4, movement pattern 90, starting position 46, grip type 14 (+ 115 NULL), secondary muscles 59 case-sensitive values in 219 rows (every jsonb is an array).
- Duplicates after accent folding (AccentFolding table read from `Mentora.Core/Text/AccentFolding.cs`) + lower + trim + whitespace collapse: 0.
- The 27 rows without source code are exactly 27 ExerciseSeeder default names (list in 1.2).

### 1.2 Legacy map (ExerciseSeeder.DefaultExercises, list order = LG01 to LG45)

```
LG01 Développé couché barre          superseded by EX001   LG24 Crunch                          null-code row
LG02 Développé couché haltères       superseded by EX016   LG25 Gainage planche                 null-code row
LG03 Écarté couché haltères          null-code row         LG26 Relevé de jambes suspendu       superseded by EX198
LG04 Pompes                          superseded by EX029   LG27 Rotation russe                  null-code row
LG05 Développé incliné haltères      superseded by EX018   LG28 Squat                           null-code row
LG06 Soulevé de terre                superseded by EX003   LG29 Fentes avant                    null-code row
LG07 Traction / tirage vertical      null-code row         LG30 Presse à cuisses                superseded by EX140
LG08 Rowing barre buste penché       superseded by EX004   LG31 Leg extension                   superseded by EX142
LG09 Rowing haltère unilatéral       superseded by EX052   LG32 Leg curl allongé                superseded by EX155
LG10 Tirage horizontal poulie basse  null-code row         LG33 Soulevé de terre jambes tendues null-code row
LG11 Développé épaules barre         null-code row         LG34 Good morning barre              superseded by EX154
LG12 Élévations latérales haltères   superseded by EX010   LG35 Hip thrust barre                superseded by EX007
LG13 Élévations frontales haltères   null-code row         LG36 Fentes bulgares                 null-code row
LG14 Oiseau (élévations arrière)     null-code row         LG37 Kickback à la poulie            null-code row
LG15 Curl biceps barre               null-code row         LG38 Mollets debout                  null-code row
LG16 Curl biceps haltères alterné    null-code row         LG39 Mollets assis                   superseded by EX164
LG17 Curl marteau haltères           null-code row         LG40 Burpees                         null-code row
LG18 Dips                            null-code row         LG41 Kettlebell swing                null-code row
LG19 Extension triceps poulie haute  null-code row         LG42 Thruster haltères               null-code row
LG20 Barre au front                  superseded by EX124   LG43 Corde à sauter                  superseded by EX235
LG21 Curl poignet barre              null-code row         LG44 Course à pied (tapis)           null-code row
LG22 Extension poignet haltère       null-code row         LG45 Rameur                          superseded by EX013
LG23 Suspension à la barre           null-code row
6a matched null-code rows: 27 | 6b superseded: 18 | 6c neither: 0 | 6d null-code rows matching no default: 0
```

No stop condition (no superseded default also has a null-code row, 6d = 0). The prompt expected "about 20" superseded defaults; the count is 18.

### 1.3 Map coverage

`REFERENCE_VALUE_MAP.csv`: 210 rows, 0 duplicate keys after NFC + trim + case folding, 0 codes absent from `EXERCISE_REFERENCES.csv` (62 rows) or from the MUSCLE_GROUPS names. Values from 1.1 absent from the map: 0. Expected counts derived from the map (used in R1):

```
category: mapped=248 unmapped=0
type: mapped=246 unmapped=2
laterality: mapped=248 unmapped=0
movement_pattern: mapped=218 unmapped=30
starting_position: mapped=238 unmapped=10
grip_type: mapped=160 unmapped=0
secondary: rows with jsonb=219 expected links=381 dropped elements=6
```

### 1.4 Mobile contract baseline

API start without migration (`[EMAIL] routing mode=TEST provider=Logging testRecipients=4 productionBcc=0`, no fail line). Member login (isCoach false) and coach login (isCoach true) 200. Saved: `GET /api/v1/member/exercises?limit=100` (100 items), the details of EX002, EX029 and "Squat" (no source code at that time), `GET /api/v1/member/exercise-filters`, and the coach list count (275). API stopped, `port 5243 in use: False`.

---

## 2. Decisions applied

D1 to D8 of the 12.2a prompt, unchanged, plus the following, agreed during the run:
- R4 versus D5: D5 kept. In R4, `sourceCode` going from null to an LG code is expected for the 27 legacy rows only; any other `sourceCode` change and any `updatedDate` change is a failure.
- The two patches never write `EXERCISE_UPDATED_DATE`: the member and coach contract exposes `updatedDate`.
- Known limitation (accepted): the normalization patch runs once. A CSV row created after the first start would keep raw text without foreign keys. Expected, no code change: the CSV is frozen since the database became the source of truth (12.G), and new exercises will be created through the 12.2b admin API, which writes the foreign keys and the text mirror.

## 3. Files

New:
- `Mentora.Core/Entities/ExerciseReferenceItems.cs` (ExerciseCategoryItem, ExerciseTypeItem, ExerciseLateralityItem, MovementPatternItem, StartingPositionItem, GripTypeItem), `ExerciseSecondaryMuscleGroup.cs`, `DataPatch.cs`
- `Mentora.Infrastructure/Persistence/Configurations/ExerciseReferenceConfigurations.cs` (one shared mapping helper + six configurations), `ExerciseSecondaryMuscleGroupConfiguration.cs`, `DataPatchConfiguration.cs`
- `Mentora.Infrastructure/Persistence/Migrations/20261109113329_Lot12_2a_ExerciseReferences.cs` (+ `.Designer.cs`)
- `Mentora.Infrastructure/Seeding/Catalog/EXERCISE_REFERENCES.csv`, `REFERENCE_VALUE_MAP.csv` (UTF-8 without BOM, content of the prompt, embedded as `Mentora.Catalog.<FILE>.csv`)
- `Mentora.Infrastructure/Seeding/ExerciseReferenceSeeder.cs`, `DataPatchRunner.cs`, `LegacyExerciseCodePatch.cs`, `ReferenceNormalizationPatch.cs`, `SeedingText.cs` (`SeedingText.NormalizeName` moved unchanged from CatalogSeeder, `SeedingText.FoldedKey`, `SeedingText.ReferenceKey`, `NormalizedNameComparer`, `LegacyMuscleGroupMapping.GroupName` moved from `CatalogSeeder.HistoricalGroupName` and rewritten as an if chain)

Modified:
- `Mentora.API/Program.cs`: seed order of D6.
- `Mentora.Core/Entities/Exercise.cs`: six foreign keys, six navigations, `SecondaryMuscleGroups`.
- `Mentora.Infrastructure/Mentora.Infrastructure.csproj`: two embedded resources.
- `Mentora.Infrastructure/Persistence/Configurations/ExerciseConfiguration.cs`: columns, indexes, Restrict foreign keys.
- `Mentora.Infrastructure/Persistence/MentoraDbContext.cs`: eight DbSets.
- `Mentora.Infrastructure/Persistence/Migrations/MentoraDbContextModelSnapshot.cs`: generated.
- `Mentora.Infrastructure/Seeding/CatalogSeeder.cs`: exercise step create-only by EX code with folded-name refusal; removed FillNulls, adoption by name, objective links on existing rows, historical equipment and muscle group fills; `ReadCsv` and `Clean` made internal for reuse.
- `Mentora.Infrastructure/Seeding/ExerciseSeeder.cs`: LG codes by list position (comment: never reorder nor remove), hard-coded superseded map, create-only by LG / EX code, folded-name refusal, primary group from the legacy code, "Sans matériel" for Bodyweight.
- `Mentora.Infrastructure/Seeding/ProgramTemplateSeeder.cs`: exercise lookup by normalized name, duplicate key logs a warning and keeps the first.

Differences from the prompt and the inventory:
- `Program.cs` seed calls were at lines 472-481 (inventory: 392-405, before 12.1 added code).
- The 18 superseded defaults (not about 20), see 1.2.
- Only `*.log` is gitignored in `docs/review`: dumps and logs are `.log` files; the JSON captures were deleted.

## 4. Migration `20261109113329_Lot12_2a_ExerciseReferences`

- `EXERCISE_CATEGORIES`, `EXERCISE_TYPES`, `EXERCISE_LATERALITIES`, `MOVEMENT_PATTERNS`, `STARTING_POSITIONS`, `GRIP_TYPES`: `<P>_ID` uuid PK `gen_random_uuid()`, `<P>_CODE` varchar(40), `<P>_LABEL` varchar(80), `<P>_SORT_ORDER` integer, `<P>_IS_ACTIVE` boolean (no database default, C# initializer true), `<P>_CREATED_DATE` / `<P>_UPDATED_DATE` timestamptz, all not null; `UX_<T>_CODE`, `UX_<T>_LABEL`; `CK_<T>_CODE_FORMAT` (`~ '^[A-Z0-9_]{1,40}$'`) and `CK_<T>_SORT_ORDER` (`>= 0`) by `migrationBuilder.Sql`.
- `EXERCISES`: `EXERCISE_CATEGORY_ID`, `EXERCISE_TYPE_ID`, `EXERCISE_LATERALITY_ID`, `EXERCISE_MOVEMENT_PATTERN_ID`, `EXERCISE_STARTING_POSITION_ID`, `EXERCISE_GRIP_TYPE_ID` uuid null, FK Restrict (EF-generated names, all under 63 characters), indexes `IX_EXERCISES_CATEGORY`, `_TYPE`, `_LATERALITY`, `_MOVEMENT_PATTERN`, `_STARTING_POSITION`, `_GRIP_TYPE`.
- `EXERCISE_SECONDARY_MUSCLE_GROUPS`: composite PK; `FK_EXERCISE_SECONDARY_MUSCLE_GROUPS_EXERCISE` (Cascade) and `FK_EXERCISE_SECONDARY_MUSCLE_GROUPS_MUSCLE_GROUP` (Restrict), named explicitly because the EF names would be truncated; `IX_EXERCISE_SECONDARY_MUSCLE_GROUPS_MUSCLE_GROUP`.
- `DATA_PATCHES`: `DATA_PATCH_NAME` varchar(100) PK, `DATA_PATCH_APPLIED_DATE` timestamptz not null.
- `UX_EXERCISES_MENTORA_NAME_CI`: `CREATE UNIQUE INDEX … ON "EXERCISES" (lower(btrim("EXERCISE_NAME"))) WHERE "EXERCISE_COACH_ID" IS NULL`. `IX_EXERCISES_COACH_NAME` unchanged.
- `Down`: drops the index, then the 12 CHECKs, then the generated objects.

Build after the changes: `19 Avertissement(s)` (all CS1573), `0 Erreur(s)`; `has-pending-model-changes`: `No changes`, 32 `warn` lines (no new sentinel warning).

---

## 5. Runtime proofs

### R1 First start (migration approved)

```
Applying migration '20261109113329_Lot12_2a_ExerciseReferences'.
MuscleGroupSeeder: created 0 muscle group(s).
[CATALOG] references created=62 existing=0
[PATCH] Lot12_2a_LegacyExerciseCodes applied: codes given=27 candidates=27 code already used=0
CatalogSeeder exercises: created 0, already present 248, skipped 2, objective links created 0, plural-only equipment matches 0, warnings 1.
[CATALOG] exercise EX135: source code used by 2 active rows ('Dead hang', 'Front squat'), all skipped
ExerciseSeeder: created 0, existing 27, superseded 18, skipped 0.
[PATCH] Lot12_2a_ReferenceNormalization category mapped=248 unmapped=0
[PATCH] Lot12_2a_ReferenceNormalization type mapped=246 unmapped=2
[PATCH] Lot12_2a_ReferenceNormalization laterality mapped=248 unmapped=0
[PATCH] Lot12_2a_ReferenceNormalization movement_pattern mapped=218 unmapped=30
[PATCH] Lot12_2a_ReferenceNormalization starting_position mapped=238 unmapped=10
[PATCH] Lot12_2a_ReferenceNormalization grip_type mapped=160 unmapped=0
[PATCH] Lot12_2a_ReferenceNormalization secondary links created=381 jsonb rewritten=219 elements dropped=6
(one line per unmapped value, see normalization-report.md)
[PATCH] Lot12_2a_ReferenceNormalization applied: category mapped=248 unmapped=0, type mapped=246 unmapped=2, laterality mapped=248 unmapped=0, movement_pattern mapped=218 unmapped=30, starting_position mapped=238 unmapped=10, grip_type mapped=160 unmapped=0, secondary links created=381, secondary elements dropped=6
Now listening on: http://localhost:5243
fail lines: 0 | exception lines: 0
```

The EX135 warning is pre-existing (two CSV rows share the code). The 138 "group mismatch" lines of CatalogSeeder are pre-existing information lines (sheet label versus derived group), unchanged.

Read-only SQL after the start:

```
 cat | typ | lat | mov | pos | grip
   4 |   8 |   4 |  23 |  15 |    8

       list        | expected_mapped | fk_set | expected_unmapped | text_no_fk
 category          |             248 |    248 |                 0 |          0
 type              |             246 |    246 |                 2 |          2
 laterality        |             248 |    248 |                 0 |          0
 movement_pattern  |             218 |    218 |                30 |         30
 starting_position |             238 |    238 |                10 |         10
 grip_type         |             160 |    160 |                 0 |          0

 expected_links | secondary_links
            381 |             381
 links_on_coach_rows: 0 | links_equal_primary: 0

 code  | name                | category    | type            | laterality | pattern             | position            | grip      | jsonb                             | linked_groups
 EX002 | Squat barre arrière | Musculation | Polyarticulaire | Bilatéral  | Squat               | Debout              | Pronation | ["Ischio-jambiers", "Fessiers"]   | Ischio-jambiers, Fessiers
 EX011 | Planche             | Gainage     | Isométrique     | Bilatéral  | Anti-extension      | Au sol              | (null)    | ["Épaules", "Fessiers"]           | Épaules, Fessiers
 EX029 | Pompes              | Musculation | Polyarticulaire | Bilatéral  | Poussée horizontale | Position de planche | Pronation | ["Épaules", "Bras", "Abdominaux"] | Épaules, Bras, Abdominaux

 expected | mentora_exercises
      275 |               275
 DATA_PATCHES: Lot12_2a_LegacyExerciseCodes, Lot12_2a_ReferenceNormalization
 lg_codes: 27
 CHECKs: CK_EXERCISE_CATEGORIES_CODE_FORMAT, CK_EXERCISE_CATEGORIES_SORT_ORDER, CK_EXERCISE_LATERALITIES_CODE_FORMAT, CK_EXERCISE_LATERALITIES_SORT_ORDER, CK_EXERCISE_TYPES_CODE_FORMAT, CK_EXERCISE_TYPES_SORT_ORDER, CK_GRIP_TYPES_CODE_FORMAT, CK_GRIP_TYPES_SORT_ORDER, CK_MOVEMENT_PATTERNS_CODE_FORMAT, CK_MOVEMENT_PATTERNS_SORT_ORDER, CK_STARTING_POSITIONS_CODE_FORMAT, CK_STARTING_POSITIONS_SORT_ORDER
 index: UX_EXERCISES_MENTORA_NAME_CI
```

EX011 keeps "Au sol" as starting position (deliberately unmapped value).

### R2 Idempotence

Fingerprint = md5 of the ordered full-row text of every Mentora `EXERCISES` row, md5 of the ordered `EXERCISE_OBJECTIVES` and `EXERCISE_SECONDARY_MUSCLE_GROUPS` rows, reference table counts.

```
before the second start (and again after the PC restart, identical):
exercises=275 md5=7779367946fc4df98f01972a85805602
objectives=561 md5=909084eea11b2ea33359c07645833073
secondary=381 md5=5369f423b7cfc0f560347825b50619d4
references=4/8/4/23/15/8

second start:
[CATALOG] references created=0 existing=62
[PATCH] Lot12_2a_LegacyExerciseCodes skipped (already applied)
CatalogSeeder exercises: created 0, already present 248, skipped 2, objective links created 0, plural-only equipment matches 0, warnings 1.
ExerciseSeeder: created 0, existing 27, superseded 18, skipped 0.
[PATCH] Lot12_2a_ReferenceNormalization skipped (already applied)
fail lines: 0 | exception lines: 0

after the second start: same four lines, identical to before: True
```

### R3 Admin edits survive (12.G)

LG04 is superseded (its row is EX029), so (b) used LG03. Originals read first (read-only): EX029 `Pompes`; LG03 `Écarté couché haltères`; EX002 movement pattern id `ac2df07c-ddae-4958-86a7-0918feb42d77`, text `Squat`, objectives Force / Hypertrophie / Performance, secondary groups Fessiers / Ischio-jambiers; EX006 active. Both link tables have only their two id columns (`information_schema.columns`: uuid, not null, no default), so a link is fully restored by its two ids.

Approved write (one transaction, a DO block raising unless each statement changes exactly 1 row; ASCII-only so no encoding can alter a name; no `*_UPDATED_DATE` touched):

```sql
BEGIN;
DO $$ DECLARE n int; BEGIN
  UPDATE "EXERCISES" SET "EXERCISE_NAME" = "EXERCISE_NAME" || ' test12a'
   WHERE "EXERCISE_ID" = 'e6524d10-d1b9-4627-a6fb-eb5610ec5c18' AND "EXERCISE_SOURCE_CODE" = 'EX029' AND "EXERCISE_NAME" NOT LIKE '% test12a';
  -- (check n = 1, same after each statement)
  UPDATE "EXERCISES" SET "EXERCISE_NAME" = "EXERCISE_NAME" || ' test12a'
   WHERE "EXERCISE_ID" = 'ed277597-8165-40dc-8a17-18cfad694cf7' AND "EXERCISE_SOURCE_CODE" = 'LG03' AND "EXERCISE_NAME" NOT LIKE '% test12a';
  UPDATE "EXERCISES" SET "EXERCISE_MOVEMENT_PATTERN_ID" = NULL, "EXERCISE_MOVEMENT_PATTERN" = NULL
   WHERE "EXERCISE_ID" = 'd4e29166-4101-4fae-bac6-7281a082c7f2' AND "EXERCISE_MOVEMENT_PATTERN_ID" = 'ac2df07c-ddae-4958-86a7-0918feb42d77' AND "EXERCISE_MOVEMENT_PATTERN" = 'Squat';
  DELETE FROM "EXERCISE_OBJECTIVES"
   WHERE "EXERCISE_OBJECTIVE_EXERCISE_ID" = 'd4e29166-4101-4fae-bac6-7281a082c7f2' AND "EXERCISE_OBJECTIVE_OBJECTIVE_ID" = 'dc39947d-4d5d-4c39-818b-b00d4762e8eb';
  DELETE FROM "EXERCISE_SECONDARY_MUSCLE_GROUPS"
   WHERE "EXERCISE_SECONDARY_MUSCLE_GROUP_EXERCISE_ID" = 'd4e29166-4101-4fae-bac6-7281a082c7f2' AND "EXERCISE_SECONDARY_MUSCLE_GROUP_MUSCLE_GROUP_ID" = '987b88ab-faa5-49b2-b1d6-2b119cbc31be';
  UPDATE "EXERCISES" SET "EXERCISE_IS_ACTIVE" = false
   WHERE "EXERCISE_ID" = '69b855e3-1cca-4f91-b800-ee383e499c8c' AND "EXERCISE_SOURCE_CODE" = 'EX006' AND "EXERCISE_IS_ACTIVE" = true;
  RAISE NOTICE 'R3 write: 6 statements, 1 row each';
END $$;
COMMIT;
-- NOTICE:  R3 write: 6 statements, 1 row each / COMMIT
```

```
fingerprint after the edits, before the start:
exercises=275 md5=beb9e3cecdc85637dab82bf21bbdaf2b
objectives=560 md5=b6aff1af580ea667aee427b58cf1f79b
secondary=380 md5=e33181ab41a26c45f8791fb931a55371
references=4/8/4/23/15/8
start: both patches skipped, CatalogSeeder created 0, ExerciseSeeder created 0, fail lines 0
fingerprint after the start: same four lines, unchanged by the start: True

 mentora_exercises: 275 (expected 275)
 EX029 | Pompes test12a
 LG03  | Écarté couché haltères test12a
 rows_with_original_names: 0
 ex002_pattern_id_null: t | ex002_pattern_text_null: t
 ex002_deleted_objective_present: 0
 ex002_deleted_secondary_present: 0
 ex006_active: f
```

Approved restore (same guard): names restored with `left("EXERCISE_NAME", -8)` where the name ends with ` test12a`, EX002 pattern id and text set back where both are null, the objective and secondary link reinserted by their two ids, EX006 set active where inactive.

```
NOTICE:  R3 restore: 6 statements, 1 row each / COMMIT
fingerprint after the restore:
exercises=275 md5=7779367946fc4df98f01972a85805602
objectives=561 md5=909084eea11b2ea33359c07645833073
secondary=381 md5=5369f423b7cfc0f560347825b50619d4
references=4/8/4/23/15/8
equals R2 fingerprint: True
```

### R4 Mobile contract

Captured on the R2 second start (catalog proven equal to the R2 fingerprint, which is also the state restored after R3). Same calls as 1.4.

```
keys identical at every level, member-list.json: True
keys identical at every level, member-detail-EX002.json: True
keys identical at every level, member-detail-EX029.json: True
keys identical at every level, member-detail-SQUAT.json: True
keys identical at every level, member-filters.json: True
member list: items before=100 after=100, same ids same order: True
member list body identical: True
coach list count before=275 after=275 equal: True
exercise-filters identical: True
--- detail EX002 (exerciseId d4e29166-4101-4fae-bac6-7281a082c7f2)
  gripType : "Pronée" -> "Pronation" [allowed]
  secondaryMuscles : ["Fessiers","ischio-jambiers"] -> ["Ischio-jambiers","Fessiers"] [allowed]
--- detail EX029 (exerciseId e6524d10-d1b9-4627-a6fb-eb5610ec5c18)
  secondaryMuscles : ["Triceps brachial","Deltoïde antérieur","Abdominaux"] -> ["Épaules","Bras","Abdominaux"] [allowed]
--- detail SQUAT (exerciseId 0a087c69-4fad-4bbb-94f9-37ceac076d87)
  sourceCode : null -> "LG28" [expected (legacy row, null -> LG code)]
detail failures: 0
```

No `updatedDate` changed. Every `sourceCode` change in the database (preflight dump versus a dump after R2, same 275 ids on both sides): 27 changes, all null to an LG code, all on legacy rows; no other `sourceCode` changed.

```
exercise id                          | before | after | name
ed277597-8165-40dc-8a17-18cfad694cf7 | null   | LG03  | Écarté couché haltères
b900b872-4a52-4e47-9151-25a43fef3e00 | null   | LG07  | Traction / tirage vertical
e183a428-e015-4e7f-91ec-09704786816f | null   | LG10  | Tirage horizontal poulie basse
0680ade4-af1f-4d3a-b420-de563aa86243 | null   | LG11  | Développé épaules barre
5433ecdf-cc35-4d75-9a1c-bd5604eb0a0e | null   | LG13  | Élévations frontales haltères
7423f08e-2349-4894-addf-649160213dd8 | null   | LG14  | Oiseau (élévations arrière)
de6b8a62-6708-4596-a708-e002566c14df | null   | LG15  | Curl biceps barre
79265c3f-211a-4636-80ae-cda18a6bcde1 | null   | LG16  | Curl biceps haltères alterné
03e7c5f3-f39e-4e48-a734-4e46f991eabd | null   | LG17  | Curl marteau haltères
7caa3fe7-e12e-40a8-8112-8bd91e20a1f9 | null   | LG18  | Dips
d2b7d1b1-fe2c-4a06-a1b6-df276673e569 | null   | LG19  | Extension triceps poulie haute
01ed62fb-3db6-4c3f-9879-e76560163205 | null   | LG21  | Curl poignet barre
4082eab9-032d-44c7-8d05-b8ec1e5f21c4 | null   | LG22  | Extension poignet haltère
873d24b6-e18c-41c8-a63d-f4f6fd6ac187 | null   | LG23  | Suspension à la barre
50847180-2747-4bf3-9d87-26e846cd3a4b | null   | LG24  | Crunch
f17f916b-b88d-4c73-904f-e8b1cdedf4c2 | null   | LG25  | Gainage planche
c9ef4b6f-a4e5-46f2-87a8-7c88691696e8 | null   | LG27  | Rotation russe
0a087c69-4fad-4bbb-94f9-37ceac076d87 | null   | LG28  | Squat
0cb74028-d2f2-4a8c-95f0-d097d6c9439c | null   | LG29  | Fentes avant
238ed23c-d1cb-4bb4-aa5a-fb1e88d0280b | null   | LG33  | Soulevé de terre jambes tendues
cbfe4f47-1018-4f81-a8b9-08fb65d2722b | null   | LG36  | Fentes bulgares
daecb6a0-4f67-4393-888a-19fa6ce6f59b | null   | LG37  | Kickback à la poulie
2aaddeac-118e-4a8e-aa3e-5903007cd0a9 | null   | LG38  | Mollets debout
18323604-d35c-48ec-8715-4d94da7dd217 | null   | LG40  | Burpees
91689976-f239-4f88-bc94-5b66fc75483b | null   | LG41  | Kettlebell swing
3e57e6fa-f87e-434d-9f3a-625aa5dc9aa0 | null   | LG42  | Thruster haltères
e5d4adb6-b943-47cd-9b87-69bff972eccc | null   | LG44  | Course à pied (tapis)
```

### R5 Fresh database (D8)

Approved `CREATE DATABASE mentora_parity` → `CREATE DATABASE`. First start against it: the connection string was built in a variable from the Development settings by replacing only the database name and passed as `ConnectionStrings__DefaultConnection` inside the start command (`connection string override: database replaced=True`, never printed, no file, not left in the shell).

```
first start on mentora_parity: 23 "Applying migration" lines
MuscleGroupSeeder: created 14 muscle group(s).
[CATALOG] references created=62 existing=0
[PATCH] Lot12_2a_LegacyExerciseCodes applied: codes given=0 candidates=0 code already used=0
CatalogSeeder no-equipment row: created 1, existing 0.
CatalogSeeder equipment: created 70, existing 0, skipped 0, warnings 0.
CatalogSeeder objectives: created 53, existing 0, skipped 0, warnings 0.
CatalogSeeder extra equipment: created 47, warnings 0.
CatalogSeeder exercises: created 248, already present 0, skipped 2, objective links created 561, plural-only equipment matches 3, warnings 1.
ExerciseSeeder: created 27, existing 0, superseded 18, skipped 0.
[PATCH] Lot12_2a_ReferenceNormalization applied: category mapped=248 unmapped=0, type mapped=246 unmapped=2, laterality mapped=248 unmapped=0, movement_pattern mapped=218 unmapped=30, starting_position mapped=238 unmapped=10, grip_type mapped=160 unmapped=0, secondary links created=381, secondary elements dropped=6
ProgramTemplateSeeder: seeded 2 Mentora program template(s).
QuizQuestionSeeder: created 900, existing 0, filled 0, skipped 74, warnings 74.
fail lines: 0 | exception lines: 0
```

Read-only comparison:

```
mentora exercises: mentora_db=275 | mentora_parity=275 | match=True
fk category: mentora_db=248 | mentora_parity=248 | match=True
fk type: mentora_db=246 | mentora_parity=246 | match=True
fk laterality: mentora_db=248 | mentora_parity=248 | match=True
fk movement_pattern: mentora_db=218 | mentora_parity=218 | match=True
fk starting_position: mentora_db=238 | mentora_parity=238 | match=True
fk grip_type: mentora_db=160 | mentora_parity=160 | match=True
secondary links: mentora_db=381 | mentora_parity=381 | match=True
reference counts: mentora_db=4/8/4/23/15/8 | mentora_parity=4/8/4/23/15/8 | match=True
objectives: mentora_db=53 | mentora_parity=53 | match=True
equipment: mentora_db=118 | mentora_parity=118 | match=True
muscle groups: mentora_db=14 | mentora_parity=14 | match=True
quiz questions: mentora_db=900 | mentora_parity=900 | match=True
exercise objective links: mentora_db=561 | mentora_parity=561 | match=True
data patches: mentora_db=2 | mentora_parity=2 | match=True
source codes: mentora_db=275 | mentora_parity=275 | symmetric difference=0 | match=True
normalized names: mentora_db=275 | mentora_parity=275 | symmetric difference=0 | match=True
program template names: mentora_db=2 | mentora_parity=2 | symmetric difference=0 | match=True
   template: Prise de masse — débutant
   template: Remise en forme — débutant
ALL MATCH: True
```

Second start on mentora_parity: 0 "Applying migration", references created=0 existing=62, both patches skipped, CatalogSeeder created 0, ExerciseSeeder created 0, fail lines 0. API stopped, port 5243 free. Approved `DROP DATABASE mentora_parity` → `DROP DATABASE`; `mentora_parity_exists: 0` (databases left: mentora_db, postgres, template0, template1).

### R6 Build and model

```
19 Avertissement(s)   (all CS1573, baseline)
0 Erreur(s)
has-pending-model-changes: No changes have been made to the model since the last migration.
warn lines: 32 (baseline)
port 5243 in use: False
```

## 6. Leak checks

Counts only, on the explicit list of the 26 files to commit (24 for the code commit + this file + `normalization-report.md`, `*.log` excluded). Each pattern first proven on a known file: JWT pattern, the OTP console marker (simple match), an address pattern (broader than the 12.1 one, hence 14 hits on the proof file instead of 1), and a connection string password pattern (key name followed by an equals sign and a value).

```
--- patterns proven on known files
jwt on docs/review/lot7/claims-after-4a.json: 2
otp marker on Mentora.Infrastructure/Services/AuthService.cs: 1
addr on docs/review/lot11/11.4/runtime-evidence.md: 14
conn on Mentora.API/appsettings.Development.json (gitignored, count only): 1
--- files to commit: 26
files with any match: 0
```

No file to commit matches any pattern, so no comparison with `2d1f843` is needed. `mobile-before/` and `mobile-after/` deleted: `Test-Path` False for both.

## 7. Notes and known limitations

- Known limitation (accepted, section 2): CSV rows added after the first start would not be normalized; the CSV is frozen and new exercises come from the 12.2b admin API.
- Reference labels are varchar(80) while the mirror columns are shorter (`EXERCISE_LATERALITY` 40, category/type/pattern/grip 60, starting position 80). Every current label fits; the 12.2b admin label validation must respect the mirror column length.
- The jsonb mirror of the secondary muscles now holds MUSCLE_GROUPS names (e.g. EX029 `["Épaules","Bras","Abdominaux"]` instead of the anatomical terms); this is the change allowed by R4.
- Deleting a secondary link (as in R3) does not rewrite the jsonb mirror: keeping the mirror in step is the job of the 12.2b admin write path.
- CatalogSeeder still logs the 138 pre-existing "group mismatch" information lines at each start (it resolves groups for every CSV row before checking existence).
