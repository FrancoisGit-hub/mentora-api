# Lot 11.2c: runtime evidence (local)

Run start (UTC, `SELECT now()`): **2026-10-05T20:51:03.170555Z** (`run-start.txt`, recorded before any fixture write). Local PostgreSQL 16 (port 5433), API on port 5243 started with `ASPNETCORE_ENVIRONMENT=Development`, `Email__Provider=Logging`, and `Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command=Information` on the command line (no appsettings change, no sensitive data logging). The raw log stayed in the gitignored `api.log`; only statement text without parameter values is quoted below. Fixtures prefixed `l11c-`. Tokens were kept in the gitignored `tokens.json`, OTP codes only in a variable; neither is recorded here.

## Build

`dotnet build`: 0 errors, 19 warnings before and after the change, identical list (compared line by line), none on a touched file.

## Startup

`dotnet ef migrations list` (Development): last `20261004181109_Lot11_2_ProgramTreeFields`, nothing pending. Startup log filtered on `Applying migration|Seeder|Now listening|fail:|crit:|Exception`: no migration applied, seeders all `created 0`, `Now listening on: http://localhost:5243`, no `fail:` / `crit:` / exception line.

## Schema check (read only)

Unique indexes / constraints on `PROGRAM_BLOCKS`, `PROGRAM_SESSIONS`, `PROGRAM_CIRCUITS`, `PROGRAM_EXERCISES`: the four primary keys and `IX_PROGRAM_SESSIONS_BOOKING_MEMBER` (`SESSION_ID, MEMBER_ID`, partial). None covers (parent, position), so swapping positions in one PUT cannot violate a unique rule.

## Fixtures

All through HTTP except one approved SQL UPDATE.
- Product `l11c-visio` (VISIO, published), voucher grant of 4 (order `d928160b-...`), 4 VISIO slots (`offerType: "Visio"`), days +3 to +6.
- P1 `l11c-completed` (`1d2e08ab-...`), P2 `l11c-archived` (`1bc6ca0c-...`), P3 `l11c-main` (`851b246e-...`); each assignment archived the previous one. `fixture-completed-dryrun.sql` (ROLLBACK) then `fixture-completed-commit.sql` (diff: last line only): P1 ARCHIVED -> COMPLETED, `UPDATE 1`.
- P3 tree: MACROCYCLE > MESOCYCLE (100) > W1 [s1 VISIO, s2 PRESENTIEL_SOLO, s3 PRESENTIEL_SOLO (2 exercises), s4 AUTONOME, s5 PRESENTIEL_OU_VISIO], W2 [s6 VISIO].
- Member reservations (auto link): B1 -> s1, B2 -> s5, B3 -> s6; B3 cancelled by the member (`CANCELLED`); B4 reserved afterwards, no candidate (s6 still holds the dead link), unlinked.
- Completion (coach endpoint): s2 `DONE`; s3 `PLANNED` with actuals on its 2nd exercise `7e844008-...` (3 x 8, 22.5 kg, RPE 8).

## HTTP evidence

Ids: s1 `13f83205`, s2 `5933793a`, s3 `0d11bc67`, s4 `2c2189f0`, s5 `9c338248`, s6 `71a5bbb2`, s7 `1e832902` (new in case 2); bookings B1 `b6363782`, B2 `55b13a59`, B3 `be65a5b9` (cancelled), B4 `ec6e712d`.

