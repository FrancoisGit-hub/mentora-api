# Lot 12, wave 1 review fixes: R2, R6, R8, R9 (visible text), R10 (admin secret guard, blank line)

Branch `feature/lot_12`, base `0839658`. Local run only (Docker PostgreSQL 16 on 5433, container `mentora-postgres-local`, API on 5243, `ASPNETCORE_ENVIRONMENT=Development`, `Email__Provider=Logging`). No token, OTP, secret, connection string or address appears below. The admin used is admin #1; the app accounts are "the member" and "the coach". Tokens and OTPs stayed in variables of the driver scripts (never in a file, never printed). API logs were written to the session scratchpad outside the repository and deleted after the proofs.

F1 to F5 were edited in auto mode and reviewed line by line by Claude (chat) from the diffs shown, with no finding. From the F1 follow-up on, every write, start and SQL had explicit approval.

Decision recorded in the report: R1 is not fixed (native Flutter apps, no browser CORS), line added under R1 in `review-wave1.md`. R3, R4, R5, R7, the em dashes in code comments and the other R10 items stay in the backlog.

---

## 1. Fixes

| Fix | Finding | Change |
|---|---|---|
| F1 | R2 | `AdminExerciseService.BuildState` keeps a stored value the request does not decide. `isPolyarticular`: recomputed only when the request carries a type, otherwise the stored value. Legacy `EXERCISE_MUSCLE_GROUP`: recomputed when the category or the primary muscle group changes (decision of 2026-11-10, a category change counts too), otherwise the stored value. Legacy `EXERCISE_EQUIPMENT` (read by the mobile and coach endpoints, same rule): recomputed only when the primary equipment changes. Create is unchanged (nothing stored yet). The "nothing changed" detection then sees an unchanged resave as no change. |
| F2 | R6 | `docs/review/lot12/12.5a/prod-runbook.md`, check A: second read-only query ignoring accents (`translate()` with the two strings of `Mentora.Core/Text/AccentFolding`, `EXERCISE_COACH_ID IS NULL`, `lower(btrim(...))`, `GROUP BY`, `HAVING count(*) > 1`), with the French instruction (rename one exercise on api-demo before deploying, with François). |
| F3 | R8 | `.gitattributes`: `Mentora.Admin/wwwroot/index.html text eol=lf`. |
| F4 | R9 | Em dash replaced by a colon in the five visible texts only: admin code mail (HTML title and first text line, `AdminAuthService.cs`), Swagger admin title (`Program.cs`), Swagger tags of `AdminAuthController` and `AdminExercisesController`. |
| F5 | R10 | `StartupGuards`: outside Development `AdminAuth__JwtSecret` must be an environment variable of at least 32 characters (same pattern and message style as `Jwt__Secret`, no value in a message). Blank line removed at `AuthService.cs:175`. |

Diff: 8 modified files and 1 new file (`.gitattributes`), 45 insertions, 9 deletions.

## 2. Proofs

### P1. Build

`dotnet build --no-incremental` (run twice: after F1 to F5, and after the F1 follow-up): 19 warnings, all CS1573 (19 unique), 0 other warning, 0 error, 0 warning from Mentora.Admin.

### P2. R2 on the local API (admin token, OTP read from the API log into a variable)

Fixtures chosen read-only: (a) `LG07` (no type, `isPolyarticular` true, legacy `BACK`), (b) `EX009` (legacy `TRICEPS`, `isPolyarticular` false).

Before: audit rows 0; (a) `xmin` 2024; (b) `xmin` 2026.

(b) EX009:
- GET 200, then PUT unchanged 200, body identical to the GET body, GET after identical to the first GET.
- Snapshot (`xmin`, `isPolyarticular`, legacy muscle group, legacy equipment, md5 of the whole row without the updated date) identical before and after: `xmin` 2026, audit rows 0, `f`, `TRICEPS`, `CABLE`.
- One real change (`coachTip` null to a temporary text): PUT 200, `xmin` 2227, audit rows 1, the audit row lists only `coachTip` (before null, after the temporary text), `isPolyarticular`, `TRICEPS` and `CABLE` unchanged.
- Restore with a second PUT built from the initial GET: 200, row hash equal to the initial one, final admin GET equal to the initial (updated date ignored), legacy columns equal to the initial ones. `xmin` 2228.
- A second change and restore cycle (for P3) left the same final state (row hash equal to the initial one, `xmin` 2237).

(a) LG07:
- GET 200. The PUT unchanged returns **422** (`CategoryId` and `PrimaryEquipmentId` required): LG07 holds no category and no primary equipment (all legacy text values null), so the form cannot be saved as is. Nothing is written: `xmin` 2024, row hash unchanged, final GET identical to the first one.
- This is not specific to LG07: of the 27 Mentora LG exercises without a type, 0 holds a category (so none can be resaved unchanged; an admin save always has to choose a category, and an equipment when missing).
- Consequence: the "no type keeps `isPolyarticular`" rule of F1 could not be proven by a unchanged PUT on an LG exercise. A proof needs a PUT that adds a category (LG18 would fit: polyarticular true, equipment set, only the category missing), which cannot be undone through the API. That write was not made. Decision (François, 2026-11-10): no write on LG18. Accepted as proven by code review: a typeless LG exercise cannot be saved without a category (422 from the validator), and the kept value is a single conditional reviewed line by line; runtime proof covered by (b).

