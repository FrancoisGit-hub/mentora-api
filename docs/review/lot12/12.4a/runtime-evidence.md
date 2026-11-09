# Lot 12, sub-lot 12.4a: Mentora.Admin (Blazor WebAssembly) shell, admin login and theme

Branch `feature/lot_12`, base `6e1b1ec`. Local run only (Docker PostgreSQL 16 on 5433, API on 5243 with `ASPNETCORE_ENVIRONMENT=Development` and `Email__Provider=Logging`, admin dev server on 4200). No token, code, secret, connection string or address appears below: the admins are not named, secrets stayed in variables, only statuses, lengths and booleans were printed. Logs are under `docs/review/lot12/12.4a/*.log` (gitignored by `*.log`, not committed), the publish check folder is gitignored by `publish/`.

No file of Mentora.API, Mentora.Core or Mentora.Infrastructure changed.

---

## 1. Preflight

| Step | Output |
|---|---|
| `git status` / `log --oneline -3` | clean; HEAD `6e1b1ec docs(lot12): 12.2b evidence` |
| `dotnet --list-sdks` | `8.0.200` |
| `dotnet new list blazorwasm` | `Application WebAssembly Blazor`, short name `blazorwasm` |
| `dotnet build --no-incremental` (solution) | `19 Avertissement(s)` (all CS1573), `0 Erreur(s)`: baseline |
| Ports | 4200 and 5243 free |

12.1 auth contract read in `AdminAuthController` and its DTOs (JSON in camelCase, envelope `{ success, data, error, statusCode }`):

| Route | Body | Answer |
|---|---|---|
| `POST /api/v1/admin/auth/otp/request` | `{ email }` | always 200 |
| `POST .../otp/verify` | `{ email, code }` | 200 with `data = { accessToken, expiresIn, refreshToken, refreshExpiresIn, admin: { adminUserId, email } }`; any failure 400 `Invalid or expired code.` |
| `POST .../token/refresh` | `{ refreshToken }` | 200 same `data`; any failure 401 with the envelope |
| `POST .../logout` | `{ refreshToken }` | always 200 |
| `GET .../me` | none, Bearer | 200 `data = { adminUserId, email }`; 401 without body for a missing or bad token; 401 with the envelope for a disabled admin |
| rate limiter | | 429 with the envelope and `Retry-After` |

Other contract points used by the client: 409 carries the code in `error` and an object `details`; 422 carries `errors` with PascalCase keys (12.2b). CORS policy `MentoraCorsPolicy` already allows `http://localhost:4200`, any header, any method.

## 2. Decisions applied

C1 to C10 of the 12.4a prompt, unchanged. Points worth noting:

- C1: `Mentora.Admin` created with `dotnet new blazorwasm --empty --framework net8.0`, added to `Mentora.sln`, no project reference, own records in `Mentora.Admin/Api`. Nullable and implicit usings on.
- Packages: the template pinned `Microsoft.AspNetCore.Components.WebAssembly` and its DevServer at 8.0.2, and the transitive `System.Text.Json 8.0.0` was reported High by `dotnet list package --vulnerable --include-transitive`. Both Microsoft packages moved to 8.0.33 (latest 8.0.x); the check then reports no vulnerable package. No other package.
- C2: `wwwroot/appsettings.json` holds `ApiBaseUrl` and `EnvironmentLabel` (both empty); `appsettings.Development.json` holds `http://localhost:5243` and `LOCAL`. An empty or invalid `ApiBaseUrl` shows the `ConfigError` screen instead of the app. `launchSettings.json` has one profile, `http://localhost:4200`.
- `.gitignore`: the rule `**/appsettings.Development.json` would have hidden the admin file, so the exception `!Mentora.Admin/wwwroot/appsettings.Development.json` was added. `git check-ignore -v` proves the API file is still ignored (rule line 487) and the admin file is not (matched by the exception, line 489).
- Publish: `Content Update="wwwroot\appsettings.Development.json" CopyToPublishDirectory="Never"` keeps the development file out of the published folder (it stays for local dev).
- C5/C6: the exclusive lock is taken in `admin.js` (`navigator.locks.request("mentora-admin-refresh", { mode: "exclusive" })`) which calls back a `[JSInvokable]` method of `AdminSession`; that method reads the stored refresh token inside the lock, calls `token/refresh` with the single `HttpClient`, and writes the new token before the lock is released. Parallel calls in one tab share one in-flight refresh; after a 401 a call whose token was already replaced just retries.
- C7: `otp/verify` 400 (and 422) shows `Code incorrect ou expiré.`; 429 and network or 5xx messages as specified. Two small additions in French: `Saisissez votre adresse e-mail.` (empty field), `Saisissez les 6 chiffres du code.` (code not made of 6 digits), `Saisissez une adresse e-mail valide.` (422 on the address).
- C4: the logo is shown on the login card inside a small dark badge (the logo is green on transparent and would not read on a white card).

