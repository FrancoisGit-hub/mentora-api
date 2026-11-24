# Lot 14.9a: the 12 circuit modes (block types), runtime evidence

Branch feature/lot_14, base commit c6ac24f, local database only (mentora_db, docker container mentora-postgres-local).
API run locally on port 5243 with ASPNETCORE_ENVIRONMENT=Development and Email__Provider=Logging, log in api-149a.log (ignored by git, not committed).

## 1. Decisions applied (decision 14.I)

| Decision | Applied |
|---|---|
| 3.1 the 12 modes | `Mentora.Core/Programs/CircuitModes.cs`, `All` |
| 3.2 rules per mode | `ProgramCircuitRules.cs` (see section 2) |
| 3.3 legacy STANDARD accepted, stored as SERIE_SIMPLE | validated as SERIE_SIMPLE, normalised on every write path |
| 3.4 single source | `CircuitModes` (values, `StandardLike`, `LegacyStandard`, `Normalize`, `IsValid`, `NormalizeAll`), no enum, no EnumMappings entry |
| 3.5 normalisation on every write path | section 3 |
| 3.6 data-only migration | `20261124221921_Lot14_9a_CircuitModes` (section 5) |
| 3.7 XML docs | `ProgramTemplateCircuit.Mode`, `UpdateProgramCircuit.Mode`, both exercise DTOs, `ProgramCircuit` entity comment |

## 2. Files changed (file:line on this working tree)

Code:
- `Mentora.Core/Programs/CircuitModes.cs` (new): 12 constants, `All` (line 28), `StandardLike`, `Normalize` line 38, `NormalizeAll` lines 44 and 57.
- `Mentora.Core/Validators/ProgramTemplate/ProgramCircuitRules.cs`: normalised mode line 41, mode check and message lines 43-44, INTERVAL Rounds line 46, mode passed to exercises, STANDARD-like exercise rules line 83, INTERVAL exercise rules line 91.
- `Mentora.Infrastructure/Seeding/ProgramTemplateSeeder.cs`: seven `Mode = "STANDARD"` literals replaced by `CircuitModes.SerieSimple` (lines 119, 137, 155, 165, 190, 217, 233). Not in the original list: accepted by François, because on a fresh database the seeder would otherwise write STANDARD after the migration. The seeder still inserts only missing platform templates, so the migration stays necessary for existing rows.
- XML doc comments: `ProgramTemplateCircuit.cs`, `UpdateProgramCircuit.cs`, `ProgramTemplateExercise.cs`, `UpdateProgramExercise.cs`, `ProgramCircuit.cs` (comments only, no property change).

Migration:
- `Mentora.Infrastructure/Persistence/Migrations/20261124221921_Lot14_9a_CircuitModes.cs` and its `.Designer.cs`. `MentoraDbContextModelSnapshot.cs` is unchanged (no model change).

Failure keys are unchanged. One message change: the exercise-level messages now name the mode of the request (for example "SERIE_SIMPLE circuits require ...") instead of "STANDARD circuits require ...".

## 3. Normalisation call sites (3.5)

| Write path | Call site |
|---|---|
| Program templates POST | `Mentora.Infrastructure/Services/ProgramTemplateService.cs:69` |
| Program templates PUT | `ProgramTemplateService.cs:102` |
| Session templates POST | `Mentora.Infrastructure/Services/SessionTemplateService.cs:132` |
| Session templates PUT | `SessionTemplateService.cs:163` |
| Training programs POST (body, no templateId) | `Mentora.Infrastructure/Services/ProgramService.cs:107` |
| Training programs PUT (id-based) | `ProgramService.cs:398` |

Every call runs after validation and before the value is stored or serialised.

Copy paths without validation, no change needed once the data migration has run (the source data no longer holds STANDARD, so the copy cannot reintroduce it):
- assignment of a template to a member: `ProgramService.cs:856` (`ProgramCircuitMode = tc.Mode`);
- session template duplication: `SessionTemplateService.cs:199` (jsonb copied as is).
Proven at runtime: the assignment of `l149a template` stored BISET, AMRAP and INTERVAL unchanged (section 7).