| # | Call | Expected | Actual | Reason / keys |
|---|---|---|---|---|
| 1 | `GET /api/v1/coach/training-programs/{P3}` | 200 | 200 | ids kept for the next calls |
| 2 | `PUT .../training-programs/{P3}` echoing every id; s1 exercise reps 10 -> 12; s2/s3 swapped; s4 moved W1 -> W2; new session s7 and new exercise in s1 (no id) | 200 | 200 | see SQL 2 |
| 3 | PUT omitting s1 (linked to B1) | 409 | 409 | `PROGRAM_HAS_HISTORY`, programSessionIds [s1], programExerciseIds [] |
| 3b | PUT moving s2 (DONE) to W2 | 409 | 409 | `PROGRAM_HAS_HISTORY`, programSessionIds [s2] |
| 4 | PUT omitting s2 (DONE) | 409 | 409 | `PROGRAM_HAS_HISTORY`, programSessionIds [s2] |
| 5 | PUT omitting s3's 2nd exercise (actuals), s3 kept | 409 | 409 | `PROGRAM_HAS_HISTORY`, programSessionIds [], programExerciseIds [`7e844008-...`] |
| 6 | PUT omitting s4 (no history) | 200 | 200 | row deleted (SQL 6) |
| 7a | PUT s1 (VISIO booking) -> PRESENTIEL_GROUPE | 409 | 409 | `PROGRAM_SESSION_TYPE_INCOMPATIBLE`, conflicts [{programSessionId s1, sessionId B1}] |
| 7b | PUT s1 -> AUTONOME | 409 | 409 | same, conflicts [{s1, B1}] |
| 7c | PUT s1 -> PRESENTIEL_OU_VISIO | 200 | 200 | booking unchanged (SQL 7c) |
| 8a | PUT with an unknown id on s4 | 422 | 422 | `Body.Blocks[1].Blocks[1].Blocks[2].Sessions[2].Id`: "Id 00000000-...-0000000000c1 is not a session of this program." |
| 8b | PUT with a P2 session id on s4 | 422 same message | 422 | same key: "Id a14fa05a-... is not a session of this program." |
| 8c | PUT with s5's id also on s4 | 422 | 422 | same key: "Id 9c338248-... appears more than once." |
| 8d | (superseded) s1's id on s4's circuit | 422 wrong kind | 422 | hit the duplicate rule first, since s1 still carried its own id: "appears more than once" |
| 8d' | s4's session id only on s4's circuit (s4 itself without id) | 422 | 422 | `...Sessions[2].Circuits[1].Id`: "Id 2c2189f0-... is not a circuit of this program." |
| 8e | PUT with `loadPercent: 105` on a MICROCYCLE | 422 | 422 | `Body.Blocks[1].Blocks[1].Blocks[1].LoadPercent` |
| 9a | (superseded, first run) PUT archived P2 | 409 | 409 | `PROGRAM_NOT_EDITABLE`, programStatus ARCHIVED |
| 9a (rerun) | GET P2 (200), PUT P2's own tree back to P2 | 409 | 409 | `PROGRAM_NOT_EDITABLE`, programStatus ARCHIVED; P2 fingerprint identical before and after |
| 9b | GET P1, PUT P1's own tree back to P1 (COMPLETED) | 409 | 409 | `PROGRAM_NOT_EDITABLE`, programStatus COMPLETED |
| 10a | PUT P3 without any id (old app behaviour) | 409 | 409 | `PROGRAM_HAS_HISTORY`, programSessionIds [s1, s2, s5], programExerciseIds [`7e844008-...`]; s6 (dead link to cancelled B3) not listed (11.AJ) |
| 10b | Assign P4 `l11c-nohistory` (201, archives P3), PUT P4 without any id | 200 | 200 | rows replaced (SQL 10b) |
| 11a | `PUT /api/v1/coach/program-sessions/{s5}/booking` B4 (s5 linked to B2) | 409 | 409 | `PROGRAM_SESSION_ALREADY_LINKED`, programSessionId s5, currentSessionId B2 |
| 11b | same, B2 (same booking) | 200, no write | 200 | `xmin` of s5 1875 before 11a and after 11b |
| 11c-null | same, `sessionId: null` | 200 detached | 200 | sessionId null |
| 11d | same, B4 | 200 | 200 | s5 -> B4 |
| 11.AJ(b) | `.../program-sessions/{s6}/booking` B2, s6 linked to cancelled B3 | 200 | 200 | dead link replaced, no `PROGRAM_SESSION_ALREADY_LINKED` |
| 11c | setup: s6 detached (200), relinked by hand to cancelled B3 (200); then tree PUT s6 VISIO -> AUTONOME | 200 | 200 | s6 SESSION_ID null, B3 row unchanged (SQL 11c) |
| 2b | tree PUT: W2 omitted, new MICROCYCLE `l11c-w2-new` (no id, week 2) holding s6 and s7 (ids kept, no history) | 200 | 200 | SQL 2b |
| 12a | PUT P3 without `Authorization` | 401 | 401 | |
| 12b | PUT P3 with the member token | 403 | 403 | |

