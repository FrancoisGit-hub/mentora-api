# Lot 13.4: voice upload routes, listening route, real time broadcast, rate limit

Branch `feature/lot_13`, on top of `409ca88`. Decision 13.O: every runtime proof ran against the API in the Docker image `mentora-api:lot13-4-test` (ffmpeg is not installed on the development PC). Q7 is the only proof run against the local Development API, on purpose (no ffmpeg).

Accounts are named "local coach A", "local member" and "fixture coach B" (a throwaway coach created for this run with fixed ids `13400000-0000-4000-8000-000000000001` (user) and `...0002` (coach), no link with the local member, deleted at cleanup).

## 1. Starting point

The working tree already held a first draft of the 13.4 code from an interrupted earlier session (no build, no runtime, fixture never committed). It was reviewed line by line against B1 to B5 before any change. One gap was fixed (G1 below); two additions were kept on purpose (A1, A2).

- G1 (fixed): the rate limit partition key fell back to the raw `sub` claim, then to `"unknown"`, with a `user:` prefix. Now: `FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "no-user"`, no prefix, the same lookup as `AccountController.cs:29`, `AuthController.cs:67`, `UserDevicesController.cs:27` (`MapInboundClaims = true` at `Program.cs:177` maps the JWT `sub` claim to `ClaimTypes.NameIdentifier`).
- A1 (kept): a 413 guard in both upload actions, see B1.
- A2 (kept): `IConversationService.GetAudioFileForParticipantAsync`, the single query of B3.

## 2. Implementation

### B1 Upload routes

| | Member | Coach |
|---|---|---|
| Action | `MemberConversationMessagesController.cs:83-110` | `CoachConversationMessagesController.cs:83-110` |
| Route | `POST /api/v1/member/coaches/{coachId}/conversation/messages/audio` | `POST /api/v1/coach/members/{memberId}/conversation/messages/audio` |
| Policy (class level) | `MemberOnly` (`:16`), memberId from the `memberId` claim (`:104`) | `CoachOnly` (`:16`), coachId from the `coachId` claim (`:104`) |
| Swagger group | `mobile` (`:17`) | `coach` (`:17`) |

- Attributes (`:83-94`): `[HttpPost("audio")]`, `[Consumes("multipart/form-data")]`, `[RequestSizeLimit(VoiceMessageRules.MaxRequestBytes)]`, `[RequestFormLimits(MultipartBodyLengthLimit = VoiceMessageRules.MaxRequestBytes)]`, `[EnableRateLimiting(VoiceUploadRateLimiting.PolicyName)]`, ProducesResponseType 201, 404, 413, 415, 422, 429, 503.
- Form fields: `file` (IFormFile?) and `durationMs` (int?, accepted and ignored: the server measures the duration).
- The controller passes `file?.OpenReadStream()` and `file?.Length` to the service (Core stays free of ASP.NET types), broadcasts (B4) and returns `CreatedAtAction(nameof(List), ..., new SendMessageResponseDto(message))`: 201, no envelope, same as the text send.
- New constants in `VoiceMessageRules.cs`: `MaxRequestBytes = 8 * 1024 * 1024`, `VoiceUploadPermitLimit = 30`, `VoiceUploadWindowMinutes = 10`.
- A1, 413 guard (`:101-102`, helpers `:151-173`): `SuppressModelStateInvalidFilter = true` (`Program.cs:438`) means Kestrel's 413 thrown during form binding is turned into a model state error and the file binds as null, which would answer 422 "File is required." instead of 413. The guard answers 413 when `Content-Length` is above 8 MB, or when replaying the cached form read rethrows the server's 413 (chunked body). Body: `{"success":false,"data":null,"error":"Payload too large.","statusCode":413}`. Locally the 413 therefore comes from this guard; on the VPS, nginx (`client_max_body_size 8m`) answers 413 itself before the request reaches the API.

### B2 Service

`ConversationService.SendAudioMessageAsync` (`ConversationService.cs:159-245`), declared in `IConversationService.cs`. `IValidator<SendAudioMessageRequest>`, `IVoiceAudioProcessor` and `IVoiceMessageStorage` are injected (`:17-23`). Order of checks (13.J):

