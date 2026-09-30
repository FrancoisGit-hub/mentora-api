# Lot 10.2 - evidence: basic quiz endpoints, device mode, device claim, rate limiting

Branch `feature/lot_10`, code commit on top of `e10e00c`. No migration (10.1 schema unchanged). Local only: postgres 5433, API 5243, Development. Run start (UTC): `2026-09-30T10:55:07Z` (`run-start.txt`).

## What was added
- `QuizOptions` (section `Quiz`, defaults in `appsettings.json`, `AddOptions().Bind().Validate().ValidateOnStart()`).
- Policy `QuizPlayer` = `QuizPlayerRequirement` + `QuizPlayerHandler` (next to `CoachOnly`). No Authorization header -> success (device mode). Header present -> the user must be authenticated and pass the `CoachOnly` policy (evaluated through `IAuthorizationService`, not copied). Invalid/expired token -> challenge (401, no fallback to device mode); authenticated member -> 403.
- `IQuizBasicService`/`QuizBasicService`, `IQuizDeviceClaimService`/`QuizDeviceClaimService` (services inject `MentoraDbContext`, FluentValidation inside the services, validators found by the existing assembly scan of Mentora.Core).
- `QuizBasicController` (`/api/v1/quiz/basic`: `POST sessions` 201, `POST sessions/{id}/answers` 200, `GET me` 200) and `CoachQuizBasicController` (`POST /api/v1/coach/quiz/basic/claim-device` 200), Swagger doc `coach`, typed envelopes, `X-Device-Id` documented through `[FromHeader]`.
- Rate limiter policy `QuizBasic` (fixed window per `RemoteIpAddress` for requests without Authorization; requests with an Authorization header are not limited), `UseRateLimiter` after `UseAuthentication`/`UseAuthorization`, `UseForwardedHeaders` still first. 429 uses the standard envelope plus `Retry-After`.

Choices worth knowing:
- Order of checks on submit: device-id rule (422), then ownership (unknown or foreign session -> 404, row locked with `SELECT ... FOR UPDATE` including the owner filter, so a foreign session is neither locked nor revealed), then body structure (422), then ALREADY_SUBMITTED / EXPIRED (409), then drawn-set mismatch (422, key `Answers`). The expected item count is the session's stored question count.
- `GET me`: invalid `limit` (outside 1..50) or cursor -> 400 (`InvalidOperationException`, same as the exercise list).
- Claim: the device's unclaimed sessions are locked `FOR UPDATE` so a concurrent submit cannot be missed in the points; if none are found the claim table is re-read (a concurrent claim that committed meanwhile gives ALREADY_CLAIMED / idempotent 200, not NOTHING_TO_CLAIM); a unique violation on the device id maps to 409 ALREADY_CLAIMED. Sessions the device plays after its claim stay unclaimed (one claim per device, ever).
- Coach total = sum of scores of the coach's COMPLETED sessions (claimed ones carry the coach id), so a claim raises the total without double counting.

## Decisions recorded
1. **401 and 403 keep the project's current behaviour: empty body, no envelope.** No JwtBearer `OnChallenge`/`OnForbidden`, no `IAuthorizationMiddlewareResultHandler`, no existing response changed. The envelope applies to 404, 409, 422, 429. H4, H5 and the 401/403 cases of H19 check the **status code only**.
2. **ForwardedHeaders unchanged** (`git diff e10e00c -- Mentora.API/Program.cs` shows only quiz options, the `QuizPlayer` policy/handler, quiz services, `AddRateLimiter`/`UseRateLimiter`). Reason: `KnownNetworks.Clear()` + `KnownProxies.Clear()` leaves both lists empty, and the middleware then accepts forwarded headers from any source, so the nginx container is already trusted whatever Docker subnet it gets. Acceptable because API port 8080 is not published (only nginx reaches it) and `ForwardLimit` keeps its default of 1. An earlier draft added `172.16.0.0/12` + loopback; it was reverted because it would restrict trust.
   nginx headers found (`nginx/conf.d`, locations `/` and `/hubs/`; there is no separate `/api` location): `X-Real-IP $remote_addr`, `X-Forwarded-For $proxy_add_x_forwarded_for`, `X-Forwarded-Proto $scheme` - both `X-Forwarded-For` and `X-Forwarded-Proto` are present.
3. Coach B = fixture `l102-coach-b@test.local` (user + coach, `fixture-01-coach-b.sql`), created after the run start, JWT through the real OTP login (`get-tokens.ps1`). Coach A = `coach@mentora.fr` (userType BOTH), member = `member@mentora.fr`.

## Not proven locally
The Docker network case (real client IP through nginx, `RemoteIpAddress` after forwarded headers) cannot be reproduced locally. Locally only the loopback case is covered (H21: a request sent with `X-Forwarded-For: 203.0.113.9` lands in its own partition, not in the exhausted 127.0.0.1 one). **To be proven on the VPS in 10.3.**

## Build
`dotnet build --no-incremental`: baseline (before any change) **0 errors, 19 warnings**; final (after the ForwardedHeaders revert) **0 errors, 19 warnings**.

