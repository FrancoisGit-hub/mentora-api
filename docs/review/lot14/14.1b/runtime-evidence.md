# Lot 14.1b runtime evidence: optional member fields on POST /api/v1/coach/members

- Date: 2026-11-20 (local run)
- Branch: feature/lot_14, base f2039b6
- Environment: local API (Development, `Email__Provider=Logging`, routing TEST, port 5243, single API process), local PostgreSQL (port 5433).
- No token, OTP code, password or connection string appears in this file. OTP codes were read from the API log into variables by the script and never printed. No `[EMAIL-LOG]` line is quoted. Test inbox addresses are not quoted.
- Run start date, read in the database BEFORE the first write: **2026-11-20 18:36:17 UTC** (used by the cleanup).

## Change

- `AddMemberRequest`: 4 optional fields at the end: `phone` (string?), `gender` (MALE / FEMALE / OTHER), `heightCm` (short?), `birthDate` (yyyy-MM-dd).
- `AddMemberRequestValidator`: same rules and same messages as `UpdateMemberParameterRequestValidator` (its pattern, messages and `BeAPlausibleBirthDate` are now public and shared; no second copy of the rule).
- Phone: new `Mentora.Core/PhoneNormalizer.cs` (trim, blank = NULL); `MemberParameterService.NormalizePhone` delegates to it, same behaviour.
- `CoachMemberInviteService`: the 4 fields are written only in case e (new account). In case d (existing member attached to another coach) nothing of the member is written and the request values are ignored; the response carries the values on file (like firstName and lastName), null when never set.
- `AddMemberResponse`: 4 fields at the end. No migration, no package, same route, same codes, same mail, same rate limit.

## Build

`dotnet build Mentora.sln --no-incremental`: 0 error, 19 warnings (same count as the 14.1 baseline, none in the changed files).

## Fixture

`fixture-dryrun.sql` and `fixture-commit.sql` (diff: last line only), ids `141b0000-0000-4000-8000-0000000000b1` (user) and `...b2` (coach), prefix `l141b-`. Dry run: USERS 1, COACHES 1, ROLLBACK, then COMMIT after approval. Coach A is `coach@mentora.fr`, coach B is the fixture coach.

## Cases (HTTP code and body, no token)

| # | Call | Result |
|---|---|---|
| 1 | coach A adds `l141b.full@example.com`, phone `" 06 12 34 56 78 "`, gender FEMALE, heightCm 168, birthDate 1995-04-12 | **201** `isNewAccount true`, `isPrimary true`, `phone "06 12 34 56 78"` (trimmed), `gender FEMALE`, `heightCm 168`, `birthDate 1995-04-12` |
| 2 | coach A adds `l141b.min@example.com`, email and names only | **201** `isNewAccount true`; `phone`, `gender`, `heightCm`, `birthDate` all `null` |
| 3 | coach B adds `l141b.full@example.com` with other names (`Other` / `Name`) and other values (phone `+33 7 00 00 00 00`, MALE, 190, 1980-01-01) | **201** `isNewAccount false`, `isPrimary false`, same `memberId` as case 1, names `Full` / `Member`, response keeps the case 1 values (`06 12 34 56 78`, FEMALE, 168, 1995-04-12) |

SQL read after cases 1 to 3 (phone shown between brackets to show the trim):

```
 USER_EMAIL             | fn   | ln     | phone            | gender | h   | birth
 l141b.full@example.com | Full | Member | [06 12 34 56 78] | FEMALE | 168 | 1995-04-12
 l141b.min@example.com  | Min  | Member | [<NULL>]         |        |     |
```

Links: `l141b.full` has 2 rows (coach A primary true, coach B primary false); `l141b.min` has 1 row (coach A, primary true). Case 3 changed nothing on the member row (names and the 4 fields are those of case 1).

### Case 4: validation (422, keys only)

| Body | Status | Key |
|---|---|---|
| phone `abc` | **422** | `Phone` |
| heightCm 20 | **422** | `HeightCm` |
| heightCm 300 | **422** | `HeightCm` |
| birthDate tomorrow (2026-11-21) | **422** | `BirthDate` |
| birthDate giving age 10 (2016-11-20) | **422** | `BirthDate` |

Phone and BirthDate messages are exactly those of the member parameters validator. The HeightCm message is the FluentValidation default, in the server culture (French here), same as the member parameters endpoint.

### Case 5: invalid gender string

`"gender":"BAD"`: **400**, body `{"success":false,"data":null,"error":"Request body is required.","statusCode":400}`. This is the existing model binding behaviour (the enum converter rejects the value before validation); global behaviour not changed. Not a 422, no `Gender` key.

Rows written by the refused cases (4, 5): none (SQL read: 3 `l141b` users in total, the fixture coach and the two members of cases 1 and 2).

### Case 6: mail

One `[EMAIL]` line per created or attached member, 3 lines for cases 1, 2, 3, each with `to=` the 4 test recipients (counted, not printed), `bcc=` empty, mode TEST:

```
[EMAIL] mode=TEST to=<4 test recipients> bcc= subject=[TEST pour l141b.full@example.com] Votre coach Federico vous a ajouté sur Mentora
[EMAIL] mode=TEST to=<4 test recipients> bcc= subject=[TEST pour l141b.min@example.com] Votre coach Federico vous a ajouté sur Mentora
[EMAIL] mode=TEST to=<4 test recipients> bcc= subject=[TEST pour l141b.full@example.com] Votre coach l141b-Coach vous a ajouté sur Mentora
```

The mail routing, subject and body are those of 14.1 (unchanged).

## Cleanup

`cleanup-dryrun.sql` and `cleanup-commit.sql` (diff: last line only, `ROLLBACK;` vs `COMMIT;`). Scope: users whose email starts with `l141b` AND created after 2026-11-20 18:36:17 (cascade for members, coaches, links, parameters, programs), plus the OTPs and refresh tokens created after the run start by the pre-existing users.

Dry run (ROLLBACK): 3 `l141b` users in scope, 2 pre-existing users kept; deleted 3 OTPs and 2 refresh tokens of the pre-existing users, then 3 users; leftover check on every table with a `*_CREATED_DATE` column: 0 row; `MEMBER_COACHES` 2 and `USERS` 2 after. COMMIT run after approval, output: `BEGIN`, before 3 `l141b` users in scope and 2 pre-existing users kept, `DELETE 3` (OTPs), `DELETE 2` (refresh tokens), `DELETE 3` (users), after-check 0 row, `MEMBER_COACHES` 2, `USERS` 2, `COMMIT`.

Final check in a separate read-only session (`BEGIN READ ONLY` ... `ROLLBACK`): every table with a `*_CREATED_DATE` column, rows created after 2026-11-20 18:36:17: **0 row**; `l141b` users left: 0.

API stopped, port 5243 free.
