# Lot 13.0b: read-only CORS inventory (annex to 13.0)

- Date: 2026-11-17
- Branch: `feature/lot_13`
- HEAD: `9337c5d docs(lot12): wave 2 VPS deployment evidence and runbook (MUX)`
- Scope: facts only about the current code and docs, for a later "debug origin" `http://localhost:8100` on the PROD (api.mentorapp.fr, `ASPNETCORE_ENVIRONMENT=Production`). No design, no code.
- Paths are relative to the repo root. "To verify" marks points that come from framework behaviour, not from the repo, and that no runtime evidence in the repo confirms.

## 1. Preflight

| Command | Result |
|---|---|
| `git --no-optional-locks status --short` | `?? docs/review/lot13/` (only) |
| `git --no-optional-locks branch --show-current` | `feature/lot_13` |
| `git --no-optional-locks log --oneline -3` | `9337c5d`, `cea79d1`, `b9b510f` |
| `Test-Path docs/review/lot13/13.0/inventory.md` | `True` (only file under `docs/review/lot13/` before this one) |

## 2. CorsOptions and its validator

### 2.1 `Mentora.Core/Options/CorsOptions.cs`

| Member | Line | Fact |
|---|---|---|
| class doc | 3-7 | origins come from `Cors__AllowedOrigins` (comma separated); Development origins added by `Program.cs`; messages never contain a value |
| `SectionName = "Cors"` | 10 | |
| `AllowedOrigins` (string, default `""`) | 13 | single comma separated string (a list cannot bind from one env value) |
| `OriginList` | 19-35 | split on `,` with `RemoveEmptyEntries | TrimEntries` (27); one trailing `/` removed (29); duplicates removed case insensitively, first spelling kept (23, 30); empty/whitespace input gives an empty list (25-26) |
| `IsLocalhostOrigin(string)` | 38-40 | `Uri.TryCreate(origin, Absolute)` and `uri.Host` equals `"localhost"` (OrdinalIgnoreCase). Scheme and port are not looked at |
| `Validate(bool isDevelopment)` | 43-68 | returns first failure message or `null` |

`Validate` rules, in order (label = `Cors:AllowedOrigins item #<n>`, line 52):

| # | Line | Condition | Message | Depends on isDevelopment |
|---|---|---|---|---|
| R1 | 46-47 | list empty | `Cors:AllowedOrigins must contain at least one origin outside Development.` | yes (only outside Development) |
| R2 | 53-54 | item contains `*` | `<label> must not contain a wildcard.` | no |
| R3 | 55-57 | not an absolute URI, or empty `Host`, or no `://` | `<label> is not an absolute origin (scheme://host[:port]).` | no |
| R4 | 58-59 | `UserInfo` not empty | `<label> must not contain credentials.` | no |
| R5 | 60-63 | query, fragment, path other than `/`, or any `/` after `://` | `<label> must not contain a path, query or fragment.` | no |
| R6 | 64-65 | scheme is not `https` | `<label> must use https outside Development.` | yes (only outside Development) |

Facts worth noting:
- No rule refuses a localhost host outside Development. `https://localhost:8100` passes R1-R6 in Production today (R6 only checks the scheme). Proof: no `IsLocalhostOrigin` call inside `Validate` (lines 43-68).
- R6 refuses every `http` origin outside Development, localhost or not: this is the rule that refuses `http://localhost:8100` in PROD today.
- `capacitor://localhost` outside Development would fail R6 (scheme not https).

### 2.2 `IsLocalhostOrigin` on the asked inputs

Based on `CorsOptions.cs:38-40` and documented `System.Uri.Host` behaviour (lowercased DNS host for http/https, IPv6 hosts returned with brackets).

| Input | `uri.Host` | Result | Confidence |
|---|---|---|---|
| `http://localhost:8100` | `localhost` | true | certain |
| `http://LOCALHOST:8100` | `localhost` (Uri lowercases it; the comparison is case insensitive anyway) | true | certain |
| `http://127.0.0.1:8100` | `127.0.0.1` | false | certain |
| `http://[::1]:8100` | `[::1]` (brackets kept in `Host`) | false | certain from docs, to verify |
| `http://localhost.evil.com` | `localhost.evil.com` | false (exact equality, not prefix) | certain |
| `http://sub.localhost` | `sub.localhost` | false | certain |
| `capacitor://localhost` (not asked, in the dev list) | `localhost` | true | to verify (non registered scheme parsing) |
| `http://localhost.` (trailing dot, not asked) | `localhost.` | false | to verify |

