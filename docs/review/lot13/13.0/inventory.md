# Lot 13.0: read-only inventory for voice messages in the chat

- Date: 2026-11-17
- Branch: `feature/lot_13` (created from `origin/main`, upstream unset)
- HEAD: `9337c5d docs(lot12): wave 2 VPS deployment evidence and runbook (MUX)`
- origin/main: `9337c5d` (same commit)
- Scope: inventory only. No design, no code. Paths are relative to the repo root.

---

## 1. Preflight and lot branch

| Step | Command | Result |
|---|---|---|
| 1 | `git --no-optional-locks status --short` | empty (clean tree) |
| 2 | `git --no-optional-locks branch --show-current` | `feature/lot_12` |
| 3 | `git fetch origin` (approved) | no output |
| 4 | `git --no-optional-locks log --oneline -1 origin/main` | `9337c5d docs(lot12): wave 2 VPS deployment evidence and runbook (MUX)` (as expected) |
| 5 | `git --no-optional-locks fsck --no-dangling` | no output (OK) |
| 6 | `git switch -c feature/lot_13 origin/main` (approved) | `Switched to a new branch 'feature/lot_13'` |
| 7 | `git branch --unset-upstream` (approved) | no output |
| 8 | `git --no-optional-locks branch --show-current` / `log --oneline -1` | `feature/lot_13` / `9337c5d` |

`Mentora.Admin/wwwroot/appsettings.Development.json` was not modified (clean tree), so it was not displayed.

---

## 2. Chat data model

### 2.1 Entities

`Mentora.Core/Entities/Conversation.cs`
| Line | Property | Type |
|---|---|---|
| 5 | ConversationId | Guid |
| 6 | MemberId | Guid |
| 7 | CoachId | Guid |
| 8 | ConversationCreatedDate | DateTime |
| 9 | ConversationLastMessageDate | DateTime? |
| 11 | Member (nav) | Member |
| 12 | Coach (nav) | Coach |
| 13 | Messages (nav) | ICollection&lt;Message&gt; |

`Mentora.Core/Entities/Message.cs`
| Line | Property | Type |
|---|---|---|
| 7 | MessageId | Guid |
| 8 | ConversationId | Guid |
| 9 | MessageContent | string (initialised to `""`) |
| 10 | MessageSenderType | MessageSenderType |
| 11 | MessageSenderId | Guid |
| 12 | MessageIsRead | bool |
| 13 | MessageSentDate | DateTime |
| 14 | MessageReadDate | DateTime? |
| 16 | Conversation (nav) | Conversation |

No message type, media, duration or waveform property exists (see 15).

`Mentora.Core/Enums/MessageSenderType.cs:3-7`: `enum MessageSenderType { Member, Coach }`.

### 2.2 EF configuration

`Mentora.Infrastructure/Persistence/Configurations/ConversationConfiguration.cs`
- Table `CONVERSATIONS` (:11). PK ConversationId (:13).
- `CONVERSATION_ID` uuid, default `gen_random_uuid()` (:14-16).
- `MEMBER_ID` required (:18-20); `COACH_ID` required (:22-24).
- `CONVERSATION_CREATED_DATE` required, default `NOW()` (:26-29).
- `CONVERSATION_LAST_MESSAGE_DATE` nullable (:31-32).
- Unique index `IX_CONVERSATIONS_MEMBER_COACH` on (MemberId, CoachId) (:35-37).
- FK Member: `OnDelete(Cascade)` (:39-42). FK Coach: `OnDelete(Restrict)` (:45-48), comment "coach history is never wiped".
- Messages: FK ConversationId `OnDelete(Cascade)` (:50-53).

`Mentora.Infrastructure/Persistence/Configurations/MessageConfiguration.cs`
- Table `MESSAGES` (:13). PK MessageId (:15).
- `MESSAGE_ID` uuid, default `gen_random_uuid()` (:16-18).
- `CONVERSATION_ID` required (:20-22).
- `MESSAGE_CONTENT` type `text`, required (NOT NULL) (:24-27).
- `MESSAGE_SENDER_TYPE` varchar(10), required, value converter Member→`"MEMBER"`, Coach→`"COACH"`; any value other than `"MEMBER"` reads back as Coach (:30-39).
- `MESSAGE_SENDER_ID` required (:41-43).
- `MESSAGE_IS_READ` required, default `false` (:45-48).
- `MESSAGE_SENT_DATE` required, default `NOW()` (:50-53).
- `MESSAGE_READ_DATE` nullable (:55-56).
- Index `IX_MESSAGES_CONVERSATION_SENT_DESC` on (ConversationId ASC, MessageSentDate DESC) (:59-61).
- FK Conversation `OnDelete(Cascade)` (:63-66), duplicated with ConversationConfiguration:50-53.

### 2.3 Migrations

- Creation: `Mentora.Infrastructure/Persistence/Migrations/20260619193709_AddConversationsAndMessages.cs`
  - `CONVERSATIONS` :14-40 (with `CONVERSATION_VISIO_URL` text null :21; FK coach Restrict :28-33; FK member Cascade :34-39).
  - `MESSAGES` :42-64 (`MESSAGE_CONTENT` text NOT NULL :48; `MESSAGE_SENDER_TYPE` varchar(10) :49; FK conversation Cascade :58-63).
  - Indexes :66-81 (`IX_CONVERSATIONS_COACH_ID`, `IX_CONVERSATIONS_MEMBER_COACH` unique, `IX_MESSAGES_CONVERSATION_SENT_DESC`).
- Later change: `20260730194501_AddLot5Parameters.cs:14-16` drops `CONVERSATION_VISIO_URL` (Down re-adds it :317-321).
- No other migration touches CONVERSATIONS or MESSAGES. Proof: `grep -E "MESSAGES|MESSAGE_|CONVERSATIONS"` over Migrations excluding `*.Designer.cs` → 3 files: AddConversationsAndMessages (25), AddLot5Parameters (5), ModelSnapshot (14).
- No CHECK constraint on CONVERSATIONS or MESSAGES: every `CHECK` / `AddCheckConstraint` hit in migrations targets other tables (ORDERS, PROGRAM_*, QUIZ_*, EXERCISE_*, COACHES, SESSION_TEMPLATES, ADMIN_*, MUX_*).
- Latest migration: `20261110104601_Lot12_3a_MuxVideos` (as expected; `ls Migrations | grep -v Designer | sort | tail` → last before the snapshot; 25 migrations + snapshot).

### 2.4 Model snapshot (`MentoraDbContextModelSnapshot.cs`)

