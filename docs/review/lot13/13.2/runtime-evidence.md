# Lot 13.2: voice message columns, migration and DTO fields, runtime evidence

- Date: 2026-11-17 / 2026-11-18 (UTC run start `2026-11-17 23:27:11.769391+00`)
- Branch: `feature/lot_13`, base commit `bbafcc5` (13.1 evidence)
- Design source: decisions D1 to D9 of the 13.2 brief (13.G, 13.H), inventory `docs/review/lot13/13.0/inventory.md`.
- Scope: data model and response fields only. No upload route, no listening route, no ffmpeg, no file storage, no hub change.
- Accounts: "local coach A" and "local member" (local seed accounts), linked by MEMBER_COACHES, conversation `58d5c27c-d823-48a8-bc68-59f053b02252`.

## 1. Decisions as implemented

| Decision | Implementation |
|---|---|
| D1 enum `MessageType { Text, Audio }`, wire "TEXT" / "AUDIO" through the existing SnakeCaseUpper converter, `EnumMappings.MessageTypeMapping` | `Mentora.Core/Enums/MessageType.cs:7`; `Mentora.Core/Enums/EnumMappings.cs:549` (WireValues, Parse, ToWire, if chains). No converter added: the wire values are proven by P2, P3 and P6. |
| D2 entity properties | `Mentora.Core/Entities/Message.cs:17` (`MessageType = MessageType.Text`), `:20` path, `:23` content type, `:26` duration ms, `:29` size bytes, `:32` `List<int>? MessageAudioWaveform` |
| D3 columns, converter, sentinel | `Mentora.Infrastructure/Persistence/Configurations/MessageConfiguration.cs:64-72` (`MESSAGE_TYPE` varchar(10) NOT NULL, `HasDefaultValueSql("'TEXT'")`, converter, `HasSentinel((MessageType)(-1))`), `:78`, `:82`, `:86`, `:89`, `:93` (audio columns, waveform `jsonb`); helpers `MessageTypeToDb` `:107` and `MessageTypeFromDb` `:114` (if chains, `InvalidOperationException` on an unknown value). No new index. |
| D4 named CHECK constraints | migration `:63` (`CK_MESSAGES_TYPE`), `:71` (`CK_MESSAGES_AUDIO_FIELDS`), dropped in Down `:113-114`. Amended during the run, see section 5. |
| D5 migration `Lot13_2_VoiceMessages`, guarded Down | `Mentora.Infrastructure/Persistence/Migrations/20261117234001_Lot13_2_VoiceMessages.cs`; Down guard `:100-111` (`RAISE EXCEPTION 'Lot13_2 Down refused: audio messages exist.'` at `:108`), then the 2 CHECK drops, then the 6 columns `:116-139` |
| D6 `VoiceMessageRules` | `Mentora.Core/Messaging/VoiceMessageRules.cs`: constants `:10-28`, `MediaUrl` `:34` (relative path, GUID "D" format, lowercase), `DurationSeconds` `:40` (`Math.Max(1, round half away from zero)`) |
| D7 DTO fields appended at the end | `Mentora.Core/DTOs/Conversation/MessageDto.cs:19-32` (`MessageType`, `MediaUrl`, `DurationSeconds`, `Waveform` after `ReadDate`); `Mentora.Core/DTOs/Conversation/ConversationDto.cs:38-47` (`MessageType`, `MediaUrl`, `DurationSeconds` after `IsRead`). Existing parameters unchanged. XML `param` tags added on both records. |
| D8 service | `Mentora.Infrastructure/Services/ConversationService.cs`: list projection `:63` then in memory mapping `:83`; `SendMessageAsync` sets `MessageType = MessageType.Text` `:128` and returns `ToMessageDto` `:139`; inbox `:221` then `ToLastMessageDto` `:230`; conversation read `:314`; race path `:366`; helpers `ToMessageDto` `:395`, `ToLastMessageDto` `:416`. MediaUrl and DurationSeconds are computed in memory, never stored. Each read is one SQL query (P4). |
| D9 unchanged | `SendMessageRequestValidator`, routes, status codes, `ChatHub.cs`, the bare (non enveloped) chat REST responses: not touched. |

