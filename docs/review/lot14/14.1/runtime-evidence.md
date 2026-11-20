# Lot 14.1 runtime evidence: a coach adds a member by email

- Date: 2026-11-19 / 2026-11-20 (local runs)
- Branch: feature/lot_14
- Environment: local API (Development, `Email__Provider=Logging`, routing TEST), local PostgreSQL (port 5433).
- No token, OTP code, password or connection string appears in this file. OTP codes were read from the API log into
  variables by the scripts and never printed. No `[EMAIL-LOG]` line is quoted. Test inbox addresses are not quoted.
- Run start date, read in the database BEFORE the first write: **2026-11-19 21:46:18 UTC** (used by the cleanup).

## Build

`dotnet build Mentora.sln --no-incremental`: 0 error, 19 warnings (same count as the baseline before the change, none in the new files).

## Migration and startup

- Precheck `docs/review/lot14/14.1/precheck.sql` on the local DB (BEGIN READ ONLY ... ROLLBACK, output in `precheck-local.txt`): query 1 (emails duplicated case-insensitively) 0 row, query 2 (surrounding whitespace) 0 row.
- First start, migration applied once:
  - `Applying migration '20261119221223_Lot14_1_UserEmailCi'.`
  - `CREATE UNIQUE INDEX "UX_USERS_EMAIL_CI" ON "USERS" (lower("USER_EMAIL"));`
- Second start: no `Applying migration` line. Startup line present before the migrations: `[INVITE] installUrl=False permitLimit=30 windowMinutes=60`.
- The existing case-sensitive index `IX_USERS_USER_EMAIL` is kept (removal out of scope).

## Generated SQL (text only, parameters are shown as `?`, column lists shortened)

OTP request and verify lookup (`AuthService`):

```sql
SELECT u."USER_ID", ..., c."COACH_ID", ..., m."MEMBER_ID", ...
FROM "USERS" AS u
LEFT JOIN "COACHES" AS c ON u."USER_ID" = c."USER_ID"
LEFT JOIN "MEMBERS" AS m ON u."USER_ID" = m."USER_ID"
WHERE lower(u."USER_EMAIL") = @__email_0 AND u."USER_IS_ENABLED"
LIMIT 1
```

Member lookup (`CoachMemberInviteService`), preceded by `SELECT pg_advisory_xact_lock(7141401)` as the first statement of the transaction:

```sql
SELECT u."USER_ID", ..., c."COACH_ID", ..., m."MEMBER_ID", ...
FROM "USERS" AS u
LEFT JOIN "COACHES" AS c ON u."USER_ID" = c."USER_ID"
LEFT JOIN "MEMBERS" AS m ON u."USER_ID" = m."USER_ID"
WHERE lower(u."USER_EMAIL") = @__email_0
LIMIT 1
```

Case e writes, in one `SaveChanges`: `INSERT INTO "USERS" (... 11 columns ...)`, `INSERT INTO "MEMBERS" (... 12 columns ...)`, `INSERT INTO "MEMBER_COACHES" (... 6 columns ...)`.
Case d writes one `INSERT INTO "MEMBER_COACHES"`; EF omits `IS_PRIMARY` there (value false equals the column default, `RETURNING "IS_PRIMARY"`); when the value is true EF sends it.
EF command logging was then turned off (`Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command=Warning`, verified: 0 `Executed DbCommand` line at startup).

## USERS columns written in case e (existing member row used as the model)

| Column | Existing member row (local) | Written by the service (`CoachMemberInviteService.cs`) |
|---|---|---|
| USER_ROLE | `MEMBER` | `"MEMBER"` (`:32`, `:66`) |
| USER_LOGIN | NULL | not set, NULL |
| USER_PSW | NULL | not set, NULL |
| USER_IS_ENABLED | true | true (`:65`) |
| USER_CREATED_DATE | set | now (`:63`) |
| USER_EMAIL | n/a | normalized email (`:64`) |
| other nullable columns | NULL | not set, NULL |

The OTP request and verify flow never reads `UserPsw` (only `User.cs:9` and `UserConfiguration.cs:22` mention it).

