# Lot 14.8a: runtime evidence, admin API to list and create coaches

- Date: 2026-11-23. Branch `feature/lot_14`. Local API on port 5243, `ASPNETCORE_ENVIRONMENT=Development`, `Email__Provider=Logging`, mail routing mode TEST.
- No token, OTP code, refresh token, JWT, connection string or real address appears in this file. OTP codes were read from the API log by a script into a variable and never printed (14.1 method). Tokens stayed in memory or in the gitignored `tokens.json`. `[EMAIL]` lines are given with the recipients replaced by counts.
- Fixtures use `l148-...@example.com`. The local fictional accounts `coach@mentora.fr` (coach and member profiles, "BOTH") and `member@mentora.fr` (member) appear.
- Run start read in the database before the first write: `2026-11-23 04:40:28.649562+00`.
- Build: `dotnet build --no-incremental`, 0 error, 19 warnings before and after, 0 new warning. `dotnet ef migrations has-pending-model-changes`: no change.

## Method

- API started from the built dll (`Mentora.API.dll`) with the log redirected to `docs/review/lot14/14.8a/api*.log` (gitignored by `*.log`). Several restarts, one log per start: `api.log` (first start, migration applied), `api-gamma.log` (with `MemberInvite__InstallUrl` for that process only), `api-sql.log` (EF command logging at Information for that process only), `api-main2.log` and `api-main3.log` (normal starts).
- Admin token through `/api/v1/admin/auth/otp/request` then `otp/verify` (code read silently from the `[EMAIL-LOG]` line). App coach token for `coach@mentora.fr` with `isCoach: true` (code read silently from the app OTP log line).
- HTTP calls from a PowerShell script (`HttpClient`), printing the status code and the body, never a token.
- SQL through `docker exec -i mentora-postgres-local sh -c 'psql ...'` with the SQL on stdin; reads inside `BEGIN READ ONLY; ... ROLLBACK;`.
- Mail bodies: the Logging provider logs the text body only. The text body is proved from the log with a script that prints booleans. The HTML body is proved by calling the internal builder through reflection from a throwaway console project kept outside the repository (scratchpad), printing booleans.

## Startup (4.4 b)

First start after the migration was written, expected lines present:

```
[EMAIL] routing mode=TEST provider=Logging testRecipients=4 productionBcc=0
[INVITE] installUrl=False permitLimit=30 windowMinutes=60
[AUTH] otpRateLimit permitLimit=30 windowMinutes=15
Applying migration '20261123042911_Lot14_8_CoachCreatedAudit'.
Now listening on: http://localhost:5243
Application started. Press Ctrl+C to shut down.
```

`fail:` 0, `crit:` 0. The `warn:` lines are the known EF model validation warnings plus the reference data seeders' `[CATALOG]` and `[QUIZ]` lines (see "Unexpected points").

## CHECK definitions (4.4 c, d, l)

| Moment | `CK_ADMIN_AUDIT_LOG_ACTION` | `CK_ADMIN_AUDIT_LOG_ENTITY_TYPE` |
|---|---|---|
| After the first start (migration applied) | 7 values, ends with `COACH_CREATED` | `ARRAY['EXERCISE','COACH']`, 2 values |
| After `dotnet ef database update 20261120193442_Lot14_2_OtpAttempts` (Down, no coach yet) | 6 values | 1 value (`'EXERCISE'`) |
| After `dotnet ef database update` (Up again) | 7 values | 2 values |
| After the refused Down (l), one coach audit row present | 7 values | 2 values |

The single value form of the ENTITY_TYPE CHECK is rendered by PostgreSQL as `= 'EXERCISE'::text`, so a counter of `::character varying` shows 0 for it. The definition was proved by replaying the Down SQL inside `BEGIN ... ROLLBACK`:

```
CHECK ((("ADMIN_AUDIT_LOG_ENTITY_TYPE")::text = 'EXERCISE'::text))
```

Down refusal (l), API stopped, 7 coach audit rows present: `dotnet ef database update 20261120193442_Lot14_2_OtpAttempts` exits with code 1:

```
Reverting migration '20261123042911_Lot14_8_CoachCreatedAudit'.
Npgsql.PostgresException (0x80004005): P0001: Lot14_8_CoachCreatedAudit Down refused: ADMIN_AUDIT_LOG holds COACH rows. Remove them first.
```

After it: the two CHECKs unchanged (7 and 2 values), `__EFMigrationsHistory` still ends with `20261123042911_Lot14_8_CoachCreatedAudit` (previous row `20261120193442_Lot14_2_OtpAttempts`).

## HTTP cases (4.4 f)

| # | Case | Result |
|---|---|---|
| 1 | GET list, no token | 401, empty body |
| 2 | GET list, app coach token | 401, empty body |
| 3 | GET `/api/v1/coach/me`, admin token | 401, empty body |
| 4 | GET list, admin | 200, 1 item (the existing local coach), `nextCursor` false, item keys `coachId,userId,email,firstName,lastName,phone,createdDate,isEnabled,isActive,memberCount` |
| 5a | `limit=0` | 400 `limit must be between 1 and 100 (got 0).` |
| 5b | `limit=101` | 400 `limit must be between 1 and 100 (got 101).` |
| 5c | `cursor=abc` | 400 `Invalid cursor format.` |
| 6 | POST `l148-alpha@example.com`, names, no phone | 201, `phone` null, `isEnabled` true, `isActive` true, `memberCount` 0. USERS 2 to 3 |
| 7 | POST the same email | 409 `error` `EMAIL_TAKEN`, `details` `{"existingAccount":"COACH","isEnabled":true}`. USERS stays 3 |
| 8 | POST ` L148-Alpha@Example.COM ` | 409, same body |
| 9 | POST `member@mentora.fr` | 409 `existingAccount` `MEMBER`, `isEnabled` true |
| 10 | POST `coach@mentora.fr` | 409 `existingAccount` `BOTH`, `isEnabled` true. USERS stays 3 |
| 11 | POST `l148-beta@example.com`, phone `" +33 6 12 34 56 78 "` | 201, `phone` `+33 6 12 34 56 78`; SQL `COACH_PHONE` = `+33 6 12 34 56 78` (trimmed) |
| 12a | empty email | 422, key `Email` |
| 12b | `not-an-email` | 422, key `Email` |
| 12c | blank first name | 422, key `FirstName` |
| 12d | phone of 25 characters | 422, key `Phone` |
| 13 | `limit=1` | page 1: 1 item (beta, newest) and a `nextCursor`; page 2 with that cursor: 1 item (alpha), no duplicate, `createdDate` descending. Full walk with `limit=1`: 3 items, 3 distinct ids, sorted descending |
| 14 | Swagger | `/api/v1/admin/coaches` present in the `admin` doc (13 paths, 1 match), absent from `mobile` (34 paths), `coach` (50) and `internal` (5): 0 match each |

After the 422 cases the USERS count was 4 (alpha and beta created, nothing else written by the 409 and 422 calls).

## Concurrency (4.4 g)

| Scenario | Result |
|---|---|
| g1: two parallel POSTs `l148-race1@example.com` and `L148-RACE1@EXAMPLE.COM` | one 201, one 409 `EMAIL_TAKEN`, 1 USERS row |
| g2: parallel admin creation and `POST /api/v1/coach/members` (coach `coach@mentora.fr`) for `l148-race2@example.com` | admin 201, member add 409 `EMAIL_IS_COACH`, 1 USERS row (1 coach profile, 0 member profile), no 500 |
| g3: same with `l148-race3@example.com` | admin 201, member add 409 `EMAIL_IS_COACH`, 1 USERS row (1 coach profile, 0 member profile), no 500 |

In g2 and g3 the admin creation won both times. The other order (member add first, then the coach creation answering 409 `MEMBER`) was not observed in these two runs; the 409 `MEMBER` branch itself is proved by case 9.

## SQL read for `l148-alpha` (4.4 h)

Results (columns abridged to the relevant ones):