### P3. Mobile and coach contract (`GET member/exercises/{id}`, `GET coach/exercises/{id}`, exercises (a) and (b))

- Before and after the unchanged PUTs: status 200 for the four calls, raw bodies identical (True for member-a, coach-a, member-b, coach-b).
- After a real change and its restore on (b): same 32 keys, same key set, the only differing key is `updatedDate` (the mobile `ExerciseResponse` exposes it; the write moved it). Every other value is identical. (a) identical to the initial bodies.

### P4. R10 startup refusal (Production, process variables only)

Process only: `ASPNETCORE_ENVIRONMENT=Production`, `ConnectionStrings__DefaultConnection` a dummy value (`Host=127.0.0.1;Port=1;...`, never a real one), `Jwt__Secret` random 48 characters generated in the command, started with `dotnet run --no-build --no-launch-profile`.

- `AdminAuth__JwtSecret` removed from the process: refusal `InvalidOperationException: AdminAuth__JwtSecret must be set as an environment variable outside Development (the appsettings.json default is never accepted).`, 0 migration or database line, 0 "Now listening" line.
- `AdminAuth__JwtSecret` set to 10 characters: refusal `AdminAuth__JwtSecret must be at least 32 characters.`, same counts (0, 0).
- Port 5243 free after both runs (0 listener), 0 Mentora process left.
- Lesson: without `--no-launch-profile`, `dotnet run` applies `launchSettings.json` and forces Development, so the guard is skipped (a first attempt without the flag showed only the older `AdminAuthOptions` validation message). The flag is needed for any Production test with `dotnet run`.

### P5. R8

`.gitattributes` effect: `git check-attr -a` on the file gives `text: set` and `eol: lf`; `git ls-files --eol` gives `i/lf w/lf attr/text eol=lf`. It is the only one of the 846 tracked files with a non-empty attribute (`attr/` empty for the 845 others). The working copy holds 0 CR. CSP hash of the inline script (one inline script, UTF-8 bytes between the tags): `sha256-oW+3IccTLsL4xJ3s2rKPxyyczlz2dfeyKhTyG6r3Zkc=`, equal to the one of `nginx/conf.d/mentora-admin.conf.example`.

### P6. Dashes and leak checks

- Added lines of the diff: 45, em dash 0, en dash 0. The 5 removed lines that held an em dash are exactly the five F4 targets.
- Patterns proven first on their known file (count of matching lines): JWT shape on `docs/review/lot7/claims-after-4a.json` = 2; the console OTP marker (literal match) on `Mentora.Infrastructure/Services/AuthService.cs` = 1; address shape on `docs/review/lot11/11.4/runtime-evidence.md` = 7; `Password=` on `Mentora.API/appsettings.Development.json` (gitignored, count only) = 1.
- Explicit list of the 11 files to commit (9 for the code commit: `.gitattributes`, the 7 changed `.cs` files, `prod-runbook.md`; 2 for the docs commit: `review-wave1.md` and this file): JWT 0, address 0. OTP marker: 1 line, in `AuthService.cs`, the existing console line, 0 added by this diff; 0 in the other 10 files, this one included. `Password=`: `prod-runbook.md` 4 lines (the variable name `POSTGRES_PROD_PASSWORD=` twice with no value, and two `${...}` references), `review-wave1.md` 1 line (prose), 0 added by this diff.

## 3. Cleanup

- `db.ps1` (first helper) deleted, `Test-Path` False (checked twice). The driver scripts and the API and P4 logs of the scratchpad were deleted at the end (OTP codes were in the API logs).
- API stopped, port 5243 free, 0 Mentora process.
- Tokens: never written to a file. Rows left by the logins in the authentication tables (OTPs, refresh tokens, last login dates) are normal rows, as in the previous lots.
- Fixtures: the local `ADMIN_AUDIT_LOG` holds 4 rows left by the proofs (2 real changes and 2 restores of EX009, all `EXERCISE_UPDATED`), count was 0 before. Decision (François, 2026-11-10): they stay (local development database only, same as the existing local test OTP and refresh rows); no SQL write was made. The EX009 exercise itself is back to its initial state except its updated date and `xmin`.

## 4. Backlog (no code change in this commit)

- R3 admin code request timing (mail sent in the request).
- R4 concurrent wrong codes can exceed the attempts limit.
- R5 lost refresh response or no `navigator.locks` revokes every session.
- R7 deactivation race and inactive values.
- R9 em dashes in code comments and XML doc (58 lines, list in the report).
- R10 remaining items: admin rate limit shared by an office NAT, disabled admin read access up to 15 minutes, `ADMIN_OTPS` and `ADMIN_REFRESH_TOKENS` never purged, `ReferenceNormalizationPatch` drops unmapped secondary muscles (pg_dump is the only way back).
- New: LG exercises without a category cannot be saved without choosing one, and a category change now also recomputes the legacy muscle group (decision of 2026-11-10).
- New: the `isPolyarticular` keep rule has no runtime write proof on an LG exercise; accepted by code review (see P2).
