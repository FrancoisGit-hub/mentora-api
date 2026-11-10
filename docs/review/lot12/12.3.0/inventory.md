# Lot 12, sub-lot 12.3.0: read-only inventory for MUX videos (wave 2)

Date: 10/11/2026. Branch feature/lot_12. Read-only: no build, no run, no code change. Every line number refers to the current working tree at 3b9d876.

## Pre-flight

```
> git --no-optional-locks branch --show-current
feature/lot_12

> git --no-optional-locks status --short
(empty)

> git --no-optional-locks log --oneline -1
3b9d876 docs(lot12): VPS deployment evidence and runbook as deployed

> git --no-optional-locks rev-parse origin/main
3b9d87650fe9d3935eb6664867cafc5dc3c59f4a
```

All four match the expected values.

---

## API side

### I1. EXERCISE_VIDEOS

**Entity** `Mentora.Core/Entities/ExerciseVideo.cs:9-27`:

```csharp
public Guid ExerciseVideoId { get; set; }
public Guid ExerciseVideoExerciseId { get; set; }
public Exercise Exercise { get; set; } = null!;
public string ExerciseVideoMuxAssetId { get; set; } = null!;
public string? ExerciseVideoMuxPlaybackId { get; set; }
public VideoPlaybackPolicy ExerciseVideoPlaybackPolicy { get; set; } = VideoPlaybackPolicy.Public;
public ExerciseVideoStatus ExerciseVideoStatus { get; set; } = ExerciseVideoStatus.Waiting;
public decimal? ExerciseVideoDurationSeconds { get; set; }
public string? ExerciseVideoAspectRatio { get; set; }
public DateTime ExerciseVideoCreatedDate { get; set; }
public DateTime ExerciseVideoUpdatedDate { get; set; }
```

No column for a MUX upload id, an error message or a passthrough today. Navigation from the exercise: `Exercise.Video` (`Mentora.Core/Entities/Exercise.cs:71`), one-to-one.

**Configuration** `Mentora.Infrastructure/Persistence/Configurations/ExerciseVideoConfiguration.cs`:
- table `EXERCISE_VIDEOS` (:13), PK `EXERCISE_VIDEO_ID` default `gen_random_uuid()` (:15-18);
- `EXERCISE_VIDEO_EXERCISE_ID` required (:20-22);
- `EXERCISE_VIDEO_MUX_ASSET_ID` **required**, varchar(100) (:24-27);
- `EXERCISE_VIDEO_MUX_PLAYBACK_ID` nullable varchar(100) (:29-31);
- `EXERCISE_VIDEO_PLAYBACK_POLICY` required varchar(10), converter, default `'PUBLIC'` (:33-42);
- `EXERCISE_VIDEO_STATUS` required varchar(10), converter, default `'WAITING'` (:44-53);
- `EXERCISE_VIDEO_DURATION_SECONDS` numeric(8,2) (:55-57); `EXERCISE_VIDEO_ASPECT_RATIO` varchar(10) (:59-61);
- created and updated dates required (:63-69);
- unique index `UX_EXERCISE_VIDEOS_EXERCISE` on the exercise id (:73-75), comment at :71-72 says "a later lot lifts" it;
- unique index `UX_EXERCISE_VIDEOS_MUX_ASSET` on the asset id (:77-79);
- FK to EXERCISES, `OnDelete(DeleteBehavior.Cascade)` (:81-84);
- private if-chain converters for policy (:87-99) and status (:101-115), duplicating EnumMappings.

**Enums**
- `Mentora.Core/Enums/ExerciseVideoStatus.cs:7-12`: `Waiting, Ready, Errored` ("Persisted as VARCHAR(10) UPPERCASE", :5).
- `Mentora.Core/Enums/VideoPlaybackPolicy.cs:7-11`: `Public, Signed`.

**Migration** `Mentora.Infrastructure/Persistence/Migrations/20260929171450_Lot9_ExerciseCatalog.cs`
- CreateTable `EXERCISE_VIDEOS` at :152-175 (asset id `nullable: false` at :157, FK cascade at :169-174).
- Indexes at :262-272.
- CHECK constraints (raw SQL), Up:

```sql
-- :327-333
ALTER TABLE "EXERCISE_VIDEOS"
ADD CONSTRAINT "CK_EXERCISE_VIDEOS_POLICY" CHECK (
    "EXERCISE_VIDEO_PLAYBACK_POLICY" IN ('PUBLIC','SIGNED')
);
-- :335-341
ALTER TABLE "EXERCISE_VIDEOS"
ADD CONSTRAINT "CK_EXERCISE_VIDEOS_STATUS" CHECK (
    "EXERCISE_VIDEO_STATUS" IN ('WAITING','READY','ERRORED')
);
```

- Down (:369-378) drops both: `ALTER TABLE "EXERCISE_VIDEOS" DROP CONSTRAINT "CK_EXERCISE_VIDEOS_STATUS";` (:374) and `... "CK_EXERCISE_VIDEOS_POLICY";` (:375), then DropTable `EXERCISE_VIDEOS` (:399).

**What a migration would have to change so a row can exist with an upload id and no asset id yet** (facts only, no migration written):
1. `EXERCISE_VIDEO_MUX_ASSET_ID` is `NOT NULL` (configuration :26, migration :157). It must become nullable (entity `string?` + configuration without `IsRequired()` + `AlterColumn ... nullable: true`), otherwise a placeholder value would be needed.
2. An upload id column does not exist. It would need a new column (MUX upload ids; length to be chosen) and probably a unique index of its own.
3. Unique index on a nullable column in PostgreSQL: by default NULLs are distinct, so `UX_EXERCISE_VIDEOS_MUX_ASSET` keeps working with many NULL asset ids (several rows with NULL do not collide). The repo already uses explicit partial unique indexes for nullable codes (`UX_EXERCISES_SOURCE_CODE ... WHERE "EXERCISE_SOURCE_CODE" IS NOT NULL`, migration :351-356) and `NULLS NOT DISTINCT` elsewhere (`ExerciseConfiguration.cs:161-164`); either style is a precedent. A partial index cannot be expressed by `HasIndex().IsUnique()` alone (needs `.HasFilter(...)` or raw SQL).
4. `UX_EXERCISE_VIDEOS_EXERCISE` (one row per exercise) means a replacement upload must update the existing row or delete it first; it cannot coexist with a previous row.
5. `CK_EXERCISE_VIDEOS_STATUS` allows only WAITING / READY / ERRORED. Any new state (for example an upload-waiting state distinct from asset preparing, a timed-out or cancelled upload) requires dropping and re-creating that CHECK in Up, with the inverse in Down, plus the column length (varchar(10)) if a longer wire value is chosen, plus the enum, both converters (configuration :101-115 and EnumMappings, see I2) and the DTO doc comment.
6. A "consistency" CHECK (asset id NULL only while status is WAITING, for example) does not exist and would be new.

