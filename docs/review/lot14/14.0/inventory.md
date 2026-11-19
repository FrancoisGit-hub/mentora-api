# Lot 14.0: read-only inventory (Beta launch)

- Date: 2026-11-19
- Branch: feature/lot_14 (created from origin/main = d727ba1, upstream unset)
- Mode: read-only. No code changed. Only this file was written.
- Paths are relative to the repository root. Migrations live in `Mentora.Infrastructure/Persistence/Migrations/`.
- Sensitive files (API appsettings, .env, logs) were not opened. Personal data found in a committed script is mentioned by file only, not reproduced.

---

## A. Accounts and membership

### A1. Entities and configurations

USERS (`Mentora.Core/Entities/User.cs:3-22`, `Mentora.Infrastructure/Persistence/Configurations/UserConfiguration.cs`)
- PK `USER_ID` default `gen_random_uuid()` (`UserConfiguration.cs:14-16`).
- NOT NULL: `USER_CREATED_DATE` (`:18`), `USER_EMAIL` varchar(255) (`:20`), `USER_IS_ENABLED` (`:24`), `USER_ROLE` varchar(50) (`:26`). None has a DB default.
- Nullable: `USER_MODIFICATION_DATE`, `USER_PSW`, `USER_LOGIN`, `USER_DISABLED_DATE`, `USER_DELETION_REQUESTED_DATE`, `USER_DELETION_REASON` (`:19,22,23,25,27,28`).
- `UserRole` is a plain string, not an enum (`User.cs:13`).
- Unique index on email: `HasIndex(e => e.UserEmail).IsUnique()` (`UserConfiguration.cs:21`), created as plain btree `IX_USERS_USER_EMAIL` (`20260326120304_AddAuthTables.cs:214-218`). No citext, no collation, no lower() index anywhere. **The unique index is case sensitive.** By contrast ADMIN_USERS has a lower-case CHECK (`20261108223153_Lot12_1_AdminAuth.cs:92-97`).
- FKs from USERS, all cascade: Coach (`:30-33`), Member (`:35-38`), AuthOtps (`:40-43`), AuthRefreshTokens (`:45-48`).

COACHES (`Coach.cs`, `CoachConfiguration.cs`)
- PK default uuid (`:14-16`). NOT NULL first name / last name (100), created date, is active, USER_ID (`:18-23`). Phone nullable (`:20`).
- Unique `IX_COACHES_USER_ID` (`AddAuthTables.cs:191-195`).

MEMBERS (`Member.cs:5-25`, `MemberConfiguration.cs`)
- PK default uuid (`:16-18`).
- NOT NULL without DB default: `MEMBER_FIRST_NAME` (100), `MEMBER_LAST_NAME` (100) (`:20-21`), `MEMBER_CREATED_DATE` (`:23`), `MEMBER_IS_ACTIVE` (`:24`), `MEMBER_HAS_ACTIVATED` (`:25`), `USER_ID` (`:47`).
- Nullable: phone, `MEMBER_ACTIVATION_DATE`, gender (MALE/FEMALE/OTHER, `:34-37`), height, birth date.
- Unique `IX_MEMBERS_USER_ID` (`AddAuthTables.cs:208-212`). MemberCoaches cascade (`:49-52`).

MEMBER_COACHES (`MemberCoach.cs`, `MemberCoachConfiguration.cs`)
- PK default uuid (`:14-16`). `MEMBER_ID`, `COACH_ID` NOT NULL (`:18-19`).
- `IS_PRIMARY` NOT NULL default false (`:20`); `STARTED_AT` NOT NULL default `now()` (`:21`). These are DB defaults only: an EF insert must set them explicitly or the CLR default is sent.
- `MEMBER_COACH_PRESENTIAL_ADDRESS` nullable text (`:23-25`).
- Unique (`MEMBER_ID`, `COACH_ID`) (`:27`, `AddAuthTables.cs:202-206`). Both FKs cascade (`:29-37`).

MEMBER_PARAMETERS (`MemberParameter.cs`, `MemberParameterConfiguration.cs`)
- All NOT NULL with defaults (language 'FR', notif messages true, reminders true, marketing false, reminder hours 24) (`:18-42`). Created/updated dates NOT NULL without default (`:44-50`).
- Unique `MEMBER_ID` (`:56`), cascade (`:58-61`).

COACH_PARAMETERS (`CoachParameter.cs`, `CoachParameterConfiguration.cs`)
- All NOT NULL with defaults (`:20-93`), dates without default (`:95-101`), unique `COACH_ID` (`:107`), cascade (`:109-112`).

### A2. Who creates USERS / MEMBERS / MEMBER_COACHES rows

- **No C# code creates a User, Member, Coach or MemberCoach row.** Grep for `new User`, `new Member`, `new MemberCoach`, `Users.Add`, `Members.Add`, `MemberCoaches.Add` returns nothing. Stated in code at `Mentora.Infrastructure/Services/MemberParameterService.cs:44-45` and in `docs/review/lot12/12.0/inventory.md:64`.
- Creation today is manual SQL only: `scripts/seed-test-members.sql:29-151` (ON CONFLICT on USER_EMAIL and on (MEMBER_ID, COACH_ID), sets `IS_PRIMARY=true`, `MEMBER_HAS_ACTIVATED=true`, `MEMBER_ACTIVATION_DATE=NULL` at `:77,123`), plus review fixtures (`docs/review/lot9/9.1/fixture-01-users.sql`, `docs/review/lot11/11.5/fixture.sql`, others).
- USER_ROLE casing is inconsistent across scripts: `'MEMBER'` (`scripts/seed-test-members.sql:35`), `'COACH'`/`'MEMBER'` (`docs/review/lot9/9.1/fixture-01-users.sql:13,15`), `'Coach'` (`docs/review/lot11/11.5/fixture.sql:17,22`, `docs/review/lot11/11.2b/fixture-coachb-commit.sql:7`).
- **No C# code reads `User.UserRole`.** The role is derived from which profile exists (`AuthService.cs:134-161`). The `UserRole` enum (`Mentora.Core/Enums/UserRole.cs:9-13`) is only used for `AuthRefreshToken.AuthRefreshTokenUserRole`, mapped MEMBER/COACH in `AuthRefreshTokenConfiguration.cs:41-53`.
- `MemberHasActivated` / `MemberActivationDate`: never written by C# code; only read and echoed by `MemberMeService.cs:89-90`. They gate nothing (login, lists, conversations).

### A3. IS_PRIMARY and several coaches per member