## Fixtures

`fixture-dryrun.sql` and `fixture-commit.sql` (diff: last line only, `ROLLBACK;` vs `COMMIT;`), ids `14100000-0000-4000-8000-0000000000b1..b5`, prefix `l141-`:
coach B (b1 user, b2 coach), disabled member user (b3 user, b4 member), enabled user with no profile (b5). Member-style users have USER_LOGIN and USER_PSW NULL like the existing member row. Dry run counts: USERS 3, COACHES 1, MEMBERS 1. After the COMMIT and an API start, the seeders created the coach B parameters (1 row) and 4 offer programs.

## Cases (HTTP code and body, no token)

Coach A is `coach@mentora.fr` (logged in with `isCoach: true`, both profiles), coach B is the fixture coach, the member JWT is `member@mentora.fr`.

| # | Call | Result |
|---|---|---|
| 1 | coach A adds `"  L141.New.Member@Example.com "`, names `"  Lea "` / `" Martin "` | **201** `isNewAccount true`, `isPrimary true`, email `l141.new.member@example.com`, `firstName Lea`, `lastName Martin` (trimmed). SQL read: stored email is lowercase, role `MEMBER`, enabled, login NULL, psw NULL, `MEMBER_HAS_ACTIVATED` false, activation date NULL, link `IS_PRIMARY` true, `STARTED_AT` set |
| 2 | coach A, same email again | **409** `details.reason ALREADY_ATTACHED` |
| 3 | coach B, same email in other casing, other names (`Other` / `Name`) | **201** `isNewAccount false`, `isPrimary false`, names unchanged (`Lea` / `Martin`). SQL read: 2 links (A true, B false), 1 USERS row for that email |
| 4a | coach A adds coach B's email | **409** `EMAIL_IS_COACH` |
| 4b | coach B adds coach A's email (both profiles, typed `COACH@mentora.fr`) | **409** `EMAIL_IS_COACH` |
| 4c | coach A adds coach A's own email | **409** `EMAIL_IS_COACH` |
| 5 | coach A adds the disabled fixture user | **409** `ACCOUNT_DISABLED`, nothing written |
| 5b | coach A adds the enabled fixture user without profile | **409** `NO_MEMBER_PROFILE`, nothing written |
| 6 | validation, keys only | **422** `Email` for: missing, blank, bad format, 256 characters. **422** `FirstName` for: missing, blank, 101 characters. **422** `LastName` for: missing, blank, 101 characters. `{}` gives `Email,FirstName,LastName` |
| 7a | no JWT | **401** |
| 7b | member JWT | **403** |
| 8 | API restarted with `MemberInvite__PermitLimit=3` for that process only (`[INVITE] ... permitLimit=3`) | coach A calls 1, 2, 3: **201**; call 4: **429** `Retry-After=3600`, body `{"success":false,"data":null,"error":"Too many requests.","statusCode":429}`; coach B call 1 in the same window: **201** (own partition). Restarted afterwards without the variable: `[INVITE] ... permitLimit=30` |
| 9 | two simultaneous POST (HttpClient, same new email, coach A) | one **201** (`isNewAccount true`), one **409** `ALREADY_ATTACHED`. SQL read: 1 USERS row and 1 MEMBER_COACHES row for that email |
| 10 | mail | see below |
| 11 | login of the new member | see below |
| 12 | existing coach routes | see below |
| 13 | startup refusal | see below |
| 14 | OTP contract | see below |

Rows written by the refused cases (2, 4, 5, 5b, 6, 7): none (SQL read: no USERS row besides the expected ones).

### Case 10: mail

One `[EMAIL]` line per case d and e (log file written with the console code page, so the accent shows as a replacement character there; the subject sent has the real "é"):

```
[EMAIL] mode=TEST to=<the 4 test inboxes> bcc= subject=[TEST pour l141.new.member@example.com] Votre coach Federico vous a ajouté sur Mentora
[EMAIL] mode=TEST to=<the 4 test inboxes> bcc= subject=[TEST pour l141.new.member@example.com] Votre coach l141-Coach vous a ajouté sur Mentora
```

