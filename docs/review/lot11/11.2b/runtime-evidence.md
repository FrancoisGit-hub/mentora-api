# Lot 11.2b: runtime evidence (local)

Run start (UTC, `SELECT now()`): **2026-10-04T20:46:22.905787Z** (recorded before the coach B COMMIT; `run-start.txt`). Local PostgreSQL 16 (port 5433), API on port 5243 started with `ASPNETCORE_ENVIRONMENT=Development`, `Email__Provider=Logging`. Fixtures prefixed `l11b-`. Tokens and OTPs were kept in scratch files and are not recorded here.

## Build

`dotnet build`: 0 errors. Warnings are CS1573 (missing `<param>` tags) only; the ones in `ProgramSessionResponse.cs` are the same kind the file already produced for its undocumented parameters, `SessionId` itself is documented.

## Code

- `ProgramSessionTypes.CompatibleWithOffer(OfferType)` (if-chain) and `IsCompatibleWithOffer(string, OfferType)` in `Mentora.Core/Enums/ProgramSessionWire.cs`.
  VISIO -> VISIO, PRESENTIEL_OU_VISIO; PRESENTIEL_SOLO -> PRESENTIEL_SOLO, PRESENTIEL_OU_VISIO; PRESENTIEL_GROUPE -> PRESENTIEL_GROUPE; VISIO_GROUPE -> none.
- `SessionService.LinkToProgramSessionAsync`: active program of `session.SessionCoachId`; candidates filtered in SQL on `PROGRAM_SESSION_COACH_ID` and `compatibleTypes.Contains(type)`; early return when the list is empty.
- `ProgramService.UpdateBookingAsync`: uses `IsCompatibleWithOffer`; 409 message and `details` unchanged.
- `ProgramSessionResponse.SessionId` (`Guid?`) filled in `AssembleTree` and `BuildSessionResponseAsync` (the only two constructor calls).
- `VisioTokenService`: eligible = individual session with offer VISIO or PRESENTIEL_SOLO.
- `IS_PRIMARY` is read only by `MemberMeService.cs:47,63` (coach list ordering and display); no path of this run needs it, so coach B is non-primary.

## Fixture: coach B

`fixture-coachb-dryrun.sql` (ROLLBACK, 1 row per table) then `fixture-coachb-commit.sql` (diff: last line only). Ids `11b00000-0000-4000-8000-0000000000b1` (user), `...b2` (coach), `...b3` (member link, `IS_PRIMARY` false). Coach B email `l11b-coachb@mentora.local`.
After the API start the seeders created, for coach B: `COACH_PARAMETERS` 1, `OFFER_PROGRAMS` 4.

## Program (coach A, member `member@mentora.fr`)

Body path, 1 MICROCYCLE week, 4 sessions: s1 `PRESENTIEL_OU_VISIO` (day 1), s2 `AUTONOME` (day 2), s3 `VISIO` (day 3), s4 `PRESENTIEL_OU_VISIO` (day 4). `POST /api/v1/coach/members/{memberId}/training-programs`: 201.
Fixtures: coach A products `PRESENTIEL_SOLO`, `VISIO`, `PRESENTIEL_GROUPE` (published for the group test); coach B product `PRESENTIEL_SOLO`; one slot each (slot offer types use the PascalCase wire values), vouchers through `voucher-grants` (201 each).

## HTTP evidence

| # | Step | Expected | Actual |
|---|---|---|---|
| 1 | Member reserves coach A `PRESENTIEL_SOLO` slot (200); GET program as coach and as member | s1 (`PRESENTIEL_OU_VISIO`) has `sessionId` = booking id | coach 200 and member 200: s1 `sessionId=8ffd1d2f-...` (the booking), s2/s3/s4 null |
| 2 | Member reserves coach B `PRESENTIEL_SOLO` slot (200); GET coach A program; SQL | no coach A program session gets B's booking | s4 (`PRESENTIEL_OU_VISIO`, unlinked, would have matched under the old code) stays null; SQL: 0 `PROGRAM_SESSIONS` rows with `PROGRAM_SESSION_SESSION_ID` = B's booking |
| 3a | PUT booking, s2 `AUTONOME` + A `PRESENTIEL_SOLO` booking | 409 same shape | 409 `Session and program session types do not match.` `details {sessionType: PRESENTIEL_SOLO, programSessionType: AUTONOME}` |
| 3b | PUT booking, s1 `PRESENTIEL_OU_VISIO` + A `VISIO` booking | 200 + `sessionId` | first attempt 409 (see deviation 1); after detaching s3: 200, `sessionId=dcee7940-...` (the VISIO booking) |
| 3c | PUT booking, s3 `VISIO` + `PRESENTIEL_SOLO` booking | 409 | 409 `details {sessionType: PRESENTIEL_SOLO, programSessionType: VISIO}` |
| 4a | Visio token, `PRESENTIEL_SOLO` booking, coach endpoint | 200 | 200, `roomName session-8ffd1d2f-...`, `identity coach-e268a512-...`, `expiresAt 2026-10-04T22:11:00Z` |
| 4b | same, member endpoint | 200 | 200, same room, `identity member-38b05ed2-...`, `expiresAt 2026-10-04T22:11:00Z` |
| 4c | Visio token, `VISIO` booking, coach and member | 200 (regression) | 200 and 200, room `session-dcee7940-...`, `expiresAt 2026-10-04T22:12:00Z` |
| 4d | Member cancels the `PRESENTIEL_SOLO` booking, then token on coach and member endpoints | 409 `CANCELLED` | cancel 200; coach 409 and member 409, `Cannot issue a visio token: CANCELLED.` |
| 4e | Group session (published `PRESENTIEL_GROUPE` product, group slot, `POST /coach/sessions/group` 201), coach token | 409 `NOT_ELIGIBLE` | 409 `NOT_ELIGIBLE` |