- `IsPrimary` is read in one place: ordering in `MemberMeService.cs:47` and exposure at `:63` in `MemberCoachGroupDto` (`Mentora.Core/DTOs/Member/MemberCoachGroupDto.cs:16`).
- No business rule depends on the primary coach, and nothing enforces a single primary (no filtered unique index, `MemberCoachConfiguration.cs:20,27`).
- Multi-coach is handled by scoping on the (member, coach) pair:
  - member routes carry `{coachId}`: cart (`MemberCartController.cs:10`), catalog (`MemberCatalogController.cs:10`), conversation (`MemberConversationController.cs:9`), messages (`MemberConversationMessagesController.cs:15`);
  - link checks on MEMBER_COACHES existence: `MemberCatalogService.cs:23-27`, `ConversationService.cs:32-35,44-47,437-440`, `CartService.cs:377`, `ProgramService.cs:35,64,595`, `SessionService.cs:494,733-736`, `VoucherGrantService.cs:24-27`, `ExerciseService.cs:290`;
  - vouchers grouped per coach ordered by `StartedAt` (`VoucherService.cs:44-65`); member agenda is not coach-filtered (`AgendaService.cs:56-80`).

### A4. Coach routes that list or read members

| Route | Controller / service | Notes |
|---|---|---|
| GET /api/v1/coach/me | `CoachMeController.cs:9,21`, `CoachMeService.cs:34-45` | members where `Member.MemberIsActive`, sorted by last/first name; DTO `CoachMemberSummaryDto(MemberId, FirstName, LastName, Email, Phone)` (`CoachMeService.cs:147-153`); throws if CoachParameter missing (`:29-31`) |
| GET /api/v1/coach/conversations | `CoachConversationsController.cs:9,20`, `ConversationService.cs:333-396` | starts from MEMBER_COACHES LEFT JOIN CONVERSATIONS: a member without conversation appears with null ConversationId, sorted last (`:335-346,392-394`); does **not** filter on `MemberIsActive`; returns a bare list, no envelope (`CoachConversationsController.cs:27`) |
| GET /api/v1/coach/members/{memberId}/conversation | `CoachConversationController.cs:9,22`, `ConversationService.cs:40-50,511-529` | lazy get-or-create, 23505 race handled |
| GET/PUT /api/v1/coach/members/{memberId}/parameters | `CoachMemberSettingsController.cs:10,20,32` | 404 when not linked (`CoachMemberSettingsService.cs:37-43`) |
| GET /api/v1/coach/agenda | `CoachAgendaController.cs:10,28` | built from sessions/programs only (`AgendaService.cs:19-52`) |

Effect for a newly attached member: visible in `/coach/me` if `MemberIsActive=true`, and in the inbox immediately (conversation row not required).

### A5. Startup seeders and rows needed after member creation

- Seeders after `MigrateAsync` (`Mentora.API/Program.cs:563`), called at `Program.cs:560-578`: CoachParameter, OfferProgram, MuscleGroup, ExerciseReference, LegacyExerciseCodePatch, Catalog, Exercise, ReferenceNormalizationPatch, SplitArmsPatch, ProgramTemplate, QuizQuestion, AdminUser.
- `CoachParameterSeeder.cs:16-35` and `OfferProgramSeeder.cs:25-46` are per coach and run at startup only. **No seeder creates per-member rows.**
- MemberParameter is get-or-create on GET (`MemberParameterService.cs:23-29`) and PUT (`:46-51`). Cart is lazy (`CartService.cs:82-97`), Conversation is lazy (A4).
- A new member needs only USERS + MEMBERS + MEMBER_COACHES.

### A6. CoachOnly route pattern to copy

- Attributes: `[ApiController]`, `[Route("api/v1/coach/...")]`, `[Authorize(Policy = "CoachOnly")]`, `[ApiExplorerSettings(GroupName = "coach")]`, `[Tags(...)]` (`CoachVoucherGrantsController.cs:9-14`, `CoachMemberSettingsController.cs:9-14`).
- Policies: `CoachOnly` at `Program.cs:245-246`, `MemberOnly` at `:248-250` (BOTH excluded). Swagger doc "coach" at `Program.cs:48-53`.
- coachId read with `Guid.Parse(User.FindFirst("coachId")!.Value)` (`CoachVoucherGrantsController.cs:35`).
- Envelope is an inline anonymous object, no ApiResponse type. 201 example: `CoachVoucherGrantsController.cs:37`.
- Validators: assembly scan `AddValidatorsFromAssemblyContaining<ProductRequestValidator>()` (`Program.cs:302`), invoked in the service with `ValidateAndThrowAsync` (`VoucherGrantService.cs:21`, `CoachMemberSettingsService.cs:24`). Validators sit in `Mentora.Core/Validators/<Area>/`. Auto ModelState validation is suppressed (`Program.cs:436-439`); `NullBodyGuardFilter` turns a null body into 400 (`Middleware/NullBodyGuardFilter.cs:27-29`). The only `.EmailAddress()` rule today: `Validators/Admin/AdminAuthValidators.cs:14,26`.
- Status mapping in `GlobalExceptionMiddleware.cs`: NotFound 404 (`:14-17`), Unauthorized 401 (`:19-22`), Forbidden 403 (`:24-27`), Conflict 409 + optional details (`:29-32`, `Mentora.Core/Exceptions/ConflictException.cs:3-6`), ValidationException 422 + errors (`:34-55`), InvalidOperation 400 (`:73-76`), other 500 (`:78-81`).
- Cross-coach access returns 404 rather than 403 (`CoachMemberSettingsService.cs:34-36`).

### A7. Routes under /api/v1/coach/members

- `{memberId}/conversation` GET (`CoachConversationController.cs:22`)
- `{memberId}/conversation/messages` GET, POST, POST audio, PATCH read (`CoachConversationMessagesController.cs:31,50,83,116`)
- `{memberId}/parameters` GET, PUT (`CoachMemberSettingsController.cs:20,32`)
- `{memberId}/training-programs` GET, POST (`CoachProgramsController.cs:26,63`)
- `{memberId}/voucher-grants` POST (`CoachVoucherGrantsController.cs:25`)
- **No collection route** (`GET` or `POST /api/v1/coach/members`) exists. A POST on the collection path does not collide.

---

## B. App authentication (OTP)

### B1. Request and verify flow

