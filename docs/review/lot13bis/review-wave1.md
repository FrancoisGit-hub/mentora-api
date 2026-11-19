# Lot 13 bis review, wave 1 (read-only): 13bis.1 (Biceps / Triceps split) and 13bis.2 (Dupliquer button)

- Date: 2026-11-18
- Branch: `feature/lot_13bis` (no upstream), base `origin/main` = `189a00155e47f8ddb12a6fa8a867bad08cac5994`
- Method: code reading, git with `--no-optional-locks`, one `dotnet build --no-incremental`. No database, no run, no network.

---

## 1. Scope

Commits `origin/main..feature/lot_13bis` (exactly 5, as expected):

| Commit | Title |
|---|---|
| 3ec4140 | docs(lot13bis): 13bis.2 runtime evidence |
| 860ee57 | feat(lot13bis): 13bis.2 Dupliquer button in the admin exercise form (prefilled create form) |
| 302c8e6 | docs(lot13bis): 13bis.1 runtime evidence, before and after snapshots, parity |
| 3eb8339 | feat(lot13bis): 13bis.1 split muscle group Bras into Biceps and Triceps (data patch Lot13bis_SplitArms) |
| f9103f7 | docs(lot13bis): 13bis.0 read-only inventory |

Tree clean before and after the review (`git status --short` empty).

Diff stat of the whole diff (`git diff --stat origin/main...feature/lot_13bis`), 9 files, 1392 insertions, 3 deletions:

| File | Lines |
|---|---|
| Mentora.API/Program.cs | +1 |
| Mentora.Admin/Pages/Exercices/Form.razor | +130 / -3 |
| Mentora.Infrastructure/Seeding/MuscleGroupSeeder.cs | +6 |
| Mentora.Infrastructure/Seeding/SplitArmsPatch.cs | +241 (new) |
| docs/review/lot13bis/13bis.0/inventory.md | +356 |
| docs/review/lot13bis/13bis.1/after.txt | +213 |
| docs/review/lot13bis/13bis.1/before.txt | +162 |
| docs/review/lot13bis/13bis.1/runtime-evidence.md | +240 |
| docs/review/lot13bis/13bis.2/runtime-evidence.md | +43 |

Reviewed code: the four code files. Excluded from the code review: `*.Designer.cs`, `*ModelSnapshot.cs`, `*.csv` (none in the diff) and `docs/review/**` (read for context; included in the searches of sections F and G).

Proof that nothing else changed: `git diff --name-only origin/main...feature/lot_13bis -- '*Migrations*' '*ModelSnapshot.cs' '*.csv' 'Mentora.Admin/wwwroot'` returns an empty list. No migration, no snapshot, no CSV, no `wwwroot` file in the lot.

## 2. Verdict

**Ready to deploy**: 0 BLOCKER, 1 SHOULD (a missing log line, cheap), 8 BACKLOG. The pre-deployment SQL of section 5.1 must be run on PROD right before the deployment because of the daily catalogue edits (findings F2 and F3 rely on it).

## 3. Findings

### F1. SHOULD. A listed code that does not exist is skipped without any log line

- Where: `Mentora.Infrastructure/Seeding/SplitArmsPatch.cs:114-117` (primary loop) and `:139-145` (secondary loop).
- What is wrong: both loops iterate over the exercises in the database and test membership in the code lists. A code of the three lists that is absent from the Mentora exercises (deleted, never created, coach-owned, code changed) produces no line at all. Only codes that exist but are not on "Bras" get the "not applicable" info line (`:120`, `:151`). Decision 13bis.H says every skipped change is reported.
- Why it matters for the Beta: on PROD the operator reads the summary (`primaryToTriceps=13` instead of 14, for example) and has no line telling which code is missing; he has to rerun SQL to find it. The data written stays correct.
- Minimal fix: after the loops, for each code of the three lists not found among `exercises` (by `ExerciseSourceCode`), log `{Tag} {Patch} {Code} not found among Mentora exercises (not applicable)`; no counter change.
- Confidence: high.

### F2. BACKLOG. An unlisted Mentora exercise on "Bras" stays on Biceps silently

- Where: `SplitArmsPatch.cs:114-136`; legacy rule `Mentora.Infrastructure/Services/AdminExerciseService.cs:835-839` with `LegacyExerciseColumns.cs:22-24`.
- What: an exercise created or regrouped by the admin since the 18/11 read, with "Bras" as primary group or secondary link and not in a list (for example a new triceps extension), stays on the renamed row (Biceps), as decided (13bis.C, 13bis.D). It is not logged. The only safety net, the warning "primary group stays Biceps but the legacy column is TRICEPS" (`:130-136`), never fires for admin-made exercises: the admin always passes `principalMuscle = null` (`AdminExerciseService.cs:839`), so a "Bras" exercise saved in the admin always carries `BICEPS`.
- Why it matters: such an exercise would show "Biceps" in the apps. Data stay valid; only the classification can be wrong.
- Mitigation (no code): query 5.1.4 and 5.1.5 list every "Bras" exercise with a `listed` flag; the product owner reviews the unlisted ones right before the deployment, and fixes any of them in the admin after it.
- Confidence: high.

### F3. BACKLOG. Name matching of the guard: exact for "Bras", case-only fold for the clash

- Where: `SplitArmsPatch.cs:69` (`g.MuscleGroupName == ArmsName`, ordinal and exact) and `:77-78` (`OrdinalIgnoreCase`, no trim, no accent fold).
- What: "biceps" or "TRICEPS" next to "Bras" is caught (startup refused, as decided). "Biceps " (trailing space) or "Bíceps" (accent) is not caught; the rename then succeeds (the unique index `UX_MUSCLE_GROUPS_NAME` is exact) and two near-identical groups remain. A row "bras" without an exact "Bras" would lead to "nothing changed" plus the marker, so the split would never run.
- Why it is only backlog: every startup of the current PROD code runs `MuscleGroupSeeder`, which get-or-creates the exact name "Bras" (`MuscleGroupSeeder.cs:41-56`), so an exact "Bras" always exists; and there is no admin route to create or rename a group (inventory C.2). A near-duplicate could only come from hand-written SQL. Query 5.1.3 checks it.
- Confidence: high on the behaviour, low on the probability.

### F4. BACKLOG. Two independent "Bras" literals must stay equal

