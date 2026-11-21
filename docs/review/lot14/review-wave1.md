# Lot 14 wave 1 review (14.1, 14.1b, 14.2, 14.3)

Read-only single pass, 21/11/2026. Branch feature/lot_14 against origin/main.

## 1. Scope

- origin/main is d727ba11ba74. The tree was clean. The branch holds exactly the 9 expected commits: 3c3e7ff, 1f8d31e, 46ade63, 6cf95b1, 4783a87, 3a938eb, f2039b6, 4ada1a8, 40b5c27.
- Whole diff: 53 files, 10358 insertions, 19 deletions. Excluded from reading: the two migration Designer files (about 8100 lines), the snapshot (read hunks only: +6 lines, the new column, matches the 14.2 migration; 14.1 has no model change, which is right for a raw SQL index), and docs/review/** except the runbook and the two precheck.sql files.
- Code files read in full: AuthService.cs, CoachMemberInviteService.cs, MemberAddedEmailBuilder.cs, the two options classes and validators, the two rate limit classes, both controllers, AddMemberRequestValidator, both migrations, the entity configurations of USERS, MEMBERS, MEMBER_COACHES, AUTH_OTPS, GlobalExceptionMiddleware, SmtpEmailSender, RoutingEmailSender, LoggingEmailSender, EmailOptions, and the pipeline part of Program.cs.
- `show --stat 1f8d31e`: 2 files (AuthService.cs, docs/review/lot12/12.5a/prod-runbook.md), as expected.
- `show --stat 3c3e7ff`: 3 files (docs/review/lot14/14.3/cleanup-commit.sql, cleanup-dryrun.sql, runtime-evidence.md), as expected.
- Build: `dotnet build --no-incremental`: 0 error, 19 warnings, none in a file touched by the lot (all 19 are inherited XML comment warnings such as CS1573 in other controllers).

## 2. Verdict

No BLOCKER. Ready to deploy to api-demo and PROD. Two SHOULD items are worth a look first: W1 (a one line runbook addition, no code) and W2 (small code change, low exploitability).

## 3. Findings

### SHOULD

**W1. The login code can reach the PROD logs through the Logging mail provider**
- Severity: SHOULD. Confidence: high on the mechanism, medium on the likelihood.
- File: Mentora.Infrastructure/Services/Email/LoggingEmailSender.cs:9-11 and Mentora.Core/Options/EmailOptions.cs:13.
- What: the `[EMAIL-LOG]` line prints the full text body, which holds the login code. The provider defaults to Logging when `Email__Provider` is missing or misspelled (anything other than Smtp or Logging is refused, but empty means Logging). If api-prod ever starts without `Email__Provider=Smtp`, every code is written to the PROD logs, which defeats 14.3. The runbook proof (step d) counts only the `[OTP]` marker, so it would not catch this.
- Why it matters: 14.3 promises that PROD logs never contain a login code. This is the one remaining path.
- Minimal fix: no code needed for the deployment. In runbook step d, also count `[EMAIL-LOG]` on api-prod (expected 0), and keep the `provider=Smtp` check of the Bascule step 3. A code fix, if wanted later: log the body in LoggingEmailSender only when the environment is Development.

**W2. Wrong-code attempts are counted after the check, so parallel guesses can exceed 5**
- Severity: SHOULD. Confidence: high that the race exists, low that it is practically exploitable.
- File: Mentora.Infrastructure/Services/AuthService.cs:116-135.
- What: the candidates are loaded with `AttemptCount < 5`, then BCrypt is verified in memory, and only afterwards the counter is incremented (ExecuteUpdate with the same `< 5` condition). The increment is atomic and the counter never passes 5, so the stored value is safe. But N parallel requests that all loaded the code before the first increment each evaluate one guess. So more than 5 guesses can be evaluated, up to the burst size. The BCrypt cost widens the window.
- Why it matters: bounded by the IP rate limit (30 calls per 15 minutes per IP, shared by both OTP routes) and by the 900000 possible codes, so one IP gets about 30 guesses per code at best. A spread of many IPs is the only real danger.
- Minimal fix: take the attempt first, then verify. One conditional `UPDATE ... SET attempts = attempts + 1 WHERE id = ... AND attempts < 5` per candidate, and evaluate the guess only when a row was updated (and give the attempt back on success, or ignore it since the code is consumed). Can wait for after the Beta if the deployment date is tight.

### BACKLOG

**W3. Add route does not check MEMBER_IS_ACTIVE** (CoachMemberInviteService.cs:97-112). A member with `MemberIsActive = false` can be linked to a coach but never shows in the coach lists (CoachMeService.cs:35 filters on it). Rare; fix by adding a 409 or by reactivating on purpose. Confidence: high.

**W4. Lowercasing differs between C# and PostgreSQL for non ASCII letters** (EmailNormalizer.cs:10 uses ToLowerInvariant, the lookups translate to lower() in SQL). On a database whose locale does not lower non ASCII letters, an email with an accented capital would not match itself. Team and Beta emails are ASCII. Confidence: low.

**W5. Precheck and runbook gaps** (docs/review/lot14/14.1/precheck.sql). It has no `BEGIN READ ONLY ... ROLLBACK` wrapper (the 14.2 file has one; both only contain SELECT, so no risk), and query 1 and 2 print the stored email values and user ids on screen (they are team accounts, but the output should not be pasted anywhere). Neither precheck verifies the last applied migration: see section 5 for the extra SQL. Confidence: high.

**W6. Names are stored with control characters** (CoachMemberInviteService.cs:40-41 trims only). They are cleaned wherever they are put in a mail (AuthService.SanitizeFirstName, MemberAddedEmailBuilder.CleanName), so no header injection. Other readers (coach back office) may display odd characters. Confidence: medium.

**W7. The OTP rate limit depends on the real client IP** (Program.cs:498-503, 614). KnownProxies and KnownNetworks are cleared, so X-Forwarded-For is trusted from anyone. Behind nginx this is correct (nginx appends the real address and the default ForwardLimit of 1 takes the last entry). If the API container port is also reachable directly from the internet, a client could rotate the header to skip the limit. To check on the VPS: the api and api-prod ports are bound to 127.0.0.1 or only to the Docker network. Not changed by this lot. Confidence: medium.

**W8. 14.1 response email for an existing member** (CoachMemberInviteService.cs:125) returns the stored value, which could contain capitals on old rows, while a new account returns the normalized value. Harmless. Confidence: high.

## 4. Verified OK

A. Email normalization
1. Every USERS email lookup in code: AuthService.cs:42-48 (request), :106-111 (verify), CoachMemberInviteService.cs:39,53 (add). Each normalizes with EmailNormalizer and compares `UserEmail.ToLower()`. No exact comparison is left (git grep of UserEmail and `.Users`). Other hits are projections or by id (AccountService, CoachMeService, MemberMeService, MemberParameterService). Admin code and AdminUserSeeder use ADMIN_USERS, a separate table with its own lowercase CHECK (Lot12_1 migration), out of scope. No seeder writes USERS.
2. The old IX_USERS_USER_EMAIL is kept (UserConfiguration.cs:21). UX_USERS_EMAIL_CI is created in the 14.1 migration. An existing row with capitals is still found by the lower() lookup, and the add route finds it too (same lookup under the lock) so it cannot create a second account; the unique index is the backstop.
3. Contracts (docs/review/lot14/14.1/contract-before.txt and contract-after.txt, and 14.2): same bodies, status codes and key order for every case. The only change is the capitals request, 400 before and 200 after, as accepted.

B. Add member
1. Transaction with the lock as the first statement (CoachMemberInviteService.cs:45-48), before any read that decides. All reads and writes use the same context and transaction, one SaveChanges per branch, the commit at :130; any throw leaves the `await using` block and rolls back. Two concurrent adds of the same new email: the second waits on the lock, then its lookup (a new statement, READ COMMITTED) sees the committed user. It returns 409 ALREADY_ATTACHED if it is the same coach, or links as a second coach otherwise. The unique index UX_USERS_EMAIL_CI would turn any path outside the lock into an error instead of a duplicate. The lock key 7141401 differs from the other one in the code (AdminExerciseService.cs:32, 7122002).
2. 409 order at :99-104: ACCOUNT_DISABLED, EMAIL_IS_COACH, NO_MEMBER_PROFILE, then ALREADY_ATTACHED at :111. The 201 body order follows the AddMemberResponse record (memberId, email, firstName, lastName, isNewAccount, isPrimary, phone, gender, heightCm, birthDate), wrapped in the standard envelope (CoachMembersController.cs:38).
3. IS_PRIMARY: the MEMBER_COACHES table has no active or deactivated flag (MemberCoach.cs), so there is no deactivated link; a removed link is a deleted row. `links.Count == 0` gives primary only for the first coach. A unique index on (member, coach) (MemberCoachConfiguration.cs:30) prevents duplicates.
4. New account columns: USERS gets id, created date, email, enabled, role "MEMBER" (no CHECK on USER_ROLE in any migration, no C# code reads it); MEMBERS gets names, created date, active, has activated false, optional fields; MEMBER_COACHES gets id, member, coach, primary, started. All NOT NULL columns without default are set (inventory 14.0 plus the configurations). MEMBER_PARAMETERS is created lazily by MemberParameterService.cs:26,49, so a new member works without it. Local runtime evidence (14.1) already shows the flow.
5. First login: AuthService.cs:154-158 sets HAS_ACTIVATED and the date once, saved in the same transaction (CreateRefreshTokenAsync flushes). The coach path is untouched.
6. 14.1b: the validator reuses the shared pattern, message constants and the public plausibility check (AddMemberRequestValidator.cs:36-48, UpdateMemberParameterRequestValidator.cs:12-14,31,42). Optional fields are written only in the new account branch (:79-82); the existing branch writes nothing to MEMBERS and returns stored values (:124-127). MemberParameterService.cs:85 now delegates to PhoneNormalizer with the same trim and blank-to-null body.
7. Welcome mail: sent after the commit (:133), inside try/catch, with a warning that carries ids and the exception type only (:170,175). The coach name and member email are HTML encoded in the HTML body; control characters are removed from names and the subject (MemberAddedEmailBuilder.cs:20-26, AuthService.SanitizeFirstName). The email cannot contain whitespace or control characters (validator :54), so the text body is safe. InstallUrl is validated absolute https with no whitespace at startup (MemberInviteOptions.cs:30-37), HTML encoded in the link, and never logged (the [INVITE] line prints a boolean). The mail goes through IEmailSender, registered as the RoutingEmailSender decorator (Program.cs:432-440), so TEST mode redirection applies.
8. CoachAddMember limiter: partition key is the coachId claim with a fixed "no-coach" fallback (MemberInviteRateLimiting.cs:23-25). The limiter runs after authentication and authorization (Program.cs:631-633), so a 401 or 403 never consumes quota. 429 body and Retry-After come from the shared OnRejected (QuizRateLimiting.cs:40-52), as for VoiceUpload.
9. Route: POST on api/v1/coach/members does not collide with the existing routes that extend it with a member id (conversation, parameters, voucher-grants). Group "coach", policy CoachOnly. The member appears in the coach lists through MEMBER_COACHES (CoachMeService.cs:35).

C. Login code protection
1. Counter: one atomic ExecuteUpdate on the loaded candidates (AuthService.cs:131-133). A code with 5 attempts is excluded from the candidates by the query (:119), so even the right code cannot consume it. Parallel wrong attempts: the stored count cannot pass 5. The evaluation race is W2.
2. Right code: single conditional ExecuteUpdate (not used, attempts under 5, not expired) at :145-148 inside the transaction opened at :143, with the activation and the refresh token; 0 rows throws the same 400 text as a wrong code. Two parallel verifies with the same right code: the second UPDATE waits for the row lock, re-evaluates the condition after the first commit and updates 0 rows, so exactly one 200.
3. No execution strategy: Program.cs:162-163 is a plain `UseNpgsql(dataSource)`, and EnableRetryOnFailure appears nowhere in the solution. User transactions are compatible, so the 14.1 and 14.2 transactions work.
4. A new request invalidates previous unused codes (AuthService.cs:53-58, unchanged).
5. AppAuthOtp: partition by RemoteIpAddress with "no-ip" fallback (AuthOtpRateLimiting.cs:23); UseForwardedHeaders is the first middleware (Program.cs:614). Applied on otp/request and otp/verify only (AuthController.cs:18,33), one shared counter per IP for both routes. Refresh and logout are not limited, which is fine: refresh needs a valid secret token and logout needs a valid JWT. 429 envelope and Retry-After are the shared ones. The admin limit is unchanged (AdminAuthRateLimiting.cs not in the diff). See W7 for the IP assumption.
6. Migration Lot14_2_OtpAttempts: integer NOT NULL default 0, CHECK CK_AUTH_OTPS_ATTEMPT_COUNT created in Up and dropped (IF EXISTS) before the column in Down. The configuration has HasDefaultValue(0) and the entity initializer is 0 (AuthOtp.cs). Snapshot hunk matches.

D. 14.3
1. The only write of the plain code outside the mail bodies is AuthService.cs:73, under `environment.IsDevelopment()` (:72). No other Console call exists in the solution outside migrations (grep). AuthService logs no code. `[EMAIL-LOG]` exists only in LoggingEmailSender.cs:11 (and prints the body: see W1). The admin auth service has no code write.
2. IHostEnvironment is registered by the generic host in every host; the only host that builds AuthService is the API (Program.cs:260). No test project exists in the solution (4 csproj: Admin, API, Core, Infrastructure) and `new AuthService(` appears nowhere.
3. Production routing: RoutingEmailSender.cs:36-47 keeps the real recipient in To and appends the Bcc addresses, skipping any address already in To or already in the Bcc list (no duplicate). SmtpEmailSender.cs:78-81 adds Bcc to the MimeMessage and sends through MailKit SmtpClient, which puts it in RCPT TO and hides the Bcc header in the transmitted message. No custom FormatOptions or header copy is used. The OTP subject carries only the sanitized first name (AuthService.cs:78-80). The Production mode requires at least one Bcc address (EmailRoutingOptions.cs:47-48).

E. Mobile contract: the only API changes are the new route POST /api/v1/coach/members and the new 429 on otp/request and otp/verify, plus the capitals email now accepted. No DTO, route or JSON field of the app was renamed, removed, reordered or made required (diff of Core DTOs: two new files only). One data behaviour to be aware of, not a contract change: HasActivated in the member profile now becomes true at the next login of members that never logged in since the 14.1 code (AuthService.cs:154-158).

F. Migrations
1. Lot14_1_UserEmailCi: only `CREATE UNIQUE INDEX "UX_USERS_EMAIL_CI" ON "USERS" (lower("USER_EMAIL"))` by raw SQL in Up, DROP INDEX IF EXISTS in Down; no model change, snapshot untouched, consistent.
2. Prechecks read, see W5 and section 5.
3. At d727ba1 the last migration is 20261117234001_Lot13_2_VoiceMessages (same on origin/main). The two new ones have later timestamps (20261119221223, 20261120193442) and apply in order at one startup, inside MigrateAsync (Program.cs:595) before every seeder. A second startup applies nothing. Both are in the snapshot order.

G. Options: both classes use IValidateOptions with ValidateOnStart (Program.cs:392-404), and `.Value` is read before the migrations (Program.cs:521-536, migration at :595). Messages name the setting and never a value. Defaults 30 per 60 and 30 per 15 with no InstallUrl are valid, so a Production start without any variable works. Log lines (Program.cs:524-526 and 534-536): `[INVITE] installUrl={bool} permitLimit={n} windowMinutes={n}` and `[AUTH] otpRateLimit permitLimit={n} windowMinutes={n}`.

H. Runbook (added lines): variable names match the section names MemberInvite and AuthRateLimit and the existing EmailRouting names; the migration names are exact; the [INVITE] and [AUTH] lines match the code literally; the [EMAIL] routing line matches Program.cs:516-518. Rollback claim is true: the previous image never mentions the new column, so inserts into AUTH_OTPS take the database default 0 and selects list only known columns; no code at d727ba1 creates USERS rows (the first creating path is the new add route; only SQL scripts do), so the new index cannot reject an insert from the old image; MigrateAsync ignores history rows it does not know. Users created by the new route have lowercase emails, so the old exact-match login finds them. Added lines have 0 email addresses, 0 dashes, 0 secret values.

I. Public repo (diff-wide `git diff --name-only -G`): JWT shape: empty (control: docs/review/lot7 claim files hit on main, 2 lines each). OTP marker: 5 files (AuthService.cs, prod-runbook.md, 14.0 inventory, 14.2 and 14.3 runtime evidence); in all added lines there is no marker followed by 6 digits (count 0), they only name the marker. gmail: empty (control: 11.4 evidence hits). web2success: empty. mentorapp: 1 file (14.0 inventory), the only host in added lines is the public API host, already present on main in 13 files; admin and demo hosts also already on main. PROD account id prefixes: empty. Control for web2success and mentorapp: docs/review/lot8/vps/cleanup-vps.sql hits (6 lines each) on main. The fixtures use example.com placeholders. No MUX, Firebase or LiveKit secret and no signed URL in the added lines (count 0). Configuration files touched by the lot: none (no appsettings, .env, docker-compose, nginx, Dockerfile, .dockerignore or .gitattributes in the diff; so no appsettings key was added).

J. Dashes: 0 in the added lines of the diff (U+2014 and U+2013, control on a known string returns a match).

K. Build: 0 error, 19 warnings, none in a touched file.

## 5. Deployment

### SQL before (both bases, read only, counts only)

The two prechecks are fine as they are: 14.1 query 1 must return no row (stop otherwise; it prints stored emails, do not paste the output); 14.2 only counts. Add this one, to run on mentora_prod and mentora_db:

```sql
BEGIN READ ONLY;
SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1;
-- expected: 20261117234001_Lot13_2_VoiceMessages (anything else: stop)
SELECT count(*) AS case_collisions
FROM (SELECT lower("USER_EMAIL") FROM "USERS" GROUP BY 1 HAVING count(*) > 1) t;
-- expected: 0
SELECT count(*) AS emails_with_capitals FROM "USERS" WHERE "USER_EMAIL" <> lower("USER_EMAIL");
-- informational
ROLLBACK;
```

### Expected log lines

First startup, on each base: `Applying migration '20261119221223_Lot14_1_UserEmailCi'`, then `Applying migration '20261120193442_Lot14_2_OtpAttempts'`, `[EMAIL] routing mode=TEST ...` (before the switch of 24/11), `[INVITE] installUrl=False permitLimit=30 windowMinutes=60`, `[AUTH] otpRateLimit permitLimit=30 windowMinutes=15`, the usual lines (`[CORS]`, `[AUDIO]`, `[ADMIN]`), `Application started`, and no `fail:` or `crit:`.

Second startup (any recreate): the same `[INVITE]`, `[AUTH]`, `[EMAIL]` lines and no `Applying migration` line for the two lot 14 migrations.

### Rollback

Previous image per service (runbook section "Lot 14 vague 1", step e). No Down: both migrations are additive and the previous image works with them (verified in H). Members created by the add route and attempt counters stay; they are harmless for the old code. To go back to the exact previous data state, restore the backups taken before the deployment.

### Runbook corrections

1. Step d of "Lot 14 vague 1": also count `[EMAIL-LOG]` on api-prod (expected 0, never print the lines). Closes W1.
2. Step a: add the last migration check above, and say that the output of 14.1 query 1 and 2 contains email values (do not paste it).
3. Step c: add the line "second startup: no `Applying migration` for the two lot 14 migrations".
4. Optional, closes W7: add a check that the api and api-prod ports are not published to the internet (published only on 127.0.0.1 or not published).
