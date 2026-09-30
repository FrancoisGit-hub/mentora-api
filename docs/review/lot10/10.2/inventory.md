# Lot 10.2 - preflight inventory (read-only, before any code)

**a. Branch / status.** `feature/lot_10`, working tree clean, `git log --oneline -3`:
`e10e00c docs(lot10): 10.1 inventory and runtime evidence`, `6719745 feat(lot10): quiz schema and question import`, `fd3c69c docs(lot9): 9.6 review fixes runtime evidence`.

**b. Current coach id / CoachOnly.** No helper exists: every coach controller does `Guid.Parse(User.FindFirst("coachId")!.Value)` (claim name `coachId`, e.g. `CoachVoucherGrantsController`). `CoachOnly` is `policy.RequireClaim("userType", "COACH", "BOTH")` in `Program.cs`. Both are reused as is: controllers read the same claim; the new `QuizPlayer` handler evaluates the `CoachOnly` policy through `IAuthorizationService` instead of copying the rule.

**c. Authentication.** `AddJwtBearer` with `MapInboundClaims = true`, `ValidateIssuer/Audience/Lifetime/IssuerSigningKey`, `ClockSkew = Zero`. The only event is `OnMessageReceived` (reads `access_token` for `/hubs/chat`). No `OnChallenge`/`OnForbidden`, no `IAuthorizationMiddlewareResultHandler`, so 401/403 have an empty body. On an `[AllowAnonymous]` endpoint (none exists in the controllers) an invalid bearer token would silently leave the user anonymous: authentication failure is not an error by itself. This is why `QuizPlayer` must inspect the header itself.

**d. Middleware order (Program.cs).** `UseForwardedHeaders` -> `GlobalExceptionMiddleware` -> Swagger -> `UseCors` -> `UseAuthentication` -> `UseAuthorization` -> endpoints. No rate limiter. `ForwardedHeadersOptions`: `ForwardedHeaders = XForwardedProto | XForwardedFor`, `KnownNetworks.Clear()`, `KnownProxies.Clear()`. With both lists empty, `ForwardedHeadersMiddleware` only checks the proxy address when at least one list is non-empty, so forwarded headers are accepted from **any** source: the nginx container is trusted whatever Docker subnet it gets. Acceptable because API port 8080 is not published (only nginx reaches it) and `ForwardLimit` keeps its default of 1. nginx (`nginx/conf.d`, locations `/` and `/hubs/`, there is no separate `/api` location) sets `X-Real-IP $remote_addr`, `X-Forwarded-For $proxy_add_x_forwarded_for` and `X-Forwarded-Proto $scheme`. **No change to ForwardedHeaders in this lot** (an earlier draft added `172.16.0.0/12` + loopback; reverted, it would restrict trust).

**e. Swagger docs.** Three docs `mobile` / `coach` / `internal`; `DocInclusionPredicate` matches `ApiExplorerSettings(GroupName)`, default `internal`. Lot 9 coach endpoints use `[ApiExplorerSettings(GroupName = "coach")]` + `[Tags("Coach - ...")]`, so the four quiz endpoints use `coach`.

**f. Create status code.** POST endpoints that create a resource return **201** (`CoachVoucherGrantsController`: `StatusCode(201, envelope)`). `POST .../sessions` returns 201; the submit and claim endpoints (no new resource returned to the caller) return 200 as specified.

**g. Route collisions.** No controller or endpoint under `api/v1/quiz` or `api/v1/coach/quiz` before this lot (`git ls-files | grep -i quiz` shows only the 10.1 entities, configurations, migration and seeder).

**h. Local fixtures.** Lot 9 obtained JWTs through the real OTP flow (`docs/review/lot9/9.4/get-tokens.ps1`: `otp/request`, read the code from the API log, `otp/verify`) and created coach B by SQL (`fixture-01-coach-b.sql`). Locally there are only `coach@mentora.fr` (role Coach, has a coach row `e268a512-...` and a member row, so JWT `userType` = BOTH) and `member@mentora.fr` (member only). Coach B does not exist: created as a fixture `l102-coach-b@test.local` (user + coach) by SQL, JWT by OTP login (decision after this preflight). `QUIZ_QUESTIONS`: 900 rows, 900 active.

**i. Baseline build.** `dotnet build --no-incremental`: **0 errors, 19 warnings**.

## Decisions taken after the preflight
1. 401 and 403 keep the current behaviour (empty body). No JwtBearer challenge/forbidden handler and no authorization result handler; no existing response changed. The envelope applies to 404, 409, 422 and 429.
2. Coach B is a SQL fixture; included in the cleanup dry run.
