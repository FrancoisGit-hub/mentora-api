# Post-upgrade verification — 4a (IdentityModel family -> 8.22.0)

Scope: `System.IdentityModel.Tokens.Jwt` and the whole `Microsoft.IdentityModel.*`
family bumped from 7.0.3 to 8.22.0. `Microsoft.AspNetCore.Authentication.JwtBearer`
left untouched at 8.0.0 (that bump is 4d, separate commit/pass).

## Package graph diff (packages-before.txt -> packages-after.txt)

| Package | Before | After |
|---|---|---|
| `System.IdentityModel.Tokens.Jwt` | 7.0.3 | 8.22.0 |
| `Microsoft.IdentityModel.Abstractions` | 7.0.3 | 8.22.0 |
| `Microsoft.IdentityModel.JsonWebTokens` | 7.0.3 | 8.22.0 |
| `Microsoft.IdentityModel.Logging` | 7.0.3 | 8.22.0 |
| `Microsoft.IdentityModel.Protocols` | 7.0.3 | 8.22.0 |
| `Microsoft.IdentityModel.Protocols.OpenIdConnect` | 7.0.3 (transitive via JwtBearer) | 8.22.0 (now also an explicit top-level reference in Mentora.API) |
| `Microsoft.IdentityModel.Tokens` | 7.0.3 | 8.22.0 |
| `Microsoft.Bcl.Cryptography` | (absent) | 10.0.2 (new transitive, pulled in by IdentityModel 8.22.0) |
| `System.Formats.Asn1` | 8.0.1 | 10.0.2 (bumped transitively) |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 8.0.0 | 8.0.0 (unchanged, confirmed) |

Everything else in the graph (EF Core, Npgsql, MailKit, FluentValidation, Swashbuckle,
BCrypt.Net-Next, ...) unchanged.

**NU1605 / version-conflict warnings on restore:** none. `dotnet restore` output
(`restore-after.txt`) shows a clean restore, no downgrade or conflict warnings.