### I2. EnumMappings

`Mentora.Core/Enums/EnumMappings.cs`:

```csharp
// :441-461
public static class ExerciseVideoStatusMapping
{
    public static readonly ReadOnlyCollection<string> WireValues =
        new(["WAITING", "READY", "ERRORED"]);
    public static ExerciseVideoStatus Parse(string value) { ... }   // :446-452
    public static string ToWire(ExerciseVideoStatus value) { ... }  // :454-460
}
// :463-481
public static class VideoPlaybackPolicyMapping
{
    public static readonly ReadOnlyCollection<string> WireValues =
        new(["PUBLIC", "SIGNED"]);
    ...
}
```

Callers (whole repo, *.cs):
- `ExerciseVideoStatusMapping.ToWire`: `Mentora.Infrastructure/Services/ExerciseService.cs:449` and `Mentora.Infrastructure/Services/AdminExerciseService.cs:349`. `Parse` and `WireValues` have no caller.
- `VideoPlaybackPolicyMapping`: **no caller at all**. The policy is never exposed on the wire.
- The DB converters are separate private copies in `ExerciseVideoConfiguration.cs:87-115`.

### I3. EXERCISES columns that touch video

- **`EXERCISE_VIDEO_URL`**: entity `Exercise.cs:16` (`string? ExerciseVideoUrl`), configuration `ExerciseConfiguration.cs:37-39` varchar(500).
  - Written only by the coach exercise service: `ExerciseService.cs:92` (create, `request.VideoUrl?.Trim()`) and `:123` (update). Validator `Mentora.Core/Validators/Exercise/ExerciseRequestValidator.cs:23-25` (max 500). Routes: `POST /api/v1/coach/exercises` and `PUT /api/v1/coach/exercises/{id}` (`CoachExercisesController.cs:82,104`).
  - Exposed as `videoUrl` by `ExerciseResponse` (`ExerciseService.cs:414`, DTO `ExerciseResponse.cs:42`), so on every coach and member exercise detail and coach list route (see I4).
  - Exposed as `exerciseVideoUrl` in program trees: `ProgramService.cs:1007`, DTO `Mentora.Core/DTOs/Program/ProgramExerciseResponse.cs:13`.
  - The admin never reads nor writes it: absent from `AdminExerciseService.cs` (no match), from `AdminExerciseResponse` (`AdminExerciseDtos.cs:118-148`) and from the admin front models.
- **Exercise code**: column `EXERCISE_SOURCE_CODE`, entity `ExerciseSourceCode` (`Exercise.cs:29`), varchar(10) (`ExerciseConfiguration.cs:85`), nullable, partial unique index `UX_EXERCISES_SOURCE_CODE` (Lot9 migration :351-356). Admin creation allocates `"EX" + (highest + 1).ToString("D3")` under an advisory lock (`AdminExerciseService.cs:431-447`). Legacy codes are `LG` + two digits (`ExerciseSeeder.cs:251`, `$"LG{index + 1:D2}"`), given by `LegacyExerciseCodePatch` only when a NULL-code Mentora exercise has a matching default name (`LegacyExerciseCodePatch.cs:8-13,32,46`). Admin search recognises `^(EX|LG)[0-9]+$` (`AdminExerciseService.cs:42-43`). Max 10 chars fits MUX `meta.external_id` (128). A Mentora exercise can still have a NULL code (see Open points).
- **Name**: `EXERCISE_NAME` required varchar(120) (`ExerciseConfiguration.cs:24-27`); admin form `maxlength="120"` (`Form.razor:113`). 120 is below MUX `meta.title` 512, so no truncation is needed.

### I4. Mobile and coach contract

**Loading**: `ExerciseService.WithDetail` includes the video (`ExerciseService.cs:292-298`, `.Include(e => e.Video)` at :297).

**Mapping** `ExerciseService.ToResponse` (`ExerciseService.cs:408-452`), video part at :445-451 (the passation reference 505-511 is outdated; **445-451 is current**):

```csharp
e.Video is null
    ? null
    : new ExerciseVideoResponse(
        e.Video.ExerciseVideoMuxPlaybackId,
        EnumMappings.ExerciseVideoStatusMapping.ToWire(e.Video.ExerciseVideoStatus),
        e.Video.ExerciseVideoDurationSeconds,
        e.Video.ExerciseVideoAspectRatio)
```

No status filter: a WAITING or ERRORED row is returned as is.

**DTO** `Mentora.Core/DTOs/Exercise/ExerciseResponse.cs:91-95`:

```csharp
public record ExerciseVideoResponse(
    string? PlaybackId,
    string Status,
    decimal? DurationSeconds,
    string? AspectRatio);
```

JSON keys (default ASP.NET Core camelCase, no custom naming policy on controllers: `Program.cs:411-418` only adds an enum converter): `playbackId`, `status`, `durationSeconds`, `aspectRatio`. The parent key is `video` (`ExerciseResponse.cs:68`). The sibling legacy key `videoUrl` stays at `ExerciseResponse.cs:42`.

Full `ExerciseResponse` key list, in order (`ExerciseResponse.cs:36-69`), for a later before/after comparison:
`exerciseId, coachId, name, description, instructions, videoUrl, imageUrl, muscleGroup, equipment, isPolyarticular, isActive, createdDate, updatedDate, sourceCode, nameEn, category, movementPattern, exerciseType, laterality, startingPosition, gripType, primaryMuscleGroup, secondaryMuscles, primaryEquipment, secondaryEquipment, level, breathing, commonMistakes, precautions, coachTip, objectives, video { playbackId, status, durationSeconds, aspectRatio }`.

**Routes whose response contains `video`**
| Route | Controller | Service | Shape |
|---|---|---|---|
| `GET /api/v1/coach/exercises` (list, not paged) | `CoachExercisesController.cs:36` | `ListForCoachAsync` → `ToResponse` (`ExerciseService.cs:59,68`) | full `ExerciseResponse` per item, with `video` |
| `GET /api/v1/coach/exercises/{id}` | `:66` | `GetByIdForCoachAsync` (:71-79) | with `video` |
| `POST /api/v1/coach/exercises` | `:82` | `CreateAsync` (:105) | `video` always null (new coach row, no Include needed) |
| `PUT /api/v1/coach/exercises/{id}` | `:104` | `UpdateAsync` (:114 WithDetail, :132) | with `video` |
| `GET /api/v1/member/exercises/{id}` | `MemberExercisesController.cs:55` | `GetByIdForMemberAsync` (:249-256) | with `video` |
| `GET /api/v1/admin/exercises/{id}`, `POST`, `PUT`, `PUT .../activation` | `AdminExercisesController.cs:78,94,112,132` | `LoadDetailAsync` → `ToDetail` (`AdminExerciseService.cs:273-295,345-351`) | admin DTO, same `ExerciseVideoResponse` |