- USERS: `USER_EMAIL` `l148-alpha@example.com` (lower case), `USER_IS_ENABLED` true, `USER_ROLE` `COACH`, `USER_MODIFICATION_DATE` null.
- COACHES: `COACH_IS_ACTIVE` true, `COACH_OFFERS_VISIO` false, `COACH_OFFERS_IN_PERSON_SOLO` false, `COACH_IN_PERSON_LOCATIONS` `[]`, `COACH_PHONE` null.
- COACH_PARAMETERS: hourly rate `50.00`, cancellation delay `24`, language `FR`, notif messages true, notif new booking true, notif booking cancelled true, notif marketing false, min booking notice 24, max booking horizon 90, late cancellation refunds false, accepting new bookings true, default session duration 60, missed session behavior `SKIP`.
- 4 OFFER_PROGRAMS, all active: `Perte de poids`, `Préparation Hyrox`, `Préparation Marathon`, `Prise de masse`.
- ADMIN_AUDIT_LOG: 1 row, action `COACH_CREATED`, entity type `COACH`, entity id equals `COACH_ID` (true), admin id present, changes:

```json
{"email": "l148-alpha@example.com", "phone": null, "userId": "f986fa50-f98c-4718-b736-8b45f737a7e9", "coachId": "56b7e077-da80-4536-9185-921c68ae8344", "isActive": true, "lastName": "Coach", "firstName": "Alpha", "offerPrograms": ["Prise de masse", "Perte de poids", "Préparation Marathon", "Préparation Hyrox"], "hourlyRateEuros": 50.00, "cancellationDelayHours": 24}
```

- All created dates equal: the 9 values (USERS, COACHES, COACH_PARAMETERS created and updated, 4 OFFER_PROGRAMS, ADMIN_AUDIT_LOG) are all `2026-11-23 04:59:19.533991+00`, and the `createdDate` of the 201 is `2026-11-23T04:59:19.533991Z` (same microsecond value, thanks to the truncation of `now`).

### SQL generated for the list (one query per page)

Captured with `Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command=Information` for that process only. The page 2 request (`limit=2` with a cursor) produced exactly 1 `Executed DbCommand`:

```sql
SELECT t."COACH_ID", t."USER_ID", u."USER_EMAIL", t."COACH_FIRST_NAME", t."COACH_LAST_NAME", t."COACH_PHONE", t."COACH_CREATED_DATE", u."USER_IS_ENABLED", t."COACH_IS_ACTIVE", (
    SELECT count(*)::int
    FROM "MEMBER_COACHES" AS m
    INNER JOIN "MEMBERS" AS m0 ON m."MEMBER_ID" = m0."MEMBER_ID"
    WHERE t."COACH_ID" = m."COACH_ID" AND m0."MEMBER_IS_ACTIVE")
FROM (
    SELECT c."COACH_ID", c."COACH_CREATED_DATE", c."COACH_FIRST_NAME", c."COACH_IS_ACTIVE", c."COACH_LAST_NAME", c."COACH_PHONE", c."USER_ID"
    FROM "COACHES" AS c
    WHERE c."COACH_CREATED_DATE" < @__cursorDate_0 OR (c."COACH_CREATED_DATE" = @__cursorDate_0 AND c."COACH_ID" < @__cursorId_1)
    ORDER BY c."COACH_CREATED_DATE" DESC, c."COACH_ID" DESC
    LIMIT @__p_2
) AS t
INNER JOIN "USERS" AS u ON t."USER_ID" = u."USER_ID"
ORDER BY t."COACH_CREATED_DATE" DESC, t."COACH_ID" DESC
```

The member count is a correlated subquery (no N+1). `limit + 1` is passed as the parameter `@__p_2`.

## Mail (4.4 i)

`[EMAIL]` lines after the creations (recipients replaced by counts):

| # | Line |
|---|---|
| 1 to 3 | `mode=TEST toCount=4 bccCount=0`, admin code mails and the coach app code mail (not welcome mails) |
| 4 | `mode=TEST toCount=4 bccCount=0 subject=[TEST pour <addr>] Alpha, votre compte coach Mentora est prêt` |
| 5 | same, `Beta, votre compte coach ...` |
| 6 to 8 | same, `Race, votre compte coach ...` (race1 winner, race2, race3) |

Exactly one welcome mail per 201 (5 in `api.log`), none for the 409 and 422 calls. Each mail is routed to the 4 test recipients, no bcc.