- Request: `POST api/v1/auth/otp/request` (`Mentora.API/Controllers/Auth/AuthController.cs:15-27`), inline try/catch turning `InvalidOperationException` into 400. DTO `RequestOtpRequest(string Email, bool? IsCoach)` with no validator (`Mentora.Core/DTOs/Auth/RequestOtpRequest.cs:3`).
- `AuthService.RequestOtpAsync` (`Mentora.Infrastructure/Services/AuthService.cs`):
  1. Lookup `u.UserEmail == email && u.UserIsEnabled` (`:38`); none gives 400 "User not found or account is disabled." (`:39`). **No user is ever created; the 400 reveals account existence.**
  2. `ResolveEffectiveIsCoach` (`:41,134-161`): missing profile for the sent flag gives 400 (`:140,142`); both profiles and no flag gives `ConflictException` 409 (`:146-153`).
  3. Previous live OTPs set `AuthOtpIsUsed = true` (`:44-48`).
  4. Code from `RandomNumberGenerator.GetInt32(100_000, 1_000_000)` (`:334-335`), BCrypt hash stored (`:50-59`).
  5. **`Console.WriteLine` of the "[OTP]" line with email and plain code at `AuthService.cs:61`, no environment guard.** `AuthService` has no `IHostEnvironment` (`:20-25`).
  6. Mail sent, result discarded with `_ =` (`:63-75`).
- **Email normalization: none** (no Trim, no ToLower; exact `==` at `:38` and `:97`). Admin side normalizes with `Trim().ToLowerInvariant()` (`Services/Admin/AdminAuthService.cs:209`).
- `OtpLifetimeMinutes` is a const = 10 (`AuthService.cs:28`), not an option.
- Verify: `POST api/v1/auth/otp/verify` (`AuthController.cs:29-41`), DTO without code format validation (`VerifyOtpRequest.cs:3`). Steps: same lookup 400 (`:94-98`), load live OTPs newest first (`:101-104`), BCrypt.Verify each, miss gives 400 "Invalid or expired OTP." (`:106-107`), role resolution (`:109-110`), mark used + refresh token via tracked SaveChanges (`:112-115`, not atomic), `AuthResponse` (`:117-124`). `userType` claim built at `:258-266`; verify always emits COACH or MEMBER.
- `logger.LogError` "Ambiguous role" logs the email (`:148-150`).

### B2. AUTH_OTPS

- Columns: `AUTH_OTP_ID`, `AUTH_OTP_CODE_HASH` (255), `AUTH_OTP_EXPIRATION_DATE`, `AUTH_OTP_IS_USED`, `AUTH_OTP_CREATED_DATE`, `USER_ID` (`Mentora.Core/Entities/AuthOtp.cs:5-11`, `AuthOtpConfiguration.cs:11-22`). Cascade from USERS (`UserConfiguration.cs:40-43`).
- **No attempt counter.** A wrong code changes nothing in DB (`AuthService.cs:100-107`).
- Invalidation on new request: IsUsed flag, no delete (`:44-48`).
- Model to copy: admin OTP has `AdminOtpFailedAttempts` (`Mentora.Core/Entities/AdminOtp.cs:11`, `AdminOtpConfiguration.cs:26,29-30`, CHECK created in migration SQL), `OtpMaxAttempts = 5` range 1..10 (`AdminAuthOptions.cs:19,58-59`), candidates filtered on attempts < max (`AdminAuthService.cs:98-106`), atomic `ExecuteUpdateAsync` increment then burn at max (`:109-127`), dummy BCrypt for timing (`:36,91-95,124`), conditional atomic consume (`:130-134`), always-200 request (`:49-54`), FluentValidation code `^[0-9]{6}$` (`AdminAuthValidators.cs:7-33`).

### B3. Rate limiter

- Registration `AddRateLimiter` with `AddQuizPolicy`, `AddAdminAuthPolicy`, `AddVoiceUploadPolicy` (`Program.cs:382-387`).
- `QuizBasic` (`Authorization/QuizRateLimiting.cs:11,23-38`): no limiter when an Authorization header is present; else fixed window per `RemoteIpAddress`, default 30 per 10 min, queue 0 (`Mentora.Core/Options/QuizOptions.cs:15-17`).
- `AdminAuth` (`Authorization/AdminAuthDefaults.cs:13`, `AdminAuthRateLimiting.cs:18-30`): fixed window per IP, default 30 per 15 min, queue 0 (`AdminAuthOptions.cs:20-21,60-63`).
- `VoiceUpload` (`Authorization/VoiceUploadRateLimiting.cs:13,28-39`): per NameIdentifier, 30 per 10 min (`Mentora.Core/Messaging/VoiceMessageRules.cs:32,35`).
- 429: `RejectionStatusCode` and `OnRejected` are set once inside `AddQuizPolicy` and shared by all policies (`QuizRateLimiting.cs:21,40-54`). Sets `Retry-After` when available (`:43-44`); body `{success:false,data:null,error:"Too many requests.",statusCode:429}` (`:47-53`).
- `[EnableRateLimiting]` used at `AdminAuthController.cs:18`, `QuizBasicController.cs:18`, `CoachConversationMessagesController.cs:87`, `MemberConversationMessagesController.cs:87`.
- **`/api/v1/auth` is not rate limited today** (no attribute at `AuthController.cs:9-13`, no global limiter, no nginx `limit_req`).
- Pipeline: `UseForwardedHeaders` (`Program.cs:582`), exception middleware (`:585`), CORS (`:598`), `UseAuthentication` (`:599`), `UseAuthorization` (`:600`), `UseRateLimiter` (`:601`).
- ForwardedHeaders: XForwardedFor + Proto, KnownNetworks/KnownProxies cleared (`Program.cs:482-487`). nginx sets X-Forwarded-For (`nginx/conf.d/mentora.conf:64-66,81-83`). The API container has no published port (`docs/review/lot12/12.5a/prod-runbook.md:125`), which is what makes the forwarded IP trustworthy.

### B4. Refresh token

- `RefreshTokenAsync`: malformed token gives 400 (`AuthService.cs:165,313-317`); record must be not revoked, not expired and `User.UserIsEnabled` (`:167-175`); BCrypt check (`:177-178`); old revoked (`:181-182`); role kept from original token, missing profile gives 400 (`:190-199`).
- A new member account must have: USERS row with `UserIsEnabled = true` (`User.cs:11`), an email matching the typed string exactly, a Member row, and no Coach row (else 409 without `isCoach`).
- Not checked by auth: `MemberIsActive` (`Member.cs:12`), `UserRole`, `UserDeletionRequestedDate`.

---

## C. Email

### C1. Senders, options, routing

