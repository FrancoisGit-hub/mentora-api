# Lot 14.8.0: read-only inventory for the admin "Coachs" screen

- Branch `feature/lot_14`, no upstream. HEAD `56a8b01` = `origin/main`, `git status --short` empty at start (checked 2026-11-23).
- Read-only session: no build, no run, no database, no network. No appsettings of Mentora.API, `.env` or `*.log` opened.
- Paths are relative to the repo root. `Cfg/` = `Mentora.Infrastructure/Persistence/Configurations/`, `Mig/` = `Mentora.Infrastructure/Persistence/Migrations/`, `Svc/` = `Mentora.Infrastructure/Services/`.
- Example addresses are fictional (`coach.demo@example.com`).

## 3.1 What makes a coach account today

### 3.1.0 Key fact
No C# code creates a COACHES row today (no `new Coach`, no `Coaches.Add` anywhere). Coaches only come from SQL fixtures, for example `docs/review/lot11/11.5/fixture.sql:24`, `docs/review/lot14/14.1/fixture-commit.sql:19`. 14.8 is the **first code path that creates a coach**, exactly like 14.1 was the first one creating a member (`docs/review/lot14/14.0/inventory.md:329`).

### 3.1.1 USERS
Entity `Mentora.Core/Entities/User.cs:5-15`, configuration `Cfg/UserConfiguration.cs:11-28`, table created in `Mig/20260326120304_AddAuthTables.cs:14-31`, two columns added in `Mig/20260730194501_AddLot5Parameters.cs:18-28`.

| Column | Type | Required | Default | Source |
|---|---|---|---|---|
| USER_ID | uuid | PK | `gen_random_uuid()` | cfg `:14-16` |
| USER_CREATED_DATE | timestamptz | yes | none | cfg `:18` |
| USER_MODIFICATION_DATE | timestamptz | no | | cfg `:19` |
| USER_EMAIL | varchar(255) | yes | none | cfg `:20` |
| USER_PSW | varchar(255) | no | | cfg `:22` (unused by OTP auth) |
| USER_LOGIN | varchar(100) | no | | cfg `:23` (a login name, not a date; nothing reads it) |
| USER_IS_ENABLED | boolean | yes | none | cfg `:24` |
| USER_DISABLED_DATE | timestamptz | no | | cfg `:25` |
| USER_ROLE | varchar(50) | yes | none, no converter | cfg `:26` |
| USER_DELETION_REQUESTED_DATE | timestamptz | no | | cfg `:27`, mig Lot5 `:24-28` |
| USER_DELETION_REASON | text | no | | cfg `:28`, mig Lot5 `:18-22` |

- Required without default, so the service must set them: USER_CREATED_DATE, USER_EMAIL, USER_IS_ENABLED, USER_ROLE.
- USER_ROLE is **never read by the code**: the only reference to `.UserRole` is the configuration line (`Cfg/UserConfiguration.cs:26`). The role used by auth comes from the existence of the COACHES / MEMBERS rows (3.1.4).
- USER_ROLE casing in existing data is mixed: `'MEMBER'`, `'COACH'`, and the known exception `'Coach'` (listed in `docs/review/lot14/14.0/inventory.md:50`). 14.1 writes upper case `"MEMBER"` (`Svc/CoachMemberInviteService.cs:31-32`, `:67`). By symmetry a coach creation would write `"COACH"`; the column has no CHECK, any value is accepted.
- Email indexes:
  - `IX_USERS_USER_EMAIL` unique, case-sensitive, from `HasIndex(e => e.UserEmail).IsUnique()` (`Cfg/UserConfiguration.cs:21`, `Mig/20260326120304_AddAuthTables.cs:214-218`).
  - `UX_USERS_EMAIL_CI` unique on `lower("USER_EMAIL")`, raw SQL (`Mig/20261119221223_Lot14_1_UserEmailCi.cs:16`, Down `:22`). Not declared in the EF model.
- Relations: COACHES and MEMBERS are 1:0..1 on USER_ID, cascade delete (`Cfg/UserConfiguration.cs:30-38`).

### 3.1.2 COACHES
Entity `Mentora.Core/Entities/Coach.cs:5-17`, configuration `Cfg/CoachConfiguration.cs:11-43`, table `Mig/20260326120304_AddAuthTables.cs:78-99`, 11.3 columns `Mig/20261006001014_Lot11_3_CoachSessionPreferences.cs:14-40`.

| Column | Type | Required | Default | Source |
|---|---|---|---|---|
| COACH_ID | uuid | PK | `gen_random_uuid()` | cfg `:14-16` |
| COACH_FIRST_NAME | varchar(100) | yes | none | cfg `:18` |
| COACH_LAST_NAME | varchar(100) | yes | none | cfg `:19` |
| COACH_PHONE | varchar(20) | no | | cfg `:20` |
| COACH_CREATED_DATE | timestamptz | yes | none | cfg `:21` |
| COACH_IS_ACTIVE | boolean | yes | none | cfg `:22` |
| USER_ID | uuid | yes, FK USERS cascade | | cfg `:23`, mig `:93-98` |
| COACH_OFFERS_VISIO | boolean | yes | false | cfg `:26-29` |
| COACH_OFFERS_IN_PERSON_SOLO | boolean | yes | false | cfg `:30-33` |
| COACH_IN_PERSON_LOCATIONS | jsonb | yes | `'[]'::jsonb` | cfg `:34-38` |