First line is case 1 (case e, coach A), second is case 3 (case d, coach B). The mail goes through `RoutingEmailSender` like every mail (TEST mode: the 4 test inboxes, original recipient in the subject prefix).
### Case 10b: mail with the install link (`MemberInvite:InstallUrl` set)

API restarted (Development, `Email__Provider=Logging`, single API process, port 5243 checked free) with `MemberInvite__InstallUrl` set to an `https://example.com/...` test URL for that process only.

- Startup line: `[INVITE] installUrl=True permitLimit=30 windowMinutes=60` (the URL is not printed; 0 startup line contains it).
- Coach A adds `l141.link.member@example.com` (first name `Link`, last name `Member`): **201**, `isNewAccount true`, `isPrimary true`.
- `[EMAIL]` line of that mail: `[EMAIL] mode=TEST to=<4 test recipients> bcc= subject=[TEST pour l141.link.member@example.com] Votre coach Federico vous a ajouté sur Mentora`
- Counts only, lines written after that call (the Logging sender writes the text body, so the text body is counted without being printed):
  - lines containing the install URL: 1 (expected at least 1)
  - lines containing "Installer l'application": 1
  - lines containing "Votre coach vous enverra le lien": 0 (expected 0)
- API stopped, port 5243 free.

Together with case 10 (no URL set, the no-link sentence is used), both branches of 14.A8 ran. The HTML variant (colored border on a white background) is not rendered in this evidence: the Logging sender logs the text body only.

Mail failure path, proven by reading the code (`CoachMemberInviteService.cs`): the mail is sent at `:125`, after `tx.CommitAsync` (`:122`). `SendMemberAddedMailAsync` (`:133`) wraps everything in a try/catch (`:165`): an exception logs one warning with the exception type, the member id and the coach id (no address, no body). A `false` result of `IEmailSender.SendAsync` logs one warning (`:161`). Neither changes the returned 201.

### Case 11: login of the new member

- OTP request typed `L141.NEW.Member@EXAMPLE.com`, no `isCoach`: **200** `{"success":true,"data":null,"error":null,"statusCode":200}`. Verify with the code read silently from the log: **200**, keys `accessToken,refreshToken,expiresIn,isCoach`, `isCoach false`.
- SQL read before the first login: `MEMBER_HAS_ACTIVATED` false, activation date NULL. After: `MEMBER_HAS_ACTIVATED` true, activation date set (17:22:04.970975).
- Second login (same flow, 200 and 200): activation time unchanged (17:22:04.970975).

### Case 12: the new member in the existing coach routes (14.A11)

Existing routes that now return the new member (inventory A4), unchanged:

| Route | Returns the new member |
|---|---|
| `GET /api/v1/coach/me` | yes, in `members` (needs `MemberIsActive`, true here) |
| `GET /api/v1/coach/conversations` | yes, with `conversationId null` (no conversation yet) |
| `GET/PUT /api/v1/coach/members/{memberId}/parameters` | yes (404 only when not linked) |
| `GET /api/v1/coach/members/{memberId}/conversation` and `.../conversation/messages` | accepted for a linked member (not called, the first call creates a row) |
| `GET /api/v1/coach/agenda` | no (built from sessions and programs only) |

Proof, coach A:

```
GET /api/v1/coach/me -> 200, entry: {"memberId":"da0c5e94-560b-4ae8-b15b-f063a525dff0","firstName":"Lea","lastName":"Martin","email":"l141.new.member@example.com","phone":null}
GET /api/v1/coach/conversations -> 200, entry: {"conversationId":null,"memberId":"da0c5e94-...","memberFirstName":"Lea","memberLastName":"Martin","unreadCount":0,"lastMessage":null,"lastMessageDate":null}
GET /api/v1/coach/members/{memberId}/parameters -> 200 as coach A and as coach B (both linked)
```

### Case 13: startup refusal

`ASPNETCORE_ENVIRONMENT=Production`, `dotnet run --no-build --no-launch-profile`, fake connection string and generated random secrets set in the process environment, `MemberInvite__InstallUrl=http://x`: the process exits at startup, before any migration, with
`Microsoft.Extensions.Options.OptionsValidationException: MemberInvite:InstallUrl must be an absolute https URL.`
The value `http://x` appears nowhere in the output. 0 migration line.