**List versus detail (member)**: `GET /api/v1/member/exercises` (`MemberExercisesController.cs:31`) returns `ExerciseSummaryDto` (`ExerciseService.cs:229-234`): `exerciseId, name, level, primaryEquipment, secondaryEquipment`. **No video** in the member list. Program trees (`ProgramExerciseResponse`) carry only `exerciseVideoUrl`, no `video` object.

**What the apps receive today**
- No row: `"video": null`.
- WAITING: `{ "playbackId": null or value, "status": "WAITING", "durationSeconds": null or value, "aspectRatio": ... }` (no filtering).
- READY: same shape, `status` `"READY"`, playbackId expected non-null (nothing enforces it).
- ERRORED: same shape, `status` `"ERRORED"`.
- In practice: local table has 0 rows (section 6) and no code writes EXERCISE_VIDEOS (I13), so every app today only ever sees `video: null`.

### I5. Admin side

- Detail: `AdminExerciseService.cs:345-351` reuses `ExerciseVideoResponse` (same four keys as I4). Included at :287.
- Admin DTO: `AdminExerciseResponse.Video` (`Mentora.Core/DTOs/AdminExercise/AdminExerciseDtos.cs:144`), list item `HasVideo` (:98).
- List: `HasVideo = e.Video != null` (`AdminExerciseService.cs:174` and :217), i.e. **any row, whatever its status**, counts as "with video".
- No admin list filter on video: `ListAsync` parameters (`AdminExerciseService.cs:112-114`) and `ExercisesApi.ListAsync` (`Mentora.Admin/Api/ExercisesApi.cs:14-36`) have none.
- Front models: `Mentora.Admin/Api/ExerciseModels.cs:38` (`bool HasVideo`), :54 (`record ExerciseVideo(string? PlaybackId, string Status, decimal? DurationSeconds, string? AspectRatio)`), :82 (`ExerciseVideo? Video` on the detail).
- `List.razor` "Vidéo" column (`Mentora.Admin/Pages/Exercices/List.razor:136,153-162`): badge "Avec vidéo" when `item.HasVideo`, else muted badge "Sans vidéo". It does not show the status.
- `Form.razor` does not display `_detail.Video` anywhere today.

### I6. ADMIN_AUDIT_LOG

- Entity `Mentora.Core/Entities/AdminAuditLog.cs:9-24`: `AdminAuditLogAdminUserId` is a non-nullable `Guid` (:13) with required navigation `AdminUser` (:14); `Action`, `EntityType`, `EntityId`, `Changes` (jsonb string, doc at :20), `CreatedDate`.
- Configuration `Mentora.Infrastructure/Persistence/Configurations/AdminAuditLogConfiguration.cs`: admin user id `.IsRequired()` (:24), FK `FK_ADMIN_AUDIT_LOG_ADMIN_USER` with `DeleteBehavior.Restrict` (:52-56), action and entity type varchar(40) with converters (:26-40), changes jsonb required (:44-47), three indexes (:58-65).
- Enums and mappings `Mentora.Core/Enums/AdminAuditAction.cs`: actions `ExerciseCreated, ExerciseUpdated, ExerciseActivated, ExerciseDeactivated` (:4-10), entity type `Exercise` only (:13-16); DB values `EXERCISE_CREATED` etc. (:21-37), `EXERCISE` (:39-49).
- CHECKs in `20261109160428_Lot12_2b_AdminAuditLog.cs`: `CK_ADMIN_AUDIT_LOG_ACTION CHECK ("ADMIN_AUDIT_LOG_ACTION" IN ('EXERCISE_CREATED', 'EXERCISE_UPDATED', 'EXERCISE_ACTIVATED', 'EXERCISE_DEACTIVATED'))` (:55-56) and `CK_ADMIN_AUDIT_LOG_ENTITY_TYPE CHECK (... IN ('EXERCISE'))` (:62-63); Down drops both (:70-71).
- Writing: helper `NewAudit(adminUserId, action, exerciseId, changes, now)` (`AdminExerciseService.cs:621-631`), payload `JsonSerializer.Serialize(changes)`, added to `db.AdminAuditLogs` in the same `SaveChanges` as the change (:473, :545, :602-605). Payload shapes: created = dictionary of stored values (:468-470), updated = list `{ field, before, after }` (:941-953), activation = `{ isActive: { before, after } }` (:598-601). `EnsureAdminAsync` refuses a disabled admin with 401 (:614-619).
- **Reuse for video**: new action codes ("video upload created", "video removed", "video ready") need enum values, both mapping directions and a migration that drops and re-creates `CK_ADMIN_AUDIT_LOG_ACTION`. **A webhook entry cannot be written today**: the admin user id is NOT NULL with a FK to ADMIN_USERS (configuration :24, :52-56), so a row without an admin is impossible without a schema change (nullable column, or a system admin row, or no audit row for webhook events).

### I7. Options and startup

Files in `Mentora.Core/Options` and their registration in `Mentora.API/Program.cs`:

| Options | Section | Registration | Validation |
|---|---|---|---|
| `AdminAuthOptions` (`AdminAuthOptions.cs:9,11`) | `AdminAuth` | `AddOptions().Bind().ValidateOnStart()` :212-214 | `IValidateOptions` `AdminAuthOptionsValidator` :215 (class in `Mentora.Infrastructure/Services/Email/EmailOptionsValidators.cs:27`); `.Value` read before migrations :487-497 with extra cross-checks |
| `LiveKitOptions` (`LiveKitOptions.cs:3,5`) | `LiveKit` | :333-341 | inline `.Validate(...)` lambdas + `ValidateOnStart` |
| `QuizOptions` (`QuizOptions.cs:4,6`) | `Quiz` | :345-351 | inline `.Validate(...)` + `ValidateOnStart` |
| `RefreshTokenPurgeOptions` (`RefreshTokenPurgeOptions.cs:3,5`) | `RefreshTokenPurge` | `Configure<>` :369-370 | none |
| `EmailOptions` (`EmailOptions.cs:5,7`) | `Email` | :375-377 | `EmailOptionsValidator` :378 (`EmailOptionsValidators.cs:7`); `.Value` read before migrations :462-472 |
| `EmailRoutingOptions` (`EmailRoutingOptions.cs:10,12`) | `EmailRouting` | :379-381 | `EmailRoutingOptionsValidator` :382 (`EmailOptionsValidators.cs:17`); `.Value` read before migrations :462-472 |
| `CorsOptions` (`CorsOptions.cs:8,10`) | `Cors` | :431-433 | `CorsOptionsValidator` :434 (`Mentora.API/Startup/StartupGuards.cs:43-50`, delegates to `CorsOptions.Validate`, `CorsOptions.cs:43-68`); `.Value` read before migrations :476-483 |
| `JwtSettings` (`Mentora.Core/Settings`, not Options) | `Jwt` | `Configure<>` :163 | none in options; guarded by StartupGuards |