- Where: `MuscleGroupSeeder.cs:23` (list entry) and `SplitArmsPatch.cs:23` (`ArmsName`), compared at `MuscleGroupSeeder.cs:57`. Also `LegacyExerciseColumns.cs:22` and `SeedingText.cs:49-50`.
- What: the marker name is shared (`SplitArmsPatch.Name`, `MuscleGroupSeeder.cs:47`), good. The group name is not: if the list entry is ever edited ("Bras " or a translation) and `ArmsName` is not, the seeder recreates an empty group at each startup on a split database.
- Fix later: use `SplitArmsPatch.ArmsName` in the seeder list.
- Confidence: high (latent only, no current defect).

### F5. BACKLOG. Rename-only mirror rewrites have no per-exercise line

- Where: `SplitArmsPatch.cs:186-214`.
- What: `mirrorsRewritten` (71 locally) counts the 42 moved, the 5 both and the 24 Biceps-only exercises whose mirror changes from "Bras" to "Biceps"; the 24 have no individual line. Accepted in practice by the evidence (section 6.5 lists them), noted for completeness of 13bis.H.
- Confidence: high.

### F6. BACKLOG. Rollback to `mentora-api:lot13` recreates an empty "Bras" that survives the roll-forward

- Where: old `MuscleGroupSeeder` (same file at `189a001`, `:41-56`), new `MuscleGroupSeeder.cs:57`.
- What: see section 5.5. The old seeder recreates "Bras" (sort 4, `BICEPS`). Back on lot 13 bis, the marker exists, so the patch is skipped and the new seeder only stops creating "Bras"; it never removes it. Anything the product owner assigned to "Bras" during the rollback window stays there.
- Fix: procedure of section 5.5; no code change needed now.
- Confidence: high.

### F7. BACKLOG. A previous flash message survives the "Dupliquer" navigation

- Where: `Mentora.Admin/Pages/Exercices/Form.razor:542-554` (`_flash = UiState.TakeFlash() ?? _flash`), `LoadAsync` `:560-571` does not clear `_flash`.
- What: after "Exercice enregistré." on the edit page, a click on "Dupliquer" opens the copy form with that green flash still above the copy banner, although the copy is not saved. The same pattern already existed for other route changes inside the component; 13bis.2 makes it more visible.
- Minimal fix: in `OnParametersSetAsync`, when the key changes, reset `_flash` before reading `UiState.TakeFlash()`.
- Confidence: medium (code path read, not run).

### F8. BACKLOG. Name truncation can split a surrogate pair

- Where: `Form.razor:670-673` (`baseName[..room].TrimEnd()`).
- What: arithmetic is right (section 4, I.4), but the cut counts UTF-16 units: a source name of 113 to 120 units with an emoji or other non-BMP character crossing position 112 leaves a lone high surrogate before " (copie)". JSON serialization or validation of that name can then fail or store a replacement character. Exercise names with non-BMP characters are unlikely in this catalogue.
- Minimal fix: if `char.IsHighSurrogate(baseName[^1])`, drop that last char before `TrimEnd`.
- Confidence: low (not exercised).

### F9. BACKLOG. "Dupliquer" copies the saved version, not the unsaved edits

- Where: `Form.razor:643-647`, `:574-575`.
- What: on an edit page with unsaved changes, "Dupliquer" goes through the leave dialog and the copy is built from the server detail; the unsaved edits are lost if the user confirms. Consistent with the decision "duplication in the admin front only, prefilled from the source"; mentioned so that the product owner knows to save first.
- Confidence: high.

## 4. Verified OK

### A. SplitArmsPatch

1. **Transaction**: `RunAsync` takes the scoped `MentoraDbContext` and passes it to `DataPatchRunner.RunOnceAsync` (`SplitArmsPatch.cs:59-64`), same shape as `ReferenceNormalizationPatch.cs:55-60`. `DataPatchRunner.cs:25-29` opens the transaction on that context, runs `apply`, adds the marker, saves and commits. The only `SaveChangesAsync` inside the patch (`SplitArmsPatch.cs:101`) runs on the same context inside that transaction; no raw SQL, no second context. A throw leaves `await using var transaction` without commit, so everything rolls back, marker included. The clash guard throws before any change (`:81-84`). `MentoraDbContext` has no `SaveChanges` override that would stamp dates (grep: no `SaveChanges`, `UpdatedDate` or `QueryTrackingBehavior` in `MentoraDbContext.cs`).
2. **Fixed lists**, copied from the code:
   - Primary to Triceps (`:37-41`): EX009, EX118, EX119, EX120, EX121, EX122, EX123, EX124, EX125, EX126, EX127, EX128, LG18, LG19. 14 codes, equal to the decision.
   - Secondary moved to Triceps (`:44-51`): EX001, EX012, EX228, EX065, EX066, EX067, EX075, EX076, EX077, EX078, EX079, EX080, EX081, EX082, EX083, EX084, EX085, EX213, EX220, EX245, EX016, EX017, EX018, EX019, EX020, EX021, EX022, EX023, EX024, EX025, EX026, EX027, EX028, EX029, EX030, EX031, EX032, EX033, EX034, EX035, EX036, EX037. 6 + 11 + 3 + 22 = 42 codes, equal to the decision.
   - Both (`:54-57`): EX013, EX227, EX233, EX237, EX238. 5 codes, equal.
   - No missing, extra or duplicated code; no code in two lists. Note: a duplicate in a `HashSet` initializer would be silently ignored, not detected; none exists.
3. **Scope of writes**: one exercise query, filtered `ExerciseCoachId == null` (`:105-109`); every later write is on that list. `ExerciseUpdatedDate` is never assigned. No `AdminAuditLog` use in the file. The legacy column is assigned only at `:125`, inside the primary loop, after the `PrimaryToTricepsCodes` and "on Bras" checks, so only for the 14 primary moves. `MuscleGroupUpdatedDate` is set on the renamed row (`:91`, accepted at 3.3).
4. **Drift tolerance**:

| Case | What the patch does | Safe |
|---|---|---|
| Listed primary code no longer on "Bras" | info line "primary not applicable: primary group is X" (`:118-122`), not counted, unchanged | yes |
| Listed secondary code no longer linked to "Bras" | info line "secondary not applicable: no link to Bras" (`:147-152`), unchanged | yes |
| Exercise linked to "Bras" whose primary becomes Triceps | cannot happen: a primary "Bras" exercise has no "Bras" link (validator `AdminExerciseValidators.cs:56-60`, normalization `ReferenceNormalizationPatch.cs:122-123`) and the primary and secondary lists do not overlap; defensive refusal anyway (`:222-223`), logged as skipped | yes |
| Listed code already linked to Triceps | impossible before the patch; refused as duplicate (`:224-225`), logged as skipped | yes |
| "Both" exercise with 6 links | `countAfter = 7 > 6`, skipped with a warning line (`:226-228`, `:156-161`), Biceps link kept; a "move" never changes the count | yes |
| Mentora exercise on "Bras" (primary or secondary), not listed | stays on Biceps (renamed row); not logged unless primary Biceps with legacy TRICEPS (`:130-136`); see F2 | yes, data valid |
| Inactive exercise | no filter on `ExerciseIsActive`: treated like an active one, consistent with the lists | yes |
| Listed code that does not exist | silently ignored; see F1 | yes, no write |

5. **Guard 13bis.G**: "Bras" found by exact ordinal name (`:69`); no row: info line, summary with all zeros plus "(no 'Bras' row, nothing changed)", marker written (`:70-74`). Clash: `OrdinalIgnoreCase` on "Biceps" and "Triceps", so "biceps" or "TRICEPS" in another case is caught; trailing space or accent is not (F3).
6. **Mirror 13bis.E**: patch `:202-207` (`OrderBy(MuscleGroupSortOrder).ThenBy(MuscleGroupName, StringComparer.Ordinal)`, `null` when empty) equals the admin rule `AdminExerciseService.cs:806-809` and `:824`, and `ReferenceNormalizationPatch.cs:129-134`. Rewritten set: every Mentora exercise whose links (after the moves) touch Biceps or Triceps, plus any Mentora mirror naming "Bras" without a link (warning, `:195-200`); written only when different (`:209-213`). After the patch a Mentora mirror cannot contain "Bras": every mirror that contained it is either linked to the renamed row or caught by `:192`, and is rebuilt from current names. Coach mirrors are out of scope by design.
7. **Logs 13bis.H**: summary built at `:232-235`, format `renamed= created= primaryToTriceps= secondaryToTriceps= secondaryBoth= skipped= mirrorsRewritten= warnings=`, printed by `DataPatchRunner.cs:31` as `[PATCH] Lot13bis_SplitArms applied: ...`. One line per primary move (`:127`), secondary move (`:177`), both (`:182`), skip (`:159`), warning (`:134`, `:198`). Values logged: source codes (or the exercise name when there is no code), group names, counts. No user data, no token, no address.

### B. Seeders and other uses

1. `MuscleGroupSeeder.cs:47` reads the marker through `SplitArmsPatch.Name` (single constant); `:57` skips only "Bras" and only when the marker exists. See F4 for the name literal.
2. New database order (`Program.cs:568-574`): `MuscleGroupSeeder` (marker absent, "Bras" created), `ExerciseReferenceSeeder`, `LegacyExerciseCodePatch`, `CatalogSeeder`, `ExerciseSeeder`, `ReferenceNormalizationPatch`, then `SplitArmsPatch`. The patch therefore sees the same shape as on an existing database. The parity run (13bis.1 evidence section 8: 275 codes, 0 difference) agrees with the code.
3. Remaining uses of "Bras", BICEPS and TRICEPS in code (CSV and migrations excluded):

| File:line | Use | After the rename |
|---|---|---|
| `LegacyExerciseColumns.cs:22-24` | triceps rule keyed on the name "Bras" | dead on a split database (no group is named "Bras"); legacy then comes from the group legacy code: Biceps -> `BICEPS`, Triceps -> `TRICEPS` (`:25-26`, `:40-41`). Correct. |
| `MuscleGroupSeeder.cs:23` | default "Bras", BICEPS | created only before the split (marker check). Correct. |
| `SeedingText.cs:49-50` | legacy BICEPS and TRICEPS -> "Bras" for `ExerciseSeeder.cs:300, 311` | create-only; on a split database a recreated LG15 to LG19 would get no primary group (accepted decision). No throw. |
| `CatalogSeeder.cs:356-413` | groups by name, case insensitive | create-only; on a split database EX116 logs the accepted `[CATALOG]` warning; nothing written. |
| `ProgramTemplateSeeder.cs:149, 154` | session and circuit display names | text only, unaffected. |
| `EnumMappings.cs:244-272`, `ExerciseConfiguration.cs:201-220`, `ExerciseRequest.cs:11` | legacy wire and database values | unchanged, still 13 values. |
| `ExerciseSeeder.cs:97-119` | LG legacy enums | create-only, unchanged. |
| `SplitArmsPatch.cs` | the patch | runs once. |

   Admin save path after the patch: `AdminExerciseService.cs:835-839`. On update with unchanged primary group and category, the stored legacy value is kept (`TRICEPS` for the 14 moved rows, `BICEPS` for the others). Otherwise `LegacyExerciseColumns.MuscleGroupFor(category, primaryGroup, null)`: a cardio category gives `CARDIO`; primary Biceps gives `BICEPS` (legacy code of the renamed row, `SplitArmsPatch.cs:90`); primary Triceps gives `TRICEPS` (`:96`). This fixes the old flip from TRICEPS to BICEPS described in inventory A.5.
4. Mobile contract: no DTO, controller or service of `Mentora.API` or `Mentora.Infrastructure/Services` is in the diff. Filters list the groups from the table (`ExerciseService.cs:332-335`, fields `muscleGroupId`, `name`, `sortOrder`); detail `primaryMuscleGroup` from the table (`ExerciseService.cs:433`) and `secondaryMuscles` from the mirror. Only values change ("Biceps" instead of "Bras", one more group "Triceps" with the same sort order 4, ordered before "Avant-bras"). No API code keys behaviour on the name "Bras" (only `LegacyExerciseColumns.cs:22`, which becomes inert). Same-sort ties order by name: "Biceps" before "Triceps" in any collation, and in the ordinal mirror.

### C. Program.cs

`Program.cs:574`: `SplitArmsPatch.RunAsync` right after `ReferenceNormalizationPatch` (`:573`), inside the same `CreateAsyncScope` block (`:560-578`), before the middleware pipeline (`:582` onwards) and `app.Run`. The diff of `Program.cs` is this single added line.

### D. Form.razor (13bis.2)

