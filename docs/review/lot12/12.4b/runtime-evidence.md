# Lot 12, sub-lot 12.4b: Mentora.Admin "Exercices et vidéos" screen (list, filters, form, create, update, activation)

Branch `feature/lot_12`, base `d6e1e01`. Local run only (Docker PostgreSQL 16 on 5433, API on 5243 with `ASPNETCORE_ENVIRONMENT=Development` and `Email__Provider=Logging`, admin dev server on 4200). No token, code, secret, connection string or address appears below: secrets stayed in variables, only statuses, lengths, booleans and counts were printed. Logs are under `docs/review/lot12/12.4b/*.log` (gitignored by `*.log`, not committed), the publish folder is gitignored by `publish/`.

No file of Mentora.API, Mentora.Core or Mentora.Infrastructure changed.

Run start date read from the database before any write: `2026-11-09 21:14:57.33+00` (bound of the fixture cleanup).

---

## 1. Preflight

| Step | Output |
|---|---|
| `git status` / `log --oneline -3` | clean; HEAD `d6e1e01 docs(lot12): 12.4a evidence` |
| `dotnet build Mentora.sln --no-incremental` | `19 Avertissement(s)` (all CS1573), `0 Erreur(s)`; Mentora.Admin 0 warnings |
| Ports | 5243 and 4200 free |

12.2b contract read in `AdminExercisesController`, `AdminExerciseDtos`, `AdminExerciseValidators`, `AdminExerciseService` and `GlobalExceptionMiddleware` (JSON in camelCase, envelope `{ success, data, error, statusCode }`):

- List item: `exerciseId, sourceCode, name, category, primaryMuscleGroup, primaryEquipment, level, isActive, hasVideo, hasLegacyValues, updatedDate`. There is no English name in the list item.
- Detail: the list fields plus `nameEn, description, instructions, category/type/laterality/movementPattern/startingPosition/gripType` (each `{ id, code, label }`), `legacyValues` (six keys), `isPolyarticular`, `primaryMuscleGroup`, `secondaryMuscleGroups`, `primaryEquipment`, `secondaryEquipment`, `breathing`, `commonMistakes`, `precautions`, `coachTip`, `searchTags`, `objectives`, `video`, `usage { programs, programTemplates, sessionTemplates }`, `createdDate`, `updatedDate`.
- Request (21 fields, full replacement): `name` (required, 1 to 120), `nameEn` (120), `description` (2000), `instructions` (8000), `categoryId` (required), `typeId`, `lateralityId`, `movementPatternId`, `startingPositionId`, `gripTypeId`, `primaryMuscleGroupId`, `secondaryMuscleGroupIds` (6 at most, never the primary), `primaryEquipmentId` (required), `secondaryEquipmentId` (never the primary, never "Sans matériel"), `level`, `breathing` (2000), `commonMistakes` (20 items, 1 to 300 each), `precautions` (2000), `coachTip` (2000), `searchTags` (500), `objectiveIds` (20 at most).
- Level wire values: `BEGINNER`, `INTERMEDIATE`, `ADVANCED` (sent upper case, never a label).
- List filters: `search` (2 to 60), `isActive`, `categoryId`, `muscleGroupId`, `equipmentId`, `objectiveId`, `level`, `hasLegacyValues`, `limit` (1 to 100), `cursor`. The admin uses `search`, `categoryId`, `muscleGroupId`, `level`, `isActive`, `hasLegacyValues`, `limit`, `cursor`.
- 409: `error` is the code itself. `EXERCISE_NAME_TAKEN` with `details { exerciseId, sourceCode, name }`; `EXERCISE_IN_USE` with `details { programs, programTemplates, sessionTemplates, mentoraProgramTemplateNames, mentoraSessionTemplateNames }`.
- 422: `errors` keys are PascalCase property names (`CategoryId`, `SecondaryMuscleGroupIds`, ...), messages in English.

Read-only SQL, Mentora exercises (`EXERCISE_COACH_ID` null):

```
active 275 / inactive 0
exercises with any legacy value (text with a null foreign key): 42
ADMIN_AUDIT_LOG rows: 0
legacy example: EX066 Pull-over haltère | used by a program template: EX001 Développé couché barre
```

## 2. Decisions applied

D1 to D11 of the prompt, with these accepted changes:

- The English name is not in the list item, so the second line of the Exercice cell shows only the source code (accepted).
- Help line of "Synonymes pour la recherche": the POC text, exactly.
- `<TrimmerRootAssembly Include="Mentora.Admin" />` in the csproj: the records are read by reflection (System.Text.Json) and must not be trimmed from a Release publish.
- Extras: hint "Saisissez au moins 2 caractères pour rechercher." (the API refuses a 1 character search), status lines "Exercice activé." and "Exercice désactivé.", scroll to the top after a save.
- Required fields (change to D5, front only), then amended after the browser test (F3):
  - Create: name, category, level, primary muscle group, primary equipment and description are required.
  - Edit: category and primary equipment always block when empty (the API requires them on every write). Level, primary muscle group and description block only when they were filled at load and the user emptied them. A required field already empty at load shows the badge "À compléter" next to its label and one muted line "Champs à compléter : ..." under the header; the save sends null for it, as stored.
- 422 messages from the API (English) are never shown: a mapped field shows "Valeur refusée, vérifiez ce champ." and the summary box stays.

Read-only SQL used for the required-fields decision (Mentora exercises): no description 0, no level 27, no primary muscle group 4, no category foreign key 27, no primary equipment 22, at least one of these 27.

Level label map (client side, built from the wire values above):

| Wire value | Label |
|---|---|
| `BEGINNER` | Débutant |
| `INTERMEDIATE` | Intermédiaire |
| `ADVANCED` | Avancé |

## 3. Files

Commit 1 (all under `Mentora.Admin`):

- New: `Api/ExerciseModels.cs`, `Api/ExercisesApi.cs`, `Services/ReferencesCache.cs`, `Services/ExerciseUiState.cs` (also holds `ExerciseText`: level labels, 409 and 422 helpers), `Components/ChipPicker.razor`, `Components/ConfirmDialog.razor`, `Components/FieldError.razor`, `Pages/Exercices/List.razor`, `Pages/Exercices/Form.razor`.
- Modified: `Program.cs` (three registrations), `Layout/ShellLayout.razor` (the exercise title for every `/exercices/...` route), `Mentora.Admin.csproj` (trimmer root), `wwwroot/css/app.css`, `wwwroot/js/admin.js` (`showDialog`, `focusById`, `scrollTop`).
- Deleted: `Pages/Exercices.razor` (the 12.4a placeholder).

Commit 2: this file and `docs/review/lot12/12.4b/cleanup-fixtures.sql`.

## 4. Build and publish

```
dotnet build Mentora.sln --no-incremental (before the browser test) -> 19 Avertissement(s) (all CS1573), 0 Erreur(s)
dotnet publish Mentora.Admin -c Release -o docs/review/lot12/12.4b/publish
publish files: 134 | size: 14.3 MB (includes the precompressed .br and .gz copies)
publish/wwwroot/appsettings.Development.json present: False
publish folder ignored by git (.gitignore:186 publish/): True
```

After the browser fixes the API was running (it locks its DLLs), so a `--no-incremental` solution build was not possible: `dotnet build Mentora.Admin --no-incremental` gave 0 warnings and 0 errors, and an incremental solution build gave 0 errors. The full `--no-incremental` solution build is repeated after the servers are stopped (section 5, B7).

## 5. Runtime proofs

### B1 Scope of the change

```
git status --short: 6 modified or deleted files, 8 new files or folders, all under Mentora.Admin
git diff --stat d6e1e01: ShellLayout.razor, Mentora.Admin.csproj, Exercices.razor (deleted), Program.cs, app.css, admin.js
files outside Mentora.Admin and docs/review/lot12/12.4b: 0 (none under Mentora.API, Mentora.Core, Mentora.Infrastructure)
```

### B2 Servers

```
run start date (select now()): 2026-11-09 21:14:57.33+00 | ADMIN_AUDIT_LOG rows: 0 | Mentora exercises: 275
API: [EMAIL] routing mode=TEST provider=Logging testRecipients=4 productionBcc=0 | [ADMIN] accounts listed=4 created=0 reenabled=0 disabled=0 | Now listening on http://localhost:5243
admin dev server: Now listening on http://localhost:4200
```

### B3 Contract check with curl (code read from the API log into a variable, never printed)

```
otp/request -> 200 | code read from the log True (length 6) | otp/verify -> 200 | access token present True
exercise-references -> 200 | categories 4, muscleGroups 14, levels BEGINNER,INTERMEDIATE,ADVANCED
exercises?limit=50 -> 200 | items 50 | nextCursor not null True
filters (each with limit=50): search 200 (9 items) | categoryId 200 (50) | muscleGroupId 200 (31) | level=BEGINNER 200 (50) | isActive=false 200 (0) | hasLegacyValues=true 200 (42) | isActive+hasLegacyValues+level 200 (11)
detail EX066 -> 200 | legacyValues non-null keys: 1
detail keys: exerciseId,sourceCode,name,nameEn,description,instructions,category,type,laterality,movementPattern,startingPosition,gripType,legacyValues,isPolyarticular,primaryMuscleGroup,secondaryMuscleGroups,primaryEquipment,secondaryEquipment,level,breathing,commonMistakes,precautions,coachTip,searchTags,objectives,video,isActive,usage,createdDate,updatedDate
```

