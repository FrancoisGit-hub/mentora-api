# Auth runtime baseline — BEFORE the JWT upgrade (Lot 7 / E1.1)

Captured against `feature/lot_7_jwt` at commit `12ab0f0` (after `3faa0f4` pinned
`options.MapInboundClaims = true` explicitly), local API on port 5243,
`ASPNETCORE_ENVIRONMENT=Development`, local Postgres (`mentora-postgres-local`,
port 5433).

Test accounts (pre-existing in the local DB, not created by this session):
- Coach: `coach@mentora.fr` — `CoachId = e268a512-84d6-4e27-b5c0-1d6c08a47190`, `UserId = ff2be73b-8a80-46d6-90d8-cd462b876095`. This account also has a Member profile (dual-role), so `isCoach` must be passed explicitly on OTP request/verify.
- Member: `member@mentora.fr` — `MemberId = 38b05ed2-e577-4cf7-81e9-365eaafab626`, `UserId = 874d7734-cc4f-47ee-9527-3a4c0262f7b3`. Attached (`IS_PRIMARY = true`) to coach `e268a512-...`.

Full issued/resolved claim payloads: `docs/review/lot7/claims-before.json`.

## Status-code matrix

| # | Scenario | Expected | Actual | Notes |
|---|---|---|---|---|
| 1 | Valid access token on protected **coach** endpoint (`GET /api/v1/coach/me`) | 200 | **200** | |
| 2 | Valid access token on protected **member** endpoint (`GET /api/v1/member/me`) | 200 | **200** | |
| 3 | Tampered access token (signature corrupted) on `GET /api/v1/coach/me` | 401 | **401** | |
| 4 | Refresh with a valid, unused refresh token | 200 | **200** | Re-run cleanly after an unrelated PowerShell/IE-parsing artifact consumed the first attempt (see caveat below) |
| 5 | Refresh reusing the same token immediately after (now revoked) | 400 | **400** | **Known preexisting gap** — should be 401, not fixed in this sub-lot per scope |
| 6a | `POST /api/v1/auth/logout` with a valid access token | 200 | **200** | Also satisfies the amendment's explicit logout check |
| 6b | Refresh attempted with the token `6a` just revoked via logout | 400 | **400** | Same preexisting gap as #5 — logout-revoked and rotation-revoked tokens both surface as 400 |
| 7 | `CoachOnly` policy hit with a **member** token (`GET /api/v1/coach/me`) | 403 | **403** | |
| 8 | `MemberOnly` policy hit with a **coach** token (`GET /api/v1/member/me`) | 403 | **403** | |
| 9 | Scoped coach endpoint (`GET /api/v1/coach/members/{memberId}/parameters`) with a `memberId` not attached to the calling coach (random GUID) | 404 (never 403) | **404** | |

### Amendment — the 6 `ClaimTypes.NameIdentifier` call sites, exercised for real

| # | Scenario | Expected | Actual | Resolved user id |
|---|---|---|---|---|
| 10 | `POST /api/v1/auth/logout` valid token | 200 | **200** | (see #6a — same call) |
| 11a | `POST /api/v1/devices` (`UserDevicesController.Register`), valid coach token | 200 | **200** | Row inserted with `USER_ID = ff2be73b-8a80-46d6-90d8-cd462b876095` (verified by SQL, then removed by 11b) |
| 11b | `DELETE /api/v1/devices` (`UserDevicesController.Delete`), same token | 200 | **200** | Row for that token removed; `USER_DEVICES` confirmed empty for this user afterward |
| 12a | `POST /api/v1/account/deletion-request` (`AccountController.RequestDeletion`), valid coach token | 200 | **200** | `USERS."USER_DELETION_REASON"` set for `UserId = ff2be73b-...` (confirmed via the immediate GET in 12b) |
| 12b | `GET /api/v1/account/deletion-request` (`AccountController.GetDeletionStatus`) | 200 | **200** | Body: `{"isPending":true,"requestedAt":"2026-09-14T16:47:54.67145Z","reason":"lot7 e1.1 baseline check"}` — confirms the write in 12a landed on the correct user |
| 12c | `DELETE /api/v1/account/deletion-request` (`AccountController.CancelDeletion`) | 200 | **200** | `USERS."USER_DELETION_REASON"`/`"USER_DELETION_REQUESTED_DATE"` confirmed reset to null for `UserId = ff2be73b-...` after cancel |

**No `NullReferenceException` or 500 on any of the 6 `ClaimTypes.NameIdentifier` call sites.** All resolve correctly to the token's `sub` claim (`ff2be73b-8a80-46d6-90d8-cd462b876095`, the coach's `UserId`), consistent with `MapInboundClaims = true` mapping `sub` to `ClaimTypes.NameIdentifier` as recorded in `claims-before.json`.

## Caveat on run #4 (documented plainly, not hidden)

The first attempt at scenario 4 threw a PowerShell-side exception unrelated to the
API (`Invoke-WebRequest` without `-UseBasicParsing` tried to use the IE COM
rendering engine, which isn't available in this non-interactive shell — pure
tooling issue). The HTTP request had already reached the server and rotated the
refresh token before the client-side exception was thrown, silently invalidating
the original refresh token client-side scripting was tracking. All subsequent
`Invoke-ApiCall`s use `-UseBasicParsing`. Scenario 4 was then re-run end-to-end
with a fresh OTP verification to get a clean, never-touched refresh token, and
that clean run is what's recorded above (200). This was a test-harness mistake,
not an API behavior change — noted for transparency per your instruction to say
plainly when something doesn't match expectations rather than bend the data.

## Observation (out of scope, not fixed)

`AuthService.RefreshTokenAsync` returns `AuthResponse` without setting `IsCoach`
(only `VerifyOtpAsync` sets it), so the refresh response's `isCoach` field is
always `false` regardless of actual role — confirmed: a coach refresh returned
`"isCoach":false` even though the access token payload correctly shows
`"userType":"COACH"` and the `coachId` claim. Functionally harmless (nothing reads
`isCoach` off the refresh response server-side, and the access token itself is
correct), but worth flagging as a client-facing correctness gap for a future
sub-lot. Not touched here — out of scope per session rules (no refactoring beyond
the JWT upgrade and the claim-mapping pin).