Consequence: "localhost" for this helper means exactly the name `localhost`; loopback IPs are not localhost.

### 2.3 `CorsOptionsValidator`

- Definition: `Mentora.API/Startup/StartupGuards.cs:43-50`, `IValidateOptions<CorsOptions>`, calls `options.Validate(environment.IsDevelopment())` (47). Primary constructor takes `IHostEnvironment` (43).
- Registration: `Mentora.API/Program.cs:447-450`: `AddOptions<Mentora.Core.Options.CorsOptions>().Bind(GetSection("Cors")).ValidateOnStart()` (447-449), `AddSingleton<IValidateOptions<...>, CorsOptionsValidator>()` (450).
- First `.Value` read: `Program.cs:493`, in the `[CORS]` block (`Program.cs:490-499`), after `builder.Build()` (474) and the `[EMAIL]` block (476-488), before `[MUX]` (501-508), `[ADMIN]` (510-522), and before `MigrateAsync` (528) and every seeder (530-541). The policy `Configure` callback also reads `.Value` (455), but only when the CORS policy options are first resolved, which happens later.
- Evidence: in the C4 refusals the log held only the `[EMAIL] routing` line before the CORS exception (`docs/review/lot12/12.5a/runtime-evidence.md:86`).

### 2.4 `Cors` in `Mentora.API/appsettings*.json` (count only)

Command: `grep -c 'AllowedOrigins' <file>` and `grep -c '"Cors"' <file>`.

| File | `AllowedOrigins` | `"Cors"` |
|---|---|---|
| `Mentora.API/appsettings.json` | 0 | 0 |
| `Mentora.API/appsettings.Development.json` | 0 | 0 |
| `Mentora.API/appsettings.Production.json` | 0 | 0 |

The setting comes only from the environment variable `Cors__AllowedOrigins`.

## 3. StartupGuards

| Guard | Lines | Checks | Environment |
|---|---|---|---|
| `StartupGuards.MinJwtSecretLength = 32` | `StartupGuards.cs:13` | constant | |
| `VerifyOutsideDevelopment(IHostEnvironment)` | `StartupGuards.cs:15-39` | returns at once in Development (17-18) | outside Development only |
| `Jwt__Secret` set | 20-23 | non empty | |
| `Jwt__Secret` length | 24-26 | >= 32 | |
| `ConnectionStrings__DefaultConnection` set | 28-30 | non empty | |
| `AdminAuth__JwtSecret` set | 32-35 | non empty | |
| `AdminAuth__JwtSecret` length | 36-38 | >= 32 | |
| `CorsOptionsValidator` | 43-50 | see 2.3 | both (rules R1 and R6 differ) |

- Pattern for reading an environment variable directly: `Environment.GetEnvironmentVariable("Jwt__Secret")` (`StartupGuards.cs:20`), same at 28 and 32. Failures throw `InvalidOperationException`.
- Call site and order in `Program.cs`: `WebApplication.CreateBuilder(args)` (32) → `StartupGuards.VerifyOutsideDevelopment(builder.Environment)` (36) → service registrations, options with `ValidateOnStart` (213-215, 270-272, 349-357, 361-367, 391-398, 447-449) → `builder.Build()` (474) → `.Value` reads `[EMAIL]` (479-480), `[CORS]` (493), `[MUX]` (503), `[ADMIN]` (513-514) → `MigrateAsync` (528) → seeders (530-541) → pipeline (546-586) → `app.Run()` (588).
- Does a guard touch CORS today: `VerifyOutsideDevelopment` does not (lines 15-39 mention no CORS variable). CORS is checked only through `CorsOptionsValidator` (options validation, not the direct env read pattern).
- Note on `ValidateOnStart`: the CORS check effectively fires at the explicit `.Value` (493), before `app.Run()`, where `ValidateOnStart` would otherwise run.

## 4. Program.cs: CORS pipeline

### 4.1 Development origins and merge

- `developmentOrigins`: `Program.cs:437-446`: `http://localhost:4200` (439), `http://localhost:8100` (440), `http://localhost` (441), `https://localhost` (442), `https://localhost:4200` (443), `https://localhost:8100` (444), `capacitor://localhost` (445).
- Merge: `Program.cs:452-465`. `origins = configured.Value.OriginList.ToList()` (455); `if (builder.Environment.IsDevelopment()) origins.AddRange(developmentOrigins)` (456-457); `WithOrigins(origins.Distinct(OrdinalIgnoreCase).ToArray())` (460). Outside Development the policy holds `OriginList` only.
- `builder.Services.AddCors()` (451).

