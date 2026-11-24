# Lot 14, sub-lot 14.9.0: read-only inventory (block types and push notifications)

Date: 24/11/2026. Branch feature/lot_14, HEAD 603aae8 (equal to origin/main), clean tree at start.
Scope: read-only. No build, no run, no database, no network. Every reference is file:line on HEAD 603aae8.
Configuration files (appsettings, .env, logs) were not opened.

## Summary

Part A (block types)
- The circuit mode is a free string everywhere: no C# enum, no EnumMappings entry, no CHECK constraint (table column or jsonb). The only gate is `ProgramCircuitRules` (STANDARD | INTERVAL, 422 otherwise).
- One validator, three entry points: program templates (POST/PUT), training programs (POST with body, PUT id-based tree), session templates (POST/PUT). Two write paths copy stored data WITHOUT validation: assignment from a template (`ProgramService.AssignAsync` with templateId) and session template duplication.
- Storage: one table column (`PROGRAM_CIRCUITS.PROGRAM_CIRCUIT_MODE`, varchar(20), NOT NULL, no default) and two jsonb documents (`PROGRAM_TEMPLATES.PROGRAM_TEMPLATE_BODY`, `SESSION_TEMPLATES.SESSION_TEMPLATE_CONTENT`), key `"mode"`.
- Mode is never branched on outside the validator: no mapping, completion, last-performed, visio or response logic depends on it. Adding values is therefore a validator + data change only.
- `DISTANCE` has no distance field in the model (not verified with PJ how the app sends a distance).
- Recommended path: per-type rules table in `ProgramCircuitRules`, one data-only migration rewriting STANDARD to SERIE_SIMPLE (column and both jsonb), STANDARD still accepted on write and normalized, named CHECK on the column. About 7 to 8 hours.

Part B (push notifications)
- Devices: table, unique token, re-assignment and logout removal exist and match the contract. Missing: a delete-by-token path for FCM cleanup, last error, language, role per device.
- Preferences exist on both sides (`MemberParameter`, `CoachParameter`), but not one per event type on each side (no member booking pref, no coach reminder pref).
- Every Beta event has a single, clear commit point in a service. Chat (REST text, REST voice, SignalR) converges on `ConversationService`.
- Background work: exactly one `BackgroundService` (`RefreshTokenPurgeService`). No queue, no channel, no scheduler, no notifications table or route.
- RS256 signing for the Google service-account flow is possible with the packages already referenced. No Google or Firebase package is referenced.
- Neither `.gitignore` nor `.dockerignore` would stop a service-account JSON key placed in the repo; the Web SDK would also publish a `*.json` in the API project into the image.
- Recommended design: transactional outbox table `NOTIFICATIONS` + one dispatcher `BackgroundService` + SDK-free FCM HTTP v1 client + reminder scan with a dedup key. About 19 hours.

## Part A: block types

### 5.1 ProgramCircuitRules: current rules and callers

Rules (`Mentora.Core/Validators/ProgramTemplate/ProgramCircuitRules.cs`):
- Empty circuit list ignored here, callers decide (line 26-27).
- Positions unique and contiguous from 1, null entry refused (line 29-30, 103-119).
- `Name` required (line 36-37).
- `Mode` must be exactly `STANDARD` or `INTERVAL`, else 422 on key `{path}[{Position}].Mode`, message "Mode must be one of: STANDARD, INTERVAL." (line 39-43).
- INTERVAL requires `Rounds` (line 45-46). STANDARD does not forbid `Rounds` (not checked).
- `RestSecondsAfterExercices` between 0 and 600 (`MaxRestAfterSeconds`, line 13, 48-50).
- `RestBetweenRoundsSeconds`: no rule at all.
- At least one exercise per circuit (line 60-64), exercise positions (line 66-67), visibility callback per exercise (line 73).
- `LoadType`: ELASTIC refused, else KG | BODYWEIGHT (line 15, 75-78).
- STANDARD: `PrescribedSets` and `PrescribedReps` required, `WorkSeconds` and `RestWorkSeconds` forbidden, key `{path}[p].Exercises[q]` (line 80-87).
- INTERVAL: `WorkSeconds` and `RestWorkSeconds` required, `PrescribedSets` and `PrescribedReps` forbidden (line 88-95).
- Any other mode: no exercise-level rule (the mode failure at line 43 already makes the request 422).
- No numeric range on sets, reps, weight, work or rest-work seconds, nor on `RestSecondsAfterSeries`.

Callers:
| Caller | Entry | file:line |
|---|---|---|
| `ProgramTemplateBodyValidator.ValidateCircuits` | every MICROCYCLE session of a tree | `Mentora.Core/Validators/ProgramTemplate/ProgramTemplateBodyValidator.cs:188, 206-212` |
| `ProgramTemplateRequestValidator` | program templates POST/PUT | `Mentora.Core/Validators/ProgramTemplate/ProgramTemplateRequestValidator.cs:39`; called by `Mentora.Infrastructure/Services/ProgramTemplateService.cs:67, 99, 127-132` |
| `ProgramService.ValidateBodyTreeAsync` | training programs POST (assign with a body, no templateId) | `Mentora.Infrastructure/Services/ProgramService.cs:105, 792-801` |
| `UpdateProgramRequestValidator` | training programs PUT (11.2c id-based tree, id-free copy validated under `Body`) | `Mentora.Core/Validators/Program/UpdateProgramRequestValidator.cs:38-40`; `Mentora.Core/DTOs/Program/Body/UpdateProgramBody.cs:18-22`; `Mentora.Core/DTOs/Program/Body/UpdateProgramCircuit.cs:27-37`; called by `ProgramService.cs:179-184` |
| `SessionTemplateRequestValidator` | session templates POST/PUT | `Mentora.Core/Validators/SessionTemplate/SessionTemplateRequestValidator.cs:60-63`; called by `Mentora.Infrastructure/Services/SessionTemplateService.cs:130, 160, 222-229` |