- Conversation entity :579-614 (`ToTable("CONVERSATIONS")` :613; index CoachId :607; unique MEMBER_COACH :609-611).
- Message entity :1418-1468 (`MESSAGE_CONTENT` IsRequired text :1430-1433; `MESSAGE_SENDER_TYPE` varchar(10) :1449-1453; index :1463-1465; `ToTable("MESSAGES")` :1467).
- Relations: Conversation :3405-3422 (Coach Restrict, Member Cascade); Message :3589-3598 (Cascade); navigation Messages :3988-3991.

---

## 3. DTOs and construction sites

### 3.1 DTOs (field order)

| DTO | File:line | Fields in order |
|---|---|---|
| MessageDto | `Mentora.Core/DTOs/Conversation/MessageDto.cs:5-14` | MessageId, ConversationId, Content, SenderType (MessageSenderType), SenderId, IsRead, SentDate, ReadDate (DateTime?) |
| LastMessageDto | `Mentora.Core/DTOs/Conversation/ConversationDto.cs:28-34` | MessageId, Content, SenderType, SentDate, IsRead |
| ConversationDto | `Mentora.Core/DTOs/Conversation/ConversationDto.cs:18-26` | ConversationId, MemberId, CoachId, VisioUrl (string?), CreatedDate, LastMessageDate (DateTime?), LastMessage (LastMessageDto?) |
| CoachConversationSummaryDto | `Mentora.Core/DTOs/Coach/CoachConversationSummaryDto.cs:13-20` | ConversationId (Guid?), MemberId, MemberFirstName, MemberLastName, UnreadCount (int), LastMessage (LastMessageDto?), LastMessageDate (DateTime?) |
| MessageListResponseDto | `Mentora.Core/DTOs/Conversation/MessageListResponseDto.cs:3-6` | Messages (IReadOnlyList&lt;MessageDto&gt;), NextCursor (DateTime?) |
| SendMessageResponseDto | `Mentora.Core/DTOs/Conversation/SendMessageResponseDto.cs:3` | Message (MessageDto) |
| MarkAsReadResponseDto | `Mentora.Core/DTOs/Conversation/MarkAsReadResponseDto.cs:3` | MarkedCount (int) |
| SendMessageRequestDto | `Mentora.Core/DTOs/Conversation/SendMessageRequestDto.cs:3` | Content (string) |

All are positional `sealed record`s except CoachConversationSummaryDto (`record`, not sealed). LastMessageDto has no ConversationId, SenderId or ReadDate.

### 3.2 Construction sites

Command: `grep -rn "new (MessageDto|LastMessageDto|SendMessageResponseDto|MessageListResponseDto|ConversationDto|CoachConversationSummaryDto)\("` over the whole repo → 11 hits, all below.

| Site | DTO | SQL or memory |
|---|---|---|
| `Mentora.Infrastructure/Services/ConversationService.cs:63-71` (GetMessagesAsync) | MessageDto | inside `.Select` before `ToListAsync`: translated to SQL (record constructor projection) |
| `ConversationService.cs:114-122` (SendMessageAsync) | MessageDto | in memory from the tracked entity |
| `ConversationService.cs:199-204` (GetInboxForCoachAsync) | LastMessageDto | in memory, after `ToListAsync` (:190); SQL projects an anonymous type (:176-183) |
| `ConversationService.cs:277-282` (GetOrCreateInternalAsync) | LastMessageDto | in memory; SQL projects an anonymous type (:263-270) |
| `ConversationService.cs:323-328` (race path) | LastMessageDto | in memory; SQL anonymous type (:311-318) |
| `ConversationService.cs:193` | CoachConversationSummaryDto | in memory |
| `ConversationService.cs:337` (Map) | ConversationDto | in memory |
| `ConversationService.cs:53`, `:81` | MessageListResponseDto | in memory |
| `Mentora.API/Controllers/Member/MemberConversationMessagesController.cs:55` | SendMessageResponseDto | in memory |
| `Mentora.API/Controllers/Coach/CoachConversationMessagesController.cs:55` | SendMessageResponseDto | in memory |

The "last message" is selected by three separate anonymous-type projections (:176-183, :263-270, :311-318) with the same five columns.

### 3.3 Names versus the contract

- Classes in the code: `MessageDto`, `LastMessageDto`.
- Names in the contract: `ConversationMessageDto`, `ConversationLastMessageDto`.
- `grep -rn "ConversationMessageDto|ConversationLastMessageDto"` → 0. Swagger uses `CustomSchemaIds(t => t.FullName)` (`Mentora.API/Program.cs:146`), so the schema ids are `Mentora.Core.DTOs.Conversation.MessageDto` and `Mentora.Core.DTOs.Conversation.LastMessageDto`.

---

## 4. REST endpoints

### 4.1 Routes touching conversations or messages

| Verb | Full route | Controller file:line | Policy | Swagger group | Declared | Actual return | Identity resolution |
|---|---|---|---|---|---|---|---|
| GET | `/api/v1/member/coaches/{coachId:guid}/conversation` | `Mentora.API/Controllers/Member/MemberConversationController.cs:9,24-35` | MemberOnly (:10) | mobile (:11) | 200 ConversationDto, 403, 404 | `Ok(ConversationDto)` (:34) | `memberId` claim (:32), coachId route |
| GET | `/api/v1/member/coaches/{coachId:guid}/conversation/messages?before&limit` | `Mentora.API/Controllers/Member/MemberConversationMessagesController.cs:10,23-37` | MemberOnly (:11) | mobile (:12) | 200 MessageListResponseDto, 403, 404 | `Ok(...)` (:36) | `memberId` claim (:33); limit clamped 1..100, default 50 (:34) |
| POST | `/api/v1/member/coaches/{coachId:guid}/conversation/messages` | same file :42-56 | MemberOnly | mobile | 201 SendMessageResponseDto, 400, 403, 404 | `CreatedAtAction(nameof(List), new { coachId }, SendMessageResponseDto)` (:55) | `memberId` claim (:52), senderId = memberId (:54) |
| PATCH | `/api/v1/member/coaches/{coachId:guid}/conversation/messages/read` | same file :62-73 | MemberOnly | mobile | 200 MarkAsReadResponseDto, 403, 404 | `Ok(...)` (:72) | `memberId` claim (:70) |
| GET | `/api/v1/coach/members/{memberId:guid}/conversation` | `Mentora.API/Controllers/Coach/CoachConversationController.cs:9,22-33` | CoachOnly (:10) | coach (:11) | 200 ConversationDto, 403, 404 | `Ok(...)` (:32) | `coachId` claim (:30) |
| GET | `/api/v1/coach/members/{memberId:guid}/conversation/messages?before&limit` | `Mentora.API/Controllers/Coach/CoachConversationMessagesController.cs:10,23-37` | CoachOnly (:11) | coach (:12) | 200, 403, 404 | `Ok(...)` (:36) | `coachId` claim (:33) |
| POST | `/api/v1/coach/members/{memberId:guid}/conversation/messages` | same file :42-56 | CoachOnly | coach | 201, 400, 403, 404 | `CreatedAtAction(nameof(List), new { memberId }, ...)` (:55) | `coachId` claim (:52), senderId = coachId (:54) |
| PATCH | `/api/v1/coach/members/{memberId:guid}/conversation/messages/read` | same file :62-73 | CoachOnly | coach | 200, 403, 404 | `Ok(...)` (:72) | `coachId` claim (:70) |
| GET | `/api/v1/coach/conversations` | `Mentora.API/Controllers/Coach/CoachConversationsController.cs:9,20-28` | CoachOnly (:10) | coach (:11) | 200 List&lt;CoachConversationSummaryDto&gt;, 403 | `Ok(...)` (:27) | `coachId` claim (:25) |

