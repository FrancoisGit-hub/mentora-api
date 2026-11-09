# Lot 12, sub-lot 12.1: admin authentication (12.A) and disabled user refresh check (12.D)

Branch `feature/lot_12`, base `9a62981`. Local run only (Docker PostgreSQL 16 on 5433, API on 5243, `ASPNETCORE_ENVIRONMENT=Development`, `Email__Provider=Logging`). No token, code, secret, connection string or address appears below: the admins are called admin #1 to admin #4 (order of `AdminAuth__Emails`), the app test accounts "the coach" and "the member". Every output was produced by a command; secrets stayed in variables, only lengths and booleans were printed. API logs are under `docs/review/lot12/12.1/*.log` (gitignored by `*.log`, not committed).

---

## 1. Preflight

| Step | Output |
|---|---|
| `git --no-optional-locks status` / `log --oneline -2` | clean; `9a62981 docs(lot12): 12.0 read-only inventory`, `2d1f843 docs(lot11): 11.7 inventory and runtime evidence` |
| `docker ps` | first attempt: no container (stopped at preflight, reported). After the container was recreated on its existing volume `repo-mentora_mentora_local_data`: `mentora-postgres-local \| Up \| 0.0.0.0:5433->5432/tcp, [::]:5433->5432/tcp` |
| Read-only SQL (`BEGIN READ ONLY … ROLLBACK`) | last migration `20261006092320_Lot11_7_RestFieldsJsonKeys`; `AUTH_OTPS` 88 rows; `USERS` 2 rows |
| `dotnet build --no-incremental` | `19 Avertissement(s)`, `0 Erreur(s)` (all CS1573, XML doc params) — baseline |
| `has-pending-model-changes` | `No changes have been made to the model since the last migration.`; 32 `warn` lines — baseline |
| Swagger baseline | first start refused: `EmailRouting:TestRecipients must contain at least one address.` (the session variables were not inherited by the tool shell; fixed by relaunching from the environment holding them). Second start: no `Applying migration`, `[EMAIL] routing mode=TEST provider=Logging testRecipients=4 productionBcc=0`. Saved mobile 123392 bytes, coach 304009 bytes, internal 6923 bytes under `swagger-before/`; API stopped, port 5243 free |

## 2. Decisions applied

D1 to D10 of the 12.1 prompt (version 2), unchanged:
- D1 admin accounts in `ADMIN_USERS` (never `USERS`), driven by `AdminAuth__Emails` at each startup (create, re-enable, disable + revoke), emails trimmed and lower case.
- D2/D3 6-digit code by mail through `IEmailSender` (11.4 routing), lifetime `AdminAuth:OtpLifetimeMinutes` (10), 5 wrong attempts invalidate it, a new request invalidates the previous codes; same 200 for any request, same 400 `Invalid or expired code.` for any verify failure.
- D4 HS256 access token, own key/issuer/audience, 15 min, claims `sub`, `adminId`, `email`, `jti` (+ `iat`, `nbf`, `exp`, `iss`, `aud`).
- D5 opaque refresh token `{id}:{secret}` (32 random bytes, BCrypt hash), 12 h, rotated, in the response body; reuse of a revoked token whose secret verifies revokes every live token of that admin; disabled admin → 401; every refresh failure → same 401 `Invalid or expired refresh token.`.
- D6 scheme `AdminBearer` (`MapInboundClaims = false`, `ClockSkew` zero, own key/issuer/audience, HS256 only); default `Bearer` options unchanged; policy `AdminOnly`.
- D7 rate limit policy `AdminAuth` per client IP on every `/api/v1/admin/auth` route, shared 429 envelope; no new limit on the app.
- D8 `AuthService.GenerateOtp` uses `RandomNumberGenerator.GetInt32(100_000, 1_000_000)`; nothing else changes in the app OTP flow (no column, no limit; the console OTP line stays).
- D9 (12.D) `POST /api/v1/auth/token/refresh` → neutral 401 when `USER_IS_ENABLED` is false.
- D10 4th Swagger doc `admin` with only `AdminBearer`; mobile, coach and internal unchanged.

## 3. Files

