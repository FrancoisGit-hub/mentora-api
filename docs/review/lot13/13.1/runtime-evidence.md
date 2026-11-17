# Lot 13.1: CORS debug origins (Cors__DebugOrigins), runtime evidence

- Date: 2026-11-17
- Branch: `feature/lot_13`, base commit `003d92a` (13.0 inventories)
- Design source: `docs/review/lot13/13.0/inventory-cors.md` plus decisions N1 to N6 of the 13.1 brief.

## 1. Decisions as implemented

| Decision | Implementation |
|---|---|
| N1 new setting `Cors:DebugOrigins` (env `Cors__DebugOrigins`), feeds `MentoraCorsPolicy` in every environment | `Mentora.Core/Options/CorsOptions.cs:25` (property), `:34` (`DebugOriginList`); `Mentora.API/Program.cs:457` (merged into the policy origins before the Development merge, `Distinct(OrdinalIgnoreCase)` unchanged) |
| N2 per item rules: no wildcard, absolute origin, no credentials, no path/query/fragment, http or https, host exactly `localhost`, explicit non default port, at most 5 items | `CorsOptions.cs:79-101` (`ValidateDebugOrigins`: count 82-83, scheme 92-94, host 95-96, port 97-98); shared syntax checks in `CheckOriginSyntax` (`:107`), same checks and messages as before for AllowedOrigins |
| N3 outside Development an AllowedOrigins item with host localhost is refused, https rule unchanged | `CorsOptions.cs:72-73` (after the unchanged https rule at 70-71); Development behaviour unchanged |
| N4 keep the two existing `[CORS]` lines, add `debugOrigins={Count}` and one `debug origin` line per origin (always printed) | `Program.cs:502-505`; the existing lines (`Program.cs:496-499`) are not modified |
| N5 messages name the setting and item number, never a value; validation in `CorsOptions.Validate`, run by `CorsOptionsValidator` at the first `.Value` read, before migrations | `CorsOptions.cs:60-77`, `:79-101`; `StartupGuards.cs` not changed |
| N6 unchanged: 7 Development origins, AllowAnyHeader, AllowAnyMethod, AllowCredentials, no preflight max age, no exposed headers, middleware order, `Mux__CorsOrigin` | not touched |

`OriginList` and `DebugOriginList` share one private parser, `ParseOrigins` (`CorsOptions.cs:36`), whose body is the previous `OriginList` body (split on `,`, trim, drop empty, one trailing `/` removed, case insensitive dedup). Validation order for AllowedOrigins is unchanged (R1 to R6), with the new localhost rule added after R6; debug origins are validated after all AllowedOrigins items.

## 2. Files changed

- `Mentora.Core/Options/CorsOptions.cs`
- `Mentora.API/Program.cs` (CORS policy block and `[CORS]` block only)
- `docs/review/lot12/12.5a/prod-runbook.md` (new section at the end only: "Lot 13.1 : origine de débogage (Cors__DebugOrigins)")
- `docs/review/lot13/13.1/runtime-evidence.md` (this file)
- Not changed: `StartupGuards.cs`, `CLAUDE.md` (it contains no CORS line, so nothing to add), `appsettings*.json`, nginx files, Dockerfile, migrations, NuGet packages.

## 3. Build

`dotnet build --no-incremental`: 0 errors, 19 warnings, all `CS1573` (missing `param` XML tags in `Mentora.Core/DTOs/Program/ProgramSessionResponse.cs` and the two `*ConversationMessagesController.cs` files). None in `CorsOptions.cs` or `Program.cs`: no new warning.

## 4. Runtime proofs

Shape of the commands: one helper script outside the repo (`$env:TEMP\mentora-lot13-131\run-case.ps1`) starts `dotnet run --project Mentora.API --no-build ...` with stdout and stderr redirected to log files under `$env:TEMP\mentora-lot13-131\`, sends the probes with `Invoke-WebRequest`, stops the process tree, proves port 5243 free, and prints only lines matching `[CORS]|[EMAIL] routing|[ADMIN]|[MUX]|Application started|Applying migration|No migrations|Unhandled exception|fail:|crit:` (never `OTP|EMAIL-LOG`).

- Development runs (P1, P2): `--launch-profile http`, `Cors__AllowedOrigins` unset, `Cors__DebugOrigins` unset (P1) or set (P2).
- Production runs (P3, P4): `--no-launch-profile`, `ASPNETCORE_ENVIRONMENT=Production`, `ASPNETCORE_URLS=http://localhost:5243`, `Email__Provider=Logging`, random 48 byte base64 `Jwt__Secret` generated in the script, `AdminAuth__*` and `EmailRouting__TestRecipients` inherited from the session, throwaway `Mux__*` and `LiveKit__*` values, `Mux__CorsOrigin=https://admin.mentorapp.fr`. Connection string: dummy `Host=127.0.0.1;Port=1;...` for P3, local database values read from `docker-compose.local.yml` inside the script for P4 (never printed).

### P1 Development, both settings unset

```
[p1-dev] preflight POST /api/v1/auth/otp/request origin=http://localhost:8100 status=204 ACAO=http://localhost:8100 ACAC=true
[p1-dev] port5243Free=True
[CORS] origins=0 localhost=True
[CORS] debugOrigins=0
No migrations were applied. The database is already up to date.
Application started.
```

Unchanged behaviour confirmed.

