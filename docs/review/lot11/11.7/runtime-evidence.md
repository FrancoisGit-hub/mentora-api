# Lot 11.7 runtime evidence: rest fields renamed in the JSON seen by clients

Branch `feature/lot_11`, HEAD `ac5eaa3` plus the uncommitted rename. Local PostgreSQL 16 (port 5433), API on port 5243 started with `dotnet run --no-build` and `ASPNETCORE_ENVIRONMENT=Development`, `Email__Provider=Logging` and `EmailRouting__TestRecipients` taken from the shell (never written to a file). OTP only in a shell variable, token only in the gitignored `tokens.json`; neither is recorded here.

Run start date (`SELECT now()` read before the first write): **2026-10-06 09:28:00.636468+00**. Fixture ids: program template `11700000-0000-4000-8000-0000000000a1`, session template `11700000-0000-4000-8000-0000000000a2` (both coach A, prefix `l11rn-`).

Mapping: `restSeconds` becomes `restSecondsAfterSeries`; `restAfterSeconds` becomes `restSecondsAfterExercices`. No column, CHECK or prefixed entity property was renamed (11.AS).

## 1. Build and model

`dotnet build --no-incremental`: 0 errors, 19 warnings (all CS1573, as before). `dotnet ef migrations has-pending-model-changes`: "No changes have been made to the model since the last migration". `Lot11_7_RestFieldsJsonKeys` generated empty by EF (no schema operation), snapshot unchanged, Up and Down filled by hand with `migrationBuilder.Sql` (exact quoted tokens, `WHERE` on the `::text`).

## 2. Pre-apply check (`precheck.sql`, read only, `precheck-output.txt`)

No JSON string value equals one of the four tokens (0 and 0). Before the fixtures: `PROGRAM_TEMPLATES` 2 rows, 88 `restSeconds`, 0 `restAfterSeconds`, new keys 0; `SESSION_TEMPLATES` 0 rows.

## 3. Fixtures with OLD keys (`fixture-dryrun.sql` shown with ROLLBACK first, then `fixture.sql`, diff = last line only; output `fixture-output.txt`)

Both bodies carry `restSeconds` and `restAfterSeconds`, plus `restBetweenRoundsSeconds` on an INTERVAL circuit. Body md5 before: program template `871504be5e94e01b11a3f00d88bd73f4`, session template `43783a7d51555d94512e043ac2f5597c`.

## 4. Migration: Up, Down, Up (`snapshot.sql` after each step, `baseline.txt`, `after-up1.txt`, `after-down.txt`, `after-up2.txt`)

History before: only `Lot11_7` pending. Aggregate md5 = md5 of the string_agg of all body texts ordered by primary key.

| State | Store | Rows | old `restSeconds` / `restAfterSeconds` | new `AfterSeries` / `AfterExercices` | `restBetweenRoundsSeconds` | Aggregate md5 |
|---|---|---|---|---|---|---|
| Baseline | PROGRAM_TEMPLATES | 3 | 91 / 2 | 0 / 0 | 34 | `aa821dc5c728f4ff54c261c347b8ef05` |
| Baseline | SESSION_TEMPLATES | 1 | 3 / 2 | 0 / 0 | 2 | `43783a7d51555d94512e043ac2f5597c` |
| After Up1 | PROGRAM_TEMPLATES | 3 | 0 / 0 | 91 / 2 | 34 | `cec763eb57376f38c308a0461a8018c8` |
| After Up1 | SESSION_TEMPLATES | 1 | 0 / 0 | 3 / 2 | 2 | `283234ec3d68144bdc4079eb76ec7daa` |
| After Down | PROGRAM_TEMPLATES | 3 | 91 / 2 | 0 / 0 | 34 | `aa821dc5c728f4ff54c261c347b8ef05` (= baseline) |
| After Down | SESSION_TEMPLATES | 1 | 3 / 2 | 0 / 0 | 2 | `43783a7d51555d94512e043ac2f5597c` (= baseline) |
| After Up2 | PROGRAM_TEMPLATES | 3 | 0 / 0 | 91 / 2 | 34 | `cec763eb57376f38c308a0461a8018c8` (= Up1) |
| After Up2 | SESSION_TEMPLATES | 1 | 0 / 0 | 3 / 2 | 2 | `283234ec3d68144bdc4079eb76ec7daa` (= Up1) |

Fixture body md5: program template `871504be…` (before, and after Down) then `ec1cde4f025cee9d91996ccee800cdd9` (after Up1 and Up2); session template `43783a7d…` then `283234ec…`. Only the two keys changed: the counts of `restBetweenRoundsSeconds` and the row counts did not move.

Seeded template "Prise de masse - débutant": 52 exercise rest values read under `restSeconds` at baseline (`[90, 90, 60, 90, 120, 90, 90, 60, 60, 60, 60, 45, 45, ...]`), read under `restSecondsAfterSeries` after Up1 and Up2 they are the same 52 values in the same order; empty under the other key in each state. History after: `Lot11_7`, `Lot11_5`, `Lot11_3`, `Lot11_2` applied (database left in the Up state).

## 5. API start and seeder idempotence

First start: `[EMAIL] routing mode=TEST provider=Logging testRecipients=4 productionBcc=0`, 0 `fail:`/`crit:`. API stopped (port 5243 free), started a second time, same startup line, 0 `fail:`/`crit:`. `snapshot.sql` after the second start: `PROGRAM_TEMPLATES` 3 rows, aggregate md5 `cec763eb57376f38c308a0461a8018c8`, identical to after Up2; `SESSION_TEMPLATES` 1 row, `283234ec…`. The seeders did not touch the migrated rows.