New:
- `Mentora.Core/Entities/AdminUser.cs`, `AdminOtp.cs`, `AdminRefreshToken.cs`
- `Mentora.Core/Options/AdminAuthOptions.cs` (section `AdminAuth`, `Validate()` without values)
- `Mentora.Core/Interfaces/IAdminAuthService.cs`
- `Mentora.Core/DTOs/Admin/Auth/AdminRequestCodeRequest.cs`, `AdminVerifyCodeRequest.cs`, `AdminRefreshRequest.cs`, `AdminLogoutRequest.cs`, `AdminAuthResponse.cs`, `AdminMeResponse.cs`
- `Mentora.Core/Validators/Admin/AdminAuthValidators.cs` (request code, verify code)
- `Mentora.Infrastructure/Persistence/Configurations/AdminUserConfiguration.cs`, `AdminOtpConfiguration.cs`, `AdminRefreshTokenConfiguration.cs`
- `Mentora.Infrastructure/Persistence/Migrations/20261108223153_Lot12_1_AdminAuth.cs` (+ `.Designer.cs`)
- `Mentora.Infrastructure/Seeding/AdminUserSeeder.cs`
- `Mentora.Infrastructure/Services/Admin/AdminAuthService.cs`
- `Mentora.API/Authorization/AdminAuthDefaults.cs`, `AdminAuthRateLimiting.cs`
- `Mentora.API/Controllers/Admin/AdminAuthController.cs` (`[Tags("Admin — Auth")]`, group `admin`)
- `Mentora.API/Swagger/SecuritySchemePerDocumentFilter.cs`

Modified:
- `Mentora.API/Program.cs`: 4th SwaggerDoc + endpoint, `AdminBearer` security definition, document filter instead of the global `AddSecurityRequirement`, `.AddJwtBearer("AdminBearer")` chained after the unchanged `Bearer` one (key/issuer/audience bound from `AdminAuthOptions` by a deferred `AddOptions<JwtBearerOptions>("AdminBearer")` so an empty secret fails in the validator, not in `SymmetricSecurityKey`), `AdminOnly` policy, options + validator + `ValidateOnStart`, `AddAdminAuthPolicy()`, eager admin check before migrations, `AdminUserSeeder` after `QuizQuestionSeeder`.
- `Mentora.API/appsettings.json`: `AdminAuth` section, non-secret defaults, `JwtSecret` and `Emails` empty.
- `Mentora.Infrastructure/Persistence/MentoraDbContext.cs`: 3 DbSets.
- `Mentora.Infrastructure/Persistence/Migrations/MentoraDbContextModelSnapshot.cs`: generated.
- `Mentora.Infrastructure/Services/AuthService.cs`: refresh lookup requires `r.User.UserIsEnabled`; `GenerateOtp` with `GetInt32`.
- `Mentora.Infrastructure/Services/Email/EmailOptionsValidators.cs`: `AdminAuthOptionsValidator`.

Build after the changes: `19 Avertissement(s)`, `0 Erreur(s)` (baseline). `has-pending-model-changes`: `No changes`, 32 `warn` lines (baseline, no new sentinel warning).

## 4. Migration `20261108223153_Lot12_1_AdminAuth`

- `ADMIN_USERS`: `ADMIN_USER_ID` uuid PK `gen_random_uuid()`, `ADMIN_USER_EMAIL` varchar(255) not null, `ADMIN_USER_IS_ENABLED` boolean not null, `ADMIN_USER_LAST_LOGIN_DATE` timestamptz null, `ADMIN_USER_CREATED_DATE` / `ADMIN_USER_UPDATED_DATE` timestamptz not null; unique `UX_ADMIN_USERS_EMAIL`; `CK_ADMIN_USERS_EMAIL_LOWERCASE`.
- `ADMIN_OTPS`: `ADMIN_OTP_ID` PK, `ADMIN_OTP_ADMIN_USER_ID` FK Restrict, `ADMIN_OTP_CODE_HASH` varchar(255), `ADMIN_OTP_EXPIRATION_DATE`, `ADMIN_OTP_IS_USED`, `ADMIN_OTP_FAILED_ATTEMPTS` integer, `ADMIN_OTP_CREATED_DATE`, all not null; `IX_ADMIN_OTPS_ADMIN_USER`; `CK_ADMIN_OTPS_FAILED_ATTEMPTS` (>= 0).
- `ADMIN_REFRESH_TOKENS`: `ADMIN_REFRESH_TOKEN_ID` PK, `…_ADMIN_USER_ID` FK Restrict, `…_HASH` varchar(255), `…_EXPIRATION_DATE`, `…_IS_REVOKED`, `…_REVOKED_DATE` null, `…_CREATED_DATE`; `IX_ADMIN_REFRESH_TOKENS_ADMIN_USER`.
- CHECKs created with `migrationBuilder.Sql`, dropped first in `Down`. No existing table changed (`AUTH_OTPS` columns after the run: `AUTH_OTP_ID, AUTH_OTP_CODE_HASH, AUTH_OTP_EXPIRATION_DATE, AUTH_OTP_IS_USED, AUTH_OTP_CREATED_DATE, USER_ID`).
- FK names are the EF-generated ones (`FK_ADMIN_OTPS_ADMIN_USERS_ADMIN_OTP_ADMIN_USER_ID`, `FK_ADMIN_REFRESH_TOKENS_ADMIN_USERS_ADMIN_REFRESH_TOKEN_ADMIN_~`, truncated by EF to the 63-character PostgreSQL limit), kept as approved.

