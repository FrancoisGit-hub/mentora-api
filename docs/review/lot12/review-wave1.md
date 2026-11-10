# Lot 12, wave 1 review (12.0 to 12.5b), before PROD deployment and merge

Read-only, single pass. Reviewer: Claude Code. Date: 2026-11-10.

## 1. Scope

- Branch `feature/lot_12`, HEAD `0839658` "docs(lot12): 12.5b evidence and admin runbook", parent `6c9385f`. Tree clean. Merge base with `origin/main`: `2d1f843`.
- Commits (`git log --oneline origin/main..feature/lot_12`), oldest first: `9a62981` 12.0 inventory, `cdf9e08` 12.1 admin auth + disabled user refresh, `d900e4f` 12.1 evidence, `c1a9216` 12.2a reference lists, normalization, create-only seeders, `de916be` 12.2a evidence, `3a5137d` 12.2b admin exercise API, usage check, audit log, `6e1b1ec` 12.2b evidence, `6125521` 12.4a admin shell, login, theme, `d6e1e01` 12.4a evidence, `b4a087e` 12.4b exercises screen, `2cbf874` 12.4b evidence, `00b1d6d` 12.5a CORS, startup guards, docker context, `2a54701` 12.5a evidence and PROD runbook, `6c9385f` 12.5b admin production settings and nginx, `0839658` 12.5b evidence and runbook.
- Diff stat (excluding `*.Designer.cs`, `*ModelSnapshot.cs`, `*.csv`, `docs/review/**`): 101 files, 7784 insertions, 363 deletions. The three lot 12 migrations are in scope and were read in full.
- Files excluded: migration designers, `MentoraDbContextModelSnapshot.cs`, the catalog CSV files (frozen), all of `docs/review/**` (read only as context for decisions and evidence).
- Not opened by rule: `Mentora.API/appsettings*.json` (only the changed key names were printed with the mandated command: `Logging.Default` in one file, and the `AdminAuth` section with `JwtSecret`, `JwtIssuer`, `JwtAudience`, `AccessTokenMinutes`, `RefreshTokenHours`, `OtpLifetimeMinutes`, `OtpMaxAttempts`, `RequestPermitLimit`, `RequestWindowMinutes`, `Emails`).

## 2. Verdict

**Not ready to deploy until R1 is cleared**: one BLOCKER, conditional on how the mobile app sends its HTTP calls (CORS on PROD has no mobile app origin). If a 2 minute check proves the mobile app is not subject to CORS, the verdict becomes "ready to deploy", with R2 recommended before deployment.

## 3. Findings

### R1. BLOCKER. PROD CORS allows no mobile app origin, and the validator forbids `capacitor://localhost`