Tags: "Member — Conversation" / "Coach — Conversation". The routes declare 403 but the service never throws ForbiddenException (404 only, see 5). 422 (validation) is never declared on the POST routes. A `coachId` claim missing on a CoachOnly token would throw a NullReferenceException at `User.FindFirst("coachId")!` → 500 (no guard).

### 4.2 Response envelope

- There is NO global envelope mechanism. Proof: `grep -E "IResultFilter|IAsyncResultFilter|ResultFilterAttribute|IAlwaysRunResultFilter|OutputFormatter"` over Mentora.API, Core and Infrastructure → 0. The only global MVC filter is `NullBodyGuardFilter` (`Mentora.API/Program.cs:421`, an `IActionFilter`).
- The envelope `{ success, data, error, statusCode }` is built inline by each action that wants it, e.g. `Mentora.API/Controllers/Coach/CoachMeController.cs:30`, `CoachExercisesController.cs:94` (201 with `statusCode = 201` inside), `CoachGroupSessionsController.cs:79`, `AccountController.cs:31`.
- The 5 conversation controllers do NOT use it: they return bare DTOs (`Ok(result)`, `CreatedAtAction(..., new SendMessageResponseDto(message))`). The REST chat responses today are therefore NOT enveloped. A 201 from the chat POST is the bare `SendMessageResponseDto` with a `Location` header pointing at the List route.
- Errors are enveloped by `GlobalExceptionMiddleware` (`Mentora.API/Middleware/GlobalExceptionMiddleware.cs:80-106`), only for thrown exceptions.
- Binary responses: since there is no result filter, a `FileResult` / `PhysicalFileResult` / `FileStreamResult` returned by an action would go out untouched (not wrapped, not broken). Only an exception thrown before the response starts is turned into a JSON envelope; an exception thrown after the body has started streaming cannot be rewritten (the middleware sets StatusCode and ContentType on a started response: not handled, to verify at implementation time). No `File(`, `PhysicalFile`, `FileStreamResult`, `FileContentResult` exists today (count 0, see 9).
- `[ApiController]` client-error mapping is active (`SuppressMapClientErrors` count 0): a bare `StatusCode(4xx)` / `NotFound()` without body becomes a ProblemDetails body. This is already worked around in `Mentora.API/Controllers/Internal/MuxWebhookController.cs:56-58` (`EmptyResult` instead of `StatusCode(401)`).

### 4.3 Route collision check

`grep -iE '\[Route\(".*conversation|Http(Get|Post|Put|Patch|Delete)\(".*conversation'` → 5 class-level routes, listed in 4.1.
- Under `/api/v1/member/coaches/{coachId}/conversation`: `""` (GET), `/messages` (GET, POST), `/messages/read` (PATCH). No `/messages/audio`, no `/messages/{id}`.
- Under `/api/v1/coach/members/{memberId}/conversation`: same set. No `/messages/audio`, no `/messages/{id}`.
- Routes starting with `/api/v1/conversations`: 0 (`grep 'api/v1/conversations'` → 0). Only `/api/v1/coach/conversations` (inbox) exists.
- Minimal API mappings: `/api/v1/health` (`Program.cs:568`) and the hub `/hubs/chat` (`Program.cs:586`).

---

## 5. ConversationService (`Mentora.Infrastructure/Services/ConversationService.cs`)

- Constructor (:14-17): `MentoraDbContext db`, `IValidator<SendMessageRequestDto> sendMessageValidator`, `IVisioUrlGenerator visioUrlGenerator`. Interface: `Mentora.Core/Interfaces/IConversationService.cs:7-62`.
- `ValidateAsync` (:214-222): `MemberCoaches.AnyAsync(memberId && coachId)`; the MEMBER_COACHES link is the existence check; throws `NotFoundException("Conversation not found.")` → 404. `MemberIsActive` / `CoachIsActive` are not checked.
- `GetOrCreateForMemberAsync` (:19-29): same link check, 404 `"Coach {coachId} not found."`. `GetOrCreateForCoachAsync` (:31-41): 404 `"Member {memberId} not found."`. Both → `GetOrCreateInternalAsync` (:254-333).
- `GetMessagesAsync` (:43-82): ValidateAsync, then conversation lookup; no conversation → empty list, no creation (:52-53). Cursor = `MessageSentDate < before` (:57-58), ordered by `MessageSentDate DESC`, `Take(limit + 1)` (:60-62); `NextCursor` = SentDate of the last kept item (:74-79). Cursor is a timestamp, not a (date, id) pair.
- `SendMessageAsync` (:84-123):
  1. FluentValidation `ValidateAndThrowAsync(new SendMessageRequestDto(content))` (:87), before the link check.
  2. `ValidateAsync` (:88).
  3. `GetOrCreateConversationEntityAsync` (:90), BEFORE the transaction opens.
  4. `BeginTransactionAsync` (:92), insert Message with `content.Trim()` (:94-106), `ConversationLastMessageDate = message.MessageSentDate` (:108), `SaveChangesAsync` + `CommitAsync` (:111-112).
  5. Returns MessageDto built in memory (:114-122). `MessageSentDate = DateTime.UtcNow` set by the app (:102), not by the DB default.
