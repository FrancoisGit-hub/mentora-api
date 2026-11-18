# Lot 13 review, wave 1 (13.1 to 13.4)

Read-only, single pass, 2026-11-18. Reviewer: Claude (Opus 5.5).

## 1. Scope

Preflight: tree clean, branch `feature/lot_13`, HEAD `e98677e docs(lot13): 13.4 runtime evidence and API documentation`, parent `efa8ffd feat(lot13): 13.4 ...`, then `409ca88`. `origin/main` = `9337c5dd...`. All expected.

Commits (`git --no-optional-locks log --oneline origin/main..feature/lot_13`):

```
e98677e docs(lot13): 13.4 runtime evidence and API documentation
efa8ffd feat(lot13): 13.4 voice upload and listening routes, broadcast after REST sends, upload rate limit
409ca88 docs(lot13): 13.3 runtime evidence
c2d743b feat(lot13): 13.3 audio analysis, disk storage, startup guard, ffmpeg in image
6ff374d docs(lot13): 13.2 runtime evidence
4206937 feat(lot13): 13.2 voice message columns and DTO fields
bbafcc5 docs(lot13): 13.1 runtime evidence and runbook
150d4f4 feat(lot13): 13.1 CORS debug origins (Cors__DebugOrigins)
003d92a docs(lot13): 13.0 read-only inventories (voice messages, CORS)
```

Diff stat: 40 files, 7811 insertions, 57 deletions. Code read in full: `VoiceUploadRateLimiting.cs`, both conversation message controllers, `ConversationAudioController.cs`, `Dockerfile`, `ChatHub.cs`, `GlobalExceptionMiddleware.cs` (hunk), `Program.cs` (auth, policies, CORS, startup blocks, pipeline), `ConversationDto.cs`, `MessageDto.cs`, `Message.cs`, `EnumMappings.cs` (hunk), `MessageType.cs`, `AudioOptions.cs`, `CorsOptions.cs`, `VoiceMessageRules.cs`, `SendAudioMessageRequestValidator.cs`, `MessageConfiguration.cs`, migration `20261117234001_Lot13_2_VoiceMessages.cs`, `AudioAvailability.cs`, `AudioOptionsValidator.cs`, `AudioProcessRunner.cs`, `DiskVoiceMessageStorage.cs`, `FfmpegVoiceAudioProcessor.cs`, `ConversationService.cs`.

Excluded: `20261117234001_Lot13_2_VoiceMessages.Designer.cs`, `MentoraDbContextModelSnapshot.cs`, `docs/review/**` (no `*.csv` in the diff). Exception applied: section "Lot 13.1" of `docs/review/lot12/12.5a/prod-runbook.md` (lines 521 to 570) was read. The `docs/review/lot13/**` files were included in the searches of area F.

## 2. Verdict

Code is ready. Deployment is not ready until one BLOCKER is fixed, and it is a runbook gap, not a code defect: L1, the runbook has no audio storage section (Audio__StoragePath, mounted folder, backup).

## 3. Findings

### L1. BLOCKER. The runbook has no audio storage section for api-demo and api-prod

- Where: `docs/review/lot12/12.5a/prod-runbook.md:521-570` (only `Cors__DebugOrigins`). Code that needs it: `Mentora.API/Program.cs:533-543`, `Mentora.Core/Options/AudioOptions.cs:52-55`, `Mentora.Infrastructure/Services/Audio/AudioAvailability.cs:86-94`, `Mentora.Infrastructure/Services/Audio/DiskVoiceMessageStorage.cs:86-91`.
- What is wrong: the code needs `Audio__StoragePath` set to an absolute, existing and writable folder. Nothing in the runbook sets it, mounts a host folder, or backs it up. The 13.3 and 13.4 evidence files only test locally (`/app/media` in the image).
- Why it matters for the Beta:
  - api-prod (Production): without the variable the API refuses to start (`OptionsValidationException`, then `InvalidOperationException` at `Program.cs:541-542`). That is a startup failure on PROD.
  - api-demo (Development): the API starts with `[AUDIO] configured=False` and every voice upload and every listen answers 503 `AUDIO_NOT_CONFIGURED`. The failure is silent.
  - If the variable points to a folder inside the container that is not a host mount (for example a folder created by hand in the container), files are written to the container layer and lost at the next `docker compose up --force-recreate`, which every deployment runs. The rows stay, and every listen then answers 404. That is data loss.
  - Backups are `pg_dump` only (`prod-runbook.md:25-30`). Restoring a dump without the audio folder gives rows with no file.