Paths that write circuits WITHOUT the validator:
- Assignment of a template to a member: `ProgramService.AssignAsync` with `templateId` deserializes the stored body (`ProgramService.cs:84`) and copies it (`CopyBlockTree`, `ProgramService.cs:148, 844-859`) with no validation. The comment at `ProgramService.cs:837` already documents this ("Path W3a copies a stored template without validation").
- Session template duplication: `SessionTemplateService.DuplicateAsync` copies the jsonb text as is (`SessionTemplateService.cs:183-206`, line 196).
- `ProgramTemplateSeeder` (insert only, platform templates missing by name): `Mentora.Infrastructure/Seeding/ProgramTemplateSeeder.cs:41-49, 83-94`.
- There is no program template duplication route (grep of the controllers: only `CoachSessionTemplatesController.cs:133`).

Consequence: a stored STANDARD (template jsonb) assigned after the change is copied into `PROGRAM_CIRCUITS` as STANDARD unless the data is rewritten or the copy maps it.

### 5.2 Every place that reads or writes the circuit mode

| Layer | Item | file:line |
|---|---|---|
| Entity | `ProgramCircuit.ProgramCircuitMode` (string, comment "STANDARD \| INTERVAL") | `Mentora.Core/Entities/ProgramCircuit.cs:13` |
| EF configuration | column `PROGRAM_CIRCUIT_MODE`, required, max length 20, no default | `Mentora.Infrastructure/Persistence/Configurations/ProgramCircuitConfiguration.cs:36-39` |
| Migration | `character varying(20)`, NOT NULL | `Mentora.Infrastructure/Persistence/Migrations/20260812084549_Lot6_3_Programs.cs:128` |
| Snapshot | `PROGRAM_CIRCUIT_MODE` | `Mentora.Infrastructure/Persistence/Migrations/MentoraDbContextModelSnapshot.cs:2198` |
| Request DTO (templates, POST tree, session templates) | `ProgramTemplateCircuit.Mode`, XML doc "STANDARD \| INTERVAL." (Swagger description) | `Mentora.Core/DTOs/ProgramTemplate/Body/ProgramTemplateCircuit.cs:14-15`; class remark on STANDARD execution (superset / triset / giant set) line 3-8 |
| Request DTO (PUT tree) | `UpdateProgramCircuit.Mode`, same XML doc; copied by `ToTemplateCircuit` | `Mentora.Core/DTOs/Program/Body/UpdateProgramCircuit.cs:13-14, 31` |
| Response DTO (training programs) | `ProgramCircuitResponse.Mode` | `Mentora.Core/DTOs/Program/ProgramCircuitResponse.cs:7` |
| Response DTO (program templates) | body returned as the deserialized stored `ProgramTemplateBody` | `Mentora.Infrastructure/Services/ProgramTemplateService.cs:174` |
| Response DTO (session templates) | `SessionTemplateResponse.Circuits` is `List<ProgramTemplateCircuit>` read from jsonb | `Mentora.Core/DTOs/SessionTemplate/SessionTemplateResponses.cs:40`; `SessionTemplateService.cs:279-280, 304` |
| Service write (PUT) | `circuit.ProgramCircuitMode = node.Mode` | `Mentora.Infrastructure/Services/ProgramService.cs:396` |
| Service write (assign copy) | `ProgramCircuitMode = tc.Mode` | `ProgramService.cs:854` |
| Service read | `BuildCircuitResponses` passes the stored value through | `ProgramService.cs:1041` |
| jsonb write (program templates) | `JsonSerializer.Serialize(request.Body)` | `ProgramTemplateService.cs:77, 105` |
| jsonb write (session templates) | `JsonSerializer.Serialize(request.Circuits)` | `SessionTemplateService.cs:141, 166`; duplicate line 196 |
| jsonb columns | `PROGRAM_TEMPLATE_BODY` jsonb; `SESSION_TEMPLATE_CONTENT` jsonb | `Mentora.Infrastructure/Persistence/Configurations/ProgramTemplateConfiguration.cs:50-52`; `Mentora.Infrastructure/Persistence/Configurations/SessionTemplateConfiguration.cs:47-48` |
| EnumMappings | no circuit mode entry (only a `using` matches) | `Mentora.Core/Enums/EnumMappings.cs:1` |
| Swagger | no operation filter or example mentions circuits or mode (grep of `Mentora.API/Swagger`, no match); the description comes from the XML doc comments above only | not applicable |

CHECK constraints: none on the circuit mode. Searched every non-Designer migration for `CHECK`, `CIRCUIT` and `mode`: the only CHECK on `PROGRAM_CIRCUITS` is `CK_PROGRAM_CIRCUITS_REST_AFTER_SECONDS` (`20261004181109_Lot11_2_ProgramTreeFields.cs:81-83`). The only jsonb CHECK is a shape check on `SESSION_TEMPLATE_CONTENT` ("array with at least one element", constraint name not verified) (`20261006065717_Lot11_5_SessionTemplates.cs:64`), which does not look at `mode`. No CHECK on `PROGRAM_TEMPLATE_BODY`.

### 5.3 String literals "STANDARD" / "INTERVAL" and branches on the mode

