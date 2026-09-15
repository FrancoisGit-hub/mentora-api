# E1.1 cleanup manifest

## Boundary used, and why

Cleanup deletes every `AUTH_OTPS` / `AUTH_REFRESH_TOKENS` row for the two Lot 7
test accounts (coach `ff2be73b-8a80-46d6-90d8-cd462b876095`, member
`874d7734-cc4f-47ee-9527-3a4c0262f7b3`) with a creation date on or after
**2026-09-15 00:00:00** — i.e. **calendar day, not a row-count subtraction**.
2026-09-15 is this session (first read of `git status` to this manifest);
2026-09-14 is the prior session's own baseline-capture work. Everything dated
2026-09-15 is this session's residue, debug runs and real runs alike, and all of
it goes. Everything dated 2026-09-14 stays.

## The subtraction estimate was wrong — say why plainly

The cleanup was first proposed as `current total - auth-test-run-20260915-103213.md
"Before" column` (39/31 coach, 20/18 member -> LIMIT 45/49/14/13). That arithmetic
implicitly assumed the `103213` report's `Before` snapshot was entirely
2026-09-14 residue — i.e. that nothing from *this* session existed yet at that
point. That assumption was false: before `103213` was ever produced, this session
had already run `run-auth-tests.ps1` twice while debugging it (once failing
outright on the `-like`/`[OTP]` bracket bug, once passing 15/16 before the
tamper-flakiness bug was found and fixed) — both runs happened on 2026-09-15,
both created real `AUTH_OTPS` rows, and the passing one created real
`AUTH_REFRESH_TOKENS` rows too. Those rows were silently folded into the "39/31 /
20/18" figure and misattributed to the prior session.

The date-predicate verification step (run before any DELETE, as instructed) caught
this directly: it returned 61/58/18/15 against an expected 45/49/14/13. Had the
original LIMIT-based statement been run instead, it would have deleted 45 of the
61 coach `AUTH_OTPS` rows and stopped — leaving 16 rows of 2026-09-15 residue
behind while this manifest would have (wrongly) claimed the DB was "back to
baseline." Safe with respect to data loss, but a false proof, which matters more
here than the leftover rows themselves would have.

No attempt was made to separate "debug runs" from "real runs" within 2026-09-15:
there is no reliable signal to draw that line on, and no reason to — every row
dated today is this session's, full stop.

## What was deleted

Verified immediately before deletion (matches the prior verification step exactly):

| Row | Count just before DELETE |
|---|---|
| Coach `AUTH_OTPS`, `>= 2026-09-15` | 61 |
| Coach `AUTH_REFRESH_TOKENS`, `>= 2026-09-15` | 58 |
| Member `AUTH_OTPS`, `>= 2026-09-15` | 18 |
| Member `AUTH_REFRESH_TOKENS`, `>= 2026-09-15` | 15 |

Executed, one `DELETE` per table per user, scoped by `USER_ID` and the same date
predicate, no subselect/LIMIT:

```sql
DELETE FROM "AUTH_OTPS" WHERE "USER_ID" = 'ff2be73b-8a80-46d6-90d8-cd462b876095' AND "AUTH_OTP_CREATED_DATE" >= '2026-09-15 00:00:00';
DELETE FROM "AUTH_REFRESH_TOKENS" WHERE "USER_ID" = 'ff2be73b-8a80-46d6-90d8-cd462b876095' AND "AUTH_REFRESH_TOKEN_CREATED_DATE" >= '2026-09-15 00:00:00';
DELETE FROM "AUTH_OTPS" WHERE "USER_ID" = '874d7734-cc4f-47ee-9527-3a4c0262f7b3' AND "AUTH_OTP_CREATED_DATE" >= '2026-09-15 00:00:00';
DELETE FROM "AUTH_REFRESH_TOKENS" WHERE "USER_ID" = '874d7734-cc4f-47ee-9527-3a4c0262f7b3' AND "AUTH_REFRESH_TOKEN_CREATED_DATE" >= '2026-09-15 00:00:00';
```

Result: `DELETE 61`, `DELETE 58`, `DELETE 18`, `DELETE 15` — exact match to the
verified counts, no silent partial delete.

