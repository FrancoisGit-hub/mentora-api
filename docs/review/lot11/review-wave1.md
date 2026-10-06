# Lot 11, wave 1: code review (read only)

Branch `feature/lot_11` (HEAD `1947f8d`) against `origin/main`. Scope:
`git --no-optional-locks diff origin/main...feature/lot_11 -- . ':!*.Designer.cs' ':!*.csv' ':!docs/review'`,
16 commits (`e45a5f2` to `1947f8d`), 67 files, +2737 / -183.
Baseline: CLAUDE.md, `docs/review/lot11/11.0/inventory.md`, and the evidence files of 11.1 to 11.5.
11.1, 11.2 and 11.2b have only a `runtime-evidence.md`; they have no `preflight.md`.

Nothing was run except `git --no-optional-locks`, file reads, `dotnet build` and
`dotnet ef migrations has-pending-model-changes`. No API was started, the database was not touched,
and no file was changed except this one.

## Summary

| Severity | Count |
|---|---|
| BLOCKER | 1 |
| SHOULD | 3 |
| BACKLOG | 9 |

Tool results:
- `dotnet build`: 0 errors, 19 warnings. All are CS1573 (missing `<param>` tag) in `CoachConversationMessagesController`, `MemberConversationMessagesController`, `LastPerformedResponse`, `ProgramExerciseActualRequest` and `ProgramSessionResponse`. The `ProgramSessionResponse` warnings are about parameters that already existed on `origin/main` without a tag. No new warning comes from a parameter added in lot 11.
- `dotnet ef migrations has-pending-model-changes` (Development): **"No changes have been made to the model since the last migration."** exit 0. The command also printed EF sentinel warnings, all on pre-existing enum properties (Order, Product, Session, ...). None is on an entity or property added in lot 11.

---

## BLOCKER

### B1. The demo server will not start without the new `EmailRouting` variables
- **Where:** `Mentora.API/Program.cs:309-312` and `:380-390`; `Mentora.Core/Options/EmailRoutingOptions.cs:33-36`; `Mentora.Core/Options/EmailOptions.cs` (`Validate`).
- **What:** `EmailRoutingOptions.Validate` always requires `EmailRouting:TestRecipients`, even in Production mode. `Mode=Production` also requires `EmailRouting:ProductionBcc`. `EmailOptions.Validate` now requires, when the provider is Smtp, `Smtp:Host`, `Smtp:Username`, `Smtp:Password` and a valid `FromAddress`. The tracked `appsettings.json` sets `Email:Provider = Smtp`. All of this is read before migrations and the API refuses to start if any value is missing. These keys appear in no appsettings file and in no doc outside `docs/review` (grep `EmailRouting`: only 11.4 and 11.5 evidence).
- **Why it matters:** if the demo server's compose file does not already have these variables, the container crash-loops right after deploy. The deploy is blocked, and migrations never run.
- **Smallest fix (no code):** before deploying, add `EmailRouting__TestRecipients` to the demo server environment. If the demo should send real mails, also add `EmailRouting__Mode=Production` and `EmailRouting__ProductionBcc`. Check that `Email__Smtp__Username`, `Email__Smtp__Password` and `Email__FromAddress` are set there. Add a short "Email routing" paragraph to CLAUDE.md that lists the variable names, without values.

---

## SHOULD

### S1. A `null` entry in a tree list returns 500 instead of 422
- **Where:**
  - `Mentora.Core/DTOs/Program/Body/UpdateProgramBody.cs:21` and `UpdateProgramBlock.cs:35-36`; the same pattern is in `UpdateProgramSession.ToTemplateSession` and `UpdateProgramCircuit.ToTemplateCircuit`. They call `b.ToTemplateBlock()` and its siblings on each element.
  - `Mentora.Core/Validators/ProgramTemplate/ProgramCircuitRules.cs:101`: the `ValidatePositions` selector reads `.Position` on every element.
  - `ProgramTemplateBodyValidator.cs:94-95`.
  - `UpdateProgramNodeIdsValidator.cs:58`.
  - `ProgramService.cs:451` (`PlanTree`).