## 6. HTTP (real coach JWT from an OTP login, member `0d5608a9-...`)

Pre-check: the member had no program of any status, so the assign archived nothing from before. All commands ran through `proofs.js` with `put.js` (copied from `review-wave1-fixes`, renamed fields). Property names below are the unique names containing "rest" in each response; "old names in raw response" is a search for the quoted tokens `"restSeconds"` and `"restAfterSeconds"` in the raw response text.

| # | Case | Result |
|---|---|---|
| A | POST assign, seeded template "Prise de masse" (program `be2f1e45-7179-42f9-918a-f4a708b25803`) | 201, `archivedProgramId` null. Rest names: `restBetweenRoundsSeconds, restSecondsAfterExercices, restSecondsAfterSeries, restWorkSeconds`. Old names: none. Exercise rest values: template body (52 values, sorted) equal to `PROGRAM_EXERCISE_REST_SECONDS` (52 values, sorted). Circuit rest: the seeded template carries none (0 keys), the 16 circuit columns are null |
| B | POST assign, fixture template a1 (program `ea26c6c2-8003-4e18-9fa6-4ef083c53f0e`) | 201, `archivedProgramId` = program A. Same four rest names, old names none. Exercise rest: template `[45,90,null]` = column `[45,90,null]`. Circuit rest: template `[60,120]` = `PROGRAM_CIRCUIT_REST_AFTER_SECONDS` `[60,120]` |
| C | GET program B as the coach | 200. Rest property names: `restBetweenRoundsSeconds, restSecondsAfterExercices, restSecondsAfterSeries, restWorkSeconds`. Old names: none |
| D | PUT with the new names (first exercise 77, first circuit 88) | 200. Response names as above, old names none. Columns before: exercise `[45,90,null]`, circuit `[60,120]`; after: exercise `[77,90,null]`, circuit `[88,120]` |
| E | PUT with the OLD names only (`restSeconds` 55, `restAfterSeconds` 99), new names omitted | 200 (unknown members ignored, no error). The old values are not applied: 55 and 99 not stored. The omitted new names are stored as null, because a PUT replaces the whole tree: exercise `[90,null,null]` (the 77 became null), circuit `[120,null]` (the 88 became null) |
| F | PUT with `restSecondsAfterExercices` = 601 | 422. Error key `Body.Blocks[1].Blocks[1].Blocks[1].Sessions[1].Circuits[1].RestSecondsAfterExercices`, message "RestSecondsAfterExercices must be between 0 and 600." Nothing written (circuit column unchanged) |
| G | POST session template with the new names, then GET (id `bf47bcdc-7071-4e41-ad2d-77074a9f5b45`) | 201 then 200. Rest names: four names above, old names none. Stored content: 1 `restSecondsAfterSeries`, 1 `restSecondsAfterExercices`, 0 old key |
| H | GET the migrated fixture session template a2 | 200. Same four rest names, old names none |

Case E is the accepted behaviour (11.AR, no transition): a client still sending the old names does not get an error, its values are ignored, and the PUT then stores null for the two renamed fields. PJ must switch to the new names before it writes programs again.

## 7. API log

0 `fail:` or `crit:` line, 0 `NullReferenceException`, 0 unhandled exception over both starts and the whole HTTP run.

## 8. Created by this run (for the cleanup)

Fixture rows `PROGRAM_TEMPLATES` a1 and `SESSION_TEMPLATES` a2; programs `be2f1e45-7179-42f9-918a-f4a708b25803` (archived) and `ea26c6c2-8003-4e18-9fa6-4ef083c53f0e` with their blocks, sessions, circuits and exercises; session template `bf47bcdc-7071-4e41-ad2d-77074a9f5b45`; the `AUTH_OTPS` and `AUTH_REFRESH_TOKENS` rows of the coach login.

## 9. Cleanup (`cleanup-dryrun.sql` shown with ROLLBACK, `cleanup-commit.sql`, diff = last line only; output `cleanup-output.txt`)

Bounded by the ids above, coach A and the run start date. Deleted: 2 `PROGRAMS`, 9 `PROGRAM_BLOCKS`, 13 `PROGRAM_SESSIONS`, 18 `PROGRAM_CIRCUITS`, 55 `PROGRAM_EXERCISES`, 2 `SESSION_TEMPLATES` (the HTTP test one and fixture a2), 1 `PROGRAM_TEMPLATES` (fixture a1), 1 `AUTH_OTPS`, 1 `AUTH_REFRESH_TOKENS`. The two seeded program templates and `__EFMigrationsHistory` were not touched.

Correction found by the first dry run: program A kept the template name ("Prise de masse - débutant") because `name` is ignored when `templateId` is given, so a `PROGRAM_NAME LIKE 'l11rn-%'` guard excluded it (only 1 of 2 programs would have been deleted). The guard was removed from the `PROGRAMS` delete only; that delete stays bounded by the two program ids, coach A and the run start date.

After the COMMIT: leftover scan over the 31 tables with a `*_CREATED_DATE` column, 0 rows created on or after the run start; `PROGRAM_TEMPLATES` 2 rows, `SESSION_TEMPLATES` 0, `PROGRAMS` 0; last 4 rows of `__EFMigrationsHistory` end with `Lot11_7_RestFieldsJsonKeys` (still applied). The API was stopped (port 5243 free). `tokens.json` and the two API output capture files (which held the one-time code and e-mail log lines) were deleted by exact path; `Test-Path` returned False for each.