1. **I.1**: button rendered only when `_detail is not null` (`Form.razor:40-43`), which is set only by `Fill` (edit mode or after a save); `InitCreate` sets it to null (`:632`), so no button on create or copy forms. Disabled while saving or activating.
   **I.2**: `Depuis` is a string (`:475`), parsed with `Guid.TryParse` and `Guid.Empty` rejected (`:477-478`), no exception path; `from` is read only in create mode (`:545`) and is part of the reload key (`:547`, `:553`).
   Transitions: edit A -> copy A (mode changes, reload); copy -> `/exercices/nouveau` (`from` null, reload, `InitCreate`); copy A -> copy B (`from` changes, reload); copy -> save -> edit (`_currentFrom` reset at `:1020`, key equals the new route, no second load, same as before the lot). On every reload `LoadAsync` resets the video state (`:563`, `ResetVideoState` `:1230-1240`), `_notFound`, `_loadError`, messages (`ClearMessages` `:778-786`: errors, unmapped, summary, name taken, save error, action alert), the copy banner, skipped list and copy error (`:568-570`); `InitCreate` resets `_detail` (edit bar and video card hidden), `_held`, `_incomplete`, the model; the baseline is set at `:607` or `:627`. Only the flash survives (F7).
2. **Concurrency**: `LoadAsync` takes `var token = ++_loadToken` (`:562`) and drops the result when `token != _loadToken` after both awaits (`:578-579`), before touching `_refs`, the model or the banner. A navigation during the parallel loads cannot apply a stale source.
3. **I.3**: `ApplyCopy` (`:675-700`) and `Fill` (`:708-733`) map the same fields from the same detail properties: name (copy adds the suffix), NameEn, Description, Instructions, the six lists (copy goes through `Pick`), PrimaryGroupId, PrimaryEquipmentId, SecondaryEquipmentId, Level, Breathing, MistakesText, Precautions, CoachTip, SearchTags, Secondary, Objectives. Copy does not set `_detail`, `_held`, `_incomplete` (create semantics), stays `_currentIsCreate = true`, so save uses `Api.CreateAsync` (`:1002-1004`), no edit bar, no activation button, no video card.
4. **I.4**: `NameMaxLength = 120` (`:481`) equals the validator (`AdminExerciseValidators.cs:19`, trimmed length <= 120), the column (`ExerciseConfiguration.cs:24-27`, `HasMaxLength(120)`) and the input `maxlength="120"` (`Form.razor:134`). Room = 120 - 8 = 112. Source of 113 to 120 characters (after `Trim`): cut to 112, `TrimEnd` (so a space at position 112 is removed, result <= 112), plus " (copie)" = at most 120; the suffix ends with ")", so the server trim does not change the length. Surrogate pair: F8.
5. **I.5**: `Pick` (`:657-668`) checks the six reference lists (category, type, laterality, movement pattern, starting position, grip type) against `RefItem.IsActive`, and also drops a value missing from the cached references; each dropped value is listed in the banner. Not checked: primary and secondary muscle groups (no active flag, `MuscleGroupRef` `ExerciseModels.cs:9`), primary and secondary equipment (the table has `EquipmentIsActive` but neither the admin references nor the API write check it, `AdminExerciseService.cs:96-100`, `:732-735`), objectives (no flag). This matches exactly what the API refuses on create (`AdminExerciseService.cs:686-704`). Legacy texts live in `LegacyValues` and are never read by `ApplyCopy`, so they are not copied.
6. **I.6**: banner texts in French (`:48-60`), `div.info` and `div.alert` are existing styles (`app.css:65-66`), the header wrapper reuses `.editbar-state` (`app.css:99`). No em or en dash (section G). **I.7**: baseline = snapshot right after `InitCreate` (`:597-598`, `:607`), so leaving an unsaved copy prompts (evidence B5). **I.8**: any non-success detail result in copy mode: empty create form plus "Exercice à dupliquer introuvable." (`:603-606`), no throw. **I.9**: save path unchanged except clearing the copy state on success (`:1015-1020`); a 409 goes through the existing `ApplyFailureAsync` and keeps the banner.
7. Only the existing `RefCache.GetAsync` and `Api.GetAsync` calls are used (`:573-575`); no new package (csproj not in the diff); no `MarkupString` in the diff; `@copy.Code`, `@copy.Name`, `@_copyError` and the skipped list are rendered by Blazor with HTML encoding.

### F. Public repository

`git diff --name-only "-G<pattern>" origin/main...feature/lot_13bis` (whole diff, docs included):

| Pattern | Result | Control |
|---|---|---|
| JWT shape `eyJ[A-Za-z0-9_-]+[.][A-Za-z0-9_-]+[.][A-Za-z0-9_-]+` | empty | `git grep -c -E` on `origin/main -- docs/review/lot7`: 2 files match |
| `[[]OTP[]]` | empty | `Mentora.Infrastructure/Services/AuthService.cs`: 1 match |
| `gmail[.]com` | empty | `docs/review/lot11/11.4/runtime-evidence.md`: 1 match |
| `web2success[.]fr` | empty | `docs/review/lot8/vps/cleanup-vps.sql`: 6 matches |
| `mentorapp[.]fr` | empty | `docs/review/lot8/vps/cleanup-vps.sql`: 6 matches |
| `13a00000-` | empty | no file on `origin/main` holds it (0), so no positive control exists; the `-G` mechanism itself is proven by `-GLot13bis_SplitArms` (3 files) |
| `example[.]invalid` | empty | 1 file on `origin/main` matches |

No file found, so nothing to classify. Other checks on the diff, all empty: `[Mm][Uu][Xx]_`, `[Ff]irebase`, `[Ll]ive[Kk]it`, `[Tt]oken=`, `[Ss]ignature=`, `X-Amz-`, `BEGIN ... PRIVATE KEY`, `[Pp]assword=`, `[Ss]ecret`. `[Mm][Uu][Xx]` hits only `docs/review/lot13bis/13bis.0/inventory.md` (a migration file name and the words "MUX asset"); `https?://` hits only `docs/review/lot13bis/13bis.2/runtime-evidence.md:23` (two localhost URLs). Email-like strings in added doc lines: 0. First names in the docs (product owner) are already on `origin/main` (6 files). The docs contain local database UUIDs only (local, test data).

Configuration files touched by the lot (appsettings, .env, docker-compose, nginx, Dockerfile, .dockerignore, .gitattributes): none.

### G. Em dash and en dash

Byte search (`\xe2\x80\x94`, `\xe2\x80\x93`) on the added lines of the diff (docs included, Designer, snapshot and CSV excluded): **0**. Control: 11 em dashes in `ProgramTemplateSeeder.cs` on `origin/main` (inherited, backlog).