### 4.2 Policy `MentoraCorsPolicy` (`Program.cs:458-464`)

| Feature | Present | Line / proof |
|---|---|---|
| `WithOrigins` | yes | 460 |
| `AllowAnyHeader` | yes | 461 |
| `AllowAnyMethod` | yes | 462 |
| `AllowCredentials` | yes | 463 |
| `SetPreflightMaxAge` | no | grep `SetPreflightMaxAge` in `*.cs`: 0 matches |
| `WithExposedHeaders` | no | grep `WithExposedHeaders` in `*.cs`: 0 matches |
| `SetIsOriginAllowed` / `AllowAnyOrigin` | no | grep in `*.cs`: 0 matches |

### 4.3 Middleware order

| Step | Line |
|---|---|
| (implicit `UseRouting`, added by `WebApplication` at the start; no explicit call) | grep `UseRouting`: not in `Program.cs` |
| `UseForwardedHeaders` | 546 |
| `UseMiddleware<GlobalExceptionMiddleware>` | 549 |
| `UseSwagger` / `UseSwaggerUI` | 551-560 |
| `UseCors("MentoraCorsPolicy")` | 562 |
| `UseAuthentication` | 563 |
| `UseAuthorization` | 564 |
| `UseRateLimiter` | 565 |
| `MapGet /api/v1/health` | 568-581 |
| `MapControllers` | 583 |
| `MapHub<ChatHub>("/hubs/chat")` | 586 |

Conclusions:
- (a) Preflight: `UseCors` (562) sits before `UseAuthentication` (563) and `UseRateLimiter` (565). The CORS middleware answers a preflight itself (204) without calling the next middleware, so a preflight is answered before authentication and the rate limiter. Runtime evidence: `204` preflights in Development and Production (`docs/review/lot12/12.5a/runtime-evidence.md:57-58, 68-69`) and on the real PROD (`docs/review/lot12/12.5a/vps-evidence.md:40`). A preflight from a non allowed origin still gets 204 but no `Access-Control-Allow-Origin` (`runtime-evidence.md:58, 69`).
- (b) Error responses: `GlobalExceptionMiddleware` (549) is outside `UseCors` (562). It catches exceptions thrown downstream and writes the envelope with `StatusCode`, `ContentType`, `WriteAsJsonAsync` (`Mentora.API/Middleware/GlobalExceptionMiddleware.cs:42-55, 80-105`); it never calls `Response.Clear()` and never touches headers (grep `Clear\(|Headers|HasStarted` in that file: 0 matches). For actual (non preflight) requests the ASP.NET Core CORS middleware applies its headers through a `Response.OnStarting` callback registered before calling next, so the headers should also be added to the 4xx/500 envelopes written by the outer middleware. To verify at runtime (no evidence in the repo). 401/403 produced by authentication/authorization and 429 by the rate limiter are produced inside `UseCors`, so they get the CORS headers in the same way (to verify at runtime). An exception thrown in Swagger (551-560) is before CORS: not relevant for API calls.

### 4.4 Other CORS / WebSocket knobs

Command: Grep `EnableCors|DisableCors|RequireCors|WebSocketOptions|UseWebSockets|AllowedOrigins|SetPreflightMaxAge|WithExposedHeaders|SetIsOriginAllowed|AllowAnyOrigin` in `*.cs`.
Result: 7 matches, all `AllowedOrigins` (`Program.cs:436` comment, `CorsOptions.cs:4, 13, 25, 27, 47, 52`). `[EnableCors]` 0, `[DisableCors]` 0, `RequireCors` 0, `WebSocketOptions` 0, `UseWebSockets` 0. So one global policy applies to every endpoint, including the hub, and WebSocket upgrades have no origin restriction of their own.

### 4.5 SignalR `/hubs/chat` from a browser