- Minimal fix: add a "Lot 13.2 to 13.4" section to the runbook, with:
  1. One host folder per environment, never shared (for example `~/mentora/audio-demo` and `~/mentora/audio-prod`), created before the deployment. The container runs as root (pre-existing, accepted), so owner root and mode 750 work. Note for later: if `USER app` is ever added, the folder must belong to UID 1654.
  2. In the compose file, for `api` and `api-prod`: one volume line (`./audio-demo:/data/audio` and `./audio-prod:/data/audio`) and `Audio__StoragePath=/data/audio`. Optionally set `Audio__MaxConcurrentDecodes` and `Audio__DecodeTimeoutSeconds` (defaults are 2 and 10).
  3. The expected startup line on both: `[AUDIO] configured=True ffmpeg=True ffprobe=True storage=True maxConcurrent=2 timeoutSeconds=10`. On api-demo, `configured=False` must stop the deployment, even though the API starts.
  4. Backup: a `tar` of each audio folder taken at the same time as the `pg_dump` (step 2).
  5. nginx: decide on `client_max_body_size` for both API confs (25M today, see L10).
  6. Rollback: going back to the previous image is safe for the database. The old code ignores the new columns, and its text inserts get `MESSAGE_TYPE='TEXT'` from the default, which passes both CHECKs. Voice messages then show as empty text. `Down` is refused once a voice message exists (by design).
  7. The pre-migration SQL of section 5.
- Confidence: high. What would prove it wrong: an existing written procedure (outside the repo) that sets the variable and the volume for both services.

### L2. SHOULD. A client disconnect during COMMIT can leave a voice message row whose file was deleted

- Where: `Mentora.Infrastructure/Services/ConversationService.cs:209-225` (`SaveChangesAsync(ct)` at 217, `tx.CommitAsync(ct)` at 218, `audioStorage.Delete(relativePath)` at 223).
- What is wrong: `ct` is the request token (`RequestAborted`). When the token fires while `COMMIT` is in flight, Npgsql sends a cancel request. A `COMMIT` that the server has already applied is not undone by that request. When the server does not answer the cancel in time, Npgsql breaks the connection and throws `OperationCanceledException` anyway. The catch then deletes the committed file. The row exists, and the listening route answers 404 to both participants for good (`ConversationAudioController.cs:53-57`). A broken connection during `COMMIT` (with or without the token) has the same unknown outcome.
- Why it matters: mobile uploads on weak networks, so disconnects are expected. The probability per upload is low, but each case is a broken message in a real conversation that cannot be repaired from the app.
- Should `CancellationToken.None` be used after the file is committed: yes. Once `File.Move` has run, the work left is two short SQL statements. Finishing it costs nothing, and it keeps the user's message (the sender often retries after a disconnect, but the recipient should still get the first one).
- Minimal fix:
  1. Pass `CancellationToken.None` to `BeginTransactionAsync`, `SaveChangesAsync` and `CommitAsync` at lines 211, 217 and 218.
  2. In the catch, delete the file only when the failure is known to have happened before `COMMIT` was sent: set a `commitSent` flag just before `CommitAsync`. When the flag is set, check `db.Messages.AnyAsync(m => m.MessageId == messageId, CancellationToken.None)` on a fresh query and delete the file only when the row does not exist (if that check also fails, keep the file: an orphan file is harmless, a row with no file is not).
- Confidence: medium (the race window is a few milliseconds; the Npgsql cancel behavior is documented but was not reproduced here).

### L3. BACKLOG. `EnumMappings.MessageTypeMapping` is unused; the converter has its own copy

- Where: `Mentora.Core/Enums/EnumMappings.cs:549-567`, `Mentora.Infrastructure/Persistence/Configurations/MessageConfiguration.cs:107-119`.
- What is wrong: both use if chains (no switch) and agree today, but the mapping is written twice and the `EnumMappings` copy has no caller (grep: one hit, its own declaration).
- Fix: have the `ValueConverter` call `EnumMappings.MessageTypeMapping.ToWire` and `Parse`, or delete the unused class. Confidence: high.

### L4. BACKLOG. Orphan files are never cleaned up