### H. Build

`dotnet build --no-incremental` of the solution: 0 error, 19 warnings (same count as the evidence). None in a file touched by the lot (`Form.razor`, `SplitArmsPatch.cs`, `MuscleGroupSeeder.cs`, `Program.cs`); they are XML doc warnings (CS1573 and similar) in `Mentora.Core` and `Mentora.API`. Tree still clean after the build.

## 5. Deployment

### 5.1 SQL before the deployment (PROD `mentora_prod`, api-demo `mentora_db`)

Run right before the image switch, with the admin frozen if possible. Read-only; returns codes, names, ids of groups and counts only.

```sql
BEGIN READ ONLY;

-- 5.1.1 Groups (expected: 14 rows, "Bras" sort 4 BICEPS, no Biceps, no Triceps). Note the "Bras" id.
SELECT "MUSCLE_GROUP_ID", "MUSCLE_GROUP_NAME", "MUSCLE_GROUP_SORT_ORDER", "MUSCLE_GROUP_LEGACY_CODE"
FROM "MUSCLE_GROUPS"
ORDER BY "MUSCLE_GROUP_SORT_ORDER", "MUSCLE_GROUP_NAME";

-- 5.1.2 Markers (expected: Lot12_2a_LegacyExerciseCodes, Lot12_2a_ReferenceNormalization; no Lot13bis_SplitArms)
SELECT "DATA_PATCH_NAME" FROM "DATA_PATCHES" ORDER BY 1;
SELECT count(*) AS split_marker FROM "DATA_PATCHES" WHERE "DATA_PATCH_NAME" = 'Lot13bis_SplitArms';  -- expected 0

-- 5.1.3 Guard: exact "Bras" = 1, any Biceps/Triceps/bras variant (case, spaces) = only the exact "Bras"
SELECT "MUSCLE_GROUP_NAME", length("MUSCLE_GROUP_NAME") AS len
FROM "MUSCLE_GROUPS"
WHERE lower(btrim("MUSCLE_GROUP_NAME")) IN ('bras', 'biceps', 'triceps', 'bíceps', 'tríceps');  -- expected 1 row: Bras, 4

-- 5.1.4 Mentora exercises with primary "Bras" (expected 32 if PROD equals the 18/11 read)
WITH p(code) AS (SELECT unnest(ARRAY['EX009','EX118','EX119','EX120','EX121','EX122','EX123','EX124','EX125','EX126','EX127','EX128','LG18','LG19']))
SELECT e."EXERCISE_SOURCE_CODE", e."EXERCISE_NAME", e."EXERCISE_MUSCLE_GROUP", e."EXERCISE_IS_ACTIVE",
       (e."EXERCISE_SOURCE_CODE" IN (SELECT code FROM p)) AS listed_to_triceps
FROM "EXERCISES" e
JOIN "MUSCLE_GROUPS" g ON g."MUSCLE_GROUP_ID" = e."EXERCISE_PRIMARY_MUSCLE_GROUP_ID"
WHERE e."EXERCISE_COACH_ID" IS NULL AND g."MUSCLE_GROUP_NAME" = 'Bras'
ORDER BY 5 DESC, 1;
-- Review every row with listed_to_triceps = false: it will become Biceps (F2).

-- 5.1.5 Mentora secondary links to "Bras" (expected 71 if PROD equals the 18/11 read)
WITH s(code) AS (SELECT unnest(ARRAY['EX001','EX012','EX228','EX065','EX066','EX067','EX075','EX076','EX077','EX078','EX079','EX080','EX081','EX082','EX083','EX084','EX085','EX213','EX220','EX245','EX016','EX017','EX018','EX019','EX020','EX021','EX022','EX023','EX024','EX025','EX026','EX027','EX028','EX029','EX030','EX031','EX032','EX033','EX034','EX035','EX036','EX037'])),
     b(code) AS (SELECT unnest(ARRAY['EX013','EX227','EX233','EX237','EX238']))
SELECT e."EXERCISE_SOURCE_CODE", e."EXERCISE_NAME", pg."MUSCLE_GROUP_NAME" AS primary_group,
       (SELECT count(*) FROM "EXERCISE_SECONDARY_MUSCLE_GROUPS" l2
         WHERE l2."EXERCISE_SECONDARY_MUSCLE_GROUP_EXERCISE_ID" = e."EXERCISE_ID") AS links,
       CASE WHEN e."EXERCISE_SOURCE_CODE" IN (SELECT code FROM s) THEN 'to Triceps'
            WHEN e."EXERCISE_SOURCE_CODE" IN (SELECT code FROM b) THEN 'both'
            ELSE 'stays Biceps' END AS planned
FROM "EXERCISE_SECONDARY_MUSCLE_GROUPS" l
JOIN "MUSCLE_GROUPS" g ON g."MUSCLE_GROUP_ID" = l."EXERCISE_SECONDARY_MUSCLE_GROUP_MUSCLE_GROUP_ID" AND g."MUSCLE_GROUP_NAME" = 'Bras'
JOIN "EXERCISES" e ON e."EXERCISE_ID" = l."EXERCISE_SECONDARY_MUSCLE_GROUP_EXERCISE_ID"
LEFT JOIN "MUSCLE_GROUPS" pg ON pg."MUSCLE_GROUP_ID" = e."EXERCISE_PRIMARY_MUSCLE_GROUP_ID"
WHERE e."EXERCISE_COACH_ID" IS NULL
ORDER BY 5, 1;
-- Review every 'stays Biceps' row (expected the 24 of evidence 6.5).

-- 5.1.6 State of each listed code compared with what the patch expects (every line not 'ok' is a difference)
WITH lists(code, list) AS (
  SELECT c, 'primary' FROM unnest(ARRAY['EX009','EX118','EX119','EX120','EX121','EX122','EX123','EX124','EX125','EX126','EX127','EX128','LG18','LG19']) c
  UNION ALL
  SELECT c, 'secondary' FROM unnest(ARRAY['EX001','EX012','EX228','EX065','EX066','EX067','EX075','EX076','EX077','EX078','EX079','EX080','EX081','EX082','EX083','EX084','EX085','EX213','EX220','EX245','EX016','EX017','EX018','EX019','EX020','EX021','EX022','EX023','EX024','EX025','EX026','EX027','EX028','EX029','EX030','EX031','EX032','EX033','EX034','EX035','EX036','EX037']) c
  UNION ALL
  SELECT c, 'both' FROM unnest(ARRAY['EX013','EX227','EX233','EX237','EX238']) c),
st AS (
  SELECT l.code, l.list,
         count(e."EXERCISE_ID") OVER (PARTITION BY l.code) AS found,
         e."EXERCISE_IS_ACTIVE" AS active, e."EXERCISE_MUSCLE_GROUP" AS legacy,
         pg."MUSCLE_GROUP_NAME" AS primary_group,
         EXISTS (SELECT 1 FROM "EXERCISE_SECONDARY_MUSCLE_GROUPS" x
                 JOIN "MUSCLE_GROUPS" g ON g."MUSCLE_GROUP_ID" = x."EXERCISE_SECONDARY_MUSCLE_GROUP_MUSCLE_GROUP_ID"
                 WHERE x."EXERCISE_SECONDARY_MUSCLE_GROUP_EXERCISE_ID" = e."EXERCISE_ID" AND g."MUSCLE_GROUP_NAME" = 'Bras') AS bras_link,
         (SELECT count(*) FROM "EXERCISE_SECONDARY_MUSCLE_GROUPS" x
           WHERE x."EXERCISE_SECONDARY_MUSCLE_GROUP_EXERCISE_ID" = e."EXERCISE_ID") AS links
  FROM lists l
  LEFT JOIN "EXERCISES" e ON e."EXERCISE_SOURCE_CODE" = l.code AND e."EXERCISE_COACH_ID" IS NULL
  LEFT JOIN "MUSCLE_GROUPS" pg ON pg."MUSCLE_GROUP_ID" = e."EXERCISE_PRIMARY_MUSCLE_GROUP_ID")
SELECT code, list, found, active, legacy, primary_group, bras_link, links,
       CASE WHEN found = 0 THEN 'DIFF: code not found (no log line, F1)'
            WHEN found > 1 THEN 'DIFF: code duplicated'
            WHEN list = 'primary' AND primary_group IS DISTINCT FROM 'Bras' THEN 'DIFF: primary not Bras (not applicable)'
            WHEN list <> 'primary' AND NOT bras_link THEN 'DIFF: no Bras link (not applicable)'
            WHEN list = 'both' AND links >= 6 THEN 'DIFF: 6 links, Triceps will be skipped'
            ELSE 'ok' END AS check_result
FROM st
ORDER BY (check_result = 'ok'), list, code;

-- 5.1.7 Listed codes with 6 secondary links (expected 0)
SELECT e."EXERCISE_SOURCE_CODE", count(*) AS links
FROM "EXERCISES" e
JOIN "EXERCISE_SECONDARY_MUSCLE_GROUPS" x ON x."EXERCISE_SECONDARY_MUSCLE_GROUP_EXERCISE_ID" = e."EXERCISE_ID"
WHERE e."EXERCISE_COACH_ID" IS NULL
  AND e."EXERCISE_SOURCE_CODE" IN ('EX013','EX227','EX233','EX237','EX238')
GROUP BY 1 HAVING count(*) >= 6;

-- 5.1.8 Mentora mirrors naming "Bras" without a link (drift; expected 0)
SELECT count(*) AS mirror_bras_without_link
FROM "EXERCISES" e
WHERE e."EXERCISE_COACH_ID" IS NULL
  AND e."EXERCISE_SECONDARY_MUSCLES" ? 'Bras'
  AND NOT EXISTS (SELECT 1 FROM "EXERCISE_SECONDARY_MUSCLE_GROUPS" x
                  JOIN "MUSCLE_GROUPS" g ON g."MUSCLE_GROUP_ID" = x."EXERCISE_SECONDARY_MUSCLE_GROUP_MUSCLE_GROUP_ID"
                  WHERE x."EXERCISE_SECONDARY_MUSCLE_GROUP_EXERCISE_ID" = e."EXERCISE_ID" AND g."MUSCLE_GROUP_NAME" = 'Bras');

-- 5.1.9 Coach exercises (untouched by design): count on "Bras", total and fingerprint (note them for 5.2)
SELECT count(*) AS coach_on_bras
FROM "EXERCISES" e
WHERE e."EXERCISE_COACH_ID" IS NOT NULL
  AND (e."EXERCISE_PRIMARY_MUSCLE_GROUP_ID" IN (SELECT "MUSCLE_GROUP_ID" FROM "MUSCLE_GROUPS" WHERE "MUSCLE_GROUP_NAME" = 'Bras')
       OR EXISTS (SELECT 1 FROM "EXERCISE_SECONDARY_MUSCLE_GROUPS" x
                  JOIN "MUSCLE_GROUPS" g ON g."MUSCLE_GROUP_ID" = x."EXERCISE_SECONDARY_MUSCLE_GROUP_MUSCLE_GROUP_ID"
                  WHERE x."EXERCISE_SECONDARY_MUSCLE_GROUP_EXERCISE_ID" = e."EXERCISE_ID" AND g."MUSCLE_GROUP_NAME" = 'Bras')
       OR e."EXERCISE_SECONDARY_MUSCLES" ? 'Bras');
SELECT count(*) AS coach_total, md5(string_agg(e::text, '|' ORDER BY e."EXERCISE_ID")) AS coach_fingerprint
FROM "EXERCISES" e WHERE e."EXERCISE_COACH_ID" IS NOT NULL;

-- 5.1.10 Mentora updated dates fingerprint (must be identical after the patch)
SELECT count(*) AS mentora_total,
       md5(string_agg(e."EXERCISE_ID"::text || ':' || e."EXERCISE_UPDATED_DATE"::text, '|' ORDER BY e."EXERCISE_ID")) AS updated_fingerprint
FROM "EXERCISES" e WHERE e."EXERCISE_COACH_ID" IS NULL;

ROLLBACK;
```

