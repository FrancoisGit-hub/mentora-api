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
