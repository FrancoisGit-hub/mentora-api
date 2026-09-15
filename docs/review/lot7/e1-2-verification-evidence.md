# E1.2 section 5 — runtime verification evidence

Session resumed after a machine reboot mid-verification (prior session's code work,
commits `ee2d71e`..`5e48326`, was already complete and untouched). This document
records the restart-from-scratch runtime verification pass, `ASPNETCORE_ENVIRONMENT=Development`
throughout, `mentora-postgres-local` on port 5433.

## 5.1 — first start: purge fires immediately (Development override)

`Mentora.API/appsettings.Development.json` (gitignored, local-only) sets
`InitialDelayMinutes: 0`, so the purge ran at startup rather than waiting. Count-only
log line, verbatim:

```
info: Mentora.Infrastructure.Services.RefreshTokenPurgeService[0]
      RefreshTokenPurgeService (count-only): 4 AUTH_REFRESH_TOKENS row(s) expired or revoked before 2026-08-16T13:41:55.4788704Z would be deleted. Nothing deleted. First 50 id(s): a3456df1-3f9c-4d33-9b57-91138176555c, 9fe06ee0-ba0b-4f53-8dad-6b66e6fe6e32, 8e3b73be-b6d1-47cc-ab62-80c999a6dbe1, e816da91-e06c-496f-b259-6bbb248442ae
```

## 5.2 — EF-generated SQL and why today's residue is excluded

`RetentionDays = 30` (`RefreshTokenPurgeService.cs:27`); cutoff = `UtcNow - 30 days` =
`2026-08-16T13:41:55.48Z` at this start. Generated SQL (via the temporary DEBUG SQL
line, `RefreshTokenPurgeService.cs:69-70`):

```sql
SELECT a."AUTH_REFRESH_TOKEN_ID", a."AUTH_REFRESH_TOKEN_CREATED_DATE", a."AUTH_REFRESH_TOKEN_EXPIRATION_DATE",
       a."AUTH_REFRESH_TOKEN_HASH", a."AUTH_REFRESH_TOKEN_IS_REVOKED", a."AUTH_REFRESH_TOKEN_REVOKED_DATE",
       a."AUTH_REFRESH_TOKEN_USER_ROLE", a."USER_ID"
FROM "AUTH_REFRESH_TOKENS" AS a
WHERE a."AUTH_REFRESH_TOKEN_EXPIRATION_DATE" <= @__cutoff_0
   OR (a."AUTH_REFRESH_TOKEN_IS_REVOKED" AND a."AUTH_REFRESH_TOKEN_REVOKED_DATE" <= @__cutoff_1)
```

4 rows selected. Queried directly:

| `AUTH_REFRESH_TOKEN_ID` | Created | Expiration | Revoked | Revoked date |
|---|---|---|---|---|
| `a3456df1-3f9c-4d33-9b57-91138176555c` | 2026-03-26 | 2026-04-25 | true | 2026-03-26 |
| `9fe06ee0-ba0b-4f53-8dad-6b66e6fe6e32` | 2026-03-26 | 2026-04-25 | false | — |
| `8e3b73be-b6d1-47cc-ab62-80c999a6dbe1` | 2026-04-30 | 2026-05-30 | false | — |
| `e816da91-e06c-496f-b259-6bbb248442ae` | 2026-04-30 | 2026-05-30 | false | — |

All four created March/April 2026 — long predating any 2026-09-14/2026-09-15 residue.

**Why the 2026-09-14/2026-09-15 residue can never be selected:** every residue row has
`CREATED_DATE >= 2026-09-14`. Purge eligibility requires `EXPIRATION_DATE` (or, if
revoked, `REVOKED_DATE`) `<= 2026-08-16` — a date *before* the row could even have been
created, since `EXPIRATION_DATE`/`REVOKED_DATE` are always set at or after
`CREATED_DATE` (`AuthService.cs:277` sets expiration at issuance to
`UtcNow.AddDays(_jwt.RefreshTokenExpirationDays)`; revocation only ever happens after
creation). Confirmed empirically: `COUNT(*) FILTER (WHERE CREATED_DATE >= '2026-09-14')`
= 29, `COUNT(*) FILTER (WHERE EXPIRATION_DATE <= cutoff)` = 4,
`COUNT(*) FILTER (WHERE revoked AND REVOKED_DATE <= cutoff)` = 1 (the latter is a
subset of the former, row `a3456df1...`) — the residue set and the eligible set are
structurally disjoint.

