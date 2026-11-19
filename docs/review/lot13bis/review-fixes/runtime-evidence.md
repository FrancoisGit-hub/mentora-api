# Lot 13 bis, review fixes: runtime evidence (F7)

Branch feature/lot_13bis, base HEAD 3ec4140. Review report: docs/review/lot13bis/review-wave1.md.

## 1. Disposition of F1 to F9

| Finding | Disposition | Note |
|---|---|---|
| F1 | BACKLOG (moved from SHOULD) | No change to SplitArmsPatch.cs. The "code not found" path cannot be reached through the admin (no physical delete, codes never change, coach exercises have no source code) and the pre-deployment SQL 5.1 flags every listed code that is absent or in an unexpected state. The proven patch code stays untouched. |
| F2 | BACKLOG | As written in the report |
| F3 | BACKLOG | As written in the report |
| F4 | BACKLOG | As written in the report |
| F5 | BACKLOG | As written in the report |
| F6 | BACKLOG | As written in the report |
| F7 | FIXED | Stale flash message on the copy form, see section 2 |
| F8 | BACKLOG | As written in the report |
| F9 | BACKLOG | As written in the report |

## 2. F7 diff summary

File: Mentora.Admin/Pages/Exercices/Form.razor, method OnParametersSetAsync. 6 insertions, 3 deletions. No other source file changed.

- UiState.TakeFlash() is read exactly once per call into the local variable pendingFlash (it was read twice, once before the guard and once after).
- Route key unchanged (guard): if pendingFlash is not null it is assigned to _flash, then the method returns (previous behaviour kept, so the create flow still shows "Exercice enregistré (code EXnnn)." because _currentIsCreate and _currentId are set before NavigateTo).
- Route key changed: _flash = pendingFlash (null when none), so a flash never survives a route change unless it was set for this navigation.
- Save flow, activation flow and LoadAsync untouched.

## 3. Build

`dotnet build --no-incremental` of the solution, output in docs/review/lot13bis/review-fixes/build.log (ignored by git): 19 Avertissement(s), 0 Erreur(s). No line mentions Form.razor. Warning count unchanged (19).

## 4. Browser results

Claude in Chrome, local admin http://localhost:4200 against local API http://localhost:5243 (Development, Email__Provider=Logging), logged in as the FG admin account, login code typed by François.

- C1: local test exercise EX251 (id 1bccf2b5-1b52-47e1-91fb-b5591b21b942), edit page. " Test F7." appended to the coach tip, saved. Flash "Exercice enregistré." shown, URL unchanged, coach tip now ends with "Test F7.".
- C2: right after C1, click "Dupliquer" opens /exercices/nouveau?depuis=1bccf2b5-1b52-47e1-91fb-b5591b21b942, title "Nouvel exercice". The only message is the copy banner "Copie de EX251 « Extension triceps à la poulie (copie) ». Changez le nom si besoin, vérifiez les champs, puis enregistrez. La vidéo n'est pas copiée." NO "Exercice enregistré." message (F7 fixed). Name prefilled "Extension triceps à la poulie (copie) (copie)", English name "Triceps Pushdown".
- C3: that copy saved. Flash "Exercice enregistré (code EX252)." shown on /exercices/68fa5db1-1a64-4860-a0ff-f3af82384e78, title "Modifier l'exercice", Duplicate button present (create flow unchanged).
- C4: browser console from 09:11 to 09:12 (EX251 page load, save, Dupliquer, copy save, EX252 full reload): only the usual dotnet resource loading group and the Blazor debugging hint. No error, warning, exception or CSP message.

## 5. Local test data created

LOCAL database only, never deployed, same treatment as EX251. No SQL written for them.

- EX252 "Extension triceps à la poulie (copie) (copie)" (id 68fa5db1-1a64-4860-a0ff-f3af82384e78), with its ADMIN_AUDIT_LOG line.
- Coach tip edit on EX251 (" Test F7." appended), with its ADMIN_AUDIT_LOG line.

## 6. Hygiene counts (three files to commit)

Counts of em dash, en dash, OTP marker and JWT pattern, in review-wave1.md, Form.razor and this file: all 0 in each of the three files.
