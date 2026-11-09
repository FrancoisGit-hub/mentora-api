# Lot 12, sub-lot 12.5a: API ready for the Beta PROD (CORS per environment, production startup guards, Docker build context)

Branch `feature/lot_12`, base `2cbf874`. Local run only (Docker PostgreSQL 16 on 5433, API on 5243). No token, code, secret, connection string or address appears below. The API logs are under `docs/review/lot12/12.5a/*.log` (gitignored by `*.log`, not committed). The session was in auto mode for the edits and the API starts; it is back to manual for the rest.

## 1. Preflight

| Step | Output |
|---|---|
| `git status` / `log --oneline -3` | clean; `2cbf874 docs(lot12): 12.4b evidence`, `b4a087e`, `d6e1e01` |
| `dotnet build --no-incremental` | `19 Avertissement(s)` (all CS1573, baseline), `0 Erreur(s)` |
| Port 5243 | free |

Inventory of what depends on the hosting environment (Mentora.API, Mentora.Core, Mentora.Infrastructure):

| Place | Behavior | Production changes it? |
|---|---|---|
| `Mentora.Infrastructure/Services/Email/RoutingEmailSender.cs:28`, `Mentora.Core/Options/EmailRoutingOptions.cs:19,22,24,41` | `IsProduction` is the email mode (`EmailRouting__Mode`), not the hosting environment | no |
| `Mentora.API/Properties/launchSettings.json:19,29,37` | `ASPNETCORE_ENVIRONMENT=Development` for local profiles only | no (not used in the image) |
| `Mentora.Core/Options/RefreshTokenPurgeOptions.cs:17` | comment: `appsettings.Development.json` sets the purge first pass to 0 | no |
| `appsettings.Development.json` (loaded only in Development) | local connection string, `RefreshTokenPurge`, `LiveKit` values | yes: not loaded in Production; excluded from the image by `.dockerignore` |
| `appsettings.Production.json` (loaded only in Production) | log levels | yes: before 12.5a `Default` was `Warning`, which hid every `[EMAIL]`, `[ADMIN]`, `[PATCH]` startup line |
| `Program.cs` | no `IsDevelopment` call before 12.5a; Swagger open in every environment (accepted open, 12.L) | no |

Keys read at startup with a default in `appsettings.json` and that are secrets (names only): `Jwt:Secret` (public dev default, now refused outside Development), `ConnectionStrings:DefaultConnection` (dead default, now refused outside Development), `Email:Smtp:Password` (empty), `LiveKit:ApiKey` and `LiveKit:ApiSecret` (empty), `AdminAuth:JwtSecret` (empty). Rule 3 slip noted during the preflight: my key-name grep also printed the two public defaults of `Jwt:Secret` and the connection string of `appsettings.json`; nothing else leaked.

## 2. Decisions applied

- E1 `Mentora.Core/Options/CorsOptions.cs` (section `Cors`, property `AllowedOrigins`). Deviation accepted: the property is a comma separated string (a single environment value cannot bind to a list); the parsed `OriginList` is trimmed, drops empty entries and one trailing `/`, removes duplicates case insensitively. `Validate(isDevelopment)` gives precise messages without any value: absolute origin, no wildcard, no credentials, no path, query or fragment, https only and at least one origin outside Development. Development adds the seven local origins. Startup lines `[CORS] origins=<n> localhost=<bool>` and one `[CORS] origin <origin>` per configured non localhost origin.
- E2 and E3 `Mentora.API/Startup/StartupGuards.cs`: outside Development `Jwt__Secret` must be an environment variable of at least 32 characters, `ConnectionStrings__DefaultConnection` must be an environment variable. Both are read with `Environment.GetEnvironmentVariable`. The difference with `AdminAuth__JwtSecret` reuses the existing check in `Program.cs`.
- E4 the guards run first in `Program.cs` (before the builder is used); the CORS validation runs with the email and admin ones, before migrations and seeders.
- E5 `.dockerignore`: `**/bin`, `**/obj`, `**/publish`, `Mentora.Admin`, `docs/review`; comments kept, the bin/obj one updated.
- E6 `nginx/conf.d/mentora-prod.conf.example`: HTTP block and HTTPS block for `api.mentorapp.fr`, `proxy_pass http://api-prod:8080`.
- E7 `prod-runbook.md` (French).
- Log levels (change requested after review): `appsettings.Production.json` now has `Default=Information`, `Microsoft.AspNetCore=Warning`, `Microsoft.EntityFrameworkCore.Database.Command=Warning`. No `Logging__LogLevel__*` variable is used anywhere.
- Em dashes added in the first version of `Program.cs` comments were replaced by colons.

## 3. Files

Modified: `.dockerignore`, `Mentora.API/Program.cs`, `Mentora.API/appsettings.Production.json`.
New: `Mentora.Core/Options/CorsOptions.cs`, `Mentora.API/Startup/StartupGuards.cs`, `nginx/conf.d/mentora-prod.conf.example`, `docs/review/lot12/12.5a/prod-runbook.md`, `docs/review/lot12/12.5a/runtime-evidence.md`.

## 4. Build

`dotnet build --no-incremental`: `19 Avertissement(s)` (all CS1573), `0 Erreur(s)`.

## 5. Runtime proofs

### C1 Scope