- Unique index `IX_COACHES_USER_ID` (`Mig/20260326120304_AddAuthTables.cs:191-195`): one coach per user.
- CHECK `CK_COACHES_IN_PERSON_LOCATIONS` (raw SQL, `Mig/20261006001014_Lot11_3_CoachSessionPreferences.cs:35-40`): array, at most 10 items, empty unless in-person solo. The defaults (false, `[]`) satisfy it.
- Required without default: first name, last name, created date, COACH_IS_ACTIVE, USER_ID.
- COACH_IS_ACTIVE is only echoed in `coach/me` and `member/me` (`Svc/CoachMeService.cs:143`, `Svc/MemberMeService.cs:105`); nothing blocks on it.
- **No Stripe column, no validation or publication status on COACHES** (`Coach.cs:3-30`, `CoachConfiguration.cs:9-44`). Stripe exists only at order level (`Mentora.Core/Entities/Order.cs:18-19`), checkout is a stub (`Mentora.API/Program.cs:308`).
- Session preferences of 11.3 are columns of COACHES with defaults, so they cannot be missing (`Coach.cs:12-15`); GET/PUT `api/v1/coach/settings/session-preferences` (`Mentora.API/Controllers/Coach/CoachSessionPreferencesController.cs:10,17,30`).
- AGENT_TOKENS (1:0..1, `Cfg/CoachConfiguration.cs:40-43`) is never read or written by any service: not needed.

### 3.1.3 Rows a coach needs besides USERS + COACHES

Seeders, run once at each API start after `MigrateAsync` (`Mentora.API/Program.cs:595-598`):

- `CoachParameterSeeder` (`Mentora.Infrastructure/Seeding/CoachParameterSeeder.cs`): for every coach without a COACH_PARAMETERS row (`:16-18`, ignores COACH_IS_ACTIVE), one row with hourly rate 50.00 (`:27`), cancellation delay 24 h (`:28`), created and updated dates = now; every other column takes the entity initializers (`Mentora.Core/Entities/CoachParameter.cs:8-24`), equal to the DB defaults (`Cfg/CoachParameterConfiguration.cs:20-93`): language FR, notif messages / new booking / booking cancelled true, marketing false, min notice 24, horizon 90, late refunds false, accepting bookings true, default duration 60, missed session SKIP. Created and updated dates have **no DB default** (`:95-101`). `COACH_ID` unique, cascade (`:103-112`). No CHECK.
- `OfferProgramSeeder` (`Mentora.Infrastructure/Seeding/OfferProgramSeeder.cs`): for every coach with zero active OFFER_PROGRAMS (`:25-27`), 4 rows "Prise de masse", "Perte de poids", "Préparation Marathon", "Préparation Hyrox" (`:11-17`), active, created date = now (`:36-42`). Partial unique index `IX_OFFER_PROGRAMS_ACTIVE_NAME` on (coach, name) where active (`Cfg/OfferProgramConfiguration.cs:37-40`). Name max 80, created date no default (`:18-30`).
- The other startup seeders (`Program.cs:600-609`) are reference data, nothing per coach.

Per-coach child rows: get-or-create vs assumed:

| Row | Behaviour when missing | Source |
|---|---|---|
| COACH_PARAMETERS | **assumed**: GET `coach/me` throws `InvalidOperationException` "Seeder invariant broken" → **400** (not 500, the middleware maps it to 400) | `Svc/CoachMeService.cs:29-31`, `Mentora.API/Middleware/GlobalExceptionMiddleware.cs:73-77` |
| COACH_PARAMETERS | **assumed**: GET `member/me` of **any member linked to that coach** → 400 | `Svc/MemberMeService.cs:96-98` |
| COACH_PARAMETERS | **assumed**: member cancels a session of that coach → 400 | `Svc/SessionService.cs:169-172` |
| COACH_PARAMETERS | get-or-create on GET and PUT `coach/parameters` | `Svc/CoachParameterService.cs:24-30`, `:45-52` |
| COACH_PARAMETERS | tolerated (fallback SKIP) in agendas | `Svc/AgendaService.cs:36-39`, `:84-87` |
| OFFER_PROGRAMS | list returns empty; product create/update needs an active program, else 404 | `Svc/OfferProgramService.cs:12-18`, `Svc/ProductService.cs:39-45`, `:85-91` |
| SESSION_TEMPLATES, conversations, carts | created by use, empty is fine | `Svc/SessionTemplateService.cs:147`, `Svc/ConversationService.cs:443-471`, `Svc/CartService.cs:82-97` |

