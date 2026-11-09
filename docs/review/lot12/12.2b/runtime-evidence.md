# Lot 12, sub-lot 12.2b: admin exercise API, reference lists, usage check and admin audit log (12.C, 12.E, decision A)

Branch `feature/lot_12`, base `de916be`. Local run only (Docker PostgreSQL 16 on 5433, API on 5243, `ASPNETCORE_ENVIRONMENT=Development`, `Email__Provider=Logging`). No token, code, secret, connection string or address appears below: the app test accounts are "the coach" and "the member", the admin used is admin #1 (admin #2 for the legacy proof). Every output was produced by a command; secrets stayed in variables. API logs are under `docs/review/lot12/12.2b/*.log` (gitignored by `*.log`, not committed). The JSON captures of the mobile contract (`mobile-before`, `mobile-after`) lived in the session scratchpad outside the repository and were deleted after P10.

Run start date read from the database before the first write: `2026-11-09 16:10:23.920982+00` (bound of the fixture cleanup).

---

## 1. Preflight

| Step | Output |
|---|---|
| `git status` / `log --oneline -3` | clean; HEAD `de916be docs(lot12): 12.2a evidence and normalization report` |
| `docker ps` | `mentora-postgres-local Up … 0.0.0.0:5433->5432/tcp`; last migration `20261109113329_Lot12_2a_ExerciseReferences` |
| `dotnet build --no-incremental` | `19 Avertissement(s)` (all CS1573), `0 Erreur(s)` |
| `has-pending-model-changes` | `No changes have been made to the model since the last migration.`, 32 `warn` lines |

Read-only SQL, Mentora exercises (`EXERCISE_COACH_ID` null):

```
active 275 / inactive 0
highest numeric EX code 250 (prompt expected EX251), EX codes 248, LG codes 27, EX202 present 0
legacy text with a null foreign key: category 0, type 2, laterality 0, movement pattern 30, starting position 10, grip 0 -> exercises with any legacy value: 42
unique indexes on EXERCISES: PK_EXERCISES, IX_EXERCISES_COACH_NAME (COACH_ID, NAME, NULLS NOT DISTINCT), UX_EXERCISES_SOURCE_CODE, UX_EXERCISES_MENTORA_NAME_CI
usage baseline (distinct Mentora exercises): PROGRAM_EXERCISES 0, PROGRAM_TEMPLATES bodies 20, SESSION_TEMPLATES contents 0
used by a program template: EX001, EX004, EX029 | used nowhere: EX002, EX003, EX005
```

Differences from the prompt: the highest code is EX250 (not EX251), so the next codes are EX251 then EX252; the `hasLegacyValues=true` count is 42 (2 + 30 + 10, no overlap).

Mobile contract baseline (old build, API start approved): member list `limit=100` 25776 bytes (100 items), details of EX002, EX029 and LG28, search `squat` (`limit=20`), `exercise-filters`, coach list 275 items and the full EX002 coach item. API stopped, port 5243 free.

## 2. Decisions applied

B1 to B10 of the prompt, unchanged. Accepted deviations (agreed after the build report):
- an extra enabled-admin check on every write (the token of a disabled admin stays valid up to 15 minutes): 401 `Admin account not found or disabled.`;
- the 409 `error` text is the code itself (`EXERCISE_NAME_TAKEN`, `EXERCISE_IN_USE`), `details` alongside;
- 422 `errors` keys are PascalCase property names (`CategoryId`, `SecondaryMuscleGroupIds`, …), like the rest of the API;
- an update that changes only mirrors or derived columns (a drifted mirror being corrected) lists them in the audit row under their column names (`categoryText`, `secondaryMuscles`, `isPolyarticular`, `legacyMuscleGroup`, …) instead of an empty list;
- deprecated `EXERCISE_MUSCLE_GROUP` for an admin write has no principal-muscle text: a "Bras" primary group gives BICEPS, never TRICEPS (open point for the mobile app, section 7).

Other choices:
- `hasLegacyValues=false` returns the exercises with no legacy value.
- A search term shaped like `^(EX|LG)[0-9]+$` (case-insensitive) filters on that exact source code and skips the name ranking.
- Label length guard: a reference label longer than its mirror column (category 60, type 60, laterality 40, movement pattern 60, starting position 80, grip 60) is a 422 on that property instead of a database error.
- Usage counts use one statement per table (`jsonb_path_exists(... '$.**.exerciseId ? (@ == $id)', jsonb_build_object('id', <id>))`, passed as a parameter, never a LIKE). The names of the Mentora templates in the 409 `EXERCISE_IN_USE` details come from two extra statements run on that path only.

