# Lot 14 wave 2 review: 14.8a admin coaches API and 14.8b admin "Coachs" screen

Single-pass, read-only review. Reviewer: Claude. Date: 2026-11-23. Nothing was fixed.

## 1. Scope

Branch `feature/lot_14`, tree clean, on top of `origin/main` 56a8b01 (wave 1). Exactly 5 commits, as expected:

- 3714723 Lot 14.8b: browser test evidence and cleanup
- 5bf7864 Lot 14.8b: admin Coachs screen (list and creation)
- 208c4db docs(lot14): 14.8a runtime evidence
- 76adae6 feat(lot14): 14.8a admin API to list and create coaches
- f7e50b8 docs(lot14): 14.8.0 coach screen inventory

Diff stat (whole diff, docs included): 29 files, 6146 insertions, 38 deletions.

- Reviewed in full: 19 code files (AdminCoachesController, AdminCoachService, CoachWelcomeEmailBuilder, CoachDefaults, UserEmailLock, validator, DTOs, migration, enum mappings, the two seeders, CoachMemberInviteService hunk, API Program.cs hunk, and the 7 Mentora.Admin files).
- Excluded as asked: `*.Designer.cs` (4090 lines, checked against the snapshot, see D), `*ModelSnapshot.cs` (not in the diff), `*.csv`, `docs/review/**` (read only for pattern searches H and I).
- No configuration file is touched by the wave: no appsettings, `.env`, docker-compose, nginx, Dockerfile, `.dockerignore`, `.gitattributes`, `.gitignore`, no `.csproj`, nothing under `Mentora.Admin/wwwroot`.

## 2. Verdict

**Ready to deploy.** No BLOCKER. One SHOULD (welcome mail can make the 201 late, V1, cheap to fix). Four BACKLOG items.

## 3. Findings

### V1 (SHOULD) The welcome mail is sent inline with no time limit, so the screen can report an error for a coach that was created

- Where: `Mentora.Infrastructure/Services/AdminCoachService.cs:190` (awaited before the 201), `:207-213` (token is `CancellationToken.None`), `Mentora.Infrastructure/Services/Email/SmtpEmailSender.cs:35-38` and `:26-61` (connect, authenticate, send, 2 attempts with a 2 second pause, `Mentora.Core/Options/EmailOptions.cs:60-61`).
- What: the creation is committed, then the request waits for the SMTP exchange. If the SMTP server is slow or silent (not refusing), each attempt can last as long as the MailKit socket timeout (2 minutes by default, my knowledge of MailKit, not run here) and there are 2 attempts. The Blazor `HttpClient` of the admin (`Mentora.Admin/Program.cs:17`) keeps its default 100 second timeout.
- Why it matters: on Tuesday the team creates the Beta coaches on PROD. With a stuck SMTP server the screen shows "Le serveur ne répond pas, réessayez." while the coach exists. The team retries and gets "Cette adresse est déjà celle d'un coach." They will think the creation failed and did not send the mail. Data is correct, the message is misleading. The accepted decision is "mail best effort"; this finding is only about how long the 201 may wait.
- Minimal fix: in `SendWelcomeMailAsync`, use `using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15))` and pass `cts.Token` instead of `CancellationToken.None`. `SmtpEmailSender` rethrows the cancellation, and the existing `catch (Exception)` at `:219` already logs a warning with ids only and keeps the 201.
- Confidence: medium (the code path is certain; the 2 minute default is from MailKit documentation, not tested here).

### V2 (BACKLOG) A first or last name holding a NUL character gives a 500

- Where: `AdminCoachService.cs:88-89`, validator `Mentora.Core/Validators/AdminCoach/AdminCreateCoachRequestValidator.cs:27-37` (only emptiness and length are checked).
- What: `"A\u0000B"` passes validation (`Trim` does not remove it). PostgreSQL refuses U+0000 in `text` and in `jsonb` (the audit `changes` column, `AdminAuditLogConfiguration.cs:45-46`), so `SaveChangesAsync` throws a `DbUpdateException` that is not the email violation, and the middleware answers 500. The transaction is rolled back, nothing is written. Other control characters (line breaks) are accepted and stored in the name, which is only cosmetic; the mail subject is protected by `SanitizeFirstName` (see C2).
- Why it matters: cannot be typed in the screen; only a hand-made API call. Admin only.
- Fix: reject control characters in `FirstName` and `LastName` in the validator, with the same 422 key.
- Confidence: medium (PostgreSQL behavior known, not run).

