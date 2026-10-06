# Lot 11.5 — Session template library: preflight (read-only)

Branch `feature/lot_11`, HEAD `e09087f docs(lot11): 11.4 preflight and runtime evidence`, working tree clean at start.
Nothing was written or run except reads. No migration applied, no API started.

## Points that need your decision (read first)

1. **Validator reuse needs a behavior-preserving extraction (4.1).** The circuit / exercise rules live in *private instance methods* of `ProgramTemplateBodyValidator` (`ValidateCircuits` / `ValidateExercises`, `ProgramTemplateBodyValidator.cs:207-280`), so they cannot be called from a template validator as they are. Not duplicable, so I propose to **extract them, unchanged, into a shared public class** that both validators call. This touches an existing validator (same keys, same messages, same order). Needs your OK. The alternative (wrap the circuits in a fake MICROCYCLE block and remap the 422 keys) is rejected: it drags in `DurationWeeks` and produces wrong keys.
2. **422 keys use the `Position` value, not the 0-based array index.** Existing keys are `Blocks[1].Sessions[1].Circuits[1].Exercises[2].LoadType` (`$"{path}[{position}]"`, lines 176/218/254), positions being 1-based and checked contiguous. Reusing the rules gives `Circuits[1].Exercises[2].ExerciseId` for the first circuit's 2nd exercise, not `Circuits[0]...`. I propose to keep the existing convention (identical to the program tree). Your example `Circuits[0]` was illustrative; confirm.
3. **The existing exercise-visibility failure has an empty key** (`context.AddFailure(string.Empty, "Unknown or inaccessible exercise id(s): ...")`, line 82). 11.S wants the key on the path (`Circuits[p].Exercises[q].ExerciseId`). The extracted rules will therefore *return* the list of (path, exerciseId) instead of failing, and each caller decides: the program body validator keeps its current aggregated message (no behavior change), the template validator emits one failure per path with one message for unknown and other-coach.
4. **Nature and session type are NOT enums in this codebase.** `PROGRAM_SESSIONS.PROGRAM_SESSION_TYPE/NATURE` are plain `string` properties with `varchar` + named CHECKs (`ProgramSessionConfiguration.cs:47-58`, `CK_PROGRAM_SESSIONS_TYPE` / `_NATURE` in `Lot11_2_ProgramTreeFields.cs:60-69`); wire constants are `ProgramSessionTypes.All` / `ProgramSessionNatures.All` (`Mentora.Core/Enums/ProgramSessionWire.cs:4-42`). No `EnumMappings` entry, no ValueConverter, no ToDb/FromDb exist for them. The new columns will follow that existing precedent (plain strings, same constants, same CHECK value lists), not invent a C# enum + converter. Deviation from the general rule in the prompt, justified by consistency with the tree.
5. **`sessionNode` needs two non-null values that are not "default or null".** The tree validator requires `DayOfWeek` 1..7 (`:181`) and `Position` unique contiguous from 1 (`:172`). A node with 0 would be rejected on the program write. Proposal: `dayOfWeek = 1`, `position = 1` in the node (the app overrides them when it inserts). Confirm.
6. **Content jsonb shape.** Proposed: a bare JSON array of circuits (`[ {name, position, mode, ..., exercises:[...]}, ... ]`), camelCase, produced by the same `JsonSerializerOptions` as `ProgramTemplateService.cs:18-21`. Optional extra CHECK `CK_SESSION_TEMPLATES_CONTENT` (`jsonb_typeof = 'array' AND jsonb_array_length >= 1`): not in 11.Q, I propose to add it. Confirm or drop.
7. **List details not fixed by 11.R**, following existing convention unless you object: limit default 50, max 100 (`MemberExercisesController.cs:42`, `ExerciseService.cs:155`), search 2..60 chars once trimmed (`ExerciseService.cs:18-19,161-166`), nature / sessionType filters normalized with `ToUpperInvariant` and rejected if unknown (`ExerciseService.cs:372-394` style).
8. `nature` and `sessionType` are **required** in POST/PUT (no implicit STANDARD), since the columns are NOT NULL and PUT is a full replace. In the tree `Nature` null means STANDARD; for templates I propose explicit.