No mismatch between the client records and the API JSON.

### B5 Round trip (EX066 and EX001 opened and saved unchanged during the browser test)

```
baseline (before the test): EX001 xmin 2026, updated 2026-09-29 18:58:57.654289+00, audit rows 0 | EX066 xmin 2026, updated 2026-09-29 18:22:32.871648+00, audit rows 0 | audit total 0
after the test:             EX001 xmin 2026, updated 2026-09-29 18:58:57.654289+00, audit rows 0 | EX066 xmin 2026, updated 2026-09-29 18:22:32.871648+00, audit rows 0
identical xmin and updated date: True | no audit row for them: True
audit rows after the run start: 4 (EXERCISE_CREATED 1, EXERCISE_UPDATED 1, EXERCISE_DEACTIVATED 1, EXERCISE_ACTIVATED 1), all about the fixture EX251
```

### B6 Fixture cleanup

The only fixture left by the browser test: EX251 `l124b Exercice de test modifie` (1 exercise, 1 objective link, 1 secondary group link, 4 audit rows). Mentora exercises 276 before the cleanup.

Dry run (`cleanup-fixtures.sql`, last line `ROLLBACK;`), deletes bounded by the fixture id (temporary table) and by the run start date, the DO block raises unless exactly 1 fixture is found and 1 exercise deleted:

```
NOTICE: deleted: audit 4 | objective links 1 | secondary links 1 | exercises 1
 mentora_exercises | exercises_after_start | fixtures_left | audit_after_start | audit_total | objective_links | secondary_links
               275 |                     0 |             0 |                 0 |           0 |             561 |             381
after the ROLLBACK: fixture still there 1 | audit total 4 (nothing changed)
```

COMMIT version `cleanup-fixtures-commit.sql` (Write copy of `cleanup-fixtures.sql`). Proof that only the last line differs (`Compare-Object` of the two files read as lines, 56 lines each, only the differing lines printed):

```
=> COMMIT;
<= ROLLBACK;
differing lines: 2
```

Run after approval, counts printed by the script:

```
NOTICE: deleted: audit 4 | objective links 1 | secondary links 1 | exercises 1
 mentora_exercises | exercises_after_start | fixtures_left | audit_after_start | audit_total | objective_links | secondary_links
               275 |                     0 |             0 |                 0 |           0 |             561 |             381
```

Final check (read-only), run start date `2026-11-09 21:14:57.33+00`:

```
EXERCISES created after the run start 0 | named l124b 0 | Mentora exercises 275 (preflight 275)
ADMIN_AUDIT_LOG created after the run start 0 | total 0
EXERCISE_OBJECTIVES total 561, orphan links 0 | EXERCISE_SECONDARY_MUSCLE_GROUPS total 381, orphan links 0
(the link tables have no creation date: the totals equal those before the fixture, 561 and 381, and no link is orphaned)
EX001 active True, xmin 2026 | EX066 active True, xmin 2026
```

The logins of the test also left normal rows in the admin authentication tables (OTPs, refresh tokens, last login dates), as in the previous lots; they are not fixtures and were not deleted.

### B7 Servers stopped, full build

```
port 5243 in use: False
port 4200 in use: False
Mentora dotnet processes left: 0
dotnet build Mentora.sln --no-incremental (API stopped) -> 19 Avertissement(s), 0 Erreur(s)
non-CS1573 warnings: 0 | distinct CS1573 warnings in the API projects: 19 | Mentora.Admin warning lines: 0
scratchpad files b3-body.json and b3-out.json (they held a code and tokens) deleted: Test-Path False for both
```

## 6. Browser test (Claude in Chrome, OTP typed by François)

Run on `http://localhost:4200`; the code was typed by François in the tab and never printed.