### V3 (BACKLOG) The list route does not check that the admin is still enabled

- Where: `AdminCoachService.cs:41-78` (no `EnsureAdminAsync`), against `:100` for the creation.
- What: a disabled or deleted admin keeps a valid access token until it expires (15 minutes by default, `Mentora.Core/Options/AdminAuthOptions.cs:16`). During that time the token can still list every coach with email and phone. The exercise reads behave the same way (`AdminExerciseService.cs:420, 502, 571` check only writes), but exercise reads return no personal data.
- Fix: call `EnsureAdminAsync` in `ListAsync` (needs the admin id passed from the controller, as for the creation).
- Confidence: high on the fact, low on the impact (4 associates, 15 minutes).

### V4 (BACKLOG) Every mail sent logs the recipient address and the subject

- Where: `SmtpEmailSender.cs:41-42, 51-53, 63-65` (`To={To} Subject={Subject}`).
- What: existing behavior for OTP and member mails, now also for the coach welcome mail (subject carries the coach first name). The 14.8a comment "never the address" is true for `AdminCoachService` only (`:216-222`).
- Why it matters: PROD logs hold coach addresses and first names. Not a secret, but personal data in logs.
- Fix (later lot): log the mail kind and a hash or ids instead. No action before the deployment.
- Confidence: high.

### V5 (BACKLOG) Welcome mail to an address with non-ASCII characters is not tested

- Where: `AdminCreateCoachRequestValidator.cs:46-56` accepts any non-ASCII local part or domain; `CoachWelcomeEmailBuilder` and MailKit handle it, but no run proved it.
- Why it matters: such a coach would be created but could receive no mail (best effort, logged). Unlikely for the Beta.
- Fix: none needed now. Check the mail log line if a coach reports no mail.
- Confidence: low.

## 4. Verified OK

### A. List route
1. `AdminCoachesController.cs:16` uses `AdminAuthDefaults.Policy`, which is the `AdminBearer` scheme only plus the admin id claim (`Mentora.API/Program.cs:239-243`, comment "an app token is rejected with 401"). Routes: `api/v1/admin/coaches` is unique among admin controllers (`AdminAuthController` has `api/v1/admin/auth`, the exercise controllers use `api/v1/admin` with `exercises...`). The admin id comes from the same claim as on the exercise routes (`:60-65`, missing claim gives 401). Disabled admin: write refused (`:100`, `:229-234`), read not checked (V3).
2. Keyset: order `COACH_CREATED_DATE DESC, COACH_ID DESC` (`AdminCoachService.cs:57-58`), condition `date < cursor OR (date = cursor AND id < cursor id)` (`:51-52`), which is the exact keyset of that order, ties included. The generated SQL was captured in `docs/review/lot14/14.8a/runtime-evidence.md:121`. Cursor: Base64 of JSON with `ToString("O")` (7 digits) parsed with `RoundtripKind` (`:259-261`, `:271-273`); timestamptz keeps microseconds and the value read back is already whole microseconds, so the round trip is exact; created rows use a microsecond rounded `now` (`:117-118`). Kind: values read from timestamptz are UTC; the decoded cursor is forced to UTC (`:276-278`). Bad Base64, bad JSON, bad GUID, null payload, empty string: all end as `InvalidOperationException` (`:270-283`), which the middleware maps to 400 (`GlobalExceptionMiddleware.cs:73-77`). Limit outside 1 to 100: same exception (`:43-44`). Proven at runtime: evidence rows 5c and 13.
3. One query per page: member count is a correlated subquery in the same `Select` (`:70`), captured as exactly one `Executed DbCommand` (14.8a evidence line 110). `memberCount` counts `MEMBER_COACHES` rows of the coach whose member has `MEMBER_IS_ACTIVE = true`. The link has no active flag (`MemberCoachConfiguration.cs:18-25`). It is the same rule as the coach routes (`CoachMeService.cs:35`).
4. `isEnabled` is `USERS.USER_IS_ENABLED` (`:68`), `isActive` is `COACHES.COACH_IS_ACTIVE` (`:69`). The team SQL scripts of 17/11 and 19/11 that created the PROD coaches are not in the repository, so I could not confirm they set both flags to true. See the deployment SQL in section 5.

