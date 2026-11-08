# Lot 12, sub-lot 12.0: read-only inventory

Branch `feature/lot_12`, HEAD `2d1f843`. Read-only pass: no file changed except this one, no commit, no API start, no migration applied, no SQL run (see 5.3). All paths are repo-relative. Configuration is described by key names only.

Abbreviations used below:
- `P` = `Mentora.API/Program.cs`
- `Cfg/` = `Mentora.Infrastructure/Persistence/Configurations/`
- `Mig/` = `Mentora.Infrastructure/Persistence/Migrations/`
- `M6` = `Mig/20260811115228_Lot6_1_Exercises.cs`, `M9` = `Mig/20260929171450_Lot9_ExerciseCatalog.cs`
- `CS` = `Mentora.Infrastructure/Seeding/CatalogSeeder.cs`
- `ES` = `Mentora.Infrastructure/Services/ExerciseService.cs`
- `AS` = `Mentora.Infrastructure/Services/AuthService.cs`

---

## Preflight

| Check | Output |
|---|---|
| `git --no-optional-locks status` | `On branch feature/lot_12` / `nothing to commit, working tree clean` |
| `git --no-optional-locks log --oneline -1` | `2d1f843 docs(lot11): 11.7 inventory and runtime evidence` |
| `dotnet --list-sdks` | `8.0.200 [C:\Program Files\dotnet\sdk]` (only SDK; no `global.json` in the repo) |
| `dotnet build` | `La génération a réussi.` `0 Avertissement(s)` `0 Erreur(s)`, 14.07 s. Three projects built, all `net8.0` (`Mentora.Core`, `Mentora.Infrastructure`, `Mentora.API`, each `bin\Debug\net8.0\*.dll`). Note: the build was incremental, so warnings from unchanged files may not be re-emitted. |

---

## 4.1 Authentication

### 4.1.1 AuthService (`AS`)

Dependencies (`AS:20-25`): `MentoraDbContext`, `IOptions<JwtSettings>`, `IEmailSender`, `IValidator<LogoutRequest>`, `ILogger<AuthService>`. Registered scoped at `P:198`.

| Method | Behaviour | Ref |
|---|---|---|
| `RequestOtpAsync(email, isCoach?)` | Loads the user (with Coach, Member) where `UserEmail == email && UserIsEnabled`; none → `InvalidOperationException`. Calls `ResolveEffectiveIsCoach`. Marks every unused, unexpired OTP of the user as used. Inserts a new `AuthOtp` (BCrypt hash, expiry now + 10 min), `SaveChanges`. Writes the console OTP log line. Sends the OTP mail via `IEmailSender`; the send result is discarded. | `AS:32-76`; `OtpLifetimeMinutes = 10` at `AS:28`; console OTP log line at `AS:61` |
| `VerifyOtpAsync(email, code, isCoach?)` | Same user lookup. Loads unused, unexpired OTPs newest first, picks the first where `BCrypt.Verify` succeeds, else `InvalidOperationException("Invalid or expired OTP.")`. Resolves role, marks OTP used, creates refresh token for that role, returns `AuthResponse(AccessToken, RefreshToken, ExpiresIn, IsCoach)`. Access token carries only `coachId` or only `memberId`. | `AS:92-125` |
| `ResolveEffectiveIsCoach` | `isCoach` given → must match an existing profile, else `InvalidOperationException`. Absent + both profiles → logs error, throws `ConflictException` (409). No profile → `InvalidOperationException`. | `AS:134-161` |
| `RefreshTokenAsync(token)` | Parses `"{guid}:{secret}"`, loads non-revoked unexpired record by id (incl. User, Member, Coach), else `UnauthorizedException`. `BCrypt.Verify` secret, else `UnauthorizedException`. Revokes old (IsRevoked + RevokedDate) and rotates. Role from `AuthRefreshTokenUserRole`, never re-derived. Does **not** check `UserIsEnabled`. | `AS:163-209` (lookup `AS:167-174`) |
| `LogoutAsync(userId, req, ct)` | `ValidateAndThrowAsync` (422). Revokes only when token parses, belongs to `userId`, is live and secret verifies; otherwise silent. If `DeviceToken` set, `ExecuteDeleteAsync` on `UserDevices` (token + userId). | `AS:211-240` |
| `GenerateAccessToken(user, memberId?, coachId?)` | Claims `sub`, `email`, `userType` (`BOTH`/`COACH`/`MEMBER`/`UNKNOWN`), `jti`, optional `memberId`, `coachId`. HMAC-SHA256, `SymmetricSecurityKey(UTF8(Secret))`, issuer/audience from `JwtSettings`, expires now + `AccessTokenExpirationMinutes`. | `AS:257-290` |
| `CreateRefreshTokenAsync(userId, role)` | 32-byte `RandomNumberGenerator` secret, base64 (`AS:340-345`), stored as BCrypt hash; expiry now + `RefreshTokenExpirationDays`; `SaveChangesAsync` immediately to get the DB Guid; returns `"{id}:{secret}"`. | `AS:293-310` |
| Token parsing | `TryParseClientToken` splits on first `:`, `Guid.TryParse` prefix (`AS:319-330`). `ParseClientToken` throws `InvalidOperationException` → 400 on malformed token (`AS:312-317`). | |
| OTP generation | 4 random bytes, `Math.Abs(BitConverter.ToInt32(bytes)) % 900_000 + 100_000` → 6 digits. | `AS:332-338` (`AS:336`) |
| OTP hashing | `BCrypt.Net.BCrypt.HashPassword`, default work factor. | `AS:53`, verify `AS:106` |
| OTP attempts | No attempt counter, no lockout; `AuthOtp` has no attempts column (`Mentora.Core/Entities/AuthOtp.cs:3-13`). | |

### 4.1.2 Controllers

| Controller | Class attributes | Action | Action attrs | try/catch | userId source |
|---|---|---|---|---|---|
| `Mentora.API/Controllers/Auth/AuthController.cs` | `[ApiController]`, `[Route("api/v1/auth")]`, `GroupName="mobile"`, `[Tags("Auth")]` (`:9-12`); no class `[Authorize]` | `POST otp/request` (`:15`) | anonymous | catches `InvalidOperationException` → 400 (`:18-26`) | – |
| | | `POST otp/verify` (`:29`) | anonymous | same (`:32-40`) | – |
| | | `POST token/refresh` (`:43`) | `ProducesResponseType` 200/401 | none (middleware) | – |
| | | `POST logout` (`:63`) | `[Authorize]` no policy (`:64`) | none | `Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value)` (`:67`) |
| `Mentora.API/Controllers/Auth/AccountController.cs` | `[Route("api/v1/account")]`, `[Authorize]` no policy, `GroupName="mobile"`, `[Tags("Account")]` (`:15-19`) | `POST/DELETE/GET deletion-request` (`:26,35,47`) | – | none | `ClaimTypes.NameIdentifier` (`:29,38,50`) |
| `Mentora.API/Controllers/Auth/UserDevicesController.cs` | `[Route("api/v1/devices")]`, `[Authorize]` no policy, `GroupName="mobile"`, `[Tags("Devices")]` (`:10-14`) | `POST` (`:24`), `DELETE` with body (`:37`) | – | none | `ClaimTypes.NameIdentifier` (`:27,40`) |

- `ConflictException` from `ResolveEffectiveIsCoach` is not caught inline → middleware 409.
- `NameIdentifier` works only because `MapInboundClaims = true` (`P:144-149`).
- Other plain `[Authorize]`: `Mentora.API/Hubs/ChatHub.cs:16` (reads `memberId`/`coachId`, `ChatHub.cs:96,102`). Coach controllers read `User.FindFirst("coachId")` (e.g. `Mentora.API/Controllers/Coach/CoachMeController.cs:28`).

### 4.1.3 Entities and EF configuration

Configurations applied by `ApplyConfigurationsFromAssembly` (`Mentora.Infrastructure/Persistence/MentoraDbContext.cs:75`). Snapshot: `Mig/MentoraDbContextModelSnapshot.cs`. No code creates a `User` row (no `new User` / `Users.Add` outside SQL fixtures under `docs/review/**`).

**USERS** (`Mentora.Core/Entities/User.cs:3-22`, `Cfg/UserConfiguration.cs:9-49`, snapshot `:2550-2610`)

| Column | Type | Constraints |
|---|---|---|
| USER_ID | uuid | PK, `gen_random_uuid()` (`:13-16`) |
| USER_CREATED_DATE | timestamptz | required |
| USER_MODIFICATION_DATE | timestamptz | nullable |
| USER_EMAIL | varchar(255) | required, unique btree index (`:20-21`, snapshot `:2606`) |
| USER_PSW | varchar(255) | nullable |
| USER_LOGIN | varchar(100) | nullable |
| USER_IS_ENABLED | boolean | required (`:24`) |
| USER_DISABLED_DATE | timestamptz | nullable |
| USER_ROLE | varchar(50) | required, plain `string`, no converter (`User.cs:13`, cfg `:26`) |
| USER_DELETION_REQUESTED_DATE | timestamptz | nullable |
| USER_DELETION_REASON | text | nullable |

Relationships, all Cascade: Coach 1:0..1 (`:30-33`), Member 1:0..1 (`:35-38`), AuthOtps 1:N (`:40-43`), AuthRefreshTokens 1:N (`:45-48`), UserDevices 1:N (device side).

**AUTH_OTPS** (`Mentora.Core/Entities/AuthOtp.cs`, `Cfg/AuthOtpConfiguration.cs:9-23`, snapshot `:64-99`, FK `:2665-2674`): AUTH_OTP_ID uuid PK `gen_random_uuid()`; AUTH_OTP_CODE_HASH varchar(255) req; AUTH_OTP_EXPIRATION_DATE timestamptz req; AUTH_OTP_IS_USED boolean req; AUTH_OTP_CREATED_DATE timestamptz req; USER_ID uuid FK USERS Cascade, conventional index (snapshot `:96`).

**AUTH_REFRESH_TOKENS** (`Mentora.Core/Entities/AuthRefreshToken.cs`, `Cfg/AuthRefreshTokenConfiguration.cs:11-53`, snapshot `:101-146`, FK `:2676-2685`): AUTH_REFRESH_TOKEN_ID uuid PK; _HASH varchar(255) req; _EXPIRATION_DATE timestamptz req; _IS_REVOKED boolean req; _USER_ROLE varchar(10) req (UserRole via inline converter `:25-34`); _CREATED_DATE timestamptz req; _REVOKED_DATE timestamptz null; USER_ID uuid FK USERS Cascade, conventional index (snapshot `:143`). No index on expiry or revoked date.

**USER_DEVICES** (`Mentora.Core/Entities/UserDevice.cs:11-21`, `Cfg/UserDeviceConfiguration.cs:11-55`, snapshot `:2612-2652`, FK `:3227-3236`): USER_DEVICE_ID uuid PK; USER_DEVICE_TOKEN varchar(512) req, unique (`:25`); USER_DEVICE_PLATFORM varchar(10) (`DevicePlatform` converter IOS/ANDROID, `:28-37,57-69`); _CREATED_DATE, _LAST_SEEN_DATE timestamptz req; USER_ID FK USERS Cascade (`:51-54`).

Migrations: auth tables `Mig/20260326120304_AddAuthTables.cs`; devices in `Mig/20260730194501_AddLot5Parameters.cs` (by naming, not opened).

