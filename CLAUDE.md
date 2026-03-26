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
- **Mentora.API** — Minimal API endpoints, DI wiring, Swagger. References both Core and Infrastructure.

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