### B. Creation
1. The lock is the first statement of the transaction (`:95-98`), before the admin check and the email lookup. Validation before it reads nothing from the database. `CoachMemberInviteService.cs:42-45` takes the same lock first; the constant moved to `UserEmailLock.Sql` with the identical text `SELECT pg_advisory_xact_lock(7141401)` (diff of both files). One context, one transaction, one `SaveChangesAsync` (`:176`), commit after it (`:177`); any throw leaves the `using` block and rolls back. Concurrency: both writers queue on the lock. The second one starts its lookup after the first commit (READ COMMITTED, new snapshot per statement), finds the row and returns 409 `EMAIL_TAKEN` (coach creation) or `ALREADY_ATTACHED` or `EMAIL_IS_COACH` (member add). Two USERS rows cannot appear; the unique index `UX_USERS_EMAIL_CI` (`20261119221223_Lot14_1_UserEmailCi.cs:16`) is the net below.
2. Safety net: `catch (DbUpdateException ex) when (IsEmailViolation(ex))` (`:183`) wraps the whole block; the transaction is disposed, so rolled back, before the catch runs. It only matches SQLSTATE 23505 on the constraint names `UX_USERS_EMAIL_CI` or `IX_USERS_USER_EMAIL` (`:248-251`). A 23505 on another index (COACHES user id, COACH_PARAMETERS coach id, active offer program name) is not turned into EMAIL_TAKEN; it stays a 500. The details are `null` fields; the screen shows "Cette adresse est déjà utilisée." (`Form.razor` mapping).
3. Rows written: USERS (id, created date, email lower case, `USER_IS_ENABLED` true, role `"COACH"`, `:122-129`), COACHES (names, phone, created date, active true, user id, `:130-139`), COACH_PARAMETERS (rate, delay, both dates, `:140-148`), 4 OFFER_PROGRAMS (name, active, created date, coach id, `:149-159`), ADMIN_AUDIT_LOG (`:161-174`). Compared with the entity configurations: every required column without default is set (`UserConfiguration.cs:23-33`, `CoachConfiguration.cs:20-24`, `CoachParameterConfiguration.cs:73-83`, `OfferProgramConfiguration.cs:25-36`); the other columns are nullable or have a database default (visio flags, locations `'[]'`, language `FR`, notifications, booking limits, `SKIP`), the same rows the seeders produce. Name lengths 100, phone 20 (pattern 8 to 20), email 255, offer program name 80 are all within the column sizes. No CHECK on USERS or COACHES exists in the migrations (search `CK_USERS` gives nothing), so no CHECK can be violated. `Guid.NewGuid()` ids are set client side; the table defaults are not needed.
4. `CoachDefaults` (`Seeding/CoachDefaults.cs:8-18`): 50.00, 24, and the 4 names `Prise de masse`, `Perte de poids`, `Préparation Marathon`, `Préparation Hyrox`. Before the refactor the seeders held 50.00m, 24 and the same 4 names (diff of `CoachParameterSeeder.cs` and `OfferProgramSeeder.cs`, removed array identical, same order). No change of value; no app contract change.
5. Login: `AuthService.cs:211-212` auto-detects a coach-only account as coach, `VerifyOtpAsync` issues the coach token with `user.Coach!.CoachId` (`:174-176`); the user is enabled (`:111`). `coach/me` needs COACH_PARAMETERS (`CoachMeService.cs:29-31`), which the creation writes. Add member refuses a coach email with `EMAIL_IS_COACH` (`CoachMemberInviteService.cs:99`). Creation answers 409 with `existingAccount` COACH, MEMBER, BOTH or NONE (`:109-113`). Proven at runtime in the 14.8b browser test (B8, B9, B11) and the 14.8a evidence.
6. Validation (`AdminCreateCoachRequestValidator.cs`): email required, 255, valid shape without whitespace, control character, comma, semicolon, angle bracket or quote; names required, 100; phone uses `UpdateMemberParameterRequestValidator.PhonePattern` like the member add (`AddMemberRequestValidator.cs:37`). Error keys come from the property names: Email, FirstName, LastName, Phone (`GlobalExceptionMiddleware.cs:37-41`).
7. Audit row in the same save (`:174`, `:176`): `COACH_CREATED`, entity `COACH`, entity id = coach id, admin id set (`:236-246`). `AdminAuditMappings` updated with `if` chains in the four methods (`AdminAuditAction.cs`, diff), no `switch`.