1. List: logo shown, 50 rows then 100 with "Afficher plus", footer count correct.
2. Search "squat": 9 rows ranked (LG28 Squat first); search "ex001": EX001 only; filters kept in the URL (`?q=squat`).
3. "À reprendre seulement": 42 rows, equal to the database, EX066 included.
4. EX066 opened: legacy hint "Valeur d'origine : « Extension d'épaule »...", usage line, state badge. Saved unchanged: PUT 200, legacy hint still present.
5. EX001: deactivation confirmed in the dialog, PUT activation 409, alert "Impossible de désactiver cet exercice : il est utilisé dans 0 programme(s), 1 trame(s) et 0 séance(s) type." followed by the Mentora template name, row still Actif. EX001 opened and saved unchanged: PUT 200.
6. Create, empty form: 5 inline "Ce champ est obligatoire." (name, category, level, primary group, description; primary equipment preselected "Sans matériel", absent from the secondary list), focus on name, no request sent.
7. Create with the name "developpe COUCHE barre": POST 409, under the name "Un exercice porte déjà ce nom : EX001 Développé couché barre." with a link to EX001.
8. Create "l124b Exercice de test" (Musculation, BEGINNER, Dos, secondary Bras, objective Activation, two mistakes with a blank line and spaces): POST 201, code EX251, redirected to the edit page, mistakes cleaned, chips kept.
9. Edit: type set to Polyarticulaire shows Polyarticulaire "Oui"; name emptied blocks the save with "Ce champ est obligatoire." and no request; renamed "l124b Exercice de test modifie": PUT 200.
10. Unsaved change then "Retour aux exercices": in-app dialog "Quitter sans enregistrer ?" (Rester / Quitter); Quitter goes back to the list.
11. List: deactivation of the fixture with confirmation, PUT 200, row "Désactivé" and a status message; reactivation without confirmation, PUT 200.
12. LG40 Burpees (imported, incomplete): badges "À compléter" on Catégorie, Niveau minimum and Groupe musculaire principal, and the line "Champs à compléter : ...".
13. Phone width (390 px frame): menu strip scrollbar hidden (`scrollbar-width: none`) and the strip scrolls; the table scrolls inside its card.

Defects found and fixed in `Mentora.Admin` only:

| | Defect | Fix |
|---|---|---|
| F1 | `/exercices?reprendre=1` crashed the app: "Cannot parse the value '1' as type Nullable<Boolean>" (Blazor error bar). | Every list query value (`categorie`, `groupe`, `niveau`, `etat`, `reprendre`) is read as text and parsed in the page: an invalid Guid, level, state or boolean is ignored (filter not applied), never an exception. `q` is cut to 60 characters. |
| F2 | Phone width: the page scrolled horizontally by 472 px. A `.sr-only` span in the table was absolutely positioned against the document (no positioned ancestor). | `position: relative` on `.tablewrap`. The only `.sr-only` use is inside that wrapper. |
| F3 | LG40 saved unchanged: PUT 422 with "'CategoryId' is required." in English under Catégorie (the API requires `categoryId` and `primaryEquipmentId` on every write). | In edit mode these two always block when empty ("Ce champ est obligatoire.", badge "À compléter" kept). The raw API 422 message is never shown: mapped field "Valeur refusée, vérifiez ce champ.", unmapped key one line in the summary box. |

Recheck after the fixes (same tool, the tab is closed):

1. F1: `/exercices?reprendre=1&categorie=abc&niveau=XX&etat=zz`: list shown with 50 rows, Blazor error bar hidden, checkbox unticked, no new console error (only the two errors from before the fix). OK.
2. F2: phone width (390 px frame, 384 CSS px viewport): `document.documentElement.scrollWidth` 369 (no horizontal page scroll; the 15 px gap is the vertical scrollbar), menu strip scrollbar none, the table scrolls inside its card. OK.
3. F3: LG40 saved unchanged: no request sent, "Ce champ est obligatoire." under Catégorie, focus on the category select. OK.

The scrollbar fix of the phone menu strip, left open at the end of 12.4a, is confirmed (item 13).

Not covered by the browser test: the 5xx or network message, the 422 mapping for a field refused by the API (the front blocks every required field first), a reload or tab close with unsaved changes (beforeunload), the 6 secondary muscles and 20 objectives limits, the "Afficher plus" end of list (no nextCursor).

## 7. Leak checks

Counts only, by line, on the explicit list of the 17 files to commit (14 `Mentora.Admin` files for commit 1, then this file and the two cleanup SQL files for commit 2). Patterns first proven on known files: JWT pattern on `docs/review/lot7/claims-after-4a.json` = 2; the console OTP marker (literal match) on `Mentora.Infrastructure/Services/AuthService.cs` = 1; address pattern on `docs/review/lot11/11.4/runtime-evidence.md` = 7 lines (the 12.4a check counted 14 there: the pattern is the same, `Select-String` counts lines, not matches); em dash on `Mentora.API/Controllers/Admin/AdminExercisesController.cs` = 2 (the en dash check uses the same method).

On the 17 files: JWT 0, OTP marker 0, email log marker (literal match) 0, address 0, em dash 0, en dash 0 for every file, this file included (re-run on the final text before the commit).

## 8. Notes and open points

- Edit of an exercise with no category or no primary equipment cannot be saved until those two fields are filled (API rule, decision F3). The 27 exercises without a category foreign key (counted in section 2; LG40 is one of them) are in that case.
- The solution is built `--no-incremental` only when the API is stopped (the running API locks its DLLs).
- No audit read screen, no video upload, no phone preview (12.3b and later).
