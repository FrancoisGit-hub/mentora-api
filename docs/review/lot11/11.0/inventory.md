# Lot 11.0: read-only inventory

Date: 2026-10-04. Branch `feature/lot_11`, created from `origin/main` at `4301fdb` (no upstream).
Scope: read only. No code, config or migration change. Database: `SELECT` only, on the local
container `mentora-postgres-local` (port 5433). HTTP: two `POST /api/v1/auth/otp/request` calls
on a local API (see Q4.2).

## 0. Run log

| Step | Result |
|---|---|
| `git status` | clean, on `feature/lot_10` |
| `git fetch origin` then `git log --oneline -1 origin/main` | `4301fdb docs(lot10): 10.3 VPS evidence` |
| `git switch -c feature/lot_11 origin/main` then `git branch --unset-upstream` | done; `git log --oneline -1` = `4301fdb`, tree clean |
| `docker start mentora-postgres-local` | the container was stopped (exited 36 min earlier), started with approval |
| psql, one run of a file of 14 SELECTs inside `BEGIN TRANSACTION READ ONLY ... ROLLBACK` | output quoted below as S0 to S13 |
| Last applied migration (S0) | `20260930085059_Lot10_Quiz`, the latest file in `Mentora.Infrastructure/Persistence/Migrations`: nothing pending |
| API started for Q4.2, two calls, API stopped | see Q4.2; port 5243 free before and after |

Note: CLAUDE.md is out of date. It mentions Minimal APIs, while the code uses controllers. It also
says `MemberOnly` "requires `memberId` claim to be present". The code also requires
`userType = MEMBER`: `Mentora.API/Program.cs:185-187`:
`policy.RequireClaim("memberId").RequireClaim("userType", "MEMBER")`.

---

## Q1. Program tree storage and every path that carries it

### 1.1 Storage: confirmed

- Templates store the tree as jsonb text:
  - `Mentora.Core/Entities/ProgramTemplate.cs:18-21`: "Recursive block tree, cannot use OwnsOne/OwnsMany/ToJson ... Stored as a plain jsonb string; the service owns serialization via System.Text.Json." `public string ProgramTemplateBody`.
  - `Mentora.Infrastructure/Persistence/Configurations/ProgramTemplateConfiguration.cs:49-52`: `.HasColumnName("PROGRAM_TEMPLATE_BODY").HasColumnType("jsonb")`.
- Assigned programs are stored in relational tables: `PROGRAM_BLOCKS` (`ProgramBlockConfiguration.cs:11`), `PROGRAM_SESSIONS` (`ProgramSessionConfiguration.cs:13`), `PROGRAM_CIRCUITS` (`ProgramCircuitConfiguration.cs:11`) and `PROGRAM_EXERCISES` (`ProgramExerciseConfiguration.cs:11`). `ProgramService.CopyBlockTree` (`ProgramService.cs:505-582`) fills these tables from a `ProgramTemplateBody`.
- The same DTO family (`Mentora.Core/DTOs/ProgramTemplate/Body/*.cs`) is the request body for templates, for assignment without a template, and for the program PUT (`AssignProgramRequest.cs:23`, `UpdateProgramRequest.cs:11`, `ProgramTemplateRequest.cs:19`).

### 1.2 Write paths of the tree

| # | Route | Controller action | Service | Validation |
|---|---|---|---|---|
| W1 | `POST /api/v1/coach/program-templates` | `CoachProgramTemplatesController.Create` (`:67-80`) | `ProgramTemplateService.CreateAsync` (`:64-87`), serialized at `:77` `JsonSerializer.Serialize(request.Body, JsonOptions)` | `ProgramTemplateRequestValidator` (`:8-41`), which delegates the tree to `ProgramTemplateBodyValidator` (`:39`) |
| W2 | `PUT /api/v1/coach/program-templates/{programTemplateId}` | `Update` (`:89-102`) | `ProgramTemplateService.UpdateAsync` (`:89-111`), serialized at `:105` | same as W1, after the ownership lookup (`:95-97`) |
| W3a | `POST /api/v1/coach/members/{memberId}/training-programs` with `templateId` | `CoachProgramsController.Assign` (`:63-77`) | `ProgramService.AssignAsync` (`:55-157`): template body deserialized at `:80`, then `CopyBlockTree` at `:144` | **no tree validation**: the stored template body is copied as is |
| W3b | same route, with `body` instead of `templateId` | same | `AssignAsync` `:82-102` | inline checks (`:84-94`) + `ValidateBodyTreeAsync` (`:101`, `:492-501`), which runs `ProgramTemplateBodyValidator` |
| W4 | `PUT /api/v1/coach/training-programs/{programId}` | `CoachProgramsController.Update` (`:86-96`) | `ProgramService.UpdateAsync` (`:159-211`), full delete and reinsert (see Q5) | `UpdateProgramRequestValidator` (`:8-36`), which delegates to `ProgramTemplateBodyValidator` (`:34`) |
| W5 | startup | `Program.cs:369` | `ProgramTemplateSeeder.SeedAsync` (`:36-93`), serialized at `:80` | **none**: bodies are built in C# (`:97-325`) and not validated |