## 3. Files

New:
- `Mentora.Core/DTOs/AdminExercise/AdminExerciseDtos.cs`
- `Mentora.Core/Validators/AdminExercise/AdminExerciseValidators.cs` (request shape rules, activation body)
- `Mentora.Core/Interfaces/IAdminExerciseService.cs`
- `Mentora.Core/Entities/AdminAuditLog.cs`, `Mentora.Core/Enums/AdminAuditAction.cs` (actions, entity types, if-chain mappings)
- `Mentora.Infrastructure/Services/AdminExerciseService.cs`, `ExerciseListPaging.cs`
- `Mentora.Infrastructure/Seeding/LegacyExerciseColumns.cs`
- `Mentora.Infrastructure/Persistence/Configurations/AdminAuditLogConfiguration.cs`
- `Mentora.Infrastructure/Persistence/Migrations/20261109160428_Lot12_2b_AdminAuditLog.cs` (+ `.Designer.cs`)
- `Mentora.API/Controllers/Admin/AdminExercisesController.cs` (route `api/v1/admin`, policy `AdminOnly`, Swagger document `admin`)

Modified:
- `Mentora.API/Program.cs`: `IAdminExerciseService` registration.
- `Mentora.Infrastructure/Persistence/MentoraDbContext.cs`: `AdminAuditLogs` DbSet.
- `Mentora.Infrastructure/Persistence/Migrations/MentoraDbContextModelSnapshot.cs`: generated.
- `Mentora.Infrastructure/Seeding/CatalogSeeder.cs`: `LegacyMuscleGroup` and the code mappings moved to `LegacyExerciseColumns` (the `switch` expressions rewritten as if chains, same results); behavior unchanged.
- `Mentora.Infrastructure/Services/ExerciseService.cs`: member list limit, search term and cursor handling moved to `ExerciseListPaging` (same messages, same cursor payloads). `CreateAsync`, `UpdateAsync`, `DeleteAsync` and the coach controller are untouched (`git diff HEAD` on `Mentora.API/Controllers/Coach`, `Controllers/Member`, `DTOs/Exercise`, `Validators/Exercise`, `IExerciseService.cs`: no change).

Route collision check: `GET/POST /api/v1/admin/exercises`, `GET/PUT /api/v1/admin/exercises/{id}`, `PUT .../activation`, `GET /api/v1/admin/exercise-references` collide with no existing controller (the only other admin controller is `api/v1/admin/auth`).

## 4. Migration `20261109160428_Lot12_2b_AdminAuditLog`

- `ADMIN_AUDIT_LOG`: `ADMIN_AUDIT_LOG_ID` uuid PK `gen_random_uuid()`, `ADMIN_AUDIT_LOG_ADMIN_USER_ID` uuid not null (FK `FK_ADMIN_AUDIT_LOG_ADMIN_USER` to `ADMIN_USERS`, Restrict), `ADMIN_AUDIT_LOG_ACTION` varchar(40), `ADMIN_AUDIT_LOG_ENTITY_TYPE` varchar(40), `ADMIN_AUDIT_LOG_ENTITY_ID` uuid, `ADMIN_AUDIT_LOG_CHANGES` jsonb, `ADMIN_AUDIT_LOG_CREATED_DATE` timestamptz, all not null.
- Indexes: `IX_ADMIN_AUDIT_LOG_ADMIN_USER`, `IX_ADMIN_AUDIT_LOG_ENTITY` (entity type, entity id, created date), `IX_ADMIN_AUDIT_LOG_CREATED_DATE`.
- `CK_ADMIN_AUDIT_LOG_ACTION` (`EXERCISE_CREATED`, `EXERCISE_UPDATED`, `EXERCISE_ACTIVATED`, `EXERCISE_DEACTIVATED`) and `CK_ADMIN_AUDIT_LOG_ENTITY_TYPE` (`EXERCISE`) by `migrationBuilder.Sql`; `Down` drops both CHECKs, then the table.
- Build after the changes: `19 Avertissement(s)` (all CS1573), `0 Erreur(s)`; `has-pending-model-changes`: `No changes`, 32 `warn` lines.