Go criteria: 5.1.2 marker 0; 5.1.3 exactly one row "Bras" of length 4; 5.1.6 every difference understood (a difference is never unsafe, it only means "not applicable"); 5.1.7 = 0 or accepted skip; unlisted rows of 5.1.4 and 5.1.5 reviewed by the product owner.

### 5.2 SQL after the deployment

```sql
BEGIN READ ONLY;

-- 5.2.1 Groups: no "Bras", Biceps keeps the former "Bras" id (compare with 5.1.1), Triceps sort 4 TRICEPS; 15 rows
SELECT "MUSCLE_GROUP_ID", "MUSCLE_GROUP_NAME", "MUSCLE_GROUP_SORT_ORDER", "MUSCLE_GROUP_LEGACY_CODE"
FROM "MUSCLE_GROUPS" ORDER BY "MUSCLE_GROUP_SORT_ORDER", "MUSCLE_GROUP_NAME";
SELECT count(*) FILTER (WHERE lower(btrim("MUSCLE_GROUP_NAME")) = 'bras') AS bras,          -- expected 0
       count(*) FILTER (WHERE "MUSCLE_GROUP_NAME" = 'Biceps')  AS biceps,                    -- expected 1
       count(*) FILTER (WHERE "MUSCLE_GROUP_NAME" = 'Triceps') AS triceps,                   -- expected 1
       count(*) AS total                                                                      -- expected 15
FROM "MUSCLE_GROUPS";
SELECT "DATA_PATCH_NAME", "DATA_PATCH_APPLIED_DATE" FROM "DATA_PATCHES" WHERE "DATA_PATCH_NAME" = 'Lot13bis_SplitArms';  -- 1 row

-- 5.2.2 Each listed code: expected state (lines flagged in 5.1.6 as 'not applicable' are expected to differ)
WITH lists(code, list) AS (
  SELECT c, 'primary' FROM unnest(ARRAY['EX009','EX118','EX119','EX120','EX121','EX122','EX123','EX124','EX125','EX126','EX127','EX128','LG18','LG19']) c
  UNION ALL
  SELECT c, 'secondary' FROM unnest(ARRAY['EX001','EX012','EX228','EX065','EX066','EX067','EX075','EX076','EX077','EX078','EX079','EX080','EX081','EX082','EX083','EX084','EX085','EX213','EX220','EX245','EX016','EX017','EX018','EX019','EX020','EX021','EX022','EX023','EX024','EX025','EX026','EX027','EX028','EX029','EX030','EX031','EX032','EX033','EX034','EX035','EX036','EX037']) c
  UNION ALL
  SELECT c, 'both' FROM unnest(ARRAY['EX013','EX227','EX233','EX237','EX238']) c),
st AS (
  SELECT l.code, l.list, pg."MUSCLE_GROUP_NAME" AS primary_group, e."EXERCISE_MUSCLE_GROUP" AS legacy,
         EXISTS (SELECT 1 FROM "EXERCISE_SECONDARY_MUSCLE_GROUPS" x JOIN "MUSCLE_GROUPS" g ON g."MUSCLE_GROUP_ID" = x."EXERCISE_SECONDARY_MUSCLE_GROUP_MUSCLE_GROUP_ID"
                 WHERE x."EXERCISE_SECONDARY_MUSCLE_GROUP_EXERCISE_ID" = e."EXERCISE_ID" AND g."MUSCLE_GROUP_NAME" = 'Biceps')  AS biceps_link,
         EXISTS (SELECT 1 FROM "EXERCISE_SECONDARY_MUSCLE_GROUPS" x JOIN "MUSCLE_GROUPS" g ON g."MUSCLE_GROUP_ID" = x."EXERCISE_SECONDARY_MUSCLE_GROUP_MUSCLE_GROUP_ID"
                 WHERE x."EXERCISE_SECONDARY_MUSCLE_GROUP_EXERCISE_ID" = e."EXERCISE_ID" AND g."MUSCLE_GROUP_NAME" = 'Triceps') AS triceps_link
  FROM lists l
  LEFT JOIN "EXERCISES" e ON e."EXERCISE_SOURCE_CODE" = l.code AND e."EXERCISE_COACH_ID" IS NULL
  LEFT JOIN "MUSCLE_GROUPS" pg ON pg."MUSCLE_GROUP_ID" = e."EXERCISE_PRIMARY_MUSCLE_GROUP_ID")
SELECT code, list, primary_group, legacy, biceps_link, triceps_link,
       CASE WHEN list = 'primary'   AND primary_group = 'Triceps' AND legacy = 'TRICEPS' THEN 'ok'
            WHEN list = 'secondary' AND triceps_link AND NOT biceps_link THEN 'ok'
            WHEN list = 'both'      AND triceps_link AND biceps_link     THEN 'ok'
            ELSE 'DIFF' END AS check_result
FROM st ORDER BY (check_result = 'ok'), list, code;

-- 5.2.3 Unlisted exercises on Triceps (expected 0)
SELECT count(*) AS unlisted_on_triceps
FROM "EXERCISES" e JOIN "MUSCLE_GROUPS" g ON g."MUSCLE_GROUP_ID" = e."EXERCISE_PRIMARY_MUSCLE_GROUP_ID"
WHERE g."MUSCLE_GROUP_NAME" = 'Triceps'
  AND e."EXERCISE_SOURCE_CODE" NOT IN ('EX009','EX118','EX119','EX120','EX121','EX122','EX123','EX124','EX125','EX126','EX127','EX128','LG18','LG19');

-- 5.2.4 Mirror consistency over all Mentora exercises (expected 0 mismatch, 0 mirror naming "Bras")
WITH m AS (
  SELECT e."EXERCISE_ID", COALESCE(e."EXERCISE_SECONDARY_MUSCLES", '[]'::jsonb) AS mirror,
         COALESCE((SELECT jsonb_agg(g."MUSCLE_GROUP_NAME" ORDER BY g."MUSCLE_GROUP_SORT_ORDER", g."MUSCLE_GROUP_NAME" COLLATE "C")
                   FROM "EXERCISE_SECONDARY_MUSCLE_GROUPS" x
                   JOIN "MUSCLE_GROUPS" g ON g."MUSCLE_GROUP_ID" = x."EXERCISE_SECONDARY_MUSCLE_GROUP_MUSCLE_GROUP_ID"
                   WHERE x."EXERCISE_SECONDARY_MUSCLE_GROUP_EXERCISE_ID" = e."EXERCISE_ID"), '[]'::jsonb) AS expected
  FROM "EXERCISES" e WHERE e."EXERCISE_COACH_ID" IS NULL)
SELECT count(*) FILTER (WHERE mirror <> expected) AS mirror_mismatch,
       count(*) FILTER (WHERE mirror ? 'Bras')      AS mirror_bras,
       count(*) AS mentora_total
FROM m;

-- 5.2.5 No link equal to the primary group, no exercise above 6 links (expected 0 and 0)
SELECT count(*) AS link_equals_primary
FROM "EXERCISE_SECONDARY_MUSCLE_GROUPS" x
JOIN "EXERCISES" e ON e."EXERCISE_ID" = x."EXERCISE_SECONDARY_MUSCLE_GROUP_EXERCISE_ID"
WHERE x."EXERCISE_SECONDARY_MUSCLE_GROUP_MUSCLE_GROUP_ID" = e."EXERCISE_PRIMARY_MUSCLE_GROUP_ID";
SELECT count(*) AS above_six
FROM (SELECT x."EXERCISE_SECONDARY_MUSCLE_GROUP_EXERCISE_ID" FROM "EXERCISE_SECONDARY_MUSCLE_GROUPS" x
      GROUP BY 1 HAVING count(*) > 6) t;

-- 5.2.6 Biceps on legacy TRICEPS (expected equal to the 'warnings' of the log, normally 0)
SELECT e."EXERCISE_SOURCE_CODE" FROM "EXERCISES" e JOIN "MUSCLE_GROUPS" g ON g."MUSCLE_GROUP_ID" = e."EXERCISE_PRIMARY_MUSCLE_GROUP_ID"
WHERE e."EXERCISE_COACH_ID" IS NULL AND g."MUSCLE_GROUP_NAME" = 'Biceps' AND e."EXERCISE_MUSCLE_GROUP" = 'TRICEPS';

-- 5.2.7 Coach exercises unchanged and Mentora updated dates unchanged (compare with 5.1.9 and 5.1.10)
SELECT count(*) AS coach_total, md5(string_agg(e::text, '|' ORDER BY e."EXERCISE_ID")) AS coach_fingerprint
FROM "EXERCISES" e WHERE e."EXERCISE_COACH_ID" IS NOT NULL;
SELECT count(*) AS mentora_total,
       md5(string_agg(e."EXERCISE_ID"::text || ':' || e."EXERCISE_UPDATED_DATE"::text, '|' ORDER BY e."EXERCISE_ID")) AS updated_fingerprint
FROM "EXERCISES" e WHERE e."EXERCISE_COACH_ID" IS NULL;

ROLLBACK;
```

