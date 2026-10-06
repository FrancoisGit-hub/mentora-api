# Lot 11.5 — Session template library: runtime evidence

Local DB (docker `mentora-postgres-local`, port 5433), API on 5243 started with `ASPNETCORE_ENVIRONMENT=Development`, `Email__Provider=Logging`, `EmailRouting__TestRecipients` (4 addresses), session variables only. Run start (`run-start.txt`): `2026-10-06 07:12:31.789409+00`.
Log rule: no JWT, refresh token, OTP code, OTP console line or email-log line is reproduced here. Tokens were obtained by OTP login (code read from the local log by a script, never printed): coach A `coach@mentora.fr` (isCoach true), coach B `l115-coachb@mentora.fr` (fixture, isCoach true), member `member@mentora.fr` (isCoach false).

## 6.2 Migration

`dotnet ef database update` applied `20261006065717_Lot11_5_SessionTemplates`. Applied list tail: `Lot11_5_SessionTemplates`, `Lot11_3_CoachSessionPreferences`, `Lot11_2_ProgramTreeFields`, `Lot10_Quiz`.
`\d "SESSION_TEMPLATES"`: 11 columns (id uuid `gen_random_uuid()`, `COACH_ID` uuid null, source code varchar(50) null, name varchar(100), description varchar(1000) null, nature varchar(20), session type varchar(40), content jsonb, `IS_ACTIVE` boolean default true, created/updated timestamptz). Indexes: `PK_SESSION_TEMPLATES`, `IX_SESSION_TEMPLATES_COACH_ID`, `UX_SESSION_TEMPLATES_SOURCE_CODE` (UNIQUE, `WHERE "COACH_ID" IS NULL`). Checks: `CK_SESSION_TEMPLATES_SOURCE_CODE`, `_NATURE`, `_SESSION_TYPE`, `_CONTENT`. FK `FK_SESSION_TEMPLATES_COACHES_COACH_ID` ON DELETE RESTRICT.

## 6.3 CHECK proofs (each in its own transaction, ROLLBACK)

| Case | Result |
|---|---|
| platform row without source code | violates `CK_SESSION_TEMPLATES_SOURCE_CODE` |
| coach row with source code | violates `CK_SESSION_TEMPLATES_SOURCE_CODE` |
| nature `BAD` | violates `CK_SESSION_TEMPLATES_NATURE` |
| session type `VISIO_GROUPE` | violates `CK_SESSION_TEMPLATES_SESSION_TYPE` |
| content `[]` | violates `CK_SESSION_TEMPLATES_CONTENT` |
| two platform rows with the same source code (same tx) | 1st INSERT ok, 2nd: duplicate key violates `UX_SESSION_TEMPLATES_SOURCE_CODE` |

`SELECT count(*) FROM "SESSION_TEMPLATES"` after: 0.

## 6.4 Fixtures (`fixture.sql`, one transaction, COMMIT)

Created: 1 user + 1 coach (coach B, `l115-coachb@mentora.fr`, no password), 1 exercise owned by coach B, 3 platform templates (`l115-BRAS` "l115-Séance épaules", `l115-JAMBES` "l115-Séance jambes", `l115-OFF` inactive). Counts after: users 1, coaches 1, exercises 1, templates 3 (2 active).
After starting the API: `COACH_PARAMETERS` rows for coach B = 1 (created by the startup seeder). Coach B JWT: OTP login of the fixture account (Logging provider), same mechanism as the earlier lots.

## 6.5 HTTP proofs (status and trimmed bodies)

1. **Auth.** GET list without token: 401. With member token: 403.
2. **POST.** Valid template with a Mentora exercise: 201 (`isPlatform` false, `sourceCode` null, 1 circuit, 1 exercise). Coach A owns no exercise of his own, so only the Mentora exercise was tested positively.
   - coach B's exerciseId: 422 `Circuits[1].Exercises[1].ExerciseId: Unknown or inaccessible exercise id.`
   - random unknown uuid: 422, same key and same message.
   - no circuit: 422 `Circuits: At least one circuit is required.`
   - name of 105 chars: 422 `Name: Name is required and must be at most 100 characters.`
   - nature `BAD`: 422 `Nature: Nature must be one of: STANDARD, EVALUATION.`
   - sessionType `VISIO_GROUPE`: 422 `SessionType: SessionType must be one of: PRESENTIEL_SOLO, PRESENTIEL_GROUPE, VISIO, AUTONOME, PRESENTIEL_OU_VISIO.`
   - STANDARD circuit without sets: 422 `Circuits[1].Exercises[1]: STANDARD circuits require PrescribedSets and PrescribedReps on every exercise.` (keys use the 1-based Position, as in the program tree).
