# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

### Build & Run
```bash
# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run the API
dotnet run --project Mentora.API

# Run with Development environment (uses local DB)
ASPNETCORE_ENVIRONMENT=Development dotnet run --project Mentora.API
```

### Database
```bash
# Start local PostgreSQL (port 5433)
docker compose -f docker-compose.local.yml up -d

# Add a new migration (run from solution root)
dotnet ef migrations add <MigrationName> --project Mentora.Infrastructure --startup-project Mentora.API

# Apply migrations manually
dotnet ef database update --project Mentora.Infrastructure --startup-project Mentora.API
```

> Migrations also run automatically at startup via `db.Database.Migrate()` in `Program.cs`.

## Architecture

3-layer clean architecture:

- **Mentora.Core** — Domain entities only. No dependencies on other projects.
- **Mentora.Infrastructure** — EF Core `MentoraDbContext` + migrations. References Core. Uses Npgsql (PostgreSQL).
- **Mentora.API** — Controllers (not Minimal APIs), middlewares, authorization policies, DI wiring, Swagger. References both Core and Infrastructure.

## Domain Model

The core domain is a coaching platform:

- `User` is the base identity. Role stored as a string field (`UserRole`). A user is either a `Coach` or a `Member` (1:0..1 relationship to each).
- `Coach` can have an `AgentToken` (for AI agent authentication).
- `Member` connects to coaches via the `MemberCoach` join entity (MEMBER_COACHES table), which carries `IsPrimary` and `StartedAt`. A member can have multiple coaches.
- Auth is OTP-based: `AuthOtp` (one-time codes) and `AuthRefreshToken` (session tokens) are linked to `User`.

Entity naming convention: properties are prefixed with the entity name (e.g., `User.UserId`, `Coach.CoachFirstName`).

## Local Database

Development connection string (in `appsettings.Development.json`):
```
Host=localhost;Port=5433;Database=mentora_db;Username=mentora_user;Password=mentora_pass
```

Docker Compose runs PostgreSQL 16 on port **5433** (not the default 5432).

## API

- Swagger UI available at `/swagger` in all environments. Includes JWT Bearer auth support (use the Authorize button to test protected endpoints).
- Endpoints follow the pattern `/api/v1/<resource>`.
- Response shape: `{ success, data, error, statusCode }`.

## Architectural decisions (Lot 2)

### No repository layer
Services call `MentoraDbContext` directly (same pattern as Lot 1 `AuthService`).
The functional spec mentions Controller → Service → Repository, but we deliberately
skip the repository abstraction to stay consistent with the existing codebase and
avoid premature indirection. If this is revisited, start at the service boundary.

### JWT claims (from Lot 2.1)
`AuthService.GenerateAccessToken` emits:
- `sub` — UserId
- `email` — user email
- `userType` — `"MEMBER"` | `"COACH"` | `"BOTH"`
- `memberId` — MemberId UUID (present when the user has a Member profile)
- `coachId`  — CoachId UUID (present when the user has a Coach profile)

Authorization policies in `Program.cs`:
- `CoachOnly`  — requires `userType` = `COACH` or `BOTH`
- `MemberOnly` — requires the `memberId` claim AND `userType` = `MEMBER` (`Program.cs:185-187`)
- `QuizPlayer` — quiz routes: no `Authorization` header = anonymous device mode (`X-Device-Id`); invalid token → 401; member token → 403

### Exception → HTTP status mapping
`GlobalExceptionMiddleware` maps:
- `NotFoundException`        → 404
- `UnauthorizedException`    → 401
- `ForbiddenException`       → 403
- `ConflictException`        → 409 (+ details)
- FluentValidation `ValidationException` → 422 (+ errors)
- `InvalidOperationException` → 400
- any other `Exception`      → 500

Lot 1 auth controllers still use inline try/catch — they are **not** affected by the
middleware and will be retrofitted in a later lot.

### Startup seeders
After `db.Database.MigrateAsync()`, these idempotent seeders run (`Program.cs`):
- `CoachParameterSeeder` — creates default parameters (50 €/h, 24 h delay) for
  every coach that has no `COACH_PARAMETERS` row.
- `OfferProgramSeeder`   — seeds 4 default programs for every coach that has zero
  active `OFFER_PROGRAMS` rows.
- `ExerciseSeeder`, `MuscleGroupSeeder`, `CatalogSeeder`, `ProgramTemplateSeeder`, `QuizQuestionSeeder` — reference data
  (exercises, muscle groups, catalog, program templates, quiz questions).

## Email routing

Environment variables only, never in an appsettings file, never a value in a doc: `EmailRouting__Mode`, `EmailRouting__TestRecipients`, `EmailRouting__ProductionBcc`, `Email__Provider` (`Smtp` or `Logging`), `Email__FromAddress`, and the `Email__Smtp__*` keys (`Host`, `Port`, `UseStartTls`, `Username`, `Password`).
- Any `EmailRouting__Mode` other than `Production` is TEST mode: every mail goes only to `EmailRouting__TestRecipients`.
- The API refuses to start (read before migrations) when `EmailRouting__TestRecipients` is empty or holds an invalid or duplicate address, even in Production mode.
- Production mode also requires `EmailRouting__ProductionBcc` with at least one valid address.
- With `Email__Provider=Smtp`, the refusal covers `Email__Smtp__Host`, `Email__Smtp__Port`, `Email__FromAddress`, `Email__Smtp__Username` and `Email__Smtp__Password`; `Logging` needs none of them.
- api-demo stays in test mode until the Alpha.