Other writes on tree rows (they do not change the shape of the tree):
`ProgramService.UpdateBookingAsync` (`:227-283`, `PROGRAM_SESSION_SESSION_ID`),
`ProgramService.ApplyCompletionAsync` (`:365-435`, status, dates, feedback, actuals),
`SessionService.LinkToProgramSessionAsync` (`:626-664`, `PROGRAM_SESSION_SESSION_ID`),
`ProgramService.DeleteAsync` (`:213-223`, archives the program only) and
`ProgramTemplateService.DeleteAsync` (`:113-123`, `IS_ACTIVE = false` only).

### 1.3 Read paths and response DTOs

| Route | Service | Response DTO carrying the tree |
|---|---|---|
| `GET /api/v1/coach/program-templates` | `ProgramTemplateService.ListForCoachAsync` (`:23-51`) | `ProgramTemplateHeaderResponse`, no body ("Headers only, the body column is never selected", `:46`) |
| `GET /api/v1/coach/program-templates/{id}` (and the POST/PUT responses) | `GetByIdForCoachAsync` (`:53-62`), `ToResponse` (`:167-178`) | `ProgramTemplateResponse.Body` of type `ProgramTemplateBody` (typed deserialization, `:174`) |
| `GET /api/v1/coach/members/{memberId}/training-programs` | `ProgramService.ListForCoachAsync` (`:28-44`) | `ProgramHeaderResponse`, no tree |
| `GET /api/v1/coach/training-programs/{programId}` | `GetByIdForCoachAsync` (`:46-53`) and `BuildResponseAsync` (`:586-622`) / `AssembleTree` (`:624-669`) | `ProgramResponse` → `ProgramBlockResponse` → `ProgramSessionResponse` → `ProgramCircuitResponse` → `ProgramExerciseResponse` |
| `POST .../training-programs` / `PUT /training-programs/{id}` | same assembly | `AssignProgramResponse(ProgramResponse, archivedProgramId)` / `ProgramResponse` |
| `GET /api/v1/member/programs/current` | `GetCurrentForMemberAsync` (`:312-319`) | `ProgramResponse` |
| `GET /api/v1/member/programs/{programId}` | `GetByIdForMemberAsync` (`:321-328`) | `ProgramResponse` |
| `PUT /api/v1/coach/program-sessions/{id}/completion` and `PUT /api/v1/member/program-sessions/{id}/completion` | `BuildSessionResponseAsync` (`:440-488`) | `ProgramSessionResponse` (one session subtree) |
| `PUT /api/v1/coach/program-sessions/{id}/booking` | `UpdateBookingAsync` (`:277-282`) | `ProgramSessionBookingResponse(ProgramSessionId, SessionId, Name, Type, Status)` |
| `GET /api/v1/coach/agenda`, `GET /api/v1/member/agenda` | `AgendaService` | `AgendaEntryResponse.Type`: the program session type for a standalone entry (`AgendaService.cs:268` `Type: ps.ProgramSessionType`), the offer type for a booking (`:195`). Also `ProgramSessionId` and `CompletedDate` |
| `GET/POST /api/v1/coach/sessions/{sessionId}/participants` | `SessionService.ListParticipantsAsync` (`:464-485`), `:797-803` | `SessionParticipantResponse.ProgramSessionId` |

### 1.4 Files to change for the three new fields

Every response DTO is a positional record, so every constructor call must change too.

**`loadPercent` on MESOCYCLE blocks (null, 100, 102.5, 105, 110)**
- Body DTO: `Mentora.Core/DTOs/ProgramTemplate/Body/ProgramTemplateBlock.cs` (one class covers templates, assignment and PUT).
- Validator: `Mentora.Core/Validators/ProgramTemplate/ProgramTemplateBodyValidator.cs`, `ValidateBlockList` (`:82-139`): value allowed only when `Level == "MESOCYCLE"`, from the list.
- Entity: `Mentora.Core/Entities/ProgramBlock.cs`; EF: `ProgramBlockConfiguration.cs` (column, numeric type such as `numeric(5,1)`, optional CHECK in raw SQL as done for `CK_PROGRAM_BLOCKS_*`); new migration and model snapshot.
- Mapping: `ProgramService.CopyBlockTree` (`:512-521`); `ProgramService.AssembleTree` (`:649-652`).
- Response: `Mentora.Core/DTOs/Program/ProgramBlockResponse.cs`.
- Templates: no migration needed (jsonb), the field travels with `ProgramTemplateBlock`. `ProgramTemplateSeeder` is optional.

**`nature` on sessions (STANDARD or EVALUATION)**
- Body DTO: `ProgramTemplateSession.cs`. Validator: `ValidateSessions` (`:157-179`). Decide whether a missing value means STANDARD.
- Entity: `ProgramSession.cs`; EF: `ProgramSessionConfiguration.cs`; migration with a default of `'STANDARD'` for existing rows.
- Mapping: `CopyBlockTree` (`:526-539`); `AssembleTree` (`:639-644`); `BuildSessionResponseAsync` (`:476-487`).
- Response: `ProgramSessionResponse.cs`. Optional: `AgendaEntryResponse.cs` + `AgendaService.cs:266-276`, and `ProgramSessionBookingResponse.cs` + `ProgramService.cs:277-282`, if the agenda or booking screens need it.

**`restAfterSeconds` on exercises**
- Body DTO: `ProgramTemplateExercise.cs`. Validator: `ValidateExercises` (`:223-263`).
- Entity: `ProgramExercise.cs`; EF: `ProgramExerciseConfiguration.cs`; migration.
- Mapping: `CopyBlockTree` (`:559-574`); `BuildExerciseResponses` (`:699-725`).
- Response: `ProgramExerciseResponse.cs`. Optional: seeder helper `Std(...)` (`ProgramTemplateSeeder.cs:306-315`).