- Where: `ConversationService.cs:168-245`, `DiskVoiceMessageStorage.cs:29-46`.
- What is wrong: if the process dies (OOM, container stop) while an upload runs, a `.incoming/*.upload` file stays. If it dies between `File.Move` (line 189) and `COMMIT`, a committed file stays with no row. Nothing sweeps them.
- Fix: a small cleanup at startup or daily: delete `.incoming` files older than one hour; optionally list files with no row. Confidence: high.

### L5. BACKLOG. The ffmpeg error head logged on a failed analysis can contain the internal file path

- Where: `Mentora.Infrastructure/Services/Audio/FfmpegVoiceAudioProcessor.cs:264-268`.
- What is wrong: ffmpeg error lines usually start with the input name, which is the `.incoming/<guid>.upload` path under the storage root. Not a secret and not user content, but it goes against the "never print a path" rule applied to the startup lines.
- Fix: replace `filePath` by `input` in the logged text (`stderrHead.Replace(filePath, "input")`). Confidence: medium.

### L6. BACKLOG. Disk usage has no limit or alert

- Where: `VoiceMessageRules.cs:10, 32-35`.
- What is wrong: within the accepted 13.K limit (30 uploads per 10 minutes, 5 MB each), one account can store up to about 900 MB per hour. There is no quota and no disk alert on the VPS. The host disk is shared with PostgreSQL.
- Fix: a disk usage alert on the VPS, and a size check of the audio folder in the post-deployment checks. Confidence: high.

### L7. BACKLOG. Files are always named `.aac`, also for `audio/mp4`

- Where: `DiskVoiceMessageStorage.cs:25-26`.
- What is wrong: cosmetic only; the content type comes from `MESSAGE_AUDIO_CONTENT_TYPE`. It can confuse someone who inspects or restores the folder by hand.
- Fix: none needed now; document it in the runbook section of L1. Confidence: high.

### L8. BACKLOG. Outside Development, `Cors__AllowedOrigins` refuses the host `localhost` but accepts loopback IPs

- Where: `Mentora.Core/Options/CorsOptions.cs:72-73`.
- What is wrong: `https://127.0.0.1:8443` or `https://[::1]:8443` passes the Production rules, while the debug origin rules refuse them. Only a configuration mistake can trigger it.
- Fix: treat `127.0.0.1`, `[::1]` and `*.localhost` like `localhost` in `IsLocalhostOrigin`. Confidence: high.

### L9. BACKLOG. Listening route: a file deleted between `Exists` and `OpenRead` gives a 500

- Where: `Mentora.API/Controllers/ConversationAudioController.cs:53-61`.
- What is wrong: a narrow race (only with a manual delete or a cleanup job). A 500 instead of the empty 404.
- Fix: open the stream in a try and map `FileNotFoundException` and `DirectoryNotFoundException` to `EmptyNotFound()`. Confidence: high.

### L10. BACKLOG. nginx body size: 25M today; setting 8m changes the 413 seen by the app

- Where: `docs/review/lot13/13.0/inventory.md:267` (`client_max_body_size 25M` on both API confs); `VoiceMessageRules.cs:29`.
- What is wrong: with 25M, nginx buffers bodies of up to 25 MB before the API answers its own enveloped 413 (with CORS headers). With `8m`, nginx answers first with its HTML 413 and no CORS header, so a browser debug client sees a CORS error instead of a 413 (native mobile still sees 413).
- Fix: either keep 25M (the API refuses above 8 MB as soon as it reads the body), or set a value just above 8 MB (for example `9m`) so the API envelope is kept between 8 and 9 MB. Write the choice in the runbook (L1). Confidence: medium.

### L11. BACKLOG. One em dash added in the docs

- Where: `docs/review/lot13/13.0/inventory.md:165`.
- What is wrong: one U+2014 in an added line (area G). No en dash added. Inherited dashes (for example in the `Tags` attribute of both message controllers) are not counted.
- Fix: replace it with a colon or a comma. Confidence: high.

## 4. Verified OK