### 4.1.4 UserRole

- `Mentora.Core/Enums/UserRole.cs:9-13`: `Member`, `Coach` (doc comment `:4`: VARCHAR(10) UPPERCASE).
- Converter: private inline `ValueConverter<UserRole,string>` in `Cfg/AuthRefreshTokenConfiguration.cs:25-28` → `"MEMBER"`/`"COACH"`; unknown value throws `ArgumentOutOfRangeException` both ways (`:41-53`).
- **No `UserRole` entry in `Mentora.Core/Enums/EnumMappings.cs`** (`git grep UserRole` on that file: no hit).
- Used only by `AuthRefreshToken.AuthRefreshTokenUserRole` and `AS:110,189,207,302`. `User.UserRole` is an unrelated free string.

### 4.1.5 Reads of USER_IS_ENABLED

| Location | Use |
|---|---|
| `AS:38` | `RequestOtpAsync` filter |
| `AS:97` | `VerifyOtpAsync` filter |

No other non-migration read. Not checked by `RefreshTokenAsync` (`AS:167-174`) nor by the JWT pipeline. Nothing in the API writes it.

### 4.1.6 JWT and authorization

- `JwtSettings` (`Mentora.Core/Settings/JwtSettings.cs:3-10`): `Secret`, `Issuer`, `Audience`, `AccessTokenExpirationMinutes`, `RefreshTokenExpirationDays`; section `Jwt`.
- `Configure<JwtSettings>` (`P:137`) + eager `GetSection("Jwt").Get<JwtSettings>()!` (`P:139`). No validation, no `ValidateOnStart`.
- `AddAuthentication(JwtBearerDefaults.AuthenticationScheme)` (`P:141`): single default scheme `Bearer`; no policy scheme, no `ForwardDefaultSelector`, no second scheme.
- `MapInboundClaims = true` (`P:149`), pinned on purpose (comment `P:144-148`).
- `TokenValidationParameters` (`P:151-161`): ValidateIssuer/Audience/Lifetime/IssuerSigningKey all true; `ClockSkew = TimeSpan.Zero` (`P:160`).
- `JwtBearerEvents`: only `OnMessageReceived` (query `access_token` for `/hubs/chat`, `P:165-177`). No `OnChallenge`, no `OnForbidden`.
- Policies (`P:181-193`): `CoachOnly` = `RequireClaim("userType","COACH","BOTH")` (`P:183-184`); `MemberOnly` = `RequireClaim("memberId").RequireClaim("userType","MEMBER")` (`P:186-188`); `QuizPlayer` = `QuizPlayerRequirement` (`P:191-192`). No DefaultPolicy/FallbackPolicy override.
- `QuizPlayerHandler` singleton (`P:195`), `Mentora.API/Authorization/QuizPlayerRequirement.cs:16-43`: no `Authorization` header → succeed (`:26-30`); header but unauthenticated → not satisfied (`:32-33`); otherwise evaluates `CoachOnly` via a per-request `IAuthorizationService` (`:38-41`). Policy name constant `:13`.

### 4.1.7 RefreshTokenPurgeService

- `Mentora.Core/Options/RefreshTokenPurgeOptions.cs:3-21`: section `RefreshTokenPurge`, `CountOnly` (default true), `InitialDelayMinutes` (default 15).
- Registration: plain `Configure<>` without validation (`P:299-300`), `AddHostedService<RefreshTokenPurgeService>()` (`P:301`).
- `Mentora.Infrastructure/Services/RefreshTokenPurgeService.cs`: `RetentionDays = 30`, `LoggedIdCap = 50`, `Interval = 1 day` (`:27-30`); initial delay (`:36`); loop (`:43-55`); eligible = expired ≤ cutoff or revoked with RevokedDate ≤ cutoff (`:65-67`); count-only logs (`:71-78`); live mode `ExecuteDeleteAsync` by id (`:81-95`). `RunOnceAsync` has no try/catch (`:58-96`).
- No purge exists for AUTH_OTPS.

### 4.1.8 Options validated at startup (patterns to copy)

| Options | Section | Pattern | Ref |
|---|---|---|---|
| `LiveKitOptions` (`Mentora.Core/Options/LiveKitOptions.cs:3-11`) | `LiveKit` | `AddOptions<T>().Bind(GetSection(T.SectionName)).Validate(lambda, "msg")` × n, then `.ValidateOnStart()` | `P:267-275` |
| `QuizOptions` (`Mentora.Core/Options/QuizOptions.cs:4-17`) | `Quiz` | same inline `.Validate(...)` chain + `.ValidateOnStart()` | `P:279-285` |
| `EmailOptions` (`Mentora.Core/Options/EmailOptions.cs:5-61`, own `string? Validate()` `:28-45`) | `Email` | `AddOptions<T>().Bind(...).ValidateOnStart()` + `AddSingleton<IValidateOptions<EmailOptions>, EmailOptionsValidator>()` | `P:305-308` |
| `EmailRoutingOptions` (`Mentora.Core/Options/EmailRoutingOptions.cs:10-62`, `Validate()` `:31-44`) | `EmailRouting` | same with `EmailRoutingOptionsValidator` | `P:309-312` |

- Validators: `Mentora.Infrastructure/Services/Email/EmailOptionsValidators.cs:7-24`; each calls `options.Validate()` → `ValidateOptionsResult.Success` / `Fail(error)`; messages never contain a value.
- **Eager read before migrations:** after `builder.Build()`, a scope reads `IOptions<EmailOptions>.Value` and `IOptions<EmailRoutingOptions>.Value` (fires validators) and logs a counts-only line (`P:378-390`), before `MigrateAsync` (`P:393-396`). LiveKit/Quiz rely only on `ValidateOnStart`, which fires in `app.Run()`, i.e. after migrations and seeders.
- `ValidateDataAnnotations` is not used.

### 4.1.9 Rate limiting (`Mentora.API/Authorization/QuizRateLimiting.cs`)

- Registration `AddRateLimiter(o => o.AddQuizPolicy())` (`P:288`); `app.UseRateLimiter()` at `P:427`, after `UseAuthentication` (`P:425`) and `UseAuthorization` (`P:426`).
- Policy name `"QuizBasic"` (`:11`); constant `DeviceIdHeader = "X-Device-Id"` (`:12`).
- Partition: request with `Authorization` header → `GetNoLimiter("authenticated")` (`:25-26`); otherwise fixed window keyed on `Connection.RemoteIpAddress` (or `"unknown"`), `AnonymousPermitLimit` per `AnonymousWindowMinutes`, `QueueLimit = 0` (`:29-37`).
- Real client IP: relies on `UseForwardedHeaders()` first (`P:409`) with `XForwardedFor | XForwardedProto`, `KnownNetworks.Clear()`, `KnownProxies.Clear()` (`P:369-374`). Runtime confirmation behind nginx: `docs/review/lot10/10.3/vps-evidence.md:39`.
- Rejection: 429 (`:21`); `OnRejected` sets `Retry-After` (ceil seconds) when lease metadata exists (`:43-44`); body `{ success:false, data:null, error:"Too many requests.", statusCode:429 }` (`:46-53`). Limiter options are global, so a new policy shares `OnRejected`.
- Applied only on `Mentora.API/Controllers/Quiz/QuizBasicController.cs:18`. No auth endpoint is rate-limited.

---

## 4.2 Swagger, CORS, middleware

### 4.2.1 Swagger (Swashbuckle.AspNetCore 6.5.0, `Mentora.API/Mentora.API.csproj:20`)

- `AddEndpointsApiExplorer()` `P:31`; `AddSwaggerGen` `P:32-127`; XML docs `Mentora.API.xml` + `Mentora.Core.xml` (`P:106-116`); `DescribeAllParametersInCamelCase()` `P:118`; `CustomSchemaIds(t => t.FullName)` `P:119`.

| Doc | Title | Ref |
|---|---|---|
| `mobile` | Mentora API — Mobile | `P:34-39` |
| `coach` | Mentora API — Coach Back-Office | `P:40-45` |
| `internal` | Mentora API — Internal | `P:46-51` |

- `DocInclusionPredicate` (`P:53-58`): `var groupName = apiDesc.GroupName ?? "internal"; return groupName == docName;`. No GroupName → `internal` only. Unknown GroupName (e.g. `"admin"` today) → in no doc.
- `TagActionsBy` (`P:61-84`): endpoint `ITagsMetadata` → controller `ITagsMetadata` → controller name → `"Other"`.
- Security: one `Http`/`bearer`/`JWT` definition, id `Bearer` (`P:86-100`); **global** `AddSecurityRequirement` (`P:101-104`), applies to every operation of every doc.
- Operation filters: `VoucherStatusOperationFilter` (`P:122`), `ExerciseListOperationFilter` (`P:123`), `DeprecatedCoachProgramsRouteOperationFilter` (`P:126`). No schema or document filters.
- `UseSwagger()` `P:414`; `UseSwaggerUI` endpoints `/swagger/mobile|coach|internal/swagger.json` (`P:417-419`), `RoutePrefix = "swagger"` (`P:420`). No environment check: served in every environment.
- GroupName: all 45 controllers set it at class level with `[ApiExplorerSettings(GroupName=...)]`: 20 `mobile`, 24 `coach`, 1 `internal` (`Mentora.API/Controllers/Internal/InternalStripeWebhookController.cs:17`). None lacks one. Minimal API `/api/v1/health` → `.WithGroupName("internal")` (`P:442`). `QuizBasicController` (QuizPlayer, `Mentora.API/Controllers/Quiz/QuizBasicController.cs:17-19`) is in `coach`.

**Adding a 4th doc "admin" requires:**
1. `c.SwaggerDoc("admin", ...)` next to `P:34-51`.
2. `c.SwaggerEndpoint("/swagger/admin/swagger.json", ...)` in `P:417-419`.
3. `[ApiExplorerSettings(GroupName = "admin")]` on each admin controller (else it falls into `internal`, `P:56`).
4. No change to `DocInclusionPredicate` (`P:54-58`).
5. If the admin JWT is a distinct scheme, the single definition and global requirement (`P:86-104`) need rework to show a different scheme per doc.

### 4.2.2 CORS

- One policy `"MentoraCorsPolicy"` (`P:350-367`). Origins are a hard-coded literal list (`P:354-362`), not configuration: `http://localhost:4200`, `http://localhost:8100`, `http://localhost`, `https://localhost`, `https://localhost:4200`, `https://localhost:8100`, `capacitor://localhost`. `AllowAnyHeader` (`P:363`), `AllowAnyMethod` (`P:364`), `AllowCredentials` (`P:365`).
- Applied globally `app.UseCors("MentoraCorsPolicy")` (`P:424`); no `[EnableCors]`/`[DisableCors]` anywhere. nginx adds no `Access-Control-*` header.

### 4.2.3 Pipeline order (`P`)

Before pipeline: email options eager read `P:378-390`, then migrations + seeders `P:392-405`.