Circuit mode usages (code):
- `ProgramCircuitRules.cs:39, 40, 43` (mode check), `:80, :88` (exercise branches). These are the ONLY branches on the circuit mode in the code base.
- `ProgramTemplateSeeder.cs:118, 136, 154, 164, 189, 216, 232` (`Mode = "STANDARD"`) and `:199` (`Mode = "INTERVAL"`, Rounds 8, helper `Interval(...)` at line 328-334).
- Comments: `ProgramCircuit.cs:13`, `ProgramTemplateCircuit.cs:4, 14, 17, 22`, `ProgramTemplateExercise.cs:11, 22`, `UpdateProgramCircuit.cs:13, 16, 21`.

Not a circuit mode (session nature STANDARD / EVALUATION, must NOT be touched):
- `Mentora.Core/Entities/ProgramSession.cs:22`, `Mentora.Core/Enums/ProgramSessionWire.cs:38` (`ProgramSessionNatures.Standard`), `ProgramSessionConfiguration.cs:57` (default), `20261004181109_Lot11_2_ProgramTreeFields.cs:20, 68`, `20261006065717_Lot11_5_SessionTemplates.cs:54`, `MentoraDbContextModelSnapshot.cs:2379`, `ProgramService.cs:373, 838`, `CoachSessionTemplatesController.cs:24`.
- In jsonb, the session nature key is `"nature"` (program template sessions) and the session template nature is a table column, so a value rewrite targeting the key `"mode"` cannot hit a nature.

No branch on the mode in: completion (`ProgramService.cs:662-732`), last performed (`ProgramService.cs:1052-1095`), visio, agenda, linking (`SessionService.cs:626-670`), response building. No CSV-seeded template exists (the seeding CSV files are exercises, muscles, equipment, references and quiz only). Docs and review scripts (`docs/review/...`, `run-vps-tests.ps1:529, 629, 1286`) use STANDARD in fixtures; they are evidence, not runtime code.

### 5.4 Exercise and circuit numeric fields

| Field (wire) | Stored as | Validated | Written | Returned |
|---|---|---|---|---|
| `rounds` (circuit) | `PROGRAM_CIRCUIT_ROUNDS` int null (`ProgramCircuitConfiguration.cs:41-42`); jsonb | required for INTERVAL only (`ProgramCircuitRules.cs:45-46`), no range | `ProgramService.cs:397, 855` | `ProgramService.cs:1042`; jsonb as is |
| `restBetweenRoundsSeconds` | `PROGRAM_CIRCUIT_REST_BETWEEN_ROUNDS_SECONDS` (`:44-45`) | none | `ProgramService.cs:398, 856` | `ProgramService.cs:1042` |
| `restSecondsAfterExercices` | `PROGRAM_CIRCUIT_REST_AFTER_SECONDS` + CHECK 0..600 (`:47-50`) | 0..600 (`ProgramCircuitRules.cs:48-50`) | `ProgramService.cs:399, 857` | `ProgramService.cs:1042` (response record `ProgramCircuitResponse.cs:10`) |
| `prescribedSets`, `prescribedReps` | `PROGRAM_EXERCISE_PRESCRIBED_SETS/REPS` (`ProgramExerciseConfiguration.cs:45-49`) | required STANDARD, forbidden INTERVAL (`ProgramCircuitRules.cs:82-83, 93-94`) | `ProgramService.cs:416-417, 872-873` | `ProgramService.cs:1016-1017`; `ProgramExerciseResponse.cs:24-25` |
| `restSecondsAfterSeries` | `PROGRAM_EXERCISE_REST_SECONDS` (`ProgramExerciseConfiguration.cs:55-56`) | none | `ProgramService.cs:419, 875` | `ProgramService.cs:1019`; `ProgramExerciseResponse.cs:27` |
| `workSeconds`, `restWorkSeconds` | `PROGRAM_EXERCISE_WORK_SECONDS` / `REST_WORK_SECONDS` (`ProgramExerciseConfiguration.cs:58-62`) | required INTERVAL, forbidden STANDARD (`ProgramCircuitRules.cs:85-86, 90-91`) | `ProgramService.cs:420-421, 876-877` | `ProgramService.cs:1020-1021`; `ProgramExerciseResponse.cs:28-29` |

DTO declarations: `ProgramTemplateExercise.cs:12-13, 20, 23-24`; `UpdateProgramExercise.cs:20-21, 26, 29-30` (copied at 39-44); `ProgramTemplateCircuit.cs:18, 20, 23`; `UpdateProgramCircuit.cs:17, 19, 22`.

Note on names: the wire names were renamed in Lot 11.7 inside the jsonb only (`20261006092320_Lot11_7_RestFieldsJsonKeys.cs:9-11`); the columns keep their old names. Already-returned fields `workSeconds` and `restWorkSeconds` therefore exist on every response path; for AMRAP, EMOM, FOR_TIME and TEMPS_CONTINU the only change needed is to stop forbidding them in the validator.

### 5.5 Storage of existing STANDARD data and read-only count SQL

Storage locations:
1. Table column `"PROGRAM_CIRCUITS"."PROGRAM_CIRCUIT_MODE"` (assigned programs, training-programs).
2. jsonb `"PROGRAM_TEMPLATES"."PROGRAM_TEMPLATE_BODY"`, key path `blocks[*](.blocks[*])*.sessions[*].circuits[*].mode` (recursive, depth up to 3). Platform rows have `"PROGRAM_TEMPLATE_COACH_ID"` NULL (`ProgramTemplateConfiguration.cs:22`).
3. jsonb `"SESSION_TEMPLATES"."SESSION_TEMPLATE_CONTENT"`, key path `[*].mode` (bare array of circuits, `SessionTemplate.cs:31-35`). Coach column name is `"COACH_ID"` (`SessionTemplateConfiguration.cs:19-20`), NULL for platform templates.