Shared by all three: existing template bodies keep working because a missing key deserializes to
null. Path W3a copies a template without validating it, so the copy code must handle null.

### 1.5 Unknown JSON properties: silently ignored

- `Program.cs:321-328`: `AddJsonOptions` only adds `JsonStringEnumConverter(SnakeCaseUpper)`. No `UnmappedMemberHandling` setting and no `[JsonExtensionData]` anywhere: a grep for `UnmappedMember|JsonUnmapped|JsonExtensionData` over `*.cs` finds nothing. MVC therefore keeps the System.Text.Json default: unknown members are skipped and property names match case-insensitively (web defaults).
- Service options: `ProgramService.cs:21-24`, `ProgramTemplateService.cs:18-21` and `ProgramTemplateSeeder.cs:14-17` only set `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`. They set nothing about unmapped members.
- Consequence (deduced from the code, not tested over HTTP): a `loadPercent` sent by the app today is dropped during model binding, with no 400 or 422. Templates are re-serialized from the typed DTO (`ProgramTemplateService.cs:77`, `:105`), so the key never reaches the jsonb. Programs have no column for it.

### 1.6 SQL: templates with a `loadPercent` key (S1)

```sql
SELECT count(*) AS total_templates,
       count(*) FILTER (WHERE jsonb_path_exists("PROGRAM_TEMPLATE_BODY", 'strict $.**.loadPercent')
                           OR jsonb_path_exists("PROGRAM_TEMPLATE_BODY", 'strict $.**.LoadPercent')) AS with_load_percent_key
FROM "PROGRAM_TEMPLATES";
```
```
 total_templates | with_load_percent_key
               2 |                     0
```

**Impact on lot 11:** one DTO family carries the tree everywhere, so each new field is one
change in the body DTO, entity, configuration, copy code and response. Watch the unvalidated copy
path (W3a) and the unvalidated seeder (W5).

---

## Q2. Session type `A_DISTANCE`

### 2.1 Every occurrence

A repo-wide grep (code, migrations, docs) finds 8 lines in 7 files. There is none in `docs/`,
the Swagger filters or the snapshot.

| File:line | Excerpt |
|---|---|
| `Mentora.Core/Validators/ProgramTemplate/ProgramTemplateBodyValidator.cs:30` | `["PRESENTIEL_SOLO", "PRESENTIEL_GROUPE", "VISIO", "A_DISTANCE"];` |
| `Mentora.Infrastructure/Seeding/ProgramTemplateSeeder.cs:174` | `Name = "Séance A — Cardio et renforcement", Type = "A_DISTANCE", ...` |
| `Mentora.Infrastructure/Persistence/Configurations/ProgramSessionConfiguration.cs:83-84` | `// CHECK (TYPE <> 'A_DISTANCE' OR SESSION_ID IS NULL) ... an A_DISTANCE session never carries a booking.` |
| `Mentora.Infrastructure/Persistence/Migrations/20260812084549_Lot6_3_Programs.cs:283-284` | `ADD CONSTRAINT "CK_PROGRAM_SESSIONS_A_DISTANCE_HAS_NO_BOOKING" CHECK ("PROGRAM_SESSION_TYPE" <> 'A_DISTANCE' OR "PROGRAM_SESSION_SESSION_ID" IS NULL);` |
| `Mentora.Core/Entities/ProgramSession.cs:21` | `// PRESENTIEL_SOLO \| PRESENTIEL_GROUPE \| VISIO \| A_DISTANCE` |
| `Mentora.Core/DTOs/ProgramTemplate/Body/ProgramTemplateSession.cs:7` | `/// <summary>PRESENTIEL_SOLO \| PRESENTIEL_GROUPE \| VISIO \| A_DISTANCE.</summary>` |

### 2.2 What the code does differently

- **Booking link:** never possible. `OfferType` has no `A_DISTANCE` value (`EnumMappings.cs:15`: `["VISIO", "PRESENTIEL_SOLO", "PRESENTIEL_GROUPE", "VISIO_GROUPE"]`). The automatic link filters on `ps.ProgramSessionType == offerTypeWire` (`SessionService.cs:644`), and the manual link returns 409 "Session and program session types do not match." (`ProgramService.cs:249-253`) before the CHECK can fire. The CHECK (S13) is a database safety net.
- **Agenda:** no special case. It appears like any unlinked program session (`AgendaService.cs:248-276`), with `IsBooked = false`, `Type = "A_DISTANCE"` and `IsOverdue` when the date is past and it is still PLANNED.
- **Visio:** no relation. Eligibility only reads `SESSIONS.SESSION_OFFER_TYPE` (`VisioTokenService.cs:76`).
- **Completion:** no type check. Both the member and the coach can complete it (`ProgramService.cs:287-346`).
- **Meaning ("done by the member alone"):** not documented. The only statement is "an A_DISTANCE session never carries a booking" (`ProgramSessionConfiguration.cs:83-84`).

### 2.3 SQL