| # | Call | Line |
|---|---|---|
| 1 | `UseForwardedHeaders()` (options `P:369-374`) | `P:409` |
| 2 | `UseMiddleware<GlobalExceptionMiddleware>()` | `P:412` |
| 3 | `UseSwagger()` | `P:414` |
| 4 | `UseSwaggerUI(...)` | `P:415-422` |
| 5 | `UseCors("MentoraCorsPolicy")` | `P:424` |
| 6 | `UseAuthentication()` | `P:425` |
| 7 | `UseAuthorization()` | `P:426` |
| 8 | `UseRateLimiter()` | `P:427` |
| 9 | `MapGet("/api/v1/health")` anonymous | `P:430-443` |
| 10 | `MapControllers()` | `P:445` |
| 11 | `MapHub<ChatHub>("/hubs/chat")` | `P:448` |
| 12 | `app.Run()` | `P:450` |

Not present: explicit `UseRouting`, `UseHttpsRedirection`, `UseHsts`, `UseStaticFiles`, `MapFallbackToFile`, `UseExceptionHandler`, `AddProblemDetails`, `MapHealthChecks`.

### 4.2.4 GlobalExceptionMiddleware (`Mentora.API/Middleware/GlobalExceptionMiddleware.cs`)

| Exception | Status | Line |
|---|---|---|
| `NotFoundException` | 404 | `:14-18` |
| `UnauthorizedException` | 401 | `:19-23` |
| `ForbiddenException` | 403 | `:24-28` |
| `ConflictException` | 409 + `details` | `:29-33` |
| FluentValidation `ValidationException` | 422 + `errors` (PropertyName → messages), `error = "Validation failed."` | `:34-56` |
| `InvalidOperationException` | 400 | `:57-61` |
| other | 500 `"An unexpected error occurred."` | `:62-66` |

- Envelope: anonymous object `{ success, data, error, statusCode }` (+ `details`/`errors`) written by `WriteResponseAsync` (`:69-95`). No `ApiResponse` type exists in any project. Controllers build success envelopes inline (e.g. `Mentora.API/Controllers/Coach/CoachAgendaController.cs:42`).
- 401/403 from the auth handler: framework default (no envelope body), because there is no `OnChallenge`/`OnForbidden` (`P:165-177`).

### 4.2.5 Filters, model state, JSON, validation

- `NullBodyGuardFilter` global (`P:328-336`; `Mentora.API/Middleware/NullBodyGuardFilter.cs:13-34`): null body → `InvalidOperationException("Request body is required.")` → 400.
- `SuppressModelStateInvalidFilter = true` (`P:337-340`); no `InvalidModelStateResponseFactory`.
- JSON: `JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper)` (`P:346-347`); property naming = default camelCase. SignalR same converter (`P:291-296`). No `ConfigureHttpJsonOptions` (middleware `WriteAsJsonAsync` uses default `HttpJsonOptions`).
- FluentValidation: `AddValidatorsFromAssemblyContaining<ProductRequestValidator>()` (`P:208`); no auto-validation, validators called explicitly.

---

## 4.3 Exercises

No trigram, GIN, `unaccent` or `tsvector` object in any migration. Search uses `translate()` + `LIKE` (`Mentora.Infrastructure/Persistence/MentoraDbContext.cs:55-59`, `Mentora.Infrastructure/Services/ExerciseSearch.cs:25-29`).

### 4.3.1 Tables

**EXERCISES** (`Mentora.Core/Entities/Exercise.cs:5-59`, `Cfg/ExerciseConfiguration.cs`)

| Column | Type | Null | Default | Ref |
|---|---|---|---|---|
| EXERCISE_ID | uuid PK | no | `gen_random_uuid()` | Cfg:15-18, M6:18 |
| EXERCISE_COACH_ID | uuid FK COACHES Restrict | yes (NULL = Mentora) | – | Cfg:21-22,149-152; M6:19,35-40 |
| EXERCISE_NAME | varchar(120) | no | – | Cfg:24-27 |
| EXERCISE_DESCRIPTION | text | yes | – | Cfg:29-31 |
| EXERCISE_INSTRUCTIONS | text | yes | – | Cfg:33-35 |
| EXERCISE_VIDEO_URL | varchar(500) | yes | – | Cfg:37-39 |
| EXERCISE_IMAGE_URL | varchar(500) | yes | – | Cfg:41-43 |
| EXERCISE_MUSCLE_GROUP (legacy enum, "deprecated since Lot 9", `Exercise.cs:19-20`) | varchar(40) | **no** | – (no CHECK) | Cfg:45-53 |
| EXERCISE_EQUIPMENT (legacy enum, `Exercise.cs:22-23`) | varchar(40) | **no** | – (no CHECK) | Cfg:55-63 |
| EXERCISE_IS_POLYARTICULAR | boolean | no | false | Cfg:65-68 |
| EXERCISE_IS_ACTIVE | boolean | no | true | Cfg:70-73 |
| EXERCISE_CREATED_DATE / _UPDATED_DATE | timestamptz | no | – | Cfg:75-81 |
| EXERCISE_SOURCE_CODE | varchar(10) | yes | – | Cfg:85, M9:111-116 |
| EXERCISE_NAME_EN | varchar(120) | yes | – | Cfg:86, M9:68 |
| EXERCISE_CATEGORY | varchar(60) | yes | – | Cfg:87, M9:21 |
| EXERCISE_MOVEMENT_PATTERN | varchar(60) | yes | – | Cfg:88, M9:61 |
| EXERCISE_TYPE | varchar(60) | yes | – | Cfg:89, M9:125 |
| EXERCISE_LATERALITY | varchar(40) | yes | – | Cfg:90, M9:47 |
| EXERCISE_STARTING_POSITION | varchar(80) | yes | – | Cfg:91, M9:118 |
| EXERCISE_GRIP_TYPE | varchar(60) | yes | – | Cfg:92, M9:40 |
| EXERCISE_PRIMARY_MUSCLE_GROUP_ID | uuid FK MUSCLE_GROUPS Restrict | yes | – | Cfg:94,121-124; M9:87,302-308 |
| EXERCISE_SECONDARY_MUSCLES (`List<string>`) | jsonb | yes | – | Cfg:95, M9:105 |
| EXERCISE_PRIMARY_EQUIPMENT_ID | uuid FK EQUIPMENT Restrict | yes | – | Cfg:96,126-129; M9:81,286-292 |
| EXERCISE_SECONDARY_EQUIPMENT_ID | uuid FK EQUIPMENT Restrict | yes | – | Cfg:97,131-134; M9:99,294-300 |
| EXERCISE_MIN_LEVEL | varchar(20) converter | yes | – | Cfg:99-106, M9:54 |
| EXERCISE_BREATHING | text | yes | – | Cfg:108, M9:15 |
| EXERCISE_COMMON_MISTAKES (`List<string>`) | jsonb | yes | – | Cfg:109, M9:34 |
| EXERCISE_PRECAUTIONS | text | yes | – | Cfg:110, M9:75 |
| EXERCISE_COACH_TIP | text | yes | – | Cfg:111, M9:28 |
| EXERCISE_SEARCH_TAGS | text | yes | – | Cfg:112, M9:93 |

Indexes and CHECKs on EXERCISES:
- `IX_EXERCISES_COACH_ACTIVE` (COACH_ID, IS_ACTIVE): Cfg:142-143, M6:43-46.
- `IX_EXERCISES_COACH_NAME`: raw-SQL UNIQUE (COACH_ID, NAME) NULLS NOT DISTINCT; exact, case-sensitive, inactive rows count (M6:48-56, comment Cfg:136-139).
- `IX_EXERCISES_MIN_LEVEL`, `IX_EXERCISES_PRIMARY_EQUIPMENT`, `IX_EXERCISES_PRIMARY_MUSCLE_GROUP`, `IX_EXERCISES_SECONDARY_EQUIPMENT`: Cfg:114-117, M9:231-249.
- `CK_EXERCISES_MIN_LEVEL` (NULL or BEGINNER/INTERMEDIATE/ADVANCED): raw SQL M9:343-349.
- `UX_EXERCISES_SOURCE_CODE` partial UNIQUE `WHERE EXERCISE_SOURCE_CODE IS NOT NULL`: raw SQL M9:351-356.
- M9 `Down()` drops raw-SQL objects at M9:367-375.

**EXERCISE_OBJECTIVES** (`Mentora.Core/Entities/ExerciseObjective.cs:4-11`, `Cfg/ExerciseObjectiveConfiguration.cs`): composite PK (EXERCISE_OBJECTIVE_EXERCISE_ID FK EXERCISES **Cascade**, Cfg:13-16,23-26; EXERCISE_OBJECTIVE_OBJECTIVE_ID FK OBJECTIVES **Restrict**, Cfg:18-19,28-31); M9:207-228. Index `IX_EXERCISE_OBJECTIVES_OBJECTIVE` (Cfg:33-34, M9:257-260). No CHECK.

**EXERCISE_VIDEOS** (`Mentora.Core/Entities/ExerciseVideo.cs:9-27`, `Cfg/ExerciseVideoConfiguration.cs`; created M9:151-175)

| Column | Type | Null | Default | Ref |
|---|---|---|---|---|
| EXERCISE_VIDEO_ID | uuid PK | no | `gen_random_uuid()` | Cfg:15-18 |
| EXERCISE_VIDEO_EXERCISE_ID | uuid FK EXERCISES Cascade | no | – | Cfg:20-22,81-84 |
| EXERCISE_VIDEO_MUX_ASSET_ID | varchar(100) | **no** | – | Cfg:24-27 |
| EXERCISE_VIDEO_MUX_PLAYBACK_ID | varchar(100) | yes | – | Cfg:29-31 |
| EXERCISE_VIDEO_PLAYBACK_POLICY | varchar(10) | no | `'PUBLIC'` | Cfg:37-42 |
| EXERCISE_VIDEO_STATUS | varchar(10) | no | `'WAITING'` | Cfg:48-53 |
| EXERCISE_VIDEO_DURATION_SECONDS | numeric(8,2) | yes | – | Cfg:55-57 |
| EXERCISE_VIDEO_ASPECT_RATIO | varchar(10) | yes | – | Cfg:59-61 |
| _CREATED_DATE / _UPDATED_DATE | timestamptz | no | – | Cfg:63-69 |

`UX_EXERCISE_VIDEOS_EXERCISE` UNIQUE (one video per exercise, Cfg:73-75, M9:262-266); `UX_EXERCISE_VIDEOS_MUX_ASSET` UNIQUE (Cfg:77-79, M9:268-272); `CK_EXERCISE_VIDEOS_POLICY` PUBLIC/SIGNED (M9:327-333); `CK_EXERCISE_VIDEOS_STATUS` WAITING/READY/ERRORED (M9:335-341). No column for a MUX upload id.

**MUSCLE_GROUPS** (`Mentora.Core/Entities/MuscleGroupItem.cs:7-18`, `Cfg/MuscleGroupItemConfiguration.cs`; M9:177-191): MUSCLE_GROUP_ID uuid PK; MUSCLE_GROUP_NAME varchar(60) not null (Cfg:18-21); MUSCLE_GROUP_SORT_ORDER integer not null (Cfg:23-25); MUSCLE_GROUP_LEGACY_CODE varchar(40) not null (Cfg:29-32); created/updated timestamptz. `UX_MUSCLE_GROUPS_NAME` UNIQUE (Cfg:42-44, M9:274-278); `CK_MUSCLE_GROUPS_LEGACY_CODE` = 13 MuscleGroup codes (M9:311-317). **No IS_ACTIVE.**