- Hub: `Mentora.API/Hubs/ChatHub.cs:16-17` (`[Authorize]`, any valid app JWT, default scheme). Mapped at `Program.cs:586`.
- Query string token: `JwtBearerEvents.OnMessageReceived` (`Program.cs:192-204`) copies `access_token` from the query string into `context.Token` when the path starts with `/hubs/chat` (196-200). Default scheme only; the admin scheme (208-211) has no such event.
- Requests and CORS (ASP.NET Core 8 / SignalR JS client documented behaviour, to verify on the device setup):
  - `POST /hubs/chat/negotiate`: normal fetch/XHR, goes through `UseCors`; a preflight is sent because of the `Authorization` header (when the client uses `accessTokenFactory`). The JS client sends it with credentials by default (`withCredentials` true), so the response needs an explicit origin plus `Access-Control-Allow-Credentials: true`, which the policy gives (460, 463).
  - WebSocket upgrade (`GET /hubs/chat?id=...&access_token=...`): browsers do not apply CORS to WebSockets; the `Origin` header is sent but nothing checks it (no `WebSocketOptions.AllowedOrigins`, see 4.4). Token in the query string.
  - Server-Sent Events: `GET` via `EventSource`, subject to CORS (credentials mode), token in the query string.
  - Long polling: `GET`/`POST` via fetch/XHR, subject to CORS, token in the `Authorization` header (preflight each new request shape).
- `/hubs/` on the nginx side: see 6.3.

### 4.6 `Mux__CorsOrigin` is a separate setting

- `Mentora.Core/Options/MuxOptions.cs:31-32`: `CorsOrigin`, "Origin of the admin site, sent as cors_origin of every direct upload".
- Normalization `NormalizedCorsOrigin` (50-57); validation `ValidateCorsOrigin` (100-122): single https origin, or `http://localhost` in Development only (115-120).
- Use: `Mentora.Infrastructure/Services/Mux/MuxClient.cs:49` (`cors_origin = Options.NormalizedCorsOrigin`), sent to MUX for the direct upload URL. Logged in `[MUX]` (`Program.cs:504-506`).
- It never feeds `MentoraCorsPolicy` (`Program.cs:452-465` reads only `Mentora.Core.Options.CorsOptions`). Runbook values: api-prod `Mux__CorsOrigin=https://admin.mentorapp.fr` (`prod-runbook.md:500`), api-demo `http://localhost:4200` (`prod-runbook.md:503`).

## 5. `[CORS]` startup lines

| Line | Template | Placeholders |
|---|---|---|
| `Program.cs:495-496` | `[CORS] origins={Count} localhost={Localhost}` | `Count` = `OriginList.Count` (configured origins only, development list not counted); `Localhost` = `app.Environment.IsDevelopment()`, i.e. whether the 7 development origins were added, not whether a configured origin is localhost |
| `Program.cs:497-498` | `[CORS] origin {Origin}` | one line per configured origin where `!IsLocalhostOrigin(o)` |

- Localhost filter confirmed: `Program.cs:497` (`Where(o => !CorsOptions.IsLocalhostOrigin(o))`), comment at 490-491 ("never the localhost ones"). A configured `http://localhost:8100` would be counted in `origins=` but never printed by name, and `localhost=` would still say `False` in Production.
- Expected today:
  - PROD (api-prod): `[CORS] origins=1 localhost=False` and `[CORS] origin https://admin.mentorapp.fr` (`docs/review/lot12/12.5a/prod-runbook.md:180`; also `runtime-evidence.md:72-73`).
  - api-demo (service `api`): `[CORS] origins=0 localhost=True` (Development: localhost origins only) (`prod-runbook.md:273`).

## 6. Deployment and configuration outside the code

### 6.1 `docs/review/lot12/12.5a/prod-runbook.md`

| Line | Content |
|---|---|
| 102-123 | `api-prod` compose block; 108 `ASPNETCORE_ENVIRONMENT=Production`; 113 `Cors__AllowedOrigins=https://admin.mentorapp.fr` |
| 131 | startup refusal list: `Jwt__Secret` / `AdminAuth__JwtSecret` missing or < 32 characters, `ConnectionStrings__DefaultConnection` missing, `Cors__AllowedOrigins` empty, `http://` or with a wildcard, admin secret equal to the app one; stops before migrations |
| 176 | `docker logs mentora-api-prod ... grep -E '...\[CORS\]...'` |
| 180 | expected `[CORS]` lines for PROD (see 5) |
| 195 | PROD preflight check (PowerShell `Invoke-WebRequest -Method Options`, `Origin: https://admin.mentorapp.fr`, `Access-Control-Request-Method: POST`, `Access-Control-Request-Headers: content-type`) |
| 198 | expected `204 https://admin.mentorapp.fr` |
| 253-262 | api-demo additions (`AdminAuth__*`); no `Cors__` variable for api-demo |
| 270, 273 | api-demo `[CORS]` grep and expected `origins=0 localhost=True` |
| 500, 503 | `Mux__CorsOrigin` (separate setting, see 4.6) |