## 2. Files changed

- `Mentora.Core/Entities/Message.cs`
- `Mentora.Core/Enums/MessageType.cs` (new)
- `Mentora.Core/Enums/EnumMappings.cs` (new `MessageTypeMapping` only)
- `Mentora.Core/Messaging/VoiceMessageRules.cs` (new)
- `Mentora.Core/DTOs/Conversation/MessageDto.cs`
- `Mentora.Core/DTOs/Conversation/ConversationDto.cs` (LastMessageDto only)
- `Mentora.Infrastructure/Persistence/Configurations/MessageConfiguration.cs`
- `Mentora.Infrastructure/Services/ConversationService.cs`
- `Mentora.Infrastructure/Persistence/Migrations/20261117234001_Lot13_2_VoiceMessages.cs` and `.Designer.cs` (new), `MentoraDbContextModelSnapshot.cs`
- `docs/review/lot13/13.2/runtime-evidence.md` (this file)
- Not changed: the conversation controllers (their XML comments do not describe the response fields), `ChatHub.cs`, validators, `Program.cs`, Dockerfile, nginx, `appsettings*.json`, `.dockerignore`, NuGet packages.

## 3. Build and migration commands

- `dotnet build --no-incremental` (after the code, after `migrations add`, after the CHECK fix): 0 errors, 19 warnings each time, all `CS1573` in `ProgramSessionResponse.cs`, `LastPerformedResponse.cs`, `ProgramExerciseActualRequest.cs` and the two `*ConversationMessagesController.cs` files. Same count as 13.1: no new warning.
- `dotnet ef migrations add Lot13_2_VoiceMessages --project Mentora.Infrastructure --startup-project Mentora.API`: generated. The EF model validation warnings 20601 (database default without sentinel) all name other entities (CoachParameter, ExerciseVideo policy, Order, OrderItem, Product, ProductPack, Program, ProgramSession, Session, SessionParticipant, SessionVoucher, StripeWebhookEvent); none names `Message.MessageType`, so the sentinel is taken into account.
- `dotnet ef migrations has-pending-model-changes ...`: `No changes have been made to the model since the last migration.` (before and after the CHECK fix).

## 4. Migration Up and Down

Up (order verified in the file and in the SQL log of the first application):
1. AddColumn `MESSAGE_AUDIO_CONTENT_TYPE` varchar(20) null, `MESSAGE_AUDIO_DURATION_MS` integer null, `MESSAGE_AUDIO_PATH` varchar(200) null, `MESSAGE_AUDIO_SIZE_BYTES` integer null, `MESSAGE_AUDIO_WAVEFORM` jsonb null.
2. AddColumn `MESSAGE_TYPE` varchar(10) NOT NULL DEFAULT 'TEXT' (SQL log: `ALTER TABLE "MESSAGES" ADD "MESSAGE_TYPE" character varying(10) NOT NULL DEFAULT ('TEXT');`), which fills the existing rows.
3. Then `CK_MESSAGES_TYPE`, then `CK_MESSAGES_AUDIO_FIELDS` (raw SQL).

Down: guard (`DO $$ ... RAISE EXCEPTION 'Lot13_2 Down refused: audio messages exist.' ... $$`), drop `CK_MESSAGES_AUDIO_FIELDS`, drop `CK_MESSAGES_TYPE`, drop the 6 columns.

Final definitions (`pg_get_constraintdef`, after the fix of section 5):