- **What:** System.Text.Json on .NET 8 binds `"blocks":[null]`, `"sessions":[null]`, `"circuits":[null]` or `"exercises":[null]` as a list that contains `null`. The mapping and the validators then dereference that element. The resulting `NullReferenceException` escapes FluentValidation and `GlobalExceptionMiddleware` turns it into a 500.
  - Affected routes: `PUT /api/v1/coach/training-programs/{id}`, `POST` and `PUT /api/v1/coach/session-templates`. The template and assign routes (`POST`/`PUT /coach/program-templates`, `POST .../training-programs`) have the same problem on `origin/main`.
  - This conclusion comes from reading the code; it was not run.
- **Why it matters:** a client bug (or a hand-written request) gets a 500 and an error log line instead of a 422 with a key. These are new public write routes.
- **Smallest fix:** in `ProgramCircuitRules.ValidatePositions`, before the selector: `if (items.Any(i => i is null)) { fail(path, "Entries must not be null."); return; }`. Make the four `To*` mappers null-tolerant (`Blocks?.Select(b => b?.ToTemplateBlock()!)` and so on), so the null reaches the validator and gets a 422. Skip null nodes in `UpdateProgramNodeIdsValidator.CollectBlocks`. `PlanTree` is safe once the validator rejects nulls first. Callers that loop with `foreach` after `ValidatePositions` must `return` or `continue` when a failure was added.

### S2. Open question 11.2c: an exercise with realised values whose `exerciseId` or `loadType` changes
- **Where:** `Mentora.Infrastructure/Services/ProgramService.cs:394-412` (prescribed values are overwritten, including `ProgramExerciseExerciseId` at `:403` and `ProgramExerciseLoadType`); the history block ends at `:297`.
- **What:** today this is **allowed**. A kept exercise with `ACTUAL_*` values can be switched from, say, squat to bench press, or from KG to BODYWEIGHT. The actuals stay on the row.
- **Why it matters:**
  - The realised weight and reps get attached to another movement. `ResolveLastPerformedAsync` then reports a "last performed" squat load as a bench-press history, in this program and in every later one.
  - A BODYWEIGHT exercise ends up carrying `ACTUAL_WEIGHT_KG`.
  - This is the same kind of history loss that 11.2c set out to prevent.
- **Recommendation: 409 `PROGRAM_HAS_HISTORY`.** Add the exercise id to `programExerciseIds`, with the same details shape. No new reason code is needed.
- **Cost:**
  - About 6 lines before `:297`: loop over `plan.Exercises`, look up `exercisesById`, and when `ExerciseHasHistory(e)` and (`e.ProgramExerciseExerciseId != node.ExerciseId` or `e.ProgramExerciseLoadType != node.LoadType`), add the id to `blockingExerciseIds`.
  - One more sentence in the `CoachProgramsController.Update` remarks.
  - Two runtime cases (change `exerciseId` → 409; change `prescribedReps` only → 200).
  - Roughly 30 to 45 minutes including evidence.
- **Allowing it instead** costs nothing now, but needs a product rule for what "last performed" means after the swap, and the app would have to warn the coach. Not recommended.

### S3. Personal addresses committed in a versioned evidence file
- **Where:** `docs/review/lot11/11.4/runtime-evidence.md:49` (commit `e09087f`). The `[EMAIL] mode=PRODUCTION` line quotes the two real Production Bcc addresses (personal gmail).
- **What:** rule 3.1 says no personal address in a versioned file. The evidence file states "accepted for this evidence", so this is your call, not a code defect. Every other line of that file writes `<the four addresses>`.
- **Why it matters:** once the branch is pushed, the addresses stay in git history. The branch is **not pushed yet** (`git branch -r --contains e09087f` is empty), so it is still cheap to fix.
- **Smallest fix:** replace the two addresses with `<the two ProductionBcc addresses>` and rewrite `e09087f` before the first push (fixup and rebase of the 4 later commits). A new commit on top would leave the addresses in history.

---

## BACKLOG