- Where: `Mentora.API/Program.cs:421-449` (the seven local origins, among them `capacitor://localhost`, `https://localhost`, `http://localhost`, are added only when `IsDevelopment()`, line 440-441); `Mentora.Core/Options/CorsOptions.cs:64-65` (every configured origin must be `https` outside Development); runbook value `Cors__AllowedOrigins=https://admin.mentorapp.fr` (`docs/review/lot12/12.5a/prod-runbook.md:100`).
- What is wrong: before 12.5a the CORS policy held the Capacitor and localhost origins in every environment. 12.5a moved them to Development only. On PROD (`ASPNETCORE_ENVIRONMENT=Production`) the only allowed origin is the admin. A Capacitor WebView sends `Origin: capacitor://localhost` (iOS) and `https://localhost` or `http://localhost` (Android, depending on `androidScheme`). The preflight of every call carrying `Authorization` or `Content-Type: application/json` then gets no `Access-Control-Allow-Origin`, and the WebView blocks the call. `capacitor://localhost` cannot even be added through `Cors__AllowedOrigins` on PROD: the https rule refuses it and the API does not start. The 12.5a evidence (`runtime-evidence.md:63-79`) only tested the admin origin and `http://localhost:4200` on Production; no mobile origin was tested. This code was written in auto mode (area E).
- Why it matters for the Beta: if the Beta build of the mobile app uses the WebView `fetch`/`XMLHttpRequest` (Angular `HttpClient`) against `https://api.mentorapp.fr`, every call fails from day one (login included). api-demo is not affected (it stays in Development).
- Condition: this is not a defect if the mobile app sends its calls natively (Capacitor `CapacitorHttp` enabled, or a native HTTP plugin), because CORS then does not apply. The mobile code is not in this repository, so the code cannot prove it either way.
- Minimal fix: allow the mobile WebView origins in every environment, for example a constant list `capacitor://localhost`, `https://localhost`, `http://localhost` applied outside Development too (without the 4200 and 8100 dev server ports), or accept `capacitor://localhost` as a named exception in `CorsOptions.Validate`, then set `Cors__AllowedOrigins` on PROD accordingly. The tokens are bearer tokens, not cookies, so these origins add little risk. Then repeat the C3 preflight test of 12.5a with the three mobile origins.
- Quick check that clears or confirms the BLOCKER: look at `capacitor.config.*` of the mobile app (`plugins.CapacitorHttp.enabled`), or run a preflight with `Origin: capacitor://localhost` against a Production start of the API and look for the `Access-Control-Allow-Origin` header.
- Confidence: high that PROD refuses these origins; medium that the mobile app depends on them (strongly suggested by the fact that the pre-lot code listed `capacitor://localhost` and the Ionic port 8100).
- Decision 2026-11-10: no code change, PROD CORS stays admin only; checked at runtime by a login from the Beta build; a future web client gets its https origin through Cors__AllowedOrigins.

### R2. SHOULD. An admin save silently rewrites `isPolyarticular` and the deprecated `muscleGroup` read by the mobile and coach endpoints

- Where: `Mentora.Infrastructure/Services/AdminExerciseService.cs:829-830` (`IsPolyarticular = refs.Type?.Code == POLYARTICULAIRE`, `MuscleGroupFor(category.Text, refs.PrimaryGroup, null)`), `:510` (no-op detection compares these derived values too), `:880-881` (written). Read by `Mentora.Infrastructure/Services/ExerciseService.cs:416-418` (response fields `muscleGroup`, `equipment`, `isPolyarticular`) and `:40` (the `muscleGroup` filter).
- What is wrong: (a) The 45 historical exercises created by `ExerciseSeeder` (LG codes) have no type (the seeder never sets `ExerciseType`/`ExerciseTypeId`) but carry `ExerciseIsPolyarticular` from the seeder. The type field is optional in the form (`Mentora.Admin/Pages/Exercices/Form.razor:183`, not in `Required` at `:499-510`). Any save of such an exercise without picking a type turns `isPolyarticular` from true to false. (b) An exercise whose deprecated `muscleGroup` is `TRICEPS` (primary group "Bras", principal muscle text mentioning triceps at import) becomes `BICEPS` on any save, because the admin path passes no principal muscle text (`MuscleGroupSeeder.cs:23` maps "Bras" to `BICEPS`). Point (b) is a known open point (`docs/review/lot12/12.2b/runtime-evidence.md:40,293`); point (a) is not documented. In both cases a PUT where the admin changed nothing still writes (new `EXERCISE_UPDATED_DATE`, one audit row), which contradicts "no write when nothing changes".
- Why it matters for the Beta: the associates will edit the catalog during the Beta; each edit can silently move an exercise out of the coach/member `TRICEPS` filter or flip its polyarticular flag, with no visible cause.
- Minimal fix: in `BuildState`, when the request cannot determine a derived value, keep the stored one: `IsPolyarticular = refs.Type is null ? current?.ExerciseIsPolyarticular ?? false : refs.Type.Code == POLYARTICULAIRE`; and pass `"triceps"` as principal muscle when `current?.ExerciseMuscleGroup == MuscleGroup.Triceps` and the primary group is still "Bras".
- Confidence: high.