### P2 Development, `Cors__DebugOrigins=http://localhost:9999`

```
[CORS] origins=0 localhost=True
[CORS] debugOrigins=1
[CORS] debug origin http://localhost:9999
Application started.
[p2-dev-debug] port5243Free=True
```

### P3 Production refusals

Each start: exit code `0xE0434352` (unhandled exception), no `Applying migration` line, only the `[EMAIL] routing` line before the refusal, port 5243 free afterwards. Exact refusal lines:

| Case | Settings | Refusal line |
|---|---|---|
| a | Debug `http://example.com:8100` | `Unhandled exception. Microsoft.Extensions.Options.OptionsValidationException: Cors:DebugOrigins item #1 must use the host localhost.` |
| b | Debug `http://127.0.0.1:8100` | `Unhandled exception. Microsoft.Extensions.Options.OptionsValidationException: Cors:DebugOrigins item #1 must use the host localhost.` |
| c | Debug `http://localhost` | `Unhandled exception. Microsoft.Extensions.Options.OptionsValidationException: Cors:DebugOrigins item #1 must have an explicit non default port.` |
| d | Debug `http://localhost:*` | `Unhandled exception. Microsoft.Extensions.Options.OptionsValidationException: Cors:DebugOrigins item #1 must not contain a wildcard.` |
| e | Debug `http://localhost:8100/app` | `Unhandled exception. Microsoft.Extensions.Options.OptionsValidationException: Cors:DebugOrigins item #1 must not contain a path, query or fragment.` |
| f | Allowed `https://admin.mentorapp.fr,https://localhost:8100` | `Unhandled exception. Microsoft.Extensions.Options.OptionsValidationException: Cors:AllowedOrigins item #2 must not use the host localhost outside Development, use Cors:DebugOrigins.` |
| g | Allowed `http://admin.mentorapp.fr` (regression) | `Unhandled exception. Microsoft.Extensions.Options.OptionsValidationException: Cors:AllowedOrigins item #1 must use https outside Development.` |

For a to e, `Cors__AllowedOrigins=https://admin.mentorapp.fr`. The first run of P3 printed an empty exit code (a `Start-Process -PassThru` quirk of my helper script, handle not cached); the script was fixed and P3 was run again, the table above is from the second run (same refusal lines in both runs).

### P4 Production, `Cors__AllowedOrigins=https://admin.mentorapp.fr`, `Cors__DebugOrigins=http://localhost:8100`, local database

Startup lines:

```
[EMAIL] routing mode=TEST provider=Logging testRecipients=4 productionBcc=0
[CORS] origins=1 localhost=False
[CORS] origin https://admin.mentorapp.fr
[CORS] debugOrigins=1
[CORS] debug origin http://localhost:8100
[MUX] configured=True corsOrigin=https://admin.mentorapp.fr apiBase=api.mux.com
[ADMIN] auth emails=4 accessMinutes=15 refreshHours=12
No migrations were applied. The database is already up to date.
Application started.
```

Preflights (status, Access-Control-Allow-Origin, Access-Control-Allow-Credentials):

```
OPTIONS /api/v1/auth/otp/request  origin=http://localhost:8100      status=204 ACAO=http://localhost:8100      ACAC=true
OPTIONS /api/v1/auth/otp/request  origin=https://admin.mentorapp.fr status=204 ACAO=https://admin.mentorapp.fr ACAC=true
OPTIONS /api/v1/auth/otp/request  origin=http://localhost:4200      status=204 ACAO=<none> ACAC=<none>
OPTIONS /api/v1/auth/otp/request  origin=https://localhost:8100     status=204 ACAO=<none> ACAC=<none>
OPTIONS /api/v1/auth/otp/request  origin=http://localhost:9999      status=204 ACAO=<none> ACAC=<none>
OPTIONS /api/v1/coach/conversations (GET, authorization) origin=http://localhost:8100 status=204 ACAO=http://localhost:8100 ACAC=true
```

Actual requests from `Origin: http://localhost:8100`:

```
GET  /api/v1/coach/conversations (no token)   status=401 ACAO=http://localhost:8100 ACAC=true  (empty body, see note)
POST /api/v1/auth/otp/request (unknown email) status=400 ACAO=http://localhost:8100 ACAC=true  success=False statusCode=400 error=User not found or account is disabled.
POST /hubs/chat/negotiate?negotiateVersion=1 (no token) status=401 ACAO=http://localhost:8100 ACAC=true
```

The error envelope written by `GlobalExceptionMiddleware` (outside `UseCors`) carries the CORS headers: open question 9 of the 13.0 inventory is answered (headers present on the 400 envelope). The 401 from authentication has no JSON envelope (empty body, `success=`, `statusCode=`, `error=` empty): this is the existing JWT challenge behaviour, not a change of this lot.

Port 5243 free after the run: `port5243Free=True`.

## 5. Differences from the expected results

- None on the CORS results: every status, ACAO and ACAC value matches the brief.
- The Development runs (P1, P2) show `provider=Smtp` in the `[EMAIL] routing` line because the Development process inherited the session email settings; no mail is sent by these proofs (no OTP request in Development).
- The 401 on `GET /api/v1/coach/conversations` has an empty body, so the keys `success`, `statusCode`, `error` are empty there (see note above).
- Exit code capture of the first P3 run failed because of the helper script, fixed and re-run (section 4, P3).
