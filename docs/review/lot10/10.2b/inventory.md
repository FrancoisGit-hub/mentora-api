# Lot 10.2b - preflight inventory (read-only, before any code change)

**a. Branch / status.** `feature/lot_10`, clean, `git log --oneline -3`: `246e2bd docs(lot10): 10.2 inventory and runtime evidence`, `b7b8be3 feat(lot10): basic quiz endpoints, device mode and claim`, `e10e00c docs(lot10): 10.1 inventory and runtime evidence`.

**b. Endpoints accepting `search` (lot 9 exercises) - before the change.**

| Endpoint | Service | Fields matched | How | Order | Cursor |
|---|---|---|---|---|---|
| `GET /api/v1/member/exercises` (`MemberExercisesController.GetAll`) | `ExerciseService.ListForMemberAsync` | `EXERCISE_NAME`, `EXERCISE_NAME_EN`, `EXERCISE_SEARCH_TAGS` | `ILIKE '%term%'` (LIKE metacharacters escaped), term trimmed, 2 to 60 chars else 400 | `ORDER BY name, id` (no relevance) | base64 JSON `{"Name","ExerciseId"}` |
| `GET /api/v1/coach/exercises` (`CoachExercisesController.GetAll`) | `ExerciseService.ListForCoachAsync` | `EXERCISE_NAME` only | `ILIKE '%term%'` | `ORDER BY name`, no pagination (full list) | none |

`GET .../member/exercises/filters` and `GET .../{id}` take no `search`. `CoachProgramTemplatesController` has a `search` too, but it is for program templates, not lot 9 exercises (out of scope).

**Accent handling today: there is none.** The brief assumed unaccent/normalization exists; it does not. No `unaccent` extension is installed (`pg_extension`: plpgsql only), no normalized column, no collation trick (database collation `en_US.utf8`). Matching is `ILIKE`: case-insensitive, accent-**sensitive**. Baseline capture confirms: `search=dev` returns **0** results, `search=DÉV` 22, `search=dév` 22.

**c. Read-only SQL for `Chest press machine`** (`inventory-queries.sql` / `inventory-queries-output.txt`):
- `EXERCISE_NAME` = `Chest press machine`, `EXERCISE_NAME_EN` = `Machine Chest Press`, `EXERCISE_SEARCH_TAGS` = `chest press ; machine pectoraux ; développé machine`.
- The field that contains "dév" is **`EXERCISE_SEARCH_TAGS`** (`développé machine`) - name and English name do not (also false once accents are folded).
- Today `search=dév` matches 22 active exercises: 20 by name, 2 through the tags only (`Chest press machine` and `Shoulder press machine`); as they sort by name, `Chest press machine` came first.

**d. Names / alternative names versus free text.**

| Field | Nature | Searched today (member / coach) | After |
|---|---|---|---|
| `EXERCISE_NAME` | French name | yes / yes | yes (ranks 0-2) |
| `EXERCISE_NAME_EN` | alternative name (English) | yes / no | yes (rank 3) |
| `EXERCISE_SEARCH_TAGS` | alias / synonym list (`;` separated, e.g. `développé couché ; bench press ; pecs ; barre`) | yes / no | yes (rank 3) |
| description, instructions, coach tip, precautions, breathing, common mistakes, category, movement pattern, type, laterality, starting position, grip type, secondary muscles | free text / classification | **no** (never searched) | no |

So no free-text field was searched, contrary to the suspicion in the brief; the "more than the name" behaviour came from the English name and the tags. Judgement call to flag: `EXERCISE_SEARCH_TAGS` is the sheet's alias/synonym column (`tags_recherche`) and I kept it as an alternative-name field, but it also holds some keyword-type entries (muscles, equipment, level: `cardio`, `fessiers`, `débutant`, `barre`...; 248 exercises have 2 to 4 tags). Since it now only ranks 3, such a match can never outrank a name match. If you prefer to drop the tags (English name only), it is a one-line change in `ExerciseSearch.WithRank`.

Resulting field list: **name (ranks 0-2), English name and search tags (rank 3)**.

**e. Baseline build.** `dotnet build --no-incremental`: **0 errors, 19 warnings**.

**f. Baseline capture before any code change** (`baseline/`, script `capture.py`, API on the unchanged code, run start in `baseline-run-start.txt`, real OTP JWTs of `member@mentora.fr` and `coach@mentora.fr`): member without search (limit 50 and 7, every page), `search` = dév / dev / DÉV / blank / no match, `dév` with limit 2, `dév` + Haltères, malformed cursors; coach without search, dév / dev / DÉV / blank / no match, dév + Haltères. API stopped afterwards.