S2, `PROGRAM_SESSIONS` per type: **0 rows**. The local database has 0 programs (S5).
```sql
SELECT "PROGRAM_SESSION_TYPE", count(*), count(*) FILTER (WHERE "PROGRAM_SESSION_SESSION_ID" IS NOT NULL)
FROM "PROGRAM_SESSIONS" GROUP BY 1 ORDER BY 1;   -- (0 rows)
```
S3, templates containing each type:
```sql
SELECT x.type #>> '{}' AS session_type, count(DISTINCT t."PROGRAM_TEMPLATE_ID") AS templates, ...
FROM "PROGRAM_TEMPLATES" t, LATERAL jsonb_path_query(t."PROGRAM_TEMPLATE_BODY", 'strict $.**.sessions[*].type') AS x(type)
GROUP BY 1 ORDER BY 1;
```
```
  session_type   | templates | mentora_templates | active_templates
 A_DISTANCE      |         1 |                 1 |                1
 PRESENTIEL_SOLO |         2 |                 2 |                2
 VISIO           |         1 |                 1 |                1
```
S4: both local templates are Mentora templates (`COACH_ID` null), both active.

### 2.4 Impact if `A_DISTANCE` were renamed `AUTONOME` (not done)

- Code: `ProgramTemplateBodyValidator.cs:30`, `ProgramTemplateSeeder.cs:174`, the comments `ProgramSession.cs:21`, `ProgramTemplateSession.cs:7` and `ProgramSessionConfiguration.cs:83-84`.
- Constraint: new migration that drops `CK_PROGRAM_SESSIONS_A_DISTANCE_HAS_NO_BOOKING` and recreates it, for example as `CK_PROGRAM_SESSIONS_AUTONOME_HAS_NO_BOOKING` with `<> 'AUTONOME'`. The old migration must stay unchanged.
- Data, in the same migration:
  - `UPDATE "PROGRAM_SESSIONS" SET "PROGRAM_SESSION_TYPE" = 'AUTONOME' WHERE ... = 'A_DISTANCE'` (0 rows locally, VPS not measured).
  - Rewrite every jsonb body that holds `"type": "A_DISTANCE"`: 1 template locally, the Mentora "Remise en forme — débutant" template. The seeder looks templates up by name (`ProgramTemplateSeeder.cs:41-49`), so it never updates an existing body. Without the data migration, that template keeps `A_DISTANCE`.
  - Order: drop the CHECK, update the rows, recreate the CHECK.
- API contract for the mobile app: `type` changes in the template body requests and responses, `ProgramSessionResponse.Type`, `ProgramSessionBookingResponse.Type` and `AgendaEntryResponse.Type` for standalone entries. An app that still sends `A_DISTANCE` gets 422 from the validator ("Type must be one of: ..."), unless both values are accepted during a transition.

---

## Q3. Rest fields and how a session is performed

### 3.1 Meanings, as documented

| Field | XML comment | Validator | Docs |
|---|---|---|---|
| `PrescribedSets`, `PrescribedReps` | `ProgramTemplateExercise.cs:11` "STANDARD circuits only." (on `PrescribedSets`) | `ProgramTemplateBodyValidator.cs:248-249` required on every exercise of a STANDARD circuit; `:259-260` forbidden in INTERVAL | `docs/review/lot9/inventory.md:84-85` "sets / reps: present" |
| `RestSeconds` (exercise) | none (`ProgramTemplateExercise.cs:16`) | none: allowed in both modes, no range check | `lot9/inventory.md:91` "rest: present, `ProgramExerciseRestSeconds` (prescribed only, no actual-rest column)". Meaning: not documented |
| `WorkSeconds` (exercise) | `ProgramTemplateExercise.cs:18` "INTERVAL circuits only." | `:256-257` required in INTERVAL; `:251-252` forbidden in STANDARD | `lot9/inventory.md:93` "work duration: present" |
| `RestWorkSeconds` (exercise) | none (same block as `WorkSeconds`, `:20`) | same as `WorkSeconds` | `lot9/inventory.md:106` "its exact semantics ... were not further documented" |
| `Rounds` (circuit) | `ProgramTemplateCircuit.cs:11` "Required when Mode is INTERVAL." | `:216-217`; not forbidden in STANDARD | none |
| `RestBetweenRoundsSeconds` (circuit) | none (`ProgramTemplateCircuit.cs:14`) | none | none |

The only concrete example is the seeder, which is data, not documentation. INTERVAL:
`Rounds = 8, RestBetweenRoundsSeconds = 60, Note = "Format Tabata : 20 s d'effort / 10 s de repos."`
with `Interval(..., 20, 10)`, which is `WorkSeconds = 20, RestWorkSeconds = 10`
(`ProgramTemplateSeeder.cs:189-194`). STANDARD: `Std(..., sets, reps, loadType, restSeconds)`
with 45 to 120 s per exercise (`:111-114`, `:306-315`).
`docs/review/tri-ecarts-lot6.md:13-15` adds that the mode decides the mobile display:
"compteur de répétitions ou chronomètre".

### 3.2 Execution order in a STANDARD circuit with several exercises

Not documented. No comment, doc or validator says whether exercise 1 is done in full before
exercise 2, or whether the exercises alternate.

### 3.3 Is there a "rest after the last set of this exercise, before the next exercise" field?

**No.** The exercise has only `RestSeconds`, whose meaning is undocumented. Its values (45 to
120 s, next to sets and reps) look like rest between sets, but that is a reading, not a fact. No
other column or DTO property exists (entity `ProgramExercise.cs:18-23`).

**Impact on lot 11:** `restAfterSeconds` is a new field. Lot 11 should also write down what
`RestSeconds` means, otherwise the two will be confused.

---

## Q4. OTP request behaviour

### 4.1 Cases (code)