- Lazy creation and race (23505): `GetOrCreateConversationEntityAsync` (:224-252) and `GetOrCreateInternalAsync` (:286-330) each insert and catch `DbUpdateException` with `PostgresException.SqlState == "23505"`, clear the tracker and re-read.
- `MarkMessagesAsReadAsync` (:125-151): ValidateAsync, no conversation → 0, then `ExecuteUpdateAsync` on messages of the OTHER side with `!MessageIsRead`, sets `MessageIsRead = true`, `MessageReadDate = now` (:140-148). Returns the count.
- `GetInboxForCoachAsync` (:153-210): one SQL query (`ToListAsync` :190) from MEMBER_COACHES LEFT JOIN CONVERSATIONS, last message and `UnreadCount` (member-sent and unread, :186-187) as correlated subqueries; sorting by LastMessageDate done in memory (:208).
- DI: `AddScoped<IConversationService, ConversationService>()` (`Mentora.API/Program.cs:329`). Validators: `AddValidatorsFromAssemblyContaining<ProductRequestValidator>()` (`Program.cs:290`), which scans Mentora.Core and registers `SendMessageRequestValidator` (default lifetime of the FluentValidation extension: scoped, documented default).

---

## 6. SignalR

### 6.1 ChatHub (`Mentora.API/Hubs/ChatHub.cs`)

- `[Authorize]` without policy or scheme (:16). Depends on `IConversationService` and `MentoraDbContext` (:22-26).
- Group name: `conversation:{conversationId}` (:84).
- `JoinConversationAsync(Guid conversationId)` (:31-35): access check, then `Groups.AddToGroupAsync`. Requires the CONVERSATIONS row to exist (:93-94): a conversation never opened through GET cannot be joined.
- `LeaveConversationAsync(Guid)` (:40-43): no access check.
- `SendMessageAsync(Guid conversationId, string content)` (:49-60): text only; calls `IConversationService.SendMessageAsync`, then `Clients.Group(...).SendAsync("ReceiveMessage", message)` (:58-59). Payload: one argument, the `MessageDto` (sender included in the group).
- `MarkAsReadAsync(Guid conversationId)` (:66-80): calls `MarkMessagesAsReadAsync`; if count > 0, `SendAsync("MessagesRead", markedCount, DateTime.UtcNow)` (:77-78). Payload: two positional arguments (int, DateTime). The DateTime is a new `UtcNow`, not the `now` written in the DB (`ConversationService.cs:140`).
- `HubException`: "Conversation not found." (:94), "You don't have access to this conversation." (:108), "Unable to resolve caller role for this conversation." (:139). A `NotFoundException` or FluentValidation `ValidationException` thrown by the service inside the hub is NOT a HubException: the client receives the generic SignalR error (detailed errors not enabled: `grep "HubOptions|MaximumReceiveMessageSize|EnableDetailedErrors"` → 0).
- Access check (:86-109): `memberId` claim matching, else `coachId` claim matching. No `userType` check: a `BOTH` token can use the hub on the member side, while REST member routes refuse it (MemberOnly requires `userType = MEMBER`, `Program.cs:247-249`).

### 6.2 Authentication of the hub

- Default scheme: `AddAuthentication(JwtBearerDefaults.AuthenticationScheme)` (`Program.cs:168`), so `[Authorize]` on the hub resolves to the `Bearer` scheme.
- `JwtBearerEvents.OnMessageReceived` (`Program.cs:192-204`): token read from `access_token` query string only when the path starts with `/hubs/chat` (:199).
- Admin token: the `AdminBearer` scheme (`Program.cs:208-211`) is never the default, the hub does not name it (`grep -c AuthenticationSchemes ChatHub.cs` → 0), and the startup refuses an admin secret or audience equal to the app ones (`Program.cs:515-518`). An admin token therefore fails Bearer validation: it cannot connect.

### 6.3 Serialization

- `AddSignalR().AddJsonProtocol(...)` (`Program.cs:377-382`): only adds `JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper)`; `PropertyNamingPolicy` is not set (the 4 `PropertyNamingPolicy` hits are in seeders/services, none in Program.cs).
- Documented default of the SignalR JSON hub protocol (`JsonHubProtocolOptions.PayloadSerializerOptions`): camelCase property names. Conclusion: hub payloads are camelCase (`messageId`, `content`, `senderType`...), the same as REST, NOT PascalCase. Runtime confirmation: to verify (no trace in the repo; `scripts/dev/test-signalr.html:86` only logs the raw object).
- Enums: `SenderType` → `"MEMBER"` / `"COACH"` on both REST (`Program.cs:432-433`) and SignalR (`Program.cs:380-381`).
- MessagePack: not referenced (count 0).

### 6.4 IHubContext and real-time broadcast of the REST send

- `grep -rn "IHubContext"` over all files (bin/obj excluded) → **0**.
- Conclusion: a message sent through the REST POST (4.1) is NOT broadcast in real time today; only `ChatHub.SendMessageAsync` broadcasts `ReceiveMessage`. Same for the REST PATCH read: no `MessagesRead` event.

### 6.5 nginx `/hubs/`

- `nginx/conf.d/mentora.conf:58-72`: `proxy_pass http://api:8080`, HTTP/1.1, Upgrade/Connection via the map (:5-9), `proxy_buffering off`, `proxy_read_timeout 3600s`, `proxy_send_timeout 3600s`.
- `nginx/conf.d/mentora-prod.conf.example:53-67`: identical, `proxy_pass http://api-prod:8080`.

---

## 7. Validation and errors

- `Mentora.Core/Validators/Conversation/SendMessageRequestValidator.cs:10-13`: `Content` NotNull ("Content is required."), not empty / whitespace ("Content cannot be empty or whitespace only."), `Trim().Length <= 6500`. No cascade mode set anywhere (`grep "CascadeMode|ValidatorOptions.Global"` → 0): with a null Content the third rule (`c.Trim()`) would also run (FluentValidation default cascade Continue): possible NullReferenceException → 500 (to verify; the REST path binds a JSON body).
- `GlobalExceptionMiddleware` (`Mentora.API/Middleware/GlobalExceptionMiddleware.cs`): NotFound 404 (:14-18), Unauthorized 401 (:19-23), Forbidden 403 (:24-28), Conflict 409 + details (:29-33), ValidationException 422 (:34-56), MuxUnavailable 502 (:57-62), MuxNotConfigured 503 (:63-67), InvalidOperation 400 (:68-72), other 500 (:73-77). Registered `Program.cs:549`.
- Exact 422 body (:48-55): `{ "success": false, "data": null, "error": "Validation failed.", "statusCode": 422, "errors": { "<PropertyName>": ["<message>", ...] } }`, PropertyName in PascalCase (e.g. `Content`).
- `SuppressModelStateInvalidFilter = true` (`Program.cs:423-426`). Effects today:
  - Missing / malformed JSON body on a `[FromBody]` parameter → `NullBodyGuardFilter` (`Mentora.API/Middleware/NullBodyGuardFilter.cs:15-31`) throws InvalidOperationException → 400 enveloped "Request body is required.". The filter only checks `BindingSource.Id == "Body"` (:22): a `[FromForm]` parameter is NOT covered.
  - Body present but `content` missing → `Content = null` → validator 422 (or 500, see above).
  - Non-numeric query value (`?limit=abc`): binding fails, ModelState invalid but ignored, action runs with `null` → default 50.
  - Non-GUID route segment: route constraint `:guid` does not match → 404 from routing (no envelope).