## 3. Files

Commit 1 (27 text files plus the 2 brand images, 29 files):

- `.gitignore` (exception), `Mentora.sln` (project added)
- `Mentora.Admin/Mentora.Admin.csproj`, `Program.cs`, `AppConfig.cs`, `App.razor`, `_Imports.razor`
- `Mentora.Admin/Api/`: `ApiTransport.cs`, `ApiClient.cs`, `ApiResult.cs`, `AdminSession.cs`, `AuthModels.cs`
- `Mentora.Admin/Services/ThemeService.cs`
- `Mentora.Admin/Layout/`: `EmptyLayout.razor`, `LoginLayout.razor`, `ShellLayout.razor`
- `Mentora.Admin/Components/`: `ConfigError.razor`, `ThemeButton.razor`
- `Mentora.Admin/Pages/`: `Index.razor`, `Login.razor`, `Exercices.razor`
- `Mentora.Admin/Properties/launchSettings.json`
- `Mentora.Admin/wwwroot/`: `index.html`, `appsettings.json`, `appsettings.Development.json`, `css/app.css`, `js/admin.js`, `img/logo-mentora.png`, `img/icon.png`

`wwwroot/img/` holds exactly these two files (`Get-ChildItem`: `icon.png` 20802 bytes, `logo-mentora.png` 6409 bytes).

## 4. Build and publish

```
dotnet build Mentora.sln --no-incremental   -> 19 Avertissement(s) (all CS1573), 0 Erreur(s)
Mentora.Admin warnings                      -> 0
dotnet publish Mentora.Admin -c Release -o docs/review/lot12/12.4a/publish
publish files: 136 | size: 14.07 MB (includes the precompressed .br and .gz copies)
publish/wwwroot/appsettings.json            -> keys ApiBaseUrl, EnvironmentLabel, both empty
publish/wwwroot/appsettings.Development.json present: False
```

(The note on the SDK: `wasm-tools` is not installed, so the publish runs without the AOT and relinking optimisations; this is optional for Blazor.)

wasm-tools not installed on purpose: no relinking or AOT for now (about 14 MB first download, cached afterwards, acceptable for 4 admins); revisit at the .NET migration.

## 5. Runtime proofs

### A1 Scope of the change

```
git status --short (before the commit): ' M .gitignore', ' M Mentora.sln', '?? Mentora.Admin/'
git diff --stat 6e1b1ec: .gitignore 2 insertions, Mentora.sln 6 insertions
files under Mentora.API, Mentora.Core, Mentora.Infrastructure: none
```

### A2 Servers

```
API: [EMAIL] routing mode=TEST provider=Logging testRecipients=4 productionBcc=0 | [ADMIN] accounts listed=4 created=0 reenabled=0 disabled=0 | Now listening on http://localhost:5243
admin dev server: Now listening on http://localhost:4200, environment Development
GET http://localhost:4200/                              -> 200, Blazor script present: True, title Mentora Admin: True
GET http://localhost:4200/appsettings.Development.json  -> 200, keys: ApiBaseUrl, EnvironmentLabel
GET http://localhost:4200/appsettings.json              -> 200, keys: ApiBaseUrl, EnvironmentLabel
```

### A3 CORS preflight (Origin `http://localhost:4200`)

```
OPTIONS otp/request (POST, content-type)  -> 204, Allow-Origin equals http://localhost:4200: True, Allow-Headers lists content-type: True
OPTIONS me (GET, authorization)           -> 204, Allow-Origin equals http://localhost:4200: True, Allow-Headers lists authorization: True
```

### A4 Contract check with curl (code read from the API log into a variable, never printed)

```
otp/request                      -> 200 success=True
code read from the log           -> True (length 6)
otp/verify                       -> 200 success=True
verify fields: accessToken True, refreshToken True, expiresIn 900, refreshExpiresIn 43200, admin.email True, admin.adminUserId True
me                               -> 200, email field True, adminUserId field True
token/refresh                    -> 200, refresh token changed True, access token changed True
old refresh token again          -> 401, body length 90, error field True (envelope)
second login (the reuse revoked the sessions of that admin) verify -> 200
logout                           -> 200 success=True
verify with a wrong code         -> 400, error field True
```

No mismatch between the client records and the API JSON.

### Check after the browser fixes

The two CSS fixes were first checked on the served file (`GET /css/app.css`): `h1:focus{outline:none}` served True; `.sidenav{...scrollbar-width:none}` served True; `.sidenav::-webkit-scrollbar{display:none}` served True and located inside the `@media (max-width:900px)` block. The `.sidenav` selector is the one carrying `overflow-x:auto` in the existing phone rule. The focus outline fix was confirmed in the browser (see section 6); the scrollbar fix is to be rechecked on screen in the 12.4b browser test.

