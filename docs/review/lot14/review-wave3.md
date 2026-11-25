# Lot 14 wave 3 review: 14.9a circuit modes and 14.9b push notifications

Single-pass, read-only review. Reviewer: Claude. Date: 2026-11-25. Nothing was fixed.

## 1. Scope

Branch `feature/lot_14`, tree clean, on top of `origin/main` 603aae8 (waves 1 and 2). Exactly 5 commits, as expected:

- 7853dd7 docs(lot14): 14.9b runtime evidence and SQL files
- e687ea0 Lot 14.9b: push notifications (NOTIFICATIONS outbox, FCM sender, 4 first events)
- 47dff4c docs(lot14): 14.9a runtime evidence and SQL files
- efd3b77 Lot 14.9a: 12 circuit modes, STANDARD alias normalised to SERIE_SIMPLE, data migration
- c6ac24f docs(lot14): 14.9.0 read-only inventory of block types and push notifications

Diff stat (whole diff, docs included): 53 files, 11527 insertions, 31 deletions.

- Reviewed in full: every code file of the diff (`CircuitModes`, `ProgramCircuitRules`, the 4 body DTOs, `ProgramCircuit`, `ProgramService`, `ProgramTemplateService`, `SessionTemplateService`, `ProgramTemplateSeeder`, both migrations, `Notification`, `NotificationConfiguration`, `NotificationTypes`, `NotificationTexts`, `INotificationComposer`, `NotificationComposer`, `PushNotificationSender`, `FcmClient`, `FirebaseServiceAccount`, `FirebaseOptionsValidator`, `FirebaseOptions`, `MentoraDbContext`, `ConversationService`, `SessionService`, `AuthService`, `Program.cs`, `.gitignore`, `.dockerignore`), with the whole files read where a hunk needed context (`ConversationService` send paths, `SessionService` reserve, cancel and participant methods, `AuthService.VerifyOtpAsync`, `ProgramService` assign and id-based PUT, `ProgramTemplateService`, `UserDeviceService`, `Program.cs` 505 to 664, `Mentora.API/Dockerfile`).
- Read because they will run or be published: `docs/review/lot14/14.9a/precheck.sql`, `docs/review/lot14/14.9b/check-proofs.sql`, `cleanup-local.sql` and `cleanup-local-commit.sql` of both sub-lots.
- Excluded as asked: `20261124221921_Lot14_9a_CircuitModes.Designer.cs` (4090 lines) and `20261124234929_Lot14_9b_Notifications.Designer.cs` (4185 lines), compared by script only (see B1); `MentoraDbContextModelSnapshot.cs` (95 added lines, read only to compare with the migration); no `*.csv` in the diff; the other `docs/review/**` files were read only for the searches of H and I.
- `Mentora.API/appsettings.json` changed: one key added, `Firebase` with `Firebase:ProjectId` (value not repeated here). No `.env`, no appsettings of another environment.

## 2. Verdict

**Ready to deploy, no BLOCKER**, provided the Firebase key file is mounted and `Firebase__ServiceAccountPath` is set on both API services before they are recreated (section 5). Two SHOULD items (N1, N2) are cheap and worth fixing before the image is built; both can only trigger under rare conditions, described below. Ten BACKLOG items.

## 3. Findings

### N1 (SHOULD) `NotificationTexts.Fit` can cut a surrogate pair, and the save of the business action would then fail

- Where: `Mentora.Core/Notifications/NotificationTexts.cs:78-79` (`value[..maxLength]`, no surrogate guard), used for every title and body at `Mentora.Infrastructure/Services/Push/NotificationComposer.cs:141-142`. The 120 character chat cut has the guard (`NotificationTexts.cs:57`); `Fit` does not.
- What: names are up to 100 characters each (`MemberConfiguration.cs:20-21`, `CoachConfiguration.cs:18-19`), so `FullName` (`NotificationTexts.cs:44`) can reach 201 characters. `Fit` then cuts the chat title at 100, and the bodies at 200 (member name plus 39 characters for SESSION_BOOKED to the coach, plus 37 for SESSION_CANCELLED, plus 33 for MEMBER_JOINED). When a non-BMP character (an emoji) straddles the cut, the stored string ends with a lone high surrogate. Npgsql encodes parameters with a strict UTF-8 encoder, which throws on a lone surrogate, inside `SaveChangesAsync` of the business action.
- Why it matters for the Beta: the chat message, the booking, the cancellation or the first login that carries the row fails with a 500 and rolls back. This matches the letter of the BLOCKER definition, but it needs a full name longer than 100 characters (167 for the login case) with an emoji exactly at the cut, and only the person with that name is affected. The read-only query of section 5 gives the longest full name on each base today.
- Minimal fix: in `Fit`, `var cut = maxLength; if (char.IsHighSurrogate(value[cut - 1])) cut--; return value[..cut];` (same rule as `ChatBody`).
- Confidence: high on the cut; medium on the Npgsql exception (known Npgsql behaviour, not run here). Proof: locally, a coach with first name 98 letters, last name one emoji, sends a chat message: expected 500 before the fix, 201 after.

### N2 (SHOULD) A batch interrupted after FCM accepted a message is sent again in full

- Where: `Mentora.Infrastructure/Services/Push/PushNotificationSender.cs:65` (one transaction for up to 20 rows), `:96-97` (save and commit only after the whole batch), `:206-210` (a cancelled send is rethrown), `FcmClient.cs:179` (cancellation by the host token is not caught).
- What: the status of every row of a batch is written by one commit at the end. If the host stops during a batch (container recreate at a deployment, VPS restart), the stopping token cancels the in-flight HTTP call, the exception leaves `RunOnceAsync`, the transaction rolls back, and every row already accepted by FCM in that batch is still PENDING with its old attempt count. It is sent again after the restart (within 24 hours). The same happens if `SaveChangesAsync` or `CommitAsync` fails for an infrastructure reason: the whole batch is resent 10 seconds later, again at each loop until a commit succeeds (bounded by the 24 hour expiry). No value written by the sender can break a CHECK or a length (B3), so the commit can only fail on an infrastructure error, not on a poison row.
- Why it matters for the Beta: the user sees the same notification twice (for up to 20 notifications). Nothing is written wrongly in the database. A failed commit after FCM accepted is inherent to any outbox (at least once delivery); the shutdown case is avoidable. At Beta volume a batch lasts a fraction of a second every 3 seconds, so a stop has to land inside it.
- Minimal fix: one transaction per row (loop up to 20 times: `SELECT ... LIMIT 1 FOR UPDATE SKIP LOCKED`, send, save, commit), and do not pass the stopping token to `SendAsync`, `SaveChangesAsync` and `CommitAsync` once a row is locked (the 10 second HTTP timeout bounds the call, inside the 30 second default shutdown timeout). Optional, device side: put `android.notification.tag` and the `apns-collapse-id` header to the notification id, so that a duplicate replaces the first banner instead of adding a second one.
- Confidence: high on the mechanism, low on the frequency.

### N3 (BACKLOG) A 401 or 403 that concerns one device is handled as a token or kept forever