1. `ValidateAsync` (`:164`): 404 "Conversation not found." without a MEMBER_COACHES link.
2. `SendAudioMessageRequestValidator` (`Mentora.Core/Validators/Conversation/SendAudioMessageRequestValidator.cs`) on the record `SendAudioMessageRequest(long? FileLength)`, key `File`: "File is required." (absent or empty), "Audio file is too large (maximum 5 MB)." (above `MaxSizeBytes`). `ValidateAndThrowAsync`: 422 through the middleware.
3. `CreateIncomingFilePath()` (`:168`, 503 AUDIO_NOT_CONFIGURED when storage is not available), copy capped at `MaxSizeBytes + 1` bytes (`:172`, `CopyCappedAsync` `:369`); cap reached: incoming file deleted, 422 too large.
4. `AnalyzeAsync` (`:179`, 503 without ffmpeg); every non Ok outcome deletes the incoming file and maps to a 422 on key `File` (`AnalysisFailureMessage` `:390`).
5. Get or create the conversation (`:186`), new message id, `RelativePathFor`, `CommitAsync` (`:189`).
6. One transaction (`:211`): insert the AUDIO message (content `""`, path, content type, duration and waveform from the analysis, size = bytes actually copied, unread, `UtcNow`) and update `ConversationLastMessageDate`. If SaveChanges or Commit throws: `audioStorage.Delete(relativePath); throw;` (`:223-224`).
7. `finally` (`:240-244`): delete the incoming file if it still exists.
8. `return ToMessageDto(...)` (`:227`): mediaUrl, durationSeconds and waveform filled.

The text `SendMessageAsync` is unchanged.

### B3 Listening route

`Mentora.API/Controllers/ConversationAudioController.cs`: `GET /api/v1/conversations/{conversationId:guid}/messages/{messageId:guid}/audio`, `[Authorize]` (default scheme = app Bearer, `Program.cs:169`; no default or fallback policy, so an admin token is refused), group `mobile`, tag `Conversation: audio`.

- `GetAudioFileForParticipantAsync` (`ConversationService.cs:248`): one query, message in that conversation with type AUDIO, caller memberId equals the conversation member or caller coachId equals the conversation coach, MEMBER_COACHES link (member, coach) still present.
- Every other case (unknown ids, text message, other conversation, not a participant, link removed, file missing on disk) answers 404 with an empty body (`EmptyResult`, not `NotFound()`, which would add a ProblemDetails body with a per request traceId). A missing file also logs `[AUDIO] stored file missing for message {MessageId}`.
- Success: `File(storage.OpenRead(path), contentType, enableRangeProcessing: true)` with `Cache-Control: private, max-age=31536000, immutable` and `X-Content-Type-Options: nosniff`.
- 503 AUDIO_NOT_CONFIGURED when storage is not available (thrown by `Exists`/`OpenRead`). A non participant gets 404 before that check (accepted: nothing leaks).

### B4 Broadcast

Both controllers inject `IHubContext<ChatHub>` (`:21`). After a successful REST send, text (`:63`) and audio (`:108`), `BroadcastAsync` (`:133-145`) calls `Clients.Group(ChatHub.GroupName(message.ConversationId)).SendAsync("ReceiveMessage", message, CancellationToken.None)`. A failure is logged as a warning `[CHAT] broadcast failed for message {MessageId}: {Error}` and never changes the HTTP response. `ChatHub.cs`: only `GroupName` became `public static` (`:84`); `SendMessageAsync` stays text only.

### B5 Rate limit

`Mentora.API/Authorization/VoiceUploadRateLimiting.cs`: `AddVoiceUploadPolicy`, policy `VoiceUpload`, fixed window per user, key `FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "no-user"`, PermitLimit 30, Window 10 minutes, QueueLimit 0, AutoReplenishment true. Registered in the existing `AddRateLimiter` call (`Program.cs:386`) next to the quiz and admin policies, unchanged. The 429 envelope and Retry-After come from the shared `OnRejected` (`QuizRateLimiting.cs:40-54`). The limiter runs after authorization (`Program.cs:598-600`), so a request without a valid token is answered 401 and not counted; every authenticated request to the two routes counts, refused ones included.

## 3. Files changed