Conclusion: COACH_PARAMETERS is the only row the API assumes exists. OFFER_PROGRAMS is not required for the API to answer, but without it the coach cannot create a product until a restart reseeds. Both belong in the creation transaction, as decided.

### 3.1.4 How app login decides the role
`Svc/AuthService.cs`, routes `Mentora.API/Controllers/Auth/AuthController.cs:17-45`.

- Email normalized with `EmailNormalizer.Normalize` (trim + lower invariant, `Mentora.Core/EmailNormalizer.cs:9-10`) at `AuthService.cs:42` and `:106`; lookup on `lower(USER_EMAIL)` and `USER_IS_ENABLED` (`:45-49`, `:108-112`).
- `ResolveEffectiveIsCoach` (`:190-217`):
  - `isCoach` sent: honoured but checked against the profiles (`:192-200`).
  - `isCoach` absent: COACHES and MEMBERS rows both present → `ConflictException` → **409** (`:202-209`; AuthController only catches `InvalidOperationException`, `:26`, `:41`, so the 409 comes from the middleware `GlobalExceptionMiddleware.cs:29-33`). Only COACHES → coach (`:211-212`). Only MEMBERS → member (`:213-214`). Neither → 400 (`:216`).
- So "coach" = a COACHES row; USER_ROLE plays no part. **Deducing the role without isCoach already works today** for a coach-only account: nothing to change in AuthService for 14.8.
- Verify: refresh token stores the role (`:147`, `:169`); a coach token carries `userType=COACH` and `coachId` (`:175`, `:316-336`). The member activation flag is not touched for a coach (`:163`).
- Refresh keeps the original role (`:240-255`).
- Brand new coach (USERS + COACHES + COACH_PARAMETERS + 4 OFFER_PROGRAMS, no member, no session, no Stripe): GET `api/v1/coach/me` (`Mentora.API/Controllers/Coach/CoachMeController.cs:9-31`, `Svc/CoachMeService.cs:14-145`) returns 200 with empty members, sessions and offers, stats at 0, profile with rate and delay. **Without** COACH_PARAMETERS it returns 400 until a GET `coach/parameters` or a restart.
- `GET /api/v1/me/context` **does not exist** (only `api/v1/coach/me` and `api/v1/member/me`, `Mentora.API/Controllers/Member/MemberMeController.cs:9`).
- Note: CLAUDE.md cites MemberOnly at `Program.cs:185-187`; the policy is now at `Mentora.API/Program.cs:248-250`.

### 3.1.5 CoachMemberInviteService (14.1)
`Svc/CoachMemberInviteService.cs`.

- Validation first (`:37`), FluentValidation, 422 through the middleware. Rules in `Mentora.Core/Validators/Coach/AddMemberRequestValidator.cs:15-33`: email not empty, normalized length <= 255, valid address on the normalized value; first and last name not blank, trimmed length <= 100.
- Normalization: email `:39`, names trimmed `:40-41`.
- Transaction `:45`, first statement `SELECT pg_advisory_xact_lock(7141401)` (`:29`, `:48`), then lookup by `lower(email)` with Coach and Member included (`:50-53`).
- New account: USERS (`UserRole = "MEMBER"`, enabled), MEMBERS, MEMBER_COACHES, one `SaveChanges` (`:57-92`).
- Existing account: 409 with `details.reason` = `ACCOUNT_DISABLED` (`:99-100`), `EMAIL_IS_COACH` (`:101-102`), `NO_MEMBER_PROFILE` (`:103-104`), `ALREADY_ATTACHED` (`:111-112`). Helper `Conflict(reason, message)` → `ConflictException(message, new { reason })` (`:180-181`).
- **No unique violation handling**: no `DbUpdateException` / 23505 catch. Uniqueness relies on the advisory lock; a writer that does not take the same lock and inserts the same email would make this service fail with a `DbUpdateException` → 500.
- Commit `:130`, then mail outside the transaction `:133`, best effort in try/catch (`:141-178`): `sent == false` and exception each log one warning with ids only, no address and no body (`:169-177`). The 201 never says whether the mail went out (`Mentora.API/Controllers/Coach/CoachMembersController.cs:22`, `:38`).
- Rate limit per coach (`CoachMembersController.cs:27`, `Mentora.API/Authorization/MemberInviteRateLimiting.cs`).

Reusable as is for a coach creation: `EmailNormalizer`, the validator rules (copy for a new `AdminCreateCoachRequest`), the lock + lookup + insert + commit + mail-after-commit shape, the `Conflict(reason)` helper idea, the warning-only logging.
Not reusable as is: the member branch (attach to an existing member), the 409 reasons (a coach creation refuses **any** existing email), `MemberAddedEmailBuilder` (member wording, coach name in the subject), the per-coach rate limit (admin routes have their own context), the controller envelope (admin uses its own `Envelope`, same shape).