## 4.1 Tree session / circuit nodes and validators

Wire classes (no ids anywhere), `Mentora.Core/DTOs/ProgramTemplate/Body/`:
- `ProgramTemplateSession.cs:3-22`: `Name`(string), `Type`(string), `Nature`(string?), `DayOfWeek`(int 1..7), `Position`(int), `Circuits`(List<ProgramTemplateCircuit>).
- `ProgramTemplateCircuit.cs:9-27`: `Name`, `Position`, `Mode` (STANDARD|INTERVAL), `Rounds`(int?), `RestBetweenRoundsSeconds`(int?), `RestAfterSeconds`(int?, 0..600), `Note`(string?), `Exercises`(List<ProgramTemplateExercise>).
- `ProgramTemplateExercise.cs:3-27`: `ExerciseId`(Guid), `Position`, `LoadType` (KG|BODYWEIGHT), `PrescribedSets`, `PrescribedReps`, `PrescribedWeightKg`(decimal?), `RestSeconds`, `WorkSeconds`, `RestWorkSeconds`, `CustomNote`.
- The PUT-program tree (`Mentora.Core/DTOs/Program/Body/UpdateProgramSession.cs:5-37`, `...Circuit.cs`, `...Exercise.cs`) is the same shape plus `Guid? Id` per node (null = insert). It converts to the id-free classes above via `ToTemplateSession/Circuit/Exercise` and is validated by the same body validator (`UpdateProgramRequestValidator.cs:38-40`). So the **id-free classes are exactly the stored content format and exactly an insertable node**: a `sessionNode` without `id` fields is accepted by the PUT program write.

Validators:
- `ProgramTemplateBodyValidator` (`Mentora.Core/Validators/ProgramTemplate/ProgramTemplateBodyValidator.cs:20`), one `RuleFor(x => x).CustomAsync(...)` (line 43). Session rules `:165-192`, circuit rules `:207-238`, exercise rules `:240-280`, positions `:284-292`, all `private`. Needs `RootContextData["DurationWeeks"]` always (line 55) and `["CoachId"]` when exercises exist (line 73): fail-fast throw, never skip.
- Rules to be reused by templates: circuit name required, mode STANDARD|INTERVAL, Rounds required when INTERVAL, RestAfterSeconds 0..600, positions contiguous, at least one exercise per circuit, LoadType (ELASTIC refused), STANDARD requires sets+reps and forbids work/rest-work, INTERVAL the reverse.

Reuse plan: new `public static class ProgramCircuitRules` in `Mentora.Core/Validators/ProgramTemplate/` with `Validate(List<ProgramTemplateCircuit> circuits, string path, Action<string,string> fail, List<(string Path, Guid ExerciseId)> exerciseRefs)`: the current bodies of `ValidateCircuits/ValidateExercises/ValidatePositions` moved verbatim (the circuit-level `Circuits` empty check stays with the callers: the body validator keeps "silently ignores empty list" at `:211`, the template validator adds "at least one circuit" on key `Circuits`). `ProgramTemplateBodyValidator` calls it with `fail = context.AddFailure` and builds its `exerciseIds` HashSet from `exerciseRefs`. Existing program/template tests of 422 keys must give identical output (I will diff the 422 bodies of one PUT program with an invalid tree before/after, at runtime if you want, otherwise by code review).

## 4.2 Exercise visibility today

