# Lot 14, sub-lot 14.9b: push notifications (first events), runtime evidence

Branch feature/lot_14, base commit 47dff4c (14.9a evidence), local database only (mentora_db, docker container mentora-postgres-local), API on port 5243 with ASPNETCORE_ENVIRONMENT=Development and Email__Provider=Logging. Run start (read from the database before any fixture): 2026-11-25T00:51:37.717Z. All fixtures prefixed l149b. No device token, JWT, refresh token, OTP, access token or content of the Firebase key file appears in this document, in a log line shown, or in the committed files.

## 1. Decisions applied (François, ticket 905 spec 14.J, contract of PJ dated 21/11/2026)

| Point | Applied as |
|---|---|
| 3.1 events | CHAT_MESSAGE (both directions, text and audio), SESSION_BOOKED, SESSION_CANCELLED, MEMBER_JOINED, data.type exact strings, preferences as specified, missing parameter row counts as defaults (true), the actor never notified, no row when a preference is off |
| 3.2 texts | French, Europe/Paris dates in the format "dd/MM à HH'h'mm", chat body trimmed, one line, cut at 120 characters with "...", audio "Message vocal" (`Mentora.Core/Notifications/NotificationTexts.cs`) |
| 3.3 storage | table NOTIFICATIONS, entity `Mentora.Core/Entities/Notification.cs`, configuration `NotificationConfiguration.cs`, 4 named CHECKs in the migration, 2 indexes, string constants in `Mentora.Core/Notifications/NotificationTypes.cs` |
| 3.4 same save | `INotificationComposer` only adds entities to the caller's MentoraDbContext, never SaveChanges, one query per call |
| 3.5 sender | `PushNotificationSender` BackgroundService, every 3 s, 20 rows, FOR UPDATE SKIP LOCKED, loop wrapped in try/catch |
| 3.6 FCM client | typed HttpClient, timeout 10 s, RS256 assertion with System.IdentityModel.Tokens.Jwt, token cached until 5 minutes before expiry, one retry after a 401 |
| 3.7 options | section Firebase, `FirebaseOptions`, `FirebaseOptionsValidator`, ProjectId in appsettings.json, `[PUSH]` startup line |
| 3.8 key file protection | 3 patterns in .gitignore and .dockerignore (section 12) |
| 3.9 devices | no change (section 11) |
| No NuGet package, no Google or Firebase SDK | none added |

Interpretation of the attempt rule (3.5): 3 attempts in total. After the 1st retryable failure the row stays PENDING for 1 minute, after the 2nd for 5 minutes, the 3rd ends in FAILED RETRIES_EXHAUSTED. Final error code when no device succeeded (precision A of François): ALL_TOKENS_INVALID when every device of the recipient was removed, else RETRIES_EXHAUSTED when a retryable failure remains, else HTTP_<code> of the last non-retryable device failure, else EXCEPTION, else SEND_FAILED (`PushNotificationSender.cs:188`).

## 2. Files and call sites

New files: `Mentora.Core/Entities/Notification.cs`, `Mentora.Core/Notifications/NotificationTypes.cs` (types, statuses, error codes), `Mentora.Core/Notifications/NotificationTexts.cs`, `Mentora.Core/Options/FirebaseOptions.cs`, `Mentora.Core/Interfaces/INotificationComposer.cs`, `Mentora.Infrastructure/Persistence/Configurations/NotificationConfiguration.cs`, `Mentora.Infrastructure/Services/Push/` (`NotificationComposer.cs`, `FirebaseServiceAccount.cs`, `FirebaseOptionsValidator.cs`, `FcmClient.cs`, `PushNotificationSender.cs`), migration `20261124234929_Lot14_9b_Notifications` (with Designer and model snapshot).

Modified: `Mentora.API/Program.cs`, `Mentora.API/appsettings.json` (Firebase:ProjectId only), `MentoraDbContext.cs` (DbSet), `ConversationService.cs`, `SessionService.cs`, `AuthService.cs`, `.gitignore`, `.dockerignore`.