- `[FromForm]`, `IFormFile`, `IFormFileCollection`, `[Consumes]`: `grep` over all files → 0, 0, 0, 0. Also `IBrowserFile`, `InputFile` (admin) → 0. No upload exists in the repo.

---

## 8. Request size and timeouts

API:
- Kestrel: `grep "ConfigureKestrel|UseKestrel|KestrelServerOptions|MaxRequestBodySize"` → 0. Kestrel default applies (30,000,000 bytes, documented default).
- `FormOptions` / `MultipartBodyLengthLimit` → 0. Default applies (128 MB multipart body, documented default).
- `[RequestSizeLimit]`: 1 site, `Mentora.API/Controllers/Internal/MuxWebhookController.cs:28` (`1_048_576`), with 413 handling :46-49. `[DisableRequestSizeLimit]` → 0.

nginx:
| Directive | `nginx/nginx.conf` | `nginx/conf.d/mentora.conf` | `nginx/conf.d/mentora-prod.conf.example` |
|---|---|---|---|
| client_max_body_size | not set | `25M` server-level (:40) | `25M` server-level (:36) |
| proxy_read_timeout | not set | `3600s` in `/hubs/` only (:70); not set in `/` | `3600s` in `/hubs/` only (:65); not set in `/` |
| proxy_send_timeout | not set | `3600s` in `/hubs/` only (:71) | `3600s` in `/hubs/` only (:66) |
| proxy_request_buffering | not set | not set (nginx default on) | not set |
| client_body_timeout | not set | not set (nginx default 60s) | not set |

`nginx/nginx.conf` only sets `sendfile on` and `keepalive_timeout 65` (:13-14). REST routes (`location /`) therefore use the nginx default proxy timeouts (60s).

---

## 9. File storage and container

- Runtime file writes: `File.Write*|File.Create|File.Open|File.Copy|File.Move|File.Delete|File.Append` → 0; `FileStream` → 0; `Directory.Create` → 0; `PhysicalFile|FileStreamResult|FileContentResult|File(` → 0; `UseStaticFiles|UseFileServer` → 0; `ContentRootPath|WebRootPath` → 0; `Path.GetTempPath|GetTempFileName` → 0. Only file read: `File.Exists` of the Swagger XML files (`Program.cs:141`). The API writes nothing to disk today. Media are stored at MUX (lot 12), not locally.
- `Mentora.API/Dockerfile`:
  - Runtime base `mcr.microsoft.com/dotnet/aspnet:8.0` (:1), build `mcr.microsoft.com/dotnet/sdk:8.0` (:5).
  - `WORKDIR /app` (:2, :22), `EXPOSE 8080` (:3), `ENTRYPOINT ["dotnet", "Mentora.API.dll"]` (:24).
  - No `USER` directive. Certain: the .NET 8 images define a non-root user `app` (UID 1654, `APP_UID`) but do not switch to it, so without `USER` the process runs as **root**. Writable directories: everything as root, including `/app`; nothing is a volume in the image (to verify on the VPS compose).
  - No `apt-get` / `RUN` in the final stage.
- `.dockerignore` (:12-44): excludes `**/appsettings.Development.json`, `**/bin`, `**/obj`, `**/publish`, `Mentora.Admin`, `.git`, `.vs`, `.vscode`, `docs`, `docs/review`, `*.tar`, `**/node_modules`. Comment :8-10 states the api-demo container runs `ASPNETCORE_ENVIRONMENT=Development`.
- Compose files in the repo: only `docker-compose.local.yml` (PostgreSQL 16, port 5433, named volume `mentora_local_data:/var/lib/postgresql/data`). The VPS `docker-compose.yml` is not in the repo.
- `docs/review/lot12/12.5a/prod-runbook.md`:
  - Containers / services (:9): `mentora-postgres`, `mentora-api` (service `api`, api-demo), `mentora-api-prod` (service `api-prod`), `mentora-nginx`, `mentora-livekit`; network `mentora-network`.
  - `api-prod` block (:102-123): `image: mentora-api:lot12`, `restart: unless-stopped`, no `ports:`, **no `volumes:`**, environment variable names: `ASPNETCORE_ENVIRONMENT`, `ConnectionStrings__DefaultConnection`, `Jwt__Secret`, `AdminAuth__JwtSecret`, `AdminAuth__Emails`, `Cors__AllowedOrigins`, `Email__Smtp__Password`, `EmailRouting__TestRecipients`, `LiveKit__ApiKey`, `LiveKit__ApiSecret`; later `Mux__TokenId`, `Mux__TokenSecret`, `Mux__WebhookSecret`, `Mux__CorsOrigin` (:494-501, same for `api` with `_DEMO` values :503).
  - `api` (api-demo): its compose block is not reproduced in the runbook; no volume is mentioned for it. The only volumes mentioned are on nginx (`./admin-www:/var/www/mentora-admin:ro`, certbot, `./nginx/conf.d`) (:346, :363, :397).
  - Backups: database only, `pg_dump -Fc` from `mentora-postgres` to `~/backup-*.dump` (:25-30), restore with `pg_restore` (:289). No file backup (consistent with no file storage today).

---

## 10. Audio decoding readiness

