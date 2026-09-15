# Post-upgrade verification — 4d (JwtBearer 8.0.0 -> 8.0.31)

Scope: `Microsoft.AspNetCore.Authentication.JwtBearer` bumped from 8.0.0 to 8.0.31.
No other package touched, no source code touched. IdentityModel family stays at
8.22.0 (from 4a).

## Package graph diff (packages-after.txt -> packages-after-4d.txt)

Exactly one line changed:

```
<    > Microsoft.AspNetCore.Authentication.JwtBearer        8.0.0     8.0.0
---
>    > Microsoft.AspNetCore.Authentication.JwtBearer        8.0.31    8.0.31
```

No other top-level or transitive package moved. In particular, the whole
`Microsoft.IdentityModel.*` graph (`Abstractions`, `JsonWebTokens`, `Logging`,
`Protocols`, `Protocols.OpenIdConnect`, `Tokens`, `System.IdentityModel.Tokens.Jwt`)
stays resolved at **8.22.0** — confirmed by grepping the full post-4d package list.
The explicit `Microsoft.IdentityModel.Protocols.OpenIdConnect` 8.22.0 reference added
in 4a keeps winning over whatever version JwtBearer 8.0.31 nominally depends on.

**NU1605 / version-conflict warnings on restore:** none. `restore-after-4d.txt`
shows a clean restore.

## Build warnings

- 4a baseline: 19 warnings, 0 errors.
- 4d: **19 warnings, 0 errors** (`build-after-4d.txt`) — unchanged. No new NU1902,
  NU1605, NU1608 or NU1107 warnings.

## Auth test matrix

Re-ran `run-auth-tests.ps1` **unmodified** (no harness changes in this pass, per
instruction) against the JwtBearer 8.0.31 build: **16/16 PASS**, identical to 4a.
See `auth-test-run-20260915-111125.md`.

## Claims diff verdict (claims-after-4a.json vs claims-after-4d.json)

Compared field by field for both coach and member. Resolved `ClaimsPrincipal` claim
types, order, and count are **identical** between 4a and 4d. Only `jti` and `exp`
differ (expected, fresh token issuance each capture). No claim type, value shape, or
count changed.

**Verdict: no difference. Not a blocker.**

## SignalR verdict

Same method as 4a: real WebSocket connections to `/hubs/chat` via the query-string
`access_token` transport (`@microsoft/signalr@8.0.0`, `skipNegotiation: true` to
force the query-string path).

- Coach token -> connected, `JoinConversationAsync` on the real coach/member
  conversation (`58d5c27c-d823-48a8-bc68-59f053b02252`) succeeded (`JOIN_OK`).
- Member token -> same, succeeded via the `memberId` claim path.
- Garbage-token negative control -> WebSocket handshake failed outright.

**Verdict: authenticates correctly, identity resolves correctly. No regression.**

## Seeder idempotence (restart check)

Row counts before stopping the API and after a full restart:

| Table | Before restart | After restart |
|---|---|---|
| `COACH_PARAMETERS` | 1 | 1 |
| `OFFER_PROGRAMS` | 4 | 4 |
| `EXERCISES` (total) | 45 | 45 |
| `EXERCISES` (Mentora catalogue, `EXERCISE_COACH_ID IS NULL`) | 45 | 45 |

The Mentora exercise catalogue is the seeder that matters here: it matches by
`EXERCISE_NAME` in memory (`ExerciseSeeder.SeedAsync`), not a SQL `NOT EXISTS`
per-row check like the other two seeders, so a subtler idempotence break (e.g. a
name-matching miss producing duplicates) would show up as a count above 45, not as
an exception. It didn't: 45 in, 45 out, and the post-restart log contains **zero**
`INSERT INTO "EXERCISES"` statements (grepped directly), confirming the seeder
correctly matched all 45 existing names and inserted nothing.

## Harness

Not modified in this pass, per instruction. Same `run-auth-tests.ps1` version as
committed in `713f535`.