| Event | Call site |
|---|---|
| CHAT_MESSAGE text (REST and hub, the hub calls `ConversationService.SendMessageAsync` at `Mentora.API/Hubs/ChatHub.cs:55`) | `ConversationService.cs:146` |
| CHAT_MESSAGE audio, inside the CancellationToken.None transaction | `ConversationService.cs:226` |
| SESSION_BOOKED to the coach (member reserves) | `SessionService.cs:128` |
| SESSION_CANCELLED to the coach (member cancels) | `SessionService.cs:204` |
| SESSION_CANCELLED to the member (coach cancels an individual session) | `SessionService.cs:318` |
| SESSION_BOOKED to the member (coach registers him in a group session) | `SessionService.cs:592` |
| SESSION_CANCELLED to the member (coach removes him from a group session) | `SessionService.cs:630` |
| MEMBER_JOINED (first member login, same transaction as MemberHasActivated) | `AuthService.cs:171` |

Wiring in `Mentora.API/Program.cs`: registrations at lines 425 to 432, Firebase options read at line 580 (see section 3), `[PUSH]` startup block at lines 597 to 604 (after the `[AUDIO]` block).

Other references: composer single query `NotificationComposer.cs:115` (`LoadPairAsync`), sender SQL with FOR UPDATE SKIP LOCKED `PushNotificationSender.cs:74`, expiry rule `:107`, NOT_CONFIGURED `:113`, NO_DEVICE `:117`, device removed log `:134`, payload rejected log `:148`, retry rule `:163`, final error `:173` and `:188`, loop error log `:44`, FCM error interpretation `FcmClient.cs:206`, 401 retry `FcmClient.cs:170`, validator `FirebaseOptionsValidator.cs:14`.

## 3. Changes and overrides made after the approval of the code (accepted by François)

1. **Program.cs, `Mentora.API/Program.cs:578-580`**: the Firebase options are read (`_ = ...Value`) just before the audio probe. Reason: a Production start on this PC stopped at the audio check (Audio:StoragePath, no ffmpeg) before the Firebase check, so the Firebase refusal could not be shown. The `[PUSH]` line itself stays after the `[AUDIO]` block (line 601). Build after the change: 0 errors, 19 warnings.
2. **AuthRateLimit__PermitLimit=300**: process environment variable of the proof API only, never in a config file. Reason: repeated OTP logins of the scripts hit the rate limit (429, 30 requests per 15 minutes, in memory). The restart also reset the limiter.
3. **Exit code 127**: the background `dotnet run` commands report exit code 127 when they end. These are the API processes that I stopped myself (Stop-Process on the listening process) between proofs, not crashes. The log has 0 `fail:` and 0 `crit:` lines.
4. **Conversation reset**: sending a message writes one column of the existing member@mentora.fr / coach@mentora.fr conversation, `CONVERSATION_LAST_MESSAGE_DATE` (timestamptz, nullable), only in the send paths (`ConversationService.cs:142` and `:222`). There is no unread counter, last message id or text column. The conversation had 0 messages before the run, so its original value NULL is inferred, not recorded. The cleanup sets it back to NULL for that one conversation, matched by id and only when no message is left for it.
5. **check-proofs.out.txt** was first written with `tee` by mistake (rule 2.3) and rewritten with the Write tool, same content.

## 4. Build

`dotnet build --no-incremental` (output in build.log, ignored by git): HEAD before any change 0 errors and 19 warnings; after the code, after the Program.cs change and at the end: 0 errors and 19 warnings, all existing CS1573 documentation warnings, none in a file of this sub-lot.

## 5. Migration lines

Up (`20261124234929_Lot14_9b_Notifications.cs`): CreateTable NOTIFICATIONS (line 14) with the FK FK_NOTIFICATIONS_USERS (Restrict), indexes IX_NOTIFICATIONS_STATUS_NEXT_ATTEMPT (line 44) and IX_NOTIFICATIONS_USER_ID (line 49), then 4 CHECKs through migrationBuilder.Sql, each wrapped in COALESCE(..., false) so a NULL is rejected: CK_NOTIFICATIONS_TYPE (line 59), CK_NOTIFICATIONS_STATUS (66), CK_NOTIFICATIONS_ATTEMPT_COUNT (73), CK_NOTIFICATIONS_DATA (80). Nothing else is created. The model snapshot diff is 95 added and 0 removed lines, all of the Notification entity and its foreign key.