PackageReferences:
| Project | Package | Version |
|---|---|---|
| Mentora.API | FluentValidation.DependencyInjectionExtensions | 12.1.1 |
| Mentora.API | Microsoft.AspNetCore.Authentication.JwtBearer | 8.0.31 |
| Mentora.API | Microsoft.IdentityModel.Protocols.OpenIdConnect | 8.22.0 |
| Mentora.API | Microsoft.EntityFrameworkCore.Design | 8.0.0 |
| Mentora.API | Swashbuckle.AspNetCore | 6.5.0 |
| Mentora.Core | FluentValidation | 12.1.1 |
| Mentora.Infrastructure | BCrypt.Net-Next | 4.0.3 |
| Mentora.Infrastructure | CsvHelper | 33.1.0 |
| Mentora.Infrastructure | MailKit | 4.17.0 |
| Mentora.Infrastructure | Microsoft.EntityFrameworkCore | 8.0.0 |
| Mentora.Infrastructure | Npgsql.EntityFrameworkCore.PostgreSQL | 8.0.0 |
| Mentora.Infrastructure | System.IdentityModel.Tokens.Jwt | 8.22.0 |
| Mentora.Admin | Microsoft.AspNetCore.Components.WebAssembly | 8.0.33 |
| Mentora.Admin | Microsoft.AspNetCore.Components.WebAssembly.DevServer | 8.0.33 |

(Mentora.Infrastructure also has `FrameworkReference Microsoft.AspNetCore.App`.)

- No audio library (NAudio, FFMpegCore, NLayer, Concentus, etc.): none in the list. `grep -iE "audio|voice|waveform|MediaUrl|MessageType"` over .cs files → 0.
- The Dockerfile installs no OS package (no `apt-get`, no `RUN` in the final stage): no ffmpeg in the image.
- Linux distribution of `mcr.microsoft.com/dotnet/aspnet:8.0`: Debian 12 (bookworm) for the default `8.0` tag (official .NET 8 image tagging). Exact digest pulled on the VPS: to verify.

---

## 11. Read status

- Storage: `MESSAGE_IS_READ` / `MESSAGE_READ_DATE` (2.2).
- REST: PATCH `.../conversation/messages/read` member (`MemberConversationMessagesController.cs:62-73`) and coach (`CoachConversationMessagesController.cs:62-73`) → `MarkMessagesAsReadAsync` (`ConversationService.cs:125-151`): marks ALL unread messages of the other side at once; no per-message read.
- Hub: `MarkAsReadAsync` (`ChatHub.cs:66-80`) → event `MessagesRead(int count, DateTime)` to the group. The REST PATCH sends no event (6.4).
- DTOs: `MessageDto.IsRead` + `ReadDate` (`MessageDto.cs:11,13`); `LastMessageDto.IsRead` (`ConversationDto.cs:33`), no ReadDate.
- Coach inbox: `UnreadCount` = member-sent unread (`ConversationService.cs:186-187`, `CoachConversationSummaryDto.cs:18`).
- Member side: no unread count. `grep "UnreadCount|Unread"` → 6 hits, all in ConversationService.cs and CoachConversationSummaryDto.cs. The member only has `LastMessage.IsRead` in ConversationDto; there is no member inbox route.
- "Listened" status distinct from "read": does not exist. `grep -iE "listen(ed)?(Date|At)|IsListened|IsPlayed|Played(Date|At)|IsHeard"` → 2 false positives (the word "displayed" in `LastPerformedResponse.cs:5` and `AgendaService.cs:220`).

---

## 12. Related flows

- Account deletion: `Mentora.API/Controllers/Auth/AccountController.cs:26-53` (POST / DELETE / GET `api/v1/account/deletion-request`), `AccountService.cs:14-49`: only records `UserDeletionRequestedDate` / `UserDeletionReason` (`Mentora.Core/Entities/User.cs:14-15`). Comment :12-13: "no data is deleted synchronously". No code deletes users, members, coaches, member-coach links, conversations or messages: `grep "(Users|Members|Coaches|MemberCoaches|Conversations|Messages)\.(Remove|RemoveRange)|ExecuteDelete"` → 3 hits, all on tokens/devices (`AuthService.cs:237`, `RefreshTokenPurgeService.cs:91`, `UserDeviceService.cs:73`).
- Deactivation flags: `Member.MemberIsActive` (`Mentora.Core/Entities/Member.cs:12`), `Coach.CoachIsActive` (`Mentora.Core/Entities/Coach.cs:10`); not read by ConversationService or ChatHub.
- DB cascade (if a row were deleted manually): MEMBERS → CONVERSATIONS cascade → MESSAGES cascade; COACHES → CONVERSATIONS restrict (2.2). `db.Conversations` / `db.Messages` are only used in `ConversationService.cs` and `ChatHub.cs`.
- Rate limiting: `AddRateLimiter` (`Program.cs:370-374`), policies `QuizBasic` (`Mentora.API/Authorization/QuizRateLimiting.cs:11,19-34`, anonymous only) and `AdminAuth` (`AdminAuthRateLimiting.cs:16-26`, `AdminAuthDefaults.cs:13`). Applied only on `QuizBasicController.cs:18` and `AdminAuthController.cs:18`. No `GlobalLimiter` (0). No rate limit on conversation routes or on the hub.
- Tests: the solution has 4 projects (`Mentora.sln:6-12`: API, Core, Infrastructure, Admin), no test project (`grep xunit|nunit|mstest|Microsoft.NET.Test.Sdk` in csproj → 0). No automated test covers conversations. Manual aids: `scripts/dev/test-signalr.html` (signalr 8.0.0 client, `ReceiveMessage` :86, `MessagesRead` :87) and `scripts/dev/README.md`.

---

## 13. Swagger

- `AddSwaggerGen` (`Program.cs:39-154`): 4 documents `mobile` (:41), `coach` (:47), `internal` (:53), admin = `AdminAuthDefaults.SwaggerDoc` (:60). `DocInclusionPredicate` on `GroupName`, default `internal` (:68-72). Tags from `[Tags]` (:75-98). Security schemes Bearer and AdminBearer (:100-128), `SecuritySchemePerDocumentFilter` (:131).
- XML comments: enabled. `GenerateDocumentationFile` true in `Mentora.API.csproj:27` and `Mentora.Core.csproj:7`; included if present (`Program.cs:133-143`).
- `DescribeAllParametersInCamelCase()` (:145), `CustomSchemaIds(t => t.FullName)` (:146).
- Operation filters: `VoucherStatusOperationFilter` (:149), `ExerciseListOperationFilter` (:150), `DeprecatedCoachProgramsRouteOperationFilter` (:153). None deals with files or multipart.
- Multipart today: no operation uses `[FromForm]`/`IFormFile` (0), so no multipart operation appears. Swashbuckle 6.5 documented behaviour: an `IFormFile` bound from form produces a `multipart/form-data` request body with a `binary` field (to verify at implementation time).
- UI endpoints `Program.cs:552-560`.

---

## 14. Contract proposed by the mobile developer versus the code