### C. Welcome mail
1. After the commit (`:177` then `:190`), in a `try` that catches everything and logs only `CoachId`, `AdminUserId` and the exception type name (`:200-224`); the 201 is kept. (Time limit: V1.)
2. HTML: first name, e-mail, install URL and all fixed texts go through `WebUtility.HtmlEncode` (`CoachWelcomeEmailBuilder.cs:29-35, 44-52`). The subject uses `AuthService.SanitizeFirstName`: drops every `char.IsControl` character (CR, LF included), trims, caps at 50 (`AuthService.cs:94-102`). That is the rule that prevents header injection; the address itself cannot hold control characters (validator).
3. Sent through `IEmailSender`, so `RoutingEmailSender` applies TEST or Production routing. `InstallUrl` is never logged (`AdminCoachService.cs:216-222`) and is validated at startup as absolute `https` (`MemberInviteOptions.cs:29-36`); empty gives the fallback sentence (`CoachWelcomeEmailBuilder.cs:31-33, 62-64`).

### D. Migration `Lot14_8_CoachCreatedAudit`
1. Up (`...CoachCreatedAudit.cs:16-30`): action CHECK has the 6 values of `Lot12_3a_MuxVideos.cs:110` plus `COACH_CREATED`; entity CHECK is `('EXERCISE','COACH')`; constraint names `CK_ADMIN_AUDIT_LOG_ACTION` and `CK_ADMIN_AUDIT_LOG_ENTITY_TYPE`, identical to `Lot12_2b_AdminAuditLog.cs:55, 62`.
2. Down (`:34-64`): a `DO` block raises an exception before any change when a COACH row or `COACH_CREATED` exists; otherwise restores `('EXERCISE')` and the 6 previous actions exactly (same text as `Lot12_3a_MuxVideos.cs:110`).
3. Widening a CHECK cannot fail on existing rows: Federico's exercise and video rows have actions inside the new list. `ADD CONSTRAINT ... CHECK` scans the table once; the table is tiny. Note: both `DROP CONSTRAINT` statements have no `IF EXISTS`, so the migration fails (and the API refuses to start) if a name differs on a base. The names come from earlier migrations, so they should match; the read-only SQL below proves it before the deployment.
4. Designer vs snapshot: the model part of the new `.Designer.cs` is line for line the current `MentoraDbContextModelSnapshot.cs` (0 differing lines, checked by script); the snapshot is not in the diff, as the migration says (CHECKs are raw SQL).
5. Rollback: `mentora-api:lot14w1` never reads audit rows (`AdminAuditLogs` is only used with `.Add` in 5 places), writes only `EXERCISE_*` actions and entity `EXERCISE`, which the widened CHECKs accept. At startup EF applies only migrations missing from the history and ignores the extra history row (same reasoning as `prod-runbook.md:935`). `Down` is never needed.

### E. Admin front
1. `ShellLayout.razor` diff: order of the 10 items as decided, routes only for "Exercices et vidéos" and "Coachs"; `nav-active` and `aria-current` only when `IsOn(route)` (path equals `/route` or starts with `/route/`); disabled items keep `disabled` and `title="Bientôt disponible"`; title and subtitle for exercises, coaches and default as decided. No other change.
2. `CoachesApi.cs` goes through `ApiClient` (token, refresh before expiry, one refresh and one retry on 401, `ApiClient.cs:15-34`); `ApiTransport` logs nothing. Models match the DTOs (`AdminCoachDtos.cs:27-42`): same 10 names and types, `Phone` and `NextCursor` nullable. The API writes `DateTime` values of kind UTC as ISO text ending in `Z`; `ApiTransport` reads them with web defaults, so the kind is UTC and `ToLocalTime()` is right (browser test B10: 16:44 Paris for a creation at 15:44 UTC).
3. `List.razor`: one request of 50 per page (`PageSize`), "Afficher plus" appends and filters duplicates by `CoachId`, loading skeleton, error with "Réessayer", empty text, singular and plural footer, flash read once in `OnInitializedAsync`. Stale responses are dropped through the `_request` counter.
4. `Form.razor`: client checks and trim; blank phone becomes `null` through `Clean`; success sets the flash, then `_leaving = true` and `NavigateTo("coachs", replace: true)`; 409 mapping reads `existingAccount` and `isEnabled`; 422 maps the four keys case-insensitively and lists unknown keys in the banner; network error and 5xx give `ExerciseText.ServerDown`, other failures the generic text; `_saving` blocks a second submit and disables the button; `NavigationLock` plus `ConfirmDialog` as in the exercise form; `aria-invalid` and `aria-describedby` through `Invalid` and `Desc` with `FieldError`.
5. `git diff --stat origin/main...feature/lot_14 -- Mentora.Admin/wwwroot` prints nothing; no `style=` and no `using Mentora.Core|API|Infrastructure` in the new or changed admin files; the admin `.csproj` has no `ProjectReference` and is not in the diff; no NuGet change (no `.csproj` in the diff). So `index.html`, its CSP script hash, `admin.js`, the CSS and nginx stay valid.