A. CORS (13.1)
- `CorsOptions.Validate` (`CorsOptions.cs:57-101`): debug origins checked in every environment; at most 5 (`:82`); no wildcard, absolute origin, no credentials, no path, query or fragment (`:107-122`); http or https only (`:92-94`); host exactly `localhost` (`:95-96`, `localhost.` and `sub.localhost` refused); explicit non default port (`:97-98`). `AllowedOrigins`: https only and no `localhost` outside Development (`:70-73`). Messages carry the item number, never the value.
- Validated before migrations: `.Value` read at `Program.cs:509`, migrations at `:563`.
- Logs: `[CORS] origins=... localhost=...` and `[CORS] origin ...` unchanged (`Program.cs:511-514`); `[CORS] debugOrigins=N` and one `[CORS] debug origin` line per item added (`:515-518`).
- Policy: `AllowedOrigins` plus `DebugOrigins` in every environment, the 7 local origins only when `IsDevelopment()` (`Program.cs:468-472`), distinct (`:475`).
- Pipeline order unchanged: `UseForwardedHeaders`, `GlobalExceptionMiddleware`, Swagger, `UseCors`, `UseAuthentication`, `UseAuthorization`, `UseRateLimiter` (`Program.cs:581-600`). CORS headers are added through the response starting callback, so the 401, the 400 envelope written by the middleware and the 401 of the hub negotiate keep them (also shown in `13.1/runtime-evidence.md`).

B. Database and contract (13.2)
- `MessageType` enum (`MessageType.cs`), if chain converters (`MessageConfiguration.cs:107-119`), `HasDefaultValueSql("'TEXT'")` with `HasSentinel((MessageType)(-1))` (`:68-73`) and the C# initializer `= MessageType.Text` (`Message.cs:17`): EF always sends the value, and the default matches.
- Migration: `CK_MESSAGES_TYPE` (`IN ('TEXT','AUDIO')`); `CK_MESSAGES_AUDIO_FIELDS`: TEXT branch has all 5 audio columns `IS NULL`; AUDIO branch has an explicit `IS NOT NULL` on all 5 audio columns plus `MESSAGE_CONTENT = ''` (`MESSAGE_CONTENT` is `NOT NULL`, `MessageConfiguration.cs:24-27`). The TEXT branch does not test `MESSAGE_CONTENT`, so existing rows with empty content pass. Constraints added after the columns, so the default fills existing rows first. `Down` refused when an AUDIO row exists (`DO $$ ... RAISE EXCEPTION`).
- DTOs: new fields appended at the end only (`MessageDto.cs:27-31`, `ConversationDto.cs:44-47`). `LastMessageDto` built in 3 projections (`ConversationService.cs:322-335`, `:448-461`, `:502-515`), mapping after materialization (`ToLastMessageDto`, `:569-583`), one SQL query per read (inbox uses correlated subqueries, `:307-341`; message list one query, `:69-86`). No other place builds these DTOs (grep).
- `mediaUrl` = `/api/v1/conversations/{conversationId:D}/messages/{messageId:D}/audio` (`VoiceMessageRules.cs:47-48`), matches the listening route template (`ConversationAudioController.cs:12`). `durationSeconds` = round half away from zero, minimum 1 (`VoiceMessageRules.cs:53-54`). `waveform`, `mediaUrl`, `durationSeconds` are null for TEXT (`ConversationService.cs:564-566`, `:581-582`).
- SignalR payload: default JSON hub protocol (camelCase) with `JsonStringEnumConverter(SnakeCaseUpper)` (`Program.cs:390-395`), so `"messageType":"TEXT"` / `"AUDIO"`, same as REST (`Program.cs:445-446`).

C. Audio analysis and storage (13.3)
- `AudioOptions.Validate`: bounds 1 to 4 and 1 to 60 in every environment, `StoragePath` mandatory and fully qualified outside Development (`AudioOptions.cs:42-57`); validator wired with `ValidateOnStart` (`Program.cs:286-289`).
- `AudioProcessRunner`: `UseShellExecute = false`, `ArgumentList` only (`AudioProcessRunner.cs:48-57`); the only variable argument is a server-built path; stdout capped and process killed beyond the cap (`:96-103`); stderr drained in parallel, first 4 KB kept (`:75`, `:121-141`); tree killed on cancellation (`:73`) and in `finally` (`:117`); `using var process` (`:59`).
- `FfmpegVoiceAudioProcessor`: magic bytes (ADTS `0xFF`, `(b1 & 0xF6) == 0xF0`; M4A `ftyp` at 4..7) read from the content before any process (`:63-65`, `:149-170`); `-protocol_whitelist file` and `-f aac|mov` before the input for ffprobe and ffmpeg (`:99`, `:121`); `-t 121` (`:31`, `:122`); duration from the decoded sample count (`:133-134`); 48 slices, RMS, scaled 0 to 100, `max == 0` gives zeros (`:220-251`); semaphore released in `finally`, timeout mapped to Undecodable, client cancel rethrown (`:68-87`).
- `AudioAvailability`: `-hide_banner -version` with a 5 s timeout and exit code 0 for both tools; storage set, absolute, exists, write test file created then deleted (`AudioAvailability.cs:65-109`). Messages name the setting, never the path.
- `DiskVoiceMessageStorage`: relative path from two GUIDs only (`:25-26`); every full path checked to start with root plus separator (`:97-102`); `.incoming` under the root, then `File.Move(..., overwrite: false)` (`:37-46`); `Delete` and `DeleteIncoming` resolve inside the root first and swallow IO errors (`:57-83`).
- `AudioNotConfiguredException` caught before `InvalidOperationException`, 503 `AUDIO_NOT_CONFIGURED` (`GlobalExceptionMiddleware.cs:68-72`).
- `[AUDIO]` block after `[MUX]` and before `[ADMIN]` and the migrations, start refused outside Development (`Program.cs:530-543`).
- Dockerfile: one line in the `base` stage, `--no-install-recommends`, apt lists removed; `final` is built `FROM base`; nothing else changed (`Dockerfile:2`, `:22`).