Down (lines 88 to 93): DROP CONSTRAINT for the 4 CHECKs in reverse order, then DropTable NOTIFICATIONS.

Applied at the API start (proof 5.1): `Applying migration '20261124234929_Lot14_9b_Notifications'.` Startup line: `[PUSH] configured=True projectId=mentora-69209 pollSeconds=3 maxAttempts=3`. 0 `fail:`, 0 `crit:`. The `warn:` lines are existing seeder messages ([CATALOG], [QUIZ]) and existing EF model validation warnings, none about Notification.

## 6. CHECK proofs (5.2, `check-proofs.sql`, output in `check-proofs.out.txt`)

One transaction ending in ROLLBACK. The 10 NOT NULL columns set to NULL fail with sqlstate 23502 on the expected column. Unknown type fails with CK_NOTIFICATIONS_TYPE, unknown status with CK_NOTIFICATIONS_STATUS, attempt count 11 and -1 with CK_NOTIFICATIONS_ATTEMPT_COUNT, data as a JSON array and as a JSON string with CK_NOTIFICATIONS_DATA (sqlstate 23514). A control row is accepted (and rolled back). After the rollback the table has 0 rows.

## 7. Event proofs over HTTP (5.3)

Accounts coach@mentora.fr and member@mentora.fr, tokens obtained in scripts and never printed. Fixtures through the existing routes only: 2 products (solo and group, the group one published), 3 slots, 3 vouchers through the voucher grants (which created 2 orders), 3 sessions, 1 participant, 1 member l149b-member1@example.com.

| Step | Result |
|---|---|
| a. member sends a text to his coach | POST 201, 1 new row: CHAT_MESSAGE, recipient the coach user, data keys `memberId,type` |
| b. coach sends a text to the member | POST 201, 1 new row for the member user, data keys `coachId,type` |
| c. member NotifMessages set to false (PUT 200), coach sends again | POST 201, 0 new row; preference restored to its previous value True (PUT 200, read back True) |
| d. empty content | 422, 0 new row |
| e. member reserves | 200, 1 SESSION_BOOKED row for the coach, keys `memberId,sessionId,type`, text "Nouvelle réservation / Test Member a réservé une séance le 29/11 à 10h00." |
| e. member cancels | 200, 1 SESSION_CANCELLED row for the coach, text "Séance annulée / Test Member a annulé la séance du 29/11 à 10h00." |
| f. coach cancels an individual session | 200, 1 SESSION_CANCELLED row for the member, keys `sessionId,type`, text "Séance annulée / Votre séance du 30/11 à 10h00 avec Federico est annulée." |
| g. coach registers the member in a group session | 201, 1 SESSION_BOOKED row for the member, text "Nouvelle séance / Federico vous a inscrit à une séance le 01/12 à 10h00." |
| g. coach removes him | 204, 1 SESSION_CANCELLED row for the member |
| h. coach adds l149b-member1@example.com | 201, 0 row; first login of that member: 1 MEMBER_JOINED row for the coach, keys `memberId,type`, text "Nouvel élève connecté / L149b Member1 vient de se connecter à Mentora."; second login: 0 new row |
| i. EF SQL of the member text message request | 3 SELECT statements in the request, of which exactly 1 is the composer's (member, coach and their parameters in one projection), plus 1 INSERT into NOTIFICATIONS |

A slot at 09:00 UTC is shown as 10h00 (Europe/Paris), as expected. At the end of the event proofs the table held 9 rows, all NO_DEVICE after the sender ran.

Audio messages cannot be proven locally (no ffmpeg on the PC). By code reading: `ConversationService.SendAudioMessageAsync` adds the CHAT_MESSAGE row at `ConversationService.cs:226`, inside the `CancellationToken.None` transaction and before its SaveChanges, with a null text so the body is "Message vocal" (`NotificationTexts.cs`, `ChatBody`). The proof will be done on api-demo.

## 8. Sender proofs (5.4)