Order: `ValidateOnStart` alone fires when the host starts (`app.Run()`, :563), i.e. **after** migrations and seeders (:500-517). That is why Email, Routing, Cors and AdminAuth read `.Value` explicitly at :462-497, **before** `MigrateAsync` (:503). LiveKit and Quiz rely on `ValidateOnStart` only, so they fail after migrations.

`Mentora.API/Startup/StartupGuards.cs`:
- `VerifyOutsideDevelopment` (:15-39): outside Development, reads environment variables directly (not configuration): `Jwt__Secret` set and at least 32 chars (:20-26), `ConnectionStrings__DefaultConnection` set (:28-30), `AdminAuth__JwtSecret` set and at least 32 chars (:32-38). Messages carry no value.
- Called at `Program.cs:35`, the very first statement after `CreateBuilder`, before any service registration, migration or seeder.

`"Mux"` section in `Mentora.API/appsettings.json`: `Select-String -Path Mentora.API/appsettings.json -Pattern '"Mux"' -SimpleMatch -Quiet` → **False**.

### I8. Outbound HTTP

- No `HttpClient`, `IHttpClientFactory` or `AddHttpClient` in `Mentora.API`, `Mentora.Infrastructure` or `Mentora.Core` (grep on *.cs). The only `HttpClient` hits are in the admin front (`Mentora.Admin/Program.cs:17`, `Mentora.Admin/Api/ApiTransport.cs:11`).
- `Mentora.Infrastructure/Mentora.Infrastructure.csproj:8` has `<FrameworkReference Include="Microsoft.AspNetCore.App" />`; the API is `Microsoft.NET.Sdk.Web` targeting net8.0 (`Mentora.API.csproj:24`). `AddHttpClient` lives in `Microsoft.Extensions.Http`, which ships in the ASP.NET Core shared framework, so it is available in both projects with no new package (framework fact; not verified by a build, per the rules of this inventory). `System.Security.Cryptography.HMACSHA256` and `CryptographicOperations.FixedTimeEquals` are in the base library.
- Packages today (`Mentora.Infrastructure.csproj:12-17`): BCrypt.Net-Next, CsvHelper, MailKit, EF Core 8, Npgsql EF 8, System.IdentityModel.Tokens.Jwt. No MUX, no Polly.
- Patterns: SMTP: `SmtpEmailSender` creates a MailKit `SmtpClient` per send (`SmtpEmailSender.cs:30`), registered `AddScoped<SmtpEmailSender>()` (`Program.cs:385`) behind the `IEmailSender` factory (:387-396). LiveKit: no network call, the visio token is a locally signed JWT (`IVisioTokenService`, `Program.cs:342`), options at :333-341. So there is no typed-client precedent in the repo.
- No test project exists (only four csproj files: API, Infrastructure, Core, Admin).

### I9. Webhook precedent (Stripe)

- Controller `Mentora.API/Controllers/Internal/InternalStripeWebhookController.cs`: route `api/v1/internal/stripe` (:16), action `[HttpPost("webhook")]` (:34), Swagger group `internal` (:17), tag "System — Stripe" (:18). **No `[Authorize]` and no `[AllowAnonymous]`**: it is anonymous because no fallback policy exists (I10). TODO header (:1-4): no signature check yet, always 200 even on failure (:29-33).
- Raw body: `new StreamReader(Request.Body, Encoding.UTF8).ReadToEndAsync(ct)` (:39); no `EnableBuffering`, no `[FromBody]` parameter (so `NullBodyGuardFilter` does not apply, `NullBodyGuardFilter.cs:20-23`). Then `JsonDocument.Parse` (:41-49), 400 envelope on invalid JSON (:48, :65).
- Handler `Mentora.Infrastructure/Services/StripeWebhookHandler.cs:14-89`: idempotence by event id (lookup :19-30, returns `Idempotent: true`), archive row inserted with status RECEIVED and saved **before** processing (:32-43), dispatch on type (:48-67), PROCESSED or FAILED with error text (:69-87). Exceptions are swallowed (the controller always answers 200).
- Archive table `STRIPE_WEBHOOK_EVENTS` (`StripeWebhookEventConfiguration.cs:13-53`): `EVENT_ID` PK varchar(255) (:16-19), `EVENT_TYPE` varchar(100) (:21-24), `EVENT_PAYLOAD` jsonb (:27-30), `EVENT_RECEIVED_AT` default now() (:32-35), `EVENT_PROCESSED_AT` (:37-38), `EVENT_STATUS` varchar(20) RECEIVED/PROCESSED/FAILED (:44-49), `EVENT_PROCESSING_ERROR` text (:51-53).
- Reusable for MUX: the raw-body read pattern (no model binding, so the HMAC can be computed on the exact bytes received), the "insert by event id first, then process" idempotence pattern, the status values, the envelope. MUX events also carry an `id` and a `type`.
- Not reusable as is: no signature check exists (to be written: `Mux-Signature` parsing, timestamp tolerance, constant-time compare); the table and entity are Stripe-named (a MUX archive would need its own table or a generalisation); the "always 200" policy contradicts MUX retry semantics if the team wants retries on failure; the controller sits under `/internal` and in the `internal` Swagger doc while the planned route is `/api/v1/webhooks/mux`; `ReadToEndAsync` reads a string, while HMAC must be computed over the raw bytes (UTF-8 round-trip of a valid JSON body is identical, but reading bytes is the safe choice: design decision, left open).

### I10. Pipeline

`Mentora.API/Program.cs`, in order:
1. `StartupGuards.VerifyOutsideDevelopment` :35 (before build).
2. Options `.Value` checks :462-497, then `MigrateAsync` and seeders :500-517.
3. `app.UseForwardedHeaders()` :521 (options :451-456: XForwardedProto and XForwardedFor, known networks and proxies cleared).
4. `app.UseMiddleware<GlobalExceptionMiddleware>()` :524.
5. `UseSwagger` / `UseSwaggerUI` :526-535.
6. `app.UseCors("MentoraCorsPolicy")` :537.
7. `app.UseAuthentication()` :538.
8. `app.UseAuthorization()` :539.
9. `app.UseRateLimiter()` :540.
10. `MapGet("/api/v1/health")` :543-556, `app.MapControllers()` :558, `MapHub<ChatHub>("/hubs/chat")` :561.