Write behavior implemented in `AdminExerciseService`:
- the six lists: id set = foreign key set and text mirror = the row's label; id null with the foreign key set clears both; id null with no foreign key keeps the legacy text;
- secondary groups: links replaced by the requested set, jsonb mirror = group names by `MUSCLE_GROUP_SORT_ORDER` (null when none); objectives replaced; `isPolyarticular` = type code `POLYARTICULAIRE`; deprecated columns recomputed with the shared `LegacyExerciseColumns`;
- create: one transaction holding `pg_advisory_xact_lock(7122002)`, name check, `EX` + (highest EX number + 1), at least 3 digits, exercise + links + audit row in one `SaveChanges`;
- name uniqueness with `SeedingText.FoldedKey`; a unique violation on `UX_EXERCISES_MENTORA_NAME_CI` or `IX_EXERCISES_COACH_NAME` raised by `SaveChanges` maps to the same 409;
- update: the whole stored state (fields, links, mirrors, derived columns) is compared before and after; equal = 200, no write, no audit row, `updatedDate` unchanged.

---

## 5. Runtime proofs

Fixtures: every exercise created during the proofs has a name starting with `l122b ` (7 exercises: one coach-owned created through the coach API, six through the admin API) plus one inactive grip type `L122B_INACTIVE`.

### P1 First start and second start

```
first start
      Applying migration '20261109160428_Lot12_2b_AdminAuditLog'.
      [CATALOG] references created=0 existing=62
      [PATCH] Lot12_2a_LegacyExerciseCodes skipped (already applied)
      CatalogSeeder exercises: created 0, already present 248, skipped 2, objective links created 0, plural-only equipment matches 0, warnings 1.
      ExerciseSeeder: created 0, existing 27, superseded 18, skipped 0.
      [PATCH] Lot12_2a_ReferenceNormalization skipped (already applied)
      [ADMIN] accounts listed=4 created=0 reenabled=0 disabled=0
      Now listening on: http://localhost:5243
fail/crit/unhandled lines: 0
second start: no "Applying migration", references created=0 existing=62, both patches skipped, CatalogSeeder created 0, ExerciseSeeder created 0, fail/crit/unhandled lines 0
```

### P2 Authorization

```
GET  /api/v1/admin/exercise-references   | no token 401 | member token 401 | coach token 401 | admin token 200
GET  /api/v1/admin/exercises             | no token 401 | member token 401 | coach token 401 | admin token 200
GET  /api/v1/admin/exercises/{id}        | no token 401 | member token 401 | coach token 401
POST /api/v1/admin/exercises             | no token 401 | member token 401 | coach token 401
PUT  /api/v1/admin/exercises/{id}        | no token 401 | member token 401 | coach token 401
PUT  /api/v1/admin/exercises/{id}/activation | no token 401 | member token 401 | coach token 401
```

### P3 Reference lists

```
categories=4 types=8 lateralities=4 movementPatterns=23 startingPositions=15 gripTypes=8 muscleGroups=14 objectives=53 levels=BEGINNER,INTERMEDIATE,ADVANCED
equipment api=118 | db count 118
first equipment is 'Sans matériel': True | sort order ascending 1,2,3,4
item keys: id,code,label,sortOrder,isActive
```

### P4 List

```
default page: status 200 items 50 nextCursor present True
item keys: exerciseId,sourceCode,name,category,primaryMuscleGroup,primaryEquipment,level,isActive,hasVideo,hasLegacyValues,updatedDate
page 2 via cursor: items 50, overlap with page 1: 0
isActive=false: items 0 | isActive=true: 200
hasLegacyValues=true count: 42 (preflight 42), all flagged true | hasLegacyValues=false first page all false
search squat: 9 items, ranked: LG28 Squat, EX002 Squat barre arrière, EX137 Squat haltères, EX144 Bulgarian split squat, EX136 Goblet squat, EX139 Hack squat, EX168 Saut squat, EX150 Sissy squat assisté, ...
search ex029: 1 item (EX029) | search lg28: 1 item (LG28)
invalid level=XXX 400 | limit=0 400 | limit=101 400 | cursor=abc 400 | cursor of the wrong shape 400 | search of 1 character 400
```

SQL of one list call (`GET /api/v1/admin/exercises`, default page, EF command logging): **1 statement**.