### R3. BACKLOG. Admin code request: response time reveals which addresses are admins

- Where: `Mentora.Infrastructure/Services/Admin/AdminAuthService.cs:49-54` (unknown address: one BCrypt hash, return) versus `:56-80` (admin: BCrypt, database write and an awaited mail send through SMTP).
- What is wrong: the body is neutral, but an admin address answers hundreds of milliseconds later (SMTP round trip). The same route also lets anyone who knows an admin address invalidate that admin's live code at each call (`:56-60`), within the rate limit.
- Why it matters: 4 associate addresses; enumeration only helps phishing, and the code is still needed. Rate limited to 30 calls per 15 minutes per IP.
- Minimal fix: send the mail outside the request (queue or `Task.Run` with its own scope), or pad both paths to the same duration.
- Confidence: high.

### R4. BACKLOG. Concurrent wrong codes can exceed the 5 attempts limit

- Where: `AdminAuthService.cs:99-120` (candidates read with `FailedAttempts < max`, BCrypt verify, then the increment).
- What is wrong: N parallel `otp/verify` calls all read the code before any increment lands, so all N guesses are checked. The final consume (`:130-134`) re-checks `FailedAttempts < max`, which narrows but does not close the window.
- Why it matters: bounded by the shared rate limit (30 per 15 minutes per IP), so at most about 30 guesses per IP and window out of 900000. Acceptable for the Beta.
- Minimal fix: increment first with `UPDATE ... SET attempts = attempts + 1 WHERE id = ... AND attempts < max RETURNING`, verify only when the update returned a row.
- Confidence: high.

### R5. BACKLOG. A lost refresh response, or a client without `navigator.locks`, logs the admin out everywhere

- Where: `AdminAuthService.cs:154-159` and `:170-174` (reuse or lost race: `RevokeAllActiveAsync`); `Mentora.Admin/wwwroot/js/admin.js:79` (no lock when `navigator.locks` is missing).
- What is wrong: if the refresh response is lost on the network and the client retries with the same token, or if two tabs refresh at once in a browser without the Web Locks API, the server treats it as theft and revokes every session, including the token just issued to the winner.
- Why it matters: the admin re-enters an OTP; no data risk. Modern browsers have `navigator.locks`.
- Minimal fix: a short grace period (for example 30 seconds) during which the token that has just been rotated returns the same successor instead of revoking everything.
- Confidence: high.

### R6. BACKLOG. Accent-only duplicate names on api-demo would block editing both exercises

- Where: `AdminExerciseService.cs:505` (name check before the no-op check), `:636-653` (accent-folded comparison); the database index is only `lower(btrim(name))` (`20261109113329_Lot12_2a_ExerciseReferences.cs:363-368`), and the runbook pre-check A uses the same expression.
- What is wrong: two Mentora exercises that differ only by accents (for example with and without "é") pass the migration and the runbook check, but every PUT on either of them returns 409 `EXERCISE_NAME_TAKEN`, even a save that changes another field.
- Why it matters: the 12.2a evidence found 0 such pairs on the local copy; api-demo was not measured with accent folding. The admin can rename one of them to unlock the other.
- Minimal fix: none needed before the Beta; add the accent-folded duplicate query to runbook check A (see section 5).
- Confidence: high on the behaviour, low on the probability.

### R7. BACKLOG. Deactivation race and inactive values: acceptable for the Beta

- Where: `AdminExerciseService.cs:577-607` (usage checked, then `IS_ACTIVE` written, no lock); `:686` (an inactive reference value is accepted when it is the value already held).
- Deactivation versus a concurrent add to a program: a coach can add the exercise between the usage check and the write; the program then holds an inactive exercise. The window is a few milliseconds with 4 admins, and the 12.0 inventory already lists coach paths that skip the active check (`ProgramService.cs:73-83`, `SessionTemplateService.cs:183-200`). Acceptable for the Beta.
- Inactive list value already carried by an exercise: it stays saveable (`:686`), is returned with `isActive=false` by the references route, and the form keeps it selectable only for the exercise that holds it, labelled "(désactivé)" (`Form.razor:597-601`). Acceptable for the Beta.
- Confidence: high.