**EQUIPMENT** (`Mentora.Core/Entities/EquipmentItem.cs:7-25`, `Cfg/EquipmentItemConfiguration.cs`; M9:132-149). `NoEquipmentName = "Sans matériel"` at `EquipmentItem.cs:10`. EQUIPMENT_ID uuid PK; EQUIPMENT_SOURCE_CODE varchar(10) null (Cfg:20-22); EQUIPMENT_NAME varchar(100) not null (Cfg:24-27); EQUIPMENT_CATEGORY varchar(60) null; EQUIPMENT_SUBCATEGORY varchar(60) null; EQUIPMENT_LEGACY_CODE varchar(40) not null default `'OTHER'` (Cfg:37-41); EQUIPMENT_IS_ACTIVE boolean default true (Cfg:43-46); dates. `UX_EQUIPMENT_NAME` UNIQUE (Cfg:56-58, M9:251-255); `UX_EQUIPMENT_SOURCE_CODE` partial UNIQUE (M9:358-363); `CK_EQUIPMENT_LEGACY_CODE` = 8 Equipment codes (M9:319-325). No service filters on EQUIPMENT_IS_ACTIVE (`ES:349-355`).

**OBJECTIVES** (`Mentora.Core/Entities/Objective.cs:3-10`, `Cfg/ObjectiveConfiguration.cs`; M9:193-205): OBJECTIVE_ID uuid PK; OBJECTIVE_NAME varchar(60) not null; dates. `UX_OBJECTIVES_NAME` UNIQUE (Cfg:31-33, M9:280-284). **No IS_ACTIVE, no sort order.**

### 4.3.2 Enums

Each enum's codes exist three times: `EnumMappings` entry, a private converter in the EF configuration, and the migration CHECK.

| Enum | Values → code | EnumMappings | EF converter | DB CHECK |
|---|---|---|---|---|
| MuscleGroup (`Mentora.Core/Enums/MuscleGroup.cs:3-18`) | CHEST, BACK, SHOULDERS, BICEPS, TRICEPS, FOREARMS, ABS, QUADRICEPS, HAMSTRINGS, GLUTES, CALVES, FULL_BODY, CARDIO | `EnumMappings.cs:240-282` | `Cfg/ExerciseConfiguration.cs:171-205` | only MUSCLE_GROUPS.LEGACY_CODE (M9:311-317) |
| Equipment (`Equipment.cs:3-13`) | BODYWEIGHT, BARBELL, DUMBBELL, KETTLEBELL, MACHINE, CABLE, ELASTIC, OTHER | `EnumMappings.cs:285-317` | `Cfg/ExerciseConfiguration.cs:207-231` | only EQUIPMENT.LEGACY_CODE (M9:319-325) |
| ExerciseLevel (`ExerciseLevel.cs:7-12`) | BEGINNER, INTERMEDIATE, ADVANCED | `EnumMappings.cs:419-438` | `Cfg/ExerciseConfiguration.cs:155-169` | M9:343-349 |
| ExerciseVideoStatus (`ExerciseVideoStatus.cs:7-12`) | WAITING, READY, ERRORED | `EnumMappings.cs:441-460` | `Cfg/ExerciseVideoConfiguration.cs:101-115` | M9:335-341 |
| VideoPlaybackPolicy (`VideoPlaybackPolicy.cs:7-11`) | PUBLIC, SIGNED | `EnumMappings.cs:463-480` | `Cfg/ExerciseVideoConfiguration.cs:87-99` | M9:327-333 |

### 4.3.3 IExerciseService / ExerciseService

Interface `Mentora.Core/Interfaces/IExerciseService.cs:5-42`; DI `P:250`.

| Method | Visibility | Active filter | Ref |
|---|---|---|---|
| ListForCoachAsync | `COACH_ID IS NULL OR = coachId`; scope MENTORA/MINE/ALL | only if `includeInactive=false` | `ES:21-73` (scope `:31-38`, active `:40-41`) |
| GetByIdForCoachAsync | `IS NULL OR = coachId` | none (inactive returned) | `ES:75-83` |
| CreateAsync | writes `COACH_ID = coachId` (never NULL) | sets true | `ES:85-110` |
| UpdateAsync | `COACH_ID = coachId` only (Mentora row → 404) | none | `ES:112-137` |
| DeleteAsync | `COACH_ID = coachId` only | soft delete IS_ACTIVE=false | `ES:139-149` |
| ListForMemberAsync | `VisibleToMember` (NULL or linked coach via MEMBER_COACHES) | active only | `ES:151-259`, helper `ES:298-302` |
| GetByIdForMemberAsync | VisibleToMember | active only | `ES:261-268` |
| GetFiltersForMemberAsync | VisibleToMember | active only | `ES:270-271` |
| GetFiltersForCoachAsync | `IS NULL OR = coachId` | active only | `ES:273-277` |
| ResolveVisibleActiveIdsAsync | `IS NULL OR = coachId` | active only | `ES:279-293` |

Member list: search 2-60 chars after trim (`ES:18-19,160-167`), limit 1-100 (`ES:155-156`), both → 400. Coach list search has no length check (`ES:53-64`). Sort: name, id (`ES:184-188`); with search: rank, name, id (`ES:220-223`).

Helpers, for an admin scope (`COACH_ID IS NULL`, inactive included):

| Helper | Reusable as is? | Ref |
|---|---|---|
| `ApplyCatalogFilters` | Yes: static, no scope or active predicate. Level filter excludes NULL levels. | `ES:312-339` (`:330-335`) |
| `WithDetail` | Yes: static Includes (PrimaryMuscleGroup, PrimaryEquipment, SecondaryEquipment, Video, Objectives). | `ES:304-310` |
| `ExerciseSearch.WithRank` | Yes from inside Infrastructure (`internal static`), any `IQueryable<Exercise>`, `MaxRank=3`; matches NAME, NAME_EN, SEARCH_TAGS only. | `ExerciseSearch.cs:22,39-62` |
| Cursor encode/decode | Logic yes, but `private static` in ExerciseService; payload `(Name, Id)` or `(Rank, Name, Id)` base64 JSON, does not bind scope/filters; assumes name+id keyset. | `ES:411-457` |
| `ToResponse` | Yes for detail, `private static`; does not expose SearchTags, video id, MuxAssetId, PlaybackPolicy; always emits legacy MuscleGroup/Equipment. | `ES:468-512` |
| `BuildFiltersAsync` | Yes with any `visible` query; both callers pass active-only queries. | `ES:341-364` |

**Not reusable:**
- `ListForMemberAsync`: hard-wired to `VisibleToMember` active only (`ES:169`); `ListRow`/`ExerciseSummaryDto` carry no IsActive, CoachId, SourceCode (`ES:189-197,459-461`).
- `ListForCoachAsync`: whole list, no pagination, full `ExerciseResponse` (`ES:66-72`); requires a coachId and keeps legacy enum filters (`ES:27-28,43-47`).
- Write path: Create writes only COACH_ID, NAME, DESCRIPTION, INSTRUCTIONS, VIDEO_URL, IMAGE_URL, MUSCLE_GROUP (legacy), EQUIPMENT (legacy), IS_POLYARTICULAR, IS_ACTIVE=true, dates (`ES:90-104`). Update writes the same minus COACH_ID, IS_ACTIVE, CREATED_DATE (`ES:124-132`). Both ignore SOURCE_CODE, NAME_EN, CATEGORY, MOVEMENT_PATTERN, TYPE, LATERALITY, STARTING_POSITION, GRIP_TYPE, PRIMARY_MUSCLE_GROUP_ID, SECONDARY_MUSCLES, PRIMARY/SECONDARY_EQUIPMENT_ID, MIN_LEVEL, BREATHING, COMMON_MISTAKES, PRECAUTIONS, COACH_TIP, SEARCH_TAGS, objective links, videos. No reactivation path.
- Duplicate name on `IX_EXERCISES_COACH_NAME` is not handled in Create/Update (`ES:106-107,134`) → generic 500.

### 4.3.4 DTOs and validator (`Mentora.Core/DTOs/Exercise/`)

- `ExerciseRequest` (`ExerciseRequest.cs:19-28`): Name, Description?, Instructions?, VideoUrl?, ImageUrl?, MuscleGroup (legacy code), Equipment (legacy code), IsPolyarticular.
- `ExerciseResponse` (`ExerciseResponse.cs:36-69`): ExerciseId, CoachId?, Name, Description?, Instructions?, VideoUrl?, ImageUrl?, MuscleGroup, Equipment (legacy, "Removed in Lot 10" `:11-12`), IsPolyarticular, IsActive, CreatedDate, UpdatedDate, SourceCode?, NameEn?, Category?, MovementPattern?, ExerciseType?, Laterality?, StartingPosition?, GripType?, PrimaryMuscleGroup `ExerciseMuscleGroupRef?`, SecondaryMuscles `IReadOnlyList<string>`, PrimaryEquipment/SecondaryEquipment `ExerciseEquipmentRef?`, Level?, Breathing?, CommonMistakes `IReadOnlyList<string>`, Precautions?, CoachTip?, Objectives `IReadOnlyList<ExerciseObjectiveRef>`, Video `ExerciseVideoResponse?`.
- Refs: `ExerciseMuscleGroupRef(Guid MuscleGroupId, string Name)` `:74`; `ExerciseEquipmentRef(Guid EquipmentId, string Name)` `:79`; `ExerciseObjectiveRef(Guid ObjectiveId, string Name)` `:84`; `ExerciseVideoResponse(PlaybackId?, Status, DurationSeconds?, AspectRatio?)` `:91-95`.
- `ExerciseListResponseDto` (`ExerciseListResponseDto.cs:11-13`): Items, NextCursor?. `ExerciseSummaryDto` (`:21-26`): ExerciseId, Name, Level?, PrimaryEquipment?, SecondaryEquipment?.
- `ExerciseFiltersResponse` (`ExerciseFiltersResponse.cs:11-15`): MuscleGroups (`ExerciseFilterMuscleGroup(Id, Name, SortOrder)` `:21`), Equipment (`ExerciseFilterEquipment(Id, Name, Category?)` `:27`), Objectives, Levels.
- `ExerciseRequestValidator` (`Mentora.Core/Validators/Exercise/ExerciseRequestValidator.cs:7-41`): Name NotEmpty max 120; Description max 2000; Instructions max 4000; VideoUrl/ImageUrl max 500; MuscleGroup/Equipment NotEmpty and in WireValues (case-sensitive, no trim). Only exercise validator.

### 4.3.5 Controllers

| Controller | Route | Verb / action | Policy / Group | Query | Status |
|---|---|---|---|---|---|
| `Mentora.API/Controllers/Member/MemberExercisesController.cs:8-13` | `api/v1/member/exercises` | GET GetAll `:31-50` | MemberOnly / mobile | muscleGroupId, equipmentId, objectiveId, level, search, limit=50, cursor | 200, 400, 401, 403 |
| | `/{exerciseId:guid}` | GET GetById `:55-65` | same | – | 200, 401, 403, 404 |
| `Mentora.API/Controllers/Coach/CoachExercisesController.cs:9-14` | `api/v1/coach/exercises` | GET GetAll `:36-58` | CoachOnly / coach | search, muscleGroup, equipment (legacy), muscleGroupId, equipmentId, objectiveId, level, scope="ALL", includeInactive=false | 200, 400, 401, 403 |
| | `/{exerciseId:guid}` | GET GetById `:66-76` | same | – | 200, 401, 403, 404 |
| | root | POST Create `:82-95` (201 + Location) | same | body ExerciseRequest | 201, 400, 401, 403, 422 |
| | `/{exerciseId:guid}` | PUT Update `:104-116` | same | body | 200, 400, 401, 403, 404, 422 |
| | `/{exerciseId:guid}` | DELETE `:124-134` (204, soft) | same | – | 204, 401, 403, 404 |
| `Mentora.API/Controllers/Member/MemberExerciseFiltersController.cs:8-29` | `api/v1/member/exercise-filters` | GET | MemberOnly / mobile | – | 200, 401, 403 |
| `Mentora.API/Controllers/Coach/CoachExerciseFiltersController.cs:8-30` | `api/v1/coach/exercise-filters` | GET | CoachOnly / coach | – | 200, 401, 403 |