- Where: `FcmClient.cs:170-174` (any 401 clears the access token and retries), `FcmClient.cs:216-227` (only UNREGISTERED and INVALID_ARGUMENT remove a device).
- What: FCM answers 401 `THIRD_PARTY_AUTH_ERROR` for an iOS device when the APNs key is not uploaded to Firebase, and 403 `SENDER_ID_MISMATCH` for a token of another Firebase project. The first case costs, for every notification and every such device, a new OAuth call and a second send, then `HTTP_401`. The second case keeps the device row forever and costs one failed call per notification.
- Why it matters: Android first is the accepted decision, so only a team iPhone or a token from another build is concerned. Cost, not correctness.
- Fix (later): invalidate the access token only when the 401 has no `THIRD_PARTY_AUTH_ERROR` error code; decide later whether `SENDER_ID_MISMATCH` removes the device.
- Confidence: medium (FCM error catalogue, not provoked here).

### N4 (BACKLOG) The batch transaction stays open during the HTTP calls

- Where: `PushNotificationSender.cs:65-97`, `:133`.
- What: up to 20 rows, each sent to every device of its user, each send up to 10 s (plus an OAuth call of up to 10 s and one 401 retry). With Google not answering, the transaction can stay open for minutes. Business actions are not blocked: they only INSERT into NOTIFICATIONS and never UPDATE it (checked in the 8 call sites), and the FK check takes a key share lock on USERS that the sender never conflicts with. What can wait: a device registration or a logout on a device row the sender has just deleted (`UserDeviceService.cs:58-64`, `AuthService.cs:295-299`); the registration then updates a row that no longer exists and fails with a 500 (concurrency exception). If the server ever sets `idle_in_transaction_session_timeout` below the batch duration, every commit fails and N2 loops.
- Fix: comes with the per-row transaction of N2. The precheck of section 5 shows the server value (expected 0, disabled).
- Confidence: high on the lock, low on the impact.

### N5 (BACKLOG) An OAuth failure is retried on every device and never backed off

- Where: `FcmClient.cs:80-86` (a failed token call returns `OAUTH` with the HTTP code, non-retryable when 4xx), `PushNotificationSender.cs:188-198`.
- What: with a revoked or disabled key, every device of every row asks Google for a new token (up to 20 or more calls every 3 seconds), and each notification ends at once in FAILED `HTTP_400` or `HTTP_401`, with no retry. The log line says `code=OAUTH`, so it is easy to diagnose.
- Fix (later): remember an OAuth failure for a minute and treat it like NOT_CONFIGURED or as retryable.
- Confidence: high.

### N6 (BACKLOG) MEMBER_JOINED for existing members who were never marked activated

- Where: `AuthService.cs:164-173`.
- What: the flag is set only by the first login after lot 14.1 (deployed 21/11). A member created before that, or who has not logged in since, has `MEMBER_HAS_ACTIVATED = false`; his next login sends "vient de se connecter à Mentora" to all his coaches, even if he used the app before. Two first logins verified at the same moment (two valid codes) would also add two rows each, since the update of the flag is not conditional.
- Why it matters: a misleading notification on api-demo (old test members) and possibly for team members on PROD. The count of section 5 tells how many.
- Fix (later or by SQL before the deployment, with François): mark as activated the members who already have a refresh token.
- Confidence: high on the mechanism.

### N7 (BACKLOG) FK Restrict on USERS breaks the team cleanup scripts

- Where: `NotificationConfiguration.cs:65-69`, migration line 41; example script `docs/review/lot14/vps/cleanup-l148c-prod.sql:49`.
- What: no application path deletes a USERS row (search of `Users.Remove`, `RemoveRange` and `ExecuteDelete` on users: none). Only the hand-made cleanup scripts do. Such a script now fails (and rolls back as a whole) when the user has a NOTIFICATIONS row, for example a coach who received MEMBER_JOINED.
- Fix: add `DELETE FROM "NOTIFICATIONS" WHERE "NOTIFICATION_USER_ID" IN (...)` before the USERS delete in future scripts.
- Confidence: high.

### N8 (BACKLOG) The device list is read once per batch

- Where: `PushNotificationSender.cs:84-93`.
- What: a token that moves to another user (`UserDeviceService.cs:54-62`) during a batch can receive the previous owner's notification; a device removed for one row is tried again for a second row of the same user (one extra call, a second "device removed" line).
- Confidence: high, impact low.

### N9 (BACKLOG) Seeder still holds the literal "INTERVAL"

- Where: `Mentora.Infrastructure/Seeding/ProgramTemplateSeeder.cs:200`.
- What: a value, not an accepted set (the 7 STANDARD literals became `CircuitModes.SerieSimple`). Use `CircuitModes.Interval` for consistency.
- Confidence: high.

### N10 (BACKLOG) Key file patterns only cover the default names

- Where: `.gitignore:507-509`, `.dockerignore:47-49`.
- What: `**/*firebase-adminsdk*.json`, `**/service-account*.json` and `**/secrets/` cover the default Firebase key name and the agreed folder. A key renamed to something else and dropped outside a `secrets` folder would be committed and copied into the image. The repository folder of the PC is synchronised by OneDrive, so the PC copy of the key should live outside it.
- Confidence: high.

### N11 (BACKLOG) Default HttpClient logging adds lines for every FCM and OAuth call

- Where: `Program.cs:430` (default `IHttpClientFactory` handlers).
- What: the factory logs the method and the URL of each call at Information (the URL holds the project id only, no token); headers are logged only at Trace. It is log volume, not a leak. Same as `MuxClient`.
- Fix (later): log level Warning for `System.Net.Http.HttpClient.IFcmClient` if the volume bothers.
- Confidence: low (the log levels of the appsettings files were not opened).

## 4. Verified OK