1. **`ProgramService.cs:168-185`, status check outside the transaction.** The program is loaded and its status (`PROGRAM_NOT_EDITABLE`) checked before `BeginTransactionAsync(Serializable)`. A concurrent `AssignAsync` that archives the program in between lets the PUT write to an archived program. Separately, a PostgreSQL serialization failure (40001), for example a PUT racing a completion on the same rows, returns 500, not 409. Data stays consistent. Fix later: reload the program inside the transaction, or retry on 40001. This adds to backlog 5 of the 11.2c evidence.
2. **`SmtpEmailSender.cs:19`.** `BuildMessage` (`MailboxAddress.Parse` for To and Bcc) runs outside the try/catch, so an address MimeKit cannot parse makes `SendAsync` throw, which breaks the "must not throw" contract. The OTP request would then return 500 after the OTP row is saved. This exists on `origin/main` for To. Bcc and test addresses are checked at startup with `System.Net.Mail.MailAddress`, which is close to MimeKit but not identical. Fix: move `BuildMessage` inside the try, or wrap it in its own try that returns false.
3. **`AuthService.cs:82-91`, `SanitizeFirstName`.**
   - `cleaned[..50]` can split a surrogate pair (an emoji at position 50); the encoder then writes U+FFFD.
   - `char.IsControl` does not remove Unicode format characters such as U+202E (right-to-left override) or U+2028. A first name can therefore reorder the subject text.
   - Header injection is not possible: MimeKit encodes the subject.
   - Fix: reuse the surrogate guard from `SessionTemplateService.CopyName`, and also strip `UnicodeCategory.Format`.
4. **`SessionTemplateService.cs:88-90`, search term folding.**
   - The LIKE escape is correct: `\`, `%` and `_` are escaped, then the term is folded, and `ESCAPE '\'` is passed, the same rule as `ExerciseSearch.EscapeLike`.
   - But the term is folded in C# (`AccentFolding.Fold` → `ToLowerInvariant`), while the column is folded in SQL (`translate` + `lower()`). For uppercase non-ASCII letters that are not in the map (Œ, Ñ, Å), PostgreSQL `lower()` depends on the database ctype and may leave them unchanged, so the search can miss.
   - The escape helper also duplicates `ExerciseSearch.EscapeLike`, which is `internal` in the same assembly.
   - Fix: fold the term in SQL as `ExerciseSearch` does, or at least call `ExerciseSearch.EscapeLike`.
5. **`SessionTemplateService.cs:72`.** A `switch` expression on `scopeValue`. It is not an enum helper, but the codebase rule is if chains, never switch. Three `if`s would do.
6. **`SESSION_TEMPLATES.COACH_ID`** (`SessionTemplateConfiguration.cs:20`, migration `Lot11_5`). The column is not prefixed, while the sibling table uses `PROGRAM_TEMPLATE_COACH_ID`. Both styles already exist (`COACH_PARAMETERS.COACH_ID`, `SESSION_SLOTS.COACH_ID`), and the 11.5 preflight proposed this name, so it is noted only. Renaming is cheap only before the migration reaches the demo database.
7. **`RoutingEmailSender.cs:20-22`.** The `[EMAIL]` Information line logs real recipient addresses (PRODUCTION `to=`, and the TEST subject prefix `[TEST pour <address>]`). It never logs the body or the OTP, which is correct. Addresses in logs are consistent with `SmtpEmailSender`'s existing To logging; note it for the log-retention or GDPR review.
8. **`Lot11_2_ProgramTreeFields` Down.** Down drops `PROGRAM_SESSION_NATURE`, `PROGRAM_BLOCK_LOAD_PERCENT` and `PROGRAM_CIRCUIT_REST_AFTER_SECONDS` without a guard, so EVALUATION natures and load values are lost on rollback. `loadPercent`, `nature` and `restAfterSeconds` keys also stay in template jsonb bodies after Down, which is harmless. The `PRESENTIEL_OU_VISIO` guard is correct. This is expected for a Down; write it in the rollback runbook.
9. **Swagger tag casing.** `"Coach — Session Preferences"` (`CoachSessionPreferencesController.cs:13`) vs `"Coach — Session templates"` and `"Coach — Program templates"`. Cosmetic.

---

## Section by section: what was checked and found clean

### 3.1 Security and authorization
- **Policies on new or changed routes:**
  - `GET /api/v1/quiz/basic/sessions/{id}` inherits the class-level `QuizPlayer` policy and `EnableRateLimiting`.
  - `coach/settings/session-preferences` and `coach/session-templates` (6 routes) use `CoachOnly`.
  - `PUT coach/training-programs/{id}` and `PUT coach/program-sessions/{id}/booking` keep `CoachOnly` from the class.
- **Ownership returns 404, never 403:**
  - Programs: `ProgramCoachId == coachId` before anything else.
  - Session templates: one query each, `FindOwnedAsync` / `FindVisibleAsync`.
  - Quiz: owner in the WHERE.
  - Visio: unchanged.