Text body, booleans per welcome mail (5 mails, no install link): `subjectOk`, `greeting`, `intro`, `step1`, `step2` (with the coach address), `step3`, `noSignUp`, `footer`, `fallback` are all true; `buttonText` and `urlPresent` are false.

With `MemberInvite__InstallUrl=https://example.com/beta` for that process only (`[INVITE] installUrl=True`), `l148-gamma@example.com` created (201): the `[EMAIL]` line has `toCount=4 bccCount=0`, subject `Gamma, votre compte coach Mentora est prêt`; all fixed sentences true, `buttonText` true, `urlPresent` true (`Installer l'application : https://example.com/beta`), `fallback` false.

HTML body (reflection on `CoachWelcomeEmailBuilder`, 4 combinations: link or not, first name or not): greeting, intro, three steps (the address in `<strong>`, the `« Connexion »` sentence), no-sign-up sentence and footer present; button label and `href` present only with the link; fallback sentence present only without it; no white text (`color: #fff` or `white`) anywhere. `WebUtility.HtmlEncode` writes accented letters and apostrophes as numeric entities (same as the member mail), so the checks compare against the encoded text.

Empty first name: the subject is `Votre compte coach Mentora est prêt` and the greeting `Bonjour,` (proved by execution in the 2 combinations without first name, and by reading `CoachWelcomeEmailBuilder.BuildSubject`).

The API was then restarted without the variable (`[INVITE] installUrl=False` in `api-sql.log`, `api-main2.log`, `api-main3.log`).

## Login of a new coach without restarting the API (4.4 j)

Fresh coach `l148-delta@example.com` created (201), then, with no restart in between:

- `POST /api/v1/auth/otp/request` without `isCoach`: 200.
- `POST /api/v1/auth/otp/verify` without `isCoach`: 200, keys `accessToken,refreshToken,expiresIn,isCoach`, `isCoach` true.
- `GET /api/v1/coach/me` with that token: 200, `activeMembers` `[]`, `upcomingSessions` `[]`, `offers` `[]`, `profile.hourlyRateEuros` 50.00, `profile.cancellationDelayHours` 24, every stat at 0 (`appointmentsThisMonth`, `revenueThisMonth`, `sessionsSoldThisMonth`, `revenuePrevMonth`, `sessionsSoldPrevMonth`, `activeMembersCount`, `revenueTotal`).

This proves COACH_PARAMETERS is created by the creation transaction itself, not by a seeder at the next start.

## Seeders on restart (4.4 k)

`api-sql.log`, `api-main2.log` and `api-main3.log` (all started after coaches were created): 0 `CoachParameterSeeder: created` line, 0 `OfferProgramSeeder: seeded` line, `fail:` 0, `crit:` 0. A read after the last start shows 0 coach without a COACH_PARAMETERS row. Other seeder lines (`CatalogSeeder`, `QuizQuestionSeeder`, `ExerciseSeeder`, `MuscleGroupSeeder`) report `created 0`.

## End state (4.4 m)

API stopped, port 5243 free, no API process left.

## Counts before the cleanup

Rows created after the run start, per table (42 tables have a `*_CREATED_DATE` column, only these are non zero): ADMIN_AUDIT_LOG 7, ADMIN_OTPS 3, ADMIN_REFRESH_TOKENS 2, AUTH_OTPS 3, AUTH_REFRESH_TOKENS 3, COACHES 7, COACH_PARAMETERS 7, OFFER_PROGRAMS 28, USERS 7. The 7 coaches are alpha, beta, race1, race2, race3, gamma and delta. MEMBER_COACHES has no created date column and gets no row (no member was created).

## Cleanup dry run (4.6)

`cleanup-dryrun.sql`, `BEGIN ... ROLLBACK`, bounded by the run start. Output:

| Step | Result |
|---|---|
| ADMIN_AUDIT_LOG rows of type COACH for the l148 coaches | DELETE 7 |
| l148 USERS created after the start (cascades) | DELETE 7 |
| AUTH_OTPS and AUTH_REFRESH_TOKENS of `coach@mentora.fr` created after the start | DELETE 2, DELETE 2 |
| ADMIN_OTPS and ADMIN_REFRESH_TOKENS created after the start | DELETE 3, DELETE 2 |
| Leftovers created after the start, 42 tables scanned | 0 |
| USERS left | 2 (the 2 pre-existing users) |
| l148 USERS left | 0 |

The AUTH_OTPS of `l148-delta` (1 row) and its refresh token go with the user by cascade. After the rollback the 7 l148 users are still in the database (nothing was written by the dry run).

## Cleanup (commit)

`cleanup-commit.sql` is `cleanup-dryrun.sql` with the last line `COMMIT;` instead of `ROLLBACK;` (diff ignoring line endings: 1 line). Run after the dry run, output:

| Step | Result |
|---|---|
| ADMIN_AUDIT_LOG rows of type COACH for the l148 coaches | DELETE 7 |
| l148 USERS created after the start (cascades) | DELETE 7 |
| AUTH_OTPS and AUTH_REFRESH_TOKENS of `coach@mentora.fr` created after the start | DELETE 2, DELETE 2 |
| ADMIN_OTPS and ADMIN_REFRESH_TOKENS created after the start | DELETE 3, DELETE 2 |
| Leftovers created after the start, 42 tables scanned | 0 |
| USERS left | 2 |
| l148 USERS left | 0 |
| Transaction | COMMIT |

Leftover scan run again afterwards as a separate `BEGIN READ ONLY` query: 42 tables scanned, leftovers total 0, no table with a non zero count, 2 users left, 0 l148 user, 0 ADMIN_AUDIT_LOG row of type COACH.

## Unexpected points

1. **First start attempt failed before the API started**: my launcher passed a relative path to `dotnet`, the process exited at once ("command or file not found"). Nothing reached the API, the migration was not applied by that attempt. Fixed in the launcher (absolute path), not an API issue.
2. **`warn:` lines other than the EF model warnings.** Each start logs 129 `warn:` lines: the known `Microsoft.EntityFrameworkCore.Model.Validation[20601]` lines plus 114 `Mentora.Infrastructure.Persistence.MentoraDbContext[0]` lines, which are the `[CATALOG] group mismatch` / `[CATALOG] exercise EX135 ...` and `[QUIZ] 74 placeholder rows ... skipped` messages of the reference data seeders. They are not related to 14.8a and are documented as known pre-existing log noise (`docs/review/lot13bis/vps/vps-evidence.md:44`, `docs/review/lot9/9.3/evidence.md`). They were not in the list of known warnings of the instructions, so this is flagged here; the run continued.
3. **Scripts**: the first token script run failed on a file lock (the log is held open by the API; reading with a shared lock fixed it) and left one extra admin OTP row, which the cleanup covers (ADMIN_OTPS 3 instead of 2).
4. **Admin token expired** during the run (401 on the first list call after about 40 minutes); a new token was requested. Not an API issue.
5. **Single value CHECK after the Down**: my counter showed 0 values for `CK_ADMIN_AUDIT_LOG_ENTITY_TYPE` (PostgreSQL renders one value as `= 'EXERCISE'::text`). The definition was proved by a rolled back replay (see above).
6. **HTML body proof**: the Logging provider does not log the HTML body, so it is proved by calling the builder through reflection from a throwaway console project (outside the repository). A first version reported a false negative on "no white text" because `background-color: #ffffff` contains `color: #fff`; the regex was corrected.
7. **Race order**: in g2 and g3 the admin creation won both times (see Concurrency).
8. **`createdDate` precision** (review point 3): `now` is truncated to whole microseconds, so the 201, the rows and the audit row carry the same value (proved in the SQL read).

## Backlog

- Phone rule: the pattern `^[0-9+\-.() ]{8,20}$` is applied to the raw value, before trim, for members and now for coaches. A 20 character number surrounded by spaces is refused although its trimmed length is valid. Same behaviour as the member add; to be reviewed for both together.
- No way to resend the welcome mail (the 201 does not say whether it went out, decided).
- The `[CATALOG]` and `[QUIZ]` seeder warnings (known noise) hide any new `warn:` in the startup log.