## Before / after / delta

| Table | User | Before | After | Delta |
|---|---|---|---|---|
| `AUTH_OTPS` | coach | 84 | 23 | -61 |
| `AUTH_REFRESH_TOKENS` | coach | 80 | 22 | -58 |
| `AUTH_OTPS` | member | 34 | 16 | -18 |
| `AUTH_REFRESH_TOKENS` | member | 31 | 16 | -15 |

## 2026-09-14 residue — left in place, deliberately

| Table | User | Remaining (all dated < 2026-09-15) |
|---|---|---|
| `AUTH_OTPS` | coach | 23 |
| `AUTH_REFRESH_TOKENS` | coach | 22 |
| `AUTH_OTPS` | member | 16 |
| `AUTH_REFRESH_TOKENS` | member | 16 |

Confirmed: post-delete, `AUTH_OTPS`/`AUTH_REFRESH_TOKENS` row counts for both users
are now **entirely** 2026-09-14 rows (the "remaining >= 2026-09-15" count is 0 for
all four). This is the prior session's own baseline-capture residue — its OTPs and
refresh tokens, some presumably already expired/revoked, some possibly still live.
It is **left untouched on purpose**: E1.2 ships a purge job for
`AuthOtp`/`AuthRefreshToken` on revocation/expiry criteria, and this residue is
useful, realistic test data for exercising that job rather than a bug to hand-clean
here. Hand-deleting it now would just mean re-manufacturing equivalent test data
for E1.2 later.

**This 2026-09-14 residue is also the reason an absolute pre-2026-09-14 baseline is
untraceable.** The two run reports produced while debugging the harness earlier in
*this* session (before `auth-test-run-20260915-103213.md` was kept) were deleted as
debug noise, and their row deltas were never separately recorded before deletion.
Combined with the fact that the 2026-09-14 session's own true starting point was
never captured as a raw table count either (only as specific known behaviors in
`auth-baseline-before.md`), there is no reconstructable "zero" point for these two
test accounts further back than "however many rows existed by the end of the
2026-09-14 session" — which is exactly the 23/22/16/16 figures above, taken as
given rather than independently verified.

## Untouched, confirmed

| Check | Result |
|---|---|
| `USER_DEVICES` (coach) | 0 (unchanged) |
| `USER_DEVICES` (member) | 0 (unchanged) |
| `USERS.USER_DELETION_REASON` (coach) | `(null)` (unchanged) |
| `USERS.USER_DELETION_REQUESTED_DATE` (coach) | `(null)` (unchanged) |
| `EXERCISES` total | 45 (unchanged) |
| `EXERCISES` catalogue subset (`EXERCISE_COACH_ID IS NULL`) | 45 (unchanged) |

No statement in this cleanup touched any table other than `AUTH_OTPS` and
`AUTH_REFRESH_TOKENS`, and each was scoped to exactly one `USER_ID` at a time.

## 5.4 — post-cleanup verification run

One final `run-auth-tests.ps1` pass follows this cleanup, to prove the delete
didn't break anything. That run **will** create fresh `AUTH_OTPS` /
`AUTH_REFRESH_TOKENS` / `USER_DEVICES` rows (and touch/reset the coach deletion
markers) by design — that is what the script does. Those rows are **known,
intentional, and left in place** (not looped, not re-cleaned).

Result: **16/16 PASS** (`auth-test-run-20260915-113618.md`), confirming the cleanup
did not break the auth flow. Its own row accounting:

| Table | User | Before this run (= clean 2026-09-14 residue) | After this run |
|---|---|---|---|
| `AUTH_OTPS` | coach | 23 | 31 |
| `AUTH_REFRESH_TOKENS` | coach | 22 | 31 |
| `AUTH_OTPS` | member | 16 | 18 |
| `AUTH_REFRESH_TOKENS` | member | 16 | 18 |
| `USER_DEVICES` | both | 0 | 0 |

The `Before` column here exactly matches the post-cleanup 2026-09-14 residue
recorded above, confirming the cleanup landed cleanly. The `After` column (coach
31/31, member 18/18) is this session's final, deliberate residue — left in place,
not cleaned up again, per instruction not to loop.