- **No scope leak between coaches:**
  - Every coach id comes from the claim.
  - The automatic link filters `PROGRAM_COACH_ID` and `PROGRAM_SESSION_COACH_ID` in SQL.
  - Node ids of the PUT are checked against this program's own id sets, with the same message for unknown or foreign ids.
  - Exercise visibility goes through `ResolveVisibleActiveIdsAsync`.
- **No mass assignment:** `UpdateProgram*` nodes have no status, link or actual fields. `SessionTemplateRequest` has no coach, source code or active flag. `UpdateSessionPreferencesRequest` has 3 fields.
- **No secret in code.** For the personal address in a doc, see S3.
- **No new log line with an OTP or a mail body.** The new lines are the `[EMAIL]` startup counts and per-mail routing (to, bcc, subject). The existing OTP console line and the Logging provider mail line are unchanged, as decided in 11.4.

### 3.2 Migrations and data
- **Lot11_2:**
  - Up: add columns → drop the old CHECK → `UPDATE PROGRAM_SESSIONS` → text replace on `PROGRAM_TEMPLATES` → 5 named CHECKs.
  - Down: guard → drop CHECKs → convert back → recreate the old CHECK → drop columns. The order is correct both ways.
  - CHECK lists match `ProgramSessionTypes.All` and `ProgramSessionNatures.All`. The load list is `(100, 102.5, 105, 110)` on `numeric(4,1)` with a `LEVEL = 'MESOCYCLE'` guard. The rest-after range is 0..600.
- **Lot11_3:** 3 columns, `CK_COACHES_IN_PERSON_LOCATIONS`, and Down drops the CHECK first. Correct.
- **Lot11_5:** table, FK `Restrict`, 4 named CHECKs, a partial unique index; Down drops them in reverse. Correct.
- **A_DISTANCE coverage:** the only places that store a program session type are `PROGRAM_SESSIONS.PROGRAM_SESSION_TYPE` and `PROGRAM_TEMPLATES.PROGRAM_TEMPLATE_BODY`. Grep over configurations: the other `*_TYPE` columns hold offer, exercise, load, sender or event types, and the other jsonb columns are tags, muscles, mistakes, Stripe payload and locations. `SESSION_TEMPLATES` is created later by Lot11_5. Both places are migrated, and the seeder was updated (`ProgramTemplateSeeder.cs:174`). The text replace is safe only if `"A_DISTANCE"` never appears as a full string value outside `type`: SQL check C5 below.
- **Defaults match the C# initializers:** `NATURE 'STANDARD'` = `"STANDARD"`; coach booleans `false` = `false`; `'[]'::jsonb` = `new List<string>()`; `IS_ACTIVE true` = `true`.
- **FK delete behaviour:** the only new FK (`FK_SESSION_TEMPLATES_COACHES_COACH_ID`) is Restrict, with the reason commented.
- `has-pending-model-changes`: none.

### 3.3 Program tree
- **11.2:**
  - `loadPercent` is allowed only on MESOCYCLE, with an exact `decimal` list (`ProgramTemplateBodyValidator.cs:112-115`).
  - `nature` and `restAfterSeconds` are validated on every validated write path: template POST and PUT, assign with a body, program PUT (through `ToTemplateBody`), and session templates (through `ProgramCircuitRules`).
  - The unvalidated paths are W3a (copy of an already validated template; null nature becomes STANDARD) and the seeder.
- **11.2b:**
  - One helper, `ProgramSessionTypes.CompatibleWithOffer` / `IsCompatibleWithOffer` (if chain), is used by the automatic link (`SessionService.cs:658-674`) and the manual link (`ProgramService.UpdateBookingAsync`).
  - The automatic link is scoped to `session.SessionCoachId` on the program and on the candidates, with the type filter `= ANY(@compatibleTypes)` in SQL. Only the earliest-date ordering runs in memory, as before.
  - Visio eligibility is one private `Issue()` shared by the coach and member endpoints.
- **11.2c:**
  - Serializable transaction (`ProgramService.cs:185`).
  - Check order: 404 → 409 `PROGRAM_NOT_EDITABLE` → 422 tree → 422 ids → 409 `PROGRAM_HAS_HISTORY` → 409 `PROGRAM_SESSION_TYPE_INCOMPATIBLE` → writes, as in decision D4.
  - History: a non-cancelled linked booking, a status other than PLANNED, a completed date, feedback, a coach note, or exercise actuals. It covers deletion, and moves of blocks, sessions, circuits and exercises read on the existing tree (D3).
  - 11.AD: same booking returns 200 with no write; another live booking → 409 `PROGRAM_SESSION_ALREADY_LINKED`.
  - 11.AJ: a cancelled link is not history, is not type-checked, can be replaced, and is cleared when the new type no longer fits its offer type.
  - Open question: see S2. Races: BACKLOG 1.