### R8. BACKLOG. CSP hash depends on LF line endings of `index.html`

- Where: `Mentora.Admin/wwwroot/index.html:14-28`, `nginx/conf.d/mentora-admin.conf.example:81`.
- What is wrong: the hash `sha256-oW+3IccTLsL4xJ3s2rKPxyyczlz2dfeyKhTyG6r3Zkc=` matches the inline script with LF endings (verified in this review). With CRLF it would be `932TCiZe...`. The admin is published on the PC, where `core.autocrlf=true` and there is no `.gitattributes`; the current working copy is LF, but a fresh clone on that PC would give CRLF and the theme script would be blocked (theme flash and a CSP report, the app itself still loads).
- Minimal fix: `.gitattributes` with `Mentora.Admin/wwwroot/index.html text eol=lf`, or always recompute the hash from the published file (the conf comment already says so).
- Confidence: high.

### R9. BACKLOG. Em dashes in added lines

- 63 added lines carry an em dash (U+2014), 0 carry an en dash (U+2013). Almost all are in comments and XML doc. The visible ones: `Mentora.Infrastructure/Services/Admin/AdminAuthService.cs:298` and `:311` (title of the admin code mail, HTML and text), `Mentora.API/Program.cs:61` (Swagger title), `Mentora.API/Controllers/Admin/AdminAuthController.cs:19` and `Mentora.API/Controllers/Admin/AdminExercisesController.cs:19` (Swagger tags, same style as the existing ones).
- Full list: `AdminAuthDefaults.cs:3`, `AdminAuthRateLimiting.cs:11`, `AdminAuthController.cs:12,19`, `AdminExercisesController.cs:11,19`, `Program.cs:58,61,128,205,237,260,263,357,485`, `SecuritySchemePerDocumentFilter.cs:9`, `AdminAuthResponse.cs:4`, `AdminLogoutRequest.cs:3`, `AdminMeResponse.cs:3`, `AdminRefreshRequest.cs:3`, `AdminRequestCodeRequest.cs:3`, `AdminVerifyCodeRequest.cs:3`, `AdminExerciseDtos.cs:5`, `AdminAuditLog.cs:6`, `AdminOtp.cs:3`, `AdminRefreshToken.cs:3`, `AdminUser.cs:4`, `DataPatch.cs:4`, `Exercise.cs:38`, `ExerciseReferenceItems.cs:3`, `ExerciseSecondaryMuscleGroup.cs:4`, `AdminAuditAction.cs:3,12`, `IAdminAuthService.cs:5`, `IAdminExerciseService.cs:5`, `AdminAuthOptions.cs:6`, `AdminAuthValidators.cs:6,18`, `AdminExerciseValidators.cs:8,83`, `AdminAuditLogConfiguration.cs:10`, `AdminOtpConfiguration.cs:8`, `AdminRefreshTokenConfiguration.cs:7`, `AdminUserConfiguration.cs:8`, `DataPatchConfiguration.cs:7`, `ExerciseConfiguration.cs:114`, `ExerciseReferenceConfigurations.cs:8`, `ExerciseSecondaryMuscleGroupConfiguration.cs:7`, `MentoraDbContext.cs:17,49`, `AdminUserSeeder.cs:12`, `DataPatchRunner.cs:9`, `ExerciseReferenceSeeder.cs:11`, `LegacyExerciseColumns.cs:7`, `SeedingText.cs:8,29,40`, `AdminAuthService.cs:19,298,311`, `AdminExerciseService.cs:21`, `AuthService.cs:174,334`.
- Minimal fix: replace at least the mail title (`:298`, `:311`) with a colon.

### R10. BACKLOG. Smaller points