## SQL of the automatic link (EF command log of a reservation)

```sql
SELECT p."PROGRAM_ID", ... FROM "PROGRAMS" AS p
WHERE p."PROGRAM_MEMBER_ID" = @__memberId_0 AND p."PROGRAM_COACH_ID" = @__coachId_1 AND p."PROGRAM_STATUS" = 'ACTIVE'
LIMIT 1

SELECT p."PROGRAM_SESSION_ID", ... FROM "PROGRAM_SESSIONS" AS p
INNER JOIN "PROGRAM_BLOCKS" AS p0 ON p."PROGRAM_SESSION_BLOCK_ID" = p0."PROGRAM_BLOCK_ID"
WHERE p."PROGRAM_SESSION_PROGRAM_ID" = @__program_ProgramId_0 AND p."PROGRAM_SESSION_MEMBER_ID" = @__memberId_1
  AND p."PROGRAM_SESSION_STATUS" = 'PLANNED' AND p."PROGRAM_SESSION_SESSION_ID" IS NULL
  AND p."PROGRAM_SESSION_COACH_ID" = @__coachId_2 AND p."PROGRAM_SESSION_TYPE" = ANY (@__compatibleTypes_3)

UPDATE "PROGRAM_SESSIONS" SET "PROGRAM_SESSION_SESSION_ID" = @p18 WHERE "PROGRAM_SESSION_ID" = @p19;
```

The coach filter (`PROGRAM_COACH_ID`, `PROGRAM_SESSION_COACH_ID`) and the compatible-type filter (`= ANY`) are in the `WHERE`; nothing is filtered in memory except the earliest-date ordering.

## Deviations

1. **Manual link 3b.** The `VISIO` booking was auto-linked on reservation to s3 (`VISIO`, the only unlinked compatible candidate besides the already-linked s1). The manual PUT s1 + that booking therefore hit the existing defensive 409 (`This member already has a program session linked to that booking.`, unchanged behaviour). I detached s3 (`PUT ... {"sessionId": null}`, 200), then PUT s1: 200.
2. Slot creation first failed 400 (`PRESENTIEL_SOLO` not accepted); slot offer types use `Visio`/`PresentielSolo`/`PresentielGroupe`. Not a code change.
3. A group-session `NOT_ELIGIBLE` check needed the product to be published first (409 `Product must be published...`); published it, then 201.
4. Side effects not covered by the cleanup: `AUTH_OTPS` / refresh-token rows for coach A and the member from the logins (coach B's cascade with its user).

## Cleanup

`cleanup-dryrun.sql` (ends with `ROLLBACK`) and `cleanup-commit.sql`: `diff` shows only the last line. Dry-run counts (before -> after, inside the transaction, then rolled back):

| Table | Before | After |
|---|---|---|
| SESSIONS | 4 | 0 |
| SESSION_VOUCHERS | 3 | 0 |
| ORDERS / ORDER_ITEMS | 3 / 3 | 0 / 0 |
| SESSION_SLOTS | 4 | 0 |
| PROGRAMS / PROGRAM_SESSIONS | 1 / 4 | 0 / 0 |
| PRODUCTS | 4 | 0 |
| COACH_PARAMETERS (B) / OFFER_PROGRAMS (B) / MEMBER_COACHES (B) | 1 / 4 / 1 | 0 / 0 / 0 |
| COACHES (B) / USERS (B) | 1 / 1 | 0 / 0 |

Rolled back: afterwards 4 `l11b-` products still present.

**COMMIT executed after François's explicit written approval** (`cleanup-commit.sql`): `DELETE` counts 4 / 3 / 3 / 3 / 1 / 3 / 1 / 1 (sessions, vouchers, orders, slots, programs, products, coach B, user B); after counts all 0 (13 rows: SESSIONS, SESSION_VOUCHERS, ORDERS, ORDER_ITEMS, SESSION_SLOTS, PROGRAMS, PROGRAM_SESSIONS, PRODUCTS, COACH_PARAMETERS(B), OFFER_PROGRAMS(B), MEMBER_COACHES(B), COACHES(B), USERS(B)). `l11b-` products afterwards: 0.

### Residual check (read only)

Every `public` table with a `*_CREATED_DATE` column, rows with date >= `2026-10-04 20:46:22.905787+00`: only `AUTH_OTPS` (2) and `AUTH_REFRESH_TOKENS` (2), one each for `coach@mentora.fr` and `member@mentora.fr`. Known leftovers, not deleted (coach B's rows went with its user by cascade).

## Findings (pre-existing behavior, backlog, no code changed)

1. **Manual link silently replaces an existing link on the program session.** `ProgramService.UpdateBookingAsync` only guards the booking side (`alreadyLinked`, `ProgramService.cs:259-264`: same booking already linked to another program session of that member). It then assigns `ProgramSessionSessionId = sessionId` unconditionally (`:266`), so a program session that already has a linked booking gets that link overwritten with no check or warning. Observed: `PUT s1 + VISIO booking` returned 200 and replaced s1's link to the `PRESENTIEL_SOLO` booking (read-only SQL afterwards: s1 -> VISIO booking, 0 `PROGRAM_SESSIONS` rows reference the first booking). Identical code at `43c5147` (`git show 43c5147:Mentora.Infrastructure/Services/ProgramService.cs`, lines 259, 263, 266), so it predates 11.2b.
2. `CreateSessionSlotRequest.offerType` expects the C# enum name (`PresentielSolo`), not the wire value (`PRESENTIEL_SOLO`); pre-existing, not changed.