### Case 14: OTP request and verify contract, before and after

Captured with the same script (`member@mentora.fr`, an existing member) from the build before any edit, then from the new build. Raw captures: `contract-before.txt` and `contract-after.txt` (keys only: 0 JWT pattern, no token value).

| Request | Before | After |
|---|---|---|
| request, exact case | 200 `{"success":true,"data":null,"error":null,"statusCode":200}` | identical |
| request `isCoach: true` on a member | 400 `No coach account for this email.` | identical |
| request unknown email | 400 `User not found or account is disabled.` | identical |
| verify wrong code | 400 `Invalid or expired OTP.` | identical |
| verify unknown email | 400 `User not found or account is disabled.` | identical |
| verify good code | 200, keys `success,data,error,statusCode` / data `accessToken,refreshToken,expiresIn,isCoach`, `isCoach false`, `expiresIn 3600` | identical |
| request with capitals (`MEMBER@Mentora.FR`) | **400** `User not found or account is disabled.` | **200** (same body as the exact-case request) |

**The capitals row is the only change of the OTP contract.** It is intended (14.A3: trim then lower invariant, lookups on `lower("USER_EMAIL")`). Every exact-case request stays identical in code and body.

## Cleanup

- `cleanup-dryrun.sql` (BEGIN ... ROLLBACK), scope: USERS with email starting `l141` AND created after the run start, their cascades, plus the OTPs and refresh tokens created after the run start by the two pre-existing users. Dry run output: 9 l141 users, 2 pre-existing users kept; `DELETE 14` (OTPs), `DELETE 7` (refresh tokens), `DELETE 9` (users); leftovers per table with a `*_CREATED_DATE` column: 0 row; 2 users left in total.
- Rows present before the cleanup (per table, created after the run start): AUTH_OTPS 20, AUTH_REFRESH_TOKENS 12, COACHES 1, COACH_PARAMETERS 1, MEMBERS 7, OFFER_PROGRAMS 4, USERS 9. MEMBER_COACHES has no created date column: its l141 rows go by cascade.
- The dry run was re-run after case 10b (the counts above are the first dry run, before case 10b). Second dry run: 10 l141 users, 2 pre-existing users kept; `DELETE 15` (OTPs), `DELETE 8` (refresh tokens), `DELETE 10` (users); leftovers 0 row; 2 users left.
- `cleanup-commit.sql`: diff with the dry run is the last line only (`ROLLBACK;` replaced by `COMMIT;`), hunk shown below. Run once, output `DELETE 15`, `DELETE 8`, `DELETE 10`, `COMMIT`.

```
-ROLLBACK;
+COMMIT;
```

### Final leftover check (after the COMMIT, separate read-only session)

- Every table with a `*_CREATED_DATE` column, rows created after the run start (2026-11-19 21:46:18 UTC): **0 row**. No accepted pre-existing exception was needed.
- Residue by name: 0 USERS with an email starting `l141`, 0 MEMBERS with a first name starting `l141`, 0 COACHES with a first name starting `l141`.
- Totals after cleanup: 2 USERS (the two pre-existing ones), 2 MEMBER_COACHES (pre-existing).

## Address checks (file names and counts only)

Patterns: `@[A-Za-z0-9._-]*gmail[.]com`, `web2success[.]fr`, `mentorapp[.]fr`. Witnesses that must find something: `docs/review/lot11/11.4/runtime-evidence.md` (gmail, 1 hit), `docs/review/lot8/vps/cleanup-vps.sql` (web2success 6 hits, mentorapp 6 hits).

- The files to commit (code and evidence): 0 hit for each of the three patterns.
- Files under `docs/review/lot14/`: 0 hit for gmail and web2success; **1 accepted hit** for `mentorapp[.]fr`: `docs/review/lot14/14.0/inventory.md:250`, the public API host in the admin nginx CSP rule (`connect-src`), already committed. Accepted as pre-existing, the file is not edited.
