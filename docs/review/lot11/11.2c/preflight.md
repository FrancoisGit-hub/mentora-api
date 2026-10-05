# Lot 11.2c: preflight (read only)

HEAD `327dad4`, branch `feature/lot_11`, working tree clean at start. Code read only; database read through one psql run of SELECTs inside `BEGIN TRANSACTION READ ONLY ... ROLLBACK` (DB time `2026-10-05 19:00:40.403871+00`). Nothing written.

## 3.1 Route, controller, DTOs

- `PUT /api/v1/coach/training-programs/{programId}`: `CoachProgramsController.Update` (`Mentora.API/Controllers/Coach/CoachProgramsController.cs:86-98`), policy `CoachOnly` (class level, `:17`), calls `ProgramService.UpdateAsync` (`Mentora.Infrastructure/Services/ProgramService.cs:159-211`).
- Request: `UpdateProgramRequest(Name, Goal, StartDate, DurationWeeks, ProgramTemplateBody Body)` (`Mentora.Core/DTOs/Program/UpdateProgramRequest.cs:6-12`). **The tree nodes are the shared template body classes** `ProgramTemplateBody` / `ProgramTemplateBlock` / `ProgramTemplateSession` / `ProgramTemplateCircuit` / `ProgramTemplateExercise` (`Mentora.Core/DTOs/ProgramTemplate/Body/*.cs`), the same classes used by the template endpoints (serialized into `PROGRAM_TEMPLATE_BODY` jsonb, `ProgramTemplateService.cs:77,105`) and by `POST .../training-programs` (`AssignProgramRequest.Body`). See decision D1.
- Validator: `UpdateProgramRequestValidator` (`Mentora.Core/Validators/Program/UpdateProgramRequestValidator.cs`) delegates the tree to `ProgramTemplateBodyValidator` (`Mentora.Core/Validators/ProgramTemplate/ProgramTemplateBodyValidator.cs`).
- Response: `ProgramResponse` built by `BuildResponseAsync` / `AssembleTree` (`ProgramService.cs:592-676`), nodes `ProgramBlockResponse(ProgramBlockId, ParentId, ...)`, `ProgramSessionResponse(ProgramSessionId, ..., SessionId, Circuits)` (`ProgramSessionResponse.cs:11-25`, `SessionId` present at `:23`, filled at `ProgramService.cs:650` and `:488`), `ProgramCircuitResponse(ProgramCircuitId, ...)`, `ProgramExerciseResponse(ProgramExerciseId, ...)`.
- Confirmed: it writes the **relational assigned-program tree** `PROGRAM_BLOCKS`, `PROGRAM_SESSIONS`, `PROGRAM_CIRCUITS`, `PROGRAM_EXERCISES` (+ header `PROGRAMS`), not the jsonb template tree.

## 3.2 What UpdateAsync does today

`ProgramService.cs:159-211`:
1. Load the program by id and `PROGRAM_COACH_ID` (404 otherwise, ownership before validation).
2. FluentValidation of the request (422).
3. Serializable transaction. Load all 4 tree tables of the program (4 queries), `RemoveRange` all of them (`:180-188`).
4. Update the header (name, goal, start date, duration, updated date). **No status check**: an ARCHIVED or COMPLETED program is editable today.
5. `CopyBlockTree` re-creates every node with `Guid.NewGuid()` (`:507-588`), session status forced to `PLANNED`, `SESSION_ID` null.
6. One `SaveChanges`, commit, rebuild the response.