---

## 5. Runtime proofs

### P1 Refusal to start (run before the migration was applied)

Each case: one start with a single override inside the command, `dotnet run --no-build`, wait for exit. `applyingMigration` = count of `Applying migration` in stdout+stderr, `listening` = count of `Now listening`.

```
p1-1-secret-missing | exited=True exit=-532462766 | applyingMigration=0 listening=0
    Unhandled exception. Microsoft.Extensions.Options.OptionsValidationException: AdminAuth:JwtSecret must be at least 32 characters.
p1-2-secret-short | exited=True exit=-532462766 | applyingMigration=0 listening=0
    Unhandled exception. Microsoft.Extensions.Options.OptionsValidationException: AdminAuth:JwtSecret must be at least 32 characters.
p1-3-secret-equal | exited=True exit=-532462766 | applyingMigration=0 listening=0
    Unhandled exception. System.InvalidOperationException: AdminAuth:JwtSecret must differ from Jwt:Secret.
p1-4-audience-equal | exited=True exit=-532462766 | applyingMigration=0 listening=0
    Unhandled exception. System.InvalidOperationException: AdminAuth:JwtAudience must differ from Jwt:Audience.
p1-5-emails-empty | exited=True exit=-532462766 | applyingMigration=0 listening=0
    Unhandled exception. Microsoft.Extensions.Options.OptionsValidationException: AdminAuth:Emails must contain at least one address.
p1-6-email-invalid | exited=True exit=-532462766 | applyingMigration=0 listening=0
    Unhandled exception. Microsoft.Extensions.Options.OptionsValidationException: AdminAuth:Emails item #2 is not a valid email address.
p1-7-email-duplicate | exited=True exit=-532462766 | applyingMigration=0 listening=0
    Unhandled exception. Microsoft.Extensions.Options.OptionsValidationException: AdminAuth:Emails item #2 is a duplicate (case insensitive).
port 5243 in use: False
```

Notes: case 1 removes `AdminAuth__JwtSecret` from the process (appsettings value `""` applies). Case 3 sets `Jwt__Secret` and `AdminAuth__JwtSecret` to the same freshly generated 48-character value. Case 4 sets `Jwt__Audience` and `AdminAuth__JwtAudience` to the same value. Case 5: PowerShell removes a variable assigned `""`, so "empty" is the unset variable over the empty appsettings value. Case 7 is admin #1 followed by admin #1 in upper case. Exit code -532462766 = 0xE0434352 (unhandled .NET exception).

### P2 Valid start and account synchronisation

```
p2-1-first-start
[EMAIL] routing mode=TEST provider=Logging testRecipients=4 productionBcc=0
[ADMIN] auth emails=4 accessMinutes=15 refreshHours=12
Applying migration '20261108223153_Lot12_1_AdminAuth'.
[ADMIN] accounts listed=4 created=4 reenabled=0 disabled=0
Now listening on: http://localhost:5243

p2-2-second-start
[ADMIN] accounts listed=4 created=0 reenabled=0 disabled=0

p2-3-first-three   (AdminAuth__Emails = first three items, built inside the command)
[ADMIN] auth emails=3 accessMinutes=15 refreshHours=12
[ADMIN] accounts listed=3 created=0 reenabled=0 disabled=1

p2-4-four-again
[ADMIN] auth emails=4 accessMinutes=15 refreshHours=12
[ADMIN] accounts listed=4 created=0 reenabled=1 disabled=0
```

Between p2-3 and p2-4 one extra start ran with the three-email override still set in the same shell (`listed=3 created=0 reenabled=0 disabled=0`, a no-op); it was stopped and p2-4 rerun in a fresh shell.

Read-only SQL after p2-4:

```
 MigrationId: 20261108223153_Lot12_1_AdminAuth
 admins | enabled | not_lower
      4 |       4 |         0
 conname: CK_ADMIN_OTPS_FAILED_ATTEMPTS, CK_ADMIN_USERS_EMAIL_LOWERCASE
 users: 2
```

### P3 Admin login (admin #1)