- `IEmailSender.SendAsync(EmailMessage, ct)` returns `Task<bool>`, must not throw (`Mentora.Core/Interfaces/IEmailSender.cs:3-12`). `EmailMessage(To, Bcc, Subject, HtmlBody, TextBody)` + `ForRecipient` (`EmailMessage.cs:7-15`).
- DI `Program.cs:404-425`: options bound with `ValidateOnStart` + singleton validators; `IEmailSender` scoped factory picks Smtp or Logging and always wraps it in `RoutingEmailSender`.
- `EmailOptions` (`Mentora.Core/Options/EmailOptions.cs:7-62`): provider default Logging; Smtp requires Host, Port 1..65535, valid FromAddress, Username, Password (`:28-46`); 2 attempts, 2 s delay.
- `EmailRoutingOptions` (`EmailRoutingOptions.cs`): Production only when Mode is "Production" (trim, case-insensitive, `:12-19`); TestRecipients required and valid even in Production (`:33-38`); Production requires ProductionBcc (`:41-42`); no duplicates (`:46-58`).
- `RoutingEmailSender.cs`: TEST mode rewrites To to the test list, empties Bcc, prefixes the subject with the original recipients (`:28-36`); Production keeps To and adds ProductionBcc, deduplicated (`:39-47`). SMTP puts Bcc in the envelope only (`SmtpEmailSender.cs:75-78`).
- Log lines: `[EMAIL] mode/to/bcc/subject` (`RoutingEmailSender.cs:20-22`); SMTP logs To and Subject (`SmtpEmailSender.cs:40-42,51-53,63-65`); **`LoggingEmailSender` logs the TextBody, which contains the OTP** (`LoggingEmailSender.cs:10-12`).
- Startup: both options read with `.Value` before migrations, count-only info line (`Program.cs:493-503`).

### C2. OTP mail body and adding a mail type

- Subject "Votre code Mentora" or "{firstName}, votre code Mentora" (`AuthService.cs:63-68`). First name from Coach or Member per resolved role, sanitized by `SanitizeFirstName` (trim, control chars removed, max 50) (`:82-90`).
- HTML body is an interpolated raw string with code and lifetime only, no HTML encoding (`:344-360`); text body `:362-370`.
- Only two mail senders exist: `AuthService.cs:69` and `AdminAuthService.cs:74` (admin bodies `:293-319`).
- Adding "member added by coach": inject `IEmailSender`, build subject + HTML + text in the service, call `EmailMessage.ForRecipient`. Routing, Bcc and test mode apply automatically. No template helper exists; coach and member names must be HTML encoded explicitly and sanitized for the subject.

### C3. Startup validation pattern for a new option (install link)

- Pattern A (Email, EmailRouting, AdminAuth, Mux, Audio, Cors): POCO in `Mentora.Core/Options` with `SectionName` and `string? Validate(...)` naming keys never values; `IValidateOptions<T>` adapter (environment-aware where needed); `AddOptions<T>().Bind(...).ValidateOnStart()`; explicit `.Value` read before `MigrateAsync` (`Program.cs:493-557` vs `:563`) with a count-only tag line.
- Closest models for a URL option: `MuxOptions.Validate(bool isDevelopment)` (`Mentora.Core/Options/MuxOptions.cs:60-122`) with `Services/Mux/MuxOptionsValidator.cs:12-18`; URL syntax check `CorsOptions.CheckOriginSyntax` (`CorsOptions.cs:107-123`).
- Pattern B (LiveKit `Program.cs:361-369`, Quiz `:373-379`) validates only at host start, after migrations. Pattern C `StartupGuards` reads env vars before build (`Startup/StartupGuards.cs:15-39`).
- `RefreshTokenPurgeOptions` has no validator (`Program.cs:398-399`).

---

## D. Exercises sent to the apps

### D1. Routes and DTOs

- `ExerciseVideoMapping.ForApps` (`Services/Mux/ExerciseVideoMapping.cs:15-22`, READY + playback id else null) has two call sites: `ExerciseService.cs:447` and `ProgramService.cs:1032`.
- Only two DTOs carry `video`, both as **last positional parameter**: `ExerciseResponse` (`Mentora.Core/DTOs/Exercise/ExerciseResponse.cs:68`) and `ProgramExerciseResponse` (`Mentora.Core/DTOs/Program/ProgramExerciseResponse.cs:36`, doc comment "last key video" at `:9`). Video DTO `ExerciseVideoResponse` (`ExerciseResponse.cs:91-95`).

| Route | DTO | video |
|---|---|---|
| GET coach/exercises (`CoachExercisesController.cs:36`) | List of ExerciseResponse (`ExerciseService.cs:18-70`) | yes |
| GET coach/exercises/{id} (`:66`) | ExerciseResponse | yes |
| POST coach/exercises (`:82`) | ExerciseResponse | yes, always null (`ExerciseService.cs:82-107`) |
| PUT coach/exercises/{id} (`:104`) | ExerciseResponse | yes |
| GET member/exercises (`MemberExercisesController.cs:31`) | ExerciseListResponseDto / ExerciseSummaryDto (`ExerciseListResponseDto.cs:11-26`) | **no** |
| GET member/exercises/{id} (`:55`) | ExerciseResponse | yes |
| GET coach/training-programs/{id} (`CoachProgramsController.cs:42`) | ProgramResponse, nested ProgramExerciseResponse | yes |
| POST coach/members/{memberId}/training-programs (`:63`) | AssignProgramResponse (nested) | yes |
| PUT coach/training-programs/{id} (`:97`) | ProgramResponse | yes |
| PUT coach/program-sessions/{id}/completion (`CoachProgramSessionsController.cs:60`) | ProgramSessionResponse | yes |
| PUT member/program-sessions/{id}/completion (`MemberProgramSessionsController.cs:29`) | ProgramSessionResponse | yes |
| GET member/programs/current and /{id} (`MemberProgramsController.cs:17,34`) | ProgramResponse | yes |
| coach/program-templates (`CoachProgramTemplatesController.cs:28,51,67,89`) | ProgramTemplateResponse, stored JSON body (`ProgramTemplateService.cs:167-178`, `ProgramTemplateExercise.cs:3-27`) | **no** (exerciseId only) |
| coach/session-templates incl. sessionNode (`CoachSessionTemplatesController.cs:56,74,95,133`) | SessionTemplateResponse (`SessionTemplateService.cs:302-334`) | **no** (exerciseId only) |
| agenda coach/member | Agenda DTOs | no exercise data |

- Conclusion: appending `photos` after `video` in the two records covers every app response that carries video today. Templates, session templates and the member list carry no media and are out of scope as written.
- Legacy fields still output: `ExerciseVideoUrl`, `ExerciseImageUrl` (`ExerciseResponse.cs:42-43`, `ProgramExerciseResponse.cs:17-18`).

### D2. How the video is loaded