## 5.3 — five refresh failure modes, real HTTP, byte-identical 401

**Correction to the session brief:** the resume brief asked for "five" refresh failure
modes tested independently — unknown, revoked, reused rotated, expired, post-logout.
That count describes the brief's wording, not the code. `AUTH_REFRESH_TOKEN_IS_REVOKED`
is set `true` in exactly two places in the whole codebase:

- `Mentora.Infrastructure/Services/AuthService.cs:157` — `RefreshTokenAsync`, revoking
  the token just consumed by a rotation.
- `Mentora.Infrastructure/Services/AuthService.cs:204` — `LogoutAsync`, revoking the
  token named in a logout call.

There is no third, independent "revoked" code path. So "revoked" and "reused rotated"
name the same underlying state and the same test (attempt a refresh with a token whose
`IS_REVOKED` is `true`, whichever of the two lines above set it) — not two distinct
mechanisms. This doc reports it as one real HTTP execution covering both labels rather
than fabricating a second, mechanistically-identical scenario to hit a headcount of
five. Counting `unknown` / `revoked`+`reused-rotated` / `post-logout` / `expired`, there
are four genuinely distinct HTTP-provable scenarios, not five, and this table has four
rows for that reason:

| Cause | How produced | Status | Body |
|---|---|---|---|
| unknown | random unparseable `id:secret` pair, never existed | 401 | `{"success":false,"data":null,"error":"Invalid or expired refresh token.","statusCode":401}` |
| revoked / reused-rotated | fresh coach session, refresh once (rotates + revokes original), reuse the original | 401 | same |
| post-logout | fresh coach session, `POST /api/v1/auth/logout` (Bearer access token), then reuse its refresh token | 401 | same |
| expired | see below | 401 | same |

All four bodies are byte-identical (raw string comparison, not JSON round-trip).

**Expired case** — produced exactly per the approved procedure:
1. Fresh OTP login (coach) created `AUTH_REFRESH_TOKEN_ID = 2a7d4a7d-be50-4d76-96ce-54157d130953`,
   created `2026-09-15 13:46:03.14958+00`, expiration `2026-10-15 13:46:03.14958+00`,
   `IS_REVOKED = false`.
2. Row and exact UPDATE shown to the user, explicit go-ahead obtained before running.
3. Executed, scoped by ID, touching only the expiration column:
   ```sql
   UPDATE "AUTH_REFRESH_TOKENS"
   SET "AUTH_REFRESH_TOKEN_EXPIRATION_DATE" = '2026-09-15 00:00:00+00'
   WHERE "AUTH_REFRESH_TOKEN_ID" = '2a7d4a7d-be50-4d76-96ce-54157d130953';
   ```
   `UPDATE 1`. `IS_REVOKED` left `false` throughout — expiry alone triggers the 401.
4. Refresh attempted with that token: `401`, same byte-identical body as above.
5. Row **not reverted** — it falls inside today's (2026-09-15) cleanup window per the
   calendar-day rule in `docs/review/lot7/e1-1-cleanup-manifest.md`.

This also confirms the earlier scenario 13 (`run-auth-tests.ps1`) claim precisely:
neutrality covers these token-validity causes only (four distinct HTTP scenarios per
the correction above, not five), not the separate `AuthService.cs:169`/`:174`
data-integrity 400 path (unreachable today; see the claims backlog doc).

**Addendum — `Invoke-ApiCallRaw` latent trap found and fixed:** the scratch script used
for the unknown/revoked/post-logout checks above reused this helper's shape and, in its
first draft, called `/api/v1/auth/logout` without a Bearer token because
`Invoke-ApiCallRaw` (unlike `Invoke-ApiCall`) had no `Token` parameter. That call
silently got `401` from `[Authorize]` before ever reaching `LogoutAsync`, so the token
was never revoked and the follow-up "post-logout" refresh legitimately returned `200`
— caught immediately by inspecting the logout call's own status code, not assumed.
The committed `run-auth-tests.ps1` carries the same helper; it never tripped over this
because scenario 13 only calls the anonymous refresh endpoint. Fixed in commit
`dde0021` (`Invoke-ApiCallRaw` gains an optional `Token` parameter mirroring
`Invoke-ApiCall`, no scenario behaviour changed). Re-ran the full suite after the fix:
**16/16 PASS**, scenario 13 included — report `auth-test-run-20260915-161338.md`.