`ExerciseListOperationFilter` (`Mentora.API/Swagger/ExerciseListOperationFilter.cs:14-42`, `P:123`): on the two GetAll actions sets `level` enum to ExerciseLevel WireValues (`:26-30`); on the coach list marks `muscleGroup`/`equipment` deprecated (`:35-40`).

### 4.3.6 Consumers of exercises (rename / deactivation impact)

By id, with FK:
- `Cfg/ProgramExerciseConfiguration.cs:91-94`: PROGRAM_EXERCISES.PROGRAM_EXERCISE_EXERCISE_ID → EXERCISES **Restrict** (column `Mig/20260812084549_Lot6_3_Programs.cs:157`).
- EXERCISE_OBJECTIVES and EXERCISE_VIDEOS Cascade (above).

By id inside jsonb, no FK:
- `Cfg/ProgramTemplateConfiguration.cs:48-51` PROGRAM_TEMPLATE_BODY (`Mentora.Core/DTOs/ProgramTemplate/Body/ProgramTemplateExercise.cs:5`).
- `Cfg/SessionTemplateConfiguration.cs:46-49` SESSION_TEMPLATE_CONTENT; platform templates may reference only Mentora exercises (`Mentora.Core/Entities/SessionTemplate.cs:9-10`).

Active-visibility checks (deactivation affects them):
- `Mentora.Core/Validators/ProgramTemplate/ProgramTemplateBodyValidator.cs:74-78` → ResolveVisibleActiveIdsAsync, 422 "Unknown or inaccessible exercise id(s)". Used by `Mentora.Infrastructure/Services/ProgramTemplateService.cs:67,99`, `Mentora.Infrastructure/Services/ProgramService.cs:104` (+`:790-799`), and `Mentora.Core/Validators/Program/UpdateProgramRequestValidator.cs:38-40` via `ProgramService.cs:179-183` (every program PUT re-checks every node, kept ones included).
- `Mentora.Core/Validators/SessionTemplate/SessionTemplateRequestValidator.cs:73-80`, used by `SessionTemplateService.cs:130,160,222-229`.
- `Mentora.Core/Validators/ProgramTemplate/ProgramCircuitRules.cs:73`: `onExercise(itemPath, exercise.ExerciseId)` callback feeding both validators.

Paths skipping the active check: `ProgramService.cs:73-83` (assign from template), `ProgramService.cs:140-142,859-866` (deep copy), `SessionTemplateService.cs:183-200` (Duplicate).

Live reads by id without active filter (a rename shows everywhere at once): `ProgramService.cs:751-754`, `:898-901`; mapping `:1000-1010` (name, instructions, URLs, legacy enums; `"(unavailable)"` only when missing); `Mentora.Core/DTOs/Program/ProgramExerciseResponse.cs:4-16`; "last performed" `ProgramService.cs:1049-1090`; 409 PROGRAM_HAS_HISTORY `ProgramService.cs:299-303`.

By name: ExerciseSeeder, ProgramTemplateSeeder, CatalogSeeder (see 4.4); `ES:352` (filter sort depends on exact `NoEquipmentName`). Quiz, Agenda and Session services: no exercise reference.

---

## 4.4 Seeders

### 4.4.1 Order (all environments, no environment guard)

Inside one scope after migrations (`P:392-405`): `MigrateAsync` `P:396` → `CoachParameterSeeder` `P:398` → `OfferProgramSeeder` `P:399` → `ExerciseSeeder` `P:400` → `MuscleGroupSeeder` `P:401` → `CatalogSeeder` `P:402` → `ProgramTemplateSeeder` `P:403` → `QuizQuestionSeeder` `P:404`.

### 4.4.2 CatalogSeeder (`CS`)

- Resources (`Mentora.Infrastructure/Mentora.Infrastructure.csproj:23-26`): `Mentora.Catalog.EXERCICES.csv`, `MATERIELS.csv`, `MUSCLES.csv`, `EXERCICES_MUSCLES.csv`; prefix `CS:32`. CsvHelper 33.1.0 (`.csproj:13`).
- `ReadCsv` (`CS:838-867`): missing resource throws (`CS:841-842`); strict UTF-8, `throwOnInvalidBytes: true` (`CS:844`); `CsvConfiguration(CultureInfo.InvariantCulture)`, `Delimiter = ","`, `DetectColumnCountChanges = true` (`CS:845-849`); no ClassMap, rows read into `Dictionary<string,string>` by header (`CS:858-865`); missing required header throws (`CS:851-856`). Required EXERCICES headers `CS:34-41`.
- Row filter: kept only when `actif` = "Oui" (`CS:100`, `CS:869-870`).
- Mapping CSV → property for new rows (`CS:567-597`): exercise_id→SourceCode; nom_exercice_fr→Name; nom_exercice_en→NameEn; categorie→Category; resolved group→PrimaryMuscleGroupId; muscles_secondaires→SecondaryMuscles (SplitList); pattern_mouvement→MovementPattern; type_exercice→Type and IsPolyarticular = "Polyarticulaire" (`CS:593`); lateralite→Laterality; position_depart→StartingPosition; type_prise→GripType; materiel_principal→PrimaryEquipmentId and legacy Equipment (`CS:592`); materiel_secondaire→SecondaryEquipmentId; niveau_minimum→MinLevel (`MapLevel` `CS:738-746`, unknown → NULL + warning); consigne_courte→Description; execution_detaillee→Instructions; respiration→Breathing; erreurs_frequentes→CommonMistakes; precautions→Precautions; tags_recherche→SearchTags; objectifs_compatibles→EXERCISE_OBJECTIVES (`CS:667-680`). Legacy MuscleGroup derived (`CS:591,751-764`); CoachTip null (`CS:590`). `image_url`, `gif_url`, `video_url` are required headers but never read (only `CS:40`); no video created.
- Helpers: `Clean` = NFC + Trim, empty or `-` → NULL (`CS:873-878`); `SplitList` splits on `;`, Clean each, NULL if empty (`CS:881-889`). **Secondary muscles are stored raw in jsonb, never matched to MUSCLE_GROUPS** (`CS:575,700`). Primary group: EXERCICES_MUSCLES/MUSCLES then `groupe_musculaire_principal` label, case-insensitive (`CS:349-421`, `CS:355-357`).
- `NormalizeName` = NFC, Trim, lower, collapse whitespace (`CS:891-892`); `PluralKey` (equipment) strips one trailing s per word (`CS:895-896`).
- Idempotence keys: equipment by `EquipmentSourceCode`, never updated (`CS:256`); "Sans matériel" find-or-create by name (`CS:127-152`); missing equipment created (`CS:163-220`); objectives by name case-insensitive, never updated (`CS:303-345`); exercises by `ExerciseSourceCode` Ordinal over all Mentora rows, **no IsActive filter** (`CS:436-442`).
- Adoption: rows with null source code grouped by `NormalizeName(ExerciseName)` (`CS:443-445`); exactly one candidate → adopted (`CS:531-540,556-564`); several → CSV row skipped (`CS:533-538`); name taken by another imported row → skipped (`CS:541-546`). Other skips: duplicate exercise_id (`CS:104-110,475-479`), duplicate normalized name (`CS:457-460,511`), empty name (`CS:504-509`), over-length (`CS:513-528`).
- Transaction: one transaction, `BeginTransactionAsync` `CS:95`, `CommitAsync` `CS:118`.
- `FillNulls` (`CS:684-716`), guard `if (current is null && candidate is not null)` (`CS:690`): fills NameEn (697), Category (698), PrimaryMuscleGroupId (699), SecondaryMuscles (700), MovementPattern (701), Type (702), Laterality (703), StartingPosition (704), GripType (705), PrimaryEquipmentId (706), SecondaryEquipmentId (707), MinLevel (708), Description (709), Instructions (710), Breathing (711), CommonMistakes (712), Precautions (713), SearchTags (714). Never touches Name, IsActive, legacy MuscleGroup/Equipment, IsPolyarticular, CoachTip. Never overwrites a non-NULL value; never reactivates.

Every write to an existing EXERCISES row or its links:

| Line | Write |
|---|---|
| `CS:492-493` | FillNulls on imported row (by source code) |
| `CS:494`, `CS:676` | AddObjectiveLinks: inserts missing links, never removes |
| `CS:498` | `ExerciseUpdatedDate = now` when changed |
| `CS:559` | `ExerciseSourceCode = id` on adopted historical row |
| `CS:560` | FillNulls on adopted row |
| `CS:561` | `ExerciseUpdatedDate` on adopted row |
| `CS:605` | AddObjectiveLinks on adopted/new row |
| `CS:614-615` | historical rows (source null, primary equipment null, legacy Bodyweight): `PrimaryEquipmentId = Sans matériel`, UpdatedDate |
| `CS:633-634` | historical rows (source null, primary group null): `PrimaryMuscleGroupId` from legacy code (`CS:767-781`), UpdatedDate |
| `CS:649` | single SaveChangesAsync for exercises |

### 4.4.3 ExerciseSeeder (`Mentora.Infrastructure/Seeding/ExerciseSeeder.cs`)

- 45 hard-coded defaults (`DefaultExercises`, from `:20`).
- Existence check `:234-243`: `Where(e => e.ExerciseCoachId == null).Select(e => e.ExerciseName)` → `ToHashSet()` (Ordinal, case-sensitive, no trim), no IsActive filter, no source-code check. Missing ones inserted active (`:250-265`), no transaction, never updates.
- Admin deactivates one → not recreated. Admin renames one (even case only) or hard-deletes → **new row under the old name at next startup** (`:241-243,252`); `IX_EXERCISES_COACH_NAME` (M6:51-56) does not block since names differ. The recreated row has NULL source code, so CatalogSeeder may fill (`CS:614-615,633-634`) or adopt it (`CS:443-445`).

### 4.4.4 Other seeders (idempotence key)

- MuscleGroupSeeder: exact `MuscleGroupName`, Ordinal HashSet, create-only (`MuscleGroupSeeder.cs:41-52`); 14 names at `:18-34`.
- ProgramTemplateSeeder: exact `ProgramTemplateName` among Mentora templates, early return if none missing (`ProgramTemplateSeeder.cs:41-49`). Then `ToDictionaryAsync(e => e.ExerciseName)` over Mentora exercises, no IsActive filter (`:51-53`): two Mentora exercises with the same exact name would throw `ArgumentException` (today prevented by `IX_EXERCISES_COACH_NAME`); renamed exercise → `KeyNotFoundException` caught, template skipped with warning (`:65-71`); bodies use literal French names (e.g. `:104-116`).
- QuizQuestionSeeder: `QuizQuestionSourceCode` Ordinal (`QuizQuestionSeeder.cs:74-75,103`); fills empty fields only (`:106-110`).
- CoachParameterSeeder: coaches with no COACH_PARAMETERS row (`CoachParameterSeeder.cs:16-18`).
- OfferProgramSeeder: coaches with zero **active** OFFER_PROGRAMS rows (`OfferProgramSeeder.cs:24-25`).