- Exercise detail/list: `WithDetail` adds `.Include(e => e.Video)` (`ExerciseService.cs:293-299`); coach list uses `AsSplitQuery` (`:57,65`); detail is a single query (`:74,252`).
- Program tree `BuildResponseAsync` (`ProgramService.cs:890-926`): 4 flat queries (blocks, sessions, circuits, exercises) (`:894-897`), one exercise query with `Include(Video)` into a dictionary (`:901-903`), optional date query (`:914-918`), last performed (`:922`). No N+1.
- Single session `BuildSessionResponseAsync` (`:737-788`), video in the exercise query (`:753-756`).
- ExerciseVideo is 1:0..1, cascade, unique per exercise (`ExerciseVideoConfiguration.cs:91-93,103-106`).
- Adding a collection Include (photos) to these non-split queries multiplies rows; a separate dictionary query keeps the query count constant.

---

## E. Admin exercise screen and API

### E1. Admin API

- Both controllers: `[Route("api/v1/admin")]`, `[Authorize(Policy = AdminAuthDefaults.Policy)]` (`AdminExercisesController.cs:16-17`, `AdminExerciseVideosController.cs:16-17`). Admin still-enabled check on every write (`AdminExerciseService.cs:614-619`, `AdminExerciseVideoService.cs:330-335`).
- AdminExercisesController: GET exercise-references (`:27`), GET exercises (`:52`), GET exercises/{id} (`:78`), POST (`:94`), PUT (`:112`), PUT activation (`:132`). No DELETE, no Duplicate route.
- AdminExerciseVideosController: POST video-uploads 201 (`:29`), POST video/sync (`:48`), DELETE video (`:66`).
- Locking: create uses `pg_advisory_xact_lock(7122002)` for code allocation (`AdminExerciseService.cs:32,429-447`); update/activation take no lock (`:490-609`); video writes use `MuxVideoRules.LockExerciseAsync` = `SELECT ... WHERE EXERCISE_COACH_ID IS NULL FOR UPDATE` (`Services/Mux/MuxVideoRules.cs:34-44`), callers `AdminExerciseVideoService.cs:46,167,210`, `MuxWebhookService.cs:185`.
- Audit: one row in the same SaveChanges. `NewAudit` (`AdminExerciseService.cs:621-631`; `AdminExerciseVideoService.cs:337-347`). Created (`:468-473`), Updated diff (`:513-517,545`, none if no change `:510-511`), (De)Activated (`:598-605`), VideoUploadCreated (`AdminExerciseVideoService.cs:93-99`), VideoRemoved (`:222-224`); sync writes none.
- Cleanup after commit: `BestEffortAsync` after `tx.CommitAsync` (`AdminExerciseVideoService.cs:115-116,231-232`), retries `:296-314`, never throws; rollback cancels the upload (`:104-108`).
- Duplicate is client-side only (E3). Admin never hard-deletes; deactivation of an exercise in use gives 409 EXERCISE_IN_USE (`AdminExerciseService.cs:577-591,359-384`).

### E2. ADMIN_AUDIT_LOG

- Entity `Mentora.Core/Entities/AdminAuditLog.cs:9-24`.
- Enums in `Mentora.Core/Enums/AdminAuditAction.cs`: `AdminAuditAction` (`:4-12`) = ExerciseCreated, ExerciseUpdated, ExerciseActivated, ExerciseDeactivated, ExerciseVideoUploadCreated, ExerciseVideoRemoved; `AdminAuditEntityType` (`:15-18`) = Exercise.
- String mappings are in `AdminAuditMappings` in the same file (`:21-56`), if-chains. **Not** in `EnumMappings.cs` (which holds `ExerciseVideoStatusMapping`, `EnumMappings.cs:441-463`).
- Configuration: CHECKs created in migrations via `Sql()` (`AdminAuditLogConfiguration.cs:9-12`); varchar(40) (`:26-40`); jsonb changes (`:44-47`).
- Current `CK_ADMIN_AUDIT_LOG_ACTION`, last set by `20261110104601_Lot12_3a_MuxVideos.cs:105-110`: EXERCISE_CREATED, EXERCISE_UPDATED, EXERCISE_ACTIVATED, EXERCISE_DEACTIVATED, EXERCISE_VIDEO_UPLOAD_CREATED, EXERCISE_VIDEO_REMOVED. Down guard at `:128-131`.
- Current `CK_ADMIN_AUDIT_LOG_ENTITY_TYPE`, set only by `20261109160428_Lot12_2b_AdminAuditLog.cs:62-63`: EXERCISE.

### E3. Mentora.Admin

- `Pages/Exercices/Form.razor`: routes (`:1-2`), `NavigationLock` on IsDirty (`:13`), Dupliquer button (`:42`), save (`:44`), copy banner (`:52-61`, text says video not copied `:55`), activation bar (`:91-115`), cards: essentials (`:117-178`), **video card `id="video-block"` (`:180-280`)**, muscles (`:282-298`), movement (`:300-325`), instructions (`:327-379`), dialogs (`:383-406`).
- Video card: create mode shows "save first" (`:182-185`), saves immediately outside the form save (`:188`), hidden file input (`:195-196`), Mux thumbnail (`:251-252`).
- Load `LoadAsync` (`:563-631`), video kept outside dirty state (`:626-628`). Save `SaveAsync` (`:974-1038`), JSON request built at `:814-840`, error mapping (`:1040-1080`). `Snapshot()` (`:793-801`). Navigation guard (`:922-941`). Sync polling (`:1435-1493`).
- Duplicate flow: navigates to `exercices/nouveau?depuis={id}` (`:646-650`), `ApplyCopy` (`:655-706`) copies fields, not video (comment `:652-654`).
- API client: base URL from `wwwroot/appsettings*.json` `ApiBaseUrl` (`Mentora.Admin/Program.cs:11-20`, `AppConfig.cs:6-17`); Bearer in `ApiTransport.cs:25-26`; one refresh + retry on 401 (`ApiClient.cs:15-34`); **JSON only, no multipart** (`ApiTransport.cs:23-24`). `ExercisesApi` root `api/v1/admin/` (`:9`).
- Video upload goes browser to Mux directly, chunked XHR (`wwwroot/js/admin.js:48-186`), called at `Form.razor:1352`.
- `admin.js` exposes `window.mentoraAdmin` (`:188-234`): theme, dialog, scroll/focus, refresh token helpers, `runExclusiveRefresh`, file picker, video upload/abort. **No drag and drop anywhere** in Mentora.Admin.
- `wwwroot/index.html`: inline theme script (`:14-28`), `js/admin.js` (`:29`), blazor script (`:42`).

### E4. Admin nginx CSP