## 4. Build

`dotnet build --no-incremental` (output in build.log, not committed): 0 Erreur(s), 19 Avertissement(s), same as before the change.

## 5. Migration

Up: `PROGRAM_CIRCUITS.PROGRAM_CIRCUIT_MODE` STANDARD becomes SERIE_SIMPLE; in `PROGRAM_TEMPLATES.PROGRAM_TEMPLATE_BODY` and `SESSION_TEMPLATES.SESSION_TEMPLATE_CONTENT` the exact token `"mode": "STANDARD"` becomes `"mode": "SERIE_SIMPLE"` on the jsonb::text form (WHERE LIKE filter, cast back to jsonb).
Down: a DO block raises an exception if the column or either jsonb holds a mode other than SERIE_SIMPLE, INTERVAL or STANDARD; otherwise the reverse rewrite.

Token form check on the local database before writing the migration (read only): the jsonb::text rendering contains `"mode": "STANDARD"` exactly (key name, one space after the colon): 28 tokens in 2 program template rows.

Log lines when the API started (api-149a.log):
- `Applying migration '20261124221921_Lot14_9a_CircuitModes'.`
- 0 `fail:` lines, 0 `crit:` lines. The `warn:` lines are the 15 existing EF sentinel-value warnings and the existing `[CATALOG]` seeder warnings, none new.

## 6. Counts before and after (precheck.sql)

`before.txt` (before the migration) and `after.txt` (after the migration):

| Location | before | after |
|---|---|---|
| PROGRAM_CIRCUITS rows | 0 | 0 |
| PROGRAM_TEMPLATES rows | 2 | 2 |
| program templates, `"mode": "STANDARD"` tokens (rows with it) | 28 (2) | 0 (0) |
| program templates, `"mode": "SERIE_SIMPLE"` tokens | 0 | 28 |
| program templates, `"mode": "INTERVAL"` tokens | 4 | 4 |
| nature tokens (STANDARD, EVALUATION) in program templates | 0, 0 | 0, 0 |
| SESSION_TEMPLATES rows | 0 | 0 |

STANDARD tokens are 0 after, the SERIE_SIMPLE count equals the previous STANDARD count (28), the nature counts are identical.

Limit of the nature check: the session nature of a session template lives in the column `SESSION_TEMPLATES.SESSION_TEMPLATE_NATURE`, not in the jsonb, and the program template jsonb written locally carries no `"nature"` key. The token counts for nature are therefore 0 on both sides and prove little by themselves. Nature safety rests on the token including the key (`"mode": "STANDARD"` cannot match a nature) and on the column counts (20 rows in STANDARD before and after the Up proof of section 9). `precheck.sql` also counts the column.

## 7. HTTP proofs

Tokens were obtained inside scripts (OTP request then verify, code read from the log inside the script) and never printed. Fixture names are prefixed `l149a `.

### 7.1 Session templates (POST /api/v1/coach/session-templates, then GET by id)

One 201 per mode, GET returns the same mode:

| Mode | POST | GET mode | Other fields returned |
|---|---|---|---|
| SERIE_SIMPLE | 201 | SERIE_SIMPLE | sets 3, reps 10 |
| BISET | 201 | BISET | sets 3, reps 10 (2 exercises) |
| TRISET | 201 | TRISET | sets 3, reps 10 (3 exercises) |
| GEANTE | 201 | GEANTE | sets 3, reps 10 (3 exercises) |
| CIRCUIT | 201 | CIRCUIT | rounds 3, no sets or reps |
| AMRAP | 201 | AMRAP | workSeconds 40, restWorkSeconds 20 |
| EMOM | 201 | EMOM | rounds 10, workSeconds 40, restWorkSeconds 20 |
| FOR_TIME | 201 | FOR_TIME | workSeconds 40, restWorkSeconds 20 |
| INTERVAL | 201 | INTERVAL | rounds 8, workSeconds 40, restWorkSeconds 20 |
| TEMPS_CONTINU | 201 | TEMPS_CONTINU | workSeconds 40, restWorkSeconds 20 |
| DISTANCE | 201 | DISTANCE | exercises and load type only |
| LIBRE | 201 | LIBRE | exercises and load type only |