- a. A row for a user without device became NO_DEVICE about 2.2 s after the message.
- b. A fake token ("l149b-fake-" followed by a new GUID, platform ANDROID) registered for the coach through POST /api/v1/devices (200). A CHAT_MESSAGE to the coach: within about 2.7 s the real Google OAuth call succeeded (FCM answered INVALID_ARGUMENT, so not 401), the device row was deleted (0 devices left), and the notification is FAILED with ALL_TOKENS_INVALID, attempt count 1. Log lines of the step, ids only: `[PUSH] device removed deviceId=... reason=INVALID_ARGUMENT` and `[PUSH] sent notificationId=... type=CHAT_MESSAGE status=FAILED devices=1 ok=0 removed=1`. The log has 0 mention of the fake token.
- c. API restarted with Firebase__ServiceAccountPath emptied for that process only: `[PUSH] configured=False projectId=mentora-69209 pollSeconds=3 maxAttempts=3`, a new notification became FAILED with NOT_CONFIGURED. The variable was restored (the terminal never changed) and the check of 1.2 gave again `Logging, Development, True, True, 64, True, True, True`.
- d. Startup refusals, no database touched (no `Applying migration` line, no `Application started`), message lines only: Production with an empty path: `Firebase:ServiceAccountPath must be set as an environment variable outside Development.` Development with a path to a non-existent file: `Firebase:ServiceAccountPath does not point to an existing file.` Neither message contains the path.
- e. One row created in this run (by id) was moved 25 hours back and set to PENDING by a single UPDATE (`UPDATE 1`); within 10 s it became FAILED with EXPIRED.

Guard B (payload problem), proven by code reading: the device row is deleted only for UNREGISTERED (`FcmClient.cs:216`) or INVALID_ARGUMENT whose BadRequest fieldViolations contain "message.token" or name no field (`FcmClient.cs:218-220`). INVALID_ARGUMENT that names only other fields returns a non-retryable failure with the field name and no deletion (`FcmClient.cs:224`); the sender logs once `[PUSH] payload rejected notificationId=... field=...` (`PushNotificationSender.cs:148`, field name only, never the body) and the final code is HTTP_400 (`PushNotificationSender.cs:188`). Not provoked locally: the fake token case returned the token case.

## 9. Contract (5.5)

No DTO, controller, hub or validator file differs from HEAD (0 files under Mentora.Core/DTOs, Mentora.API/Controllers, Mentora.API/Hubs, Mentora.Core/Validators). Keys in order, captured at runtime:
- otp/verify: envelope `success,data,error,statusCode`, data `accessToken,refreshToken,expiresIn,isCoach`.
- message POST: `{ message }` (SendMessageResponseDto, unchanged; controller lines identical to HEAD).
- session list item: `sessionId,voucherId,slotId,coachId,coachDisplayName,memberId,memberFirstName,memberLastName,productId,productName,offerType,durationMinutes,sport,scheduledAt,status,visioUrl,effectiveAddress,maxParticipants,cancellationReason,cancelledBy,cancelledAt,createdDate,updatedDate`.
- devices POST: envelope `success,data,error,statusCode` with data null; DELETE answers 200.

## 10. Cleanup

`cleanup-local.sql` (dry run, ends in ROLLBACK) then `cleanup-local-commit.sql` (identical, only the last line is COMMIT; `git diff --no-index --ignore-cr-at-eol` shows `-ROLLBACK;` and `+COMMIT;` only). Every id is a literal, every DELETE bounded by ids and the run start, except the backdated notification of proof 5.4 e (by id). Before: 43 tables have a *_CREATED_DATE column; nonzero since the run start: AUTH_OTPS 33, AUTH_REFRESH_TOKENS 31, MEMBERS 1, NOTIFICATIONS 12 (13 in total with the backdated one), ORDERS 2, PRODUCTS 2, SESSIONS 3, SESSION_PARTICIPANTS 1, SESSION_SLOTS 3, SESSION_VOUCHERS 3, USERS 1; plus MESSAGES 7, MEMBER_COACHES 1, ORDER_ITEMS 2.