- **No FallbackPolicy and no DefaultPolicy override** (`AddAuthorization` at :235-253 only adds named policies; grep finds neither). An action without `[Authorize]` is anonymous. Default authentication scheme is the app `Bearer` (:167); `AdminBearer` is never the default (:205-210).
- Rate limiter (:354-358): policies `QuizBasic` (`QuizRateLimiting.cs:11,23-38`, partition by client IP only when there is no `Authorization` header, else no limiter) and `AdminAuth` (`AdminAuthRateLimiting.cs:18-30`, fixed window per client IP, applied by `[EnableRateLimiting]` on `AdminAuthController.cs:18`). Shared 429 envelope and `Retry-After` (`QuizRateLimiting.cs:40-54`). No global limiter; `AdminExercisesController` has none.
- Request body limits: no `MaxRequestBodySize`, no `KestrelServerOptions`, no `FormOptions` in *.cs (grep). Kestrel default (about 30 MB) applies; appsettings Kestrel keys were not inspected (rule: appsettings not opened). nginx in front: `client_max_body_size 25M` (I19).
- `GlobalExceptionMiddleware` (`Mentora.API/Middleware/GlobalExceptionMiddleware.cs:8-67`) wraps every controller, webhook actions included. An exception thrown from a webhook action becomes: 404/401/403/409/422/400 by type (:14-61) or **500** `{ success: false, data: null, error: "An unexpected error occurred.", statusCode: 500 }` (:62-66, writer :69-95). Any non 2xx makes MUX retry (section 3 of the brief).

### I11. Admin authorization and routes

- `AdminAuthDefaults` (`Mentora.API/Authorization/AdminAuthDefaults.cs:7,10,13,16,19`): scheme `AdminBearer`, policy `AdminOnly`, rate limit policy `AdminAuth`, claim `adminId`, Swagger doc `admin`.
- Scheme registered at `Program.cs:207-210` (`MapInboundClaims = false`), parameters from `AdminAuthOptions` at :216-232 (HS256 only, zero clock skew). Policy at :238-241: `AddAuthenticationSchemes(AdminBearer).RequireAuthenticatedUser().RequireClaim("adminId")`.
- `AdminExercisesController` (`Mentora.API/Controllers/Admin/AdminExercisesController.cs:15-20`): `[Route("api/v1/admin")]`, `[Authorize(Policy = AdminAuthDefaults.Policy)]`, `[ApiExplorerSettings(GroupName = AdminAuthDefaults.SwaggerDoc)]`, `[Tags("Admin: Exercises")]`. Admin id from the claim, 401 if missing (:146-151). Envelope helper :153-154.
- Swagger "admin" assignment: by `ApiExplorerSettings.GroupName`, filtered by `DocInclusionPredicate` (`Program.cs:67-71`; no group means `internal`). Security per document: `SecuritySchemePerDocumentFilter` (:130).

Every route under `/api/v1/admin`:
| Method | Route | Source |
|---|---|---|
| POST | `/api/v1/admin/auth/otp/request` | `AdminAuthController.cs:16,26` |
| POST | `/api/v1/admin/auth/otp/verify` | `:38` |
| POST | `/api/v1/admin/auth/token/refresh` | `:50` |
| POST | `/api/v1/admin/auth/logout` | `:62` |
| GET | `/api/v1/admin/auth/me` | `:71` |
| GET | `/api/v1/admin/exercise-references` | `AdminExercisesController.cs:27` |
| GET | `/api/v1/admin/exercises` | `:52` |
| GET | `/api/v1/admin/exercises/{exerciseId:guid}` | `:78` |
| POST | `/api/v1/admin/exercises` | `:94` |
| PUT | `/api/v1/admin/exercises/{exerciseId:guid}` | `:112` |
| PUT | `/api/v1/admin/exercises/{exerciseId:guid}/activation` | `:132` |

Routes containing "webhook": `POST /api/v1/internal/stripe/webhook` and `POST /api/v1/internal/stripe/simulate-webhook` (`InternalStripeWebhookController.cs:16,34,88`). No route starts with `/api/v1/webhooks`.

Collision check: `POST /api/v1/admin/exercises/{id}/video-uploads`, `DELETE /api/v1/admin/exercises/{id}/video` and `POST /api/v1/webhooks/mux` match no existing template (full list of `[Route]` prefixes: grep on `Mentora.API`, 47 controllers). No collision.

### I12. CORS

- `Mentora.Core/Options/CorsOptions.cs`: section `Cors` (:10), `AllowedOrigins` comma separated (:13), `OriginList` trims, drops a trailing "/", dedupes (:19-35), `IsLocalhostOrigin` (:38-40), `Validate` (:43-68): at least one origin outside Development, no wildcard, absolute origin, no credentials, no path, https outside Development.
- Policy `MentoraCorsPolicy` (`Program.cs:436-449`): configured origins, plus in Development the fixed list `http://localhost:4200`, `http://localhost:8100`, `http://localhost`, `https://localhost`, `https://localhost:4200`, `https://localhost:8100`, `capacitor://localhost` (:421-430); any header, any method, credentials allowed.
- PROD (`api-prod`): `Cors__AllowedOrigins=https://admin.mentorapp.fr` (`docs/review/lot12/12.5a/prod-runbook.md:113`). api-demo: the runbook adds no `Cors__AllowedOrigins` line to the `api` service (:253-262).
- Ways the API could know the admin origin for MUX `cors_origin` (one origin per upload), without deciding:
  1. the configured list (`CorsOptions.OriginList`): exactly one value in PROD today, but it is a list by design;
  2. the request `Origin` header of the admin call creating the upload, which the CORS middleware has already matched against the allowed list;
  3. a dedicated option (for example a future `Mux` setting), not present today (I7: no "Mux" section).
- Local admin origin: the admin dev server URL is in `Mentora.Admin/Properties/launchSettings.json` (not opened); the Development list above does not obviously contain it (see Open points). `Mentora.Admin/wwwroot/appsettings.Development.json:2` points the admin to `http://localhost:5243`.

### I13. Other code reading or writing ExerciseVideos

- `MentoraDbContext.cs:47`: `DbSet<ExerciseVideo> ExerciseVideos`. **Never used** by any service, seeder or patch (grep `ExerciseVideos` outside migrations: only this line).
- Readers via the navigation `Exercise.Video`: `ExerciseService.cs:297,445-451`; `AdminExerciseService.cs:174,217,287,345-351`. No writer anywhere.
- No seeder creates videos (`ExerciseSeeder`, `CatalogSeeder`, patches: no match).
- Deactivation (decision A, `AdminExerciseService.SetActivationAsync` :563-609) and coach soft delete (`ExerciseService.DeleteAsync` :135-145) do not touch the video row. Hard delete of an exercise does not exist in code; the FK cascade would remove the row in the database only.
- Tests: none (no test project).

---

## Admin front side (Mentora.Admin)

### I14. `Pages/Exercices/Form.razor`