Code: `AuthController.RequestOtp` (`AuthController.cs:15-27`) catches only
`InvalidOperationException` and returns 400. Everything else goes to `GlobalExceptionMiddleware`.
`AuthService.RequestOtpAsync` (`AuthService.cs:29-67`) does the lookup and the role check
(`:32-38`) before any write.

| Case | Status | Body | `AUTH_OTPS` row | Email |
|---|---|---|---|---|
| unknown email | 400 | `{"success":false,"data":null,"error":"User not found or account is disabled.","statusCode":400}` (`:36`) | no | no |
| disabled user | 400 | same message: the query filters on `u.UserIsEnabled` (`:35`) | no | no |
| `isCoach: true` on a member-only account | 400 | `"No coach account for this email."` (`:116-117`) | no | no |
| `isCoach: false` on a coach-only account | 400 | `"No member account for this email."` (`:118-119`) | no | no |
| BOTH account without `isCoach` | **409** | `ConflictException` (`:128-129`) is not caught by the controller; the middleware (`GlobalExceptionMiddleware.cs:29-33`) returns `{"success":false,"data":null,"error":"This email has both a coach and a member account. Please specify isCoach explicitly.","statusCode":409}` and logs an error with the email (`:125-127`) | no | no |
| user with neither profile, no `isCoach` | 400 | `"No coach or member account for this email."` (`:137`) | no | no |
| valid request | 200 | `{"success":true,"data":null,"error":null,"statusCode":200}` | earlier unused, unexpired codes are marked used (`:41-45`), one new row (`:48-56`, expiry +10 min) | yes (`:60-66`); the send result is ignored (`_ =`) |

Email matching is exact and case sensitive: `u.UserEmail == email` (`:35`).

### 4.2 Real HTTP calls (local, 2026-10-04 17:11 to 17:13 UTC)

The local database has no coach-only account (S8: `BOTH` 1, `MEMBER_ONLY` 1, both enabled), so
the "`isCoach: false` on a coach-only account" case was **not called**. Its row above comes from
the code only.

To prove nothing was written, I took Postgres write counters
(`pg_stat_user_tables.n_tup_ins/upd/del`) and the `AUTH_OTPS` count at three points:

1. Before API start (A): `AUTH_OTPS` = 72.
2. After "Application started" (B). Delta A→B is **0 inserts, 0 updates, 0 deletes on all 43 tables**: the startup seeders are idempotent on this database. Seeder logs say "created 0" for every catalogue.
3. After the two calls (C). Delta B→C is **0 on all 43 tables**, `AUTH_OTPS` = 72.

| Call | Request | Status | Body |
|---|---|---|---|
| 1 | `{"email":"lot11-unknown@example.invalid"}` | 400 | `{"success":false,"data":null,"error":"User not found or account is disabled.","statusCode":400}` |
| 2 | `{"email":"<local member-only account>","isCoach":true}` | 400 | `{"success":false,"data":null,"error":"No coach account for this email.","statusCode":400}` |

The API log for the two calls shows only the `USERS` SELECTs. There is no OTP console line and
no email log line. The API process I started (`dotnet run`, PID 32420, child `Mentora.API.exe`
27944) was stopped, and port 5243 was free afterwards.

### 4.3 The OTP console line

`AuthService.cs:58`: `Console.WriteLine(...)` with the prefix `OTP` in square brackets, then the
email as typed in the request, an arrow, and the **plaintext 6-digit code**. It goes to stdout
(not through `ILogger`), only on the valid path, after the row is saved. When
`Email:Provider = Logging`, `LoggingEmailSender.cs:10-12` also logs `TextBody`, which contains
the code.

### 4.4 OTP email content and first name

- Subject: `AuthService.cs:60` `$"Votre code Mentora : {plainOtp}"`. The code is in the subject.
- HTML body: `BuildOtpHtmlBody` (`:324-340`): "Mentora", "Bonjour,", "Voici votre code de connexion :", the code, "Ce code expire dans 15 minutes.", "Si vous n'avez pas demandé ce code, ignorez ce message."
- Text body: `BuildOtpTextBody` (`:342-350`), same text.
- **The text says 15 minutes, but the row expires after 10 minutes** (`:52` `AddMinutes(10)`).
- First name: `RequestOtpAsync` already loads both profiles (`:32-35` `.Include(u => u.Coach).Include(u => u.Member)`), so no extra query is needed:
  - COACH: `user.Coach.CoachFirstName` → `COACHES.COACH_FIRST_NAME` (`CoachConfiguration.cs:18`, NOT NULL, max 100).
  - MEMBER: `user.Member.MemberFirstName` → `MEMBERS.MEMBER_FIRST_NAME` (`MemberConfiguration.cs:20`, NOT NULL, max 100).
  - BOTH: both exist. `ResolveEffectiveIsCoach` gives the role for this request, but its return value is discarded at `:38`. Keeping it would choose the matching profile.

**Impact on lot 11:** the greeting needs only the `ResolveEffectiveIsCoach` result and a builder
parameter. Also fix the 15 vs 10 minutes text. Locally the provider is Smtp with real credentials
(Q7.2), so any valid local request sends a real email.

---

## Q5. PUT training-programs and history

### 5.1 Full delete and reinsert: confirmed