Counts printed by both runs (identical): NOTIFICATIONS 13, MESSAGES 7, CONVERSATIONS updated 1 (messages left 0, column null true), SESSION_PARTICIPANTS 1, SESSIONS 3, SESSION_VOUCHERS 3, ORDER_ITEMS 2, ORDERS 2, SESSION_SLOTS 3, PRODUCTS 2, AUTH_OTPS of the l149b member 2, AUTH_REFRESH_TOKENS of the l149b member 2, USER_DEVICES 0, MEMBER_PARAMETERS 0, MEMBER_COACHES 1, CONVERSATIONS 0, MEMBERS 1, USERS 1.

After the COMMIT run: all 43 per-table counts are 0 except AUTH_OTPS 31 and AUTH_REFRESH_TOKENS 29 (authentication rows of coach and member created in the run, kept on purpose as in previous sub-lots); MESSAGES, MEMBER_COACHES and ORDER_ITEMS since the run start 0; NOTIFICATIONS total 0; rows matching l149b 0; USER_DEVICES total 0 (same as before the run); NotifMessages of member@mentora.fr true (its original value). A read-only query after the COMMIT confirmed 0 l149b users, products and messages.

## 11. Devices (3.9, no change)

`UserDeviceService.RegisterAsync` already re-assigns an existing token to the new user (`UserDeviceService.cs:49-62`, reassign at `:58`), and logout already deletes the device when deviceToken is given (`AuthService.cs:295-298`, owner-scoped). Nothing changed there. The sender deletes a device by its id with ExecuteDeleteAsync (`PushNotificationSender.cs:134` area).

## 12. Key file protection (3.8)

Patterns added to .gitignore (lines 507 to 509) and .dockerignore (lines 47 to 49): `**/*firebase-adminsdk*.json`, `**/service-account*.json`, `**/secrets/`. `git check-ignore -v` on dummy paths (no file created): `Mentora.API/mentora-69209-firebase-adminsdk-abc.json` matches .gitignore:507, `some/dir/service-account-prod.json` matches .gitignore:508, `deploy/secrets/key.json` matches .gitignore:509.

## 13. Migration Down and Up (5.6)

With the API stopped (port 5243 free), Development, no notification row left:
- Down (`dotnet ef database update 20261124221921_Lot14_9a_CircuitModes`, binaries already built): `Reverting migration '20261124234929_Lot14_9b_Notifications'`, DROP CONSTRAINT of CK_NOTIFICATIONS_DATA, CK_NOTIFICATIONS_ATTEMPT_COUNT, CK_NOTIFICATIONS_STATUS, CK_NOTIFICATIONS_TYPE, `DROP TABLE "NOTIFICATIONS"`, `DELETE FROM "__EFMigrationsHistory"`, Done. Read-only check: NOTIFICATIONS absent, last history row 20261124221921_Lot14_9a_CircuitModes.
- Up (`dotnet ef database update`): `Applying migration '20261124234929_Lot14_9b_Notifications'`, CREATE TABLE, 2 CREATE INDEX, 4 ALTER TABLE ... CHECK, INSERT INTO __EFMigrationsHistory, Done. Read-only check: NOTIFICATIONS present with 0 rows, constraints CK_NOTIFICATIONS_ATTEMPT_COUNT, CK_NOTIFICATIONS_DATA, CK_NOTIFICATIONS_STATUS, CK_NOTIFICATIONS_TYPE present, indexes IX_NOTIFICATIONS_STATUS_NEXT_ATTEMPT and IX_NOTIFICATIONS_USER_ID present, last history row 20261124234929_Lot14_9b_Notifications.

## 14. Limits and out of scope

Limits:
- Audio messages are proven by code reading only (no ffmpeg on the PC); the proof will be done on api-demo.
- No real device delivery yet: it will be proven with PJ's Android phone on api-demo (registration through POST /api/v1/devices, a chat message and a booking, then uninstall and resend for UNREGISTERED).
- The payload rejected guard (guard B) is proven by code reading only.
- The original NULL of the conversation last message date is inferred, not recorded (section 3).
- The sender keeps its row locks while the FCM calls of a batch run (20 rows, 10 s timeout per call); acceptable for one API container.

Out of scope for 14.9b: SESSION_REMINDER sending (the type is allowed by the CHECK, nothing emits it), the notification click route, the welcome mail change, the profile photo, the purge of old notifications, any change of an existing response shape or route.