### F. Mobile contract
No file under `Mentora.API/Controllers` or `Mentora.Core/DTOs` changes except the new admin ones (`AdminCoachesController`, `AdminCoachDtos`). No app route, DTO or JSON field (member or coach) changes. Served defaults are unchanged (see B4). Result: none, as expected.

### G. Startup
The only API startup change is `AddScoped<IAdminCoachService, AdminCoachService>()` (`Mentora.API/Program.cs:268-269`). No new option and no new environment variable (the mail uses `MemberInvite:InstallUrl`, validated as before). `[INVITE]` and `[AUTH]` lines come from code that is not in the diff.

### H. Public repository
`git diff --name-only -G<pattern> origin/main...feature/lot_14`:

| Pattern | Result | Control on origin/main |
|---|---|---|
| JWT shape | empty | `docs/review/lot7`: found |
| `[OTP]` marker | empty | `AuthService.cs`: found |
| `[EMAIL-LOG]` marker | `docs/review/lot14/14.8a/runtime-evidence.md` | n/a |
| `gmail.com` | empty | `lot11/11.4/runtime-evidence.md`: found |
| `web2success.fr` | empty | `lot8/vps/cleanup-vps.sql`: found |
| `mentorapp.fr` | `docs/review/lot14/14.8.0/inventory.md` | `lot8/vps/cleanup-vps.sql`: found |
| `13a00000-` and `19b00000-` | empty | n/a |

Hits: the `[EMAIL-LOG]` hit is line 12 of the 14.8a evidence, a sentence naming the log marker; the only address on that line is on the `mentora.fr` placeholder domain; no code or secret. The `mentorapp.fr` hits are lines 181 and 207 of the 14.8.0 inventory, URLs without a user part; the domain is already on `origin/main` in 29 files. Across all added lines, the only addresses have the domains `mentora.fr` (17) and `example.com` (16). The pattern for MUX, Firebase, LiveKit, signed URLs, API keys and private keys matches only the generated `20261123042911_Lot14_8_CoachCreatedAudit.Designer.cs`, whose text is the same as the snapshot already on main (model property names, no value). No secret and no signed URL. The wave touches no configuration file (list in section 1).

### I. Dashes
0 em dash and 0 en dash in the added lines of the whole diff (Designer, snapshot, csv excluded).

### J. Build
`dotnet build --no-incremental` of the solution: 0 errors, 19 warnings (same as before the wave). 0 warnings are located in a file touched by the wave.

## 5. Deployment

Order: api-demo first (API image, then its admin if used), then api-prod, then the admin on https://admin.mentorapp.fr. The API must come first: the new screen calls routes that the old API does not have.

### Read-only SQL before, on both bases (mentora_db for api-demo, mentora_prod for api-prod)
Inside `BEGIN READ ONLY; ... ROLLBACK;`, counts and names only, no address:
1. Last applied migration (same query 0 as wave 1): expected `20261120193442_Lot14_2_OtpAttempts`. Anything else: stop.
2. Names of the CHECK constraints of the audit table: `SELECT conname FROM pg_constraint WHERE conrelid = '"ADMIN_AUDIT_LOG"'::regclass AND contype = 'c' ORDER BY 1;` expected exactly `CK_ADMIN_AUDIT_LOG_ACTION` and `CK_ADMIN_AUDIT_LOG_ENTITY_TYPE` (the migration drops them by name). Otherwise stop.
3. State of the existing coaches (PROD only matters): count of coaches whose `USER_IS_ENABLED` is false, whose `COACH_IS_ACTIVE` is false, and without a `COACH_PARAMETERS` row. Expected 0 for the Beta coaches; this tells whether the screen shows "Actif" for the coaches the team created by script.
4. Backups of both bases, as in wave 1 step a.1.