```
CK_MESSAGES_TYPE
CHECK ((("MESSAGE_TYPE")::text = ANY ((ARRAY['TEXT'::character varying, 'AUDIO'::character varying])::text[])))

CK_MESSAGES_AUDIO_FIELDS
CHECK ((((("MESSAGE_TYPE")::text = 'TEXT'::text) AND ("MESSAGE_AUDIO_PATH" IS NULL) AND ("MESSAGE_AUDIO_CONTENT_TYPE" IS NULL) AND ("MESSAGE_AUDIO_DURATION_MS" IS NULL) AND ("MESSAGE_AUDIO_SIZE_BYTES" IS NULL) AND ("MESSAGE_AUDIO_WAVEFORM" IS NULL)) OR ((("MESSAGE_TYPE")::text = 'AUDIO'::text) AND ("MESSAGE_CONTENT" = ''::text) AND ("MESSAGE_AUDIO_PATH" IS NOT NULL) AND (length(("MESSAGE_AUDIO_PATH")::text) > 0) AND ("MESSAGE_AUDIO_CONTENT_TYPE" IS NOT NULL) AND (("MESSAGE_AUDIO_CONTENT_TYPE")::text = ANY ((ARRAY['audio/aac'::character varying, 'audio/mp4'::character varying])::text[])) AND ("MESSAGE_AUDIO_DURATION_MS" IS NOT NULL) AND ("MESSAGE_AUDIO_DURATION_MS" > 0) AND ("MESSAGE_AUDIO_SIZE_BYTES" IS NOT NULL) AND ("MESSAGE_AUDIO_SIZE_BYTES" > 0) AND ("MESSAGE_AUDIO_WAVEFORM" IS NOT NULL) AND (jsonb_typeof("MESSAGE_AUDIO_WAVEFORM") = 'array'::text))))
```

## 5. Defect found by P7, fix, and the Down that was run

- Defect: with the CHECK exactly as written in D4, P7 case (c) "AUDIO without waveform" was ACCEPTED (no error). Probes in transactions ending with ROLLBACK showed the same for an AUDIO row with content type NULL, duration NULL or size NULL (each `INSERTED (accepted)`).
- Cause: a CHECK whose expression evaluates to NULL passes in PostgreSQL. With a NULL waveform, `jsonb_typeof(NULL) = 'array'` is NULL, the AUDIO branch is NULL, `false OR NULL` is NULL, so the row passes. Same for `IN (...)` and `> 0` on NULL. Only the path was safe because D4 states `IS NOT NULL` for it.
- Fix (after approval): in the AUDIO branch, explicit `IS NOT NULL` on `MESSAGE_AUDIO_CONTENT_TYPE`, `MESSAGE_AUDIO_DURATION_MS`, `MESSAGE_AUDIO_SIZE_BYTES` and `MESSAGE_AUDIO_WAVEFORM`, with a comment in the migration explaining why. Same file and same migration name (nothing was committed in git).
- Sequence actually run (each step approved):
  1. P8 with the first version of the CHECK (guard refused, see P8 first run).
  2. Fixtures removed (COMMIT): 3 rows deleted by fixed id, `CONVERSATION_LAST_MESSAGE_DATE` restored to `2026-11-17 23:44:20.389891+00`, its value just before P5 (the sent date of the newest l132 text message, which `SendMessageAsync` writes into the conversation in the same transaction). The 6 l132 text messages were kept. Dry run first, COMMIT file differing on the last line only.
  3. Real Down, local only: `dotnet ef database update 20261110104601_Lot12_3a_MuxVideos ...`, exit 0, `Reverting migration '20261117234001_Lot13_2_VoiceMessages'.`, `Done.` Proof: last row of `__EFMigrationsHistory` = `20261110104601_Lot12_3a_MuxVideos`, 0 Lot13_2 columns, 0 CHECK constraint on MESSAGES, 6 l132 messages still present. This replaces the "do not run the Down" line of P8 (decision of the reviewer).
  4. Migration edited, `has-pending-model-changes`: no changes, build 0 errors / 19 warnings.
  5. API restarted (migration re-applied), then P1, P5, P6, P7, P8 and P9 redone. The results below are the final ones unless stated.

## 6. Runtime proofs

