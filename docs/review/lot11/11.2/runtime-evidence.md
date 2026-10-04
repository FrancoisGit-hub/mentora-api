# Lot 11.2: runtime evidence (local)

Run start (UTC): 2026-10-04T18:12:05Z. Local PostgreSQL 16 (port 5433), API on port 5243 with `Email__Provider=Logging`. Fixture names prefixed `l112-`. No token or OTP is recorded in this file.

## Deviations from the prompt

1. **Migration applied before the file was shown.** Section 5 required showing the full migration file before applying it. The migration was applied by the first API start (`db.Database.Migrate()`), before the file had been presented for approval. François pointed it out: the content was correct and the Down/Up test passed. From now on every irreversible or data-changing step (migration apply, cleanup COMMIT) waits for explicit written approval.
2. **CHECK lists hard-coded in the migration.** The prompt says the validator and the CHECK share one list. The validator uses `ProgramSessionTypes.All` / `ProgramSessionNatures.All` (`Mentora.Core/Enums/ProgramSessionWire.cs`). The migration repeats the literal values: a migration must stay frozen even if the static list changes later.
3. `ProgramSessionResponse` XML: added a `<param name="Nature">` tag to avoid a new CS1573 warning.

## Build

`dotnet build`: 0 errors. 2 warnings (CS1573 in `Coach/MemberConversationMessagesController`, files not changed here). No warning in a changed file.

## 1. Migration applied at startup

`__EFMigrationsHistory`: `20261004181109_Lot11_2_ProgramTreeFields` on top of `20260930085059_Lot10_Quiz`.

| Table | Column | Type | Nullable | Default |
|---|---|---|---|---|
| PROGRAM_BLOCKS | PROGRAM_BLOCK_LOAD_PERCENT | numeric(4,1) | yes | |
| PROGRAM_SESSIONS | PROGRAM_SESSION_NATURE | varchar(20) | no | 'STANDARD' |
| PROGRAM_CIRCUITS | PROGRAM_CIRCUIT_REST_AFTER_SECONDS | integer | yes | |

CHECKs (`pg_constraint`): `CK_PROGRAM_BLOCKS_LOAD_PERCENT`, `CK_PROGRAM_CIRCUITS_REST_AFTER_SECONDS`, `CK_PROGRAM_SESSIONS_AUTONOME_HAS_NO_BOOKING`, `CK_PROGRAM_SESSIONS_NATURE`, `CK_PROGRAM_SESSIONS_TYPE` (plus the two pre-existing `CK_PROGRAM_BLOCKS_*`). `CK_PROGRAM_SESSIONS_A_DISTANCE_HAS_NO_BOOKING` is gone.

Mentora template "Remise en forme — débutant": no `A_DISTANCE`, contains `"AUTONOME"`.

Preflight (before code): 1 template contained `A_DISTANCE`, 4 occurrences, all 4 are `sessions[*].type` values (jsonpath count = text count = 4); no name or note contains it. `PROGRAM_SESSIONS`: 0 rows locally. No name collision.

## 2. Down then Up (API stopped)

| Step | Template body | CHECKs | Columns |
|---|---|---|---|
| `dotnet ef database update 20260930085059_Lot10_Quiz` | `A_DISTANCE` back (a_distance=t, autonome=f) | 3: the two `CK_PROGRAM_BLOCKS_*` + `CK_PROGRAM_SESSIONS_A_DISTANCE_HAS_NO_BOOKING` | 0 of the 3 new |
| `dotnet ef database update` | `AUTONOME` (a_distance=f, autonome=t) | the 7 listed above | 3 of 3 |

## 3. Seeders idempotent

`pg_stat_user_tables` (`n_tup_ins/upd/del`) snapshot before and after a start with the schema current: zero difference on every table.

## 4. HTTP, coach JWT, `POST /api/v1/coach/program-templates`