Also check `MemberInvite__InstallUrl` on api-prod: if it is not set, the welcome mail carries the sentence "L'équipe Mentora vous enverra le lien pour installer l'application." instead of the button (valid, but the team should know before Tuesday).

### Expected startup lines
- First start, per base: `Applying migration '20261123042911_Lot14_8_CoachCreatedAudit'`, then the usual lines unchanged from wave 1: `[EMAIL] routing ...` (api-prod: `provider=Smtp`, mode TEST until Tuesday), `[CORS]`, `[AUDIO]`, `[MUX]`, `[INVITE] installUrl=... permitLimit=30 windowMinutes=60`, `[AUTH] otpRateLimit permitLimit=30 windowMinutes=15`, `Application started`; no `fail:`, no `crit:`.
- Second start (recreate): the same lines and no `Applying migration` line.
- Never print lines holding the OTP or EMAIL-LOG markers.

### Smoke test
On api-demo only: create one coach with an `example.com` address from the screen; in TEST mode the mail goes to the test recipients with the routing prefix. Remove it with a script modelled on `docs/review/lot14/14.8b/cleanup-commit.sql` (prefix of the test address, run start). On PROD, do not create a test coach: the first real creation is the proof; the mail goes to the TEST recipients until the switch of Tuesday.

### Admin publication
`index.html` is unchanged and nothing under `wwwroot` is in the diff, so the CSP hash in nginx stays valid. After the publish, compare the published `index.html` with the one on the VPS (runbook Admin sections B and I) and check that `/coachs` answers 200 through the fallback.

### Rollback
- API: put back the previous image tag (`mentora-api:lot14w1`) in `docker-compose.yml` and `docker compose up -d --no-deps --force-recreate` the service, as in the wave 1 section e. No `Down`: the old image works on the migrated base (D5). Coaches created meanwhile stay valid rows for the old image.
- Admin: put back the previous folder, as in the runbook Admin section K (the old admin has no Coachs item; the API routes stay unused).
- A coach created by mistake: delete with a reviewed script (audit row, offer programs, parameters, coach, user, in this order), never `Down` while a COACH audit row exists.

### Content for a new runbook section "Lot 14 vague 2" in `docs/review/lot12/12.5a/prod-runbook.md` (French, like the file)
- Titre : `## Lot 14 vague 2 (24/11)`; contenu : 14.8a (API admin : liste et création des coachs, mail d'accueil) et 14.8b (écran Coachs de l'admin).
- a. Avant de déployer : sauvegardes des deux bases ; lecture seule sur les deux bases : dernière migration `20261120193442_Lot14_2_OtpAttempts` (sinon arrêter), noms des deux contraintes CHECK de `ADMIN_AUDIT_LOG`, comptes des coachs désactivés ou sans paramètres.
- b. Variables : aucune nouvelle. Le lien d'installation du mail d'accueil est `MemberInvite__InstallUrl` (le même que pour les membres) ; sans lui le mail dit que l'équipe enverra le lien.
- c. Lignes de démarrage : `Applying migration` pour `Lot14_8_CoachCreatedAudit` une fois par base, puis les lignes habituelles, sans `fail:` ni `crit:` ; au démarrage suivant, aucune ligne `Applying migration`.
- d. Admin : publier après l'API ; `index.html` identique (empreinte CSP inchangée) ; `/coachs` répond 200.
- e. Contrôle : créer un coach de test sur api-demo (adresse `example.com`), contrôler la liste, le mail vers les adresses de test, puis nettoyer par script.
- f. Retour arrière : image précédente `mentora-api:lot14w1`, ancien dossier de l'admin ; aucune migration `Down` (refusée dès qu'une ligne COACH existe dans le journal) ; l'ancienne image fonctionne sur la base migrée.