### 4.4.5 Conclusion: code that would overwrite or recreate admin-edited data

1. `ExerciseSeeder.cs:241-243` + `:252-264`: rename (even case) or hard delete of one of the 45 defaults → recreated.
2. `CS:488-499` → `CS:697-714`: any FillNulls field cleared to NULL by an admin is refilled from the CSV (active or inactive rows).
3. `CS:494`, `CS:605` → `CS:676`: removed objective links are re-added.
4. `CS:567-598`: hard-deleted imported exercise recreated (source code absent from `bySource`, `CS:441-442,488`).
5. `CS:559-561`: admin rename of a historical (source NULL) row to a name matching a not-yet-imported CSV row → adopted.
6. `CS:614-615`, `CS:633-634`: cleared primary equipment / muscle group on historical rows refilled.
7. `MuscleGroupSeeder.cs:51-61`: renamed muscle group recreated; same pattern for objectives (`CS:303-345`) and "Sans matériel" (`CS:127-152`).

Nothing overwrites a non-NULL `ExerciseName` and nothing reactivates an exercise.

---

## 4.5 Session templates

- Entity `Mentora.Core/Entities/SessionTemplate.cs:12-42`: `SessionTemplateCoachId` Guid? (`:16`), `SessionTemplateSourceCode` string? (`:20`), IsActive "never settable through the API" (`:37-38`); platform rows "written only by the 11.6 seeder" (`:5`).
- Config `Cfg/SessionTemplateConfiguration.cs`: table SESSION_TEMPLATES (`:11`), PK `gen_random_uuid()` (`:13-16`), COACH_ID nullable (`:19-20`), SOURCE_CODE varchar(50) (`:22-24`), CONTENT jsonb (`:47-50`), IS_ACTIVE default true (`:52-55`), FK COACHES Restrict (`:67-70`), `IX_SESSION_TEMPLATES_COACH_ID` (`:72-73`).
- Migration `Mig/20261006065717_Lot11_5_SessionTemplates.cs`: `CK_SESSION_TEMPLATES_SOURCE_CODE` (COACH_ID NULL ⇔ SOURCE_CODE NOT NULL) `:46-49`; nature/type/content CHECKs `:52-64`; `UX_SESSION_TEMPLATES_SOURCE_CODE` partial unique `WHERE COACH_ID IS NULL` `:67-70`.
- Service `Mentora.Infrastructure/Services/SessionTemplateService.cs`: Create `:127-151` (CoachId = coach, SourceCode null, active); Update `:153-172` (`FindOwnedAsync` first → 404 for platform rows); Delete `:174-181` (hard delete); Duplicate `:183-206` (`FindVisibleAsync`, copy owned by coach). `FindOwnedAsync` `:217-220`, `FindVisibleAsync` `:210-215`. Controller `Mentora.API/Controllers/Coach/CoachSessionTemplatesController.cs:14-16` (CoachOnly, coach).
- No code inserts a platform row (COACH_ID NULL): only `SessionTemplates.Add` at `SessionTemplateService.cs:147,202`; no session-template seeder in `P:398-404`.

---

## 4.6 Infrastructure files

### 4.6.1 Dockerfile (`Mentora.API/Dockerfile`, only Dockerfile in the repo)

- `mcr.microsoft.com/dotnet/aspnet:8.0 AS base` (`:1`), `EXPOSE 8080` (`:3`); `mcr.microsoft.com/dotnet/sdk:8.0 AS build` (`:5`). Floating tags, no digest.
- Context = repo root (`.dockerignore:2`). Copies the three csproj (`:8-10`), **restores the project** `Mentora.API/Mentora.API.csproj` (`:12`), not the .sln; `COPY . .` (`:14`); build (`:16`) and publish (`:19`) of the API project; final stage copies `/app/publish` only (`:21-23`), `ENTRYPOINT ["dotnet","Mentora.API.dll"]` (`:24`). No ENV, USER, HEALTHCHECK, ARG.
- **Adding Mentora.Admin (Blazor WASM) to Mentora.sln alone does not break the image**: the .sln is never read (`:12,16,19`); the Admin sources would be copied by `:14` but not compiled.
- It **would break** if Mentora.API got a `ProjectReference` to Mentora.Admin (hosted model): restore at `:12` runs before `COPY . .` with only the three csproj from `:8-10`.
- No workload install in the Dockerfile; `wasm-tools` would only be needed for AOT/relinking. `dotnet workload list` locally: maui-windows, maccatalyst, ios, android (no wasm-tools).

### 4.6.2 `.dockerignore` (`:1-38`)

Excludes: `**/appsettings.Development.json` (`:12`), `bin` (`:18`), `obj` (`:19`), `.git` (`:22`), `.vs`, `.vscode` (`:25-26`), `docs` (`:29`), `*.tar` (`:34`), `**/node_modules` (`:38`). Comments state appsettings.json and appsettings.Production.json are not excluded (`:11`) and that the container runs `ASPNETCORE_ENVIRONMENT=Development` (`:9`). `bin`/`obj` have no `**/` prefix.

### 4.6.3 nginx

Files: `nginx/nginx.conf`, `nginx/conf.d/mentora.conf`.
- `nginx.conf`: `include /etc/nginx/conf.d/*.conf;` (`:15`).
- `mentora.conf`: HTTP `listen 80`, `server_name api-demo.mentorapp.fr 87.106.108.91` (`:17`), ACME location (`:20-23`), 301 to https (`:26-28`). HTTPS `listen 443 ssl` + http2, `server_name api-demo.mentorapp.fr` (`:37`), `client_max_body_size 25M` (`:40`), cert paths under `/etc/letsencrypt/live/api-demo.mentorapp.fr/` (`:43-44`), HSTS without includeSubDomains (`:55`), `location /hubs/` (`:58-72`) and `location /` (`:75-85`) → `proxy_pass http://api:8080` with `Host`, `X-Real-IP`, `X-Forwarded-For`, `X-Forwarded-Proto`. No `api.mentorapp.fr`, no `admin.mentorapp.fr`, no static root.

### 4.6.4 docker-compose.local.yml

Single service `postgres` (`:2`), `image: postgres:16` (`:3`), `container_name: mentora-postgres-local` (`:4`), env var names `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB` (`:6-8`), `ports: "5433:5432"` (`:10`), volume `mentora_local_data` (`:12,14-15`). No other compose file in the repo (the VPS compose with `api` service is off-repo, referenced by `mentora.conf:42,59,76`).

### 4.6.5 appsettings (key names only)

- `Mentora.API/appsettings.json`: `Logging:LogLevel:Default`, `Logging:LogLevel:Microsoft.AspNetCore`; `AllowedHosts`; `ConnectionStrings:DefaultConnection`; `Jwt:Secret|Issuer|Audience|AccessTokenExpirationMinutes|RefreshTokenExpirationDays` (`:12-17`); `RefreshTokenPurge:CountOnly|InitialDelayMinutes` (`:19-21`); `Email:Provider|FromAddress|FromDisplayName`, `Email:Smtp:Host|Port|UseStartTls|Username|Password`, `Email:Retry:MaxAttempts|DelaySeconds` (`:23-36`); `LiveKit:Url|ApiKey|ApiSecret|JoinBeforeMinutes|JoinAfterEndMinutes` (`:39-44`); `Quiz:QuestionCount|SessionLifetimeMinutes|AnonymousPermitLimit|AnonymousWindowMinutes` (`:46-50`). Non-empty (values not shown): `ConnectionStrings:DefaultConnection` (`:10`), `Jwt:Secret` (`:13`), several `Email:*` keys (`:24,25,28,31`).
- `Mentora.API/appsettings.Production.json`: `Logging:LogLevel:Default` only (`:2-4`).
- `Mentora.API/appsettings.Development.json`: exists on disk, gitignored (`.gitignore:487`), excluded from image.

### 4.6.6 scripts

`scripts/seed-test-members.sql` (manual idempotent insert of 4 test members, `:1-13`), `scripts/dev/README.md`, `scripts/dev/test-signalr.html` (SignalR test client, `@microsoft/signalr@8.0.0` from jsdelivr `:54`).

### 4.6.7 Projects and packages