D. Upload and listening (13.4)
- Order in `SendAudioMessageAsync`: link 404 (`:164`), declared length 422 (`:165-166`), storage 503 (`CreateIncomingFilePath`, `:168`), capped copy at `MaxSizeBytes + 1` (`:172-177`), analysis (`:179-184`), conversation (`:186`), file commit (`:189`), one transaction for message and conversation date (`:211-218`).
- File lifecycle: the incoming file is deleted in `finally` on every path (`:240-245`); the committed file is deleted when the insert fails (`:220-225`, see L2 for the one exception).
- 413 guard: first statement of both actions, before the link check (`MemberConversationMessagesController.cs:101-102`, same in the coach controller). It checks `Content-Length`, then replays `ReadFormAsync`: the form task is cached, so the original Kestrel `BadHttpRequestException` 413 (turned into a model state error by form binding, which `SuppressModelStateInvalidFilter` lets through, `Program.cs:438`) is found again. Other IO errors are caught and fall through to the 422 path. Envelope `{ success, data, error, statusCode }` (`:172-173`). `[RequestSizeLimit]` and `[RequestFormLimits]` both use `MaxRequestBytes` (`:85-86`).
- Listening route: plain `[Authorize]` uses the default scheme `JwtBearerDefaults.AuthenticationScheme` (`Program.cs:169`); the admin scheme is never the default (`:209-212`). `GetAudioFileForParticipantAsync` is one query (`ConversationService.cs:256-265`) and its `MEMBER_COACHES` condition (member id and coach id only) matches `ValidateAsync` (`:405-406`); `MemberCoach` has no status or active flag (`MemberCoach.cs:5-13`). Caller check by `memberId` or `coachId` claim, same as `ChatHub.EnsureCallerHasAccessAsync` (`ChatHub.cs:96-106`). Every failure returns the same empty 404 (`ConversationAudioController.cs:69-73`). `enableRangeProcessing: true`; `Cache-Control` and `X-Content-Type-Options` set before the result runs, so they are on 200, 206 and 416 (`:59-61`).
- `ProducesResponseType(typeof(FileStreamResult), ...)`: Swashbuckle 6.5.0 maps `FileResult` types to `type: string, format: binary`, so the schema is correct, not misleading (to confirm at runtime, section 5).
- Broadcast: `IHubContext<ChatHub>`, event `ReceiveMessage`, payload `MessageDto` like the hub (`ChatHub.cs:58-59`); after text and audio sends in both controllers; `CancellationToken.None`; failure logged with the exception type only, response unchanged (`MemberConversationMessagesController.cs:133-145`). `ChatHub` diff: only `GroupName` made `public static` (`ChatHub.cs:84`).
- Rate limit: `VoiceUpload` registered once (`Program.cs:386`); key `ClaimTypes.NameIdentifier` with `MapInboundClaims = true` (`Program.cs:177`), fallback `"no-user"`, never null; 30 per 10 minutes, no queue (`VoiceUploadRateLimiting.cs:28-39`); shared 429 status and `OnRejected` envelope (`QuizRateLimiting.cs:21`, `:40`); `UseRateLimiter` after authentication and authorization (`Program.cs:598-600`).
- Text `SendMessageAsync` only gained `MessageType = MessageType.Text` and the new DTO mapping (`ConversationService.cs:113-157`); `ChatHub.SendMessageAsync` unchanged.