| Case | Expected | Actual | `errors` key |
|---|---|---|---|
| Valid tree (MESOCYCLE `loadPercent` 102.5, session `nature: EVALUATION`, session without nature, circuits `restAfterSeconds` 120 STANDARD and 0 INTERVAL, types `AUTONOME` and `PRESENTIEL_OU_VISIO`) | 201 | 201 | |
| GET template | all fields returned, absent nature = null | meso 102.5, macro null, s1 `AUTONOME`/`EVALUATION`, s2 `PRESENTIEL_OU_VISIO`/null, rest 120 and 0 | |
| `loadPercent: 103` on MESOCYCLE | 422 | 422 | `Body.Blocks[1].Blocks[1].LoadPercent` |
| `loadPercent: 105` on MICROCYCLE | 422 | 422 | `Body.Blocks[1].Blocks[1].Blocks[1].LoadPercent` |
| `nature: "TEST"` | 422 | 422 | `Body.Blocks[1].Blocks[1].Blocks[1].Sessions[1].Nature` |
| `restAfterSeconds: 601` | 422 | 422 | `...Sessions[1].Circuits[1].RestAfterSeconds` |
| `restAfterSeconds: -1` | 422 | 422 | `...Sessions[1].Circuits[1].RestAfterSeconds` |
| `type: "A_DISTANCE"` | 422 | 422 | `...Sessions[1].Type`: "Type must be one of: PRESENTIEL_SOLO, PRESENTIEL_GROUPE, VISIO, AUTONOME, PRESENTIEL_OU_VISIO." |

(Each key is prefixed `Body.` and positions are 1-based.)

## 5. Assign from template (W3a)

`POST /api/v1/coach/members/{memberId}/training-programs` with `templateId`: 201, `archivedProgramId` empty (the member had no program). `GET /api/v1/coach/training-programs/{id}` and member `GET /api/v1/member/programs/current` both return: meso `loadPercent` 102.5; s1 `AUTONOME`/`EVALUATION`; s2 `PRESENTIEL_OU_VISIO`/`STANDARD` (sent without nature, so the copy turns null into `STANDARD`); `restAfterSeconds` 120 and 0.

## 6. Assign with body (W3b) then PUT

- W3b with `loadPercent: 105`: 201, values returned (105.0, EVALUATION, STANDARD, 120, 0). The W3a program was archived (`archivedProgramId` = the W3a program), as designed.
- `PUT /api/v1/coach/training-programs/{id}` with `loadPercent: 110`, s1 `VISIO`/`STANDARD`, `restAfterSeconds` 300: 200, values returned (110.0, VISIO, STANDARD, 300, 0).

## 7. SQL

Stored values for the two fixture programs (`l112-valid`, `l112-w3b`): `PROGRAM_BLOCK_LOAD_PERCENT` 102.5 and 110.0 on MESOCYCLE rows, null on the others; `PROGRAM_SESSION_TYPE`/`NATURE` as sent (`AUTONOME`/`EVALUATION`, `PRESENTIEL_OU_VISIO`/`STANDARD`, `VISIO`/`STANDARD`); `PROGRAM_CIRCUIT_REST_AFTER_SECONDS` 120, 0, 300, 0.

Direct `UPDATE "PROGRAM_BLOCKS" SET "PROGRAM_BLOCK_LOAD_PERCENT" = 103` inside a transaction: `ERROR: new row for relation "PROGRAM_BLOCKS" violates check constraint "CK_PROGRAM_BLOCKS_LOAD_PERCENT"`, then `ROLLBACK`; 0 rows at 103 afterwards.

## Cleanup

Files: `cleanup-dryrun.sql` (ends with `ROLLBACK`) and `cleanup-commit.sql`; `diff` shows only the last line (`ROLLBACK;` vs `COMMIT;`).
Dry run result: fixture programs 2, fixture templates 1 (before: 2 programs, 3 templates); after delete inside the transaction: programs 0, templates 2, blocks/sessions/circuits/exercises 0 (children cascaded); then `ROLLBACK`.
Previous member program to restore: none (0 programs before the run).
Side effects not covered by the cleanup: `AUTH_OTPS` / refresh-token rows from the two logins.

**COMMIT executed after François's explicit written approval** (`cleanup-commit.sql`): before 2 fixture programs, 2 programs and 3 templates in total; `DELETE 2` (programs, children cascaded), `DELETE 1` (template); after: programs 0, templates 2, blocks/sessions/circuits/exercises 0; `COMMIT`. A follow-up query confirms 0 `l112-` programs remain.