`Mentora.sln`: 3 projects (`:6,8,10`), Debug/Release Any CPU (`:13-16`). No `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `nuget.config`, CI folder.

| Project | TFM | Packages | Refs |
|---|---|---|---|
| `Mentora.API/Mentora.API.csproj` (Sdk.Web) | net8.0 (`:24`) | FluentValidation.DependencyInjectionExtensions 12.1.1 (`:9`), Microsoft.AspNetCore.Authentication.JwtBearer 8.0.31 (`:10`), Microsoft.IdentityModel.Protocols.OpenIdConnect 8.22.0 (`:11-15`), Microsoft.EntityFrameworkCore.Design 8.0.0 (`:16-19`), Swashbuckle.AspNetCore 6.5.0 (`:20`) | Core, Infrastructure (`:4-5`) |
| `Mentora.Core/Mentora.Core.csproj` | net8.0 (`:4`) | FluentValidation 12.1.1 (`:12`) | – |
| `Mentora.Infrastructure/Mentora.Infrastructure.csproj` | net8.0 (`:35`) | BCrypt.Net-Next 4.0.3 (`:12`), CsvHelper 33.1.0 (`:13`), MailKit 4.17.0 (`:14`), Microsoft.EntityFrameworkCore 8.0.0 (`:15`), Npgsql.EntityFrameworkCore.PostgreSQL 8.0.0 (`:16`), System.IdentityModel.Tokens.Jwt 8.22.0 (`:17`); FrameworkReference Microsoft.AspNetCore.App (`:8`) | Core (`:4`) |

`dotnet list package --vulnerable`: no vulnerable top-level package in any project. With `--include-transitive`: Mentora.API and Mentora.Infrastructure report Microsoft.Extensions.Caching.Memory 8.0.0 (High, GHSA-qj66-m88j-hmgj), Npgsql 8.0.0 (High, GHSA-x9vc-6hfv-hg8c), System.Security.Cryptography.Pkcs 8.0.1 (High GHSA-846f-f764-76mx, Moderate GHSA-2fmv-rv2c-59x7); Mentora.API also System.Text.Json 8.0.0 (High GHSA-hh2w-p6rv-4g7w, GHSA-8g4q-xg66-9fp4). Backlog `docs/review/lot7/vulnerable-packages-backlog.md:12-18` lists the first, second and fourth; Pkcs is not in it.

---

## 4.7 Migrations

Folder: `Mentora.Infrastructure/Persistence/Migrations/` (21 migrations):

InitialCreate, AddAuthTables, AddLot2_Foundation, AddLot2_2_Catalog, AddLot2_3_Purchase, AddLot2_3_1b_PackDiscount, AddLot2_4_Sessions, AddConversationsAndMessages, AddLot5Parameters, Lot6_1_Exercises, Lot6_2_ProgramTemplates, Lot6_3_Programs, Lot6_4_GroupSessions, Lot6_5_Agenda, Lot9_PurchaseOrigin, Lot9_ExerciseCatalog, Lot10_Quiz, Lot11_2_ProgramTreeFields, Lot11_3_CoachSessionPreferences, Lot11_5_SessionTemplates, **Lot11_7_RestFieldsJsonKeys** (last, `20261006092320`).

`$env:ASPNETCORE_ENVIRONMENT="Development"; dotnet ef migrations has-pending-model-changes --project Mentora.Infrastructure --startup-project Mentora.API` → `No changes have been made to the model since the last migration.` The output also carries 32 `warn:` lines (EF `Model.Validation[20601]`, database default without sentinel) on 16 properties: CoachParameter.CoachParameterMissedSessionBehavior, ExerciseVideo.ExerciseVideoPlaybackPolicy, ExerciseVideo.ExerciseVideoStatus, Order.Origin, Order.Status, OrderItem.Sport, Product.ProductSport, Product.ProductStatus, ProductPack.ProductPackStatus, Program.ProgramStatus, ProgramSession.ProgramSessionStatus, Session.SessionStatus, SessionParticipant.SessionParticipantStatus, SessionVoucher.Sport, SessionVoucher.Status, StripeWebhookEvent.Status.

---

## 5.1 Embedded CSV distinct values

Source: `Mentora.Infrastructure/Seeding/Catalog/EXERCICES.csv`, read with `Import-Csv -Encoding UTF8 -Delimiter ','` (delimiter and encoding as in `CS:844-847`). Values NFC-normalized and trimmed; empty or `-` shown as `<NULL>` (same rule as `Clean`, `CS:873-878`). **251 data rows; 250 have `actif = Oui`, 1 has an empty `actif` (EX202, see Risks), so the seeder keeps 250 (`CS:100`). Counts below cover all 251 rows.** Grouping by PowerShell `Group-Object` is case-insensitive.

**categorie** (4 values)

| Value | Count |
|---|---|
| Musculation | 188 |
| Gainage | 35 |
| Cardio | 20 |
| Conditionnement | 8 |

**type_exercice** (10 values)

| Value | Count |
|---|---|
| Polyarticulaire | 123 |
| Monoarticulaire | 74 |
| Cardio | 18 |
| Isolation | 11 |
| Isométrique | 8 |
| Dynamique | 8 |
| Pliométrique | 4 |
| Conditionnement | 2 |
| Isométrique / dynamique | 2 |
| Bilatéral | 1 (EX202, shifted row) |

**lateralite** (5 values)

| Value | Count |
|---|---|
| Bilatéral | 179 |
| Unilatéral | 55 |
| Alterné | 14 |
| Diagonal | 2 |
| Allongé au sol | 1 (EX202, shifted row) |

**niveau_minimum** (4 values)

| Value | Count |
|---|---|
| Débutant | 126 |
| Intermédiaire | 107 |
| Avancé | 17 |
| Gainage ; Contrôle moteur ; Renforcement | 1 (EX202, shifted row) |

**type_prise** (16 values incl. NULL)

| Value | Count |
|---|---|
| `<NULL>` | 88 |
| Neutre | 85 |
| Pronation | 28 |
| Neutre ou pronation | 15 |
| Supination | 15 |
| Pronée | 4 |
| Pronation ou neutre | 3 |
| Variable | 3 |
| Pronation serrée | 3 |
| Débutant | 1 (EX202, shifted row) |
| Pronation / rack avant | 1 |
| Supination ou prise semi-pronée | 1 |
| Supinée | 1 |
| Pronée ou neutre | 1 |
| Pronée / mixte | 1 |
| Pronation large | 1 |

**position_depart** (47 values)

| Value | Count |
|---|---|
| Debout | 99 |
| Assis | 38 |
| Allongé au sol | 16 |
| Allongé sur banc | 11 |
| Position de planche | 8 |
| Suspendu | 8 |
| Buste penché | 7 |
| Appui avant-bras | 5 |
| À quatre pattes | 4 |
| Allongé sur banc incliné | 4 |
| Allongé ventre sur banc | 3 |
| Assis ou incliné | 3 |
| Dos sur banc | 3 |
| À genoux | 3 |
| Allongé sur le côté | 3 |
| Incliné | 2 |
| Buste incliné | 2 |
| Allongé sur banc décliné | 2 |
| Demi-squat | 2 |
| Assis ou allongé | 1 |
| Assis / dos appuyé | 1 |
| Assis ou danseuse | 1 |
| Appui bras tendus | 1 |
| Appui mains et pieds | 1 |
| À quatre appuis | 1 |
| Appui mains | 1 |
| Banc à 45 degrés | 1 |
| Allongé ventre sur support | 1 |
| Appui avant-bras et genou | 1 |
| Poids du corps | 1 (EX202, shifted row) |
| Allongé ventre | 1 |
| Appui sur banc | 1 |
| Pieds surélevés | 1 |
| Incliné sous anneaux | 1 |
| Suspendu sous barre | 1 |
| Au sol | 1 |
| Couché sur banc | 1 |
| Appui mains sur support | 1 |
| Appui mains et genoux | 1 |
| Machine | 1 |
| Assis ou debout | 1 |
| Debout sur support | 1 |
| Pied arrière surélevé | 1 |
| Allongé ventre au sol | 1 |
| Banc à lombaires | 1 |
| Machine assistée | 1 |
| Assis sur banc incliné | 1 |

**pattern_mouvement** (91 values, no NULL)

| Value | Count |
|---|---|
| Poussée horizontale | 18 |
| Tirage horizontal | 16 |
| Flexion de coude | 15 |
| Extension de hanche | 14 |
| Poussée verticale | 11 |
| Extension de coude | 9 |
| Tirage vertical | 8 |
| Anti-extension | 6 |
| Squat | 6 |
| Hinge | 6 |
| Flexion de genou | 5 |
| Adduction horizontale | 5 |
| Abduction hanche | 5 |
| Flexion du tronc | 5 |
| Abduction épaule | 5 |
| Anti-rotation | 4 |
| Abduction horizontale | 4 |
| Flexion épaule | 4 |
| Porté / locomotion | 4 |
| Flexion plantaire | 4 |
| Locomotion | 4 |
| Poussée horizontale inclinée | 4 |
| Anti-inclinaison | 3 |
| Poussée membres inférieurs | 2 |
| Extension de genou | 2 |
| Adduction horizontale inclinée | 2 |
| Flexion de hanche / bassin | 2 |
| Saut cyclique | 2 |
| Adduction hanche | 2 |
| Pédalage | 2 |
| Poussée horizontale déclinée | 2 |
| Sprint | 2 |
| Abduction / locomotion | 2 |
| Flexion du poignet | 2 |
| Extension d'épaule | 2 |
| Fente | 2 |
| Élévation scapulaire | 2 |
| Poussée diagonale | 2 |
| Rotation externe épaule | 2 |
| Rotation interne épaule | 2 |
| Rotation contrôlée | 2 |
| Locomotion quadrupède | 2 |
| Poussée locomotrice | 1 |
| Isométrique | 1 |
| Gainage dynamique | 1 |
| Anti-extension / anti-rotation | 1 |
| Rotation du tronc | 1 |
| Anti-inclinaison + abduction | 1 |
| Montée / descente cyclique | 1 |
| Locomotion verticale | 1 |
| Anti-extension + locomotion | 1 |
| Ondulation simultanée | 1 |
| Ondulation alternée | 1 |
| Montée cyclique | 1 |
| Marche arrière résistée | 1 |
| Tirage locomoteur | 1 |
| Course avec changements de direction | 1 |
| Mouvement cyclique | 1 |
| Porté / anti-extension | 1 |
| Inclinaison latérale | 1 |
| Flexion + rotation | 1 |
| Tirage vertical cyclique | 1 |
| Pédalage + poussée / tirage | 1 |
| Porté / stabilité | 1 |
| Pronation / supination | 1 |
| Extension du poignet | 1 |
| Rotation externe + poussée | 1 |
| Fente / locomotion | 1 |
| Fente / squat unilatéral | 1 |
| Suspension | 1 |
| Élévation plan scapulaire | 1 |
| Tirage bras tendus | 1 |
| Tirage cyclique | 1 |
| Squat + poussée | 1 |
| Élévation diagonale | 1 |
| Extension du tronc | 1 |
| Extension de hanche et tronc | 1 |
| Rotation externe / abduction hanche | 1 |
| Saut horizontal | 1 |
| Saut vertical | 1 |
| Flexion simultanée tronc et hanches | 1 |
| Flexion de hanche | 1 |
| Enroulement du bassin | 1 |
| Squat + saut | 1 |
| Descente contrôlée | 1 |
| Montée / locomotion | 1 |
| Fente latérale | 1 |
| Dorsiflexion cheville | 1 |
| Flexion de genou excentrique | 1 |
| Extension de genou dominante | 1 |

## 5.2 Secondary muscles

Split as `SplitList` does (`CS:881-889`: split on `;`, NFC + Trim, drop empty and `-`). 472 values in total; 29 rows have none. 39 distinct values case-insensitively, **60 case-sensitively** (21 values appear in two casings, e.g. `trapèzes`/`Trapèzes`, `Fessiers`/`fessiers`, `deltoïde antérieur`/`Deltoïde antérieur`, `épaules`/`Épaules`). The seeder stores them as-is (no case folding, `CS:575,700`).

MUSCLE_GROUPS names compared: the 14 names of `MuscleGroupSeeder.cs:20-33` (Pectoraux, Dos, Épaules, Bras, Avant-bras, Quadriceps, Ischio-jambiers, Fessiers, Adducteurs, Mollets, Tibial, Abdominaux, Centre du corps, Hanche), compared case-insensitively. The local DB content was not checked (see 5.3).

| Value (first casing seen) | Count | Matches a MUSCLE_GROUPS name? |
|---|---|---|
| Fessiers | 47 | yes |
| trapèzes | 37 | **no** |
| ischio-jambiers | 34 | yes |
| deltoïde antérieur | 33 | **no** |
| Abdominaux | 26 | yes |
| Biceps | 25 | **no** |
| centre du corps | 23 | yes |
| Triceps brachial | 22 | **no** |
| épaules | 22 | yes |
| avant-bras | 22 | yes |
| Triceps | 20 | **no** |
| Mollets | 18 | yes |
| Quadriceps | 17 | yes |
| deltoïde postérieur | 14 | **no** |
| pectoraux | 13 | yes |
| Moyen fessier | 10 | **no** |
| grand rond | 9 | **no** |
| Fléchisseurs de hanche | 8 | **no** |
| érecteurs du rachis | 8 | **no** |
| petit fessier | 7 | **no** |
| rhomboïdes | 6 | **no** |
| jambes | 6 | **no** |
| Obliques | 6 | **no** |
| adducteurs | 5 | yes |
| bras | 5 | yes |
| Transverse | 4 | **no** |
| carré des lombes | 4 | **no** |
| grand dorsal | 4 | **no** |
| brachio-radial | 3 | **no** |
| coiffe des rotateurs | 3 | **no** |
| Dorsaux | 2 | **no** |
| Brachial | 2 | **no** |
| Abdominaux profonds | 1 | **no** |
| Dos | 1 | yes |
| hanches | 1 | **no** (plural of Hanche) |
| extenseurs avant-bras | 1 | **no** |
| Supra-épineux | 1 | **no** |
| Trapèzes inférieurs | 1 | **no** |
| Abducteurs hanche | 1 | **no** |

Summary: 12 distinct values (233 occurrences) match a MUSCLE_GROUPS name; **27 distinct values (239 occurrences) do not**. `Tibial` and `Hanche` are never used as secondary muscles.

## 5.3 Local database (read-only)

**Not executed.** Evidence:
- `docker ps -a --format ...` → empty output, exit 0: no container exists (no `mentora-postgres-local`).
- `docker images` → empty: no `postgres:16` image cached.
- `docker volume ls` → `repo-mentora_mentora_local_data` exists.

Starting the container would pull an image and create a container, which is beyond a read-only pass. The user chose (in this session) to skip it and record it here. No SQL was run. The data points below remain open: Mentora exercises active/inactive, with/without source code, DISTINCT counts on EXERCISE_CATEGORY, EXERCISE_MOVEMENT_PATTERN, EXERCISE_TYPE, EXERCISE_LATERALITY, EXERCISE_STARTING_POSITION, EXERCISE_GRIP_TYPE, duplicate names after `lower(trim())`, EXERCISE_VIDEOS row count.

Read-only block to run once the container exists (not run):

```sql
BEGIN READ ONLY;
SELECT "EXERCISE_IS_ACTIVE", ("EXERCISE_SOURCE_CODE" IS NOT NULL) AS has_source, count(*)
  FROM "EXERCISES" WHERE "EXERCISE_COACH_ID" IS NULL GROUP BY 1, 2 ORDER BY 1, 2;
