# Lot 11.7 inventory: rest fields still carrying the old names

Search: case-insensitive `rest_?seconds` and `rest_?after_?seconds`, over `*.cs`, `*.csv`, `*.json` (appsettings excluded), `*.md`, `*.sql`. 99 hits in 32 files before this lot's edits. Mapping: `restSeconds` becomes `restSecondsAfterSeries`, `restAfterSeconds` becomes `restSecondsAfterExercices`.

## (a) Left untouched on purpose (decision 11.AS)

Prefixed entity properties:
- `Mentora.Core/Entities/ProgramExercise.cs:21` `ProgramExerciseRestSeconds`
- `Mentora.Core/Entities/ProgramCircuit.cs:16` `ProgramCircuitRestAfterSeconds`
- `Mentora.Infrastructure/Services/ProgramService.cs:398, 418, 855, 873, 1017, 1038` read or write the prefixed entity properties (the right-hand side is already the new DTO name where it is a DTO)

HasColumnName strings and column or CHECK names:
- `Mentora.Infrastructure/Persistence/Configurations/ProgramCircuitConfiguration.cs:47-50` column `PROGRAM_CIRCUIT_REST_AFTER_SECONDS`, comment on `CK_PROGRAM_CIRCUITS_REST_AFTER_SECONDS`
- `Mentora.Infrastructure/Persistence/Configurations/ProgramExerciseConfiguration.cs:55-56` column `PROGRAM_EXERCISE_REST_SECONDS`
- `Mentora.Core/Validators/ProgramTemplate/ProgramCircuitRules.cs:13` constant `MaxRestAfterSeconds` (internal constant, never serialised; the 422 key and message at lines 48-50 already use the new name)

Older migrations, Designer files and model snapshot (never edited):
- `Migrations/20260812084549_Lot6_3_Programs.cs:164` column `PROGRAM_EXERCISE_REST_SECONDS`
- `Migrations/20260812084549_Lot6_3_Programs.Designer.cs:1343-1345`, `20260812095011_Lot6_4_GroupSessions.Designer.cs:1347-1349`, `20260813070833_Lot6_5_Agenda.Designer.cs:1355-1357`, `20260929160243_Lot9_PurchaseOrigin.Designer.cs:1368-1370`, `20260929171450_Lot9_ExerciseCatalog.Designer.cs:1680-1682`, `20260930085059_Lot10_Quiz.Designer.cs:1680-1682`: `ProgramExerciseRestSeconds` / `PROGRAM_EXERCISE_REST_SECONDS`
- `Migrations/20261004181109_Lot11_2_ProgramTreeFields.cs:23, 82, 83, 104, 132` column and CHECK `PROGRAM_CIRCUIT_REST_AFTER_SECONDS`, `CK_PROGRAM_CIRCUITS_REST_AFTER_SECONDS`
- `Migrations/20261004181109_Lot11_2_ProgramTreeFields.Designer.cs:1601-1603, 1688-1690`, `20261006001014_Lot11_3_CoachSessionPreferences.Designer.cs:1620-1622, 1707-1709`, `20261006065717_Lot11_5_SessionTemplates.Designer.cs:1620-1622, 1707-1709`: prefixed properties and column names
- `Migrations/MentoraDbContextModelSnapshot.cs:1617-1619, 1704-1706`: prefixed properties and column names

Evidence files and prompts of earlier sub-lots (history, not edited):
- `docs/review/lot9/inventory.md:69, 91, 106`: `ProgramExerciseRestSeconds`, `RestSeconds` (old exercise rest wording)
- `docs/review/lot11/11.0/inventory.md:89, 187, 196, 208, 212, 213, 460`: discussion of `RestSeconds` and `restAfterSeconds`
- `docs/review/lot11/11.2/runtime-evidence.md:23, 25, 46, 51, 52, 59, 64, 68`: old JSON names and column names as observed in 11.2
- `docs/review/lot11/11.5/preflight.md:21, 22, 27, 99`, `11.5/runtime-evidence.md:58`: old DTO and key names as observed in 11.5
- `docs/review/lot11/review-wave1.md:92, 133, 204, 209`: review text and column or CHECK names
- `docs/review/lot11/11.5/fixture.sql:47-63`: fixture used by the 11.5 run. It inserts jsonb with the OLD keys. It is a record of that run and is not reused by 11.7 (11.7 uses its own `l11rn-` fixtures). Not edited.

## (b) Still to change

Result of the search: **nothing is left in this group.**
- No embedded CSV, JSON or SQL seed data carries the keys. `ProgramTemplateSeeder` builds template bodies through the DTOs, so its bodies pick up the new names from the already-replaced code (`ProgramTemplateSeeder.cs:307, 314`). It does not rewrite existing rows, see (c).
- No string literal was missed. No Swagger remark or controller text contains either name (`Mentora.API` has no hit).
- XML remark already updated: `Mentora.Core/DTOs/ProgramTemplate/Body/ProgramTemplateCircuit.cs:6`.
- `CLAUDE.md` does not mention either name: no change.
- Two comments in the same DTOs may still describe the pause in words only (no old token): not changed.

So 4.1 reduces to confirming that, plus the migration in 4.2 and 4.3.

## (c) jsonb stores carrying the keys

| Table | Column | Carries the keys? |
|---|---|---|
| `PROGRAM_TEMPLATES` | `PROGRAM_TEMPLATE_BODY` (jsonb) | yes, nested under blocks, sessions, circuits (`restAfterSeconds`) and exercises (`restSeconds`) |
| `SESSION_TEMPLATES` | `SESSION_TEMPLATE_CONTENT` (jsonb) | yes, array of circuits (`restAfterSeconds`) with exercises (`restSeconds`) |
| `STRIPE_WEBHOOK_EVENTS` (entity `StripeWebhookEvent.Payload`) | `EVENT_PAYLOAD` (jsonb) | no: Stripe payloads, not touched by this lot |
| other jsonb columns (`COACH_IN_PERSON_LOCATIONS`, `EXERCISE_COMMON_MISTAKES`, `EXERCISE_SECONDARY_MUSCLES`, `PRODUCT_TAGS`) | string lists | no |

The PROGRAM_* tables hold the values in typed columns (`PROGRAM_EXERCISE_REST_SECONDS`, `PROGRAM_CIRCUIT_REST_AFTER_SECONDS`), not jsonb: no data change there.

Note on matching: `restBetweenRoundsSeconds` and the work fields cannot match the quoted tokens `"restSeconds"` and `"restAfterSeconds"`.