Legacy alias and invalid values:

| Input | Result |
|---|---|
| mode "STANDARD" with sets and reps | 201, GET returns SERIE_SIMPLE |
| "FOO" | 422, key `Circuits[1].Mode` |
| "standard" (lowercase) | 422, key `Circuits[1].Mode` |
| "biset" (lowercase) | 422, key `Circuits[1].Mode` |
| "" (empty) | 422, key `Circuits[1].Mode` |

Message on the Mode key: `Mode must be one of: SERIE_SIMPLE, BISET, TRISET, GEANTE, CIRCUIT, AMRAP, EMOM, FOR_TIME, INTERVAL, TEMPS_CONTINU, DISTANCE, LIBRE.`

Mode rules:

| Case | Result |
|---|---|
| SERIE_SIMPLE, BISET, TRISET, GEANTE without PrescribedSets | 422, key `Circuits[1].Exercises[1]` (4 cases) |
| SERIE_SIMPLE, BISET, TRISET, GEANTE with WorkSeconds | 422, key `Circuits[1].Exercises[1]` (4 cases) |
| INTERVAL without Rounds | 422, key `Circuits[1].Rounds` |
| INTERVAL with PrescribedSets and PrescribedReps | 422, key `Circuits[1].Exercises[1]` |
| AMRAP, EMOM, FOR_TIME, TEMPS_CONTINU with workSeconds 45 and restWorkSeconds 15, no sets or reps | 201 (4 cases), GET returns 45 and 15 as sent |
| LIBRE, DISTANCE, CIRCUIT with only exercises and load type | 201 (3 cases) |

Total: 34 POST calls, all with the expected code.

### 7.2 Programs

- POST /api/v1/coach/program-templates, `l149a template` (one MICROCYCLE week, one session, circuits BISET, AMRAP, INTERVAL): 201. GET returns modes `BISET,AMRAP,INTERVAL`.
- POST /api/v1/coach/members/{memberId}/training-programs with the templateId (member@mentora.fr, coach coach@mentora.fr): 201. Read-only SQL count of `PROGRAM_CIRCUITS` by mode: AMRAP 1, BISET 1, INTERVAL 1 (the copy keeps the three modes).
- PUT /api/v1/coach/training-programs/{id} by ids, changing circuit 1 to "EMOM" and circuit 2 to the input "STANDARD": 200, response modes `EMOM,SERIE_SIMPLE,INTERVAL`. Coach GET after the PUT: same. Read-only SQL: EMOM 1, INTERVAL 1, SERIE_SIMPLE 1 stored.
- GET /api/v1/member/programs/current as member@mentora.fr: 200, modes `EMOM,SERIE_SIMPLE,INTERVAL`.
- After all proofs, precheck.sql: STANDARD tokens 0 in both jsonb columns (20 session templates: 2 SERIE_SIMPLE tokens, one written directly and one from the STANDARD alias; 1 INTERVAL token; program templates: 28 SERIE_SIMPLE, 5 INTERVAL).

## 8. Contract (6.5): response shapes unchanged