### A. Circuit modes (14.9a)
1. Single list: `Mentora.Core/Programs/CircuitModes.cs:28` (`All`), `:25` (`LegacyStandard`). Search of the solution for `"STANDARD"`, `"INTERVAL"`, `STANDARD, INTERVAL` and `STANDARD | INTERVAL` in `*.cs`, `*.json`, `*.yml`, `*.xml`, `*.md`: the remaining `"STANDARD"` literals are the session nature (`ProgramSession.cs:22`, `ProgramSessionWire.cs:38`, `ProgramSessionConfiguration.cs:57`, old migrations); the only other `"INTERVAL"` is the seeder value of N9; no Swagger filter mentions modes; the XML docs of the 4 body DTOs list the 12 values. `Normalize` (`CircuitModes.cs:38`) is an exact ordinal comparison; `IsValid` returns false for null; `All.Contains` and the `StandardLike` set use ordinal equality (no case folding); `NormalizeAll` skips null lists and null entries.
2. Write paths, normalised after validation and before storage: program templates POST `ProgramTemplateService.cs:69` and PUT `:102`; session templates POST `SessionTemplateService.cs:132` and PUT `:163`; training programs POST with body `ProgramService.cs:107`; id-based PUT `ProgramService.cs:398`. The validator normalises for itself (`ProgramCircuitRules.cs:41`), so the PUT validated through `UpdateProgramRequestValidator` (copy of the body) sees SERIE_SIMPLE too. Other writers: assignment of a template copies the stored body (`ProgramService.cs:85`, `:856`) and duplication copies the jsonb text (`SessionTemplateService.cs:199`); both copy data that the migration has rewritten, so they cannot bring STANDARD back. The seeder writes `CircuitModes.SerieSimple` and inserts only missing templates. No server path inserts a session template into a program tree. After the migration, no path can store STANDARD.
3. Rules: `ProgramCircuitRules.cs:43-44` (mode, key `{path}[{Position}].Mode`, message lists the 12 values), `:46` (INTERVAL needs Rounds, key `.Rounds`), `:83-89` (SERIE_SIMPLE, BISET, TRISET, GEANTE: former STANDARD rules, key `{path}[p].Exercises[q]`), `:91-97` (INTERVAL unchanged), other modes: nothing required or forbidden. An unknown or lowercase value fails on the same key as before. Only the message text of the STANDARD-like exercise errors changed (it names the normalised mode), as stated in the 14.9a evidence.
4. Id-based PUT: the only change is the normalisation at `ProgramService.cs:398`. Mode is never compared with the stored value, the history rule looks at the actual values only (`ProgramService.cs:438-440`), and the actual columns are never written (`:412`). A mode change on a circuit with a DONE session behaves exactly as before: no new 409, nothing deleted.
5. Migration Up (`20261124221921_Lot14_9a_CircuitModes.cs:24-41`): exact `UPDATE ... WHERE "PROGRAM_CIRCUIT_MODE" = 'STANDARD'`; both jsonb columns (real names `PROGRAM_TEMPLATES.PROGRAM_TEMPLATE_BODY` and `SESSION_TEMPLATES.SESSION_TEMPLATE_CONTENT`) rewritten with `replace` of the exact token `"mode": "STANDARD"`, only on rows whose text contains it, then cast back to jsonb. The token holds the key, so a nature (`"nature"` key or the `SESSION_TEMPLATE_NATURE` column) cannot match; inside a JSON string the quotes are escaped, so free text cannot match either; replacing one complete string value by another keeps valid JSON. Empty database: three updates of 0 rows, then the seeder writes SERIE_SIMPLE. PROD data: every team circuit and template token in STANDARD becomes SERIE_SIMPLE; INTERVAL untouched; the precheck before and after proves the counts. Down (`:51-67`): one `DO` block checks the column and both jsonb columns and raises before any update; the reverse updates (`:73-94`) are the exact mirror.
6. Rollback to `mentora-api:lot14w2` with Lot14_9a applied: GET of templates and programs still works and returns SERIE_SIMPLE (no validation on read). Any PUT of a program template, session template or training program that contains a former STANDARD circuit is refused with 422 ("Mode must be one of: STANDARD, INTERVAL."), because the app sends back the mode it read. So an image rollback needs the 14.9a data reversed first. `dotnet ef` is not on the VPS: section 5 gives the same SQL as `Down` plus the removal of the history row. It is refused (on purpose) as soon as a circuit uses one of the 10 new modes; from then on only a forward fix or a backup restore is possible. The 14.9b migration needs no Down for a rollback (the old image ignores the table, and EF Core 8 ignores unknown history rows, as in the lot 13 runbook).
7. `precheck.sql`: `BEGIN READ ONLY` (line 3) ... `ROLLBACK` (line 44), counts and mode values only, no row content, no fixture name, no local id. The local cleanup and up-proof files name local fixtures and ids only and must never run on the VPS (the evidence says so); the dry run and commit copies differ only on the last line (checked for both sub-lots).

### B. NOTIFICATIONS storage (14.9b)
1. Entity, configuration and migration agree column by column: id uuid default `gen_random_uuid()`; user id uuid NOT NULL; type varchar(40); title varchar(100); body varchar(200); data jsonb; status varchar(20) default `'PENDING'` with C# initialiser `Pending`; attempt count integer default 0 with initialiser 0; next attempt, created and updated timestamptz NOT NULL; sent, clicked timestamptz NULL; error varchar(40) NULL; FK `FK_NOTIFICATIONS_USERS` Restrict (`...Notifications.cs:41`); `IX_NOTIFICATIONS_STATUS_NEXT_ATTEMPT` on (STATUS, NEXT_ATTEMPT_DATE) (`:44`), `IX_NOTIFICATIONS_USER_ID` (`:49`). The 95 snapshot lines match; the model part of the 14.9b Designer equals the snapshot and that of the 14.9a Designer equals the snapshot of `origin/main` (0 differing lines, by script).
2. CHECKs (`:59`, `:66`, `:73`, `:80`), each in `COALESCE(..., false)`: the 5 types equal `NotificationTypes`, the 4 statuses equal `NotificationStatuses`, attempt count 0 to 10 (`MaxAttempts` 3), data `jsonb_typeof = 'object'`. Down (`:88-93`) drops the 4 CHECKs, then the table (indexes and FK go with it).
3. `NOTIFICATION_ERROR` only receives `EXPIRED`, `NOT_CONFIGURED`, `ALL_TOKENS_INVALID`, `RETRIES_EXHAUSTED`, `EXCEPTION`, `SEND_FAILED` or `HTTP_<code>` (at most 18 characters) through `Finish` and `FinalError` (`PushNotificationSender.cs:188-198`); never an FCM field name, a token, a body or an exception message.
4. FK Restrict: no application code deletes USERS (see N7 for the scripts).

### C. Composer and call sites
1. The composer only calls `db.Notifications.Add` (`NotificationComposer.cs:133-150`), never SaveChanges. Call sites, each before the existing save and inside the existing transaction: `ConversationService.cs:146` (text, REST and hub: `ChatHub.cs:55` calls `SendMessageAsync`), `:226` (audio, inside the `CancellationToken.None` transaction and its `try`, so a failure deletes the moved file as before, `:231-263`), `SessionService.cs:128` (reserve, Serializable), `:204` (cancel by member), `:318` (cancel by coach, after the group guard `:285`), `:592` (register participant, Serializable), `:630` (unregister), `AuthService.cs:171` (first member login, transaction `:152`, save `:176`, commit `:177`). The composer is scoped and shares the DbContext of the service. A failed or rolled-back action leaves no row. The composer only reads MEMBERS, COACHES and the two parameter tables, which no Serializable transaction writes, and writes NOTIFICATIONS, which no Serializable transaction reads, so it adds no new serialization conflict (no new 409 on reserve).
2. Recipients: chat goes to the other party of the pair (`:31-48`); SESSION_BOOKED and SESSION_CANCELLED from the member go to the coach of the voucher or session, from the coach go to that member; MEMBER_JOINED goes to the user of every coach linked in MEMBER_COACHES (`:103-107`, the link has no active flag), only inside `if (!effectiveIsCoach && !user.Member!.MemberHasActivated)` (`AuthService.cs:164`), so never on a later login and never on a coach login. A member with several coaches: chat, booking and cancellation reach only the coach concerned; MEMBER_JOINED reaches each linked coach once (`Distinct`). The actor is never a recipient (a coach email cannot be added as member, so a pair never shares a user).
3. Preferences (`NotificationComposer.cs:115-131`): `MemberParameterNotifMessages` for coach to member chat, `CoachParameterNotifMessages` for member to coach chat, `CoachParameterNotifNewBooking` and `CoachParameterNotifBookingCancelled` for member actions; coach actions toward a member always sent; a missing row gives `?? true`; preference off means `return` before `Add`.
4. Queries: one per call site (`LoadPairAsync` single projection, or the MEMBER_COACHES query), no query in a loop. Matches evidence 7.i.
5. Texts as in the evidence (the 14.J spec itself is not in the repository): chat title is the sender full name, body cut at 120 with `...` after the surrogate guard, whitespace collapsed (`NotificationTexts.cs:47-61`), audio "Message vocal". Fixed titles are at most 21 characters; bodies with a coach first name stay under 150. Long names: see N1. Dates: `Europe/Paris` then `Romance Standard Time` (`:23-32`), format `dd/MM 'à' HH'h'mm` in invariant culture, `SpecifyKind(Utc)`; without a time zone the date part is dropped (never UTC) and one warning is logged at startup (`Program.cs:603-604`). The .NET 8 Debian base image ships tzdata, so no warning is expected.
6. data: `Data()` (`NotificationComposer.cs:153-159`) writes `type` plus Guid strings only; no null, no route, no URL. `ReadData` (`PushNotificationSender.cs:229-241`) keeps non-empty strings and adds `notificationId` at send time. Keys: chat to coach `type, memberId`; chat to member `type, coachId`; booked or cancelled to coach `type, sessionId, memberId`; to member `type, sessionId`; MEMBER_JOINED `type, memberId`.