- The real `docker-compose.yml` of the VPS is not in the repo; its `api-prod` service is described in `vps-evidence.md:25` (`Cors__AllowedOrigins=https://admin.mentorapp.fr`).

### 6.2 docker-compose files in the repo

- Only `docker-compose.local.yml` (postgres). `grep -ci cors docker-compose.local.yml`: 0.

### 6.3 nginx

| File | `add_header Access-Control-*` | `if ($request_method = OPTIONS)` | `proxy_hide_header` | `proxy_set_header Origin` |
|---|---|---|---|---|
| `nginx/nginx.conf` | 0 | 0 | 0 | 0 |
| `nginx/conf.d/mentora.conf` (api-demo) | 0 (only HSTS at 55) | 0 | 0 | 0 |
| `nginx/conf.d/mentora-prod.conf.example` (PROD) | 0 (only HSTS at 50) | 0 | 0 | 0 |

Commands: Grep `add_header|request_method|OPTIONS|proxy_hide_header|Origin|location|Upgrade|Connection|proxy_read_timeout|...` over `nginx/`, and `grep -cE 'add_header|Access-Control|request_method|proxy_hide_header|Origin' nginx/nginx.conf` (0).

- `/hubs/` location PROD: `mentora-prod.conf.example:53-67`: `proxy_pass http://api-prod:8080` (54), `proxy_http_version 1.1` (55), `Upgrade $http_upgrade` (56), `Connection $connection_upgrade` (57), `Host`, `X-Real-IP`, `X-Forwarded-For`, `X-Forwarded-Proto` (58-61), `proxy_buffering off` (64), `proxy_read_timeout 3600s` (65), `proxy_send_timeout 3600s` (66). Catch-all `/` at 70-80.
- `map $http_upgrade $connection_upgrade` declared once in `mentora.conf:5` (reused by the prod file, `mentora-prod.conf.example:5-6`). api-demo `/hubs/` at `mentora.conf:58-72`.
- Conclusion: nginx neither answers OPTIONS nor adds, hides or rewrites CORS headers or `Origin`; preflights and CORS headers pass through untouched to and from the API (confirmed for PROD by the 204 + ACAO check, `vps-evidence.md:40`). The admin site file (`mentora-admin.conf.example:81`) has a CSP `connect-src 'self' https://api.mentorapp.fr https://*.mux.com`, which concerns the admin page, not the mobile app.

## 7. Previous proofs to reuse

Evidence files in `docs/review/lot12/12.5a/`: `prod-runbook.md`, `runtime-evidence.md`, `vps-evidence.md`, and logs `c2-dev`, `c3-prod`, `c4a-cors-unset`, `c4b-cors-http`, `c4c-cors-wildcard`, `c4d-jwt-unset`, `c4e-jwt-short`, `c4f-conn-unset` (`.log` and `.err.log`; not opened, per rule 3).

- Refusal of an http origin outside Development: `runtime-evidence.md:84-92`, case (b) `http://admin.mentorapp.fr` → `OptionsValidationException: Cors:AllowedOrigins item #1 must use https outside Development.` (91); exit code `0xE0434352`, no `Applying migration`, only the `[EMAIL] routing` line before the refusal (86); port 5243 free after (97). The 12.5a file does not spell the exact start command; the shape used for the later Production refusals is in `docs/review/lot12/review-fixes/runtime-evidence.md:54`: process variables only, `ASPNETCORE_ENVIRONMENT=Production`, dummy `ConnectionStrings__DefaultConnection` (`Host=127.0.0.1;Port=1;...`), random 48 character `Jwt__Secret` generated in the command, `dotnet run --no-build --no-launch-profile`. Lesson at 59: without `--no-launch-profile`, `launchSettings.json` forces Development and the guards are skipped. Same shape in `docs/review/lot12/review-fixes-wave2/runtime-evidence.md:27` (with "a CORS origin set") and `docs/review/lot12/12.3a/runtime-evidence.md:88`.
- Local preflight proofs: `runtime-evidence.md:52-61` (Development: `http://localhost:4200` → 204 with ACAO; `https://admin.mentorapp.fr` → 204 without ACAO) and 63-80 (Production with `Cors__AllowedOrigins=https://admin.mentorapp.fr`: admin origin → 204 with ACAO; `http://localhost:4200` → 204 without ACAO).
- PROD preflight: command `prod-runbook.md:195`, expected `204 https://admin.mentorapp.fr` (198); result recorded in `vps-evidence.md:40` (`204 https://admin.mentorapp.fr`), and a real cross origin OTP call from the admin site (`vps-evidence.md:53`). Headers checked: status and `Access-Control-Allow-Origin` only (`Access-Control-Allow-Credentials`, `-Methods`, `-Headers` were not printed).