## 5.4 — rotation: role comes from the refresh token, not re-derived

Coach rotation (fresh session, refresh once):
```json
{"success":true,"data":{"accessToken":"...","refreshToken":"...","expiresIn":3600,"isCoach":true},"error":null,"statusCode":200}
```
Decoded access-token payload carries `"userType":"COACH"` and a `coachId` claim, no
`memberId`.

Member rotation (fresh session, refresh once):
```json
{"success":true,"data":{"accessToken":"...","refreshToken":"...","expiresIn":3600,"isCoach":false},"error":null,"statusCode":200}
```
Decoded access-token payload carries `"userType":"MEMBER"` and a `memberId` claim, no
`coachId`.

`isCoach` is `true` for the coach and `false` for the member on refresh, confirming
`aa591b0`'s fix holds at runtime.

## 5.5 — full `run-auth-tests.ps1`

**16/16 PASS**, including scenario 13 (byte-identical 401 body, revoked vs. garbage).
Report: `docs/review/lot7/auth-test-run-20260915-160420.md`.

## 5.6 — SignalR `/hubs/chat`

Valid coach access token, query-string `access_token` transport:
- `POST /hubs/chat/negotiate?access_token=<token>` → `200`, returned `connectionToken`
  and `availableTransports` (WebSockets, ServerSentEvents, LongPolling).
- WebSocket upgrade to `ws://localhost:5243/hubs/chat?id=<connectionToken>&access_token=<token>`
  → connected (`State = Open`).
- SignalR JSON handshake (`{"protocol":"json","version":1}` + record separator) →
  server responded `{}` (handshake accepted), connection closed cleanly.

Garbage-token negative control: `POST /hubs/chat/negotiate?access_token=not-a-real-jwt-token`
→ `401`, rejected before any WebSocket upgrade was attempted.

## 5.7 — second start: idempotency

API stopped (`Stop-Process`, graceful) and restarted. On the second start:
- Migration check: "No migrations were applied. The database is already up to date."
- Both seeders (`CoachParameterSeeder`, `OfferProgramSeeder`) ran their `NOT EXISTS`
  queries but issued **no** follow-up INSERT — zero coaches needed seeding, confirming
  idempotency.
- `EXERCISES`: 45 total / 45 catalogue (`EXERCISE_COACH_ID IS NULL`) — unchanged.
- Purge fired again immediately (Development override), same count-only result: 4 rows,
  same 4 IDs (expected — nothing created during this session's testing is old enough to
  cross the 30-day cutoff).

## 5.8 — committed vs. local-only purge config

`git check-ignore -v Mentora.API/appsettings.Development.json` →
`.gitignore:487:**/appsettings.Development.json` — confirmed gitignored, so the
`InitialDelayMinutes: 0` override used throughout this verification session is
**local-only** and never reaches source control. The single committed value is
`Mentora.API/appsettings.json`'s `InitialDelayMinutes: 15` (`CountOnly: true`), last
touched in `7f807af`.

## 5.9 — DEBUG SQL line removed

Removed the temporary line (`logger.LogInformation("RefreshTokenPurgeService DEBUG SQL: {Sql}", eligible.ToQueryString());`)
and its preceding comment from `RefreshTokenPurgeService.cs`. Proven, not asserted:

```
$ git diff 5e48326 -- Mentora.Infrastructure/Services/RefreshTokenPurgeService.cs
(no output)
$ git status --short Mentora.Infrastructure/Services/RefreshTokenPurgeService.cs
(no output)
```

File is byte-identical to the committed `5e48326` state. Rebuild after removal:

```
$ dotnet build
La génération a réussi.
    0 Avertissement(s)
    0 Erreur(s)
```