### D. Sender
1. `PushNotificationSender.cs:65-97`: transaction, `SELECT * FROM "NOTIFICATIONS" WHERE "NOTIFICATION_STATUS" = 'PENDING' AND "NOTIFICATION_NEXT_ATTEMPT_DATE" <= {0} ORDER BY "NOTIFICATION_CREATED_DATE" LIMIT {1} FOR UPDATE SKIP LOCKED`, parameters, real column names; one devices query per batch; rows updated by EF and committed in the same transaction. Lock duration: N4.
2. The loop (`:29-57`) catches every exception of a pass and logs the type name only (`:44`), waits 10 s after an error (no tight loop), and breaks on cancellation of the stopping token in the pass or in the delay (no error at shutdown). The service cannot stop the host.
3. Status machine (`:107-174`): EXPIRED first when created more than 24 h ago (no FCM call), then NOT_CONFIGURED, then NO_DEVICE, SENT when at least one device succeeded, PENDING again only when a retryable failure happened and fewer than 3 attempts were made (next try +1 min, then +5 min), else FAILED with `FinalError`. Retryable means 429, 5xx, timeout or network (`FcmClient.cs:84`, `:179-186`, `:226`). Every processed row either finishes or increases its attempt count, and the expiry ends any row after 24 h, so no row stays PENDING forever. A row is never sent again after a committed success (it is SENT). The commit-after-send case: N2.
4. Device removal (`FcmClient.cs:216-224`): only 404 `UNREGISTERED`, or 400 `INVALID_ARGUMENT` whose `fieldViolations` name `message.token` or name nothing; the delete is by `UserDeviceId` (`PushNotificationSender.cs:133`). A payload error that names another field returns `Failed` with the field name and the device is kept. Parsing (`FcmClient.cs:235-275`) follows the FCM v1 error format: `error.details[].errorCode` of the FcmError detail, else `error.status`; `fieldViolations[].field` of the BadRequest detail. The body is parsed and dropped, never logged.
5. Logs: `[PUSH] sent notificationId type status devices ok removed` once per row (`:177-179`), `[PUSH] device removed deviceId reason` (`:134`), `payload rejected notificationId field` (`:148`), `send retryable` and `send failed` with device id, HTTP code and FCM code, `send error` with the exception type. No token, no title, no body, no client_email.

### E. FcmClient and options
1. Assertion (`FcmClient.cs:119-131`): RS256 through `RsaSecurityKey` and `SecurityAlgorithms.RsaSha256`, `iss` client_email, `scope` firebase.messaging, `aud` the OAuth token URL, `iat` now, `exp` now plus 3600. The key is imported once per process (lazy, under a lock) and disposed with the singleton; the validator disposes its own copy; a failed import disposes the RSA object. Token cache: inside a `SemaphoreSlim` (`:62`), refreshed 5 minutes before expiry (`:45`), cleared once on a 401 then one retry (`:170-174`). Two scopes calling at the same time are serialised on the semaphore; the RSA signing is inside it; `Invalidate` outside the lock only forces an extra token fetch.
2. `AddHttpClient<IFcmClient, FcmClient>` with a 10 s timeout (`Program.cs:430`), same pattern as `MuxClient` (`:279-283`); no default header; the bearer header is set per request; no custom handler. Default factory logging: N11.
3. `FirebaseOptionsValidator.cs`: empty path refused outside Development, accepted in Development; with a path, `FirebaseServiceAccount.TryLoad` refuses a missing file, unreadable file or bad JSON, a non-object, a wrong `type`, a `project_id` different from `Firebase:ProjectId`, an empty `client_email` or `private_key`, a key that does not import. Every message is a fixed text with no path, no content and no address. `Program.cs:580` only moves the Firebase validation before the audio probe; the order and text of the other checks and lines are unchanged, and a Firebase refusal happens before `MigrateAsync` (`:625`), so no migration runs on a refused start.
4. `.gitignore:507-509` and `.dockerignore:47-49` hold the three patterns, and no negation pattern re-includes them. `Mentora.API/Dockerfile:15` copies the build context, which these patterns now filter; nothing else copies a file. appsettings: only `Firebase:ProjectId` added.

### F. Mobile contract
No file under `Mentora.API/Controllers`, `Mentora.API/Hubs` or `Mentora.Core/Enums` changes. Under `Mentora.Core/DTOs` the 4 changed files differ only in `///` comments (script over the diff). No route, DTO or JSON key is added, removed or reordered; only the mode values change (SERIE_SIMPLE returned for migrated data, 10 new values accepted on write, STANDARD still accepted). The push data keys (C6) are new and follow the contract cited in the evidence.

### G. Startup
New settings: environment variable `Firebase__ServiceAccountPath` (never in a file) and `Firebase:ProjectId` (appsettings.json, may be overridden by `Firebase__ProjectId`). api-prod (Production) refuses to start until the variable is set and the file mounted and valid (`Firebase:ServiceAccountPath must be set as an environment variable outside Development.`). api-demo (Development) starts without the variable, with `[PUSH] configured=False`, and every notification ends FAILED NOT_CONFIGURED; but once the variable is set on api-demo, a missing or invalid file stops it too. The code of the `[EMAIL]`, `[INVITE]`, `[AUTH]`, `[CORS]`, `[MUX]`, `[AUDIO]` and `[ADMIN]` lines is not in the diff. New line: `[PUSH] configured=<bool> projectId=mentora-69209 pollSeconds=3 maxAttempts=3` after `[AUDIO]`.

### H. Public repository
`git --no-optional-locks diff --name-only "-G<pattern>" origin/main...feature/lot_14` on the whole diff:

| Pattern | Result | Control |
|---|---|---|
| JWT shape `eyJ...` with two `[.]` segments | empty | `git grep -l` on `origin/main -- docs/review/lot7`: 2 files |
| `[[]OTP[]]` | empty | `origin/main:Mentora.Infrastructure/Services/AuthService.cs`: found |
| `[[]EMAIL-LOG[]]` | empty | 14 files on `origin/main` |
| `gmail[.]com` | empty | `docs/review/lot11/11.4/runtime-evidence.md`: found |
| `web2success[.]fr` | empty | `docs/review/lot8/vps/cleanup-vps.sql`: found |
| `mentorapp[.]fr` | empty | `docs/review/lot8/vps/cleanup-vps.sql`: found |
| `13a00000-`, `19b00000-` | empty | none |
| `-----BEGIN` | empty | no control file; the leading-dash form `-G-- Lot 14[.]9a precheck` finds `precheck.sql`, and the same command form finds `private_key` below |
| `iam[.]gserviceaccount[.]com`, `gserviceaccount` | empty | no control file, same proof |
| `private_key` | `FirebaseServiceAccount.cs`, `14.9.0/inventory.md` | property names in code and doc |
| `client_email` | `Program.cs`, `FcmClient.cs`, `FirebaseServiceAccount.cs`, `14.9.0/inventory.md` | property names in comments, code and doc |
| `mentora-69209-firebase` | `14.9b/runtime-evidence.md` (line 121) | a dummy path ending in `-abc.json`, used for `git check-ignore`; placeholder, no key |
| `l149b-fake-[0-9a-f]` | empty | the fake token itself is not in the diff |
| `APA91` (FCM token), `ya29[.]` (Google access token), `AIza` key shape | empty | same proof |