Shape of the commands: helper scripts in a scratch folder outside the repo (`$env:TEMP\mentora-lot13-132\`, deleted at the end). The API is started with `dotnet run --project Mentora.API --launch-profile http` through `cmd /c` with stdout and stderr redirected to a log file in that folder, and `Email__Provider=Logging` set in the child process environment only (plus `Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command=Information` for the after runs; sensitive data logging stays off, parameters are logged as `?`). Tokens: POST `/api/v1/auth/otp/request`, code read in memory from the last OTP console line of the log (never printed), POST `/api/v1/auth/otp/verify`, access tokens stored in a scratch JSON file (never printed). REST calls with `Invoke-WebRequest`; SignalR with a raw `System.Net.WebSockets.ClientWebSocket` (negotiate `?negotiateVersion=1`, websocket with `id` and `access_token`, JSON handshake plus 0x1E, invocations `JoinConversationAsync` then `SendMessageAsync`). SQL through `docker exec -i mentora-postgres-local sh -c 'psql ... -v ON_ERROR_STOP=1'`, reads in `BEGIN READ ONLY; ... ROLLBACK;`. Log filter: `Applying migration|No migrations|Application started|\[CORS\]|\[EMAIL\] routing|\[ADMIN\]|\[MUX\]|Unhandled exception|fail:|crit:`, never `OTP|EMAIL-LOG`.

### Preflight (section 1)

- Clean tree, branch `feature/lot_13`, top commits `bbafcc5`, `150d4f4`, `003d92a`.
- `EmailProvider=Logging TestRecipients=True AdminEmails=True AdminJwtLength=64 AspNetEnv=Development`. Port 5243 free. `mentora-postgres-local Up`.
- Last migration `20261110104601_Lot12_3a_MuxVideos`. MESSAGES had 8 columns (no type or audio column).
- Link local coach A / local member present (1 row), conversation present, `CONVERSATION_LAST_MESSAGE_DATE` NULL, 0 message in the conversation and 0 message in the whole table.

### Contract capture before the change (section 3)

```
[start-api] state=started log=api-before.log
[EMAIL] routing mode=TEST provider=Logging testRecipients=4 productionBcc=0
No migrations were applied. The database is already up to date.
Application started. Press Ctrl+C to shut down.
```

Statuses: 1 = 200, 2 = 200 (0 message), 3 = 201, 4 = 200, 5 = 200, 6 = 201, 7 = 200, 8 = hub handshake `{}`, join and send completions without error. Port 5243 free after the stop.

Deviation agreed before the run: the conversation was empty, so capture 1 had `lastMessage: null` and capture 2 an empty list, and there was no "oldest non test message". Captures 1 and 2 were rerun (GET only) after captures 3 to 8 (reruns 1b and 2b), and the oldest message of rerun 2b, `l132-before-member` (`2c93e1ad-2b9f-45fd-8b17-db668282bd1d`, written by the old code, filled by the migration default), was saved as the stand-in old message.

### P1 migration

```
[start-api] state=started log=api-after.log
Applying migration '20261117234001_Lot13_2_VoiceMessages'.
Application started. Press Ctrl+C to shut down.
```

Second application after the fix (`api-after2.log`): `Applying migration '20261117234001_Lot13_2_VoiceMessages'.` then `Application started.`

| column | type | nullable | default |
|---|---|---|---|
| MESSAGE_AUDIO_CONTENT_TYPE | character varying(20) | YES | |
| MESSAGE_AUDIO_DURATION_MS | integer | YES | |
| MESSAGE_AUDIO_PATH | character varying(200) | YES | |
| MESSAGE_AUDIO_SIZE_BYTES | integer | YES | |
| MESSAGE_AUDIO_WAVEFORM | jsonb | YES | |
| MESSAGE_TYPE | character varying(10) | NO | `'TEXT'::character varying` |

The 2 CHECK definitions are in section 4. `SELECT "MESSAGE_TYPE", count(*)`: first application `TEXT | 3` (0 before plus the 3 l132-before messages); second application `TEXT | 6` (the 6 kept l132 messages).

### P2 contract after (before / after key comparison)

| Capture | Level | Before | After | Verdict |
|---|---|---|---|---|
| 1 GET member conversation | status / top | 200 / 7 keys | 200 / same 7 keys | identical |
| 1 | lastMessage | null (empty conversation) | + 3 keys | see rerun 1b |
| 2 GET member messages | status / top | 200 / messages,nextCursor | same | identical |
| 2 | messages[0] | empty list | + 4 keys | see rerun 2b |
| 3 POST member message | status / top | 201 / message | same | identical |
| 3 | message | messageId,conversationId,content,senderType,senderId,isRead,sentDate,readDate | same + messageType,mediaUrl,durationSeconds,waveform | OK (4 keys appended) |
| 4 GET coach conversation | status / top | 200 / conversationId,memberId,coachId,visioUrl,createdDate,lastMessageDate,lastMessage | same | identical |
| 4 | lastMessage | messageId,content,senderType,sentDate,isRead | same + messageType,mediaUrl,durationSeconds | OK (3 keys appended) |
| 5 GET coach messages | status / top | 200 / messages,nextCursor | same | identical |
| 5 | messages[0] | 8 MessageDto keys | same + 4 keys | OK (4 keys appended) |
| 6 POST coach message | status / top | 201 / message | same | identical |
| 6 | message | 8 MessageDto keys | same + 4 keys | OK (4 keys appended) |
| 7 GET coach inbox | status / item | 200 / conversationId,memberId,memberFirstName,memberLastName,unreadCount,lastMessage,lastMessageDate | same | identical |
| 7 | item.lastMessage | 5 LastMessageDto keys | same + 3 keys | OK (3 keys appended) |
| 8 SignalR ReceiveMessage | frame | type,target,arguments | same | identical |
| 8 | arguments[0] | 8 MessageDto keys | same + 4 keys | OK (4 keys appended) |
| 1b rerun | status / top / lastMessage | 200 / 7 keys / 5 keys | 200 / same / + 3 keys | OK |
| 2b rerun | status / top / messages[0] | 200 / 2 keys / 8 keys | 200 / same / + 4 keys | OK |

Old message (stand-in): the same message in after capture 2, without the 4 new keys, is identical byte for byte (`True`, 283 = 283 characters). New keys of that message: `messageType=TEXT mediaUrl=null durationSeconds=null waveform=null`.

### P3 SignalR casing

`arguments[0]` keys of the ReceiveMessage frame, exact casing: `messageId,conversationId,content,senderType,senderId,isRead,sentDate,readDate,messageType,mediaUrl,durationSeconds,waveform`. Values: `messageType=TEXT`, `senderType=MEMBER`.

Answer to PJ's mail: the SignalR payload is camelCase (like REST), not PascalCase. This was already the case before the change (before capture 8: `messageId,...,readDate`).

### P4 SQL

From `api-after.log` (EF command log, parameters shown as `?`):
- Messages list (capture 2): one query, `SELECT m."MESSAGE_ID" ..., m."MESSAGE_TYPE" AS "MessageType", m."MESSAGE_AUDIO_DURATION_MS" AS "MessageAudioDurationMs", m."MESSAGE_AUDIO_WAVEFORM" AS "MessageAudioWaveform" FROM "MESSAGES" AS m WHERE m."CONVERSATION_ID" = @... ORDER BY m."MESSAGE_SENT_DATE" DESC LIMIT @...`.
- Conversation read (capture 1): one query, CONVERSATIONS `LIMIT 1` LEFT JOIN a `ROW_NUMBER() OVER(PARTITION BY m."CONVERSATION_ID" ORDER BY m."MESSAGE_SENT_DATE" DESC)` subquery on MESSAGES selecting `m."MESSAGE_TYPE"` and `m."MESSAGE_AUDIO_DURATION_MS"`.
- Inbox (capture 7): one query, MEMBER_COACHES LEFT JOIN CONVERSATIONS, INNER JOIN MEMBERS, correlated `count(*)` for unread, LEFT JOIN the same `ROW_NUMBER()` subquery selecting the new columns. Still one round trip, no N+1.
- INSERT of capture 3: `INSERT INTO "MESSAGES" ("MESSAGE_ID", "CONVERSATION_ID", "MESSAGE_AUDIO_CONTENT_TYPE", "MESSAGE_AUDIO_DURATION_MS", "MESSAGE_AUDIO_PATH", "MESSAGE_AUDIO_SIZE_BYTES", "MESSAGE_AUDIO_WAVEFORM", "MESSAGE_CONTENT", "MESSAGE_READ_DATE", "MESSAGE_SENDER_ID", "MESSAGE_SENDER_TYPE", "MESSAGE_SENT_DATE", "MESSAGE_TYPE") ...`: `MESSAGE_TYPE` is sent, the sentinel works.

Observation (not a change of this lot): the last message subquery numbers the rows of all MESSAGES (`PARTITION BY CONVERSATION_ID`) before joining; this is the existing EF translation of `OrderByDescending().FirstOrDefault()`, unchanged in shape by 13.2.

### P5 audio fixtures

One transaction (dry run with ROLLBACK, then the same file ending with COMMIT, diff on the last line only), run twice (before and after the fix): 3 AUDIO rows `13200000-0000-4000-8000-00000000000{1,2,3}`, senders local coach A / local member / local coach A, content `''`, `audio/aac`, path `<conversationId>/<messageId>.aac`, size 20000, 48 bar waveforms built with `generate_series` (`g*2`, `100-g*2`, `(g*7)%101`), durations 300 / 2500 / 4400 ms, sent dates now() minus 3, 2, 1 minutes, not read; conversation last message date set to the date of 0003. Result each time: `INSERT 0 3`, `UPDATE 1`, 6 to 9 messages in the conversation.

### P6 audio reads (final run)

Captures 2 and 5 (identical results):

```
13200000-...0001 type=AUDIO contentEmpty=True mediaUrl=/api/v1/conversations/58d5c27c-d823-48a8-bc68-59f053b02252/messages/13200000-0000-4000-8000-000000000001/audio urlOk=True durationSeconds=1 bars=48 waveformEqualsStored=True
13200000-...0002 type=AUDIO contentEmpty=True mediaUrl=/api/v1/conversations/58d5c27c-d823-48a8-bc68-59f053b02252/messages/13200000-0000-4000-8000-000000000002/audio urlOk=True durationSeconds=3 bars=48 waveformEqualsStored=True
13200000-...0003 type=AUDIO contentEmpty=True mediaUrl=/api/v1/conversations/58d5c27c-d823-48a8-bc68-59f053b02252/messages/13200000-0000-4000-8000-000000000003/audio urlOk=True durationSeconds=4 bars=48 waveformEqualsStored=True
```

Durations: 300 ms gives 1 (minimum), 2500 ms gives 3 (half away from zero), 4400 ms gives 4. Captures 1, 4 and 7: `lastMessage` is fixture 0003, `type=AUDIO`, content empty, mediaUrl `/api/v1/conversations/58d5c27c-d823-48a8-bc68-59f053b02252/messages/13200000-0000-4000-8000-000000000003/audio`, `durationSeconds=4`. Same results in the first run (before the fix).

### P7 CHECK refusals (final run, each insert in its own transaction ending with ROLLBACK)

```
count before P7: 9
(a) AUDIO with content x      ERROR:  new row for relation "MESSAGES" violates check constraint "CK_MESSAGES_AUDIO_FIELDS"
(b) TEXT with an audio path   ERROR:  new row for relation "MESSAGES" violates check constraint "CK_MESSAGES_AUDIO_FIELDS"
(c) AUDIO without waveform    ERROR:  new row for relation "MESSAGES" violates check constraint "CK_MESSAGES_AUDIO_FIELDS"
(d) AUDIO with audio/wav      ERROR:  new row for relation "MESSAGES" violates check constraint "CK_MESSAGES_AUDIO_FIELDS"
(e) MESSAGE_TYPE VIDEO        ERROR:  new row for relation "MESSAGES" violates check constraint "CK_MESSAGES_AUDIO_FIELDS"
(f) AUDIO content type NULL   ERROR:  new row for relation "MESSAGES" violates check constraint "CK_MESSAGES_AUDIO_FIELDS"
(g) AUDIO duration NULL       ERROR:  new row for relation "MESSAGES" violates check constraint "CK_MESSAGES_AUDIO_FIELDS"
(h) AUDIO size NULL           ERROR:  new row for relation "MESSAGES" violates check constraint "CK_MESSAGES_AUDIO_FIELDS"
(i) AUDIO waveform NULL       ERROR:  new row for relation "MESSAGES" violates check constraint "CK_MESSAGES_AUDIO_FIELDS"
(e probe) VIDEO with CK_MESSAGES_AUDIO_FIELDS dropped inside a transaction ending with ROLLBACK: ERROR:  new row for relation "MESSAGES" violates check constraint "CK_MESSAGES_TYPE"
count after P7: 9
constraints present after the probe (read only): CK_MESSAGES_AUDIO_FIELDS, CK_MESSAGES_TYPE
```

(e): both constraints fail for 'VIDEO'; PostgreSQL evaluates CHECK constraints in name order (`CK_MESSAGES_AUDIO_FIELDS` before `CK_MESSAGES_TYPE`) and reports the first one. The probe shows `CK_MESSAGES_TYPE` refusing the row on its own. The temporary drop was rolled back (both constraints present afterwards).

First run (before the fix): (a), (b), (d) and (e) refused as above, (c) accepted (no error), count 9 before and after (rolled back). See section 5.

### P8 Down guard

API stopped first, `port5243Free=True`. `dotnet ef database update 20261110104601_Lot12_3a_MuxVideos --project Mentora.Infrastructure --startup-project Mentora.API` with the 3 fixtures present, run twice (before and after the fix), same result each time:

```
exit=1
Reverting migration '20261117234001_Lot13_2_VoiceMessages'.
P0001: Lot13_2 Down refused: audio messages exist.
```

Nothing changed: last row of `__EFMigrationsHistory` still `20261117234001_Lot13_2_VoiceMessages`, 6 columns, `CK_MESSAGES_AUDIO_FIELDS, CK_MESSAGES_TYPE` present, `AUDIO | 3`, `TEXT | 6`.

### P9 second start

```
[start-api] state=started log=api-p9.log
No migrations were applied. The database is already up to date.
Application started. Press Ctrl+C to shut down.
port5243Free=True
```

No `Unhandled exception`, `fail:` or `crit:` line in any filtered log.

## 7. Cleanup

One SQL file, dry run ending with ROLLBACK, then the same file ending with COMMIT (diff on the last line only). Deletes the 3 fixtures by fixed id and the l132 messages bounded by content `LIKE 'l132-%'` AND sent date `>= '2026-11-17 23:27:11.769391+00'` AND the local conversation id; restores `CONVERSATION_LAST_MESSAGE_DATE` to NULL (section 1 value).

Final check (printed in the dry run before the COMMIT, identical in the COMMIT run):

| check | value |
|---|---|
| before: conversation messages / l132 / fixtures | 9 / 6 / 3 |
| DELETE fixtures / DELETE l132 / UPDATE conversation | 3 / 6 / 1 |
| messages left with an l132 content or a 13200000- id | 0 |
| conversation messages (section 1 value: 0) | 0 |
| AUDIO messages in the whole table | 0 |
| messages in the whole table (section 1 value: 0) | 0 |
| last message date restored to NULL | true |

Rows created since the run start, for every table with a `*_CREATED_DATE` column (42 tables): 0 everywhere except `AUTH_OTPS` 6 and `AUTH_REFRESH_TOKENS` 6, written by the OTP logins (3 token rounds: before, after, after the fix; 2 accounts each). These auth rows are left in place, as in previous lots.

The scratch folder (helper scripts, captures, logs, tokens) was deleted at the end, `Test-Path` returned False (see the final report).

## 8. Differences from the expected results

1. P7 (c) failed with the CHECK as specified in D4 (NULL passes a CHECK); fixed by explicit `IS NOT NULL` terms, migration re-applied after a real local Down, P1 and P5 to P9 redone (section 5). P7 was extended to 9 cases (f to i cover the NULL cases).
2. P7 (e) reports `CK_MESSAGES_AUDIO_FIELDS` instead of `CK_MESSAGES_TYPE` (name order); `CK_MESSAGES_TYPE` proven separately by the rolled back probe.
3. The local conversation was empty at the start: captures 1 and 2 were rerun (1b, 2b) for the before shapes, and `l132-before-member` was used as the stand-in old message (agreed before the run).
4. My first key report for the before inbox capture was wrong (a PowerShell 5.1 `ConvertFrom-Json` array quirk); the report function was fixed and the keys were re-read from the saved raw file, without a second request.
5. The real Down to Lot12_3a was run once (section 5), on the reviewer's decision, replacing "do not run the Down after the cleanup".