### 3.1.6 Existing member becoming BOTH
- Adding a COACHES row to a user that has a MEMBERS row is accepted by the schema (`IX_COACHES_USER_ID` only limits one coach per user).
- Then every app login without `isCoach` gets **409** (`Svc/AuthService.cs:202-209`). The member app (and the coach app per the 21/11 decision) does not send `isCoach`, so the member would be locked out of the member app.
- Mirror rule already in 14.1: a coach email cannot be added as a member (`EMAIL_IS_COACH`, `Svc/CoachMemberInviteService.cs:101-102`).
- This is why the spec says 409 for any existing email (member, coach, disabled or not): it is the only way to never produce a BOTH account.

## 3.2 Admin back end patterns

### 3.2.1 AdminExercisesController and AdminExerciseService
- Route prefix `api/v1/admin` (`Mentora.API/Controllers/Admin/AdminExercisesController.cs:16`), `[Authorize(Policy = AdminAuthDefaults.Policy)]` = `AdminOnly` (`:17`, `Mentora.API/Authorization/AdminAuthDefaults.cs:10`), Swagger group `admin` (`:18`, `AdminAuthDefaults.cs:19`), tag (`:19`).
- Policy: AdminBearer scheme only, authenticated, claim `adminId` (`Mentora.API/Program.cs:240-243`). Swagger doc "admin" declared at `Program.cs:61-66`, endpoint `Program.cs:625`.
- Admin user id: claim `adminId` parsed in `GetAdminUserId`, missing or malformed → `UnauthorizedException` 401 (`AdminExercisesController.cs:146-151`). The service then checks the admin exists and is enabled (`Svc/AdminExerciseService.cs:614-619`, called at `:420`).
- Envelope `{ success, data, error, statusCode }` (`AdminExercisesController.cs:153-154`); creation returns `StatusCode(201, ...)` (`:99-103`).
- Paging: `limit` default 50 (`:65`), 1 to 100 else 400 (`Svc/ExerciseListPaging.cs:15-19`); cursor = Base64 of JSON `{ Name, ExerciseId }` (`ExerciseListPaging.cs:34-38`), bad cursor → 400 "Invalid cursor format." (`:40-52`); keyset `name > cursorName OR (name = cursorName AND id > cursorId)`, order by name then id, `Take(limit + 1)` (`Svc/AdminExerciseService.cs:152-165`); `nextCursor` when one more row exists (`:232-253`).
- Filters: nullable query parameters combined with AND (`AdminExerciseService.cs:121-138`); search 2 to 60 characters (`ExerciseListPaging.cs:21-30`).
- Creation: validator inside the service (`AdminExerciseService.cs:419`), transaction + advisory lock `7122002` (`:32`, `:429-432`), uniqueness checked under the lock (`:434`), entity + audit row in one `SaveChanges`, commit (`:472-476`). Unique violation safety net: `catch (DbUpdateException) when (IsNameViolation)` → re-check and 409 (`:478-483`, `:655-658`).
- 409: `ConflictException("EXERCISE_NAME_TAKEN", details)` (`:647-652`): the code is in `error`, the details object carries the existing row. Note the difference with 14.1, where the code is in `details.reason` and `error` is an English sentence.
- 422: `ValidationException` → `errors` keyed by PascalCase property names (`GlobalExceptionMiddleware.cs:34-55`). Validators: `Mentora.Core/Validators/AdminExercise/AdminExerciseValidators.cs:11`, registered by assembly scan (`Program.cs:302`).

### 3.2.2 ADMIN_AUDIT_LOG
- Entity `Mentora.Core/Entities/AdminAuditLog.cs:9-24`; JSON contract comment: "Created: the stored values with ids" (`:20`).
- Configuration `Cfg/AdminAuditLogConfiguration.cs:17-65`: action and entity type varchar(40) through converters (`:26-40`), entity id uuid (`:42`), changes jsonb (`:44-47`), created date (`:49`), FK to ADMIN_USERS Restrict (`:52-56`), three indexes (`:58-65`).
- Enums and mappings `Mentora.Core/Enums/AdminAuditAction.cs`: actions `ExerciseCreated`, `ExerciseUpdated`, `ExerciseActivated`, `ExerciseDeactivated`, `ExerciseVideoUploadCreated`, `ExerciseVideoRemoved` (`:4-12`); entity type `Exercise` only (`:15-18`); DB values via if chains (`:23-55`).
- CHECKs:
  - `CK_ADMIN_AUDIT_LOG_ACTION`: created by `Mig/20261109160428_Lot12_2b_AdminAuditLog.cs:55-56` (4 values), last changed by `Mig/20261110104601_Lot12_3a_MuxVideos.cs:105-111`: `EXERCISE_CREATED, EXERCISE_UPDATED, EXERCISE_ACTIVATED, EXERCISE_DEACTIVATED, EXERCISE_VIDEO_UPLOAD_CREATED, EXERCISE_VIDEO_REMOVED`.
  - `CK_ADMIN_AUDIT_LOG_ENTITY_TYPE`: `('EXERCISE')`, created `Lot12_2b:62-63`, never changed.
  - Down of Lot12_3a: a `DO $$ ... RAISE EXCEPTION` guard refuses when ADMIN_AUDIT_LOG holds the new values, before anything changes (`Lot12_3a:118-133`), then drops and recreates the CHECK with the old list (`:135-141`).