```
request known admin #1 -> 200 error=null
code received: True (length 6)
request unknown address -> 200 error=null
unknown body identical to known body: True
new admin code logged for the unknown address: False
verify wrong code #1 -> 400 error="Invalid or expired code."
verify wrong code #2 -> 400 error="Invalid or expired code."
verify wrong code #3 -> 400 error="Invalid or expired code."
verify wrong code #4 -> 400 error="Invalid or expired code."
verify wrong code #5 -> 400 error="Invalid or expired code."
verify right code after 5 failures -> 400 error="Invalid or expired code."
 attempts | is_used
        5 | t
new request admin #1 -> 200 error=null
new code differs from the first: True
verify right code -> 200 error=null
response keys: success,data,error,statusCode
data keys: accessToken,expiresIn,refreshToken,refreshExpiresIn,admin | admin keys: adminUserId,email
expiresIn=900 refreshExpiresIn=43200
access token length=451 alg=HS256 typ=JWT
iss=mentora-admin aud=mentora-admin
claim names: sub,adminId,email,jti,iat,nbf,exp,iss,aud
exp-iat=900 sub equals adminId: True admin.adminUserId equals adminId: True
refresh token length=81 shape {guid}:{secret}: True
```

The code was read from the Logging provider line of the API log into a variable with a regex, never printed. The wrong codes are 6-digit values different from the real one (format errors would be 422, not tested here).

### P4 Scheme separation

```
app request coach (isCoach true) -> 200 error=null
app verify coach -> 200 error=null
GET admin/auth/me with admin token -> 200 error=null
GET admin/auth/me without token -> 401 error=null
GET admin/auth/me with coach app token -> 401 error=null
GET coach/exercises with admin token -> 401 error=null
GET coach/exercises with coach token -> 200 error=null
POST quiz/basic/sessions with admin token -> 401 error=null
POST auth/logout with admin token -> 401 error=null
```

(`error=null` on the scheme 401s: the framework challenge has no body, as for every app 401 from the JWT handler.)

### P5 Refresh, logout, disabled admin

```
refresh with RT1 -> 200 error=null
refresh token changed: True | access token changed: True
GET me with the new access token -> 200 error=null
refresh with RT1 again (reuse) -> 401 error="Invalid or expired refresh token."
 active_tokens | total_tokens      (admin #1)
             0 |            2
refresh with RT2 (revoked by the reuse) -> 401 error="Invalid or expired refresh token."
log in again -> 200 error=null
logout with RT3 -> 200 error=null
refresh with RT3 after logout -> 401 error="Invalid or expired refresh token."
logout with a made-up token -> 200 error=null
logout made-up body identical to real logout body: True
refresh with a malformed token -> 401 error="Invalid or expired refresh token."
refresh with a made-up token -> 401 error="Invalid or expired refresh token."
401 bodies identical (malformed, made-up, reuse, after logout): True
admin #4 login -> 200 error=null
admin #4 GET me before restart -> 200 error=null
(restart with the first three emails: [ADMIN] accounts listed=3 created=0 reenabled=0 disabled=1)
admin #4 refresh after restart with three emails -> 401 error="Invalid or expired refresh token."
admin #4 GET me with its still valid access token -> 401 error="Admin account not found or disabled."
admin #4 access token still unexpired: True
```

### P6 App login and 12.D

```
app request member -> 200 error=null
app verify member -> 200 error=null
app response data keys: accessToken,refreshToken,expiresIn,isCoach isCoach=False
refresh token received: True (length 81)
 USER_IS_ENABLED | USER_MODIFICATION_DATE      (read-only, before)
 t               | (null)
```

Write 1 (approved), user `874d7734-cc4f-47ee-9527-3a4c0262f7b3`:

```sql
BEGIN;
DO $$ DECLARE n int; BEGIN
  UPDATE "USERS" SET "USER_IS_ENABLED" = false
  WHERE "USER_ID" = '874d7734-cc4f-47ee-9527-3a4c0262f7b3' AND "USER_IS_ENABLED" = true AND "USER_MODIFICATION_DATE" IS NULL;
  GET DIAGNOSTICS n = ROW_COUNT; RAISE NOTICE 'rows updated: %', n;
  IF n <> 1 THEN RAISE EXCEPTION 'expected 1 row, got %', n; END IF;
END $$;
COMMIT;
-- NOTICE:  rows updated: 1 / COMMIT
```

```
 USER_IS_ENABLED: f
app refresh, disabled member, real token -> 401 error="Invalid or expired refresh token."
app refresh, made-up token -> 401 error="Invalid or expired refresh token."
401 bodies identical: True
```

Write 2 (approved): same statement with `SET "USER_IS_ENABLED" = true … AND "USER_IS_ENABLED" = false …` → `NOTICE: rows updated: 1`, `COMMIT`. Read again:

```
 USER_IS_ENABLED | USER_MODIFICATION_DATE | equals_values_read_before
 t               | (null)                 | t
```

The member refresh token was kept between steps in a file of the session scratchpad (outside the repo), deleted afterwards: `Test-Path p6-member-rt.txt: False`.

### P7 Rate limit (restart with `AdminAuth__RequestPermitLimit=3`, four emails)

```
[ADMIN] accounts listed=4 created=0 reenabled=1 disabled=0     (admin #4 back)
admin otp/request #1 (unknown address) -> 200 error=null retryAfter=none
admin otp/request #2 (unknown address) -> 200 error=null retryAfter=none
admin otp/request #3 (unknown address) -> 200 error=null retryAfter=none
admin otp/request #4 (unknown address) -> 429 error="Too many requests." retryAfter=900
    429 body: {"success":false,"data":null,"error":"Too many requests.","statusCode":429}
app otp/request #1 (unknown address) -> 400 error="User not found or account is disabled." retryAfter=none
app otp/request #2 (unknown address) -> 400 error="User not found or account is disabled." retryAfter=none
app otp/request #3 (unknown address) -> 400 error="User not found or account is disabled." retryAfter=none
app otp/request #4 (unknown address) -> 400 error="User not found or account is disabled." retryAfter=none
app otp/request #5 (unknown address) -> 400 error="User not found or account is disabled." retryAfter=none
```

### P8 Swagger

```
admin title: Mentora API — Admin
admin paths: /api/v1/admin/auth/otp/request, /api/v1/admin/auth/otp/verify, /api/v1/admin/auth/token/refresh, /api/v1/admin/auth/logout, /api/v1/admin/auth/me
admin security schemes: AdminBearer
admin document security: AdminBearer
admin tags used: Admin — Auth
mobile: identical to swagger-before = True (bytes 123392 vs 123392) schemes=Bearer adminPaths=0
coach: identical to swagger-before = True (bytes 304009 vs 304009) schemes=Bearer adminPaths=0
internal: identical to swagger-before = True (bytes 6923 vs 6923) schemes=Bearer adminPaths=0
```

(SHA-256 comparison of the raw bytes.)

### P9 Source and log checks

```
admin files checked: 21
Console.Write in admin files: 0
console OTP log line in AuthService.cs: 1
OTP console marker lines in the 12.1 logs: 2 | carrying an admin address: 0
```

(Output label reworded here so that this file does not contain the marker itself; the command counted the lines holding the OTP console marker of `AuthService.cs`.)

(The two console OTP lines are the app logins of the coach and the member in P4/P6.)

API stopped at the end: `port 5243 in use: False`.

## 6. Final local state (read-only)

```
 admins | enabled | admin_otps | admin_rts | admin_rts_active | users | users_disabled
      4 |       4 |          4 |         4 |                0 |     2 |              0
```

## 7. Leak checks on the files to commit (counts only)

Patterns proven first on known files: JWT pattern on `docs/review/lot7/claims-after-4a.json` = 2, OTP console marker (SimpleMatch) on `Mentora.Infrastructure/Services/AuthService.cs` = 1, address pattern on `docs/review/lot11/11.4/runtime-evidence.md` = 1.

On the 30 files to commit (code commit + this file), every count is 0 except two files that already matched with the same counts on `2d1f843`:

```
Mentora.API/appsettings.json: jwt=0 otp=0 addr=2 | on 2d1f843: jwt=0 otp=0 addr=2
Mentora.Infrastructure/Services/AuthService.cs: jwt=0 otp=1 addr=0 | on 2d1f843: jwt=0 otp=1 addr=0
```

The swagger captures (`swagger-before/`, `swagger-after/`) were removed after the comparison (`Test-Path` False for both); the API logs stay local (gitignored).

## 8. Notes and open points

- Refreshing with a logged-out token counts as reuse of a revoked token whose secret verifies (D5): it revokes every live session of that admin. Conservative, as specified.
- `UseRateLimiter` runs after `UseAuthorization` (pipeline unchanged): a `GET /me` refused by the `AdminOnly` policy (401) does not consume a permit. The four anonymous routes always do.
- `otp/request` sends the mail only for a known admin; with the SMTP provider the response time of a known address is longer than an unknown one (the BCrypt cost is equalised, the SMTP round trip is not). Possible later fix: send the mail after the response (background queue).
- `GET /me` of a disabled admin answers 401 with the middleware envelope (`Admin account not found or disabled.`); a bad or missing token answers the framework 401 without body, like the app.
- The app refresh still answers 400 for a malformed token (`ParseClientToken`, unchanged, out of scope).