## 8. Tests

- Solution projects (`Mentora.sln:6-12`): `Mentora.API`, `Mentora.Core`, `Mentora.Infrastructure`, `Mentora.Admin`. No test project.
- Glob `**/*Test*.csproj`: no file. `find . -name '*Test*.csproj'`: 0.
- Grep `CorsOptions|MentoraCorsPolicy|IsLocalhostOrigin` in `*.cs`: only `Mentora.API/Program.cs`, `Mentora.API/Startup/StartupGuards.cs`, `Mentora.Core/Options/CorsOptions.cs`. No automated test covers CORS; all proofs are the runtime evidence of section 7.

## 9. Impact list (facts only)

| File | Current lines a debug-origin setting would touch |
|---|---|
| `Mentora.Core/Options/CorsOptions.cs` | `SectionName` 10, `AllowedOrigins` 13, `OriginList` 19-35, `IsLocalhostOrigin` 38-40, `Validate` 43-68 (R6 https rule 64-65) |
| `Mentora.API/Startup/StartupGuards.cs` | `CorsOptionsValidator` 43-50 (passes only `isDevelopment`, 47); direct env read pattern 20, 28, 32 |
| `Mentora.API/Program.cs` | comment 436; `developmentOrigins` 437-446; options registration 447-450; policy merge 452-465 (`OriginList` 455, Development merge 456-457, `WithOrigins` 460) |
| `Mentora.API/Program.cs` (`[CORS]` lines) | 490-499 (`.Value` 493, count line 495-496, per-origin line with localhost filter 497-498) |
| `docs/review/lot12/12.5a/prod-runbook.md` | api-prod compose 102-123 (Cors line 113), refusal list 131, log grep 176, expected lines 180, preflight check 195-198; api-demo 253-273 |
| VPS `docker-compose.yml`, service `api-prod` (not in repo) | environment block, as described in `prod-runbook.md:107-117` and `vps-evidence.md:25` |
| `Mentora.API/appsettings*.json` | none today (0 `AllowedOrigins`, 0 `"Cors"`, see 2.4) |
| nginx files | none today (no CORS directive, see 6.3) |

## 10. Observations and open questions for the design