- JSON written for an exercise creation: `{ sourceCode, isActive, ...request fields }` camelCase, serialized with `JsonSerializer.Serialize` (`AdminExerciseService.cs:468-470`, `:629`). `NewAudit` hard-codes `AdminAuditEntityType.Exercise` (`:621-631`), so a coach service needs its own helper.
- What a coach creation adds: action `CoachCreated` ↔ `COACH_CREATED`, entity type `Coach` ↔ `COACH`, entity id = COACH_ID (or USER_ID, to decide). Migration (for example `Lot14_8_CoachCreatedAudit`) on the 3a model: Up drops and recreates both CHECKs with the extra value; Down first runs a guard `IF EXISTS (SELECT 1 FROM "ADMIN_AUDIT_LOG" WHERE "ADMIN_AUDIT_LOG_ACTION" = 'COACH_CREATED' OR "ADMIN_AUDIT_LOG_ENTITY_TYPE" = 'COACH') THEN RAISE EXCEPTION ...`, then restores the two previous lists. The model snapshot does not change (CHECKs are raw SQL), so the migration is hand written after an empty `migrations add`. Precheck: none needed for Up (widening a CHECK cannot fail on existing rows).
- Possible creation JSON: `{ userId, coachId, email, firstName, lastName, isActive, coachParameterId, hourlyRateEuros, cancellationDelayHours, offerProgramIds }`. The email in an append-only log is personal data (question Q3).

### 3.2.3 Error mapping on admin routes
`Mentora.API/Middleware/GlobalExceptionMiddleware.cs`, outermost (`Program.cs:617`): NotFound 404 (`:14-18`), Unauthorized 401 (`:19-23`), Forbidden 403 (`:24-28`), Conflict 409 + `details` (`:29-33`), FluentValidation 422 + `errors` (`:34-56`), MUX 502/503 and audio 503 (`:57-72`), `InvalidOperationException` 400 (`:73-77`), anything else 500 (`:78-82`). Envelope with optional `details` (`:85-111`). Authentication failures (no token, app token) are 401 from the auth middleware, before the controller.

## 3.3 Mail

### 3.3.1 MemberAddedEmailBuilder
`Svc/Email/MemberAddedEmailBuilder.cs` (internal static, `:12`).
- Subject: `Votre coach {prénom} vous a ajouté sur Mentora`, or `Votre coach vous a ajouté sur Mentora` (`:19-22`); first name cleaned by `AuthService.SanitizeFirstName` (`Svc/CoachMemberInviteService.cs:150`, `Svc/AuthService.cs:94-102`).
- Constants: install with button, fallback "Votre coach vous enverra le lien pour installer l'application Mentora.", code step, "Ne passez pas par « Créez un compte »" (`:14-17`).
- HTML (`:33-63`): `WebUtility.HtmlEncode` on coach name, email, step text, href, fixed sentences (`:35-57`); 3-step list (install, enter email then "Connexion", 6-digit code); bordered button "Installer l'application" only when the URL is set (`:38-41`); footer "Si vous ne connaissez pas ce coach, ignorez ce message.".
- Text (`:65-89`): same content, line `Installer l'application : {url}` when set (`:69-73`).
- `MemberInviteOptions` (`Mentora.Core/Options/MemberInviteOptions.cs`): section `MemberInvite` (`:9`), `InstallUrl` optional (`:12`), `HasInstallUrl` = not blank (`:20`), `PermitLimit` 30 in 1..1000, `WindowMinutes` 60 in 1..1440 (`:15-28`), URL rule absolute https, no whitespace, host present, message without the value (`:29-36`). Validator `Svc/Email/MemberInviteOptionsValidator.cs:7-14`; registration `Program.cs:399-403`.
- `[INVITE]` line before migrations: `installUrl={bool} permitLimit windowMinutes`, never the URL (`Program.cs:521-527`). VPS shows `installUrl=False` (`docs/review/lot14/vps/vps-evidence.md:41`).