Addresses in the added lines: only the domains `mentora.fr` (60) and `example.com` (4). No signed URL shape, no MUX or LiveKit variable with a value. Token-like runs of 40 characters or more are only repository paths, identifiers and psql separator lines. Configuration files touched by the wave: `.gitignore`, `.dockerignore`, `Mentora.API/appsettings.json`. No `.env`, docker-compose, nginx, Dockerfile, `.gitattributes` or `.csproj`.

### I. Dashes
0 em dash and 0 en dash in the added lines of the whole diff. Control: the same decoding counts 35 lines with an em dash in `origin/main:Mentora.Infrastructure/Services/SessionService.cs` (inherited, context lines only in this diff).

### J. Build
`dotnet build --no-incremental` (log outside the repository): 0 errors, 19 warnings, all CS1573, in `LastPerformedResponse.cs`, `ProgramExerciseActualRequest.cs`, `ProgramSessionResponse.cs` and the two conversation controllers. None is in a file touched by the wave.

## 5. Deployment

Order: image built and checked on the PC, read-only checks on both bases, key file on the VPS, compose for both services, api-demo first with its proofs, then api-prod. The key file and the variable must be in place before either service is recreated: once the variable is set, both services refuse to start without a valid file, and api-prod refuses without the variable anyway.

Read-only SQL before (both bases): last applied migration `20261123042911_Lot14_8_CoachCreatedAudit`, else stop; `docs/review/lot14/14.9a/precheck.sql` on `mentora_db` and `mentora_prod`, output kept for the comparison after; plus the extra counts below (server `idle_in_transaction_session_timeout` for N4, devices already registered, capitalised `"Mode"` keys that the migration would not see, non-activated members for N6, longest full names for N1).

Container user and the 600 file: the API image has no `USER` line (`Mentora.API/Dockerfile`) and the compose adds none (runbook lot 13 step 4 states the container runs as root). Root in the container reads a file of mode 600 owned by `mentora`, unless Docker runs rootless or with user namespace remapping (then container root is an unprivileged host user). The bind mount source is opened by the Docker daemon, so the 700 folder does not matter inside the container. Proof: `id -u` gives 0 in the container and `docker info` shows neither `rootless` nor `userns`; the final proof is `[PUSH] configured=True`. If it is not root: `sudo chown <uid shown by id -u> ~/mentora/secrets/service-account-firebase.json` and keep mode 600. If the file is unreadable, the API refuses to start with "cannot be read as JSON" (or "does not point to an existing file"), never with the path.

Single-file bind mounts: create the host file before the compose change. If the host file does not exist when the container is created, Docker creates an empty directory at that path and the API refuses to start. A replaced key file (rotation) is only seen after a recreate.

api-demo note: devices already registered in `mentora_db` (count in the precheck) start receiving real notifications from demo activity, since both bases use the same Firebase project.

Expected startup lines: first start of each service, `Applying migration '20261124221921_Lot14_9a_CircuitModes'.` and `Applying migration '20261124234929_Lot14_9b_Notifications'.` (count 2), `[PUSH] configured=True projectId=mentora-69209 pollSeconds=3 maxAttempts=3`, no `[PUSH] time zone` warning, all other startup lines identical to the ones before (compared with `diff`), `Application started`, no `fail:` or `crit:`. Second start: count 0, same lines.

Proofs on api-demo: mode round trip (precheck after, then PJ creates BISET and AMRAP session templates and saves a former STANDARD program from the app); a voice message gives one CHAT_MESSAGE row with the fixed body "Message vocal"; a real delivery on PJ's Android phone gives `[PUSH] sent ... status=SENT devices=1 ok=1 removed=0`; no FCM token shape in the logs; no `sender loop error`. On PROD: no test message, only the startup lines, the health check, the precheck after and the counts of NOTIFICATIONS by status.

Rollback: images back (`pre-lot14w3` for api-demo, compose backup for api-prod, which also removes the mount and the variable). Then, on each base, the 14.9a data reversal by SQL, dry run then commit; it is refused when a new mode exists, and then the choice is a forward fix or a backup restore with François. No action for 14.9b. The key can stay in `~/mentora/secrets`.

### Content for a new runbook section in `docs/review/lot12/12.5a/prod-runbook.md` (French, like the file)

````markdown
## Lot 14 vague 3 (25/11)

Contenu : 14.9a (les 12 types de blocs de l'app ; STANDARD devient SERIE_SIMPLE dans les données, STANDARD reste accepté à l'écriture) et 14.9b (notifications push : table `NOTIFICATIONS`, envoi FCM, 4 événements). Image `mentora-api:lot14w3`. Deux migrations : `20261124221921_Lot14_9a_CircuitModes` (données seulement) et `20261124234929_Lot14_9b_Notifications` (nouvelle table).

Nouveauté : le fichier de clé du compte de service Firebase. C'est un secret : jamais dans le dépôt, jamais dans l'image, jamais affiché (`cat`, `head`, `less` interdits). Sur le PC, le garder hors du dossier du dépôt (synchronisé par OneDrive). Dès que `Firebase__ServiceAccountPath` est défini, l'API refuse de démarrer sans un fichier valide, sur api-demo comme sur api-prod ; sans la variable, api-prod (Production) refuse aussi de démarrer. Les messages de refus ne contiennent jamais le chemin.

### a. Image **[PS]**

```powershell
git --no-optional-locks status --short
git --no-optional-locks log --oneline -1
docker build --platform linux/amd64 -f Mentora.API/Dockerfile -t mentora-api:lot14w3 .
docker run --rm --entrypoint sh mentora-api:lot14w3 -c "find / -xdev \( -name '*firebase-adminsdk*' -o -name 'service-account*' \) 2>/dev/null | wc -l"
docker save -o "$env:TEMP\mentora-api-lot14w3.tar" mentora-api:lot14w3
$t = "$env:TEMP\mentora-api-lot14w3.tar"; $i = [IO.File]::OpenRead($t); $o = [IO.File]::Create("$t.gz"); $g = New-Object IO.Compression.GZipStream($o, [IO.Compression.CompressionLevel]::Optimal); $i.CopyTo($g); $g.Dispose(); $o.Dispose(); $i.Dispose()
Remove-Item "$env:TEMP\mentora-api-lot14w3.tar"; Get-Item "$env:TEMP\mentora-api-lot14w3.tar.gz" | Select-Object Name, Length
Push-Location $env:TEMP; scp mentora-api-lot14w3.tar.gz mentora@87.106.108.91:~/; Pop-Location
scp docs/review/lot14/14.9a/precheck.sql mentora@87.106.108.91:~/precheck-149a.sql
```

Attendu : arbre propre, dernier commit de la branche, `0` pour la recherche de clé dans l'image (sinon **arrêter** : une clé est dans le contexte de build), une archive `.tar.gz`.