- `Mentora.API/Controllers/Member/MemberConversationMessagesController.cs`
- `Mentora.API/Controllers/Coach/CoachConversationMessagesController.cs`
- `Mentora.API/Controllers/ConversationAudioController.cs` (new)
- `Mentora.API/Hubs/ChatHub.cs` (GroupName public static only)
- `Mentora.API/Authorization/VoiceUploadRateLimiting.cs` (new)
- `Mentora.API/Program.cs` (one line: `o.AddVoiceUploadPolicy();`)
- `Mentora.Core/Interfaces/IConversationService.cs`
- `Mentora.Core/Messaging/VoiceMessageRules.cs` (3 constants)
- `Mentora.Core/Validators/Conversation/SendAudioMessageRequestValidator.cs` (new)
- `Mentora.Infrastructure/Services/ConversationService.cs`
- `docs/review/lot13/13.4/runtime-evidence.md` (this file)

No migration, no NuGet package, no change to the 13.3 audio services, Dockerfile, appsettings, nginx or compose files.

## 4. Build

`dotnet build --no-incremental`: 0 errors, 19 warnings, all the existing CS1573.

`docker build --platform linux/amd64 -f Mentora.API/Dockerfile -t mentora-api:lot13-4-test .`: exit 0, ffmpeg and base layers from the cache, image size 942MB (same as `lot13-3-test`, now removed).

## 5. Fixture and container setup

- Fixture coach B: one USERS row and one COACHES row with the fixed ids, columns checked against `information_schema`, every non date column copied from local coach A's rows except the email and login (fixture address), `USER_CREATED_DATE`, `USER_MODIFICATION_DATE` and `COACH_CREATED_DATE` set to `now()`, `USER_DISABLED_DATE` and `USER_DELETION_REQUESTED_DATE` NULL, guarded by coach A's id and email, a DO block aborting unless exactly one row per table. Dry run with ROLLBACK first, then the COMMIT file (last line only differed). 0 MEMBER_COACHES link.
- Container: `docker run -d --name mentora-l134-api --platform linux/amd64 -p 5243:8080 -v <scratch>\media:/app/media -e NAME ... mentora-api:lot13-4-test`, 15 variables passed as `-e NAME` without a value (read by docker from the helper script environment): `ASPNETCORE_ENVIRONMENT=Production`, a connection string to the local database through `host.docker.internal:5433` built in memory from `docker-compose.local.yml`, a random 48 byte `Jwt__Secret`, `AdminAuth__JwtSecret`, `AdminAuth__Emails` and `EmailRouting__TestRecipients` inherited, `Email__Provider=Logging`, throwaway `Mux__TokenId`, `Mux__TokenSecret`, `Mux__WebhookSecret`, `Mux__CorsOrigin=https://admin.mentorapp.fr`, throwaway `LiveKit__ApiKey` and `LiveKit__ApiSecret`, `Cors__AllowedOrigins=https://admin.mentorapp.fr`, `Audio__StoragePath=/app/media`.
- Startup (filtered):

```
[EMAIL] routing mode=TEST provider=Logging testRecipients=4 productionBcc=0
[CORS] origins=1 localhost=False
[CORS] origin https://admin.mentorapp.fr
[CORS] debugOrigins=0
[MUX] configured=True corsOrigin=https://admin.mentorapp.fr apiBase=api.mux.com
[AUDIO] configured=True ffmpeg=True ffprobe=True storage=True maxConcurrent=2 timeoutSeconds=10
[ADMIN] auth emails=4 accessMinutes=15 refreshHours=12
No migrations were applied. The database is already up to date.
[ADMIN] accounts listed=4 created=0 reenabled=0 disabled=0
Application started. Press Ctrl+C to shut down.
```

- Tokens: OTP request and verify for the 3 accounts (code read in memory from the container log, never printed): 200/200 each; coach A and coach B `userType=COACH` with a coachId claim, local member `userType=MEMBER` with a memberId claim.
- Preflight: last migration `20261117234001_Lot13_2_VoiceMessages`, the local conversation had 0 messages and a NULL `CONVERSATION_LAST_MESSAGE_DATE`, the whole MESSAGES table was empty, run start `2026-11-18 01:31:58.193531+00`.

## 6. Runtime proofs

### Q1 Text broadcast after a REST send (13.I)

Raw SignalR client (negotiate, ClientWebSocket, JSON protocol) connected with the member token, `JoinConversationAsync` completed without error.