| Contract item | What the code has today | Gap or conflict |
|---|---|---|
| POST `/api/v1/member/coaches/{coachId}/conversation/messages/audio` | Member POST text only, `MemberConversationMessagesController.cs:42-56`; no `/audio` route (4.3) | New route; no collision (the controller has no `{id}` segment route) |
| POST `/api/v1/coach/members/{memberId}/conversation/messages/audio` | Coach POST text only, `CoachConversationMessagesController.cs:42-56` | New route; no collision |
| multipart/form-data, fields `file`, `durationMs` | No `[FromForm]`, `IFormFile`, `[Consumes]` in the repo (7) | First upload of the API; `NullBodyGuardFilter` ignores form binding (`NullBodyGuardFilter.cs:22`); `SuppressModelStateInvalidFilter` means a non-numeric `durationMs` silently binds to default (7) |
| AAC ADTS `voice-note.aac`, `audio/aac`; also `audio/m4a`, `audio/mp4`, `audio/x-m4a`, `audio/wav` | No audio code, no decoder, no ffmpeg (10) | Formats and content-type check entirely new; the storage suggestion uses `.m4a` while the plugin produces ADTS `.aac` (internal inconsistency of the contract) |
| Response `SendMessageResponseDto` like the text send | `SendMessageResponseDto(MessageDto Message)` (`SendMessageResponseDto.cs:3`), returned bare via `CreatedAtAction` 201 (4.1) | Same type is possible; note the chat responses are not enveloped (4.2) |
| Limits 5 MB, 300 ms to 120 s | Kestrel default ~30 MB, multipart 128 MB, nginx 25M, REST nginx timeouts 60 s default (8); no per-action limit except MUX webhook | No limit configured for the chat; the server cannot measure the duration today (no decoder) |
| Authorization identical to text send (participant only) | MemberOnly / CoachOnly + `ValidateAsync` MEMBER_COACHES link, 404 (5) | Reusable as is; note participant = current MEMBER_COACHES link, not CONVERSATIONS row |
| DTO "ConversationMessageDto" / "ConversationLastMessageDto" | Real names `MessageDto` / `LastMessageDto` (3.3) | Naming mismatch in the contract |
| REST camelCase and SignalR PascalCase | REST camelCase (System.Text.Json web default); SignalR camelCase by documented default, no override (`Program.cs:377-382`) (6.3) | Conflict: SignalR is not PascalCase per the code (runtime check to verify) |
| `Content = ""` for audio | `MESSAGE_CONTENT` NOT NULL text (2.2) accepts `""`; validator refuses empty (`SendMessageRequestValidator.cs:12`) and is called inside `SendMessageAsync` (`ConversationService.cs:87`) | The current service path refuses an audio message with empty content |
| New `MessageType` ("TEXT" / "AUDIO") | No column, no enum, no DTO field (2, 10) | New; enum serialization would follow SnakeCaseUpper (`Program.cs:432-433`, `:380-381`) |
| New `MediaUrl` (null for text) | No field, no storage, no download route (9) | New |
| New `DurationSeconds` (int?) while the request sends `durationMs` | No field | New; unit differs between request (ms) and response (s) |
| Old messages without MessageType (nullable on app side) | All existing rows are text | Default for existing rows to decide |
| LastMessageDto extension | LastMessageDto built from 3 anonymous projections (`ConversationService.cs:176-183, 263-270, 311-318`) | Three sites to align |
| MediaUrl on API origin with Bearer, or signed URL on another origin | No file served by the API, no signed URL mechanism; MUX used for exercise videos only (lot 12) | Open choice; Bearer on GET means the app player must send headers |
| After REST audio send, hub broadcasts `ReceiveMessage` with the full message | `IHubContext` count 0 (6.4); the REST text send does not broadcast today | Conflict with "like for a text send": the text REST send is not broadcast today; only the hub method broadcasts |
| Hub `SendMessageAsync` stays text only | `ChatHub.cs:49-60` text only | Consistent |
| Storage `{conversationId}/{guid}.m4a`, `Cache-Control: private, immutable` | No disk writes, no volume on `api-prod` (runbook :102-123), container runs as root, no Cache-Control code (count 0) (9) | Storage, volume and backups to define; `pg_dump` covers DB only |
| Product decision 17/11: server-computed waveform with a fixed number of bars | No audio decoder in packages or image (10) | Requires decoding capability not present today |

---

## 15. Observations and open questions for the design

1. The REST send is not broadcast: `IHubContext` count 0; only `ChatHub.SendMessageAsync` emits `ReceiveMessage` (`ChatHub.cs:58-59`). The REST PATCH read emits no `MessagesRead` either.
2. DTO names differ from the contract: `MessageDto` (`MessageDto.cs:5`), `LastMessageDto` (`ConversationDto.cs:28`).
3. The validator refuses empty content (`SendMessageRequestValidator.cs:11-13`) and is called unconditionally at the top of `SendMessageAsync` (`ConversationService.cs:87`); also used by the hub path (`ChatHub.cs:55-56`).
4. `MESSAGE_CONTENT` is NOT NULL text (`MessageConfiguration.cs:24-27`, migration :48); no CHECK constraint on MESSAGES.
5. Chat REST responses are bare DTOs, not enveloped (4.2); the envelope is per-action, so a binary result would not be wrapped.
6. No file storage anywhere, no volume on `api-prod`, container runs as root (no `USER`, `Dockerfile:1-24`), backups are `pg_dump` only (runbook :25-30).
7. No audio decoder, no ffmpeg, no audio package (10); Debian 12 base (tag-level).
8. No account deletion executor: only a deletion request (`AccountService.cs:14-49`); media cleanup has no existing hook. Member deletion would cascade rows (`ConversationConfiguration.cs:42`, `:53`), not files.
9. Size limits: none for the chat; Kestrel/multipart defaults; nginx 25M and 60 s default timeouts on REST (`mentora.conf:40`, `:75-85`).
10. Hub casing: camelCase by default (`Program.cs:377-382`), contrary to the contract's "SignalR PascalCase" (runtime check to verify).
11. `NullBodyGuardFilter` covers `[FromBody]` only (`NullBodyGuardFilter.cs:22`); with `SuppressModelStateInvalidFilter` (`Program.cs:425`) form binding errors would not stop the action.
12. Conversation creation in `SendMessageAsync` happens before the transaction (`ConversationService.cs:90` vs `:92`).
13. The hub requires an existing CONVERSATIONS row to join (`ChatHub.cs:93-94`); service exceptions in the hub reach the client as generic errors (6.1).
14. Hub access ignores `userType`: a `BOTH` token works on the hub member side, REST member routes refuse it (`Program.cs:247-249` vs `ChatHub.cs:96-100`).
15. `MessagesRead` carries a fresh `DateTime.UtcNow` (`ChatHub.cs:78`) different from the stored `MessageReadDate` (`ConversationService.cs:140`).
16. No "listened" status; read is "all at once per side" (11). Open: does an audio message need a listened state distinct from read?
17. Member side has no unread count (11).
18. No test project; no automated test covers conversations (12).
19. No rate limiting on conversation routes or the hub (12).
20. Cursor pagination uses `MessageSentDate` alone (`ConversationService.cs:57-58, 78`); messages with equal timestamps at a page boundary are not tie-broken.
21. CLAUDE.md cites `MemberOnly` at `Program.cs:185-187`; the actual lines are `Program.cs:247-249`. CLAUDE.md's exception map omits MuxUnavailable 502 and MuxNotConfigured 503 (`GlobalExceptionMiddleware.cs:57-67`).
22. Contract internal inconsistencies: request `durationMs` versus response `DurationSeconds`; ADTS `.aac` produced versus `.m4a` storage name; several accepted MIME types including `audio/wav`.
23. Open: where the 5 MB / 120 s limits are enforced (nginx, Kestrel, action) and whether the server trusts the client `durationMs` when it cannot decode.
24. Open: how MediaUrl is authorized (API Bearer GET versus signed URL) given no file serving exists.
25. Open: default MessageType for existing rows and how old app versions render an audio row whose `content` is `""`.

