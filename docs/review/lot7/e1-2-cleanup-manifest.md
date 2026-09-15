# E1.2 cleanup manifest

## Boundary used, and why

Same rule as E1.1 (`docs/review/lot7/e1-1-cleanup-manifest.md`): calendar day, not a
row-count subtraction. Every `AUTH_OTPS` / `AUTH_REFRESH_TOKENS` / `USER_DEVICES` row
for the two Lot 7 test accounts (coach `ff2be73b-8a80-46d6-90d8-cd462b876095`, member
`874d7734-cc4f-47ee-9527-3a4c0262f7b3`) with `CREATED_DATE >= 2026-09-15 00:00:00`
goes — predicate always keyed on the row's own `CREATED_DATE` column, never on
expiration and never a hand-computed subtraction. Today (2026-09-15) is this session's
entire E1.2 runtime-verification pass, restarted from scratch after the mid-session
reboot; 2026-09-14 and earlier is prior-session residue and stays untouched.

## The backdated row — modified, not created, by this session; deleted by CREATED_DATE

`AUTH_REFRESH_TOKEN_ID = 2a7d4a7d-be50-4d76-96ce-54157d130953` was created this
session (2026-09-15 13:46:03 UTC) via a real OTP login, specifically to produce the
"expired" refresh-failure-mode test (section 5.3). Its
`AUTH_REFRESH_TOKEN_EXPIRATION_DATE` was deliberately backdated, with explicit
go-ahead, to `2026-09-15 00:00:00+00` (touching that column only, `IS_REVOKED` left
`false`) so the refresh attempt would fail on expiry alone. The cleanup predicate
above keys on `CREATED_DATE`, not `EXPIRATION_DATE`, so this row was swept up by its
real creation timestamp regardless of the backdate — confirmed explicitly before the
delete ran, not assumed.

## What was deleted

Verified by COUNT with the exact delete predicate, shown to the user, before any
DELETE ran:

| Table | User | Count just before DELETE |
|---|---|---|
| `AUTH_OTPS` | coach | 36 |
| `AUTH_REFRESH_TOKENS` | coach | 45 |
| `USER_DEVICES` | coach | 0 |
| `AUTH_OTPS` | member | 8 |
| `AUTH_REFRESH_TOKENS` | member | 10 |
| `USER_DEVICES` | member | 0 |

`USER_DEVICES` needed no DELETE statement at 0/0 for both accounts. Executed, one
`DELETE` per table per user, scoped by `USER_ID` and the same `CREATED_DATE`
predicate, no subselect/LIMIT:

```sql
DELETE FROM "AUTH_OTPS" WHERE "USER_ID" = 'ff2be73b-8a80-46d6-90d8-cd462b876095' AND "AUTH_OTP_CREATED_DATE" >= '2026-09-15 00:00:00';
DELETE FROM "AUTH_REFRESH_TOKENS" WHERE "USER_ID" = 'ff2be73b-8a80-46d6-90d8-cd462b876095' AND "AUTH_REFRESH_TOKEN_CREATED_DATE" >= '2026-09-15 00:00:00';
DELETE FROM "AUTH_OTPS" WHERE "USER_ID" = '874d7734-cc4f-47ee-9527-3a4c0262f7b3' AND "AUTH_OTP_CREATED_DATE" >= '2026-09-15 00:00:00';
DELETE FROM "AUTH_REFRESH_TOKENS" WHERE "USER_ID" = '874d7734-cc4f-47ee-9527-3a4c0262f7b3' AND "AUTH_REFRESH_TOKEN_CREATED_DATE" >= '2026-09-15 00:00:00';
```

Result: `DELETE 36`, `DELETE 45`, `DELETE 8`, `DELETE 10` — exact match to the
verified counts, no silent partial delete. `2a7d4a7d-be50-4d76-96ce-54157d130953`
went out with the `DELETE 45` coach `AUTH_REFRESH_TOKENS` statement, by its
`CREATED_DATE`.

## Deliberately left in place

**2026-09-14 residue** (all `CREATED_DATE < 2026-09-15`) — the prior session's own
baseline-capture data, kept as realistic input for the purge job rather than
hand-cleaned. Post-delete, confirmed unchanged and identical to the count recorded at
the end of E1.1's own cleanup:

| Table | User | Remaining (all `< 2026-09-15`) |
|---|---|---|
| `AUTH_OTPS` | coach | 23 |
| `AUTH_REFRESH_TOKENS` | coach | 22 |
| `AUTH_OTPS` | member | 16 |
| `AUTH_REFRESH_TOKENS` | member | 16 |

**The four March/April 2026 purge-eligible rows** — `a3456df1-3f9c-4d33-9b57-91138176555c`,
`9fe06ee0-ba0b-4f53-8dad-6b66e6fe6e32`, `8e3b73be-b6d1-47cc-ab62-80c999a6dbe1`,
`e816da91-e06c-496f-b259-6bbb248442ae`. These are the only rows in the database old
enough to satisfy `RefreshTokenPurgeService`'s 30-day retention cutoff, and are the
entire reason the purge's count-only output has ever been non-zero in this session
(section 5.1/5.2/5.7). Deleting them would leave the purge permanently reporting 0
rows with no way to demonstrate the predicate does anything, so they stay. Confirmed
present, unchanged, after the delete:

```
        AUTH_REFRESH_TOKEN_ID         | AUTH_REFRESH_TOKEN_CREATED_DATE
--------------------------------------+---------------------------------
 a3456df1-3f9c-4d33-9b57-91138176555c | 2026-03-26 12:29:44.63321+00
 9fe06ee0-ba0b-4f53-8dad-6b66e6fe6e32 | 2026-03-26 12:30:01.16686+00
 8e3b73be-b6d1-47cc-ab62-80c999a6dbe1 | 2026-04-30 15:39:54.225334+00
 e816da91-e06c-496f-b259-6bbb248442ae | 2026-04-30 15:57:19.502103+00
```

## Before / after / delta

| Table | User | Before (>= 09-15) | After (>= 09-15) | Delta |
|---|---|---|---|---|
| `AUTH_OTPS` | coach | 36 | 0 | -36 |
| `AUTH_REFRESH_TOKENS` | coach | 45 | 0 | -45 |
| `AUTH_OTPS` | member | 8 | 0 | -8 |
| `AUTH_REFRESH_TOKENS` | member | 10 | 0 | -10 |

## Untouched, confirmed post-delete

| Check | Result |
|---|---|
| `EXERCISES` total | 45 (unchanged) |
| `EXERCISES` catalogue subset (`EXERCISE_COACH_ID IS NULL`) | 45 (unchanged) |
| `USER_DEVICES` (coach + member) | 0 (unchanged) |
| `USERS.USER_DELETION_REASON` / `USER_DELETION_REQUESTED_DATE` (coach) | `(null)` / `(null)` (unchanged) |
| 2026-09-14 residue, all four buckets | unchanged, matches E1.1's own post-cleanup figures |
| Four March/April purge-eligible rows | unchanged, same `CREATED_DATE`s |

No statement in this cleanup touched any table other than `AUTH_OTPS` and
`AUTH_REFRESH_TOKENS` (no `USER_DEVICES` statement was needed), and each was scoped to
exactly one `USER_ID` at a time.