### 3.3.2 Sending path
- `IEmailSender.SendAsync` returns bool, must not throw (`Mentora.Core/Interfaces/IEmailSender.cs:3-13`); `EmailMessage.ForRecipient` (`Mentora.Core/Interfaces/EmailMessage.cs:7-15`).
- DI: `IEmailSender` always resolves to `RoutingEmailSender` around Smtp or Logging (`Program.cs:430-441`).
- `RoutingEmailSender` (`Svc/Email/RoutingEmailSender.cs`): line `[EMAIL] mode to bcc subject` (`:20-22`, recipients are logged, never the body); TEST (any mode other than Production): To replaced by the test recipients, Bcc cleared, subject prefixed `[TEST pour <original>]` (`:28-36`); PRODUCTION: real To + ProductionBcc deduplicated (`:39-47`).
- Startup refusal and `[EMAIL] routing mode provider testRecipients productionBcc` counts line (`Program.cs:507-519`); options `Mentora.Core/Options/EmailRoutingOptions.cs:31-58`, `Mentora.Core/Options/EmailOptions.cs:28-46`.
- Sender: `FromAddress` with display name `FromDisplayName`, default "Mentora" (`EmailOptions.cs:12-13`, `Svc/Email/SmtpEmailSender.cs:72`, `:80`). All mails are in French.
- Best effort after commit in 14.1: see 3.1.5 (`Svc/CoachMemberInviteService.cs:130-178`).

### 3.3.3 What a coach welcome mail changes
- Recipient is the coach; content points to the **coach app**, not the member app.
- Subject without a coach first name (the sender is Mentora, not a coach), for example "Votre compte coach Mentora est prêt"; can greet by the coach's own first name like the OTP mail (`Svc/AuthService.cs:78-80`).
- Steps: install the coach app, enter the email, receive the 6-digit code; no "ask your coach" fallback (the fallback would become "Mentora vous enverra le lien..." or similar).
- Footer: "Si vous ne connaissez pas ce coach" no longer applies.
- Install link: `MemberInvite:InstallUrl` is the only app URL option in the code (no other InstallUrl / StoreUrl / TestFlight option found). If the coach and member apps are separate installs, the same URL cannot serve both; a second optional option (for example `CoachInvite__InstallUrl`, same validation, own `[INVITE]` field) would be needed. If a single landing page offers both apps, the existing one can be reused. Decision for François (Q1).
- A new builder (for example `CoachWelcomeEmailBuilder`) is cleaner than parameterizing the member one.

## 3.4 Admin front end (Mentora.Admin)

### 3.4.1 Structure
- net8.0 Blazor WebAssembly (`Mentora.Admin/Mentora.Admin.csproj:4`).
- Pages: `Pages/Index.razor:1` (redirect, `:12`), `Pages/Login.razor:1-2` (`/connexion`), `Pages/Exercices/List.razor:1-2` (`/exercices`), `Pages/Exercices/Form.razor:1-3` (`/exercices/nouveau`, `/exercices/{id:guid}`). Router with `EmptyLayout` default and a NotFound view (`App.razor:9-23`).
- Side menu `Layout/ShellLayout.razor`: static array `MenuItem(Label, Step, Active)` (`:74-87`). There is **no "Coachs" item**: the candidates are "Utilisateurs" / Étape 4 (`:81`) and "Fiches coach" / Étape 5 (`:82`). Disabled items are `<button disabled title="Bientôt disponible">` (`:35`) greyed by `.nav:disabled` (`wwwroot/css/app.css:144`). The single active item always calls `GoToExercises` (`:28`, `:132`) and has a static `aria-current`; title and subtitle hard-wired to exercises (`:92-106`). Activating a second item needs: a route per item, `aria-current` from the current path, `Title`/`Subtitle` per section.
- Auth guard in the layout (`ShellLayout.razor:108-127`).
- HTTP: base URL `ApiBaseUrl` (`AppConfig.cs:6-23`, production `https://api.mentorapp.fr`); `Api/ApiTransport.cs` JSON only, camelCase (`:13`, `:24`), Bearer (`:25-26`), parses `data`, `error`, `details`, `errors` (`:69-107`); `Api/ApiClient.cs:9-35` refresh before expiry, one retry on 401, then end of session; `Api/AdminSession.cs` access token in memory (`:31`), refresh token in localStorage (`:15-18`), shared refresh task (`:154-163`), cross-tab `navigator.locks` through `mentoraAdmin.runExclusiveRefresh` (`:165-177`, `wwwroot/js/admin.js:214-221`).
- 403 has no specific handling (generic "Une erreur est survenue, réessayez.", `Services/ExerciseUiState.cs:37-38`).
- Models: no Models folder; DTOs in `Api/AuthModels.cs`, `Api/ExerciseModels.cs` (list page `:43`), endpoints in `Api/ExercisesApi.cs` (`Root = "api/v1/admin/"`, `:9`). A coach screen would add `Api/CoachModels.cs` and `Api/CoachesApi.cs`.
- Shared components: `Components/ConfirmDialog.razor` (native `<dialog>`, used for "Quitter sans enregistrer ?" at `Form.razor:383-386` with `NavigationLock` `:13`, `:922-941`), `FieldError.razor`, `ChipPicker.razor`, `ThemeButton.razor`, `ConfigError.razor`. No toast: inline `.info role=status` / `.alert role=alert` (`app.css:65-66`). Badges `.badge`, `.badge-muted`, `.badge-warn`, `.badge-error` (`app.css:57`, `:88`, `:90`, `:187`).
- CSS: one file `wwwroot/css/app.css`, light and dark themes as classes on `<html>` (`:3-4`), no inline styles allowed (CSP, `:189`).

