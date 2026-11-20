# Lot 14.2 runtime evidence: protect the app login codes (OTP attempts, atomic consumption, IP rate limit)

- Date: 2026-11-20 (local run)
- Branch: feature/lot_14, base 4783a87
- Environment: local API (Development, `Email__Provider=Logging`, port 5243 free before each start, single API process), local PostgreSQL (port 5433).
- No token, OTP code, password or connection string appears in this file. OTP codes were read from the API log into variables by the scripts and never printed. No `[EMAIL-LOG]` line is quoted.
- Run start date, read in the database BEFORE the first write: **2026-11-20 19:26:05 UTC** (used by the cleanup).

## Change

- `AUTH_OTPS.AUTH_OTP_ATTEMPT_COUNT` (integer NOT NULL default 0) and named CHECK `CK_AUTH_OTPS_ATTEMPT_COUNT` (`>= 0`), migration `Lot14_2_OtpAttempts` (CHECK added by hand through `migrationBuilder.Sql`, with its Down). Entity initializer `= 0` matches `HasDefaultValue(0)`.
- `AuthService`: constant `OtpMaxAttempts = 5`; candidates are unused, not expired and below 5 attempts; a wrong code increments every loaded candidate in one atomic `ExecuteUpdateAsync`; a good code is consumed by an atomic `ExecuteUpdateAsync`, and consumption, 14.A10 activation and refresh token creation run in one transaction. 0 row affected = answered like a wrong code. Earlier unused codes are still invalidated by a new request (`AuthService.cs:48-53`, unchanged; 14.C5 as amended).
- API: policy `AppAuthOtp` (fixed window per `RemoteIpAddress` after `UseForwardedHeaders`, `"no-ip"` fallback) on `POST otp/request` and `POST otp/verify` only (one shared window per IP). Options `AuthRateLimit` (`PermitLimit` default 30, 1 to 1000; `WindowMinutes` default 15, 1 to 1440) validated at startup. Not on token refresh nor logout. Shared 429 envelope and `Retry-After` (existing `OnRejected`).
- No NuGet package. Admin auth, refresh flow and the `[OTP]` console line are untouched.

## Build

`dotnet build Mentora.sln --no-incremental`: 0 error, 19 warnings (same count as the 14.1b baseline, none in the changed files).

## Migration

First start log (order kept): `[AUTH] otpRateLimit ...` line, then `Applying migration '20261120193442_Lot14_2_OtpAttempts'.` with
```
ALTER TABLE "AUTH_OTPS" ADD "AUTH_OTP_ATTEMPT_COUNT" integer NOT NULL DEFAULT 0;
ALTER TABLE "AUTH_OTPS" ADD CONSTRAINT "CK_AUTH_OTPS_ATTEMPT_COUNT" CHECK ("AUTH_OTP_ATTEMPT_COUNT" >= 0);
```
Then: column `integer | NOT NULL | default 0`; the 131 existing rows all have 0 (`count 131, min 0, max 0`). Second start: 0 migration line, `[AUTH]` line present.

### CHECK proof (BEGIN ... ROLLBACK)

```
BEGIN; UPDATE "AUTH_OTPS" SET "AUTH_OTP_ATTEMPT_COUNT" = -1 ...;   ERROR: new row for relation "AUTH_OTPS" violates check constraint "CK_AUTH_OTPS_ATTEMPT_COUNT"
BEGIN; UPDATE "AUTH_OTPS" SET "AUTH_OTP_ATTEMPT_COUNT" = NULL ...; ERROR: null value in column "AUTH_OTP_ATTEMPT_COUNT" of relation "AUTH_OTPS" violates not-null constraint
```
Both rolled back; rows with a non-zero count afterwards: 0.

## Generated SQL

See `sql-extract.txt` (SQL text only, parameters `'?'`). Atomic increment: `UPDATE "AUTH_OTPS" ... SET count = count + 1 WHERE id = ANY(...) AND NOT used AND count < 5`. Atomic consumption: `UPDATE ... SET "AUTH_OTP_IS_USED" = TRUE WHERE id = ? AND NOT used AND count < 5 AND expiration > now()`.
In Development EF command logging is on by default (appsettings level), so the extract comes from the default log; no logging override variable was ever set.

## Fixture and run settings

- Coach A is `coach@mentora.fr`. That account also has a member profile, so its OTP calls need `isCoach=true` (a first attempt without it gave 409, harmless, no row written).
- Coach A added `l142.member@example.com` through `POST /api/v1/coach/members`: **201**, `isNewAccount true`; `MEMBER_HAS_ACTIVATED` false before the first login. Also used: `member@mentora.fr`.
- Cases 1 to 7 ran on an API started with `AuthRateLimit__PermitLimit=1000` for that process only (the cases make far more than 30 OTP calls from one IP). Case 8 and the final start show the real values.