- `nginx/conf.d/mentora-admin.conf.example:81` (server level only, rule at `:10-12`):
  - `script-src 'self' 'wasm-unsafe-eval' 'sha256-oW+3IccTLsL4xJ3s2rKPxyyczlz2dfeyKhTyG6r3Zkc='` (hash of the inline theme script `index.html:14-28`, recompute procedure `:14-21`)
  - `img-src 'self' data: https://image.mux.com`
  - `connect-src 'self' https://api.mentorapp.fr https://*.mux.com`
  - no `media-src`.
- Photos shown in the admin need their host added to `img-src`.

---

## F. File storage and image processing

### F1. Lot 13 audio pipeline

- `AudioOptions` (`Mentora.Core/Options/AudioOptions.cs`): env vars only (`:4-5`), section "Audio" (`:12`), FfmpegPath/FfprobePath/StoragePath/MaxConcurrentDecodes 1..4/DecodeTimeoutSeconds 1..60 (`:15-39`); `Validate(isDevelopment)` requires absolute StoragePath outside Development (`:42-57`).
- `AudioOptionsValidator` (`Services/Audio/AudioOptionsValidator.cs:12-19`).
- `AudioAvailability` (`Services/Audio/AudioAvailability.cs`): `-version` probe 5 s (`:17-18,70-79`), storage write test file (`:84-109`), `IsAvailable` (`:27`).
- `AudioProcessRunner` (`Services/Audio/AudioProcessRunner.cs`): no shell, `ArgumentList` (`:51-57`), capped stdout (`:77-107`), stderr 4096 chars (`:121-141`), kill tree on cancel/finally (`:73,115-118,143-154`); `internal` (`:34`), reusable as is.
- `DiskVoiceMessageStorage` (`Services/Audio/DiskVoiceMessageStorage.cs`): name `{conversationId}/{messageId}.aac` (`:25-26`), `.incoming/<guid>.upload` (`:19,29-34`), commit `File.Move(..., overwrite: false)` (`:37-46`), `EnsureInside` path escape check (`:93-102`), depends on `IAudioAvailability.StorageOk` (`:86-91`). Voice-specific; not reusable for photos without changes.
- `FfmpegVoiceAudioProcessor` (`Services/Audio/FfmpegVoiceAudioProcessor.cs`): magic bytes on 12 bytes (`:27,149-170`), semaphore (`:52,67-68`), linked timeout (`:71-82`), forced demuxer (`:94`), ffprobe/ffmpeg args (`:99-100,121-122`), `[AUDIO] analysis` log (`:253-271`).
- Startup `[AUDIO]` line `Program.cs:537-540`, probe `:536`, fails startup outside Development (`:541-542`), before `MigrateAsync` (`:563`). DI `Program.cs:286-292`. `AudioNotConfiguredException` to 503 (`GlobalExceptionMiddleware.cs:71`).
- Audio is served by the API, not nginx: `ConversationAudioController.cs:12-13,47-73` (private cache, range). The API has no `UseStaticFiles`.
- Mount: no VOLUME in `Mentora.API/Dockerfile:1-25`; `docker-compose.local.yml:1-15` has postgres only. VPS: `~/mentora/media-demo` and `~/mentora/media-prod` mounted as `/app/media` with `Audio__StoragePath=/app/media` (`docs/review/lot12/12.5a/prod-runbook.md:672-722`, applied in `docs/review/lot13/vps/vps-evidence.md:30-50`). Container runs as root (`prod-runbook.md:681`). nginx container does not mount media (`prod-runbook.md:363-366`).

### F2. Dockerfile and ffmpeg

- Base `mcr.microsoft.com/dotnet/aspnet:8.0` (`Mentora.API/Dockerfile:1`), Debian 12 bookworm (`docs/review/lot13/13.3/runtime-evidence.md:82`).
- `apt-get install -y --no-install-recommends ffmpeg` (`Mentora.API/Dockerfile:2`); observed version 5.1.9-0+deb12u1 (`docs/review/lot13/13.3/runtime-evidence.md:87`).
- Debian ffmpeg normally ships mjpeg and png decoders, mjpeg encoder and WebP decoding, but nothing in the repo shows `-decoders`/`-encoders` output for this image. **JPEG/PNG/WebP decode and JPEG encode: to be proven in 14.4.** Metadata stripping (`-map_metadata -1`) and EXIF orientation handling also to be proven.

### F3. nginx

- `client_max_body_size 8m` at `nginx/conf.d/mentora.conf:40` and `nginx/conf.d/mentora-prod.conf.example:36`; not set in `nginx/nginx.conf` (1 MB default elsewhere). VPS changed from 25M to 8m (`docs/review/lot13/vps/vps-evidence.md:54-55`).
- Static locations: ACME `root /var/www/certbot` (`mentora.conf:20-23`, `mentora-prod.conf.example:16-19`), admin SPA `root /var/www/mentora-admin` (`mentora-admin.conf.example:63-64,84-86`). No `alias` anywhere.
- Proxy: `/hubs/` and `/` to `api:8080` / `api-prod:8080` (`mentora.conf:58-85`, `mentora-prod.conf.example:53-80`). Only HSTS, no Cache-Control in API confs (`mentora.conf:55`).
- Photos (description only): a dedicated `location` with `alias` to a read-only bind mount of a photo-only host folder into the nginx container (same approach as `./admin-www:/var/www/mentora-admin:ro`, `prod-runbook.md:366`), with long cache headers and `nosniff`, and no directory listing. The mount must not include the voice files.
- An 8m nginx cap means an 8 MB photo plus multipart overhead is rejected by nginx with an HTML 413 without CORS headers (`prod-runbook.md:735`).

### F4. Multipart limits in the API

- No global Kestrel `MaxRequestBodySize` or `FormOptions` (defaults: about 30 MB Kestrel, 128 MB multipart).
- Lot 13 per-action attributes: `[Consumes("multipart/form-data")]`, `[RequestSizeLimit(VoiceMessageRules.MaxRequestBytes)]`, `[RequestFormLimits(MultipartBodyLengthLimit = ...)]`, `[EnableRateLimiting(...)]` (`MemberConversationMessagesController.cs:83-87`, `CoachConversationMessagesController.cs:83-87`).
- `MaxSizeBytes` 5 MB (`VoiceMessageRules.cs:10`), `MaxRequestBytes` 8 MB (`:29`): 422 between, 413 above (`:24-28`).
- 413 detection workaround `IsRequestBodyTooLargeAsync` (`MemberConversationMessagesController.cs:147-173`; coach `:151-173`).
- Validator pattern `SendAudioMessageRequestValidator.cs:11,25-33`.