```sql
SELECT t."EXERCISE_ID", t."EXERCISE_SOURCE_CODE", t."EXERCISE_NAME", e0."EXERCISE_CATEGORY_LABEL", m."MUSCLE_GROUP_NAME", e1."EQUIPMENT_NAME", t."EXERCISE_MIN_LEVEL", t."EXERCISE_IS_ACTIVE", e2."EXERCISE_VIDEO_ID" IS NOT NULL, (<six legacy tests>), t."EXERCISE_UPDATED_DATE"
FROM (
    SELECT e."EXERCISE_ID", ... FROM "EXERCISES" AS e
    WHERE e."EXERCISE_COACH_ID" IS NULL
    ORDER BY e."EXERCISE_NAME", e."EXERCISE_ID"
    LIMIT @__p_0
) AS t
LEFT JOIN "EXERCISE_CATEGORIES" AS e0 ON ... LEFT JOIN "MUSCLE_GROUPS" AS m ON ... LEFT JOIN "EQUIPMENT" AS e1 ON ... LEFT JOIN "EXERCISE_VIDEOS" AS e2 ON ...
ORDER BY t."EXERCISE_NAME", t."EXERCISE_ID"
```

### P5 Detail

```
EX002 | Squat barre arrière
  category=Musculation type=Polyarticulaire laterality=Bilatéral pattern=Squat position=Debout grip=Pronation (ids set)
  legacyValues all null | isPolyarticular=True | primaryGroup=Quadriceps | secondary=Ischio-jambiers, Fessiers
  primaryEquipment=Barre olympique level=INTERMEDIATE objectives=Force, Hypertrophie, Performance
  usage={"programs":0,"programTemplates":0,"sessionTemplates":0} video=null
EX001 usage={"programs":0,"programTemplates":1,"sessionTemplates":0}
legacy exercise EX237 | Battle rope vagues alternées | legacyValues filled: movementPattern=Ondulation alternée | movementPattern foreign key null
random id: 404
coach fixture created through the coach API: 201 | admin GET 404 | PUT 404 | activation 404 | not in the admin list
```

### P6 Create

```
Alpha: 201 sourceCode EX251, isActive true, created==updated; name/description/mistakes trimmed, level BEGINNER (sent as "beginner"), isPolyarticular true
  secondary groups in sort order: Dos, Bras | objectives: Activation, Agilité | legacyValues all null | usage 0/0/0
SQL read-back: EX251 | mentora t | cat Musculation | typ Polyarticulaire | lat Bilatéral | pat Poussée horizontale | pos Debout | grip Sans objet | poly t | legacy_group CHEST | legacy_equip OTHER | mirror ["Dos", "Bras"] | BEGINNER | secondary links 2 | objective links 2
Bravo: 201 EX252
two creates sent in parallel (HttpClient, same instant): 201/201, codes EX253 and EX254, different: True
name equal with other case, accents and spaces: 409 error=EXERCISE_NAME_TAKEN, details.exerciseId == Alpha, details.sourceCode EX251, details.name == Alpha's name
name "SQUAT" (real exercise): 409 EXERCISE_NAME_TAKEN, details.sourceCode LG28
422 with the property keys: unknown category CategoryId | unknown primary group PrimaryMuscleGroupId | unknown secondary group SecondaryMuscleGroupIds | unknown primary equipment PrimaryEquipmentId | unknown objective ObjectiveIds | inactive grip type GripTypeId | duplicate secondary group SecondaryMuscleGroupIds | secondary group == primary SecondaryMuscleGroupIds | 7 secondary groups SecondaryMuscleGroupIds | secondary equipment "Sans matériel" SecondaryEquipmentId | secondary equipment == primary SecondaryEquipmentId | missing category CategoryId | missing primary equipment PrimaryEquipmentId | empty name Name | bad level Level | 21 mistakes CommonMistakes | empty body Name,CategoryId,PrimaryEquipmentId
no exercise left by a refused call (fixtures: 5 before the Legacy fixture: coach, Alpha, Bravo, Par1, Par2)
```

### P7 Update

A bug was found by this proof in the first run and fixed before the final run: `ApplyState` set the navigation properties to null after assigning the foreign keys. With the reference rows tracked (loaded by the existence checks), EF read that as severing the relationship and wrote the list foreign keys as NULL (the text mirrors stayed). The first fixture ("Alpha") was damaged that way: its audit row listed `categoryId`, `typeId`, … with `before: null`. Fix: `ApplyState` assigns only the foreign key columns (comment in the code). The proof was rerun on a fresh fixture "Delta" (EX256); every Alpha row was removed by the cleanup.