### 3.4.2 Exercices list and form patterns
- List: filters in the query string via `[SupplyParameterFromQuery]` (`List.razor:225-237`), lenient parsing (`:277-310`), URL rewritten with `replace: true` (`:390-403`); search debounced 300 ms, 2 to 60 characters (`:422-460`); page size 50 (`:222`), "Afficher plus" appends with dedupe (`:205-210`, `:356-385`), stale responses dropped (`:326`, `:339`).
- Form: client required check (`Form.razor:985-998`); 422 keys mapped case-insensitively to field ids, generic French message, unmapped keys listed in a banner (`:412-436`, `:1058-1070`, `:75-89`); 409 `EXERCISE_NAME_TAKEN` reads `details` and links to the existing row (`:1050-1057`, `:138-143`); creation success = flash then navigate with replace (`:1021-1027`, `:542-557`).

### 3.4.3 What a "Coachs" page can show

| Column | Exists today | Cost |
|---|---|---|
| First and last name | COACHES `:18-19` | none |
| Email | USERS.USER_EMAIL | join USERS on `IX_COACHES_USER_ID` |
| Created date | COACH_CREATED_DATE (and USER_CREATED_DATE) | none |
| Enabled | USER_IS_ENABLED and COACH_IS_ACTIVE (two flags) | none |
| Number of members | MEMBER_COACHES by COACH_ID | grouped count, index `IX_MEMBER_COACHES_COACH_ID` (`Mig/20260326120304_AddAuthTables.cs:197-200`) |
| Last login | **no column**; derivable from max AUTH_REFRESH_TOKEN_CREATED_DATE with role COACH (refresh also creates rows, so it is "last activity", and the daily purge removes old rows, `Program.cs:413-416`) | grouped max, index on USER_ID only (`AddAuthTables.cs:186-189`) |
| Stripe connected | **does not exist** | n/a |
| Account activated (first login done) | no coach equivalent of MEMBER_HAS_ACTIVATED | n/a |

With a few dozen coaches, every option is cheap; correlated subqueries per page of at most 100 rows are fine. Keyset on (created date desc, id) or (last name, first name, id) following `ExerciseListPaging`.

### 3.4.4 CSP and nginx
- CSP only in `nginx/conf.d/mentora-admin.conf.example:81`; `connect-src 'self' https://api.mentorapp.fr https://*.mux.com`. `index.html` has no CSP meta (`wwwroot/index.html:4-30`).
- The `script-src` sha256 covers the inline theme script of `index.html:14-28` (procedure `mentora-admin.conf.example:14-22`).
- SPA fallback `try_files ... /index.html` (`:84-86`).
- A new page calling the same API host needs **no CSP, nginx or index.html change**. It must not touch `index.html` (hash) and must not use inline styles.

## 3.5 Concurrency, security and proofs

### 3.5.1 Two simultaneous creations with the same email
- Normalization makes both requests use the same lower-case value, so `IX_USERS_USER_EMAIL` and `UX_USERS_EMAIL_CI` both protect; different casing in input changes nothing.
- With the advisory lock taken first in the transaction (pattern `Svc/CoachMemberInviteService.cs:45-53`), the second request waits, then sees the committed user and answers 409 (no 500).
- **Recommendation for the design:** use the **same lock key 7141401** as the member add. A coach creation and a member add for the same email at the same time would otherwise both read "no user" and the loser would hit the unique index → `DbUpdateException` → 500 in whichever service does not catch it (neither does today). A 23505 catch on `UX_USERS_EMAIL_CI` / `IX_USERS_USER_EMAIL` → 409 (pattern `Svc/AdminExerciseService.cs:478-483`) is a cheap second net.
- Expected answers: first 201, second 409 with a code (for example `EMAIL_TAKEN`).

### 3.5.2 AdminOnly and token separation (12.A)
- App token on an admin route: the policy authenticates with AdminBearer only (`Program.cs:240-243`); the app token fails admin key and audience validation (`Program.cs:218-233`) → **401**.
- Admin token on an app route: app routes use the default Bearer scheme (`Program.cs:168-206`, `CoachOnly` `:245-246`); the startup refuses an admin key or audience equal to the app ones (`Program.cs:582-585`) → admin token invalid → **401**. Same for the coach `/me`.
- Admin id from claim `adminId`, plus enabled check in the service (3.2.1).