`ProgramService.UpdateAsync` (`:176-208`): "Full replacement: delete the existing tree (children
first) and reinsert, in one transaction. Never partial." It loads every exercise, circuit,
session and block of the program and calls `RemoveRange` on each (`:180-188`). It then runs
`CopyBlockTree` with fresh ids and every session `PLANNED`, `SESSION_ID = null` (`:200`,
`:533`, `:538`). The controller says the same (`CoachProgramsController.cs:80-81`).

What the member loses:
- booking link `PROGRAM_SESSION_SESSION_ID`. The booking itself (SESSIONS row, voucher RESERVED, participant) is untouched, and the agenda then shows the booking without a `programSessionId`.
- status DONE/SKIPPED: everything is back to PLANNED.
- `PROGRAM_SESSION_COMPLETED_DATE`, `PROGRAM_SESSION_MEMBER_FEEDBACK`, `PROGRAM_SESSION_COACH_NOTE`.
- exercise actuals: `ACTUAL_SETS`, `ACTUAL_REPS`, `ACTUAL_WEIGHT_KG`, `ACTUAL_RPE`, `PROGRAM_EXERCISE_MEMBER_FEEDBACK`.
- every id: `programSessionId`, `programExerciseId`, `programBlockId` and `programCircuitId` all change. A client holding an old id gets 404 on completion or booking.
- "last performed" history. `ResolveLastPerformedAsync` (`:745-788`) reads past DONE rows, so the member's earlier records for these exercises disappear, across programs too.

What is kept: `PROGRAM_ID`, `PROGRAM_TEMPLATE_ID`, `PROGRAM_WEEK_OFFSET`, `PROGRAM_STATUS`,
`PROGRAM_CREATED_DATE`. There is no status guard: an ARCHIVED or COMPLETED program can also be
replaced (`:165-167` filters only on owner). The PUT never re-links existing bookings.

### 5.2 Foreign keys and routes on the tree ids

S12 (`pg_constraint`), consistent with the EF configurations:

| FK | From → to | ON DELETE |
|---|---|---|
| `FK_PROGRAM_BLOCKS_PROGRAM_BLOCKS_PROGRAM_BLOCK_PARENT_ID` | PROGRAM_BLOCKS → PROGRAM_BLOCKS | RESTRICT (`ProgramBlockConfiguration.cs:60-63`) |
| `FK_PROGRAM_SESSIONS_PROGRAM_BLOCKS_PROGRAM_SESSION_BLOCK_ID` | PROGRAM_SESSIONS → PROGRAM_BLOCKS | RESTRICT (`ProgramSessionConfiguration.cs:95-98`) |
| `FK_PROGRAM_SESSIONS_SESSIONS_PROGRAM_SESSION_SESSION_ID` | PROGRAM_SESSIONS → SESSIONS | SET NULL (`:102-105`) |
| `FK_PROGRAM_CIRCUITS_PROGRAM_SESSIONS_...` | PROGRAM_CIRCUITS → PROGRAM_SESSIONS | RESTRICT (`ProgramCircuitConfiguration.cs:56-59`) |
| `FK_PROGRAM_EXERCISES_PROGRAM_CIRCUITS_...` | PROGRAM_EXERCISES → PROGRAM_CIRCUITS | RESTRICT (`ProgramExerciseConfiguration.cs:87-90`) |

Every child also has a CASCADE FK to `PROGRAMS` (configurations `:51-54` / `:90-93` / `:51-54` /
`:82-85`). No other table references a tree id. Unique index `IX_PROGRAM_SESSIONS_BOOKING_MEMBER`
on (SESSION_ID, MEMBER_ID) comes from `20260813070833_Lot6_5_Agenda.cs:26`.

Queries reading these ids outside `ProgramService`: `SessionService.cs:478-480` and `:797-800`
(participants), `:637-663` (auto link); `AgendaService.cs:155-171`, `:221-227`.

Routes taking a tree id:
- `PUT /api/v1/coach/program-sessions/{programSessionId}/booking` (`CoachProgramSessionsController.cs:29`).
- `PUT /api/v1/coach/program-sessions/{programSessionId}/completion` (`:57`).
- `PUT /api/v1/member/program-sessions/{programSessionId}/completion` (`MemberProgramSessionsController.cs:29`).
- The completion body carries `programExerciseId` (`ProgramExerciseActualRequest`, checked at `ProgramService.cs:376-383`).

No route takes a block id or a circuit id.

### 5.3 Ids exposed by the GET responses

`ProgramBlockResponse.ProgramBlockId` (+ `ParentId`), `ProgramSessionResponse.ProgramSessionId`,
`ProgramCircuitResponse.ProgramCircuitId` and `ProgramExerciseResponse.ProgramExerciseId` are all
exposed, the same for coach and member (one `ProgramResponse`). **The booking id is not
exposed**: `ProgramSessionResponse` (`:9-21`) has no `SessionId`. A client sees the link only
through the agenda, the participants list or the booking PUT response.

### 5.4 SQL: programs with history (S5, S6, S7)

```
 programs_total | programs_with_history | with_done_or_skipped | with_booking_link
              0 |                     0 |                    0 |                 0
```
The local database has no programs at all. S7: 0 sessions not PLANNED, 0 feedback, 0 coach note,
0 exercises with actuals. The VPS was not queried (out of scope), so the production exposure is
unknown.

**Impact on lot 11:** an in-place update (diff by id) is needed before any change that touches
assigned programs. Otherwise every PUT erases the member's history.

---

## Q6. Booking link and visio eligibility

### 6.1 Type matching rule