```
[Q1 a] coach REST status=201 messageType=TEXT content=l134-text-coach
[Q1 a] member socket: target=ReceiveMessage keys=messageId,conversationId,content,senderType,senderId,isRead,sentDate,readDate,messageType,mediaUrl,durationSeconds,waveform
[Q1 a] member socket: messageType=TEXT sameMessageId=True
[Q1 b] member REST status=201 messageType=TEXT content=l134-text-member
[Q1 b] member socket: target=ReceiveMessage keys=messageId,conversationId,content,senderType,senderId,isRead,sentDate,readDate,messageType,mediaUrl,durationSeconds,waveform
[Q1 b] member socket: messageType=TEXT sameMessageId=True
```

The sender's own socket also receives its message (the app deduplicates by messageId).

### Q2 Accepted uploads

All 201, `messageType=AUDIO`, `content=''`, `mediaUrl=/api/v1/conversations/{conversationId}/messages/{messageId}/audio`, 48 waveform values.

| Case | Upload | durationSeconds | min | max | first 4 | last 4 |
|---|---|---|---|---|---|---|
| a | member, tone-3s.aac, `durationMs=99999` (ignored) | 3 | 83 | 100 | 83,98,100,98 | 100,99,99,83 |
| b | coach A, ramp-4s.aac | 4 | 1 | 100 | 1,3,5,7 | 99,100,100,88 |
| c | member, tone-3s.m4a | 3 | 81 | 100 | 100,97,98,96 | 97,97,97,81 |
| d | coach A, silence-2s.aac | 2 | 0 | 0 | 0,0,0,0 | 0,0,0,0 |

- (b) rising: value 0 = 1 < value 47 = 88. The member's socket received `ReceiveMessage` with the same messageId, `messageType=AUDIO`, the same mediaUrl, `durationSeconds=4` and the same 48 waveform values, camelCase keys as in Q1.
- Database:

```
case|type|contentType|durationMs|sizeBytes|waveformLen
a|AUDIO|audio/aac|3041|25395|48
b|AUDIO|audio/aac|4040|34133|48
c|AUDIO|audio/mp4|3018|25809|48
d|AUDIO|audio/aac|2043|985|48
```

Every size equals the source file size.

- Container log:

```
[AUDIO] analysis outcome=Ok durationMs=3041 elapsedMs=382
[AUDIO] analysis outcome=Ok durationMs=4040 elapsedMs=242
[AUDIO] analysis outcome=Ok durationMs=3018 elapsedMs=197
[AUDIO] analysis outcome=Ok durationMs=2043 elapsedMs=172
```

### Q3 Refusals

| Case | Request | Status | Envelope | New `[AUDIO] analysis` line |
|---|---|---|---|---|
| a | member, tone-3s.wav | 422 | `File: Unsupported audio format (AAC in ADTS or M4A only).` | `outcome=UnsupportedFormat durationMs=(null) elapsedMs=22 exitCode=(null)` (no ffprobe run) |
| b | member, short-0.2s.aac | 422 | `File: Audio is too short (minimum 0.3 s).` | `outcome=TooShort durationMs=232 elapsedMs=257 exitCode=0` |
| c | member, long-125s.aac | 422 | `File: Audio is too long (maximum 120 s).` | `outcome=TooLong durationMs=121000 elapsedMs=358 exitCode=0` (decode capped at 121 s) |
| d | member, fake-adts.aac | 422 | `File: Audio file could not be decoded.` | `outcome=Undecodable durationMs=(null) elapsedMs=252 exitCode=1` (decoder errors: reserved bit set, prediction not allowed in AAC-LC) |
| e | member, big-6mb.aac (6291456 B) | 422 | `File: Audio file is too large (maximum 5 MB).` | none |
| f | member, multipart without the file field | 422 | `File: File is required.` | none |
| g | member, JSON `{"content":"x"}`, application/json | 415 | empty body, no Content-Type | none |
| h | member, "hello" as `file`, filename voice.aac, part type audio/aac | 422 | `File: Unsupported audio format (AAC in ADTS or M4A only).` | `outcome=UnsupportedFormat elapsedMs=42` |
| i | member, 9 MB random file | 413 | `{"success":false,"data":null,"error":"Payload too large.","statusCode":413}` | none |
| j1 | member to a random coachId | 404 | `error=Conversation not found.` | none |
| j2 | fixture coach B to the local member | 404 | `error=Conversation not found.` | none |
| k | no token | 401 | empty body | none |