Structure:
| Lines | Section |
|---|---|
| 1-2 | routes `/exercices/nouveau`, `/exercices/{ExerciseId:guid}` |
| 13 | `<NavigationLock OnBeforeInternalNavigation="OnBeforeNavigation" ConfirmExternalNavigation="@IsDirty" />` |
| 15-33 | loading, not found, load error states |
| 36-40 | `<form @onsubmit="SaveAsync">`, page head with Back and "Enregistrer l'exercice" |
| 42-68 | incomplete fields line, flash, save error, 422 summary |
| 70-94 | edit bar (usage counts, Actif/Désactivé badge, activation button, action alert) |
| **96-157** | **"L'essentiel"** card (code, name, English name, category, level, primary group, primary/secondary equipment, search synonyms) |
| **159-175** | **"Muscles et objectifs"** card (secondary muscles, objectives) |
| 177-202 | "Le mouvement" card |
| 204-256 | "Consignes et sécurité" card |
| 257 | `</form>` |
| 260-263 | ConfirmDialog "Quitter sans enregistrer ?" |
| 265-268 | ConfirmDialog deactivation |
| 270-865 | `@code` |

The POC slot for the video block is therefore between line 157 (end of "L'essentiel") and line 159 (start of "Muscles et objectifs"), inside the `<form>`.

- Load: `OnParametersSetAsync` (:382-399) → `LoadAsync` (:403-446): references from `ReferencesCache` and detail from `ExercisesApi.GetAsync` in parallel; `Fill` (:461-497) copies the detail into `FormModel`; `_baseline = Snapshot()` (:445).
- Save: `SaveAsync` (:702-762): local required checks, `BuildRequest` (:567-593), `Api.CreateAsync` or `UpdateAsync`, success re-`Fill`s and resets the baseline; on create, flash and `Nav.NavigateTo("exercices/" + id, replace: true)` (:746-752).
- Dirty state: `IsDirty => ... Snapshot() != _baseline` (:366-367); `Snapshot` concatenates every form field (:546-554). Video state is not part of it. `OnBeforeNavigation` (:675-684) prevents navigation and opens the dialog; `ConfirmLeave` (:686-692), `CancelLeave` (:694-698). External navigation (tab close, reload) uses the browser prompt via `ConfirmExternalNavigation`.
- API errors: `ApplyFailureAsync` (:764-804): 404 → not found; 409 `EXERCISE_NAME_TAKEN` → link to the existing exercise (:774-781); 422 → summary + `FieldIds` mapping (:275-298), message "Valeur refusée, vérifiez ce champ." (:512), unmapped keys → one generic line (:790-791); other → `ExerciseText.Failure(result)` (:797). Activation 409 `EXERCISE_IN_USE` → `_actionAlert` (:845-848).
- The detail's `Video` (`ExerciseModels.cs:82`) is loaded but never rendered.

### I15. Api folder and services

- `Mentora.Admin/Program.cs:17-20`: one scoped `HttpClient` with `BaseAddress = config.ApiBaseUri` (fallback `http://invalid.local/`). No `IHttpClientFactory`, no `DelegatingHandler`.
- `ApiTransport` (`Mentora.Admin/Api/ApiTransport.cs:11-110`): the only typed client; builds a request on a **relative** path (`path.TrimStart('/')`, :22), JSON body (:23-24), adds `Authorization: Bearer` **only when the caller passes a token** (:25-26), maps network errors to status 0 (:39-42), parses the `{ success, data, error, details, errors }` envelope (:51-109).
- `ApiClient` (`Mentora.Admin/Api/ApiClient.cs:7-39`): gets the access token from `AdminSession` (:15), refresh before expiry, one refresh and one retry on 401 (:21-34).
- Refresh lock: `AdminSession.RunRefreshUnderLockAsync` (`AdminSession.cs:165-177`) calls `mentoraAdmin.runExclusiveRefresh` (`wwwroot/js/admin.js:73-80`, `navigator.locks.request(..., { mode: "exclusive" })`, fallback without lock).
- `ExercisesApi` (`Mentora.Admin/Api/ExercisesApi.cs:7-52`): all calls go through `ApiClient`, root `api/v1/admin/` (:9).
- **No HttpClient without auth for a foreign URL exists.** The token is not attached by a handler but passed explicitly, so the shared `HttpClient` itself carries no token; however its `BaseAddress` is the API and `ApiTransport` always expects the API envelope. A PUT to the MUX signed URL would need either a separate `HttpClient` instance, a direct `HttpRequestMessage` with an absolute URI on the existing client, or JavaScript (`fetch`/`XMLHttpRequest`). Not decided here.

### I16. JavaScript

- Files in `wwwroot` (source, excluding bin/obj): `wwwroot/js/admin.js` only.
- Script tags in `wwwroot/index.html`: inline theme bootstrap `<script>` (:14-28), `<script src="js/admin.js">` (:29), `<script src="_framework/blazor.webassembly.js">` (:42).
- `admin.js` exposes `window.mentoraAdmin` (:47-81): `getThemeMode`, `setThemeMode`, `syncTheme`, `showDialog`, `scrollTop`, `focusById`, `getRefreshToken`, `setRefreshToken`, `clearRefreshToken`, `runExclusiveRefresh`.
- IJSRuntime calls: `ThemeService.cs:27,30,43`; `AdminSession.cs:169,240,253,267`; `Form.razor:724,757,801,803`; `ConfirmDialog.razor:44`. No file-related interop.
- No `InputFile` component anywhere in the admin.
- Framework facts (Blazor .NET 8, not verified by a run): `InputFile` gives `IBrowserFile`; `OpenReadStream(maxAllowedSize)` defaults to 512,000 bytes and throws beyond it unless a larger limit is passed; in WebAssembly the stream is pulled from the browser through JS interop, so reading a large video in chunks of a multiple of 256 KB is possible from .NET. `HttpClient` in WebAssembly runs on `fetch`, which exposes no upload progress events; per-chunk progress can be computed from .NET by sending each `Content-Range` chunk as a separate request (308 continue, 200/201 done), whereas byte-level progress inside a chunk would need `XMLHttpRequest` in a small JS interop file. Whether a JS file is needed is a design decision.

### I17. CSP

`nginx/conf.d/mentora-admin.conf.example:81`, full line:

```
add_header Content-Security-Policy "default-src 'self'; script-src 'self' 'wasm-unsafe-eval' 'sha256-oW+3IccTLsL4xJ3s2rKPxyyczlz2dfeyKhTyG6r3Zkc='; style-src 'self' https://fonts.googleapis.com; font-src 'self' https://fonts.gstatic.com; img-src 'self' data:; connect-src 'self' https://api.mentorapp.fr; frame-ancestors 'none'; base-uri 'self'; form-action 'self'; object-src 'none'" always;
```