---

## G. Push notifications readiness

### G1. Devices

- `UserDevice` (`Mentora.Core/Entities/UserDevice.cs:11-21`): Id, Token, Platform, CreatedDate, LastSeenDate, UserId. Table `USER_DEVICES`, token unique max 512, platform IOS/ANDROID, cascade (`UserDeviceConfiguration.cs:13-69`; migration `20260730194501_AddLot5Parameters.cs:161,199`).
- Routes `api/v1/devices`, plain `[Authorize]`, group "mobile" (`Controllers/Auth/UserDevicesController.cs:11-13`): POST register (`:24-30`), DELETE by body (`:37-43`). Logout can remove a token (`AuthController.cs:52-69`).
- `UserDeviceService.cs`: upsert on token, 23505 race handled, reassignment to another user (`:24-62`), delete own token only (`:67-74`).
- Who calls them: the mobile apps (not in this repo).

### G2. Notification code

- **None.** No Firebase, FCM, APNs, notification service, stub or TODO in code or csproj (only comments, e.g. `UserDeviceService.cs:57`).
- Preference flags exist but are unused for sending: `CoachParameter.cs:11-14`, `MemberParameter.cs:7-9`.
- No invalid-token cleanup, no locale or app flavor stored.

---

## H. Migrations and conventions

### H1. Migrations (chronological)

InitialCreate, AddAuthTables, AddLot2_Foundation, AddLot2_2_Catalog, AddLot2_3_Purchase, AddLot2_3_1b_PackDiscount, AddLot2_4_Sessions, AddConversationsAndMessages, AddLot5Parameters, Lot6_1_Exercises, Lot6_2_ProgramTemplates, Lot6_3_Programs, Lot6_4_GroupSessions, Lot6_5_Agenda, Lot9_PurchaseOrigin, Lot9_ExerciseCatalog, Lot10_Quiz, Lot11_2_ProgramTreeFields, Lot11_3_CoachSessionPreferences, Lot11_5_SessionTemplates, Lot11_7_RestFieldsJsonKeys, Lot12_1_AdminAuth, Lot12_2a_ExerciseReferences, Lot12_2b_AdminAuditLog, Lot12_3a_MuxVideos, **Lot13_2_VoiceMessages (20261117234001, last)**. Snapshot `MentoraDbContextModelSnapshot.cs`. Auto-applied at startup (`Program.cs:563`).

### H2. Rules relevant to lot 14

- No repository layer (`CLAUDE.md`, "No repository layer").
- Entity-prefixed property names, UPPER_SNAKE tables/columns.
- Envelope `{ success, data, error, statusCode }`; Lot 1 auth controllers still use inline try/catch, not retrofitted (CLAUDE.md, exception mapping section).
- Email routing: env vars only; any mode other than Production is TEST; TestRecipients required even in Production; Production requires ProductionBcc; api-demo stays in TEST until the Alpha (CLAUDE.md, Email routing).
- Enum mappings as if-chains (`docs/review/lot12/12.3.0/inventory.md:441`).
- No em/en dashes in added lines (`docs/review/lot13/review-wave1.md:111`).
- **No test project** (4 projects in `Mentora.sln:6-12`); proof is runtime evidence under `docs/review/lotNN/`.

### H3. Contradictions or size increases versus the lot 14 scope

1. 14.1 is the **first code path that creates USERS/MEMBERS/MEMBER_COACHES** (A2). All NOT NULL columns without default must be set, USER_ROLE casing must be decided.
2. 14.1 "member types the email, receives the code": OTP lookup is exact and case sensitive (`AuthService.cs:38,97`) and the unique index is case sensitive (`UserConfiguration.cs:21`). Without normalization, a casing difference blocks login or allows duplicates.
3. 14.1 "is attached": nothing hooks first login today; `MemberHasActivated`/`MemberActivationDate` are never written (A2). The link exists from creation; "attached" can only mean setting activation on first verify.
4. 14.1 existing email: if the email belongs to a coach, adding a Member profile makes OTP return 409 without `isCoach` (`AuthService.cs:146-153`) and `MemberOnly` rejects BOTH tokens (`Program.cs:248-250`).
5. 14.2: no counter column (migration needed), verify consume is not atomic (`AuthService.cs:112-115`), unknown email answers 400 and bypasses any per-OTP counter (`:39,98`).
6. 14.3: removing the "[OTP]" line breaks manual review scripts that read it (`docs/review/lot7/run-auth-tests.ps1:13,140,148,157`, `docs/review/lot8/get-tokens.ps1:52,59`, `docs/review/lot9/9.1/get-tokens.ps1:53,60`, `docs/review/lot9/9.4/get-tokens.ps1:31`). `LoggingEmailSender` also logs the code (`LoggingEmailSender.cs:10-12`). In Production mode the ProductionBcc receives every live OTP mail (`RoutingEmailSender.cs:39-47`).
7. 14.4: voice storage class is voice-specific (F1); nginx does not mount media (F3); serving publicly needs a photo-only folder and a new nginx mount and location; admin CSP `img-src` change (E4).
8. 14.6: admin client has no multipart support (`ApiTransport.cs:23-24`) and no drag and drop exists (E3); both are new.
9. A legacy `EXERCISE_IMAGE_URL` (free URL, coach-set) already exists and is returned next to the new `photos` field (`Exercise.cs:17`, `ExerciseResponse.cs:43`, `ProgramExerciseResponse.cs:18`).
10. Mentora exercise = `ExerciseCoachId IS NULL` (`Exercise.cs:9-10`); admin and video services filter on it (`AdminExerciseService.cs:121`, `MuxVideoRules.cs:34-44`). Coach-owned exercises get no photos (consistent with scope).

---

## Gaps and risks