SELECT 'category' AS col, "EXERCISE_CATEGORY" AS val, count(*) FROM "EXERCISES" WHERE "EXERCISE_COACH_ID" IS NULL GROUP BY 2
UNION ALL SELECT 'movement_pattern', "EXERCISE_MOVEMENT_PATTERN", count(*) FROM "EXERCISES" WHERE "EXERCISE_COACH_ID" IS NULL GROUP BY 2
UNION ALL SELECT 'type', "EXERCISE_TYPE", count(*) FROM "EXERCISES" WHERE "EXERCISE_COACH_ID" IS NULL GROUP BY 2
UNION ALL SELECT 'laterality', "EXERCISE_LATERALITY", count(*) FROM "EXERCISES" WHERE "EXERCISE_COACH_ID" IS NULL GROUP BY 2
UNION ALL SELECT 'starting_position', "EXERCISE_STARTING_POSITION", count(*) FROM "EXERCISES" WHERE "EXERCISE_COACH_ID" IS NULL GROUP BY 2
UNION ALL SELECT 'grip_type', "EXERCISE_GRIP_TYPE", count(*) FROM "EXERCISES" WHERE "EXERCISE_COACH_ID" IS NULL GROUP BY 2
ORDER BY 1, 3 DESC;
SELECT count(*) AS duplicate_name_groups FROM (
  SELECT lower(trim("EXERCISE_NAME")) FROM "EXERCISES" WHERE "EXERCISE_COACH_ID" IS NULL
  GROUP BY 1 HAVING count(*) > 1) d;
SELECT count(*) AS exercise_videos FROM "EXERCISE_VIDEOS";
ROLLBACK;
```

---

## Risks and surprises

1. **Seeders undo admin edits** (4.4.5): a field cleared to NULL is refilled from the CSV at every startup (`CS:690,697-714`); renaming (even case only) or hard-deleting one of the 45 ExerciseSeeder defaults recreates it under the old name (`ExerciseSeeder.cs:241-243,252`); removed objective links come back (`CS:676`). Seeders run in every environment (`P:392-405`). Nothing marks a row as admin-edited.
2. **CSV row EX202 "Dead bug" is shifted by one column** from `type_exercice` on (EXERCICES.csv line 204): `type_exercice=Bilatéral`, `lateralite=Allongé au sol`, `position_depart=Poids du corps`, `type_prise=Débutant`, `niveau_minimum=Gainage ; ...`, `actif` empty. The seeder skips it because `actif` is not "Oui" (`CS:100`), and it passes `DetectColumnCountChanges` (same column count). It pollutes the distinct lists in 5.1 with 5 spurious values.
3. **Reference values are free text, not closed lists**: 91 movement patterns, 47 starting positions, 16 grip types, with near-duplicates (`Pronée`/`Pronation`, `Pronation ou neutre`/`Neutre ou pronation`, `Supinée`/`Supination`); laterality and type mix concepts (`Bilatéral` in type_exercice). Secondary muscles: 27 of 39 distinct values match no MUSCLE_GROUPS name and 21 values exist in two casings (5.2); they are stored as a jsonb string list (`Cfg/ExerciseConfiguration.cs:95`).
4. **No admin anything exists**: no admin policy, claim, scheme, table or Swagger doc (`P:181-193`, `P:34-51`). Plain `[Authorize]` (logout, account, devices, ChatHub) accepts any token of the default `Bearer` scheme; `CoachOnly`/`MemberOnly` are claim-only checks with no scheme restriction (`P:183-188`). An admin token signed with the same `Jwt` settings would pass them if it carried those claims.
5. **`QuizPlayer` and the quiz rate limiter decide on the presence of an `Authorization` header, not on the scheme** (`QuizPlayerRequirement.cs:26`, `QuizRateLimiting.cs:25-26`).
6. **CORS origins are hard-coded localhost/capacitor values with `AllowCredentials`, applied globally** (`P:354-365,424`): `admin.mentorapp.fr` needs a code change; adding it opens every route to it.
7. **Forwarded headers trust any source** (`P:372-373`): matters for any IP-based admin rate limit or allow-list.
8. **No auth endpoint is rate-limited and there is no OTP attempt counter** (`AuthOtp.cs:3-13`, `QuizRateLimiting.cs` only on `QuizBasicController.cs:18`).
9. **The console OTP log line runs in every environment** (`AS:61`) and the VPS container runs in Development (`.dockerignore:9`; `docs/review/tests-vps-lot51-lot6.md:247`). `appsettings.Production.json` holds only a log level (`:2-4`), so a real Production environment has never been exercised.
10. **Tracked `appsettings.json` has non-empty `Jwt:Secret`, `ConnectionStrings:DefaultConnection` and `Email:*` keys** (`:10,13,24-31`), copied into the image by publish; CLAUDE.md says Email settings are environment variables only.
11. **The coach write path ignores every Lot 9 catalog field** (`ES:90-104,124-132`); legacy EXERCISE_MUSCLE_GROUP and EXERCISE_EQUIPMENT are still NOT NULL (`Cfg/ExerciseConfiguration.cs:45-63`), so an admin create must supply or derive them.
12. **Deactivating an exercise makes every later PUT of a program or template containing it fail with 422** (`UpdateProgramRequestValidator.cs:38-40`, `ProgramTemplateBodyValidator.cs:74-78`), while assign-from-template still copies it silently (`ProgramService.cs:73-83`). Renames are visible at once in every program, past and current (`ProgramService.cs:1000-1010`).
13. **Duplicate exercise names return 500** (unique index `IX_EXERCISES_COACH_NAME` not mapped, `ES:106-107,134`); the index is case-sensitive and counts inactive rows.
14. **`ExerciseResponse` cannot round-trip admin fields**: no SearchTags, video id, MuxAssetId, PlaybackPolicy (`ES:468-512`). EXERCISE_VIDEOS requires a non-null MUX asset id (`Cfg/ExerciseVideoConfiguration.cs:24-27`) and has no upload-id column, so a direct-upload row cannot exist before MUX returns an asset id.
15. **Swagger is public in every environment** (`P:414-422`) and its Bearer security requirement is global (`P:101-104`).
16. **401/403 from the auth handler have no envelope body** (no `OnChallenge`/`OnForbidden`, `P:165-177`).
17. **Only Email options are validated before migrations** (`P:378-390`); LiveKit/Quiz `ValidateOnStart` fire after migrations and seeders. `JwtSettings` has no validation.
18. **Infrastructure off-repo**: nginx serves only `api-demo.mentorapp.fr` (`mentora.conf:17,37`); the VPS compose file is not in the repo; no CI; floating `sdk:8.0`/`aspnet:8.0` tags and no `global.json`.
19. **Transitive vulnerabilities**: Npgsql 8.0.0, System.Text.Json 8.0.0, Caching.Memory 8.0.0 (High) and System.Security.Cryptography.Pkcs 8.0.1 (High + Moderate, not in the lot 7 backlog).
20. **Platform session templates**: the "11.6 seeder" referenced at `SessionTemplate.cs:5` does not exist (`P:398-404`).
21. **Minor**: `GenerateOtp` can throw `OverflowException` when the random int is `int.MinValue` (`AS:336`); malformed refresh token → 400, wrong secret → 401 (`AS:315`); `RefreshTokenAsync` ignores `UserIsEnabled`; purge service loop has no try/catch (`RefreshTokenPurgeService.cs:58-96`); no AUTH_OTPS purge.

## Open questions for the architect

1. 5.3 was not run (no local container). Should it be run before 12.1 design, and against which DB (local volume or a dump of api-demo)?
2. Seeder strategy for admin-edited rows: disable `ExerciseSeeder` now that CatalogSeeder exists? Stop `FillNulls` on rows touched by an admin (marker column)? Are all 45 defaults already adopted (source code set)? Needs 5.3.
3. Will the admin allow hard delete, clearing optional fields to NULL, or editing `EXERCISE_SOURCE_CODE`? Each triggers recreation or adoption (4.4.5).
4. Closed lists as tables: which columns become FK tables (category, movement pattern, type, laterality, starting position, grip type, secondary muscles)? Who cleans the near-duplicates in 5.1/5.2, and is the CSV (incl. EX202) the source of truth or the DB?
5. Should secondary muscles become a join to MUSCLE_GROUPS? 27 of 39 values have no match, and MUSCLE_GROUPS has no IS_ACTIVE (neither has OBJECTIVES).
6. Admin JWT: own issuer/audience/secret or shared `Jwt` section? Should existing plain `[Authorize]` endpoints and policies be pinned to the user scheme explicitly?
7. Is the admin Swagger doc public like the others, and should it show only the admin security scheme?
8. CORS: config-driven origin list per environment? Does the admin front use cookies (refresh token) or only a Bearer header (`AllowCredentials` need)?
9. PROD: will it run `ASPNETCORE_ENVIRONMENT=Production`? Which settings move to environment variables? Where is the VPS compose file, and is PROD the same VPS as api-demo?
10. Mentora.Admin hosting: standalone static site (own nginx root/image) or hosted by the API? AOT (needs `wasm-tools`, absent from `sdk:8.0` stage)?
11. Deactivation rule: allowed while programs/templates reference the exercise, given the 422 on the next program PUT?
12. Name uniqueness for Mentora exercises: case- and accent-insensitive? Today exact and including inactive rows.
13. MUX: one video per exercise (`UX_EXERCISE_VIDEOS_EXERCISE`) and non-null asset id: is a separate upload-tracking table or a nullable asset id expected?
14. `.dockerignore` bare `bin`/`obj` (`:18-19`): root-only match, so project `bin/obj` folders may enter the build context; intended?
15. What is `USER_ROLE` (`Cfg/UserConfiguration.cs:26`) meant to hold? Nothing reads or writes it.