- `Mentora.API/Startup/StartupGuards.cs:15-31` checks `Jwt__Secret` and the connection string as environment variables, but not `AdminAuth__JwtSecret`. Safe today only because the public `appsettings.json` value is empty (stated in `12.5a/runtime-evidence.md:24`, not opened here by rule) and `AdminAuthOptions.Validate` then refuses to start. Adding the same environment check would make it independent of the JSON file.
- Admin rate limit `RequestPermitLimit=30` per `RequestWindowMinutes=15` per IP (`AdminAuthOptions.cs:20-21`) covers request, verify, refresh, logout and me. Four associates behind one office NAT share it; a busy test session could hit 429.
- A disabled admin's access token keeps read access to the admin GET routes for up to 15 minutes; writes are refused (`AdminExerciseService.cs:614-619`).
- `ADMIN_OTPS` and `ADMIN_REFRESH_TOKENS` are never purged (`RefreshTokenPurgeService` covers app tokens only). Tiny volume.
- `ReferenceNormalizationPatch.cs:112-134` drops the unmapped secondary muscle elements of api-demo exercises for good (only logged). By design; the `pg_dump` of runbook step 2 is the only way back.
- `AuthService.cs:175`: a blank line inside the query expression (cosmetic).

## 4. Verified OK

**A. Admin authentication (12.1)**
- Separate scheme: `AdminBearer` registered as a named scheme, never default (`Program.cs:207-210`); own key, issuer, audience from `AdminAuthOptions`, `MapInboundClaims=false`, `ClockSkew=0`, `ValidAlgorithms=[HS256]` (`Program.cs:216-232`); tokens signed HS256 (`AdminAuthService.cs:240-247`).
- App JWT on admin routes: `AdminOnly` policy authenticates with `AdminBearer` only and requires `adminId` (`Program.cs:238-241`); the startup refuses an admin secret or audience equal to the app ones (`Program.cs:490-493`), so an app token cannot validate. Admin JWT on app routes: app policies use the default scheme with the app key (`Program.cs:166-187`, `243-252`); an admin token fails signature, `QuizPlayer` returns 401 for an invalid token.
- `AdminOnly` coverage: `AdminExercisesController` at class level (`AdminExercisesController.cs:17`) covers its 6 actions: `GetReferences`, `GetAll`, `GetById`, `Create`, `Update`, `SetActivation`. `AdminAuthController.Me` (`AdminAuthController.cs:72`). The 4 other auth actions are `[AllowAnonymous]` by design (`:27,39,51,63`). No other admin controller.
- OTP: BCrypt hash (`AdminAuthService.cs:66`), 10 minutes and 5 attempts from options (`AdminAuthOptions.cs:18-19`), compared with `BCrypt.Verify`, dummy hash on misses (`:36,93,124`), atomic consumption (`:130-134`), uniform generator (`:283-284`).
- Refresh: opaque `id:secret`, BCrypt hashed, rotation with `ExecuteUpdateAsync` on `!IsRevoked` (`:165-174`), reuse revokes all sessions (`:154-159`); two concurrent refreshes: one wins, the other revokes all (R5).
- Neutral responses: same 200 for request (`AdminAuthController.cs:28-32`), same 400 message for verify (`AdminAuthService.cs:94,126,134`), same 401 for refresh; timing caveat in R3.
- Rate limit: `[EnableRateLimiting]` on the whole auth controller (`AdminAuthController.cs:18`), fixed window per client IP (`AdminAuthRateLimiting.cs:18-30`); `UseRateLimiter` after `UseAuthentication` and `UseAuthorization` (`Program.cs:538-540`).
- `AdminAuthOptions` validation: emails valid and unique, secret 32 characters minimum, ranges (`AdminAuthOptions.cs:30-65`), run before migrations (`Program.cs:488`).
- `AdminUserSeeder`: fresh database creates the listed admins; api-demo the same on first start; later starts re-enable, or disable and revoke tokens and codes of unlisted admins, one transaction (`AdminUserSeeder.cs:32-86`). Log line has counts only.
- 12.D: refresh lookup requires `r.User.UserIsEnabled` (`AuthService.cs:170-176`); a disabled user gets the same 401 message as an expired token, response shape unchanged. OTP request and verify already required `UserIsEnabled` (`AuthService.cs:38,97`).