- Automatic (`SessionService.LinkToProgramSessionAsync`, `:626-664`, called from `ReserveAsync` `:124` and `RegisterParticipantAsync` `:575`): `var offerTypeWire = EnumMappings.OfferTypeMapping.ToWire(session.SessionOfferType);` and candidates `ps.ProgramSessionStatus == ProgramSessionStatus.Planned && ps.ProgramSessionSessionId == null && ps.ProgramSessionType == offerTypeWire` (`:634-644`). The earliest computed date wins, then position (`:653-658`). It never fails a booking (`:621-625`).
- Manual (`ProgramService.UpdateBookingAsync`, `:249-253`): `if (sessionOfferTypeWire != programSession.ProgramSessionType) throw new ConflictException("Session and program session types do not match.", new { sessionType, programSessionType });` → 409.
- The automatic link filters on the member's ACTIVE program only (`:629-630`), **not on the coach**: a booking with coach B can be linked to a program session of coach A's program. The manual link requires the same coach on both sides (`ProgramService.cs:231-239`).

### 6.2 Other comparisons between program session type and offer type

None. A grep for `ProgramSessionType` in services finds only `ProgramService`, `SessionService`
and `AgendaService`. The agenda displays both types side by side but does not compare them
(`AgendaService.cs:195`, `:268`).

### 6.3 `VisioTokenService` eligibility

- 404 "Session not found.": the session does not exist, or belongs to another coach (`:23-25`), or `SessionMemberId != memberId` on the member side (`:39-40`, so group participants get 404).
- 409 `ConflictException("Cannot issue a visio token: {reason}.", { sessionId, reason, opensAt, closesAt })` (`:94-96`), with these reasons:
  - `NOT_ELIGIBLE` when `SessionOfferType != OfferType.Visio || SessionMemberId is null` (`:76-77`)
  - `CANCELLED` (`:79-80`)
  - `WINDOW_NOT_OPEN` (`:83-84`), `WINDOW_CLOSED` (`:85-86`). The window is `scheduledAt - JoinBeforeMinutes` to `scheduledAt + duration + JoinAfterEndMinutes` (`:71-72`).
- The program session type plays no part.

### 6.4 Origin of `SESSION_OFFER_TYPE`

- Individual booking: `SessionOfferType = voucher.OfferType` (`SessionService.cs:108`), which must equal the slot's offer type (`:79-94`). The voucher type comes from the product at purchase (`CartService.cs:280`, `:308` → `VoucherGeneration.cs:29`) or at a coach grant (`VoucherGrantService.cs:79` `OfferType = product.ProductOfferType`).
- Group session: `SessionOfferType = slot.SessionSlotOfferType` (`SessionService.cs:444`), which must equal the product's (`:421`).
- Never updated after creation: these two assignments are the only writes in the code (grep `SessionOfferType\s*=`).

**Impact on lot 11:** if visio eligibility should follow the program session type, the rule
lives in `VisioTokenService.cs:76` and must reach `PROGRAM_SESSIONS` through the link. Today
eligibility depends only on the booking's offer type.

---

## Q7. Emails

### 7.1 Call sites

There is only one: `AuthService.cs:61` `emailSender.SendAsync(...)` for the OTP. A grep for
`IEmailSender|emailSender\.` finds nothing else. The platform sends no other email.

### 7.2 Provider selection and configuration

- `Program.cs:300-306`: `Configure<EmailOptions>(...)`, then `var emailProvider = builder.Configuration["Email:Provider"] ?? "Logging";` with `Smtp` (case-insensitive) → `SmtpEmailSender`, anything else → `LoggingEmailSender`. The selection reads the raw configuration, not `EmailOptions.Provider`.
- `EmailOptions.cs:3-27`: `Provider`, `FromAddress`, `FromDisplayName`, `Smtp{Host, Port, UseStartTls, Username, Password}`, `Retry{MaxAttempts, DelaySeconds}`.
- `IEmailSender.cs:7`: implementations "Must NOT throw". `SmtpEmailSender` retries, then returns false (`:24-64`).
- `Email` keys per file (values hidden for addresses and secrets):
  - `appsettings.json` (tracked): `Provider = Smtp`, `FromAddress <set>`, `FromDisplayName = Mentora`, `Smtp:Host <set>`, `Smtp:Port = 587`, `Smtp:UseStartTls = true`, `Smtp:Username <set>`, `Smtp:Password <empty>`, `Retry:MaxAttempts = 2`, `Retry:DelaySeconds = 2`.
  - `appsettings.Development.json` (not tracked): no `Email` section.
  - `appsettings.Production.json` (tracked): no `Email` section.
  - User secrets of `Mentora.API` (local): key `Email:Smtp:Password` only.
  - No `Email__*` environment variable in this shell.
- Consequence: locally (Development) the provider resolves to **Smtp with a real password**, not Logging.

### 7.3 Startup validation

`EmailOptions` is not validated: no `.Validate(...)` and no `ValidateOnStart`. Only `LiveKitOptions`
(`Program.cs:264-272`) and `QuizOptions` (`:276-282`) use `ValidateOnStart`.

**Impact on lot 11:** any new email reuses `IEmailSender` as is. Adding `ValidateOnStart` for
Smtp would catch a missing host or password at startup.

---

## Q8. Coach settings (11.3)