After Q3: `media\.incoming` existed with 0 entries (every refused upload cleaned); 4 files under `media\{conversationId}`, 4 AUDIO rows in the database.

### Q4 Listening route

```
[Q4 a] member GET Q2(a) status=200 Content-Type=audio/aac Content-Length=25395 (MESSAGE_AUDIO_SIZE_BYTES=25395, equal=True) Accept-Ranges=bytes
[Q4 a]   X-Content-Type-Options=nosniff bytes=25395 sha256EqualsTone3sAac=True
[Q4 b] coachA GET Q2(a) status=200 Content-Type=audio/aac Content-Length=25395 equal=True Accept-Ranges=bytes sha256EqualsTone3sAac=True
[Q4 c] Range bytes=0-99 status=206 Content-Range=bytes 0-99/25395 Content-Length=100 bytes=100
[Q4 c] Range bytes=100- status=206 Content-Range=bytes 100-25394/25395 Content-Length=25295 bytes=25295
[Q4 c]   both parts joined: sha256EqualsTone3sAac=True
[Q4 d] member GET Q2(c) status=200 Content-Type=audio/mp4 bytes=25809
[Q4 e] coach B GET member audio Q2(a): status=404 bodyBytes=0
[Q4 e] member GET random messageId: status=404 bodyBytes=0
[Q4 e] member GET Q1 text messageId: status=404 bodyBytes=0
[Q4 e] member GET right messageId, random conversationId: status=404 bodyBytes=0
[Q4 e]   all 404 bodies identical=True
[Q4 f] no token status=401 bodyBytes=0
```

Raw headers on the wire (curl, member token):

```
HTTP/1.1 200 OK
Content-Type: audio/aac
Accept-Ranges: bytes
Cache-Control: private, max-age=31536000, immutable
X-Content-Type-Options: nosniff
```

### Q5 Rate limit (13.K)

Container restarted (fresh window), new tokens.

```
coach A requests 1-30 (no file field): 422 x30
coach A request 31: status=429 Retry-After=600 body={"success":false,"data":null,"error":"Too many requests.","statusCode":429}
member request 1 (no file field, separate partition): status=422 File: File is required.
coach A request 32: status=429
```

### Q6 Concurrency

4 uploads of tone-3s.aac sent in parallel by the member: `201,201,201,201`, 4 distinct messageIds, total wall time 786 ms.

```
[AUDIO] analysis outcome=Ok durationMs=3041 elapsedMs=253
[AUDIO] analysis outcome=Ok durationMs=3041 elapsedMs=296
[AUDIO] analysis outcome=Ok durationMs=3041 elapsedMs=479
[AUDIO] analysis outcome=Ok durationMs=3041 elapsedMs=477
```

The last two are longer, consistent with `maxConcurrent=2`.

### Q7 Local Development without ffmpeg

Container stopped first. Local API: `dotnet run --project Mentora.API --no-build --launch-profile http` (Development, port 5243). Check before start: `AudioEnvVarCount=0`, `Email__Provider is Logging=True`.

```
[EMAIL] routing mode=TEST provider=Logging testRecipients=4 productionBcc=0
[AUDIO] configured=False ffmpeg=False ffprobe=False storage=False maxConcurrent=2 timeoutSeconds=10
No migrations were applied. The database is already up to date.
Application started. Press Ctrl+C to shut down.
[Q7] member token request=200 verify=200 token=True
[Q7 a] member tone-3s.aac to local API: status=503 body={"success":false,"data":null,"error":"AUDIO_NOT_CONFIGURED","statusCode":503}
[Q7 b] member upload without file: status=422 File: File is required.
[Q7] local API stopped; port5243InUse=False
```

Validation runs before the audio check.

### Q8 Code reading (no fault injection)

`ConversationService.cs`:

```csharp
168        var incomingPath = audioStorage.CreateIncomingFilePath();   // outside the try: 503 before any file exists
175                audioStorage.DeleteIncoming(incomingPath);           // copy cap reached
182                audioStorage.DeleteIncoming(incomingPath);           // analysis outcome not Ok
189            await audioStorage.CommitAsync(incomingPath, relativePath, ct);
...
209            try
210            {
211                using var tx = await db.Database.BeginTransactionAsync(ct);
...
217                await db.SaveChangesAsync(ct);
218                await tx.CommitAsync(ct);
219            }
220            catch
221            {
222                // 13.J: the committed file never outlives a failed insert.
223                audioStorage.Delete(relativePath);
224                throw;
225            }
...
240        finally
241        {
242            // Every path: the incoming file is gone after the call (moved by the commit, or deleted here).
243            if (File.Exists(incomingPath))
244                audioStorage.DeleteIncoming(incomingPath);
245        }
```