---

## Commands used

Git (section 1): `git --no-optional-locks status --short`, `git --no-optional-locks branch --show-current`, `git fetch origin`, `git --no-optional-locks log --oneline -1 origin/main`, `git --no-optional-locks fsck --no-dangling`, `git switch -c feature/lot_13 origin/main`, `git branch --unset-upstream`, `git --no-optional-locks log --oneline -1`.

Reads (Read tool or `cat -n` / `sed -n`): entities Conversation, Message, MessageSenderType; ConversationConfiguration, MessageConfiguration; migrations AddConversationsAndMessages, AddLot5Parameters (:10-20, :314-325); ModelSnapshot ranges; all `Mentora.Core/DTOs/Conversation/*.cs`, `CoachConversationSummaryDto.cs`, `SendMessageRequestValidator.cs`, `IConversationService.cs`; `ConversationService.cs`; the 5 conversation controllers; `ChatHub.cs`; `Program.cs`; `GlobalExceptionMiddleware.cs`; `NullBodyGuardFilter.cs`; `AccountController.cs`; `MuxWebhookController.cs` (:1-60); the 4 csproj; `Mentora.API/Dockerfile`; `.dockerignore`; `docker-compose.local.yml`; `nginx/nginx.conf`, `nginx/conf.d/mentora.conf`, `nginx/conf.d/mentora-prod.conf.example`; `prod-runbook.md` (:94-129, :490-516 and grep).

Searches (Grep tool or `grep -rn --include=*.cs --exclude-dir=bin --exclude-dir=obj`, counts reported above):
- `Conversation|Message(Dto|SenderType|Configuration)|ChatHub` (file list)
- `"(CONVERSATIONS|MESSAGES)"|CONVERSATION_VISIO_URL|AddCheckConstraint|CHECK` and `MESSAGES|MESSAGE_|CONVERSATIONS` (count) in Migrations excluding Designer
- `new (MessageDto|LastMessageDto|SendMessageResponseDto|MessageListResponseDto|ConversationDto|CoachConversationSummaryDto)\(`
- `ConversationMessageDto|ConversationLastMessageDto` (0)
- `IResultFilter|IAsyncResultFilter|ResultFilterAttribute|IAlwaysRunResultFilter|OutputFormatter` (0)
- `statusCode\s*=|success\s*=` (envelope sites)
- `IHubContext`, `\[FromForm`, `IFormFile`, `IFormFileCollection`, `\[Consumes`, `RequestSizeLimit`, `DisableRequestSizeLimit`, `MaxRequestBodySize`, `FormOptions`, `MultipartBodyLengthLimit`, `ConfigureKestrel|UseKestrel|KestrelServerOptions`, `File\.(Write|Create|Open|Copy|Move|Delete|Append)`, `FileStream`, `Directory\.Create`, `PhysicalFile|FileStreamResult|FileContentResult|File\(`, `UseStaticFiles|UseFileServer`, `ContentRootPath|WebRootPath`, `Path\.GetTempPath|GetTempFileName`, `RequireRateLimiting|EnableRateLimiting|DisableRateLimiting`, `GlobalLimiter`
- all file types (bin, obj, .git, review, appsettings*.json, *.log, *.env excluded): `IHubContext`, `IFormFile`, `FromForm`, `Consumes`, `UseStaticFiles`, `IBrowserFile`, `InputFile`
- `CascadeMode|ValidatorOptions\.Global`, `SuppressMapClientErrors`, `PropertyNamingPolicy`, `messagepack`, `HubOptions|MaximumReceiveMessageSize|EnableDetailedErrors`, `Cache-Control|CacheControl|ResponseCache`, `AuthenticationSchemes` (ChatHub)
- `UnreadCount|Unread`, `MessageIsRead|MessageReadDate|IsRead|ReadDate`, `listen(ed)?(Date|At)|IsListened|IsPlayed|Played(Date|At)|IsHeard`, `audio|voice|waveform|MediaUrl|MessageType`
- `UserDeletion|DeletionRe|IsDeleted|DeletedDate|Deactivat|IsActive` (entities), `(Users|Members|Coaches|MemberCoaches|Conversations|Messages)\.(Remove|RemoveRange)|ExecuteDelete`, `db\.(Conversations|Messages)`
- route checks: `\[Route\(".*conversation|Http(Get|Post|Put|Patch|Delete)\(".*conversation`, `api/v1/conversations`, `MapHub|MapGet|MapPost`
- csproj: `PackageReference|TargetFramework|GenerateDocumentationFile|ProjectReference`, `xunit|nunit|mstest|Microsoft.NET.Test.Sdk`
- runbook: `image|volume|pg_dump|backup|sauvegard|environment|api-prod|...`
- `scripts/dev/test-signalr.html`: `ReceiveMessage|MessagesRead|signalr@`

Listings: `ls` of the repo root, `Mentora.API/Middleware`, `Authorization`, `Swagger`, `Startup`, `nginx`, `nginx/conf.d`, `scripts`, `docs/review`, Migrations (sorted, tail).

Sensitive checks on this file (section 16): see the final report.