Note on 5.1.10 and 5.2.7: the product owner must not save any exercise between the two reads, or the fingerprints differ for a legitimate reason.

### 5.3 Expected log lines

First startup on the new image (existing database):

```
MuscleGroupSeeder: created 0 muscle group(s).
[PATCH] Lot12_2a_LegacyExerciseCodes skipped (already applied)
CatalogSeeder ... created 0 ...            (unchanged lines; no EX116 warning yet, "Bras" still exists here)
ExerciseSeeder: created 0, ...
[PATCH] Lot12_2a_ReferenceNormalization skipped (already applied)
[PATCH] Lot13bis_SplitArms <code> primary Bras -> Triceps                 (one per moved primary, 14 expected)
[PATCH] Lot13bis_SplitArms <code> secondary Bras -> Triceps               (42 expected)
[PATCH] Lot13bis_SplitArms <code> both Bras -> Biceps+Triceps             (5 expected)
[PATCH] Lot13bis_SplitArms applied: renamed=1 created=1 primaryToTriceps=14 secondaryToTriceps=42 secondaryBoth=5 skipped=0 mirrorsRewritten=71 warnings=0
```

Possible extra lines on a drifted database: `<code> primary not applicable: primary group is X, not Bras` and `<code> secondary not applicable: no link to Bras` (info, matching 5.1.6), `<code> skipped: ...` and `<code> warning: ...` (warn). Any `fail:` or `crit:` line, or the exception text `Lot13bis_SplitArms: unexpected state`, means the API did not start (5.1.3 would have shown it).