F. Public repository
- `git --no-optional-locks diff --name-only "-G<pattern>" origin/main...feature/lot_13`:
  - JWT shape `eyJ[A-Za-z0-9_-]+[.][A-Za-z0-9_-]+[.][A-Za-z0-9_-]+`: empty. Control: 2 hits in `docs/review/lot7/claims-after-4a.json` (and `-4d`).
  - `[[]OTP[]]`: empty. Control: 1 hit in `Mentora.Infrastructure/Services/AuthService.cs`.
  - `gmail[.]com`: empty. Control: 1 hit in `docs/review/lot11/11.4/runtime-evidence.md`.
  - `web2success[.]fr`: empty. Control: 6 hits in `docs/review/lot8/vps/cleanup-vps.sql`.
  - `mentorapp[.]fr`: 5 files (`docs/review/lot12/12.5a/prod-runbook.md`, `docs/review/lot13/13.0/inventory-cors.md`, `docs/review/lot13/13.1/runtime-evidence.md`, `docs/review/lot13/13.3/runtime-evidence.md`, `docs/review/lot13/13.4/runtime-evidence.md`). Only two host names (the PROD API host and the admin host), no address (`-G@[A-Za-z0-9.-]*mentorapp[.]fr`: empty). Both hosts are already public on main (`git grep -l -F` on origin/main: 10 and 11 files). Not a secret. Control: 6 hits in `cleanup-vps.sql`.
  - the fixed UUID prefix of the PROD fixture accounts (not written here): empty. `example[.]invalid`: empty.
- MUX, Firebase, LiveKit and signed URLs: keyword hits in `docs/review/lot13/13.0/inventory.md:289`, `13.3/runtime-evidence.md:60`, `13.4/runtime-evidence.md:89` are variable names only (followed by a backtick or a comma, no value). `13.0/inventory-cors.md:152` has `token=...` (placeholder) and `13.4/runtime-evidence.md:238` has `token=True` (a boolean). No signed URL, no `X-Amz-Signature`, no `Expires=`.
- Configuration files touched by the lot: `Mentora.API/Dockerfile` only. No appsettings, `.env`, docker-compose, nginx, `.dockerignore` or `.gitattributes` in the diff stat (so the appsettings key command was not needed).

G. Dashes: 1 em dash and 0 en dash in the added lines (L11).

H. Build: `dotnet build --no-incremental`: 19 warnings, all CS1573, 0 errors.

## 5. Runtime proofs needed during deployment

1. Pre-migration SQL, read-only, on api-demo (`mentora_db`) and PROD (`mentora_prod`), before starting the new image:

```sql
-- a. Lot13_2 not applied yet (the last row must be the latest lot 12 migration)
SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 3;

-- b. None of the new columns or constraints exists already (0 rows each)
SELECT column_name FROM information_schema.columns
 WHERE table_schema = current_schema() AND table_name = 'MESSAGES'
   AND column_name IN ('MESSAGE_TYPE','MESSAGE_AUDIO_PATH','MESSAGE_AUDIO_CONTENT_TYPE',
                       'MESSAGE_AUDIO_DURATION_MS','MESSAGE_AUDIO_SIZE_BYTES','MESSAGE_AUDIO_WAVEFORM');
SELECT conname FROM pg_constraint WHERE conname IN ('CK_MESSAGES_TYPE','CK_MESSAGES_AUDIO_FIELDS');

-- c. Both CHECKs evaluated on every existing row as the migration will see it
--    (MESSAGE_TYPE = 'TEXT' from the default, the 5 audio columns NULL). Expected: both violation counts 0.
SELECT count(*) AS total_rows,
       count(*) FILTER (WHERE NOT (v.t IN ('TEXT','AUDIO'))) AS ck_type_violations,
       count(*) FILTER (WHERE NOT (
           (v.t = 'TEXT' AND v.p IS NULL AND v.c IS NULL AND v.d IS NULL AND v.s IS NULL AND v.w IS NULL)
           OR
           (v.t = 'AUDIO' AND m."MESSAGE_CONTENT" = '' AND v.p IS NOT NULL)
       )) AS ck_audio_violations,
       count(*) FILTER (WHERE m."MESSAGE_CONTENT" IS NULL) AS null_content,
       count(*) FILTER (WHERE m."MESSAGE_CONTENT" = '') AS empty_content_info_only
FROM "MESSAGES" m
CROSS JOIN LATERAL (SELECT 'TEXT'::varchar(10) AS t, NULL::varchar AS p, NULL::varchar AS c,
                           NULL::int AS d, NULL::int AS s, NULL::jsonb AS w) v;

-- d. Table size (the CHECK scan holds a short exclusive lock)
SELECT pg_size_pretty(pg_total_relation_size('"MESSAGES"'));
```

   Rows with an empty `MESSAGE_CONTENT` are allowed: the TEXT branch does not test the content. After the migration: `SELECT "MESSAGE_TYPE", count(*) FROM "MESSAGES" GROUP BY 1;` must return `TEXT` only.