## Cases (HTTP code and body, no token, no code)

| # | Scenario | Result |
|---|---|---|
| 1 | 2 wrong codes then the good code | **400, 400, 200** (200 body keys `success,data,error,statusCode`, data `accessToken,refreshToken,expiresIn,isCoach`, `isCoach false`). SQL: consumed row `used t`, `attempts 2` |
| 2 | 5 wrong codes, then the GOOD code of that request | **400 x5, then 400** `Invalid or expired OTP.`. SQL: `used f`, `attempts 5` |
| 3 | after case 2, new OTP request then its good code | request **200**, verify **200**. SQL: new row `used t`, `attempts 0` |
| 4 | two simultaneous verify, same good code | **one 200, one 400**. SQL: code `used t`, `attempts 0`; refresh tokens created for the user in that window: **1** |
| 5 | 10 simultaneous wrong verify on one fresh code | **400 x10**, a single body (`Invalid or expired OTP.`). SQL: `attempts 5` exactly, `used f` |
| 6 | two requests in a row, 1 wrong verify | after the 2 requests: first code `used t, attempts 0`, second `used f, attempts 0` (first invalidated by the second request). After 1 wrong verify: first `0`, second `1` (only the second is counted). First code: **400** (this call is itself a wrong attempt against the second code, count 2). Second code: **200**, `used t, attempts 2` |
| 7 | first login of `l142.member@example.com` (14.A10) | request **200**, verify **200**; `MEMBER_HAS_ACTIVATED true`, `MEMBER_ACTIVATION_DATE 2026-11-20 19:52:34+00`; its code `used t, attempts 0` |

### Case 8: rate limit (API restarted with `AuthRateLimit__PermitLimit=3`, that process only)

`[AUTH] otpRateLimit permitLimit=3 windowMinutes=15`. Calls from the same IP (127.0.0.1):

| Call | Result |
|---|---|
| `otp/request` #1, #2, #3 | **200** |
| `otp/request` #4 | **429**, `Retry-After: 900`, body `{"success":false,"data":null,"error":"Too many requests.","statusCode":429}` |
| `otp/verify` #5 (same window) | **429**, `Retry-After: 900`, same envelope |
| `token/refresh` x5 right after, window already exhausted | **401** each time (`Invalid or expired refresh token.`), never 429 |

Both OTP actions carry `[EnableRateLimiting("AppAuthOtp")]` and the policy key is the IP only, so they share one window per IP; the 5th call above proves it. Refresh and logout carry no attribute.
Restart without the override: `[AUTH] otpRateLimit permitLimit=30 windowMinutes=15`.

### Case 9: startup refusal

`AuthRateLimit__PermitLimit=0`: the process exits at startup with `OptionsValidationException: AuthRateLimit:PermitLimit must be between 1 and 1000.`; no "Now listening", no `[AUTH]` line, no migration line, no database command in its log.

### Case 10: contract unchanged

`contract-before.txt` (pre-edit build) and `contract-after.txt` (new build), same script (`member@mentora.fr`): `diff` reports **identical** (7 lines). The capitals row answers 200 in both (14.1 behaviour).

## Cleanup

- `cleanup-dryrun.sql` (ROLLBACK) bounded by the `l142` prefix AND the run start date, plus OTPs and refresh tokens created after the run start by the pre-existing users. Dry run: `l142` users **1**; OTPs of coach and member since run start **20**; their refresh tokens **8**; deleted 20 / 8 / 1; leftovers per table with a `*_CREATED_DATE` column after the run start: **0 row**; `MEMBER_COACHES` 2 and `USERS` 2 remain.
- `cleanup-commit.sql`: written separately; `diff` with the dry run changes only the last line (`ROLLBACK;` becomes `COMMIT;`).
- **COMMIT run** after approval. Real output: `l142_users 1`, `preexisting_users_kept 2`, `otps_preexisting_users 20`, `refresh_tokens_preexisting_users 8`, `DELETE 20`, `DELETE 8`, `DELETE 1`, leftovers per table `(0 rows)`, `member_coaches_total_after 2`, `users_left_total 2`, `COMMIT`.
- **Final leftover check** in a separate read-only session (`BEGIN READ ONLY ... ROLLBACK`), every table with a `*_CREATED_DATE` column, rows created after 2026-11-20 19:26:05: **0 row**. `l142` users: 0; users total: 2.
