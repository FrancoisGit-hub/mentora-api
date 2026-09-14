# Auth claims backlog (Lot 7 / E1.1)

## Deferred: switch `ClaimTypes.NameIdentifier` reads to the literal `"sub"` claim

As of `3faa0f4` (this branch), `AddJwtBearer` pins `options.MapInboundClaims = true`
explicitly in `Mentora.API/Program.cs`, preserving the pre-existing implicit default.
This means 6 call sites still depend on the inbound claim-type mapping to read the
subject id, rather than reading the literal `"sub"` claim directly:

- `Mentora.API/Controllers/Auth/AuthController.cs` — `Logout` (1 site)
- `Mentora.API/Controllers/Auth/UserDevicesController.cs` (2 sites)
- `Mentora.API/Controllers/Auth/AccountController.cs` (3 sites)

All six do `Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value)`.

**Right long-term end state:** switch these to
`User.FindFirst(JwtRegisteredClaimNames.Sub)` (or the literal `"sub"` string), and
drop `MapInboundClaims` reliance entirely — matching how `memberId`/`coachId` are
already read via their raw literal claim types. This removes the dependency on
inbound claim-type mapping altogether rather than just pinning its current value.

**Not done in E1.1** because it changes application code paths, not just package
versions/config, and is out of scope for a JWT package upgrade sub-lot. Tracked here
as backlog for a future sub-lot.

## E1.2: fix `isCoach` always `false` on token refresh

`Mentora.Core/DTOs/Auth/AuthResponse.cs:3` declares
`bool IsCoach = false` as a default parameter.
`Mentora.Infrastructure/Services/AuthService.cs:180-184`
(`RefreshTokenAsync`'s `return new AuthResponse(...)`) never passes `IsCoach`, so
every refresh response reports `isCoach: false` regardless of the token's actual
role — confirmed at runtime in the Lot 7 baseline: a coach refresh returned
`"isCoach":false` even though the access token payload correctly carried
`"userType":"COACH"` and the `coachId` claim (see
`docs/review/lot7/auth-baseline-before.md`, scenario 4).

Contrast: `VerifyOtpAsync` (`AuthService.cs:94-101`) does pass `IsCoach:
effectiveIsCoach` correctly — only the refresh path is wrong.

**Fix:** pass `IsCoach: record.AuthRefreshTokenUserRole == UserRole.Coach` (or
equivalent) in the `RefreshTokenAsync` return, mirroring `VerifyOtpAsync`.

**Not fixed in E1.1** — confirmed in scope for **E1.2**, same controller/service as
the claim-mapping work, to be fixed and re-verified together in that sub-lot's
verification pass. Not touched in this session.

## Trap: `.gitignore`'s `[Dd]ebug/` pattern silently swallows source folders

`.gitignore:23` has `[Dd]ebug/`, intended to ignore .NET build-output `Debug/`
folders (`bin/Debug`, `obj/Debug`). It has no path anchor, so it also matches any
directory literally named `Debug` or `debug` anywhere in the tree — including a
*source* folder like `Mentora.API/Controllers/Debug/`. A `.cs` file placed there is
invisible to `git status`, `git add -A`, and code review, while still compiling and
running normally (`.gitignore` has no effect on `dotnet build`). Confirmed in Lot 7 /
E1.1: `Mentora.API/Controllers/Debug/DebugClaimsController.cs` (a temporary
claim-dump endpoint, deleted at session cleanup) never appeared in `git status`
despite being a real, compiled, authenticated endpoint.

**Risk:** any future source file — deliberate or accidental — under a path
component named `Debug`/`debug` silently never gets committed, reviewed, or
excluded from a Docker build context (see also: no `.dockerignore` exists in this
repo, so `docker build`'s `COPY . .` in `Mentora.API/Dockerfile:14` copies whatever
is physically present in the working directory, tracked or not).

**Not fixed in E1.1** — changing `.gitignore` or adding a `.dockerignore` is outside
this sub-lot's scope (JWT upgrade only). Tracked here as backlog.