### Brand images (admin dev server restarted alone, API not started: 4200 listening True, 5243 listening False)

```
GET http://localhost:4200/img/logo-mentora.png -> 200
GET http://localhost:4200/img/icon.png         -> 200
```

### Servers stopped

```
port 5243 in use: False
port 4200 in use: False
Mentora dotnet processes left: 0
```

(Run twice: after the browser test, and again after the brand images check; same output.)

## 6. Browser test (Claude in Chrome, OTP typed by François)

Run on `http://localhost:4200`; the code was typed by François in the tab and never printed.

1. Theme: Auto, Clair, Sombre cycles, is stored in localStorage `mentora-admin-theme` and survives a reload: OK.
2. Empty address: `Saisissez votre adresse e-mail.`: OK. Unknown address: same neutral message as a real admin: OK. Wrong code: `Code incorrect ou expiré.`: OK.
3. Login with the real code: OK, lands on `/exercices`.
4. Reload: OK, one `POST /api/v1/admin/auth/token/refresh` 200 (preflight 204) then `GET /api/v1/admin/auth/me` 200, session kept.
5. Second tab: opens already logged in from the stored refresh token. Both tabs reloaded a moment apart (not strictly simultaneous): both stay logged in, no session revocation.
6. Storage: localStorage holds only `mentora-admin-refresh` and `mentora-admin-theme`; no JWT in browser storage.
7. Menu: 8 items disabled (`disabled=true`, title `Bientôt disponible`), `Exercices et vidéos` active with `aria-current="page"`.
8. Top bar: admin address and `LOCAL` badge shown.
9. Phone width (390 px frame, 384 CSS px viewport): no horizontal page scroll, the menu becomes a horizontal scrolling strip. Defect found: the strip showed a visible scrollbar on Windows (fixed, see fix 2).
10. Logout: `POST /api/v1/admin/auth/logout` 200, refresh token removed from localStorage, back to `/connexion`, theme kept. Opening `/exercices` afterwards redirects to `/connexion`.
11. No console error during the whole test.

Defects found and fixed in `Mentora.Admin/wwwroot/css/app.css` only:

1. First load: the page heading showed a visible green focus outline (`FocusOnNavigate` focuses the `h1`). Fix: `h1:focus { outline: none; }`; the programmatic focus stays for screen readers.
2. Phone breakpoint: scrollbar on the horizontal menu strip. Fix: `scrollbar-width: none;` on `.sidenav` and `.sidenav::-webkit-scrollbar { display: none; }`, both inside `@media (max-width:900px)`; the strip stays scrollable.

### Visual check of the brand images and of the focus fix (Claude in Chrome on `/connexion`, DOM measurements, screenshots unavailable)

1. Logo: `img/logo-mentora.png` loaded (naturalWidth 512, naturalHeight 295, alt `Mentora`): OK.
2. Favicon: `link rel="icon"` points to `img/icon.png`: OK.
3. Focus: on first load the focused element is the `h1` `Mentora Admin` and its computed `outline-style` is `none`: fix 1 OK.
4. No console error.
5. Phone menu scrollbar fix: rule served by the dev server, not rechecked on screen (needs the API and a login); it will be checked in the 12.4b browser test.

Not covered by the browser test: the 429 message (needs several code requests above the configured limit), the 5xx or network message, the `ConfigError` screen, the expired session message after a refresh refusal, and two tabs refreshing at the very same instant.

## 7. Leak checks on the files to commit (counts only)

Patterns proven first on known files: JWT pattern on `docs/review/lot7/claims-after-4a.json` = 2; OTP console marker (SimpleMatch) on `Mentora.Infrastructure/Services/AuthService.cs` = 1; address pattern on `docs/review/lot11/11.4/runtime-evidence.md` = 14 (the pattern is broader than the one of 12.1, which counted 1 on the same file).

On the 27 text files of commit 1 (the two PNG images are binary and not scanned): jwt=0, otp=0, addr=0 for every file. No em dash and no en dash in the `Mentora.Admin` text files.

## 8. Notes and open points

- wasm-tools: choice made, see section 4.
- The refresh token lives in localStorage (decision B of 12.1): any script running on the admin origin could read it. The admin loads no third party script; the only external resource is the Google Fonts stylesheet.
- If localStorage is unusable, the session works but does not survive a reload (in-memory fallback of the refresh token).
- Menu items other than `Exercices et vidéos` are disabled placeholders until their sub-lots.
- Real domains and CORS for them: 12.5a. Deployment and nginx: 12.5b.