2. Startup line on both services: `[AUDIO] configured=True ffmpeg=True ffprobe=True storage=True ...` (L1). On PROD, also the four `[CORS]` lines of `prod-runbook.md:549-554`.
3. Persistence: send one voice message on api-demo, recreate the container, listen again: 200 with the same bytes. Proves the host mount.
4. Listening through nginx: 200, then `Range: bytes=0-99` gives 206 with `Content-Range`; both carry `Cache-Control: private, max-age=31536000, immutable` and `X-Content-Type-Options: nosniff`. An admin token gives 401; a non-participant gives the empty 404.
5. Upload through nginx: a 6 MB file gives 422 (key `File`); a 9 MB body gives 413 (from nginx or the API, according to L10); the 31st upload in 10 minutes gives 429 with the envelope; each answer carries `Access-Control-Allow-Origin` for `http://localhost:8100` on PROD.
6. `ReceiveMessage` reaches a second connected client after a REST text send and after a REST voice send, with `messageType`, `mediaUrl`, `durationSeconds`, `waveform` in camelCase.
7. Existing api-demo data: `GET` messages of an old conversation returns `messageType: "TEXT"` and null audio fields; the coach inbox `lastMessage` has the 3 new fields.
8. Swagger `/swagger/mobile/swagger.json`: the 200 and 206 responses of the listening route have `type: string, format: binary` under `audio/aac` and `audio/mp4`.
9. Model and migration consistency, which this review could not run (`dotnet ef` forbidden): `dotnet ef migrations has-pending-model-changes` must report none.
10. Backup: the `tar` of the audio folder exists next to the `pg_dump` of the same day.

## 6. Fixes (18/11)

- L1 done: `docs/review/lot12/12.5a/prod-runbook.md:574-953`, section "Lot 13 : messages vocaux (stockage audio, taille des requêtes, origine de débogage)", right after "Lot 13.1". Choices that differ from the minimal fix above: host folders `~/mentora/media-demo` and `~/mentora/media-prod` mounted on `/app/media` (not `audio-*` on `/data/audio`); owner `mentora`, mode 750 (root in the container writes anyway). nginx: `client_max_body_size 8m` (L10 option "8m" taken: above 8 MB, nginx answers its own 413 without CORS header). Repository examples changed the same way: `nginx/conf.d/mentora.conf:40` and `nginx/conf.d/mentora-prod.conf.example:36` (`25M` to `8m`); `mentora-admin.conf.example` and `nginx.conf` have no `client_max_body_size` and were not touched. Rollback checked on `origin/main` (`Message.cs`, `MessageConfiguration.cs`, EF Core 8.0.0): the previous image works on the migrated schema.
- L2 done: `Mentora.Infrastructure/Services/ConversationService.cs:211-259`. After `CommitAsync` of the file, `BeginTransactionAsync`, `SaveChangesAsync` and `tx.CommitAsync` use `CancellationToken.None`. On failure, a fresh `AsNoTracking().AnyAsync` on the message id decides: row present, file kept and warning logged; row absent, file deleted; check failed, file kept and warning logged with the exception type name only. The original exception is always rethrown. A logger was added to the constructor (`:13`, `:25`) for these warnings. Build: 0 errors, 19 CS1573. Runtime regression of the happy path: at the api-demo deployment (runbook step 10).
- L11 done: `docs/review/lot13/13.0/inventory.md:165`, both em dashes of the quoted Tags replaced by a comma.
- BACKLOG, not touched: L3, L4, L5, L6, L7, L8, L9, L10 (the 8m decision is written in the runbook; nothing else changed for it).