## 7. Differences from the expected results (accepted)

1. Q2(a) and Q2(c): "all close to 100" holds for the inner bars (96 to 100), but the first and last bars are 83 (ADTS) and 81 (M4A). Cause: the AAC encoder fade at the very start and end of the file; the waveform reflects it faithfully.
2. Q2(b): the waveform rises, but value 47 is 88, not 100; the peak of 100 is at bars 45 and 46. Same cause (encoder fade at the end of the file).
3. Q3(d): the fake ADTS file is classed as Undecodable (the prompt allowed unsupported or undecodable).
4. Q3(g): the 415 comes from the framework `[Consumes]` check with an empty body and no Content-Type, not the envelope. Consistent with the existing empty 401 and 403 responses; to be told to PJ.
5. Q3(i): the 413 comes from the A1 guard locally (enveloped). On the VPS, nginx `client_max_body_size 8m` answers 413 before the API.

## 8. API documentation

Common rules: JSON uses camelCase keys and enums in SNAKE_CASE_UPPER (`AUDIO`, `TEXT`, `MEMBER`, `COACH`). Errors use the envelope `{success, data, error, statusCode}`; 422 adds `errors` keyed by field (`File`).

### POST /api/v1/member/coaches/{coachId}/conversation/messages/audio

- Auth: app Bearer token, policy `MemberOnly` (member token). The member is taken from the token.
- Body: `multipart/form-data`, maximum 8 MB in total.
  - `file` (required): the recording, AAC in ADTS (`.aac`) or M4A (`.m4a`), maximum 5 MB, 0.3 s to 120 s. The declared part Content-Type and file name are not trusted: the server checks the bytes.
  - `durationMs` (optional int): accepted and ignored, the server measures the duration.
- Rate limit: 30 requests per user per 10 minutes, refused uploads included.
- Responses:

| Status | When | Body |
|---|---|---|
| 201 | Voice message saved and broadcast | `SendMessageResponseDto` (below), no envelope, `Location` to the message list |
| 401 | No or invalid token | empty |
| 403 | Token without the policy (for example a coach token) | empty |
| 404 | Member not linked to this coach (checked first, before any file processing) | envelope, `error: "Conversation not found."` |
| 413 | Request body above 8 MB (nginx on the VPS, the API guard locally) | envelope `"Payload too large."` locally |
| 415 | Not multipart/form-data | empty |
| 422 | Invalid file | envelope with `errors.File`, one of the messages below |
| 429 | More than 30 uploads in 10 minutes | envelope `"Too many requests."`, `Retry-After` in seconds |
| 503 | Audio not configured on this server | envelope `error: "AUDIO_NOT_CONFIGURED"` |

422 messages (key `File`):

- `File is required.`
- `Audio file is too large (maximum 5 MB).`
- `Unsupported audio format (AAC in ADTS or M4A only).`
- `Audio is too short (minimum 0.3 s).`
- `Audio is too long (maximum 120 s).`
- `Audio file could not be decoded.`

Example 201 body (waveform shortened here; the API always returns 48 integers from 0 to 100):

```json
{
  "message": {
    "messageId": "<messageId>",
    "conversationId": "<conversationId>",
    "content": "",
    "senderType": "MEMBER",
    "senderId": "<memberId>",
    "isRead": false,
    "sentDate": "<UTC ISO 8601>",
    "readDate": null,
    "messageType": "AUDIO",
    "mediaUrl": "/api/v1/conversations/<conversationId>/messages/<messageId>/audio",
    "durationSeconds": 3,
    "waveform": [83, 98, 100, 98, "... 40 more values ...", 100, 99, 99, 83]
  }
}
```

### POST /api/v1/coach/members/{memberId}/conversation/messages/audio

Same as the member route, with policy `CoachOnly` (coach token), the coach taken from the token, `senderType: "COACH"`, and 404 when the coach is not linked to this member.

### GET /api/v1/conversations/{conversationId}/messages/{messageId}/audio