Note on the error batch: a first attempt failed locally before any request (a script variable `$base` shadowed the helper's `$Base` URL, PowerShell names are case insensitive); curl never reached the API. The batch above is the rerun.

11.AJ: of the two options, (b) was run (fewest fixtures: the cancelled B3 is reused for 11c by a manual relink). (a) was not run separately; 10a shows that a session with a dead link is not history.

### 422 key format (8e) against the 11.2 reference

| Source | Key |
|---|---|
| 11.2c, `PUT /api/v1/coach/training-programs/{id}`, loadPercent 105 on MICROCYCLE | `Body.Blocks[1].Blocks[1].Blocks[1].LoadPercent` |
| 11.2 evidence, `POST /api/v1/coach/program-templates`, same violation | `Body.Blocks[1].Blocks[1].Blocks[1].LoadPercent` |

Identical path format (indexes are block positions, 1-based).

## SQL proofs (read only, `BEGIN TRANSACTION READ ONLY ... ROLLBACK`)

- **2.** Kept session ids identical (s1 `13f83205`, s2 `5933793a`, s3 `0d11bc67`, s4 `2c2189f0`, s5 `9c338248`, s6 `71a5bbb2`); W1 positions s1 1, s3 2, s2 3, s5 4; s4 now under W2 (`c1d3bf84`) at position 2; s1 still -> B1 `SCHEDULED`, s5 -> B2, s6 -> B3 `CANCELLED`; s2 `DONE` with completed date; s3 exercise `7e844008` actuals 3 / 8 / 22.50 / 8 unchanged; s1 exercise `a0b2aa49` prescribed reps 12; new rows s7 `1e832902` (exercise `adeb236e`) and s1 exercise `815a4d88`. Blocks: 4.
- **Error batch (3 to 12b).** md5 fingerprint of the member's `PROGRAMS` rows, P3's 4 tree tables and the member's `SESSIONS` rows, before and after the batch: identical (`d5563615... / a23ff590... / 66becd47... / f927a35b... / 1420acb2... / 7d9eabf7...`). P2 fingerprint before and after the 9a rerun: identical (`b9e20ff5... / 3e4cbb60... / 089be5e1... / da90f456... / 54be85c6...`).
- **6.** 0 rows for s4 and for its exercise `093452ef`.
- **7c.** s1 `PRESENTIEL_OU_VISIO` still -> B1; B1 `SESSION_OFFER_TYPE` VISIO, `SCHEDULED`, `SESSION_UPDATED_DATE` = creation (`2026-10-05 22:00:39.838879+00`).
- **11.** s5 `xmin` 1875 before 11a and after 11b; s5 -> B4 after 11d.
- **11c.** Before: s6 VISIO -> B3; B3 row md5 `096600f1...`, `CANCELLED`, VISIO, updated `2026-10-05 22:00:40.35187+00`. After: s6 AUTONOME, SESSION_ID null; B3 row md5 identical.
- **2b.** Old W2 block `c1d3bf84` 0 rows; s6 and s7 (same ids) under `l11c-w2-new` `06be64c3`, week 2. One SaveChanges inserted the new block, updated both sessions and deleted the old block without an FK error: EF ordered the updates before the delete.
- **10b.** Old P4 session ids 0 rows, 2 new sessions.

## SQL generated by one PUT (case 2)

15 commands in total, from the EF command log (`Executed DbCommand`), no per-node SELECT:

1. `SELECT ... FROM "PROGRAMS" WHERE "PROGRAM_ID" = @__programId_0 AND "PROGRAM_COACH_ID" = @__coachId_1 LIMIT 1`
2. `SELECT e."EXERCISE_ID" FROM "EXERCISES" WHERE "EXERCISE_ID" = ANY (@__exerciseIds_0) AND (coach visibility) AND "EXERCISE_IS_ACTIVE"` (existing tree validator)
3. to 6. `SELECT ... FROM "PROGRAM_BLOCKS" | "PROGRAM_SESSIONS" | "PROGRAM_CIRCUITS" | "PROGRAM_EXERCISES" WHERE "..._PROGRAM_ID" = @__programId_0` (whole tree, one query per table)
7. `SELECT "SESSION_ID", "SESSION_OFFER_TYPE", "SESSION_STATUS" FROM "SESSIONS" WHERE "SESSION_ID" = ANY (@__linkedBookingIds_0)`
8. One batched write: `UPDATE "PROGRAMS" SET "PROGRAM_UPDATED_DATE"`, `UPDATE "PROGRAM_EXERCISES" SET "PROGRAM_EXERCISE_PRESCRIBED_REPS"`, `INSERT INTO "PROGRAM_EXERCISES"`, 4 x `UPDATE "PROGRAM_SESSIONS" SET "PROGRAM_SESSION_POSITION"` (one also `"PROGRAM_SESSION_BLOCK_ID"`), `INSERT INTO "PROGRAM_SESSIONS"`, `INSERT INTO "PROGRAM_CIRCUITS"`, `INSERT INTO "PROGRAM_EXERCISES"`. Only changed columns are updated.
9. to 15. Response (`BuildResponseAsync`, unchanged): 4 tree SELECTs, exercises, linked sessions' dates, last performed (one LATERAL query).

## Cleanup

`cleanup-dryrun.sql` (ROLLBACK) then `cleanup-commit.sql` (written with the Write tool; `git diff --no-index --ignore-cr-at-eol`: last line only). Bounded by fixture ids, `l11c-` names, coach A and `*_CREATED_DATE >= run start`. Counts (dry run and COMMIT identical):

| Table | Before | After |
|---|---|---|
| SESSIONS | 4 | 0 |
| SESSION_VOUCHERS | 4 | 0 |
| ORDERS / ORDER_ITEMS | 1 / 1 | 0 / 0 |
| SESSION_SLOTS | 4 | 0 |
| PROGRAMS | 4 | 0 |
| PROGRAM_BLOCKS / PROGRAM_SESSIONS / PROGRAM_CIRCUITS / PROGRAM_EXERCISES | 10 / 10 / 10 / 12 | 0 / 0 / 0 / 0 |
| PRODUCTS | 1 | 0 |

**COMMIT executed after François's explicit approval.**

### Residual check (read only)

30 `*_CREATED_DATE` columns scanned for rows `>= 2026-10-05 20:51:03.170555+00`: only `AUTH_OTPS` and `AUTH_REFRESH_TOKENS`. Tree tables and `ORDER_ITEMS` for the fixture program and order ids: 0 rows.

Known leftovers (not deleted):

| Table | coach@mentora.fr | member@mentora.fr |
|---|---|---|
| AUTH_OTPS | 1 | 1 |
| AUTH_REFRESH_TOKENS | 1 | 1 |

## Deviations

1. Dead link clearing (approved): a kept session linked to a CANCELLED booking whose new type no longer fits the booking's offer type loses the dead link (required for AUTONOME by `CK_PROGRAM_SESSIONS_AUTONOME_HAS_NO_BOOKING`); proven by 11c.
2. "Same booking, no write" proven with `xmin` instead of an updated date (`PROGRAM_SESSIONS` has none), approved.
3. `PROGRAM_SESSION_TYPE_INCOMPATIBLE` details carry a `conflicts` list of `{programSessionId, sessionId}` (several sessions may conflict).
4. 8d and the first 9a are superseded by 8d' and the 9a rerun (see table).

## Backlog

1. The manual booking link (`PUT /api/v1/coach/program-sessions/{id}/booking`) accepts a CANCELLED booking (s6 linked by hand to cancelled B3 returned 200). Behaviour older than this lot; to refuse later.
2. No code path sets a program to `COMPLETED` (only the mapping helpers know the value); the fixture needed SQL.
3. One active program per member is enforced across all coaches (`AssignAsync` archives the member's ACTIVE program whatever its coach); check against the multi-coach V1 decision at the marketplace lot.
4. Reordering or renumbering microcycles may move a DONE session to another week (its computed date changes); accepted V1.
5. Concurrent auto link (`SessionService.LinkToProgramSessionAsync`) racing a PUT on the same program: no new lock; accepted V1.