- Comment at :23: "MUX (12.3): its upload host will be added to connect-src then." Hash procedure at :14-22 (recompute from the published index.html with the PowerShell one-liner at :19).
- The `sha256-oW+3...` value covers the single inline theme bootstrap script of `Mentora.Admin/wwwroot/index.html:14-28`.
- `.gitattributes:1-2`: `Mentora.Admin/wwwroot/index.html text eol=lf` (the hash is computed on LF endings).
- Security headers only at server level, never inside a location (:10-12).
- Directives a video block would touch (not changed):
  - `connect-src`: today `'self' https://api.mentorapp.fr`; the browser PUT to the upload URL needs the MUX upload host (`https://*.mux.com` per section 3; `https://storage.googleapis.com` only if older-style URLs can still be returned).
  - `img-src`: today `'self' data:`; a thumbnail needs `https://image.mux.com`.
  - `media-src`: not declared, so it falls back to `default-src 'self'`; a `<video>` preview from `https://stream.mux.com` would need it, and HLS in Chrome needs a player script (section 3).
  - `worker-src`: not declared, falls back to `script-src`/`default-src`; only relevant if a player uses workers or blob workers.
  - `script-src`: any player script loaded from a CDN would need its host, or the file shipped locally.

### I18. List badges and "À compléter"

- "À compléter" exists only in the form: `Todo(fieldId)` (`Form.razor:531-532`) for the six required fields of `Required` (:499-507): name, category, minimum level, primary muscle group, primary equipment, short instruction. Video is not one of them.
- In the list, the warning badge is "À reprendre" for `HasLegacyValues` (`List.razor:172-175`); the "Vidéo" column shows "Avec vidéo" / "Sans vidéo" (:153-162). "No video" does not count as incomplete anywhere today.

---

## Local database (section 6)

```
BEGIN
 exercise_videos_rows
----------------------
                    0
(1 row)

                         conname                         |                                                                   pg_get_constraintdef
---------------------------------------------------------+-----------------------------------------------------------------------------------------------------------------------------------------------------------
 CK_EXERCISE_VIDEOS_POLICY                               | CHECK ((("EXERCISE_VIDEO_PLAYBACK_POLICY")::text = ANY ((ARRAY['PUBLIC'::character varying, 'SIGNED'::character varying])::text[])))
 CK_EXERCISE_VIDEOS_STATUS                               | CHECK ((("EXERCISE_VIDEO_STATUS")::text = ANY ((ARRAY['WAITING'::character varying, 'READY'::character varying, 'ERRORED'::character varying])::text[])))
 FK_EXERCISE_VIDEOS_EXERCISES_EXERCISE_VIDEO_EXERCISE_ID | FOREIGN KEY ("EXERCISE_VIDEO_EXERCISE_ID") REFERENCES "EXERCISES"("EXERCISE_ID") ON DELETE CASCADE
 PK_EXERCISE_VIDEOS                                      | PRIMARY KEY ("EXERCISE_VIDEO_ID")
(4 rows)

          indexname           |                                                          indexdef
------------------------------+----------------------------------------------------------------------------------------------------------------------------
 PK_EXERCISE_VIDEOS           | CREATE UNIQUE INDEX "PK_EXERCISE_VIDEOS" ON public."EXERCISE_VIDEOS" USING btree ("EXERCISE_VIDEO_ID")
 UX_EXERCISE_VIDEOS_EXERCISE  | CREATE UNIQUE INDEX "UX_EXERCISE_VIDEOS_EXERCISE" ON public."EXERCISE_VIDEOS" USING btree ("EXERCISE_VIDEO_EXERCISE_ID")
 UX_EXERCISE_VIDEOS_MUX_ASSET | CREATE UNIQUE INDEX "UX_EXERCISE_VIDEOS_MUX_ASSET" ON public."EXERCISE_VIDEOS" USING btree ("EXERCISE_VIDEO_MUX_ASSET_ID")
(3 rows)

              MigrationId
---------------------------------------
 20261109160428_Lot12_2b_AdminAuditLog
(1 row)

ROLLBACK
```

The live schema matches the configuration and the Lot9 migration exactly. Empty table: a nullability change needs no data backfill locally.

---

## Deployment side (repo files only)

### I19. Runbook and nginx

- `docs/review/lot12/12.5a/prod-runbook.md`:
  - secrets generated into `.env`: section "4. Base, rôle et secrets PROD" (:61-92), loop at :87 and presence check at :89; :92 lists the variables shared by `api` and `api-prod`.
  - `api-prod` environment: section "5. Ajouter le service `api-prod` à `docker-compose.yml`" (:94-129), `environment:` block at :107-117 (last line `LiveKit__ApiSecret` at :117). Future `Mux__TokenId`, `Mux__TokenSecret`, `Mux__WebhookSecret` would go there.
  - `api` (api-demo) environment: section "Pour api-demo", step B (:253-264), lines added "sous la dernière ligne `LiveKit__...`" (:259-262).
  - Startup refusal summary at :131 (would need the MUX rules if they become startup checks).
- `nginx/conf.d/mentora-prod.conf.example` (api.mentorapp.fr): `client_max_body_size 25M` (:36); catch-all `location /` proxies to `http://api-prod:8080` with Host, X-Real-IP, X-Forwarded-For, X-Forwarded-Proto headers (:70-80). No `proxy_read_timeout` on `/` (nginx default 60 s); `proxy_buffering off` and 3600 s timeouts only on `/hubs/` (:53-67). No body rewriting, no `proxy_request_buffering off`, no gzip of requests: a webhook POST body reaches the API unchanged (request buffering only stores it).
- `nginx/conf.d/mentora.conf` (api-demo): same structure, `client_max_body_size 25M` (:40), `location /` to `http://api:8080` (:75-85).
- `nginx/nginx.conf`: no global body size or timeout override (:10-16).
- The admin site (`mentora-admin.conf.example`) never proxies to the API; the video file goes browser → MUX directly, so the 25M nginx limit does not apply to the upload itself.

### I20. CLAUDE.md rules for this lot

- Migrations: create with `dotnet ef migrations add ... --project Mentora.Infrastructure --startup-project Mentora.API` (:27-28); they run at startup via `Migrate()` (:34).
- Layers: Core = entities only, no dependency (:40); Infrastructure = DbContext + migrations (:41); API = controllers (not Minimal APIs), middlewares, policies, DI, Swagger (:42).
- Entity naming: properties prefixed with the entity name (:53).
- Routes `/api/v1/<resource>` (:67); response shape `{ success, data, error, statusCode }` (:68).
- No repository layer, services use `MentoraDbContext` directly (:72-76).
- Exception mapping (:91-99): NotFound 404, Unauthorized 401, Forbidden 403, Conflict 409 + details, FluentValidation 422 + errors, InvalidOperation 400, other 500.
- Startup seeders list (:104-111).
- Secrets/options precedent: email settings are environment variables only, "never in an appsettings file, never a value in a doc" (:115); the API refuses to start, read before migrations, on invalid settings (:117-119).
- CLAUDE.md has no explicit rule on enums, webhooks or HTTP clients. The enum convention is visible in code (if-chain mappings, "if chains, no switch", `AdminAuditAction.cs:18`; `EnumMappings`).
- User memory (not CLAUDE.md): never print appsettings secrets; generated files via the Write tool, never Set-Content/Out-File.