- `COACHES` (S11): `COACH_ID`, `COACH_FIRST_NAME`, `COACH_LAST_NAME`, `COACH_PHONE`, `COACH_CREATED_DATE`, `COACH_IS_ACTIVE`, `USER_ID`.
- `COACH_PARAMETERS` (S11): `COACH_PARAMETER_ID`, `HOURLY_RATE_EUROS`, `CANCELLATION_DELAY_HOURS`, `CREATED_DATE`, `UPDATED_DATE`, `COACH_ID`, `DEFAULT_SESSION_DURATION_MINUTES`, `IS_ACCEPTING_NEW_BOOKINGS`, `LANGUAGE`, `LATE_CANCELLATION_REFUNDS`, `MAX_BOOKING_HORIZON_DAYS`, `MIN_BOOKING_NOTICE_HOURS`, `NOTIF_BOOKING_CANCELLED`, `NOTIF_MARKETING`, `NOTIF_MESSAGES`, `NOTIF_NEW_BOOKING`, `MISSED_SESSION_BEHAVIOR` (all prefixed `COACH_PARAMETER_`).
- Editable fields, from `UpdateCoachParameterRequest.cs:3-24` and `CoachParameterService.cs:55-73`:
  - 13 `COACH_PARAMETERS` columns.
  - 3 `COACHES` columns (`FirstName`, `LastName`, `Phone`).
  - Total: **16 fields in one PUT, not 15**.
- Existing routes:
  - `GET` and `PUT /api/v1/coach/parameters` (`CoachParametersController.cs:9,15,23`).
  - `GET` and `PUT /api/v1/coach/members/{memberId}/parameters` (`CoachMemberSettingsController.cs:10,20,32`).
  - Nothing under `/api/v1/coach/settings`, so `GET` and `PUT /api/v1/coach/settings/session-preferences` collide with no route.

---

## Q9. Session template prerequisites (11.5)

- Mentora templates (`PROGRAM_TEMPLATE_COACH_ID` null):
  - Visibility: a coach sees them next to their own (`ProgramTemplateService.cs:29-35` with `scope` ALL/MENTORA/MINE; `:56-59` for one template), and can assign them (`ProgramService.cs:71-75`).
  - Read-only: PUT and DELETE filter on `ProgramTemplateCoachId == coachId`, so a Mentora template returns 404 (`ProgramTemplateService.cs:92-97`, `:115-117`; controller docs `:83-84`, `:105-106`).
  - No API creates one: `CreateAsync` always sets `coachId` (`:72`).
  - Seeder: `ProgramTemplateSeeder` inserts by name when missing and never updates (`:41-49`).
  - Unique index (COACH_ID, NAME) `NULLS NOT DISTINCT`: `20260811132427_Lot6_2_ProgramTemplates.cs:50`.
  - Locally: 2 Mentora templates, active (S4).
- `ExerciseService.ResolveVisibleActiveIdsAsync` (`ExerciseService.cs:279-293`): `exerciseIds.Contains(e.ExerciseId) && (e.ExerciseCoachId == null || e.ExerciseCoachId == coachId) && e.ExerciseIsActive`. That is the Mentora catalogue plus the coach's own exercises, active only. It is used by the body validator (`ProgramTemplateBodyValidator.cs:75`). It is not run on path W3a (template copy) or W5 (seeder).

**Impact on lot 11.5:** session templates can follow the same pattern: nullable coach id, read
rules in the query, 404 on foreign rows, seeder by name. Remember that a copy from a template
does not re-check exercise visibility.

---

## Contradictions with the lot 11 assumptions

| Assumption | Verdict | Evidence |
|---|---|---|
| Templates jsonb, assigned programs relational | **Confirmed** | Q1.1 |
| `A_DISTANCE` = session done by the member alone, never booked | **Partly.** "Never booked" is enforced (CHECK, plus no matching offer type). "Done by the member alone" is **not documented** anywhere; the coach can also complete it, and it appears in both agendas like any session | Q2.1, Q2.2 |
| No existing "rest after exercise" field | **Confirmed**, with a caution: `RestSeconds` exists and its meaning is undocumented (values look like rest between sets) | Q3.1, Q3.3 |
| `loadPercent` currently dropped | **Confirmed from the code** (silently, no 400/422), not tested over HTTP. No stored template contains it | Q1.5, Q1.6 |
| OTP request returns 400 for an unknown email and sends nothing | **Confirmed** by HTTP (0 writes, no email). Nuances: a disabled user gives the same 400; a BOTH account without `isCoach` gives **409**, not 400 | Q4.1, Q4.2 |
| PUT training-programs loses booking links, statuses and actuals | **Confirmed**, and it also loses every tree id, feedback, coach notes and the "last performed" history. There is no status guard. Local exposure is 0 (no programs); VPS not measured | Q5.1, Q5.4 |
| OTP is the only email | **Confirmed** | Q7.1 |

Other facts that differ from what is written elsewhere:
1. The OTP email says "Ce code expire dans 15 minutes." The OTP expires after 10 minutes (`AuthService.cs:52` vs `:335`, `:348`).
2. Coach settings have 16 editable fields (13 parameters + first name, last name, phone), not 15.
3. Locally `Email:Provider` resolves to **Smtp** with a real password from user secrets, not Logging. A valid local OTP request sends a real email.
4. CLAUDE.md: `MemberOnly` also requires `userType = MEMBER` (`Program.cs:185-187`). CLAUDE.md also says Minimal APIs, while the code uses controllers.
5. The automatic booking link is not scoped to the coach (`SessionService.cs:629-645`).
6. `ProgramSessionResponse` does not expose the linked booking id (Q5.3).