1. Localhost origins are hidden from `[CORS] origin` (`Program.cs:497`). A configured `http://localhost:8100` would only raise `origins=` by one; the decision requires it to be visible.
2. `localhost=` prints `IsDevelopment()` (`Program.cs:496`), not the presence of a configured localhost origin: in PROD it would stay `False` even with a debug origin. Open question: meaning of this field after the change.
3. Outside Development every `http` origin is refused (`CorsOptions.cs:64-65`), localhost included. The decision keeps refusing non localhost http outside Development.
4. `https://localhost:*` is accepted in Production today (no localhost rule in `CorsOptions.cs:43-68`), and silently hidden from the log. Open question: is that intended once a dedicated debug setting exists.
5. `IsLocalhostOrigin` (`CorsOptions.cs:38-40`) ignores scheme and port and treats `127.0.0.1` and `[::1]` as not localhost; `capacitor://localhost` returns true (to verify). Open question: which notion of "localhost origin" the debug rule uses (name only vs loopback IPs).
6. `OriginList` dedup and comparisons are case insensitive (`CorsOptions.cs:23`, `Program.cs:460`), but the browser sends a lowercase origin; whether the CORS service matches origins case sensitively is to verify.
7. `AllowCredentials` with explicit origins (`Program.cs:460-463`): any origin added to the list gets credentialed access to every endpoint, hub included; there is no per endpoint policy (`[EnableCors]` 0, `RequireCors` 0).
8. No `SetPreflightMaxAge` (grep 0): every new request shape from the browser triggers a preflight (browser default cache, a few seconds). No `WithExposedHeaders`: response headers other than the CORS safelisted ones (for example `Content-Disposition`, `Content-Range` for a future voice download) are not readable by JS.
9. Error envelopes are written outside `UseCors` (`Program.cs:549` vs 562); CORS headers on 4xx/500 rely on the `OnStarting` callback: to verify at runtime, otherwise the browser would hide error bodies from the debug app.
10. SignalR: negotiate, SSE and long polling go through the policy; WebSockets do not and have no origin check (`WebSocketOptions` 0). Token in query string for `/hubs/chat` only (`Program.cs:194-203`); a future voice download in an `<audio src>` cannot send a Bearer header (no query token path outside `/hubs/chat`; grep `voice|audio` in `Mentora.API/*.cs`: 0 matches, the route does not exist yet).
11. The CORS check runs at `.Value` (`Program.cs:493`), after the `[EMAIL]` block and before migrations: any new refusal placed in `CorsOptions.Validate` keeps the "stop before migrations" property; a refusal placed in `VerifyOutsideDevelopment` (`StartupGuards.cs:15-39`) would fire earlier (line 36), before any log line.
12. `Mux__CorsOrigin` (`MuxOptions.cs:32, 100-122`) is unrelated to the API policy; its own `http://localhost` rule is Development only (115-120). Not to be mixed.
13. No automated test on CORS (section 8): every rule is proven by runtime evidence only; the 12.5a evidence did not record the exact start command (the shape is in `review-fixes/runtime-evidence.md:54`).
14. api-demo runs in Development (`prod-runbook.md:273`), so it already accepts the 7 development origins, `http://localhost:8100` included (`Program.cs:440`).

## Commands used

- `git --no-optional-locks status --short`; `git --no-optional-locks branch --show-current`; `git --no-optional-locks log --oneline -3`
- `Test-Path docs/review/lot13/13.0/inventory.md`; `Get-ChildItem docs/review/lot13 -Recurse`
- Read: `Mentora.Core/Options/CorsOptions.cs`, `Mentora.API/Startup/StartupGuards.cs`, `Mentora.API/Program.cs`, `Mentora.API/Middleware/GlobalExceptionMiddleware.cs`, `Mentora.Core/Options/MuxOptions.cs`, `nginx/conf.d/mentora-prod.conf.example`, `docs/review/lot12/12.5a/runtime-evidence.md` (18-97), `docs/review/lot12/12.5a/prod-runbook.md` (95-204, 250-275, 488-505), `docs/review/lot12/review-fixes/runtime-evidence.md` (48-61)
- Grep (`*.cs`): `EnableCors|DisableCors|RequireCors|WebSocketOptions|UseWebSockets|AllowedOrigins|SetPreflightMaxAge|WithExposedHeaders|SetIsOriginAllowed|AllowAnyOrigin`; `CorsOrigin|SectionName`; `localhost|8100|Capacitor|capacitor` (Mentora.API); `\[Authorize|class ChatHub` (Hubs); `voice|audio` (Mentora.API, count: 0); `Clear\(|HasStarted|Headers|WriteAsync|StatusCode =` (GlobalExceptionMiddleware, 0)
- Grep (`docs/review/lot12/12.5a/*.md`): `Cors|CORS|OPTIONS|preflight|Access-Control|no-launch-profile|refus`; (`docs/review/lot12/*.md`): `no-launch-profile|dotnet run|Start-Process|...`
- Grep (`nginx/`): `add_header|request_method|OPTIONS|proxy_hide_header|Origin|location|Upgrade|Connection|proxy_read_timeout|proxy_send_timeout|proxy_http_version|proxy_buffering|server_name|proxy_pass|map `
- Bash: `grep -c 'AllowedOrigins'` and `grep -c '"Cors"'` per `Mentora.API/appsettings*.json` (counts only); `ls docs/review/lot12/12.5a/`; `ls docker-compose* nginx nginx/conf.d`; `grep -cE '...' nginx/nginx.conf`; `grep -ci cors docker-compose.local.yml`; `grep -n 'Project(' Mentora.sln`; `grep -rlE 'CorsOptions|MentoraCorsPolicy|IsLocalhostOrigin' --include=*.cs .`; `find . -name '*Test*.csproj'` (count); Glob `**/*Test*.csproj`
- Count-only checks on this file: see the session report.