### b. Chargement, sauvegardes **[SSH]**

```bash
docker load -i ~/mentora-api-lot14w3.tar.gz && docker images mentora-api --format '{{.Repository}}:{{.Tag}}\t{{.ID}}\t{{.CreatedAt}}'
cd ~/mentora && docker exec mentora-postgres sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc' > ~/backup-$(date +%Y%m%d-%H%M).dump && ls -lht ~/backup-*.dump | head -3
cd ~/mentora && docker exec mentora-postgres sh -c 'pg_dump -U "$POSTGRES_USER" -d mentora_prod -Fc' > ~/backup-prod-$(date +%Y%m%d-%H%M).dump && ls -lht ~/backup-prod-*.dump | head -3
cd ~/mentora && cp docker-compose.yml docker-compose.yml.pre-lot14w3 && ls -l docker-compose.yml*
docker inspect --format '{{.Config.Image}}' mentora-api-prod
```

Attendu : `mentora-api:lot14w3` listé, deux `.dump` de taille cohérente, la copie du compose, et l'image actuelle d'api-prod (`mentora-api:lot14w2`), à noter pour le retour arrière.

### c. Contrôles en lecture seule, avant **[SSH]**

```bash
docker exec -i mentora-postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1' < ~/precheck-149a.sql > ~/w3-precheck-demo-avant.txt; cat ~/w3-precheck-demo-avant.txt
docker exec -i mentora-postgres sh -c 'psql -U "$POSTGRES_USER" -d mentora_prod -v ON_ERROR_STOP=1' < ~/precheck-149a.sql > ~/w3-precheck-prod-avant.txt; cat ~/w3-precheck-prod-avant.txt
```

Puis, sur api-demo :

```bash
docker exec -i mentora-postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1' <<'SQL'
BEGIN READ ONLY;
SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1;
SELECT to_regclass('"NOTIFICATIONS"') AS table_notifications;
SHOW idle_in_transaction_session_timeout;
SELECT count(*) AS appareils FROM "USER_DEVICES";
SELECT count(*) AS modeles_cle_Mode FROM "PROGRAM_TEMPLATES" WHERE "PROGRAM_TEMPLATE_BODY"::text LIKE '%"Mode":%';
SELECT count(*) AS seances_cle_Mode FROM "SESSION_TEMPLATES" WHERE "SESSION_TEMPLATE_CONTENT"::text LIKE '%"Mode":%';
SELECT count(*) AS membres_non_actives_avec_coach FROM "MEMBERS" m WHERE NOT m."MEMBER_HAS_ACTIVATED" AND EXISTS (SELECT 1 FROM "MEMBER_COACHES" mc WHERE mc."MEMBER_ID" = m."MEMBER_ID");
SELECT max(length("MEMBER_FIRST_NAME") + length("MEMBER_LAST_NAME")) + 1 AS nom_membre_max FROM "MEMBERS";
SELECT max(length("COACH_FIRST_NAME") + length("COACH_LAST_NAME")) + 1 AS nom_coach_max FROM "COACHES";
ROLLBACK;
SQL
```

Puis la même commande sur PROD, en remplaçant seulement `-d "$POSTGRES_DB"` par `-d mentora_prod`.