3. **List (coach A).**
   - `scope` default (all): "l115-A own", "l115-Séance épaules" (platform, `l115-BRAS`), "l115-Séance jambes" (platform, `l115-JAMBES`). Never `l115-OFF`, never coach B's.
   - `scope=MINE` (case insensitive): own only. `scope=platform`: the two active platform templates. `scope=zzz`: 400.
   - `search=epaules` (no accent): finds "l115-Séance épaules".
   - `nature=EVALUATION&sessionType=AUTONOME`: "l115-Séance jambes".
   - Coach B list: the two platform templates only (does not see coach A's).
   - Pagination `limit=1`: 3 pages (items 1, 1, 1; `nextCursor` true, true, false), 3 distinct ids, same ids and order as the unpaged list: no duplicate, no gap.
   - **ONE SQL query per page.** For a single `GET ...?scope=all&search=seance&limit=2` request the EF command log gained exactly **1** `Executed DbCommand` line. The single SELECT (accent-map literal shown garbled only because the console re-decoded the UTF-8 log):
     ```sql
     SELECT s."SESSION_TEMPLATE_ID", s."COACH_ID", s."SESSION_TEMPLATE_CONTENT", ..., s."SESSION_TEMPLATE_UPDATED_DATE",
            lower(translate(s."SESSION_TEMPLATE_NAME", '<accent map>', 'aaaAAAeeeeEEEEiiIIooOOuuuUUUyYcC')) AS "SortKey"
     FROM "SESSION_TEMPLATES" AS s
     WHERE (s."COACH_ID" = @__coachId_0 OR (s."COACH_ID" IS NULL AND s."SESSION_TEMPLATE_IS_ACTIVE"))
       AND lower(translate(s."SESSION_TEMPLATE_NAME", ...)) LIKE @__p_2 ESCAPE '\'
     ORDER BY lower(translate(s."SESSION_TEMPLATE_NAME", ...)), s."SESSION_TEMPLATE_ID"
     LIMIT @__p_3
     ```
     A GET by id also issues exactly 1 command.
4. **GET.** Own: 200, 1 circuit, `sessionNode` keys `name,type,nature,dayOfWeek,position,circuits`, no `id`/`sessionId` at any level checked (node and first circuit keys: `name,position,mode,rounds,restBetweenRoundsSeconds,restAfterSeconds,note,exercises`). Platform `l115-BRAS`: 200, `isPlatform` true, `sourceCode` `l115-BRAS`, 1 circuit, 2 exercises, node `type=PRESENTIEL_SOLO nature=STANDARD dayOfWeek=1 position=1`. `l115-OFF`: 404. Coach B on coach A's template: 404. Unknown id: 404.
5. **PUT.** Own, full replace (name, nature EVALUATION, type AUTONOME, 2 exercises): 200, re-GET shows 2 exercises, `updatedAt` > `createdAt`. Platform `l115-BRAS`: 404, also with an invalid body (ownership wins). Coach B PUT and DELETE on coach A's template: 404; coach A still reads it (200).
6. **Duplicate.** Platform `l115-BRAS`: 201, name "l115-Séance épaules (copie)", `isPlatform` false, `sourceCode` null, 2 exercises, new id. A template with a 100-char name duplicated: copy name 100 chars, ends with " (copie)". `l115-OFF`: 404. Coach B on coach A's template: 404.
7. **DELETE.** Own: 204, then GET 404. Platform `l115-BRAS`: 404. The three extra own templates created during the run were deleted (204 each).
8. **Insertable node.** The `sessionNode` returned by GET of `l115-BRAS`, placed unchanged in a MICROCYCLE block (week 1) of a NEW program template of coach A (`POST /api/v1/coach/program-templates`, goal GENERAL_FITNESS, 1 week): **201**, first session name "l115-Séance épaules". No existing program of coach A was modified. This new program template is a fixture, removed by the cleanup.

## 6.6 Cleanup (dry run executed, COMMIT not executed)

API stopped, port 5243 free (no listener). Rows created after the run start (`scan.sql`): AUTH_OTPS 3, AUTH_REFRESH_TOKENS 3, COACHES 1, COACH_PARAMETERS 1, EXERCISES 1, OFFER_PROGRAMS 4, PROGRAM_TEMPLATES 1, SESSION_TEMPLATES 3, USERS 1.
`cleanup-dryrun.sql` (ends with ROLLBACK), counts before the deletes: AUTH_OTPS (l115 user) 1, AUTH_REFRESH_TOKENS (l115 user) 1, COACH_PARAMETERS (coach B) 1, OFFER_PROGRAMS (coach B) 4, EXERCISES 1, SESSION_TEMPLATES fixtures 3, SESSION_TEMPLATES coach A after run start 0 (the API-created ones were already deleted by proof 7), PROGRAM_TEMPLATES coach A after run start 1, COACHES 1, USERS 1. Deleted per statement: 3, 0, 1, 1, 4, 1, 1, 1, 1, 1. State inside the transaction afterwards: 0 session templates, 0 l115 users, 0 l115 exercises, 0 l115 program templates. After the ROLLBACK the rows are still present (3 templates, 1 l115 user).
`cleanup-commit.sql` was written separately; `diff --strip-trailing-cr cleanup-dryrun.sql cleanup-commit.sql` shows only line 40: `ROLLBACK;` -> `COMMIT;`.
AUTH_OTPS / AUTH_REFRESH_TOKENS of coach A and of the member stay (accounts used).

`cleanup-commit.sql` was executed (COMMIT) after approval: same counts and per-statement deletions as the dry run (3, 0, 1, 1, 4, 1, 1, 1, 1, 1), and 0 session templates / l115 users / l115 exercises / l115 program templates afterwards.

## 6.7 Scan after the COMMIT

`scan.sql` (every table with a `*_CREATED_DATE` column, rows created after `2026-10-06 07:12:31.789409+00`): only `AUTH_OTPS` 2 and `AUTH_REFRESH_TOKENS` 2 remain, one of each for `coach@mentora.fr` and for `member@mentora.fr`. Nothing else.

## 6.8 Hygiene

Scratchpad logs, token files and scripts deleted (Test-Path False for each). No stray file at the repo root. The two greps on `docs/review/lot11/11.5` (OTP console line, three-segment JWT) find nothing.