`git diff` of `Mentora.Core/DTOs` has 0 changed lines that are not `///` comments: no response DTO property was added, removed or reordered. Runtime key lists (order as returned):
- Program template response: `programTemplateId,coachId,name,description,goal,durationWeeks,body,isActive,createdDate,updatedDate`; body circuit: `name,position,mode,rounds,restBetweenRoundsSeconds,restSecondsAfterExercices,note,exercises`.
- Training program response (same for POST assignment's `program`, PUT, coach GET, member current): `programId,coachId,memberId,templateId,name,goal,startDate,durationWeeks,weekOffset,status,createdDate,updatedDate,blocks`; circuit: `programCircuitId,name,position,mode,rounds,restBetweenRoundsSeconds,restSecondsAfterExercices,note,exercises`.
- Session template response: `id,name,description,nature,sessionType,isPlatform,sourceCode,circuitCount,exerciseCount,createdAt,updatedAt,circuits,sessionNode`; circuit: `name,position,mode,rounds,restBetweenRoundsSeconds,restSecondsAfterExercices,note,exercises`; exercise: `exerciseId,position,loadType,prescribedSets,prescribedReps,prescribedWeightKg,restSecondsAfterSeries,workSeconds,restWorkSeconds,customNote`.
Only the mode values differ.

## 9. Migration Down refusal, Up proof, Down and Up

### 9.1 Down refused with the fixtures present

`dotnet ef database update 20261123042911_Lot14_8_CoachCreatedAudit` exited 1 with:

`P0001: Cannot revert Lot14_9a_CircuitModes: circuits use a mode other than SERIE_SIMPLE, INTERVAL or STANDARD. Convert or delete them first.`

precheck.sql after the failed command was identical to the run before it (diff empty): the data is unchanged and the migration stays applied.

### 9.2 Up SQL proof on the two locations without local data before the migration

`up-proof-local.sql` (output in `up-proof-local.out.txt`, ends with ROLLBACK). Inside the transaction: one `l149a` circuit set to STANDARD, one `l149a` session template content rewritten so one circuit carries the exact token `"mode": "STANDARD"`, then the three UPDATE statements copied from `Up()`.

| | before Up | after Up |
|---|---|---|
| PROGRAM_CIRCUITS in STANDARD | 1 | 0 |
| session templates with the STANDARD mode token | 1 | 0 |
| session template nature column in STANDARD | 20 | 20 |
| rows with a `"nature": "STANDARD"` token | 0 | 0 |

The two rewritten rows are now SERIE_SIMPLE (circuit mode column, and the jsonb token). The program template UPDATE touched 0 rows (none left in STANDARD). After the ROLLBACK, precheck.sql was identical to the previous run.

### 9.3 Down then Up on the clean database

After the cleanup of section 10, API stopped:
- Down: `down.txt`, exit 0, "Reverting migration '20261124221921_Lot14_9a_CircuitModes'." then "Done."; precheck.sql identical to `before.txt` (diff empty, the 28 STANDARD tokens are back).
- Up: `up.txt`, exit 0, "Applying migration '20261124221921_Lot14_9a_CircuitModes'." then "Done."; precheck.sql identical to `after.txt` (diff empty).

## 10. Cleanup of the fixtures (local database)

`cleanup-local.sql` (dry run, ends with ROLLBACK, output `cleanup-dryrun.out.txt`) and `cleanup-local-commit.sql` (output `cleanup-commit.out.txt`). The two files differ only on the last line (`ROLLBACK;` against `COMMIT;`, checked with diff). Bounds: coach coach@mentora.fr, member member@mentora.fr, names `l149a %` (session templates) and `l149a template` (program template and program), created at or after the run start `2026-11-24 22:24:46.569129+00` read from the database before the first fixture. No program existed before the run, so nothing was archived.

Counts deleted: session templates 20, program templates 1, programs 1, blocks 1, sessions 1, circuits 3, exercises 4. After the commit: no `l149a` row left, 2 program templates left (the seeded platform ones), 0 programs. precheck.sql after the commit was identical to `after.txt` (diff empty), before the Down and Up of 9.3.

## 11. Files in this folder

`precheck.sql`, `before.txt`, `after.txt`, `up-proof-local.sql`, `up-proof-local.out.txt`, `cleanup-local.sql`, `cleanup-dryrun.out.txt`, `cleanup-local-commit.sql`, `cleanup-commit.out.txt`, `down.txt`, `up.txt`, `runtime-evidence.md`.
`precheck.sql` has no local-only assumption and is meant to be run read-only on the VPS before deployment. The cleanup and up-proof files are local only (they name the local fixtures) and must never be run on the VPS.

## 12. Out of scope (not touched)

Welcome-mail change (A1 and A2, postponed), notifications, a new field for DISTANCE, a CHECK constraint on the mode, changes of response shapes, any rule on the number of exercises per mode. No NuGet package added or upgraded.

Process note: three `using Mentora.Core.Programs;` lines were first added with `sed -i` (contrary to the Write and Edit rule). `git diff --stat` and `git diff --ignore-all-space --stat` on the three files were identical and showed only the intended lines, so no line ending was rewritten.