- Rule everywhere for a coach: `ExerciseCoachId == null || ExerciseCoachId == coachId`: `ExerciseService.cs:33` (list), `:79` (get), `:276` (filters).
- The one-query set check for trees already exists: `ExerciseService.ResolveVisibleActiveIdsAsync` (`ExerciseService.cs:279-293`, interface `IExerciseService.cs:40`): `WHERE exerciseId = ANY(@ids) AND (COACH_ID IS NULL OR COACH_ID = @coach) AND IS_ACTIVE`, returns the visible ids. It already excludes **inactive** exercises (soft-deleted coach exercise or deactivated catalog row).
- Plan: the template validator (constructor-injected `IExerciseService`, as the body validator does at `:37-41`) collects all `(path, exerciseId)` from the shared rules, makes **one** call with the distinct ids, then adds one failure per path whose id is not in the visible set: key `Circuits[p].Exercises[q].ExerciseId`, message `"Unknown or inaccessible exercise id."` identical for unknown, other coach's and inactive (no leak). CoachId passed through `RootContextData["CoachId"]` set by the service (same as `ProgramTemplateService.cs:127-135`), fail-fast if missing. No query when the template body has structural errors? The query still runs only if at least one reference was collected; structural errors are reported together with the exercise errors (same as the body validator).

## 4.3 Nature / session type constants and the new columns

- Constants: `ProgramSessionTypes` (`ProgramSessionWire.cs:4-17`: PRESENTIEL_SOLO, PRESENTIEL_GROUPE, VISIO, AUTONOME, PRESENTIEL_OU_VISIO) and `ProgramSessionNatures` (`:36-41`: STANDARD, EVALUATION). The tree validator additionally rejects `VISIO_GROUPE` explicitly (`ProgramTemplateBodyValidator.cs:197`) and requires membership in `All` (`:203`).
- No `EnumMappings` / ToDb / FromDb for them (grep: none). Pattern for real enums is `ProgramTemplateConfiguration.cs:33-41,87-109` (if-chains, never switch), used for `ProgramGoal`.
- New columns: `SESSION_TEMPLATE_NATURE varchar(20) NOT NULL`, `SESSION_TEMPLATE_SESSION_TYPE varchar(40) NOT NULL` (lengths as in `PROGRAM_SESSIONS`), C# `string`, validated with `ProgramSessionTypes.All` / `ProgramSessionNatures.All` (the template validator reuses the same two checks; the VISIO_GROUPE message too). CHECK value lists copied from `Lot11_2_ProgramTreeFields.cs:60-69`.

## 4.4 Conventions to copy