Attendu sur chaque base :
- dernière migration `20261123042911_Lot14_8_CoachCreatedAudit`, sinon **arrêter** ;
- `table_notifications` vide (la table n'existe pas encore) ;
- `idle_in_transaction_session_timeout` à `0` ;
- `modeles_cle_Mode` et `seances_cle_Mode` à `0` (sinon **arrêter** : la migration ne réécrit que la clé `"mode"` en minuscules) ;
- `appareils` : pour information (sur api-demo, ces téléphones recevront les notifications de la démo) ;
- `membres_non_actives_avec_coach` : pour information, chacun enverra « vient de se connecter » à ses coachs à sa prochaine connexion ;
- `nom_membre_max` et `nom_coach_max` inférieurs à 100 (sinon, voir N1 de la revue de la vague 3 avant de déployer).

Le fichier precheck donne, par base : les modes de `PROGRAM_CIRCUITS`, les jetons `"mode": "STANDARD"`, `SERIE_SIMPLE`, `INTERVAL` et les jetons de nature des deux colonnes jsonb, et les natures des modèles de séance. Garder les deux fichiers `-avant.txt` pour la comparaison du point h.

### d. Clé Firebase sur le VPS **[SSH] puis [PS] puis [SSH]**

**[SSH]** :

```bash
mkdir -p ~/mentora/secrets && chmod 700 ~/mentora/secrets && ls -ld ~/mentora/secrets
```

**[PS]** (le chemin du fichier sur le PC est tapé au clavier, jamais écrit dans un fichier) :

```powershell
$cle = Read-Host "Chemin complet du fichier de cle Firebase sur le PC"
scp "$cle" mentora@87.106.108.91:~/mentora/secrets/service-account-firebase.json; Remove-Variable cle
```

**[SSH]** :

```bash
chmod 600 ~/mentora/secrets/service-account-firebase.json && ls -l ~/mentora/secrets/
docker exec mentora-api-prod id -u
docker info --format '{{.SecurityOptions}}'
```

Attendu : `drwx------ mentora` pour le dossier, `-rw------- mentora` pour le fichier (environ 2,3 Ko), `0` pour `id -u`, et ni `rootless` ni `userns` dans `SecurityOptions`. Le conteneur tourne en root : il lit le fichier malgré le mode 600. Si `id -u` ne donne pas `0`, faire `sudo chown <ce numéro> ~/mentora/secrets/service-account-firebase.json` (le mode reste 600). Le nom du fichier et le dossier `secrets` sont couverts par `.gitignore` et `.dockerignore`.

### e. Compose **[SSH]**

```bash
cd ~/mentora && nano docker-compose.yml
```

Dans le service `api` (api-demo) et dans le service `api-prod`, sous la dernière ligne de `environment:` (six espaces devant) :

```yaml
      - Firebase__ServiceAccountPath=/etc/mentora/firebase-service-account.json
```

et sous `volumes:` de chacun des deux services (six espaces devant, à côté de la ligne `media-demo` ou `media-prod`) :

```yaml
      - ./secrets/service-account-firebase.json:/etc/mentora/firebase-service-account.json:ro
```

Dans `api-prod` seulement, la ligne `image:` devient `    image: mentora-api:lot14w3`. Ctrl+O, Entrée, Ctrl+X. Puis :

```bash
cd ~/mentora && docker compose config --quiet && echo "compose valide"
cd ~/mentora && docker compose config | grep -E 'Firebase__ServiceAccountPath|firebase-service-account|service-account-firebase|read_only|image: mentora-api'
```

Attendu : `compose valide`, deux `Firebase__ServiceAccountPath`, deux montages en lecture seule (`read_only: true`), l'image `mentora-api:lot14w3` pour api-prod. Ne jamais lancer `docker compose config` sans filtre.

### f. Lignes de démarrage d'avant **[SSH]**

```bash
docker logs mentora-api 2>&1 | grep -E '\[EMAIL\] routing|\[INVITE\] installUrl|\[AUTH\] otpRateLimit|\[CORS\]|\[MUX\] configured|\[AUDIO\] configured|\[ADMIN\] (auth|accounts)' | grep -v -E 'OTP|EMAIL-LOG' | sort -u > ~/w3-lignes-demo-avant.txt; wc -l ~/w3-lignes-demo-avant.txt
docker logs mentora-api-prod 2>&1 | grep -E '\[EMAIL\] routing|\[INVITE\] installUrl|\[AUTH\] otpRateLimit|\[CORS\]|\[MUX\] configured|\[AUDIO\] configured|\[ADMIN\] (auth|accounts)' | grep -v -E 'OTP|EMAIL-LOG' | sort -u > ~/w3-lignes-prod-avant.txt; wc -l ~/w3-lignes-prod-avant.txt
```

### g. Bascule d'api-demo **[SSH]**

```bash
cd ~/mentora && docker tag mentora-api:latest mentora-api:pre-lot14w3 && docker tag mentora-api:lot14w3 mentora-api:latest && docker compose up -d --no-deps --force-recreate api && sleep 25 && docker logs mentora-api 2>&1 | grep -c "Applying migration"
docker logs mentora-api 2>&1 | grep -E '\[PUSH\]|Applying migration|Application started|fail:|crit:|Exception' | grep -v -E 'OTP|EMAIL-LOG'
docker logs mentora-api 2>&1 | grep -E '\[EMAIL\] routing|\[INVITE\] installUrl|\[AUTH\] otpRateLimit|\[CORS\]|\[MUX\] configured|\[AUDIO\] configured|\[ADMIN\] (auth|accounts)|\[PUSH\] configured' | grep -v -E 'OTP|EMAIL-LOG' | sort -u > ~/w3-lignes-demo-apres.txt; diff ~/w3-lignes-demo-avant.txt ~/w3-lignes-demo-apres.txt
```

Attendu : `2` ; `Applying migration '20261124221921_Lot14_9a_CircuitModes'.`, `Applying migration '20261124234929_Lot14_9b_Notifications'.`, `[PUSH] configured=True projectId=mentora-69209 pollSeconds=3 maxAttempts=3`, `Application started`, aucune ligne `[PUSH] time zone`, `fail:` ni `crit:` ; le `diff` ne montre qu'une ligne ajoutée, `> [PUSH] configured=True ...`.

Si l'API refuse de démarrer :

```bash
docker logs mentora-api 2>&1 | grep -E 'Firebase|OptionsValidation' | grep -v -E 'OTP|EMAIL-LOG'
```

Le message dit ce qui ne va pas (variable absente, fichier absent, illisible, mauvais projet), jamais le chemin. Corriger le montage ou la variable, puis recréer. Aucune migration n'est appliquée lors d'un refus (le contrôle Firebase passe avant les migrations).

### h. Contrôles après la migration, api-demo **[SSH]**

```bash
docker exec -i mentora-postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1' < ~/precheck-149a.sql > ~/w3-precheck-demo-apres.txt; diff ~/w3-precheck-demo-avant.txt ~/w3-precheck-demo-apres.txt
docker exec -i mentora-postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1' <<'SQL'
BEGIN READ ONLY;
SELECT conname FROM pg_constraint WHERE conrelid = '"NOTIFICATIONS"'::regclass ORDER BY 1;
SELECT indexname FROM pg_indexes WHERE tablename = 'NOTIFICATIONS' ORDER BY 1;
SELECT "NOTIFICATION_TYPE", "NOTIFICATION_STATUS", count(*) FROM "NOTIFICATIONS" GROUP BY 1, 2 ORDER BY 1, 2;
ROLLBACK;
SQL
```

Attendu : dans le `diff`, les jetons `"mode": "STANDARD"` passent à 0, les jetons `SERIE_SIMPLE` prennent l'ancien nombre de `STANDARD`, la ligne `STANDARD` de `PROGRAM_CIRCUITS` devient `SERIE_SIMPLE` avec le même nombre ; `INTERVAL`, les jetons de nature et les natures des modèles de séance ne changent pas. Contraintes : `CK_NOTIFICATIONS_ATTEMPT_COUNT`, `CK_NOTIFICATIONS_DATA`, `CK_NOTIFICATIONS_STATUS`, `CK_NOTIFICATIONS_TYPE`, `FK_NOTIFICATIONS_USERS`, `PK_NOTIFICATIONS`. Index : `IX_NOTIFICATIONS_STATUS_NEXT_ATTEMPT`, `IX_NOTIFICATIONS_USER_ID`, `PK_NOTIFICATIONS`.

### i. Preuves sur api-demo (avec PJ)

1. Types de blocs : PJ crée depuis l'app un modèle de séance avec un bloc BISET et un bloc AMRAP, le rouvre (mêmes types), puis ouvre et enregistre un programme qui contenait un bloc STANDARD (enregistrement accepté, type affiché SERIE_SIMPLE).
2. Message vocal : PJ envoie un message vocal à son coach de démo, puis **[SSH]** :

```bash
docker exec -i mentora-postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1' <<'SQL'
BEGIN READ ONLY;
SELECT "NOTIFICATION_STATUS", count(*) FROM "NOTIFICATIONS" WHERE "NOTIFICATION_TYPE" = 'CHAT_MESSAGE' AND "NOTIFICATION_BODY" = 'Message vocal' AND "NOTIFICATION_CREATED_DATE" > now() - interval '15 minutes' GROUP BY 1;
ROLLBACK;
SQL
```

Attendu : une ligne, `SENT` si le destinataire a un téléphone enregistré, sinon `NO_DEVICE`. Ne jamais afficher le corps des notifications de type texte (il contient le message).

3. Vraie réception sur Android : PJ se connecte avec l'app Android branchée sur api-demo (le téléphone s'enregistre), son coach de démo lui écrit un message ; la notification s'affiche. **[SSH]** :

```bash
docker logs --since 10m mentora-api 2>&1 | grep -E '\[PUSH\] (sent|device removed|payload rejected|send )'
docker logs mentora-api 2>&1 | grep -c 'APA91'
docker logs mentora-api 2>&1 | grep -c 'sender loop error'
```

Attendu : `[PUSH] sent notificationId=... type=CHAT_MESSAGE status=SENT devices=1 ok=1 removed=0`, puis `0` (aucun jeton FCM dans les logs) et `0`.

4. Facultatif : PJ désinstalle l'app, on lui écrit de nouveau : `[PUSH] device removed deviceId=... reason=UNREGISTERED` (FCM peut mettre du temps à déclarer le jeton invalide après une désinstallation).

### j. Deuxième démarrage d'api-demo **[SSH]**

```bash
cd ~/mentora && docker compose up -d --no-deps --force-recreate api && sleep 25 && docker logs mentora-api 2>&1 | grep -c "Applying migration"
docker logs mentora-api 2>&1 | grep -E '\[PUSH\]|Application started|fail:|crit:' | grep -v -E 'OTP|EMAIL-LOG'
```

Attendu : `0`, la même ligne `[PUSH] configured=True ...`, `Application started`, sans `fail:` ni `crit:`.

### k. Bascule d'api-prod **[SSH]**

Seulement si les points g à j sont bons. L'image `mentora-api:lot14w3` est déjà dans le compose (point e).

