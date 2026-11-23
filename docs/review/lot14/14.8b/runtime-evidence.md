# Lot 14.8b runtime evidence: admin screen "Coachs"

## 1. Summary

- Sub-lot: 14.8b, admin screen "Coachs" (list and creation) in Mentora.Admin.
- Branch: `feature/lot_14`, HEAD before the commits: `208c4db`.
- Date: 2026-11-23.
- Scope: admin front only (`Mentora.Admin/`). No change in Mentora.API, Mentora.Core, Mentora.Infrastructure, nginx or docker.
- The screen uses the 14.8a admin API: `GET` and `POST /api/v1/admin/coaches`.

## 2. Run start

Database `SELECT now()` before the first write: `2026-11-23 07:52:40 UTC`.

## 3. Changed and new files (Mentora.Admin/)

Modified:
- `Layout/ShellLayout.razor` (menu order, routes, active item, top bar title and subtitle for Coachs)
- `Program.cs` (scoped `CoachesApi` and `CoachUiState`)

New:
- `Api/CoachModels.cs`
- `Api/CoachesApi.cs`
- `Services/CoachUiState.cs`
- `Pages/Coachs/List.razor`
- `Pages/Coachs/Form.razor`

No CSS class was added: existing classes of `app.css` were enough.

## 4. Build

`dotnet build --no-incremental` at the repo root:

| Build | Errors | Warnings |
|---|---|---|
| Baseline (before the code) | 0 | 19 |
| After | 0 | 19 |

No new warning. The added lines contain no em dash, no en dash and no `style=` attribute.

## 5. Local run checks

| Check | Result |
|---|---|
| `GET http://localhost:4200/` | 200 |
| `GET http://localhost:4200/coachs` | 200 (fallback to `index.html`) |
| `OPTIONS http://localhost:5243/api/v1/admin/coaches` (Origin `http://localhost:4200`, `Access-Control-Request-Method: POST`, `Access-Control-Request-Headers: authorization,content-type`) | 204, `Access-Control-Allow-Origin: http://localhost:4200` |
| `GET http://localhost:5243/api/v1/admin/coaches` without token | 401 |

## 6. Browser test

Tested by the reviewer in Chrome on 2026-11-23 between 16:41 and 16:50 Paris time (15:41 to 15:50 UTC), at http://localhost:4200, logged in with the local admin account.

| # | Test | Result |
|---|---|---|
| B1 | Admin login with the local code (read in the terminal and typed by the user) | OK, lands on /exercices |
| B2 | Menu order and states | "Exercices et vidéos" (Étape 1, active on /exercices with aria-current="page"), "Coachs" (Étape 2, clickable), "Questions du quiz" 3, "Séances types" 4, "Utilisateurs" 5, "Fiches coach" 6, "Tableau de bord" 7, then 3 items "À venir"; every item without a route is disabled with title "Bientôt disponible" |
| B3 | /coachs | page title "Coachs · Mentora Admin"; top bar "Coachs" and "Comptes coach de la Bêta : liste et création"; Coachs active; intro sentence; "Nouveau coach" button; table with 1 row (Federico Orsi, coach@mentora.fr, 26/03/2026 13:06, Actif, 2); footer "1 coach affiché" |
| B4 | /coachs/nouveau layout | page title "Nouveau coach · Mentora Admin"; Coachs stays active; intro text; 4 labelled fields (maxlength 100, 100, 255, 20; email type="email" and autocomplete="off"; phone hint); 2 columns (grid2); no style attribute in main |
| B5 | "Annuler" on the empty form | back to /coachs, no dialog |
| B6 | "Créer le coach" on the empty form | "Ce champ est obligatoire." under Prénom, Nom and Email, aria-invalid="true", focus on Prénom, no request sent |
| B7 | 422: phone `12` | POST 422; "Téléphone invalide : 8 à 20 caractères, chiffres, espaces, +, -, . et parenthèses." under the phone, focus on the phone, no banner |
| B8 | 409: `member@mentora.fr` | POST 409; "Cette adresse est déjà celle d'un élève : un même compte ne peut pas être coach et élève." under the email, focus on the email |
| B9 | 409: `coach@mentora.fr` | POST 409; "Cette adresse est déjà utilisée par un compte coach et élève." |
| B10 | 201: L148b Coach, `l148b-coach1@example.com`, `06 12 34 56 78` | POST 201; no leave dialog; back on /coachs with the flash "Coach créé : L148b Coach (l148b-coach1@example.com). Un mail d'accueil lui est envoyé." (role="status"); new row first (phone under the name, 23/11/2026 16:44, Actif, 0); footer "2 coachs affichés"; browser Back returns to /coachs, not to the form (replace: true) |
| B11 | 409: same address again | "Cette adresse est déjà celle d'un coach." |
| B12 | "Quitter sans enregistrer ?" | filled form then "Annuler": dialog "Quitter sans enregistrer ?", "Le coach n'a pas été créé.", buttons "Rester" and "Quitter"; "Rester" keeps the values; click on the Exercices menu item with the filled form: same dialog; "Quitter" goes to /exercices |
| B13 | Themes | Sombre, Auto and Clair render correctly on /coachs and /coachs/nouveau; theme left on Sombre |
| B14 | Phone width (390 px frame of the same pages, the window being maximized) | compact top bar, horizontal menu with Coachs active, table scrolls inside its card, form in one column, no horizontal page overflow |
| B15 | Console and network | 0 console error; API calls: refresh 200, POST 422, 409, 409, 201, 409, GET list 200; no 401, no 5xx; preflights 204 |