`git status --short`: three modified files and the new files of section 3 only (Mentora.API, Mentora.Core options, `.dockerignore`, the nginx example, `docs/review/lot12/12.5a/**`). `appsettings.Production.json` was added to the allowed list by the review.

### C2 Development start (Cors__AllowedOrigins unset)

```
[c2-dev] up=True exited=False
[c2-dev] health=200
[c2-dev] preflight origin=http://localhost:4200 status=204 ACAO=http://localhost:4200
[c2-dev] preflight origin=https://admin.mentorapp.fr status=204 ACAO=<none>
[c2-dev] port5243Free=True
[c2-dev] log: [CORS] origins=0 localhost=True
```

### C3 Production start, all guards satisfied (Cors__AllowedOrigins=https://admin.mentorapp.fr)

```
[c3-prod] up=True exited=False
[c3-prod] health=200
[c3-prod] preflight origin=https://admin.mentorapp.fr status=204 ACAO=https://admin.mentorapp.fr
[c3-prod] preflight origin=http://localhost:4200 status=204 ACAO=<none>
[c3-prod] port5243Free=True
[c3-prod] log: [EMAIL] routing mode=TEST provider=Logging testRecipients=4 productionBcc=0
[c3-prod] log: [CORS] origins=1 localhost=False
[c3-prod] log: [CORS] origin https://admin.mentorapp.fr
[c3-prod] log: [ADMIN] auth emails=4 accessMinutes=15 refreshHours=12
[c3-prod] log: No migrations were applied. The database is already up to date.
[c3-prod] log: [PATCH] Lot12_2a_LegacyExerciseCodes skipped (already applied)
[c3-prod] log: [PATCH] Lot12_2a_ReferenceNormalization skipped (already applied)
[c3-prod] log: [ADMIN] accounts listed=4 created=0 reenabled=0 disabled=0
[c3-prod] sqlCommandLines=0
```

The `[EMAIL] routing`, `[ADMIN]` and `[CORS]` lines are all visible in Production, "No migrations were applied" appears (the local database was already migrated), and the log holds no SQL command text (`sqlCommandLines=0`; the same count was 31 in Development C2). The Production environment loaded `Email__Provider=Logging`, a database built from `docker-compose.local.yml` in a variable, and a random 48 byte `Jwt__Secret`; LiveKit key and secret were throwaway values for the check (the Development file that normally carries them is not loaded in Production).

### C4 Refusals (each a separate Production start, port free after each)

Exit code of the six runs: `-532462766` (hexadecimal `0xE0434352`). This is the .NET unhandled exception code: the startup throws (`OptionsValidationException` for the three CORS refusals, `InvalidOperationException` from `StartupGuards` for the three others) and the process ends before any migration. For (d), (e) and (f) the log holds no line at all; for (a), (b) and (c) only the `[EMAIL] routing` line, printed before the CORS check. No `Applying migration` line in any of them.

| Case | Refusal line exactly as printed |
|---|---|
| (a) Cors unset | `Unhandled exception. Microsoft.Extensions.Options.OptionsValidationException: Cors:AllowedOrigins must contain at least one origin outside Development.` |
| (b) `http://admin.mentorapp.fr` | `Unhandled exception. Microsoft.Extensions.Options.OptionsValidationException: Cors:AllowedOrigins item #1 must use https outside Development.` |
| (c) `https://*.mentorapp.fr` | `Unhandled exception. Microsoft.Extensions.Options.OptionsValidationException: Cors:AllowedOrigins item #1 must not contain a wildcard.` |
| (d) Jwt__Secret unset | `Unhandled exception. System.InvalidOperationException: Jwt__Secret must be set as an environment variable outside Development (the appsettings.json default is never accepted).` |
| (e) Jwt__Secret 16 characters | `Unhandled exception. System.InvalidOperationException: Jwt__Secret must be at least 32 characters.` |
| (f) ConnectionStrings unset | `Unhandled exception. System.InvalidOperationException: ConnectionStrings__DefaultConnection must be set as an environment variable outside Development (the appsettings.json default is never accepted).` |

Port 5243: `port5243Free=True` after each of the six runs.

### C5 Docker

- `docker build -f Mentora.API/Dockerfile -t mentora-api:12.5a-check .`: `transferring context: 4.74MB`, build `0 Error(s)`, image tagged.
- `docker run --rm --entrypoint sh ... ls /app/appsettings*.json`: `/app/appsettings.Production.json` and `/app/appsettings.json` only.
- `Mentora.Admin` entries in `/app`: 0. `/app/wwwroot` and `/app/wwwroot/_framework`: do not exist.
- `docker rmi mentora-api:12.5a-check`: `Untagged` and `Deleted`.

### C6 Full build, ports free

`dotnet build --no-incremental`: `19 Avertissement(s)`, `0 Erreur(s)`; port 5243 free.

## 6. Open points

- Swagger stays open in Production (12.L, accepted for the Beta).
- The runbook uses placeholders for the image name, the Docker network and the PostgreSQL service name of the VPS compose file, which is not in the repository; the `pg_dump` and duplicate-name commands were not in `CLAUDE.md`, so they are written in the runbook itself.
- `AllowedOrigins` cannot be bound from a JSON array in appsettings (string only).
- Outside the repository, the helper script and its output used for C2 to C4 are removed at the end of the sub-lot.