- Auth: app Bearer token (`[Authorize]`, no policy). The caller must be the member or the coach of the conversation, and the member/coach link must still exist. Admin tokens are refused (401).
- This is the `mediaUrl` of an audio message. The client sends its Bearer token in the request.
- Range requests supported.
- Responses:

| Status | When | Body and headers |
|---|---|---|
| 200 | Whole file | binary, `Content-Type: audio/aac` or `audio/mp4`, `Content-Length`, `Accept-Ranges: bytes`, `Cache-Control: private, max-age=31536000, immutable`, `X-Content-Type-Options: nosniff` |
| 206 | `Range` header | the range, `Content-Range: bytes <start>-<end>/<size>`, same headers |
| 401 | No or invalid token | empty |
| 404 | Unknown ids, text message, message of another conversation, not a participant, link removed, file missing | empty, identical in every case |
| 503 | Audio storage not configured (participants only) | envelope `error: "AUDIO_NOT_CONFIGURED"` |

### Real time: ReceiveMessage

After every successful REST send (text and audio) the API broadcasts `ReceiveMessage` to the SignalR group of the conversation (hub `/hubs/chat`, joined with `JoinConversationAsync(conversationId)`). The sender's own connections receive it too: deduplicate by `messageId`. Payload = the `MessageDto` (same object as `message` in the 201 body). Example frame:

```json
{
  "type": 1,
  "target": "ReceiveMessage",
  "arguments": [
    {
      "messageId": "<messageId>",
      "conversationId": "<conversationId>",
      "content": "",
      "senderType": "COACH",
      "senderId": "<coachId>",
      "isRead": false,
      "sentDate": "<UTC ISO 8601>",
      "readDate": null,
      "messageType": "AUDIO",
      "mediaUrl": "/api/v1/conversations/<conversationId>/messages/<messageId>/audio",
      "durationSeconds": 4,
      "waveform": [1, 3, 5, 7, "... 40 more values ...", 99, 100, 100, 88]
    }
  ]
}
```

For a text message: `messageType: "TEXT"`, `mediaUrl`, `durationSeconds` and `waveform` are null.

## 9. Cleanup

1. Container removed (`docker rm -f mentora-l134-api`), port 5243 free.
2. One SQL file, dry run with ROLLBACK, then the COMMIT file (last line only differed). Output of the COMMIT run:

```
before: audio_to_delete=8 l134_text_to_delete=2 conversation_messages=10
DELETE 10        messages of the local conversation sent after the run start
UPDATE 1         CONVERSATION_LAST_MESSAGE_DATE restored to NULL (preflight value)
DELETE 4         OFFER_PROGRAMS of coach B
DELETE 1         COACH_PARAMETERS of coach B
DELETE 0         AGENT_TOKENS of coach B
DELETE 0         MEMBER_COACHES of coach B
DELETE 2         AUTH_OTPS of coach B
DELETE 2         AUTH_REFRESH_TOKENS of coach B
DELETE 0         USER_DEVICES of coach B
DELETE 1         COACHES (coach B)
DELETE 1         USERS (coach B)
final: audio_messages_total=0 l134_messages_total=0 conversation_messages=0 last_message_date_is_null=t fixed_id_users=0 fixed_id_coaches=0
NOTICE: tables still referencing a 13400000- id: 0
NOTICE: created since run start: AUTH_OTPS = 5, AUTH_REFRESH_TOKENS = 5
auth rows since run start: local coach A 2 OTP / 2 refresh, local member 3 OTP / 3 refresh
COMMIT
```

- Seeder rows of coach B: the startup seeders (`CoachParameterSeeder`, `OfferProgramSeeder`) ran at each API start and created 1 COACH_PARAMETERS row and 4 OFFER_PROGRAMS rows for fixture coach B. They were found by a read only scan of every table referencing USERS or COACHES and deleted in the cleanup transaction.
- The 10 auth rows of local coach A (2 OTP, 2 refresh tokens) and the local member (3 OTP, 3 refresh tokens) stay in the local database (accepted). They come from the token steps (container twice, local API once for the member).
- A DO block in the transaction raised an exception if any final check failed.

3. `$env:TEMP\mentora-lot13-134\` deleted (helper scripts, tokens, media files, SQL files): `Test-Path` False. Image `mentora-api:lot13-3-test` removed, `mentora-api:lot13-4-test` kept, `$env:TEMP\mentora-lot13-audio\` kept (9 files).