The jsonb text form of PostgreSQL is canonical (`"key": value`). The regex below matches `"mode": "<value>"` only as a JSON key/value pair: inside a JSON string (a name or a note) the quotes would be escaped, so free text cannot match. `"mode"` is the circuit key only (checked against the body DTOs above).

To run once connected to mentora_prod, then once connected to mentora_db (same script, counts only):

```sql
BEGIN READ ONLY;

SELECT 'PROGRAM_CIRCUITS' AS "location",
       c."PROGRAM_CIRCUIT_MODE" AS "mode",
       count(*) AS "circuits"
FROM "PROGRAM_CIRCUITS" c
GROUP BY c."PROGRAM_CIRCUIT_MODE"

UNION ALL

SELECT 'PROGRAM_TEMPLATES.PROGRAM_TEMPLATE_BODY '
         || CASE WHEN t."PROGRAM_TEMPLATE_COACH_ID" IS NULL THEN 'platform' ELSE 'coach' END,
       m.arr[1],
       count(*)
FROM "PROGRAM_TEMPLATES" t
CROSS JOIN LATERAL regexp_matches(t."PROGRAM_TEMPLATE_BODY"::text, '"mode": "([^"]*)"', 'g') AS m(arr)
GROUP BY 1, 2

UNION ALL

SELECT 'SESSION_TEMPLATES.SESSION_TEMPLATE_CONTENT '
         || CASE WHEN s."COACH_ID" IS NULL THEN 'platform' ELSE 'coach' END,
       m.arr[1],
       count(*)
FROM "SESSION_TEMPLATES" s
CROSS JOIN LATERAL regexp_matches(s."SESSION_TEMPLATE_CONTENT"::text, '"mode": "([^"]*)"', 'g') AS m(arr)
GROUP BY 1, 2

ORDER BY 1, 2;

ROLLBACK;
```

Optional cross-check (same session, still read only): number of circuits in each jsonb, to confirm every circuit carries a `mode` key:

```sql
BEGIN READ ONLY;
SELECT 'SESSION_TEMPLATES' AS "location", sum(jsonb_array_length(s."SESSION_TEMPLATE_CONTENT")) AS "circuits"
FROM "SESSION_TEMPLATES" s;
ROLLBACK;
```

(The program template body has no flat circuit array, so its cross-check would be a recursive query; the regex count is enough for a go/no-go.)

### 5.6 Options and recommendations

(a) Per-type validation table, keeping the 422 key format

Keep `ProgramCircuitRules` as the single source, replace the two booleans by a static table `Mode -> (Rounds, SetsReps, WorkRest)` with `Required | Allowed | Forbidden`. Keys stay `{path}[{Position}].Mode`, `{path}[{Position}].Rounds` and `{path}[p].Exercises[q]`; the "Mode must be one of" message lists the 12 values.