**NU1902 (`System.IdentityModel.Tokens.Jwt` 7.0.3 moderate vulnerability,
GHSA-59j7-ghrg-fj52):** confirmed gone. `packages-after.txt`'s vulnerable-packages
section no longer lists `System.IdentityModel.Tokens.Jwt` or
`Microsoft.IdentityModel.JsonWebTokens` (the latter shared the same advisory and is
resolved as a side effect, per `vulnerable-packages-backlog.md`'s note). Remaining
vulnerable entries (`Microsoft.Extensions.Caching.Memory`, `Npgsql`,
`System.Text.Json`) are unrelated, out-of-scope backlog items, unchanged.

## Build warnings

- Before: 21 warnings, 0 errors (`build-before.txt`).
- After: **19 warnings, 0 errors** (`build-after.txt`).
- Delta: exactly the two `NU1902` lines for `System.IdentityModel.Tokens.Jwt` 7.0.3
  are gone (that warning was emitted twice, once per csproj reference path). No new
  warnings introduced. Remaining 19 are the pre-existing CS1573 XML-doc-comment
  warnings, unrelated to this change.

## Breaking changes handled

**None applied.** The build succeeded with 0 errors on the first attempt after the
version bump — no source changes were needed anywhere in the codebase (the csproj
diff is the only change: two version bumps plus one new explicit package
reference). The 7.x -> 8.x major bump in the `Microsoft.IdentityModel.*` family did
not break any API surface this codebase touches (`JwtSecurityTokenHandler`,
`TokenValidationParameters`, `SymmetricSecurityKey`, `SigningCredentials`,
`JwtRegisteredClaimNames`, `options.MapInboundClaims`), confirmed both at compile
time (clean build) and at runtime (claims diff and full auth matrix below).

## Auth test matrix

Re-ran `run-auth-tests.ps1` against the upgraded package set: **16/16 PASS**,
identical to the pre-upgrade baseline. See
`auth-test-run-20260915-104554.md` for the full table.

## Claims diff verdict (claims-before.json vs claims-after-4a.json)

Compared field by field for both coach and member, issued JWT payload AND resolved
`ClaimsPrincipal`:

- **Issued token payload key order**: identical —
  `sub, email, userType, jti, coachId|memberId, exp, iss, aud` for both roles.
- **Resolved `ClaimsPrincipal` claim types, order, and count**: identical (8 claims
  each, same order) —
  `ClaimTypes.NameIdentifier, ClaimTypes.Email, userType, jti, coachId|memberId, exp, iss, aud`.
- **`sub` resolution**: still resolves **only** as `ClaimTypes.NameIdentifier`
  (long-form URI). No literal `"sub"` claim present. Unchanged from baseline.
- **`email` resolution**: still resolves **only** as `ClaimTypes.Email`. Unchanged.
- **Unmapped literals** (`userType`, `jti`, `coachId`, `memberId`, `exp`, `iss`,
  `aud`): pass through unchanged as their literal claim type strings, same as
  baseline.
- Only `jti` and `exp` values differ between the two captures, which is expected
  (fresh token issuance, not a claim-mapping change).

**Verdict: no difference. Not a blocker.**

## SignalR verdict

Opened real hub connections to `/hubs/chat` with `@microsoft/signalr@8.0.0` (Node
script, `signalr-test/test-hub.js` in the session scratchpad), using the
query-string `access_token` transport that `ChatHub` documents as its auth path
(`JwtBearerEvents.OnMessageReceived` in `Program.cs`), forcing WebSockets with
`skipNegotiation: true` so the query-string path is actually exercised (not just
negotiated over HTTP first).

- **Coach token** -> connected, then `JoinConversationAsync` on the real
  coach/member conversation (`58d5c27c-d823-48a8-bc68-59f053b02252`) succeeded
  (`JOIN_OK`). This required `ChatHub.EnsureCallerHasAccessAsync` to read the
  `coachId` claim off `Context.User` and match it against the conversation's
  `CoachId` — proving identity resolution, not just a successful handshake.
- **Member token** -> same connection + join, succeeded via the `memberId` claim
  path.
- **Negative control** (garbage string as the token) -> WebSocket handshake failed
  outright, confirming `[Authorize]` on `ChatHub` is genuinely enforced and the two
  successes above aren't a fluke of an open hub.

**Verdict: authenticates correctly, identity resolves correctly. No regression.**

## Seeder idempotence (restart check)

Row counts before stopping the API and after a full restart:

| Table | Before restart | After restart |
|---|---|---|
| `COACH_PARAMETERS` | 1 | 1 |
| `OFFER_PROGRAMS` | 4 | 4 |

(Single coach in the local DB; 4 is `OfferProgramSeeder`'s intended default catalogue
per coach, not a duplicate — the point being row counts are unchanged by the second
seeder run.) The restart's log shows both seeders' `NOT EXISTS` lookup queries ran
again, but produced **zero `INSERT` statements** the second time — direct evidence
the seeders correctly detected existing rows and skipped, not merely that counts
happened to match.

## Harness drift (disclosed per instruction)

`run-auth-tests.ps1` was modified *after* the pre-upgrade baseline run
(`auth-test-run-20260915-103213.md`, committed in `6468564`) was already committed.
The change (committed separately, see below) fixes two OTP-*acquisition* issues
found while running the harness against the upgraded code:

1. A fixed 300ms sleep before reading the OTP from the log was sometimes too short
   (console-redirect writes can be torn mid-flush), producing a truncated fragment
   read as the OTP line.
2. The original `Get-LatestOtp` had no way to distinguish "the new OTP line hasn't
   been flushed yet" from "there is no OTP line at all", so it could occasionally
   grab a **stale** OTP line left over from an earlier scenario in the same run.

Both are fixed by tracking the matching-line count immediately before issuing the
OTP request, then polling (up to 20 attempts x 200ms) until that count increases
*and* the newest line parses as a complete 6-digit code.

**This change touches only OTP acquisition inside `New-Session` — no assertion,
scenario, or expected-value logic changed.** The pre-upgrade baseline run
(committed) and the post-upgrade run in this document were therefore produced by
two not-quite-identical harness versions, differing only in how reliably each
fetches the OTP before verifying it. The comparison between the two runs' PASS/FAIL
results stands on that basis. Recorded here explicitly rather than left implicit.