Not exercised in the browser: "Afficher plus" (2 coaches, `nextCursor` null; pagination proved at API level in 14.8a), and the network error and 5xx messages.

Reviewer decisions on choices made in the code: the 422 banner only for unknown keys, and the leave dialog only when a field is not empty, are accepted (they follow 14.H4).

The test created one coach through the screen: `l148b-coach1@example.com`. Through the 14.8a API this wrote one row in USERS, COACHES and COACH_PARAMETERS, 4 rows in OFFER_PROGRAMS and one COACH_CREATED row in ADMIN_AUDIT_LOG. The welcome mail went to the Logging provider (no real mail).

## 7. Log counts (counts only)

`docs/review/lot14/14.8b/api.log` (git-ignored):

| Item | Count |
|---|---|
| `[EMAIL]` lines containing the coach welcome subject | 1 |
| `fail:` | 0 |
| `crit:` | 0 |
| lines matching `Exception` | 16 |

Distinct exception type names on those lines: `FluentValidation.ValidationException`, `Mentora.Core.Exceptions.ConflictException`, `Mentora.Core.Exceptions.UnauthorizedException`, plus the `GlobalExceptionMiddleware` name and a FluentValidation `RaiseValidationException` stack frame. They are the exception types mapped by `GlobalExceptionMiddleware` to 422, 409 and 401; there is no `fail:` or `crit:` line.

Sizes in bytes: `api-err.log` 0, `admin.log` 403, `admin-err.log` 0.

## 8. Cleanup

Scripts: `cleanup-dryrun.sql` and `cleanup-commit.sql` (identical except the last line, `ROLLBACK;` against `COMMIT;`). Scope: email starting with `l148b-` and created at or after the run start.

Rows before cleanup (read only): USERS 1, COACHES 1, COACH_PARAMETERS 1, OFFER_PROGRAMS 4, ADMIN_AUDIT_LOG 1 (COACH_CREATED), USERS in total 3.

Dry-run (rolled back):

| Step | Deleted |
|---|---|
| ADMIN_AUDIT_LOG | 1 |
| OFFER_PROGRAMS | 4 |
| COACH_PARAMETERS | 1 |
| COACHES | 1 |
| USERS | 1 |

Inside the dry-run transaction after the deletes: 0 `l148b-` users, 0 COACH_CREATED rows since the run start, 2 users in total.

After COMMIT (`cleanup-commit.sql`, same counts as the dry-run: 1, 4, 1, 1, 1 deleted; 0 left; 2 users):

- Read only after the commit: `l148b-` users 0, orphan coaches 0, COACH_CREATED rows since the run start 0, USERS in total 2.
- Leftovers since the run start, over the 42 tables having a `*_CREATED_DATE` column (tables with 0 omitted):

| Table | Rows |
|---|---|
| ADMIN_OTPS | 1 |
| ADMIN_REFRESH_TOKENS | 3 |

These are the admin authentication rows of the reviewer's login. Accepted, kept in the backlog.
