# 13bis.2 Runtime evidence: "Dupliquer" button in the admin exercise form

Branch `feature/lot_13bis`. Scope: `Mentora.Admin/Pages/Exercices/Form.razor` only. No API change, no database change, no NuGet package.

## 1. Decisions implemented

| Id | Implementation |
|---|---|
| I.1 | Button "Dupliquer" in the header actions, before "Enregistrer l'exercice", shown only when `_detail` is loaded (edit mode). Click navigates to `exercices/nouveau?depuis={exerciseId}`. The existing NavigationLock dialog applies. |
| I.2 | `[SupplyParameterFromQuery(Name = "depuis")]` read as text, parsed by `ParseGuid` (same pattern as `List.razor`, malformed or empty Guid ignored). `from` is part of the reload key in `OnParametersSetAsync` (create mode only). |
| I.3 | In create mode with a valid `depuis`: references and source detail loaded in parallel, then `ApplyCopy` fills the form with the same mapping as `Fill`. Stays in create mode (no edit bar, no video card), save goes through `Api.CreateAsync`. |
| I.4 | Name = source name + " (copie)". Maximum name length used: **120** (`AdminExerciseValidators.cs:19`, `Name.Trim().Length <= 120`). The source name is cut to 112 characters before the 8 character suffix. |
| I.5 | Not copied: video, activation state, code, legacy texts. A value of one of the six reference lists that is inactive today is left empty and listed in the banner. |
| I.6 | Banner with the existing `info` style, text as specified, plus the line "Valeurs non reprises car désactivées : ..." when needed. |
| I.7 | Baseline of the dirty check = snapshot of the empty create form (`InitCreate`). |
| I.8 | Unknown source or API error: empty create form plus `alert` banner "Exercice à dupliquer introuvable.". |
| I.9 | Save flow unchanged. After a successful save the copy banner is cleared and `_currentFrom` reset. A 409 keeps the banner and shows the existing message with the link. |

## 2. Build

`dotnet build --no-incremental` of the solution: 0 error, 19 warnings. None of the warnings is in `Mentora.Admin` (they come from `Mentora.Core` and `Mentora.API`, untouched). No em dash or en dash in `Form.razor`.

## 3. Browser results (Claude in Chrome, local admin http://localhost:4200 against local API http://localhost:5243, FG admin account, login code typed by François)

- B1 Duplicate button: visible on an existing exercise (EX009), navigates to `/exercices/nouveau?depuis=8b77c98b-192a-42d9-84a3-8ac512cdfc78`.
- B2 Copy banner (`div.info`, `role=status`): "Copie de EX009 « Extension triceps à la poulie ». Changez le nom si besoin, vérifiez les champs, puis enregistrez. La vidéo n'est pas copiée."
- B3 Prefilled form: name "Extension triceps à la poulie (copie)"; category Musculation; level Débutant; primary muscle group Triceps; primary equipment Poulie haute; secondary equipment Corde / barre; text fields copied; objectives Hypertrophie and Renforcement; secondary muscles none. Source EX009 checked afterwards: secondary muscles none, objectives Hypertrophie and Renforcement (identical). Muscle group lists show Biceps and Triceps, no "Bras".
- B4 No video card and no edit-only bar on the copy form.
- B5 Leaving with an unsaved copy: "Quitter sans enregistrer ?" dialog shown; "Rester" keeps the form and its values.
- B6 409: copy saved with the source name "Extension triceps à la poulie": field error "Un exercice porte déjà ce nom : EX009 Extension triceps à la poulie.", URL unchanged, nothing created.
- B7 Success: copy saved as "Extension triceps à la poulie (copie)": flash "Exercice enregistré (code EX251).", navigated to `/exercices/1bccf2b5-1b52-47e1-91fb-b5591b21b942` in edit mode (title "Modifier l'exercice", Duplicate button and video card present, copy banner gone). Saved secondary muscles none, objectives Hypertrophie and Renforcement (same as source).
- B8 Unknown source (`?depuis=00000000-0000-0000-0000-000000000001`): `div.alert` "Exercice à dupliquer introuvable.", empty creation form, save button present.
- B9 Malformed source (`?depuis=pas-un-guid`): plain empty creation form, no banner, no error.
- B10 Browser console during the B8 full page load: no error, warning, exception or CSP message.

## 4. Local test data

The test created exercise **EX251** (id `1bccf2b5-1b52-47e1-91fb-b5591b21b942`) in the LOCAL database only, with its `ADMIN_AUDIT_LOG` line (`EXERCISE_CREATED`). It is left in place: local only, never deployed, same treatment as earlier local admin audit test rows. No SQL was written for it.

## 5. Not covered by this test

- The "Valeurs non reprises car désactivées" line was not exercised (no inactive reference value on the tested source). Code path reviewed only.
- Truncation of a name longer than 112 characters was not exercised in the browser.