### 3.4 Quiz 11.1
- Ownership is identical to `SubmitAsync`: coach `QUIZ_SESSION_COACH_ID = @coachId`; device `DEVICE_ID = @deviceId AND COACH_ID IS NULL`; in both cases in the WHERE.
- Device mode reads `X-Device-Id` through `ResolveOwner` and `ownerValidator`.
- Rate limiting is applied at class level. Nothing found.

### 3.5 Session preferences 11.3
- The validator (3 required fields, at most 10 locations, 1..100 characters once trimmed, null items rejected, accent- and case-insensitive duplicates, empty list when solo is false) matches the CHECK.
- `AccentFolding.Accented` / `Plain` are the exact strings `ExerciseSearch` used before. `ExerciseSearch` now references the same constants, so the SQL and C# tables cannot drift. Nothing found.

### 3.6 Emails 11.4
- **Only one `IEmailSender` registration:** the `RoutingEmailSender` factory. Concrete providers are registered as themselves, and nothing else injects them (grep).
- **TEST mode:** `To` is replaced by the test list, `Bcc = []` (caller Bcc dropped), and the subject gets a prefix.
- **PRODUCTION mode:** caller Bcc plus `ProductionBcc`, with duplicates against `To` and against itself removed (case-insensitive).
- **Startup validation:** runs right after `Build()`, before migrations and seeders. Messages name keys or item numbers only.
- **DI lifetimes:** the decorator and the providers are scoped; options and validators are singletons; no captive dependency.
- **Subject:** first name of the effective role, sanitized (BACKLOG 3); no OTP code.
- **OTP response:** every error path throws before the mail step, the 200 path is unchanged, and the mail result is still discarded. The only new risk is BACKLOG 2, which already exists on `origin/main`.

### 3.7 Session templates 11.5
- **Extraction:** `ProgramCircuitRules` was compared line by line with `73ba887~1` (`ValidateCircuits`, `ValidateExercises` and `ValidatePositions` before the extraction). Rules, keys, messages and order are identical. `exerciseIds.Add` happens at the same point; the aggregated "Unknown or inaccessible exercise id(s)" failure is unchanged.
- **LIKE search:** escaped, see BACKLOG 4 for folding.
- **Visibility:** one query on every write, read and duplicate. Platform templates are never writable (`COACH_ID == coachId` never matches null). Inactive platform templates are invisible in list, get and duplicate.
- **`sessionNode`:** a `ProgramTemplateSession`, which has no id or sessionId field.
- **Null entries:** see S1.

### 3.8 Cross-cutting
- **N+1:** none.
  - Quiz GET: 2 queries.
  - Session template list: 1 query; get: 1.
  - Program PUT: a fixed set of 7 queries plus one batched save.
  - Booking link: one extra `AnyAsync` only when the program session is already linked.
  - `BuildResponseAsync`: unchanged.
- **Enum helpers:** if chains (`CompatibleWithOffer`); one `switch` in a non-enum spot (BACKLOG 5).
- **Naming:** new columns are UPPERCASE and prefixed (`*_CREATED_DATE`, `*_UPDATED_DATE`), except `COACH_ID` (BACKLOG 6).
- **Swagger:** the new controllers are in group `coach`. The quiz route is in the quiz controller's existing `coach` group.
- **Routes:** no collision (`coach/settings/session-preferences`, `coach/session-templates`, `{id:guid}/duplicate`, `quiz/basic/sessions/{sessionId:guid}`).
- **Build:** see the Summary.

---

## Read-only SQL checks for the demo database (run before deploying)

Run all of them in one session, inside `BEGIN TRANSACTION READ ONLY; ... ROLLBACK;`. Expected results are in the comments.