**B. Migrations and seeders**
- `Lot12_1_AdminAuth`: 3 tables, FKs `Restrict`, `UX_ADMIN_USERS_EMAIL`, `CK_ADMIN_USERS_EMAIL_LOWERCASE`, `CK_ADMIN_OTPS_FAILED_ATTEMPTS`; Down drops the CHECKs then the tables in FK order (`20261108223153_Lot12_1_AdminAuth.cs:92-121`).
- `Lot12_2a_ExerciseReferences`: 6 nullable FK columns, 6 reference tables, `DATA_PATCHES`, `EXERCISE_SECONDARY_MUSCLE_GROUPS` (exercise Cascade, muscle group Restrict), all reference FKs `Restrict`, `CK_<TABLE>_CODE_FORMAT` and `CK_<TABLE>_SORT_ORDER`, `UX_EXERCISES_MENTORA_NAME_CI` on `lower(btrim(name)) WHERE EXERCISE_COACH_ID IS NULL`; Down drops the index and CHECKs first (`:343-390`). On api-demo, a duplicate on that expression makes the migration fail and the API not start (covered by runbook check A, see section 5).
- `Lot12_2b_AdminAuditLog`: FK `Restrict`, `CK_ADMIN_AUDIT_LOG_ACTION`, `CK_ADMIN_AUDIT_LOG_ENTITY_TYPE`, Down symmetric (`20261109160428_Lot12_2b_AdminAuditLog.cs:52-75`).
- Seeders creation-only by source code, idempotent on a second start: `ExerciseReferenceSeeder.cs:52-60` (by list and code), `CatalogSeeder` (by EX code, never writes an existing row), `ExerciseSeeder` (by LG code, superseded map, folded name skip). No `Update` of an existing exercise remains.
- `DataPatchRunner.cs:19-29`: marker inserted in the same transaction as the patch, primary key on the name; skipped when present. Fresh PROD: `LegacyExerciseCodes` runs on an empty table and records its marker, `ReferenceNormalization` maps the rows just seeded. api-demo: both run once on existing data.
- Startup order (`Program.cs:503-516`): migrations, coach parameters, offer programs, muscle groups, reference lists, legacy codes patch, catalog, historical exercises, normalization patch, program templates, quiz, admin users. Matches the documented 12.2a order.

**C. Admin exercises API (12.2b)**
- Validation: closed lists by id, `categoryId` and `primaryEquipmentId` required, lengths on trimmed values, lists bounded and without duplicates (`AdminExerciseValidators.cs:15-75`); existence and active rules (`AdminExerciseService.cs:672-764`).
- 409 `EXERCISE_NAME_TAKEN` on an accent, case and space folded key (`:636-653`), plus mapping of the two unique index violations (`:655-658`).
- EX code under `pg_advisory_xact_lock(7122002)` inside the transaction, name check under the lock (`:429-441`).
- PUT full replacement, no write when the stored state is unchanged (`:510-511`, caveat R2); P7: navigations never nulled, only FK columns assigned (`:884-886`).
- Decision A: `jsonb_path_exists` on `$.**.exerciseId` in program templates and session templates, plus `PROGRAM_EXERCISES`; 409 `EXERCISE_IN_USE` with counts and names, nothing written (`:359-412`, `:577-591`).
- Audit row in the same `SaveChanges` for create (inside the transaction), update and activation (`:473-476`, `:545-549`, `:602-607`).
- Cursor pagination by (name, id) and (rank, name, id) (`:149-246`); search by code `^(EX|LG)[0-9]+$`, case insensitive (`:42-43`, `:141-146`).
- Mobile and coach routes: no controller outside `Controllers/Admin` changed; `ExerciseService` only moved limit, search and cursor helpers to `ExerciseListPaging.cs` with identical messages and payload records; no `Mentora.Core/DTOs/Exercise` file changed.