- **Invalid query value → 400.** `InvalidOperationException` thrown by the service, mapped by `GlobalExceptionMiddleware.cs:57-61` (400). Examples `ExerciseService.cs:366-370` (scope), `:372-407` (filters), invalid cursor `:419-430` (400), limit `:155-156` (400). 422 is only FluentValidation `ValidationException` (`:34-56`). So `scope`, `nature`, `sessionType`, `search` length, `limit`, `cursor` invalid → **400**. Existing scope values are uppercase and case-sensitive (`ALL/MENTORA/MINE`); the new route uses `mine|platform|all`, case-insensitive, normalized with `ToLowerInvariant`.
- **Limit**: default 50 / max 100 (member exercise list). Quiz list uses 20 (`QuizBasicController.cs:95`); I take the exercise list convention.
- **Cursor**: base64 of JSON, per-service private record + `Encode/Decode` with `FormatException/JsonException/ArgumentException` → `InvalidOperationException("Invalid cursor format.")` (`ExerciseService.cs:411-457`). Response `{ items, nextCursor }` (`ExerciseListResponseDto(items, nextCursor)`, `:258`). No shared helper exists; I will add the same private pair in the new service (cursor payload `(SortKey, Id)`).
- **DELETE**: `204 NoContent` with no body (`CoachProgramTemplatesController.cs:110-120`, `ProducesResponseType(204)`); 404 via `NotFoundException`. 11.R says physical delete: same status and body, only the service differs (`db.Remove`).
- **POST**: `Created($"/api/v1/coach/...{id}", new { success = true, data = result, error = (string?)null, statusCode = 201 })` (`:77-79`). Duplicate also 201 with Location.
- **Controller model**: `CoachProgramTemplatesController.cs:9-14` attributes: `[ApiController] [Route(...)] [Authorize(Policy = "CoachOnly")] [ApiExplorerSettings(GroupName = "coach")] [Tags("Coach — Program templates")]`, coachId from `User.FindFirst("coachId")`, `[ProducesResponseType]` list. New tag: `"Coach — Session templates"`.
- **Service model**: `ProgramTemplateService.cs` (db injected, `NotFoundException` on `Id && CoachId == coachId` for writes so platform/other coach = 404 and ownership wins over body validation `:92-99`; `ValidateAsync` with RootContextData `:127-135`; camelCase `JsonOptions` `:18-21`). DI: `Program.cs:196-207` region (`AddScoped<I..., ...>`), validators come from `AddValidatorsFromAssemblyContaining<ProductRequestValidator>()` (`Program.cs:207`, Core assembly scan).
- **Migration style**: `Lot11_3_CoachSessionPreferences.cs:35-40` (CHECK via `migrationBuilder.Sql("""ALTER TABLE ... ADD CONSTRAINT "CK_..." CHECK (...);""")`, Down `:46` drops it before dropping the columns). Partial unique index via `migrationBuilder.Sql`: `Lot9_ExerciseCatalog.cs:353-355` (`CREATE UNIQUE INDEX "UX_EXERCISES_SOURCE_CODE" ... WHERE "EXERCISE_SOURCE_CODE" IS NOT NULL;`); `Lot6_5_Agenda.cs:26-28`. Named CHECK on type/nature: `Lot11_2_ProgramTreeFields.cs:57-69`, Down `:105-108`. The Lot10 migration (`20260930085059_Lot10_Quiz.cs`) has no partial index to cite beyond these; Lot9 and Lot6_5 are the precedents. The migrations folder is `Mentora.Infrastructure/Persistence/Migrations/`. Model snapshot gets the new entity; the partial index and CHECKs are **not** declared in the configuration (so EF generates no conflicting weaker index), with a comment, as `ProgramTemplateConfiguration.cs:67-70`.
- **Configuration pattern**: `ProgramTemplateConfiguration.cs` (UPPERCASE column names, `gen_random_uuid()` default `:16-18`, jsonb string `:49-52`, `HasDefaultValue(true)` + C# initializer, FK `OnDelete(Restrict)` `:81-84`, `HasIndex(...).HasDatabaseName(...)`), picked up by `ApplyConfigurationsFromAssembly` (`MentoraDbContext.cs:74`).
- **Accent-insensitive search**: `ExerciseSearch.cs:28-29,44-46`: `MentoraDbFunctions.Translate(col, AccentFolding.Accented, AccentFolding.Plain).ToLower()` on both column and term, `EF.Functions.Like(..., "%"+t+"%", "\\")` with `ExerciseSearch.EscapeLike` (`:31-32`). C# twin `AccentFolding.Fold`. `Translate` is mapped by `modelBuilder.HasDbFunction` (`MentoraDbContext.cs:55`).

## 4.5 Route collision check

`grep session-templates` over all `*.cs`: no match. All `[Route]` in `Mentora.API/Controllers/Coach` (listing done): `coach/exercises`, `coach/exercise-filters`, `coach/program-templates`, `coach/programs` + `coach/product-programs`, `coach` (CoachProgramsController: literals `members/{id}/training-programs`, `training-programs/{id}`), `coach/program-sessions`, `coach/sessions` (3 controllers: `group`, `{sessionId:guid}`, `{id}/cancel`, `.../visio-token`, `.../participants`), `coach/session-slots`, `coach/settings/session-preferences`, `coach/products`, `coach/product-packs`, `coach/parameters`, `coach/me`, `coach/agenda`, `coach/conversations`, `coach/members/{id}/...`. Controllers in `Auth`, `Member`, `Internal`, `Quiz` have no `coach` route. `coach/session-templates` is a distinct literal segment (not `coach/sessions`, not `coach/session-slots`), and no `{x:guid}` catch-all sits at `coach/{...}`. **No collision.** Sub-route `{id:guid}/duplicate` does not conflict with `{id:guid}`.

## 4.6 DTOs, 422 keys, files

New namespace `Mentora.Core.DTOs.SessionTemplate`. JSON camelCase (framework default), reuses `ProgramTemplateCircuit` for circuits.

```csharp
public class SessionTemplateRequest            // POST and PUT (full replace)
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string Nature { get; set; } = null!;
    public string SessionType { get; set; } = null!;
    public List<ProgramTemplateCircuit> Circuits { get; set; } = [];
}   // no isActive, no coachId, no sourceCode, no isPlatform: absent from the DTO (not editable)

public record SessionTemplateItemResponse(Guid Id, string Name, string? Description, string Nature,
    string SessionType, bool IsPlatform, string? SourceCode, int CircuitCount, int ExerciseCount,
    DateTime CreatedAt, DateTime UpdatedAt);

public record SessionTemplateListResponse(List<SessionTemplateItemResponse> Items, string? NextCursor);

public record SessionTemplateResponse(Guid Id, string Name, string? Description, string Nature,
    string SessionType, bool IsPlatform, string? SourceCode, int CircuitCount, int ExerciseCount,
    DateTime CreatedAt, DateTime UpdatedAt,
    List<ProgramTemplateCircuit> Circuits,
    ProgramTemplateSession SessionNode);
// SessionNode = { name, type = SessionType, nature, dayOfWeek = 1, position = 1, circuits }.
// ProgramTemplateSession has no Id/SessionId field at all, so none is serialized.
```

422 keys (FluentValidation, via `ValidationException` → `errors` dictionary, `GlobalExceptionMiddleware.cs:34-56`):

| Rule | Key |
|---|---|
| name empty/whitespace or > 100 once trimmed | `Name` |
| description > 1000 once trimmed (empty → null, no error) | `Description` |
| nature not in STANDARD/EVALUATION (or null) | `Nature` |
| sessionType not in the 5 values / VISIO_GROUPE / null | `SessionType` |
| no circuit (null or empty) | `Circuits` |
| positions of circuits not contiguous | `Circuits` |
| circuit rules (name, mode, rounds, restAfterSeconds, positions, no exercise, per-exercise loadType, sets/reps, work/rest) | `Circuits[p].Name`, `.Mode`, `.Rounds`, `.RestAfterSeconds`, `.Exercises`, `Circuits[p].Exercises[q].LoadType`, `Circuits[p].Exercises[q]` (STANDARD/INTERVAL field mix) ... exactly as in the tree, minus the `Blocks[..].Sessions[..]` prefix |
| exercise unknown / other coach's / inactive | `Circuits[p].Exercises[q].ExerciseId`, same message for all |

Errors on a null body: framework 400 as for other endpoints.

Duplicate name: `(copie)` suffix is 8 characters, so the original part is cut to 92, `TrimEnd` applied before appending (avoids a double space), cut done on UTF-16 chars without splitting a surrogate pair.

Files to create:
- `Mentora.Core/Entities/SessionTemplate.cs`
- `Mentora.Core/DTOs/SessionTemplate/*.cs` (Request, ItemResponse, ListResponse, Response)
- `Mentora.Core/Validators/ProgramTemplate/ProgramCircuitRules.cs` (extraction)
- `Mentora.Core/Validators/SessionTemplate/SessionTemplateRequestValidator.cs`
- `Mentora.Core/Interfaces/ISessionTemplateService.cs`
- `Mentora.Infrastructure/Persistence/Configurations/SessionTemplateConfiguration.cs`
- `Mentora.Infrastructure/Services/SessionTemplateService.cs`
- `Mentora.API/Controllers/Coach/CoachSessionTemplatesController.cs`
- migration `Lot11_5_SessionTemplates` (+ designer)

Files to modify:
- `Mentora.Core/Validators/ProgramTemplate/ProgramTemplateBodyValidator.cs` (calls the extracted rules; output unchanged)
- `Mentora.Infrastructure/Persistence/MentoraDbContext.cs` (`DbSet<SessionTemplate>`)
- `Mentora.Infrastructure/Persistence/Migrations/MentoraDbContextModelSnapshot.cs` (generated)
- `Mentora.API/Program.cs` (one `AddScoped` line)
- `CLAUDE.md` (a line on the 11.R/11.S rules for the 11.6 seeder: platform templates reference Mentora catalog exercises only) — only if you want it; the rule will in any case be a comment in the service and the entity.

`Coach` entity is not modified (FK configured with `HasOne<Coach>().WithMany().HasForeignKey(...).OnDelete(Restrict)`, no navigation collection).

Persisted rule for 11.6, in the code: "a platform template may only reference Mentora catalog exercises (COACH_ID IS NULL exercises)"; duplicate does not re-validate (11.S).

## 4.7 Expected list SQL (scope all, with search) and proof of one query

Single LINQ query, no `Include`, content column read in the same row; counts (`circuitCount`, `exerciseCount`) computed in C# from the deserialized content of the at most `limit + 1` rows, so no extra query:

```sql
SELECT s."SESSION_TEMPLATE_ID", s."SESSION_TEMPLATE_NAME", s."SESSION_TEMPLATE_DESCRIPTION",
       s."SESSION_TEMPLATE_NATURE", s."SESSION_TEMPLATE_SESSION_TYPE", s."COACH_ID",
       s."SESSION_TEMPLATE_SOURCE_CODE", s."SESSION_TEMPLATE_CONTENT",
       s."SESSION_TEMPLATE_CREATED_DATE", s."SESSION_TEMPLATE_UPDATED_DATE",
       lower(translate(s."SESSION_TEMPLATE_NAME", 'àâäÀÂÄ...', 'aaaAAA...')) AS "SortKey"
FROM "SESSION_TEMPLATES" AS s
WHERE (s."COACH_ID" = @coachId OR (s."COACH_ID" IS NULL AND s."SESSION_TEMPLATE_IS_ACTIVE"))
  AND (@nature IS NULL OR s."SESSION_TEMPLATE_NATURE" = @nature)          -- only when the filter is given
  AND (@sessionType ...)                                                  -- only when given
  AND lower(translate(s."SESSION_TEMPLATE_NAME", ...)) LIKE '%' || lower(translate(@term, ...)) || '%' ESCAPE '\'
  AND (lower(translate(name)) > @cursorKey
       OR (lower(translate(name)) = @cursorKey AND s."SESSION_TEMPLATE_ID" > @cursorId))   -- only with a cursor
ORDER BY lower(translate(s."SESSION_TEMPLATE_NAME", ...)), s."SESSION_TEMPLATE_ID"
LIMIT @limit + 1
```

`scope=mine` replaces the visibility predicate with `COACH_ID = @coachId`; `scope=platform` with `COACH_ID IS NULL AND IS_ACTIVE`. The cursor is base64 JSON `(SortKey, Id)`; `nextCursor` is built from the last returned row's folded key (same expression computed in C# is **not** used: the key is selected from SQL, so there is no C#/SQL folding drift). Cursor comparison on the folded key uses the same column expression as `ORDER BY`, so order and cursor agree.

Proof of ONE query for a page: run the local API with the three mandatory session variables plus the EF command logger set by environment variable only (`Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command=Information`, session variable, never in a file, redirected to a gitignored `*.log`), send a single list request, then extract only the lines `Executed DbCommand` / `SELECT` between that request's start and end and show that exactly one `Executed DbCommand` occurs for the request (the JWT is validated statelessly; I will check that the authorization path issues no query and show the count). Only SELECT lines are read, never parameter values with secrets; no OTP console line or email-log line is printed. The same count is shown for GET by id (1 query) and for POST (1 exercise-visibility SELECT + 1 INSERT, no more).

## Other risks / notes

- The new endpoint list returns the full `content` jsonb from SQL for each row (needed for the two counts). Payload on the wire stays small (counts only); acceptable at max 100 rows; if templates grow large, a `jsonb_array_length` function mapping is the fix (backlog).
- Search on name uses a `%term%` scan on a table of tens/hundreds of rows: no index needed now.
- Folded-key ordering uses the DB collation for `>`/`=` on text, same as the existing exercise cursor (`ExerciseService.cs:180`); consistent between ORDER BY and the cursor.
- `limit` max 100 and `search` 2..60 chars are my reading of "following existing list endpoints" (point 7).

## STOP

Phase A ends here. Waiting for your approval (especially points 1, 2, 3, 5, 6) before Phase B.