```bash
cd ~/mentora && docker compose up -d --no-deps --force-recreate api-prod && sleep 25 && docker logs mentora-api-prod 2>&1 | grep -c "Applying migration"
docker logs mentora-api-prod 2>&1 | grep -E '\[PUSH\]|Applying migration|Application started|fail:|crit:|Exception' | grep -v -E 'OTP|EMAIL-LOG'
docker logs mentora-api-prod 2>&1 | grep -E '\[EMAIL\] routing|\[INVITE\] installUrl|\[AUTH\] otpRateLimit|\[CORS\]|\[MUX\] configured|\[AUDIO\] configured|\[ADMIN\] (auth|accounts)|\[PUSH\] configured' | grep -v -E 'OTP|EMAIL-LOG' | sort -u > ~/w3-lignes-prod-apres.txt; diff ~/w3-lignes-prod-avant.txt ~/w3-lignes-prod-apres.txt
docker exec -i mentora-postgres sh -c 'psql -U "$POSTGRES_USER" -d mentora_prod -v ON_ERROR_STOP=1' < ~/precheck-149a.sql > ~/w3-precheck-prod-apres.txt; diff ~/w3-precheck-prod-avant.txt ~/w3-precheck-prod-apres.txt
curl -s -o /dev/null -w 'health %{http_code}\n' https://api.mentorapp.fr/api/v1/health
```

Attendu : les mêmes résultats qu'aux points g et h, pour PROD (`2`, les deux migrations, `[PUSH] configured=True ...`, le `diff` des lignes ne montre que la ligne `[PUSH]`, le `diff` du precheck seulement STANDARD devenu SERIE_SIMPLE), puis `health 200`. Puis le contrôle de la table du point h avec `-d mentora_prod`, et le deuxième démarrage du point j avec `api-prod` et `mentora-api-prod`.

Sur PROD : aucun message de test. Les jours suivants, compter sans rien afficher d'autre :

```bash
docker logs --since 24h mentora-api-prod 2>&1 | grep -oE '\[PUSH\] sent .* status=[A-Z_]+' | grep -oE 'status=[A-Z_]+' | sort | uniq -c
docker logs --since 24h mentora-api-prod 2>&1 | grep -c 'sender loop error'
```

### l. Retour arrière

Images **[SSH]** :

```bash
cd ~/mentora && docker tag mentora-api:pre-lot14w3 mentora-api:latest && docker compose up -d --no-deps --force-recreate api
cd ~/mentora && cp docker-compose.yml.pre-lot14w3 docker-compose.yml && docker compose config --quiet && docker compose up -d --no-deps --force-recreate api-prod
```

La copie du compose remet aussi l'ancienne image d'api-prod et retire le montage et la variable Firebase (l'ancien code ne les lit pas). La clé peut rester dans `~/mentora/secrets`.

Migration 14.9b : rien à faire. L'ancienne image ignore la table `NOTIFICATIONS` et EF Core 8 ignore la ligne d'historique inconnue.

Migration 14.9a : à défaire, sinon l'ancienne image refuse (422) tout enregistrement d'un programme ou d'un modèle qui contient SERIE_SIMPLE. `dotnet ef` n'existe pas sur le VPS : le même SQL que le `Down`, plus la ligne d'historique. Essai à blanc terminé par `ROLLBACK`, puis la même chose terminée par `COMMIT` (seule la dernière ligne diffère), d'abord sur api-demo :

```bash
cat > ~/w3-retour-149a.sql <<'SQL'
\set ON_ERROR_STOP on
BEGIN;
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM "PROGRAM_CIRCUITS" WHERE "PROGRAM_CIRCUIT_MODE" NOT IN ('SERIE_SIMPLE', 'INTERVAL', 'STANDARD'))
    OR EXISTS (SELECT 1 FROM "PROGRAM_TEMPLATES" t CROSS JOIN LATERAL regexp_matches(t."PROGRAM_TEMPLATE_BODY"::text, '"mode": "([^"]*)"', 'g') AS m(arr) WHERE m.arr[1] NOT IN ('SERIE_SIMPLE', 'INTERVAL', 'STANDARD'))
    OR EXISTS (SELECT 1 FROM "SESSION_TEMPLATES" s CROSS JOIN LATERAL regexp_matches(s."SESSION_TEMPLATE_CONTENT"::text, '"mode": "([^"]*)"', 'g') AS m(arr) WHERE m.arr[1] NOT IN ('SERIE_SIMPLE', 'INTERVAL', 'STANDARD'))
    THEN
        RAISE EXCEPTION 'Retour 14.9a impossible : des blocs utilisent un des 10 nouveaux types.';
    END IF;
END
$$;
UPDATE "PROGRAM_CIRCUITS" SET "PROGRAM_CIRCUIT_MODE" = 'STANDARD' WHERE "PROGRAM_CIRCUIT_MODE" = 'SERIE_SIMPLE';
UPDATE "PROGRAM_TEMPLATES" SET "PROGRAM_TEMPLATE_BODY" = replace("PROGRAM_TEMPLATE_BODY"::text, '"mode": "SERIE_SIMPLE"', '"mode": "STANDARD"')::jsonb WHERE "PROGRAM_TEMPLATE_BODY"::text LIKE '%"mode": "SERIE_SIMPLE"%';
UPDATE "SESSION_TEMPLATES" SET "SESSION_TEMPLATE_CONTENT" = replace("SESSION_TEMPLATE_CONTENT"::text, '"mode": "SERIE_SIMPLE"', '"mode": "STANDARD"')::jsonb WHERE "SESSION_TEMPLATE_CONTENT"::text LIKE '%"mode": "SERIE_SIMPLE"%';
DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261124221921_Lot14_9a_CircuitModes';
SQL
{ cat ~/w3-retour-149a.sql; echo 'ROLLBACK;'; } | docker exec -i mentora-postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB"'
```

Attendu : `DO`, trois `UPDATE n`, `DELETE 1`, `ROLLBACK`. Seulement dans ce cas :

```bash
{ cat ~/w3-retour-149a.sql; echo 'COMMIT;'; } | docker exec -i mentora-postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB"'
docker exec -i mentora-postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1' < ~/precheck-149a.sql > ~/w3-precheck-demo-retour.txt; diff ~/w3-precheck-demo-avant.txt ~/w3-precheck-demo-retour.txt
```

Attendu : aucun écart avec l'état d'avant (si aucun programme n'a changé entre-temps). Puis les deux mêmes commandes avec `-d mentora_prod` et les fichiers `prod`.

Si le `DO` lève l'erreur « Retour 14.9a impossible » : ne rien forcer. Un coach a déjà utilisé un nouveau type ; revenir en arrière modifierait ses programmes. Choix avec François : corriger en avançant sur la nouvelle image, ou restaurer la sauvegarde du point b (perte de tout ce qui a été écrit depuis).

Au redéploiement suivant de la nouvelle image, EF rejouera `Lot14_9a_CircuitModes` (la ligne d'historique a été retirée), ce qui réécrit de nouveau STANDARD en SERIE_SIMPLE.

### m. Après le déploiement

**[SSH]** :

```bash
rm ~/mentora-api-lot14w3.tar.gz ~/precheck-149a.sql; ls ~/mentora-api-lot14w3.tar.gz
```

**[PS]** :

```powershell
Remove-Item "$env:TEMP\mentora-api-lot14w3.tar.gz"; Test-Path "$env:TEMP\mentora-api-lot14w3.tar.gz"
```

Attendu : `No such file or directory`, puis `False`. Garder `mentora-api:pre-lot14w3`, `mentora-api:lot14w2`, les fichiers `~/w3-*.txt` et `docker-compose.yml.pre-lot14w3` jusqu'au merge dans main.
````