**D. Mentora.Admin**
- No reference to the API projects (`Mentora.Admin.csproj:9-12`).
- Access token in memory only (`AdminSession.cs:31`); refresh in `localStorage` key `mentora-admin-refresh` (`admin.js:5,68-70`); every refresh under `navigator.locks` with the stored token read inside the lock (`AdminSession.cs:165-216`, `admin.js:73-80`).
- 401 handled on status only, with or without envelope (`ApiResult.cs:21`, `ApiTransport.cs:51-108`); one refresh, one retry (`ApiClient.cs:15-34`).
- XSS: no `MarkupString`, no `innerHTML`, no inline handler, no `eval`; all values rendered through Razor (escaped); JS interop takes fixed keys, element references or ids only.
- Exercises URL parameters read as text and parsed defensively, unknown values ignored, `q` capped at 60 (`List.razor:217-302`); API query values escaped (`ExercisesApi.cs:22`).
- Unsaved changes: `NavigationLock` for internal and external navigation, confirmation dialog (`Form.razor:13`, `:366-367`, `:675-698`).
- CSP: no `style=` attribute in any component, no inline event handler; one inline script.
- `appsettings.Development.json` not published (`Mentora.Admin.csproj:21`); `appsettings.Production.json` holds only `ApiBaseUrl=https://api.mentorapp.fr` and an empty label.

**E. 12.5a code**
- `UseForwardedHeaders` first in the pipeline (`Program.cs:521`), `KnownNetworks` and `KnownProxies` cleared (`:451-456`); nginx sets `X-Forwarded-For $proxy_add_x_forwarded_for` and the default `ForwardLimit=1` takes the address nginx appended.
- CORS policy before authentication (`:537`), rate limiter after authorization (`:540`); no other middleware moved (diff checked).
- `StartupGuards` called right after `CreateBuilder` (`:35`), reads `Jwt__Secret` and `ConnectionStrings__DefaultConnection` with `Environment.GetEnvironmentVariable`, 32 characters minimum, skipped in Development (`StartupGuards.cs:15-31`).
- `CorsOptions`: absolute origin, no wildcard, credentials, path, query or fragment, https and at least one origin outside Development (`CorsOptions.cs:43-68`); `[CORS]` log lines print counts and non localhost origins only (`Program.cs:476-483`). Mobile origins: see R1.
- `appsettings.Production.json`: only the `Logging` key `Default` changed (mandated key-name command).
- `.dockerignore`: `**/bin`, `**/obj`, `**/publish`, `Mentora.Admin`, `docs`, `docs/review`, `**/appsettings.Development.json`; `Mentora.API/Dockerfile` unchanged by the lot, restores the API project (and its two references) only.
- `nginx/conf.d/mentora-prod.conf.example`: HTTP block with ACME and 301, HTTPS block with TLS 1.2/1.3, HSTS, WebSocket location, forwarded headers.

**F. `mentora-admin.conf.example`**
- All `add_header` at server level with `always`, none in a location (`:73-81`).
- CSP hash recomputed in this review from `index.html`: `sha256-oW+3IccTLsL4xJ3s2rKPxyyczlz2dfeyKhTyG6r3Zkc=`, equal to the conf (LF caveat R8). `wasm-unsafe-eval` present, `connect-src` limited to self and the API, `frame-ancestors 'none'`, `object-src 'none'`.
- `try_files $uri $uri/ /index.html` (SPA fallback, `appsettings.Development.json` returns `index.html`: accepted), `gzip_static on`, `Cache-Control: no-cache`, TLS settings, single certificate path under `live/api.mentorapp.fr`, HTTP to HTTPS redirect and ACME location.