```
Delta created: 201 EX256 | 6 of 6 list FKs set
unchanged body: 200 | xmin + EXERCISE_UPDATED_DATE before == after True | ADMIN_AUDIT_LOG 11 -> 11 | response updatedDate unchanged
one field changed (coachTip): 200 | stored date changed | audit 11 -> 12 | 6 of 6 list FKs still set
  last audit: EXERCISE_UPDATED [{"after": "tip changed", "field": "coachTip", "before": "tip"}]
  detail still carries category, type, grip, primary group, primary equipment
several fields (secondary groups, objectives, level, secondary equipment, grip): 200 | 5 of 6 list FKs set | mirror ["Épaules"], grip text NULL, 1 secondary link, 1 objective link
  audit fields: gripTypeId,secondaryMuscleGroupIds,secondaryEquipmentId,level,objectiveIds
rename Bravo to Delta's name: 409 EXERCISE_NAME_TAKEN (details.sourceCode EX256)
rename Delta to its own name in another case: 200 (no conflict with itself)
update with unknown category: 422 CategoryId | update of an unknown id: 404
```

Legacy rule on the fixture "l122b Legacy" (EX255) only. Approved write (1 row, no `*_UPDATED_DATE` touched): `EXERCISE_MOVEMENT_PATTERN_ID = NULL`, `EXERCISE_MOVEMENT_PATTERN = 'l122b legacy pattern'`. Real legacy exercise EX237 untouched afterwards (`Ondulation alternée`, foreign key null).

```
after the UPDATE: fk NULL | text 'l122b legacy pattern' | detail movementPattern null, legacyValues.movementPattern filled | listed by hasLegacyValues=true: 1
step 1  id null (text present): 200, text kept (also when another field changes in the same call)
        the first call also wrote one row: it differed from the fixture by nameEn only (audit: nameEn Alpha EN -> Delta EN), not by the legacy text
step 2  id set (Poussée verticale): 200 | fk set, text = label | legacyValues.movementPattern null | listed by hasLegacyValues=true: 0 | audit fields: movementPatternId
step 3  id null again: 200 | fk NULL and text NULL | audit fields: movementPatternId
```

### P8 Activation

```
deactivate an unused fixture: 200, isActive false | ADMIN_AUDIT_LOG 14 -> 15
same call again: 200 | 15 -> 15 (no new row)
last audit: EXERCISE_DEACTIVATED {"isActive": {"after": false, "before": true}}
deactivate EX001 (used by a Mentora program template): 409 error=EXERCISE_IN_USE
  details: {"programs":0,"programTemplates":1,"sessionTemplates":0,"mentoraProgramTemplateNames":["Prise de masse — débutant"],"mentoraSessionTemplateNames":[]}
  xmin + updated date unchanged: True | audit 15 -> 15 | EX001 still active
reactivate the fixture: 200, isActive true
activation with an empty body: 422 IsActive
```

### P9 Audit rows

```
distinct admin ids in ADMIN_AUDIT_LOG / all equal to the logged-in admin: 1 / true
Delta rows in order: EXERCISE_CREATED > EXERCISE_UPDATED > EXERCISE_UPDATED > EXERCISE_UPDATED > EXERCISE_DEACTIVATED > EXERCISE_ACTIVATED
created row keys: name, level, nameEn, typeId, coachTip, isActive, breathing, categoryId, gripTypeId, searchTags, sourceCode, description, precautions, instructions, lateralityId, objectiveIds, commonMistakes, movementPatternId, primaryEquipmentId, startingPositionId, primaryMuscleGroupId, secondaryEquipmentId, secondaryMuscleGroupIds (sourceCode EX256, ids set)
activation rows: {"isActive": {"after": false, "before": true}} and {"isActive": {"after": true, "before": false}}
audit rows about a non-fixture exercise: 0
```

Whole table at the end of the proofs: 6 created, 6 updated, 2 activated, 2 deactivated (the Alpha rows of the first run included), then the 3 legacy steps (20 rows removed by the cleanup).

### Fixture cleanup

Dry run first (same file, last line `ROLLBACK;`), then the COMMIT file, approved on three conditions checked before running: legacy proof passed; the two files differ only by the last line (`Compare-Object`: `ROLLBACK;` versus `COMMIT;`, 41 lines each); in-transaction counts. Both runs printed:

```
NOTICE:  deleted: audit 20 | objective links 10 | secondary links 10 | exercises 7 | grip types 1
 exercises | exercises_after_start | objective_links | secondary_links | audit_rows | grip_types | fixtures_left
       275 |                     0 |             561 |             381 |          0 |          8 |             0
```

The DO block raises unless exactly 7 fixture exercises are found and deleted and exactly 1 grip type is deleted; deletes are bounded by the fixture ids (temporary table) and by the run start date.

Leftover check after the COMMIT (read-only):

```
EXERCISES created >= run start 0 | total 275 | named l122b 0 | coach-owned 0
EXERCISE_OBJECTIVES total 561 (orphan links 0) | EXERCISE_SECONDARY_MUSCLE_GROUPS total 381 (orphan links 0)
ADMIN_AUDIT_LOG created >= run start 0 | total 0
GRIP_TYPES created >= run start 0 | total 8 | code L122B_INACTIVE 0
EX001 still active 1
```

The logins of the proofs also left normal rows in the authentication tables (app and admin OTPs, refresh tokens, last login dates), as in the previous lots; they are not fixtures and were not deleted.

### P10 Mobile contract (fixtures deleted)

Same calls as the preflight, captured with the final build.

```
member-list: byte-identical True (26000 bytes) | keys identical at every level True
member-detail-EX002: byte-identical True | keys identical True
member-detail-EX029: byte-identical True | keys identical True
member-detail-LG28: byte-identical True | keys identical True
member-search-squat: byte-identical True | keys identical True
member-filters: byte-identical True | keys identical True
coach item EX002 (full body): byte-identical True | keys identical True
member list: items before=100 after=100, same ids same order: True
coach list count before=275 after=275 equal: True
```

Coach write routes: `git diff HEAD` shows no change on `Mentora.API/Controllers/Coach`, on `ExerciseService.CreateAsync`, `UpdateAsync` and `DeleteAsync`, on the exercise DTOs, validator and `IExerciseService`; `ExerciseService.cs` only changes the member list limit/search validation and the cursor helpers moved to `ExerciseListPaging`. At runtime `POST /api/v1/coach/exercises` answered 201 with the new build (the coach fixture of P5). `PUT /api/v1/coach/exercises/{id}` was not run: it would need a second coach fixture and one more cleanup write, and its code did not change.

### P11 Build and model

```
19 Avertissement(s)   (all CS1573, baseline; non-CS1573 warnings: 0)
0 Erreur(s)
has-pending-model-changes: No changes have been made to the model since the last migration.
warn lines: 32 (baseline)
API stopped, port 5243 in use: False
```

---

## 6. Leak checks

Counts only, on the explicit list of files to commit (see the staged file list of each commit); patterns proven first on known files (JWT pattern, console OTP marker as a simple match, address pattern, connection string password pattern). Results are in section 8.

## 7. Open points

- Mobile app: the deprecated `EXERCISE_MUSCLE_GROUP` of an exercise written through the admin API cannot be TRICEPS (no principal-muscle text in the request): a "Bras" primary group gives BICEPS. The member and coach endpoints still expose that column until it is dropped.
- A reference row that is inactive but already held by an exercise is accepted by the rule (exemption) and was not exercised at runtime (it would need a second reference fixture and a later deactivation write); the inactive-id refusal on create was proved.
- The coach `PUT /api/v1/coach/exercises/{id}` route was not run at runtime in P10 (git diff only, see above).
- Race on deactivation: the usage check and the write are not in one lock; a template saved in between could still reference a just-deactivated exercise.
- The enabled-admin check adds one query per write; reads rely on the token alone.
- `hasLegacyValues=false` and the code-shaped search term are interpretations of B2 (section 2).
- No audit read endpoint, no physical deletion, no video upload, no reference-list edition (out of scope).

## 8. Leak check results

Patterns first proven on known files: JWT pattern on `docs/review/lot7/claims-after-4a.json` = 2; the console OTP marker (literal match) on `Mentora.Infrastructure/Services/AuthService.cs` = 1; address pattern on `docs/review/lot11/11.4/runtime-evidence.md` = 14; password pattern on `Mentora.API/appsettings.Development.json` (gitignored, count only) = 1.

On the explicit list of the 18 files to commit (17 for the code commit + this file): every count is 0 (JWT, OTP marker, address, password pattern); files with any match: 0, so no comparison with `de916be` is needed. The scratch captures were deleted (`Test-Path` False for both); the 12 API logs stay local (gitignored by `*.log`).