## Runtime scenarios
Full request / status / body extract of each step: `scenarios-output.md` (H1-H20, script `verify-10.2.py`, executed against the API of `api-run-1`, which still contained the since-reverted ForwardedHeaders draft; that setting has no effect on H1-H20). Result: **ALL CHECKS OK**.

| Scenario | Result |
|---|---|
| H1 device POST sessions | 201; 12 distinct questions, positions 1..12, answers A-D in order; SQL: expires - created = 120.000 min, DEVICE_ID set, COACH_ID NULL, 12 distinct session questions |
| H2 no header / H3 `abc` | 422, `errors.DeviceId` |
| H4 garbage bearer / H5 member JWT | 401 / 403 (status only) |
| H6 coach A POST sessions | 201; SQL COACH_ID = coach A, DEVICE_ID NULL (X-Device-Id ignored in coach mode) |
| H7 submit with correct answers | 200; score 120 = sum of points, correctCount 12, matches SQL (session and per-question sums, owner total) |
| H8 resubmit | 409 `ALREADY_SUBMITTED` |
| H9 11 answers / H10 `E` / H11 duplicate / H12 question not drawn | 422 each (H12: key `Answers`); session still IN_PROGRESS |
| H13 coach B / H14 other device / H15 unknown id | 404 each |
| H16 expired (EXPIRES_DATE set in the past by SQL) | 409 `EXPIRED` |
| H17 two concurrent submissions | exactly one 200 and one 409 `ALREADY_SUBMITTED` |
| H18 GET me | coach A totals 240 / 3 sessions = SQL; device totals = SQL (0/0, only an in-progress session); `limit=1` -> 3 pages, every session once in SQL order; `limit=51` and a bad cursor -> 400 |
| H19 claim | device with 2 completed + 1 in progress: 200, claimedPoints 180 = SQL sum, claimedSessions 2, coach A total 240 -> 420; device GET me total 0; retry by coach A 200 same values, one claim row; coach B 409 `ALREADY_CLAIMED`; fresh device 409 `NOTHING_TO_CLAIM` (no claim row); no JWT 401; member 403; invalid deviceId 422 `DeviceId` |
| H20 | in-progress session of the claimed device: device mode 404, coach A 200 (total 520 = SQL) |

### H21 rate limit
- **First attempt invalid, discarded in substance**: the restarted API failed to bind because the previous API process (`Mentora.API.exe`, child of `dotnet run`, missed by my kill filter) still held port 5243, so the calls hit the default limit of 30, and the tokens had also expired. Nothing conclusive; its output was overwritten.
- **Clean rerun 1** (old process killed, `Quiz__AnonymousPermitLimit=5`): `h21-output-run1.md`, all OK. It ran before the ForwardedHeaders revert.
- **Clean rerun 2, after the revert** (final code): `h21-output-rerun.md`, all OK: anonymous calls 1-5 -> 201, call 6 -> **429** with `{"success": false, "data": null, "error": "Too many requests.", "statusCode": 429}` and `Retry-After: 600`, a 7th anonymous call still 429, a coach call right after -> 201, an anonymous call carrying `X-Forwarded-For: 203.0.113.9` -> 201 (own partition).

### Queries per request (EF logging, `sql-generated.md`)
No N+1: `POST sessions` 2 queries (random draw `ORDER BY random() LIMIT @p`, one batched insert of the session and its 12 rows), `POST answers` 4 (locked `SELECT ... FOR UPDATE`, session questions with their questions, one batched update, owner total), `GET me` 2 (one aggregate for totals, one page query). The random draw is done in SQL; the question bank is never loaded.

## Fixture cleanup
`cleanup-dry-run.sql` (ends with ROLLBACK) run twice (counts approved in the second run: 384 session questions, 32 sessions, 1 claim, coach B: 4 offer programs, 1 parameters row, 3 OTPs, 3 refresh tokens, 1 coach, 1 user; `cleanup-dry-run-output.txt`). `cleanup.sql` differs only by its last line (`cleanup-diff.txt`), executed (`cleanup-output.txt`, COMMIT). Read-only proof (`cleanup-verify.sql` / `cleanup-verify-output.txt`): 0 sessions and 0 claims with a device id `l102-%`, 0 sessions of coach A or B created since the run start, 0 USERS row for `l102-coach-b@test.local`, 0 COACHES row for coach B, 900 QUIZ_QUESTIONS. Coach A / member login artifacts (AUTH_OTPS, AUTH_REFRESH_TOKENS) are kept, as in lots 9.x.

## Files in this folder
`inventory.md`, `evidence.md`, `run-start.txt`, `fixture-01-coach-b.sql`, `get-tokens.ps1`, `start-api.sh`, `verify-10.2.py`, `scenarios-output.md`, `h21-output-run1.md`, `h21-output-rerun.md`, `sql-generated.md`, `cleanup-dry-run.sql`, `cleanup-dry-run-output.txt`, `cleanup.sql`, `cleanup-diff.txt`, `cleanup-output.txt`, `cleanup-verify.sql`, `cleanup-verify-output.txt`. The API logs (`api-run-*.log`, they contain OTP codes) are gitignored and not committed; the tokens file lived outside the repository and is not committed.