```sql
BEGIN TRANSACTION READ ONLY;

-- C0. Migration state: the last applied migration should be 20260930085059_Lot10_Quiz
--     (or earlier). None of the Lot11_* ids may be present.
SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 5;

-- C1. Objects that Lot11_* creates must not exist yet; the one it drops must exist
--     (DROP CONSTRAINT has no IF EXISTS).
SELECT conname FROM pg_constraint
WHERE conname IN ('CK_PROGRAM_SESSIONS_A_DISTANCE_HAS_NO_BOOKING',
                  'CK_PROGRAM_SESSIONS_AUTONOME_HAS_NO_BOOKING', 'CK_PROGRAM_SESSIONS_TYPE',
                  'CK_PROGRAM_SESSIONS_NATURE', 'CK_PROGRAM_BLOCKS_LOAD_PERCENT',
                  'CK_PROGRAM_CIRCUITS_REST_AFTER_SECONDS', 'CK_COACHES_IN_PERSON_LOCATIONS');
-- expected: exactly 1 row, CK_PROGRAM_SESSIONS_A_DISTANCE_HAS_NO_BOOKING
SELECT table_name, column_name FROM information_schema.columns
WHERE (table_name, column_name) IN (('PROGRAM_SESSIONS','PROGRAM_SESSION_NATURE'),
                                    ('PROGRAM_BLOCKS','PROGRAM_BLOCK_LOAD_PERCENT'),
                                    ('PROGRAM_CIRCUITS','PROGRAM_CIRCUIT_REST_AFTER_SECONDS'),
                                    ('COACHES','COACH_OFFERS_VISIO'),
                                    ('COACHES','COACH_OFFERS_IN_PERSON_SOLO'),
                                    ('COACHES','COACH_IN_PERSON_LOCATIONS'));
-- expected: 0 rows
SELECT to_regclass('public."SESSION_TEMPLATES"') AS session_templates;   -- expected: NULL

-- C2. PROGRAM_SESSIONS values that CK_PROGRAM_SESSIONS_TYPE could reject after the A_DISTANCE update.
SELECT "PROGRAM_SESSION_TYPE", count(*) AS n,
       count(*) FILTER (WHERE "PROGRAM_SESSION_SESSION_ID" IS NOT NULL) AS linked
FROM "PROGRAM_SESSIONS" GROUP BY 1 ORDER BY 1;
-- expected: only PRESENTIEL_SOLO, PRESENTIEL_GROUPE, VISIO, A_DISTANCE; A_DISTANCE linked = 0
SELECT count(*) AS would_fail_type_check FROM "PROGRAM_SESSIONS"
WHERE "PROGRAM_SESSION_TYPE" NOT IN ('PRESENTIEL_SOLO','PRESENTIEL_GROUPE','VISIO','A_DISTANCE');
-- expected: 0 (otherwise the migration fails and the API does not start)

-- C3. Rows to migrate (A_DISTANCE -> AUTONOME).
SELECT count(*) AS program_sessions_to_migrate FROM "PROGRAM_SESSIONS"
WHERE "PROGRAM_SESSION_TYPE" = 'A_DISTANCE';
SELECT count(*) AS templates_to_migrate FROM "PROGRAM_TEMPLATES"
WHERE "PROGRAM_TEMPLATE_BODY"::text LIKE '%"A_DISTANCE"%';

-- C4. Session types stored in template bodies. A value outside the list would make a later
--     "assign from template" (W3a, no validation) hit CK_PROGRAM_SESSIONS_TYPE -> 500.
SELECT x.t #>> '{}' AS session_type, count(DISTINCT pt."PROGRAM_TEMPLATE_ID") AS templates
FROM "PROGRAM_TEMPLATES" pt,
     LATERAL jsonb_path_query(pt."PROGRAM_TEMPLATE_BODY", 'strict $.**.sessions[*].type') AS x(t)
GROUP BY 1 ORDER BY 1;
-- expected: only PRESENTIEL_SOLO, PRESENTIEL_GROUPE, VISIO, A_DISTANCE

-- C5. The text replace is safe only if every "A_DISTANCE" string in a body is a session type
--     (no name or note equal to exactly "A_DISTANCE").
SELECT pt."PROGRAM_TEMPLATE_ID", pt."PROGRAM_TEMPLATE_NAME",
       (length(pt."PROGRAM_TEMPLATE_BODY"::text)
        - length(replace(pt."PROGRAM_TEMPLATE_BODY"::text, '"A_DISTANCE"', ''))) / length('"A_DISTANCE"') AS text_occurrences,
       jsonb_array_length(jsonb_path_query_array(pt."PROGRAM_TEMPLATE_BODY",
                          'strict $.**.sessions[*].type ? (@ == "A_DISTANCE")')) AS type_occurrences
FROM "PROGRAM_TEMPLATES" pt
WHERE pt."PROGRAM_TEMPLATE_BODY"::text LIKE '%"A_DISTANCE"%';
-- expected: text_occurrences = type_occurrences on every row

-- C6. "AUTONOME" / "PRESENTIEL_OU_VISIO" must not already exist (keeps Down reversible and exact).
SELECT
  (SELECT count(*) FROM "PROGRAM_SESSIONS" WHERE "PROGRAM_SESSION_TYPE" IN ('AUTONOME','PRESENTIEL_OU_VISIO')) AS sessions,
  (SELECT count(*) FROM "PROGRAM_TEMPLATES" WHERE "PROGRAM_TEMPLATE_BODY"::text LIKE '%"AUTONOME"%'
                                               OR "PROGRAM_TEMPLATE_BODY"::text LIKE '%"PRESENTIEL_OU_VISIO"%') AS templates;
-- expected: 0 | 0

-- C7. Exposure to the new 11.2c rules (information: which PUTs from an app that sends no ids
--     will now get 409, and which programs become read only).
SELECT "PROGRAM_STATUS", count(*) FROM "PROGRAMS" GROUP BY 1 ORDER BY 1;
SELECT count(DISTINCT ps."PROGRAM_SESSION_PROGRAM_ID") AS active_programs_with_history
FROM "PROGRAM_SESSIONS" ps
JOIN "PROGRAMS" p ON p."PROGRAM_ID" = ps."PROGRAM_SESSION_PROGRAM_ID" AND p."PROGRAM_STATUS" = 'ACTIVE'
LEFT JOIN "SESSIONS" s ON s."SESSION_ID" = ps."PROGRAM_SESSION_SESSION_ID"
WHERE (ps."PROGRAM_SESSION_SESSION_ID" IS NOT NULL AND s."SESSION_STATUS" <> 'CANCELLED')
   OR ps."PROGRAM_SESSION_STATUS" <> 'PLANNED'
   OR ps."PROGRAM_SESSION_COMPLETED_DATE" IS NOT NULL
   OR ps."PROGRAM_SESSION_MEMBER_FEEDBACK" IS NOT NULL
   OR ps."PROGRAM_SESSION_COACH_NOTE" IS NOT NULL
   OR EXISTS (SELECT 1 FROM "PROGRAM_EXERCISES" pe
              JOIN "PROGRAM_CIRCUITS" pc ON pc."PROGRAM_CIRCUIT_ID" = pe."PROGRAM_EXERCISE_CIRCUIT_ID"
              WHERE pc."PROGRAM_CIRCUIT_PROGRAM_SESSION_ID" = ps."PROGRAM_SESSION_ID"
                AND (pe."PROGRAM_EXERCISE_ACTUAL_SETS" IS NOT NULL OR pe."PROGRAM_EXERCISE_ACTUAL_REPS" IS NOT NULL
                  OR pe."PROGRAM_EXERCISE_ACTUAL_WEIGHT_KG" IS NOT NULL OR pe."PROGRAM_EXERCISE_ACTUAL_RPE" IS NOT NULL
                  OR pe."PROGRAM_EXERCISE_MEMBER_FEEDBACK" IS NOT NULL));

-- C8. Coaches: the new CHECK is satisfied by the defaults; count only, for the post-deploy check.
SELECT count(*) AS coaches FROM "COACHES";

ROLLBACK;
```

Column names in C7 (`PROGRAM_CIRCUIT_PROGRAM_SESSION_ID`, `PROGRAM_EXERCISE_CIRCUIT_ID`, the `SESSION_STATUS` wire value `CANCELLED`) come from the configurations and the 11.2c evidence. If one is renamed on the demo database, the query fails without writing anything.

Pre-deploy environment (B1): `EmailRouting__TestRecipients` is set; `EmailRouting__Mode` / `EmailRouting__ProductionBcc` as wanted; `Email__Smtp__Username`, `Email__Smtp__Password` and `Email__FromAddress` are set.

---

## Not checked

- No runtime run in this review: S1 (the 500 on null entries) and BACKLOG 1 and 2 come from reading the code.
- The demo database was not queried: the checks above are for you to run.
- MailKit hiding the Bcc header on the wire (backlog of the 11.4 evidence, to verify on a real Production mail).
- Swagger UI not opened: grouping read from the `ApiExplorerSettings` / `Tags` attributes only.
- Concurrency (auto link vs PUT, completion vs PUT) not exercised.
- The demo server's compose file and environment are not visible from the repository (B1).