---

## Facts for the 12.3a design

- `EXERCISE_VIDEO_MUX_ASSET_ID` is NOT NULL and there is no upload id column (`ExerciseVideoConfiguration.cs:24-27`, Lot9 migration :157); one row per exercise (`ExerciseVideoConfiguration.cs:73-75`).
- Status CHECK allows only WAITING/READY/ERRORED, varchar(10) (Lot9 migration :335-341, `ExerciseVideoConfiguration.cs:48-53`); a new state means enum + two converters (`ExerciseVideoConfiguration.cs:101-115`, `EnumMappings.cs:441-461`) + CHECK drop/re-create with Down.
- PostgreSQL unique indexes treat NULLs as distinct; repo precedents for partial unique indexes exist (Lot9 migration :351-356).
- Apps receive `video { playbackId, status, durationSeconds, aspectRatio }` unfiltered on coach list/detail and member detail (`ExerciseService.cs:445-451`, `ExerciseResponse.cs:91-95`); member list has no video (`ExerciseService.cs:229-234`); `videoUrl` and `exerciseVideoUrl` are separate legacy fields (`ExerciseService.cs:414`, `ProgramService.cs:1007`).
- Admin `HasVideo` is true for any row whatever its status (`AdminExerciseService.cs:174,217`).
- `ADMIN_AUDIT_LOG` admin id is NOT NULL with a Restrict FK; the action CHECK lists four values (`AdminAuditLogConfiguration.cs:24,52-56`, Lot12_2b migration :55-56).
- `ValidateOnStart` alone fires after migrations; options that must stop the API before migrations read `.Value` explicitly (`Program.cs:462-497` versus :500-517, :563).
- No `"Mux"` section in `appsettings.json` (I7); environment variables for `api-prod` live in the runbook at `prod-runbook.md:107-117`.
- No outbound HTTP client exists; `AddHttpClient` is available through `Microsoft.AspNetCore.App` (`Mentora.Infrastructure.csproj:8`).
- No fallback authorization policy: an action without `[Authorize]` is public (`Program.cs:235-253`); `AdminBearer` must be named explicitly (`Program.cs:238-241`).
- Raw-body webhook precedent with idempotence by event id (`InternalStripeWebhookController.cs:39`, `StripeWebhookHandler.cs:19-43`); no signature check precedent.
- Any exception in a webhook action becomes a 500 envelope through `GlobalExceptionMiddleware.cs:62-66`, which triggers MUX retries.
- No route collision for the three planned routes (I11).
- CORS origins: `CorsOptions.OriginList` (`CorsOptions.cs:19-35`), PROD single origin (`prod-runbook.md:113`), Development extra list (`Program.cs:421-430`).
- Admin front: one `HttpClient` bound to the API base address, token passed per call (`Program.cs:17-20`, `ApiTransport.cs:25-26`); no file input, no upload interop (I16).
- CSP `connect-src` and `img-src` must change for upload and thumbnail; hash procedure and LF rule must be kept (`mentora-admin.conf.example:14-23,81`, `.gitattributes:2`).
- Video block slot: between `Form.razor:157` and `:159`; dirty snapshot excludes video (`Form.razor:546-554`).
- Exercise name max 120 (below meta.title 512); code max 10 (below external_id 128) (`ExerciseConfiguration.cs:24-27,85`).

## Open points

- Can a Mentora exercise reach the video block with a NULL `EXERCISE_SOURCE_CODE` (`LegacyExerciseCodePatch.cs:8-13` only fills matching names)? What goes into `meta.external_id` then?
- Should the apps keep receiving WAITING and ERRORED rows, or only READY ones, and is `playbackId` guaranteed non-null when READY?
- Is the upload-waiting state a new status value (CHECK change) or WAITING with a NULL asset id?
- Replacement of an existing video: update the single row, or delete then insert, and what happens to the old MUX asset?
- Webhook audit: nullable admin id, a technical admin row, or no audit row for webhook-driven changes?
- MUX archive: new table, or generalise `STRIPE_WEBHOOK_EVENTS`? Keep "always 200" or answer non 2xx on processing failure?
- Webhook rate limiting: none, or a new policy for `/api/v1/webhooks/mux`?
- Swagger document for `POST /api/v1/webhooks/mux`: `internal` (default) or another?
- Source of `cors_origin`: request `Origin` header, the single configured origin, or a new option? What about api-demo, whose runbook sets no `Cors__AllowedOrigins`, and the local admin origin from `launchSettings.json` (not in the Development list)?
- Should the MUX options fail before migrations (explicit `.Value`) and should `StartupGuards` refuse missing MUX secrets outside Development?
- Does the video block take part in the dirty state and the "Quitter sans enregistrer ?" dialog while an upload is in progress?
- Should "Sans vidéo" become an "À compléter" item or a list filter?
- Is `storage.googleapis.com` still needed in `connect-src`, or only `*.mux.com`?
- Will the admin preview play video (player script, `media-src`, `worker-src`) or show a thumbnail only?
- Is the legacy `EXERCISE_VIDEO_URL` (coach-written, `ExerciseService.cs:92,123`) kept as is alongside MUX?
- Kestrel request size settings in appsettings were not inspected (rule); confirm none are set.

## Surprises

- CLAUDE.md cites `MemberOnly` at `Program.cs:185-187` (CLAUDE.md:88); it is now at `Program.cs:246-248`.
- CLAUDE.md seeder list (:104-111) omits `ExerciseReferenceSeeder`, `LegacyExerciseCodePatch`, `ReferenceNormalizationPatch` and `AdminUserSeeder` (`Program.cs:509-516`).
- The passation reference `ExerciseService.cs:505-511` is outdated; the video mapping is at `:445-451`.
- `VideoPlaybackPolicyMapping` has no caller at all (`EnumMappings.cs:463-481`).
- `ExerciseVideos` DbSet is never used and nothing writes `EXERCISE_VIDEOS`; every app response today has `video: null`.
- `AdminAuthOptionsValidator` lives in `Mentora.Infrastructure/Services/Email/EmailOptionsValidators.cs:27`, an email-named file.
- LiveKit and Quiz options validate only at `app.Run()`, after migrations and seeders, unlike Email, Routing, Cors and AdminAuth.
- The Stripe webhook is anonymous by absence of attribute (no `[AllowAnonymous]`), possible only because there is no fallback policy.
- No test project exists in the solution.
- Nothing in the repo contradicts the MUX facts of section 3 of the brief.