1. Email case sensitivity: exact match in OTP lookup and case-sensitive unique index (`AuthService.cs:38,97`, `UserConfiguration.cs:21`).
2. No C# code creates accounts today; 14.1 sets NOT NULL fields by hand (`MemberConfiguration.cs:20-25`, `UserConfiguration.cs:18-26`).
3. USER_ROLE values inconsistent in existing data ('MEMBER', 'COACH', 'Coach') (`scripts/seed-test-members.sql:35`, `docs/review/lot11/11.5/fixture.sql:17`).
4. Adding a member profile to a coach email produces BOTH, blocked by MemberOnly (`Program.cs:248-250`, `AuthService.cs:146-153`).
5. `IsPrimary` has no uniqueness rule; a new link must decide true or false (`MemberCoachConfiguration.cs:20`).
6. `/coach/me` hides inactive members, inbox does not (`CoachMeService.cs:35`, `ConversationService.cs:340-341`).
7. Concurrent adds raise 23505 on USER_EMAIL or (MEMBER_ID, COACH_ID); repo pattern catches it (`ConversationService.cs:464,524`).
8. Account enumeration: OTP request answers 400 for unknown email (`AuthService.cs:39`); a coach add route must not add a new oracle beyond the coach scope.
9. `/api/v1/auth` has no rate limit; `OnRejected` lives inside `AddQuizPolicy` (`QuizRateLimiting.cs:21,40-54`).
10. Per-IP limit relies on X-Forwarded-For with cleared known proxies; only safe while the API port stays unexposed (`Program.cs:482-487`).
11. Class-level limiter on AuthController would also cover refresh and logout (`AuthController.cs:9-13,64`).
12. No OTP attempt counter, non-atomic consume (`AuthOtpConfiguration.cs:18-22`, `AuthService.cs:112-115`).
13. "[OTP]" console line unguarded, no IHostEnvironment in AuthService (`AuthService.cs:20-25,61`).
14. LoggingEmailSender writes the OTP body to logs if Email__Provider stays Logging (`LoggingEmailSender.cs:10-12`).
15. ProductionBcc receives live login codes in Production mode (`RoutingEmailSender.cs:39-47`).
16. Mail send result discarded; SMTP failure still returns 200 (`AuthService.cs:69`).
17. Prod compose excerpt in the runbook shows no EmailRouting__Mode/ProductionBcc/Smtp keys (`docs/review/lot12/12.5a/prod-runbook.md:107-117`); the API refuses to start if Production is set without them (`EmailRoutingOptions.cs:33-43`).
18. ffmpeg image codecs and metadata stripping unproven in the image (`Mentora.API/Dockerfile:2`).
19. Photo files must live outside the voice tree or nginx must mount only a photo subfolder; voice files are private (`ConversationAudioController.cs:12-13`).
20. nginx 8m cap plus multipart overhead: an 8 MB photo can be refused by nginx with HTML 413 (`mentora.conf:40`).
21. Collection Include of photos in program tree queries multiplies rows (`ProgramService.cs:901-903,753-756`).
22. Audit CHECK must be dropped and recreated with all 6 existing values plus new ones (`20261110104601_Lot12_3a_MuxVideos.cs:105-110`).
23. Admin client JSON-only, no multipart, no drag and drop (`ApiTransport.cs:23-24`, `admin.js:188-234`).
24. Duplicate banner and comment mention video only (`Form.razor:55,652-654`).
25. Admin CSP `img-src` lacks the photo host (`mentora-admin.conf.example:81`).
26. Files on disk are not removed by DB cascades; post-commit delete needed (pattern `AdminExerciseVideoService.cs:115-116`).
27. Legacy `ExerciseImageUrl` returned next to `photos` may confuse the apps (`ExerciseResponse.cs:43`).
28. `scripts/seed-test-members.sql` is committed with real personal addresses and a host reference (`:12,34-61`).
29. No push code at all; device table has token and platform only (`UserDeviceConfiguration.cs:13-69`).
30. No automated tests; each sub-lot needs runtime evidence (`Mentora.sln:6-12`).

---

## Questions for François

1. **Existing email in 14.1.** Options: (a) email exists as a member of another coach: add the MEMBER_COACHES link only (and update nothing else); (b) email exists as a coach: refuse with 409, or create a Member profile on that user (BOTH). Stake: BOTH tokens are rejected by MemberOnly and OTP returns 409 without `isCoach` (`Program.cs:248-250`, `AuthService.cs:146-153`); allowing it enlarges 14.1 and touches the apps.
2. **Email normalization.** Options: (a) store new emails as `Trim().ToLowerInvariant()` and make the OTP lookup case-insensitive for everyone; (b) normalize new accounts only and keep exact matching. Stake: (a) needs a check that no two existing USERS differ only by case before switching, and possibly a lower() unique index; (b) leaves login failures for any casing difference.
3. **IS_PRIMARY and activation for the new link.** Options: (a) IS_PRIMARY = true only if the member has no other coach, `MEMBER_HAS_ACTIVATED=false` until first OTP verify then set with the date; (b) IS_PRIMARY = false always, activation fields untouched. Stake: (a) adds a write in the verify path (Lot 1 code); (b) leaves "attached" invisible to the coach.
4. **Production BCC and OTP mails.** Options: (a) exclude login-code mails from ProductionBcc; (b) keep the BCC on all mails. Stake: with (b) the BCC mailbox receives every live login code of the Beta (`RoutingEmailSender.cs:39-47`).
5. **Photo folder and public URL.** Options: (a) subfolder `photos/` inside the existing media folders, nginx mounts only that subfolder read-only; (b) separate host folders `photos-demo` / `photos-prod`. Public path on the API host (for example `/media/exercises/...`). Stake: (a) reuses the existing mount and backup; (b) keeps voice and photos fully apart. Both need a VPS change before 24/11.

---

## Size estimate per sub-lot

| Sub-lot | Hours | Reason |
|---|---|---|
| 14.1 Coach adds member by email + mail | 10 | first account-creation path (3 entities, NOT NULL fields, 23505 race), existing-email cases, validator, new mail type with HTML encoding, new install link option with startup validation, activation write in verify, evidence |
| 14.2 OTP protection | 5 | migration (counter + CHECK), atomic increment/burn/consume copied from admin, new per-IP policy on /api/v1/auth, evidence of 429 and 5-attempt burn |
| 14.3 Production routing + no "[OTP]" line | 2 | environment guard or removal of `AuthService.cs:61`, review script note, VPS env vars (Mode, ProductionBcc, Smtp) checked at startup |
| 14.4 Photo storage + ffmpeg processing | 8 | new options/validator/availability, photo storage (incoming, move, escape check), ffmpeg JPEG 1600 px + 480 px thumbnail, magic bytes, metadata strip, codec proof in the image, nginx mount + location |
| 14.5 Photo API + app field | 6 | entity + migration (order, caption, max 6), admin routes upload/caption/reorder/delete with FOR UPDATE and audit (CHECK recreation), post-commit file cleanup, `photos` appended to the two DTOs with a dictionary query |
| 14.6 Admin photo card | 8 | multipart in ApiTransport with refresh, new card in Form.razor, drag and drop interop in admin.js with keyboard fallback, CSP img-src, Duplicate banner text |
| 14.7 Push via Firebase (optional) | 10 | no existing code: package, credentials option and validation, sender service, invalid-token cleanup, triggers (messages at least), evidence on devices; only if PJ provides Firebase data |

Total without 14.7: about 39 hours.