### 3.5.3 Runtime proofs the implementation will need
1. Migration precheck: none needed for Up; local proof that Up then Down then Up works, and that Down refuses when a `COACH_CREATED` row exists (on a throwaway row, rolled back).
2. Run start date read in the database before the first write (14.1 model, `docs/review/lot14/14.1/runtime-evidence.md:8`).
3. Real HTTP codes with a real admin JWT: GET list 200 (paging with `limit=1` and `nextCursor`, bad cursor 400, limit 0 400); POST 201; same email 409; same email in capitals 409; email of an existing member 409; disabled account 409; invalid body 422 with PascalCase keys; no token 401; app coach token 401.
4. Concurrency: two parallel POSTs with the same email (one in capitals) → one 201, one 409, one USERS row.
5. SQL read of the created rows: USERS (email lower case, enabled, role), COACHES, COACH_PARAMETERS (50.00, 24, defaults), 4 OFFER_PROGRAMS, one ADMIN_AUDIT_LOG row with the same created timestamp as the coach (same transaction) and the expected JSON.
6. Mail: `[EMAIL] mode=TEST` line for the welcome mail, after the commit; mail failure case (Logging provider or unreachable SMTP) still 201 with one warning without address.
7. Coach login right after creation: otp/request without isCoach 200, otp/verify without isCoach 200 with `isCoach: true`; GET `coach/me` 200 with stats 0 **without restarting the API** (proves COACH_PARAMETERS was created).
8. Admin screen: list, search or paging, creation success message, 409 and 422 display, "Quitter sans enregistrer ?", dark and light themes, phone width; CSP check in the browser console on the VPS (no violation).
9. Cleanup bounded by prefix (`l148-`) and run start, dry run then commit differing by the last line only, zero leftovers in every table with a `*_CREATED_DATE` column, including ADMIN_AUDIT_LOG (the FK to ADMIN_USERS is Restrict, delete audit rows by entity id).
10. Grep of the evidence for real addresses and secrets (14.1 pattern, `runtime-evidence.md:192`).

## 4. Estimates and open questions

### 4.1 Split

| Sub-lot | Content | Estimate |
|---|---|---|
| 14.8a | API: DTOs, validator, `AdminCoachService` (list + create in one transaction with lock 7141401, COACH_PARAMETERS and 4 OFFER_PROGRAMS from the seeder constants, audit row, 409 codes, 23505 net), controller (Swagger admin), enum values + migration with Down guard, `CoachWelcomeEmailBuilder` + optional option and `[INVITE]` field if Q1 says so, runtime proofs 1 to 7, 9, 10 | 9 to 11 h (code 5 h, proofs and evidence 4 to 6 h) |
| 14.8b | Admin screen: menu item made active with per-item routes, `/coachs` list (columns per Q2, paging), creation form (validation, 409, 422, success, leave guard), API client and models, proofs 8 | 6 to 8 h |
| 14.8c | VPS deployment: migration on demo, env variable if any, proofs on the VPS (login of a created coach, CSP), cleanup | 2 to 3 h |

Total about 17 to 22 h. Sharing the seeder constants (50.00, 24, the 4 program names) between seeders and the new service avoids two sources of truth.

### 4.2 Open questions for François

**Q1. Install link in the coach welcome mail.** Today there is a single optional link `MemberInvite__InstallUrl`, meant for the member app, and it is not set on the VPS. Options: (a) reuse it for coaches too; (b) add a second optional link `CoachInvite__InstallUrl` for the coach app; (c) no link for coaches, the sentence says Mentora sends the link. Recommendation: (b), with the (c) sentence as fallback when it is empty; it costs little and does not send a coach to the member app.

**Q2. Columns of the list.** Cheap and available: name, email, created date, enabled, number of members. "Last login" has no column (only an approximation from refresh tokens, erased by the daily purge) and "Stripe connected" does not exist at all. Options: (a) the five available columns only; (b) add an approximate "dernière activité"; (c) also plan a Stripe column (needs a Stripe Connect lot first). Recommendation: (a).

**Q3. The email in the audit log.** The audit row of a creation stores "the stored values". Options: (a) store the email and names in clear like any created value; (b) store only ids, the email stays in USERS. Recommendation: (a), the log is admin-only and it is what an associate needs to read later; to be noted for the GDPR deletion procedure.

**Q4. Which menu item.** There is no "Coachs" item; there are "Utilisateurs" (Étape 4) and "Fiches coach" (Étape 5). Options: (a) rename "Fiches coach" to "Coachs" and activate it; (b) activate "Utilisateurs" with only a coaches tab for now; (c) add a new "Coachs" item and keep both. Recommendation: (a).

**Q5. Telling the admin when the welcome mail failed.** In 14.1 the 201 never says if the mail went out. For a coach created by an associate, a silent failure means the coach never hears about the account (and there is no "resend" in scope). Options: (a) same as 14.1, warning in the logs only; (b) add `welcomeMailSent: true|false` to the 201 and show "Mail non envoyé, prévenez le coach" in the screen. Recommendation: (b).

Other points the design must settle without asking (recommendations): USER_ROLE `"COACH"` upper case like `"MEMBER"`; COACH_IS_ACTIVE and USER_IS_ENABLED true; 409 code in `details.reason` like 14.1 (for example `EMAIL_TAKEN`), or in `error` like the admin exercises: pick one and document it; audit entity id = COACH_ID.