Data lost today on every PUT: all node ids (blocks, sessions, circuits, exercises get new ids, so the app's ids go stale); `PROGRAM_SESSION_SESSION_ID` (booking link); `PROGRAM_SESSION_STATUS` (DONE/SKIPPED back to PLANNED); `PROGRAM_SESSION_COMPLETED_DATE`, `PROGRAM_SESSION_MEMBER_FEEDBACK`, `PROGRAM_SESSION_COACH_NOTE`; all exercise actuals `PROGRAM_EXERCISE_ACTUAL_SETS/REPS/WEIGHT_KG/RPE` and `PROGRAM_EXERCISE_MEMBER_FEEDBACK`; and as a consequence the "last performed" of later programs (`ResolveLastPerformedAsync` reads DONE sessions). `CUSTOM_NOTE` is a prescribed field sent back in the body, so it survives if the client echoes it. No node table has a created date column, so there is none to lose.

## 3.3 Ids, realised and status columns

- Request nodes carry **no Id** today (none of the 4 body classes has one).
- Columns (live DB, P1):
  - `PROGRAM_BLOCKS`: no status, no realised column, no created/updated date.
  - `PROGRAM_SESSIONS`: status `PROGRAM_SESSION_STATUS`; realised `PROGRAM_SESSION_COMPLETED_DATE`, `PROGRAM_SESSION_MEMBER_FEEDBACK`, `PROGRAM_SESSION_COACH_NOTE`; link `PROGRAM_SESSION_SESSION_ID`. No created/updated date.
  - `PROGRAM_CIRCUITS`: no status, no realised column, no date.
  - `PROGRAM_EXERCISES`: realised `PROGRAM_EXERCISE_ACTUAL_SETS`, `_ACTUAL_REPS`, `_ACTUAL_WEIGHT_KG`, `_ACTUAL_RPE`, `PROGRAM_EXERCISE_MEMBER_FEEDBACK`. No date.
- Session status: enum `ProgramSessionStatus { Planned, Done, Skipped }`, db/wire `PLANNED | DONE | SKIPPED` (`ProgramSessionConfiguration.cs:116-130`, `EnumMappings.cs:483-486`). Initial = `PLANNED` (`CopyBlockTree :543`, DB default `'PLANNED'`).
- Program status: `ProgramStatus { Active, Completed, Archived }`, db/wire `ACTIVE | COMPLETED | ARCHIVED` (`EnumMappings.cs:353-356`, `ProgramConfiguration.cs:128-140`). Not editable = `Archived` or `Completed`. Note: **no code path sets COMPLETED** (grep: only the mapping helpers), so the phase C fixture needs SQL for it.
- Proposed definition of history (for review): session with `SESSION_ID` not null, or status <> PLANNED, or `COMPLETED_DATE` / `MEMBER_FEEDBACK` / `COACH_NOTE` not null, or any of its exercises has history; exercise with any of the 5 realised columns not null. Blocks and circuits have no history of their own; they are blocked only through their descendants.

## 3.4 ConflictException pattern

- `ConflictException(string message, object? details = null)` (`Mentora.Core/Exceptions/ConflictException.cs:3-5`); the middleware writes `{ success:false, data:null, error, details, statusCode:409 }` (`GlobalExceptionMiddleware.cs:29-33, 69-84`).
- `reason` lives **inside `details`** as a property named `reason`: `VisioTokenService.cs:95-97` (`new($"Cannot issue a visio token: {reason}.", new { sessionId, reason, opensAt, closesAt })`), `QuizDeviceClaimService.cs:49,77`, `QuizBasicService.cs:61,158,162`, `VoucherGrantService.cs:44` (`new { reason = "..." }`).
- ProgramService today: `:251-253` (type mismatch, details `{ sessionType, programSessionType }`, no reason), `:264` (no details), `:358-360` (`{ programStatus }`, no reason). These stay unchanged.
- New 409s will follow the Visio/Quiz pattern: `new ConflictException("<sentence>.", new { reason = "PROGRAM_HAS_HISTORY", programSessionIds, programExerciseIds })`, `new { reason = "PROGRAM_SESSION_TYPE_INCOMPATIBLE", conflicts = [{ programSessionId, sessionId }] }` (list, since several sessions may conflict), `new { reason = "PROGRAM_NOT_EDITABLE", programStatus }`, `new { reason = "PROGRAM_SESSION_ALREADY_LINKED", programSessionId, currentSessionId }`.

## 3.5 UpdateBookingAsync today

`PUT /api/v1/coach/program-sessions/{programSessionId}/booking` (`CoachProgramSessionsController.cs:29-39`), `ProgramService.cs:227-283`:
- (a) program session already linked to **another** booking: no check; the 404 / type / booking-side checks run, then `:266` overwrites the link silently, 200 (finding 1 of 11.2b).
- (b) linked to the **same** booking: all checks pass (the booking-side check excludes the row itself, `:260`), the assignment is a no-op, EF issues no UPDATE (unchanged value), 200.
- (c) `sessionId` null: detach exists (`:268-273`), clears the link only, booking untouched, 200. It worked at runtime in 11.2b (deviation 1: `PUT ... {"sessionId": null}` 200). On an unlinked session it is a 200 no-op.
- Proposed order after the change: 404 program session, 404 session, 404 member, **same booking -> 200 return, no write**, **other booking linked -> 409 PROGRAM_SESSION_ALREADY_LINKED**, existing type 409, existing booking-side 409. Null: unchanged.

## 3.6 Other writers of SESSION_ID / status / realised

- `SessionService.LinkToProgramSessionAsync` (`SessionService.cs:626-670`), called inside `ReserveAsync` and `RegisterParticipantAsync`: sets `SESSION_ID` on the earliest PLANNED, unlinked, compatible session of the active program of the booking's coach. Unaffected (it only writes unlinked rows; the new PUT keeps ids and links). Race with a concurrent PUT accepted (backlog).
- `ProgramService.UpdateBookingAsync` (`:266, :272`): see 3.5.
- `ProgramService.ApplyCompletionAsync` (`:365-435`), lot 6.6, coach and member endpoints: writes status, completed date, session feedback, coach note, exercise actuals. Requires an ACTIVE program (`EnsureProgramActiveAsync :350-361`). Unaffected; it still works on kept ids, which is the point of this lot.
- DB: `FK_PROGRAM_SESSIONS_SESSIONS_..._SESSION_ID ON DELETE SET NULL` (live DB P2): a deleted `SESSIONS` row clears the link.
- Cancellations (member/coach `.../cancel`, group unregister) do **not** touch `PROGRAM_SESSIONS` (grep: no other write in the solution, `AgendaService` and `SessionService.cs:478,803` only read). So a cancelled booking keeps its link and will block deletion of its program session (409 PROGRAM_HAS_HISTORY) until the coach detaches it. Consistent with the rule "SESSION_ID link = history"; noted as a product point.
- DB backstop: `CK_PROGRAM_SESSIONS_AUTONOME_HAS_NO_BOOKING` (a linked session switched to AUTONOME would be a 500 without the new 409).

## 3.7 Schema

No schema change needed. Two consequences:
- `PROGRAM_SESSIONS` has no created/updated date. "Keep created date" is moot, and for 11.AD case "same booking, no write" I propose to prove it with the PostgreSQL system column `xmin` of the row (changes on any UPDATE), read before and after. Deviation from the prompt's "updated date", no migration.
- No unique index on positions, so reordering (swapping positions) cannot collide. `IX_PROGRAM_SESSIONS_BOOKING_MEMBER` (unique on `SESSION_ID, MEMBER_ID`) is unaffected because links are kept, not moved.

## Decisions needed before phase B

**D1. Where the `Id` goes.** The PUT tree classes are shared with the template jsonb and the POST assign.
- (a) Recommended: add `public Guid? Id { get; set; }` to the 4 shared classes, documented "only accepted by PUT training-programs/{id}". `ProgramTemplateBodyValidator` gets a 422 rule "Id is not allowed here" unless the caller sets `RootContextData["AllowNodeIds"] = true` (only `UpdateAsync` sets it). Default is reject, so template create/update and POST assign refuse ids and the template jsonb never stores one. Small diff, the tree validator stays the single source of rules. Swagger shows `id` on template bodies too (documented as rejected there).
- (b) Separate PUT-only DTO tree (`UpdateProgramBlock`, ...) mapped to `ProgramTemplateBody` for validation. Cleaner contract, about 5 new files plus mapping code.

**D2. Wrong-kind and foreign ids.** One message for unknown, other program and wrong kind: `"Id {id} is not a {block|session|circuit|exercise} of this program."` on the node path (`Body.Blocks[1].Sessions[2].Id`, positions as indexes like the existing validator). Duplicate: `"Id {id} appears more than once."`. No query against other programs (no existence leak).

**D3. Re-parenting nodes with history.** As in the prompt, kept nodes may move anywhere inside the program, including an exercise with actuals moving under another session. Allowed; flagged as a risk (actuals could end up under a session with another status).

**D4. Order of checks.** 404 -> 409 PROGRAM_NOT_EDITABLE -> 422 (existing tree rules plus id rules) -> 409 PROGRAM_HAS_HISTORY -> 409 PROGRAM_SESSION_TYPE_INCOMPATIBLE -> writes. Only the first 409 category found is returned, with all its ids.

**D5. Type compatibility check.** Applied to every kept session that has a link (one query for the linked `SESSIONS` offer types), not only when the type changed.

## Planned queries for UpdateAsync (bounded)

program (1), 4 tree tables (4), exercise visibility from the existing validator (1, when the tree has exercises), linked `SESSIONS` offer types (1, only when at least one kept session is linked), one `SaveChanges` (batched INSERT/UPDATE/DELETE), commit, then `BuildResponseAsync` (6 or 7). No per-node SELECT. Inserts/updates/deletes are ordered by EF from the FK graph (Restrict FKs on parent block, session, circuit).

## 3.8 Fixture plan (phase C)

Coach A `coach@mentora.fr` (COACH_ID `e268a512-...`), member `member@mentora.fr` (MEMBER_ID `38b05ed2-...`). DB today: 0 rows in `PROGRAMS`, 0 `l11*` products or programs. Everything via HTTP except one SQL UPDATE.

1. `POST /api/v1/coach/products` `l11c-visio` (VISIO, published if reservation needs it) -> 1 `PRODUCTS`.
2. `POST /api/v1/coach/members/{memberId}/voucher-grants` quantity 3 -> `SESSION_VOUCHERS` 3 (+ `ORDERS` / `ORDER_ITEMS` as in 11.2b).
3. `POST /api/v1/coach/session-slots` x3, `offerType: "Visio"`, future dates -> `SESSION_SLOTS` 3.
4. Programs, in this order (each assignment archives the previous active one):
   - P1 `l11c-completed` (1 week, 1 session). Then P2 `l11c-archived` (P1 -> ARCHIVED). **SQL**: `UPDATE "PROGRAMS" SET "PROGRAM_STATUS" = 'COMPLETED' WHERE "PROGRAM_ID" = '<P1 id>'` (dry run with ROLLBACK, then STOP for the COMMIT).
   - P3 `l11c-main` (P2 -> ARCHIVED), 2 weeks: MACROCYCLE > MESOCYCLE (100) > MICRO W1, MICRO W2. W1: s1 VISIO d1, s2 PRESENTIEL_SOLO d2, s3 PRESENTIEL_SOLO d3 (2 exercises), s4 AUTONOME d4, s5 PRESENTIEL_OU_VISIO d5. W2: s6 AUTONOME d1. One STANDARD circuit per session, 1 exercise each (s3: 2), exercises taken from the seeded catalog.
5. Member reserves the 3 VISIO slots (`POST /api/v1/member/sessions`): B1 auto-links to s1 (earliest compatible), B2 to s5, B3 stays unlinked. -> `SESSIONS` 3.
6. Completion: s2 `DONE` (coach endpoint); s3 `PLANNED` with actuals on its 2nd exercise.
7. Cases 1-12 as in the prompt: 3 (omit s1), 4 (omit s2), 5 (omit s3's 2nd exercise), 6 (omit s4), 7 (s1 type), 8 (unknown id, a P2 session id, duplicate, s1 id on a circuit), 9 (P2 archived, P1 completed), 11 (s5: B3 409, B2 200 + `xmin` unchanged, null 200, B3 200), 12 (401, member 403).
8. Case 10b ("program without history"): only one ACTIVE program per member, so at the end assign P4 `l11c-nohistory` (P3 -> ARCHIVED) and PUT it without ids: 200. Case 10a runs on P3 before that.
9. EF command log for 5.5: if `Microsoft.EntityFrameworkCore.Database.Command` is not at Information in `appsettings.Development.json`, set it through `$env:Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command="Information"` at start (no file change).

Rows created and how cleanup finds them (all bounded by the recorded run start too):
| Table | Rows | Found by |
|---|---|---|
| PROGRAMS | 4 | `PROGRAM_NAME LIKE 'l11c-%'` and coach A / member |
| PROGRAM_BLOCKS / SESSIONS / CIRCUITS / EXERCISES | tree of P1-P4 | `*_PROGRAM_ID` in those programs (FK cascade from PROGRAMS) |
| SESSIONS | 3 | `SESSION_SLOT_ID` in the fixture slots / voucher ids |
| SESSION_VOUCHERS, ORDERS, ORDER_ITEMS | 3, n, n | product id of `l11c-visio` |
| SESSION_SLOTS | 3 | coach A and `*_CREATED_DATE >= run start` |
| PRODUCTS | 1 | `PRODUCT_NAME = 'l11c-visio'` |
| AUTH_OTPS, AUTH_REFRESH_TOKENS | known leftovers | coach@ / member@, not deleted |

Exact table and column names of the booking chain will be read from the live schema before writing the cleanup (same chain as 11.2b's `cleanup-dryrun.sql`).

**STOP: waiting for approval of phase A and the choices D1 to D5.**

## Decisions (approved by François, 2026-10-05)

- **D1: option (b).** Dedicated request tree used only by `PUT /api/v1/coach/training-programs/{programId}`, same property names as the `ProgramTemplate*` classes plus an optional `Guid? Id` on every node. The shared `ProgramTemplate*` classes stay untouched (template and assign endpoints unchanged). No rule duplicated: the dedicated tree is mapped to a `ProgramTemplateBody` (ids dropped) and validated by the existing `ProgramTemplateBodyValidator`, so 422 keys keep their paths; ids are validated by a separate FluentValidation validator run inside the service. Mapping helpers without switch.
- **D2: approved.** Same 422 message for an unknown id, an id of another program and an id at the wrong node kind; a duplicated id has its own message.
- **D3: changed.** A kept node that has history, or contains a node with history, must keep the same parent: moving it is a 409 `PROGRAM_HAS_HISTORY` with the blocking ids in the same details lists. Reordering within the same parent is allowed. Nodes without history move freely.
- **D4: approved.** 404, 409 `PROGRAM_NOT_EDITABLE`, 422, 409 `PROGRAM_HAS_HISTORY`, 409 `PROGRAM_SESSION_TYPE_INCOMPATIBLE`; first 409 category found, with all its ids.
- **D5: approved.** Compatibility checked on every kept linked session.
- **xmin** proof for "same booking, no write": approved.
- **11.AJ (new).** A `SESSION_ID` link to a booking whose `SESSION_STATUS` is `CANCELLED` (`SessionStatus.Cancelled`) does not count: not history (the program session may be deleted or moved), no `PROGRAM_SESSION_ALREADY_LINKED` (a new manual link replaces it), ignored by the type compatibility check. HTTP evidence: cancel one fixture booking through the existing cancel endpoint, then omit / relink its program session.
- **COMPLETED fixture** by one SQL UPDATE: approved under the stop rules.