Second startup:

```
MuscleGroupSeeder: created 0 muscle group(s).
[CATALOG] exercise EX116 'Curl inversé barre': no primary muscle group (label 'Brachio-radial'), left NULL   (accepted noise)
CatalogSeeder ... created 0 ...
[PATCH] Lot12_2a_ReferenceNormalization skipped (already applied)
[PATCH] Lot13bis_SplitArms skipped (already applied)
```

### 5.4 Admin publication

- `Mentora.Admin/wwwroot/index.html` is unchanged on the branch (names-only diff of `Mentora.Admin/wwwroot` is empty), so the CSP hash in the nginx configuration stays valid.
- The only admin source file changed is `Mentora.Admin/Pages/Exercices/Form.razor`. The publication ships a different `Mentora.Admin` assembly and the files that carry its fingerprint (`_framework` boot manifest and compressed copies); no static asset, CSS or JS file changes.
- After publication, admins must reload the admin (references cache per session, inventory C.3) to see Biceps and Triceps; the accepted warning about an open form sending the old "Bras" id (now Biceps) still applies.

### 5.5 Rollback

What happens if api-prod goes back to `mentora-api:lot13` after the patch ran:
- Startup: no failure. Old `MuscleGroupSeeder` does not find "Bras" and **creates a new empty "Bras"** (sort 4, `BICEPS`). `CK_MUSCLE_GROUPS_LEGACY_CODE` and `UX_MUSCLE_GROUPS_NAME` accept it. Old `CatalogSeeder` and `ExerciseSeeder` are create-only and create nothing; both Lot12_2a patches are skipped; the old code does not know the `Lot13bis_SplitArms` marker and ignores it.
- Old `LegacyExerciseColumns`: the "Bras" rule only matters for the new empty group; Biceps and Triceps keep their legacy codes (`BICEPS`, `TRICEPS`), so admin saves stay correct.
- Apps: the empty "Bras" is not in the member and coach filters (only groups used as a primary group appear); the admin lists "Bras", "Biceps", "Triceps".
- Roll-forward to lot 13 bis: marker present, patch skipped, seeder no longer creates "Bras" but does not remove it (F6).

Minimal safe procedure:
1. Before the deployment, take a dump of `MUSCLE_GROUPS`, `EXERCISES`, `EXERCISE_SECONDARY_MUSCLE_GROUPS` and `DATA_PATCHES` (the usual pre-deployment backup), kept off the repository.
2. Roll back by switching the image to `mentora-api:lot13` only; do not restore the dump (it would erase the product owner's edits) and do not undo the split (the old code works on split data).
3. Ask the product owner not to use the group "Bras" while on the old image.
4. When lot 13 bis is redeployed, run a read-only count of exercises and links on "Bras"; if both are 0, delete that one row in a reviewed transaction (separate, explicit go); if not 0, move those exercises in the admin first.
5. Restore the dump only if the patch itself wrote wrong data (5.2 differences that are not "not applicable"), and only after exporting the edits made since the backup.