Proposed table (recommendation; SERIE_SIMPLE keeps today's STANDARD rules, INTERVAL unchanged; BISET, TRISET, GEANTE are the rep-based multi-exercise cases already described for STANDARD at `ProgramTemplateCircuit.cs:4-6`; the others follow "the API stores and returns" and forbid nothing):

| Mode | Rounds | Sets + reps (per exercise) | Work + restWork seconds (per exercise) |
|---|---|---|---|
| SERIE_SIMPLE | allowed | required | forbidden |
| BISET | allowed | required | forbidden |
| TRISET | allowed | required | forbidden |
| GEANTE | allowed | required | forbidden |
| CIRCUIT | allowed | allowed | allowed |
| AMRAP | allowed | allowed | allowed |
| EMOM | allowed | allowed | allowed |
| FOR_TIME | allowed | allowed | allowed |
| INTERVAL | required | forbidden | required |
| TEMPS_CONTINU | allowed | allowed | allowed |
| DISTANCE | allowed | allowed | allowed |
| LIBRE | allowed | allowed | allowed |

Points to confirm with PJ (not verified): exercise count rules (BISET exactly 2, TRISET exactly 3, GEANTE 4 or more?): recommendation is NO count rule in V1, the app builds the blocks. DISTANCE: there is no distance field (`ProgramTemplateExercise.cs` has none); either the app uses `customNote` / circuit `note`, or a field must be added (new column + jsonb key, out of this estimate).

(b) Legacy STANDARD
- Map on read only: four read paths to patch (`ProgramService.cs:1041`, `ProgramTemplateService.cs:174`, `SessionTemplateService.cs:280`, plus the assign copy `ProgramService.cs:854`), and STANDARD lives on in the data forever. Not recommended.
- Rewrite once (data-only migration, same pattern as `20261006092320_Lot11_7_RestFieldsJsonKeys.cs`): `UPDATE "PROGRAM_CIRCUITS" SET "PROGRAM_CIRCUIT_MODE" = 'SERIE_SIMPLE' WHERE ... = 'STANDARD'`, and a text replace of `"mode": "STANDARD"` by `"mode": "SERIE_SIMPLE"` in both jsonb columns, only on rows that contain it. Down does the reverse. Recommended.
- Accept both on write: keep accepting STANDARD in requests and normalize it to SERIE_SIMPLE before validation and storage, so app builds already installed keep working. Recommended in addition to the rewrite.
- Seeder: change the literals to SERIE_SIMPLE (`ProgramTemplateSeeder.cs:118...232`). The seeder never rewrites existing rows (`:41-49`), so the migration remains necessary.

(c) CHECK constraint
Recommended: `CK_PROGRAM_CIRCUITS_MODE` on `PROGRAM_CIRCUIT_MODE IN (12 values)`, raw SQL named per project convention, added in the same migration AFTER the rewrite (so it cannot fail on STANDARD rows; a precheck count is still listed in the runbook). Length 20 is enough (longest value TEMPS_CONTINU, 13). No CHECK inside jsonb (consistent with the rest of the project: jsonb is validated by the application).

(d) Contract change for the apps
- After the migration every response (training-programs, program-templates, session-templates) returns `SERIE_SIMPLE` for former STANDARD rows; STANDARD is never returned again.
- Requests: the 12 values are accepted; STANDARD is still accepted and normalized (documented as deprecated in the XML doc of `Mode`).
- `workSeconds` / `restWorkSeconds` are accepted and returned for every mode except the four rep-based ones (SERIE_SIMPLE, BISET, TRISET, GEANTE), not only INTERVAL.
- Risk for PJ: an already-installed app build that only knows STANDARD / INTERVAL will receive SERIE_SIMPLE (not verified how the old build reacts). To confirm before deploying to api-demo.

Estimate for the recommended path: rules table and messages 2 h, STANDARD normalization on write 0.5 h, migration (rewrite + CHECK + Down) and seeder 1.5 h, XML docs / Swagger 0.5 h, local and VPS runtime evidence (422 matrix per mode, counts before / after) 3 h. Total about 7.5 h.

## Part B: push notifications

### 6.1 Devices

- Entity `UserDevice` (`Mentora.Core/Entities/UserDevice.cs:11-21`): id, token, platform, created, last seen, user. The class comment at line 6-9 says the re-assignment is "not implemented at this schema-only step": stale, it is implemented (below).
- Configuration (`Mentora.Infrastructure/Persistence/Configurations/UserDeviceConfiguration.cs`): table `USER_DEVICES`, token varchar(512) required (line 20-23), UNIQUE index on the token (line 25, migration `20260730194501_AddLot5Parameters.cs:199-200`), index on `USER_ID` (migration line 205-206), platform stored as `IOS` / `ANDROID` (line 27-37, 57-69), FK to USERS with cascade delete (line 51-54).
- `UserDeviceService.RegisterAsync` (`Mentora.Infrastructure/Services/UserDeviceService.cs:16-65`): insert when unseen; on a unique violation race (23505) falls back to the existing row (line 40-46); same user: refresh last seen (line 49-53); another user: the row MOVES to the caller (line 54-62). This matches the contract ("a token re-registered by another user must move to the new user").
- `UserDeviceService.DeleteAsync` (line 67-74): deletes only when the token belongs to the caller.
- Routes (`Mentora.API/Controllers/Auth/UserDevicesController.cs`): `POST /api/v1/devices` (line 24-30) and `DELETE /api/v1/devices` with body (line 37-43), both `[Authorize]`, any role. Validator: token not empty, max 512 (`Mentora.Core/Validators/Device/RegisterDeviceRequestValidator.cs:10-12`). Platform is an enum bound with `JsonStringEnumConverter(SnakeCaseUpper)` (`Mentora.API/Program.cs:465`); behaviour on an unknown platform value: not verified.
- Logout: `AuthService.LogoutAsync` deletes the device token of the caller when `deviceToken` is supplied (`Mentora.Infrastructure/Services/AuthService.cs:289-294`), same SaveChanges as the refresh token revocation (line 296).

Missing for push:
- A server-side delete by token regardless of user (FCM cleanup on UNREGISTERED / INVALID_ARGUMENT): today only the owner-scoped delete exists.
- Last error / failure count / last success date per device (for diagnosis and for not hammering dead tokens).
- Language per device: none. Language exists per profile (`MemberParameter.cs:6`, `CoachParameter.cs:10`, default "FR"); with French-only texts in the Beta this is not blocking.
- Role per device: a user with both profiles ("BOTH") has one set of tokens; the app chooses the screen from its current role (contract), so a coach-side notification can reach a device logged in as member. Rare case, to note only.
- App version / build: none (not needed for Beta).

### 6.2 Notification preferences

Member (`Mentora.Core/Entities/MemberParameter.cs`): `MemberParameterNotifMessages` (true, line 7), `MemberParameterNotifSessionReminders` (true, line 8), `MemberParameterNotifMarketing` (false, line 9), `MemberParameterSessionReminderHoursBefore` (24, line 10). Columns in `MemberParameterConfiguration.cs:24-37`. Read and written by `MemberParameterService` (`GetAsync` creates the row with defaults when missing, line 15-31; `UpdateAsync` line 34-64, writes at 62-64; DTO at 99-101).
Important: members can have NO `MEMBER_PARAMETERS` row (get-or-create, comment line 44-45); the sender must treat a missing row as the C# defaults (all true except marketing, 24 h).

Coach (`Mentora.Core/Entities/CoachParameter.cs`): `CoachParameterNotifMessages` (line 11), `CoachParameterNotifNewBooking` (line 12), `CoachParameterNotifBookingCancelled` (line 13), `CoachParameterNotifMarketing` (line 14). Columns `CoachParameterConfiguration.cs:37-55`. Written by `CoachParameterService.cs:70-73`, returned at 116-119. Every coach has a row (`CoachParameterSeeder`, CLAUDE.md).

Mapping gaps: no member preference for SESSION_BOOKED / SESSION_CANCELLED; no coach preference for SESSION_REMINDER or MEMBER_JOINED; no reminder lead time for coaches.

### 6.3 Hook points for each Beta event

Common facts: `Member.UserId` (`Member.cs:19`) and `Coach.UserId` (`Coach.cs:17`) resolve the recipient USER_ID; tokens via `USER_DEVICES.USER_ID`. Display names: `MemberFirstName` / `MemberLastName` (`Member.cs:8-9`), `CoachFirstName` / `CoachLastName` (`Coach.cs:6-7`). All commit points below are inside an explicit transaction; notifications must be produced only after `CommitAsync` (or written as outbox rows in the same SaveChanges, see 6.8).

CHAT_MESSAGE
| Direction / path | Commit point | Notes |
|---|---|---|
| REST text, member or coach | `ConversationService.SendMessageAsync`, tx `ConversationService.cs:123`, `SaveChangesAsync` 144, `CommitAsync` 145 | called by `MemberConversationMessagesController.cs:61` and `CoachConversationMessagesController.cs:61` |
| REST voice (lot 13) | `ConversationService.SendAudioMessageAsync`, tx 215, save 221, commit 222 (`CancellationToken.None`) | called by `MemberConversationMessagesController.cs:106`, `CoachConversationMessagesController.cs:106`; the file is committed before the row (191) |
| SignalR | `ChatHub.SendMessageAsync` (`Mentora.API/Hubs/ChatHub.cs:49-60`) calls the same service method (line 55-56) | |

One hook in `ConversationService` (after line 145 and after line 222) covers the three paths. Recipient: the other party of `(memberId, coachId)` (sender type is a parameter, line 116 / 162). Data: `memberId` (to coach) or `coachId` (to member); `conversationId` and `messageId` are also available. Title: sender's first and last name (one extra query; the service does not load names today). Body: text content (truncate) or a fixed "Message vocal" label for audio. Preference: `...NotifMessages` of the recipient. No presence tracking exists, so the push is sent even if the recipient has the chat open (the app can drop it in foreground).

SESSION_BOOKED
| Direction | Commit point | Ids / names | Preference |
|---|---|---|---|
| To coach, member books an individual slot | `SessionService.ReserveAsync`, Serializable tx `SessionService.cs:28-29`, save 126, commit 127; a 40001 conflict is turned into 409 at 135-142 (no notification then) | `sessionId`, `memberId`; coach from `voucher.CoachId` (106); member name needs a query (not loaded) | `CoachParameterNotifNewBooking` |
| To member, coach enrolls in a group session | `SessionService.RegisterParticipantAsync`, Serializable tx 499, save 577, commit 578 | `sessionId`; member from `request.MemberId`; coach name needs a query | none on the member side |
| To member, coach enrolls in an individual session | no such route exists today (individual sessions are only created by the member, `SessionService.cs:41-49`; coach routes in `CoachGroupSessionsController.cs:28, 68, 89`, `CoachSessionsController.cs:90`) | not applicable | not applicable |

Group session creation (`SessionService.cs:383-462`, no participant) is not a booking event.

SESSION_CANCELLED
| Direction | Commit point | Ids | Preference |
|---|---|---|---|
| To coach, member cancels | `SessionService.CancelByMemberAsync`, tx 150, save 198, commit 199 | `sessionId`, `memberId`; reason available (193) | `CoachParameterNotifBookingCancelled` |
| To member, coach cancels an individual session | `SessionService.CancelByCoachAsync`, tx 263, save 307, commit 308 | `sessionId`; member from `session.SessionMemberId` | none on the member side |
| To members, coach cancels a whole group session | not implemented: the route refuses group sessions with `InvalidOperationException` (400) at `SessionService.cs:276-280` | not applicable | not applicable |
| To member, coach removes them from a group session | `SessionService.UnregisterParticipantAsync`, tx 597, save 611, commit 612 | `sessionId`, member from the route | recommendation: send SESSION_CANCELLED to that member |

SESSION_REMINDER
- Start time: `SESSIONS.SESSION_SCHEDULED_AT`, `timestamp with time zone` (`20260506160309_AddLot2_4_Sessions.cs:27`), copied from the slot start (`SessionService.cs:111, 447`), written from UTC `DateTime`.
- Eligible: stored `SessionStatus.Scheduled` AND start in the future. The stored status is never moved to COMPLETED (`SessionService.cs:674-683`, "deferred to a future cron"), so the scan must filter on the time window, never on the status alone. Cancelled sessions are excluded by the status.
- Recipients: members via the view `V_SESSION_MEMBERS` (individual member plus non-cancelled group participants, `20260812095011_Lot6_4_GroupSessions.cs:132-138`, used as `db.SessionMembers`), and the coach (`SESSION_COACH_ID`).
- Lead time: member `MemberParameterSessionReminderHoursBefore` (default 24) and `MemberParameterNotifSessionReminders`; coach: no parameter (a fixed lead time from options is proposed).
- Scheduler: none exists (see 6.4).

MEMBER_JOINED
- Commit point: `AuthService.VerifyOtpAsync`, tx `AuthService.cs:151`, `MemberHasActivated = true` and `MemberActivationDate` at 163-167 (member login only), save 170, commit 171. The flag is only set once, so the event fires once per member.
- Recipients: coaches linked in `MEMBER_COACHES` at that time. A member created by a coach has exactly one primary link (`CoachMemberInviteService.cs:81-88`).
- Data: `memberId`; name from `user.Member` (already loaded, `AuthService.cs:109`).
- Caveats: an existing member attached later to another coach (`CoachMemberInviteService.cs:103-120`) is usually already activated, so that coach gets no MEMBER_JOINED; how members created before lot 14.1 were backfilled for `MemberHasActivated` is not verified.

### 6.4 Background work

- One hosted service: `RefreshTokenPurgeService : BackgroundService` (`Mentora.Infrastructure/Services/RefreshTokenPurgeService.cs:22-97`), registered at `Mentora.API/Program.cs:419`. Pattern: initial delay from options, loop with `Task.Delay`, new DI scope per pass (line 60-61).
- Note: `RunOnceAsync` is not wrapped in a try/catch inside the loop (line 43-55). An exception would end `ExecuteAsync`; with the .NET 8 default (`BackgroundServiceExceptionBehavior.StopHost`) that stops the host (not verified at runtime). The new services must catch per iteration.
- No `IHostedService` other than this one, no `PeriodicTimer`, no `System.Threading.Timer`, no `Channel<T>`, no queue, no Quartz or Hangfire, no `Task.Run` (grep on all `*.cs`).

### 6.5 Outbound HTTP pattern and signing

- `MuxClient` (`Mentora.Infrastructure/Services/Mux/MuxClient.cs`): typed HttpClient, no SDK (line 12), registered with base address and 15 s timeout (`Program.cs:278-282`). Per-request auth header (line 111-118). Timeouts and network errors mapped to `MuxUnavailableException` (line 132-141), non-2xx too (156-161), JSON parse errors too (168-178). Logs carry operation, status and provider request id only, never URL, body or credential (line 31-36).
- Error mapping in `GlobalExceptionMiddleware`: `MuxUnavailableException` -> 502 `MUX_UNAVAILABLE` (`Mentora.API/Middleware/GlobalExceptionMiddleware.cs:57-62`), `MuxNotConfiguredException` -> 503 (63-67), `AudioNotConfiguredException` -> 503 (68-72). For push, no HTTP status is exposed to callers (best effort, background), so only the logging part of this pattern applies.
- Options pattern: `AddOptions<T>().Bind(...).ValidateOnStart()` + `IValidateOptions<T>` (`Program.cs:274-277` MUX, 289-292 audio, 397-398 auth, 404-405 invite, 425-430 email); `.Value` read before migrations, then one startup line with booleans / counts only: `[EMAIL]` 519, `[INVITE]` 528, `[AUTH]` 536, `[CORS]` 546, `[MUX]` 559, `[AUDIO]` 573. `MuxOptions.Validate` (`Mentora.Core/Options/MuxOptions.cs:60-98`): Development allows "all empty" (feature disabled) or "all valid", outside Development all required; messages never contain a value. `StartupGuards.VerifyOutsideDevelopment` (`Mentora.API/Startup/StartupGuards.cs:15-39`) reads env variables directly.
- RS256 JWT for the Google OAuth2 service-account flow: possible with what is referenced. `System.IdentityModel.Tokens.Jwt` 8.22.0 (`Mentora.Infrastructure/Mentora.Infrastructure.csproj:17`) brings `Microsoft.IdentityModel.Tokens` (`RsaSecurityKey`, `SigningCredentials` with `SecurityAlgorithms.RsaSha256`); the .NET 8 BCL provides `RSA.ImportFromPem` for the PKCS#8 private key of the JSON file. No package to add.
- Google or Firebase packages referenced: none (all `PackageReference` lines: `Mentora.API.csproj:9-20`, `Mentora.Core.csproj:12`, `Mentora.Infrastructure.csproj:12-17`).

### 6.6 Existing notification routes or tables

None. No `notifications` route, no `NOTIFICATION*` table, no FCM / Firebase string in `*.cs`, `*.csproj`, `*.yml` or the Dockerfile. `POST /api/v1/notifications/{notificationId}/click` does not exist (consistent with the production 404 reported by PJ).

### 6.7 Configuration names and key file protection

Proposed option names (environment variables only, same rule as email and MUX; no value in any file or doc):
- `Push__Provider`: `Fcm` or `Logging` (same idea as `Email__Provider`; `Logging` writes the would-be message without token to the log).
- `Push__Fcm__ProjectId`.
- `Push__Fcm__ServiceAccountPath`: absolute path of the service-account JSON, mounted read-only from the VPS host into the container (for example a directory outside the repo and outside the image). Recommended.
- `Push__Fcm__ServiceAccountJson`: alternative, the JSON content in one variable (only if a mount is not possible). Exactly one of the two must be set.
- `Push__Fcm__ApiBaseUrl` and `Push__Fcm__TokenUri`: defaults to the Google endpoints, overridable for tests.
- `Push__DispatchIntervalSeconds`, `Push__MaxAttempts`, `Push__ReminderScanIntervalSeconds`, `Push__CoachReminderHoursBefore`.
Validator: when `Fcm`, the file must exist and parse, carry `type` = service_account, `project_id` equal to `Push__Fcm__ProjectId`, `client_email` and `private_key`; messages never print a value or a path.

Key file protection, current state:
- `.gitignore` ignores `.env`, `appsettings.Development.json`, `appsettings.Local.json`, `*.pfx`, review tokens (`.gitignore:7, 487, 490, 245, 502-504`). Nothing matches a service-account JSON.
- `.dockerignore` excludes `appsettings.Development.json`, bin / obj, docs, `.git` (`.dockerignore:12-35`). Nothing matches a service-account JSON.
- `Mentora.API/Dockerfile:15` copies the whole context into the build stage, and the final image takes the publish output (line 24). The Web SDK publishes `*.json` files of the API project folder as content, so a key file dropped into `Mentora.API/` would end up in the image.
- Recommendation (in the implementation sub-lot): add to both `.gitignore` and `.dockerignore` patterns such as `**/*service-account*.json`, `**/*firebase*.json`, `**/secrets/`, and keep the key file outside the repo on the VPS (mounted volume).

### 6.8 Minimal Beta design proposal

1. Table `NOTIFICATIONS` (one migration): id, `USER_ID`, type, title, body, data (jsonb, string values only), status (`PENDING`, `SENT`, `FAILED`, `SKIPPED`), attempts, dedup key (UNIQUE, nullable), created / sent / clicked dates. It is the outbox (survives restarts), the idempotence store for reminders, and the target of the click route.
2. Producing: services add the notification rows in the SAME SaveChanges as the business change (transactional outbox: no row without commit, no commit without row), resolving the recipient USER_ID and applying preferences at write time (`SKIPPED` rows are not written, to keep the table small). For chat, one row per message to the other party. No new transaction, no lock change in the existing Serializable flows.
3. Dispatcher: one `BackgroundService` polling `PENDING` rows every few seconds (plus an in-memory signal for immediate wake-up after a commit), each row sent to every token of the user, `try/catch` per iteration (see 6.4). `FOR UPDATE SKIP LOCKED` is not needed with a single API container (not verified that only one replica runs on the VPS).
4. FCM HTTP v1 client without SDK, typed HttpClient like `MuxClient`: OAuth2 JWT bearer (RS256 with existing packages, scope firebase.messaging), access token cached until a margin before expiry; message with `notification.title` / `body`, `data` (strings only, empty fields omitted, `type` and `notificationId` always present), `android.priority` HIGH with default sound, `apns` default sound.
5. Token cleanup: delete the device row on 404 UNREGISTERED; on 400 INVALID_ARGUMENT delete only when the error concerns the token (an INVALID_ARGUMENT caused by our own payload would otherwise wipe every token; to confirm in the FCM error details at implementation).
6. Preferences: chat -> `NotifMessages` (both sides); coach booking / cancellation -> `CoachParameterNotifNewBooking` / `NotifBookingCancelled`; member booking / cancellation -> always sent (transactional, no preference exists); reminders -> member `NotifSessionReminders` and its own lead time, coach fixed lead time from options; MEMBER_JOINED -> always.
7. Reminder scheduler: second `BackgroundService` (or a step of the dispatcher) every 5 minutes: sessions with stored status SCHEDULED whose start is inside `[now, now + lead time]`, one row per recipient with dedup key `SESSION_REMINDER:{sessionId}:{userId}` (the UNIQUE index makes it idempotent across restarts and overlapping scans). A session booked less than the lead time before its start gets its reminder at the next scan (acceptable).
8. MEMBER_JOINED: outbox row(s) in `AuthService.VerifyOtpAsync` before the save at line 170, for the coaches linked at that time.
9. Click route: yes. `POST /api/v1/notifications/{notificationId}/click`, authenticated, empty body, sets the clicked date only when the row belongs to the caller, always 200 (the app ignores errors anyway). About 1 h with the table already in place.
10. Logging: `[PUSH]` startup line (`provider`, `configured`, `projectIdSet`), per send: notification id, type, user id, device count, FCM status and error code. Never a token, a key, a message body or an access token.
11. Not configured: Development allows `Push__Provider` empty or `Logging` (rows are written and marked SENT by the logging provider, or stay disabled); outside Development the API refuses to start unless `Fcm` is fully configured or `Logging` is set explicitly (same spirit as MUX and email).
12. Runtime proof: first the Firebase console "send test message" to PJ's Android FCM token (proves the device side), then the API path: PJ registers through `POST /api/v1/devices`, a chat message and a booking on api-demo produce `[PUSH]` lines with status 200 and the notification on the device; the click route is proven by the app call or a manual call; UNREGISTERED proven by uninstalling the app and sending again (row deleted).

## Decisions for François

1. Validation of the 12 modes: adopt the table of 5.6 (a): SERIE_SIMPLE, BISET, TRISET, GEANTE keep today's STANDARD rules, INTERVAL unchanged, the 7 others permissive; no exercise-count rule. Recommendation: yes, and ask PJ how DISTANCE carries the distance (no field today).
2. Legacy STANDARD: one data-only migration rewriting the column and both jsonb to SERIE_SIMPLE, plus STANDARD still accepted on write and normalized. Recommendation: yes, after running the 5.5 counts on mentora_prod and mentora_db.
3. CHECK `CK_PROGRAM_CIRCUITS_MODE` on the 12 values, in the same migration after the rewrite. Recommendation: yes (column only, not jsonb).
4. Push architecture: `NOTIFICATIONS` outbox table + dispatcher `BackgroundService` + SDK-free FCM client, key file mounted read-only on the VPS (`Push__Fcm__ServiceAccountPath`). Recommendation: yes, rather than an in-memory queue (lost on restart, no reminder idempotence).
5. Scope details: click route included; reminders use the member's own lead time (default 24 h) and a fixed coach lead time; coach removal of a group participant sends SESSION_CANCELLED to that member; member booking / cancellation pushes are always sent (no member preference). Recommendation: yes to all four.

## Estimates

| Item | Hours |
|---|---|
| Part A: rules table, STANDARD normalization, migration (rewrite + CHECK), seeder, docs | 4.5 |
| Part A: runtime evidence (422 matrix per mode, counts before and after, local and VPS) | 3 |
| Part A total | 7.5 |
| Part B: `NOTIFICATIONS` table, entity, configuration, migration | 1.5 |
| Part B: options, validator, startup line, Development behaviour, ignore rules | 1.5 |
| Part B: FCM HTTP v1 client (OAuth2 RS256, token cache, send, error mapping, token cleanup) | 4 |
| Part B: dispatcher `BackgroundService` (poll + signal, retries, preferences) | 3 |
| Part B: hooks (chat text and voice, booking, group registration, cancellations, unregistration, MEMBER_JOINED) | 3 |
| Part B: reminder scan with dedup key | 2 |
| Part B: click route | 1 |
| Part B: docs, Swagger, runtime proof with PJ (console test, real Android token, api-demo) | 3 |
| Part B total | 19 |
| Overall | 26.5 |