**G. Public repository**
- JWT shape `eyJ` + two dot separated segments: 0 file in the lot diff. Control: the same `-G` pattern on the commit that added `docs/review/lot7/claims-after-4a.json` finds that file.
- OTP marker `[[]OTP[]]`: 0 file in the lot diff. Control: the same `-G` on the commit of `AuthService.cs` that added the marker finds it.
- `gmail[.]com`: 0 file. `web2success[.]fr`: 0 file (controls: the patterns match 2 and 4 files on `origin/main`).
- `mentorapp[.]fr`: 4 code or config files (`Mentora.Admin/wwwroot/appsettings.Production.json`, `Mentora.Core/Options/CorsOptions.cs`, both nginx examples) and 4 review docs. Email-shaped hits on that domain in these 8 files: 0. All hits are public host names (API and admin URLs), new on `main`, not secrets.
- `Password=`: only in the runbook and its evidence, each value is a `${...}` variable reference (`prod-runbook.md:96,109`). No private key, no Stripe key.
- Configuration files touched by the lot: `.dockerignore`, `.gitignore`, `Mentora.API/appsettings.json`, `Mentora.API/appsettings.Production.json`, `Mentora.Admin/Properties/launchSettings.json`, `Mentora.Admin/wwwroot/appsettings.json`, `Mentora.Admin/wwwroot/appsettings.Production.json`, `Mentora.Admin/wwwroot/appsettings.Development.json` (localhost only, public, committed on purpose via `.gitignore`), `nginx/conf.d/mentora-admin.conf.example`, `nginx/conf.d/mentora-prod.conf.example`. No `.env`, no `docker-compose` file.

**H.** 63 em dashes, 0 en dash in added lines (R9).

**I. Build**: `dotnet build --no-incremental`: 19 warnings, all CS1573 (19 unique), 0 error; no warning from `Mentora.Admin`.

## 5. Points needing a runtime proof during deployment

1. R1: preflight `OPTIONS` with `Origin: capacitor://localhost`, `https://localhost` and `http://localhost` against `https://api.mentorapp.fr`, or a login from the Beta mobile build pointed at PROD. Do it before announcing the Beta.
2. api-demo before the new code: runbook check A (duplicates on `lower(btrim(name))` among Mentora exercises must be 0, else the migration fails and api-demo stops). Add the accent-folded variant (R6). Take the `pg_dump` just before.
3. `AdminAuth__JwtSecret` and `AdminAuth__Emails` set on api-demo and PROD (the API refuses to start without them, in Development too), different between the two environments and from `Jwt__Secret`; `[ADMIN] auth emails=4` in both start logs.
4. PROD variable names exactly `Jwt__Secret` and `ConnectionStrings__DefaultConnection` (the guard reads the environment with that case); start log shows no guard error and `[CORS] origins=...` as expected.
5. The `api-prod` container port is not published on the host (only nginx reaches it); otherwise `X-Forwarded-For` can be forged and the admin rate limit bypassed. Check that two admins on different networks get separate rate limit buckets.
6. nginx: start `api-prod` before enabling `mentora-prod.conf` (an unresolvable `proxy_pass` host makes `nginx -t` or the reload fail, which would also affect api-demo); `/var/www/mentora-admin` mounted in the nginx container; `.wasm` served as `application/wasm` by the nginx `mime.types`.
7. CSP: the hash computed from the final published `index.html` equals the header (R8); the browser console shows no CSP violation on login and on the Exercises screen.
8. api-demo first start: `[PATCH] Lot12_2a_LegacyExerciseCodes applied` and `[PATCH] Lot12_2a_ReferenceNormalization applied` once, with their unmapped counts kept for the record; a second restart shows both `skipped`. Expect the category, type and secondary muscle texts seen by coaches and members to change to the reference labels (by design).
9. PROD first start on the fresh `mentora_prod`: all migrations applied, catalog and historical exercises created, then a restart shows `created 0`.
10. Mobile on api-demo after deployment: login, refresh, and refresh refused (401) for a disabled user (12.D).
11. Admin: login with a code received in TEST mode, two tabs open across a token expiry without being logged out, edit and save an exercise, check one `ADMIN_AUDIT_LOG` row.
