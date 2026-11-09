# Lot 12.5b: runtime evidence (admin ready for https://admin.mentorapp.fr)

Branch `feature/lot_12`, base `2a54701`. No change to Mentora.API, Mentora.Core or Mentora.Infrastructure.

## 1. Preflight

- `git status` clean, HEAD `2a54701` "docs(lot12): 12.5a evidence and PROD runbook".
- `dotnet build --no-incremental` of the solution: 19 warnings (all CS1573), Mentora.Admin 0 warning, 0 error.
- `wwwroot/index.html`: 1 inline script (theme applied before Blazor starts, no flash), 0 inline style. Other scripts are external (`js/admin.js`, `_framework/blazor.webassembly.js`).
- External URLs loaded by the admin: `https://fonts.googleapis.com` (stylesheet) and `https://fonts.gstatic.com` (font files), both in the CSP.
- No `style=` attribute and no `<style>` in any `.razor`, `.cs`, `.js`, `.html` or `.css` of Mentora.Admin. So the CSP `style-src` has no `'unsafe-inline'`.
- `AppConfig` reads `ApiBaseUrl` and `EnvironmentLabel` from the configuration. Blazor merges `appsettings.json` then `appsettings.{Environment}.json`; outside the dev server the environment is Production, so `appsettings.Production.json` overrides the empty `ApiBaseUrl` of `appsettings.json`.
- `Mentora.Admin.csproj` already excludes `appsettings.Development.json` from the publish (`CopyToPublishDirectory="Never"`).

## 2. Decisions applied

- G1: `Mentora.Admin/wwwroot/appsettings.Production.json` = `{ "ApiBaseUrl": "https://api.mentorapp.fr", "EnvironmentLabel": "" }`, published.
- G2: `nginx/conf.d/mentora-admin.conf.example`. The security headers are declared once at server level, with no `add_header` inside a location (second option of the prompt). So there is no `.inc.example` file: one file to copy. A comment at the top gives the PowerShell one-liner to recompute the script hash.
- G3: section "Admin (12.5b)" appended to `docs/review/lot12/12.5a/prod-runbook.md` (steps A to K, in French).

Differences from the prompt, following the code:
1. The certificate was issued with `--cert-name api.mentorapp.fr` for both names (runbook 6.3). The conf points to `/etc/letsencrypt/live/api.mentorapp.fr/`, not `live/admin.mentorapp.fr/`.
2. One file to copy instead of two (see G2).
3. In the runbook, the `.tgz` archive is created in `publish/` (gitignored) so it cannot be committed by mistake.

After review, the runbook was corrected: VPS user `mentora` in step D, step H talks about the single `.example` file, and the file is sent from the PC with `scp` (`~/mentora` on the VPS is not a git clone), after checking which folder the nginx service mounts.

## 3. File list

Files to commit:

- `Mentora.Admin/wwwroot/appsettings.Production.json` (new)
- `nginx/conf.d/mentora-admin.conf.example` (new)
- `docs/review/lot12/12.5a/prod-runbook.md` (modified, section "Admin (12.5b)")
- `docs/review/lot12/12.5b/runtime-evidence.md` (this file)

Not committed: `docs/review/lot12/12.5b/publish/` (gitignored, `.gitignore:186:publish/`, proven with `git check-ignore -v`).

## 4. Runtime proofs

### D1. git diff --stat 2a54701

Before the evidence file was added (new files are untracked, so they show in `git status`):

```
 M docs/review/lot12/12.5a/prod-runbook.md
?? Mentora.Admin/wwwroot/appsettings.Production.json
?? nginx/conf.d/mentora-admin.conf.example
 docs/review/lot12/12.5a/prod-runbook.md | 154 ++++++++++++++++++++++++++++++++
```

Only the allowed paths appear.

### D2. Publish output

`dotnet publish Mentora.Admin -c Release -o docs/review/lot12/12.5b/publish`

- `appsettings*.json` in `publish/wwwroot`: `appsettings.json` and `appsettings.Production.json` only.
- Content of the published `appsettings.Production.json`:

```
{
  "ApiBaseUrl": "https://api.mentorapp.fr",
  "EnvironmentLabel": ""
}
```

- Total size of `publish/wwwroot`: 14.3 MB.
- Precompressed `.gz` files: 43.

### D3. CSP hash

- Inline scripts in the published `index.html`: 1.
- sha256 (base64): `sha256-oW+3IccTLsL4xJ3s2rKPxyyczlz2dfeyKhTyG6r3Zkc=`
- This is the value written in `mentora-admin.conf.example` and in the test conf (the browser test below confirms it matches the served header).

### D4. Local smoke test (nginx:stable-alpine, nginx/1.30.6)

Container `mentora-admin-smoke`, `-p 8088:80`, published `wwwroot` mounted read-only on `/var/www/mentora-admin`, test conf derived from the HTTPS block (listen 80, server_name localhost, no TLS, no HSTS, everything else identical).

- `grep wasm /etc/nginx/mime.types` in the image: `application/wasm   wasm;`. No `types` entry needed.
- `nginx -t`: `syntax is ok`, `test is successful`.

| Request | Result |
|---|---|
| `/` | 200 `text/html`, CSP header present |
| `/exercices` | 200 `text/html` (SPA fallback on index.html) |
| `/_framework/dotnet.native.wasm` | 200 `application/wasm` |
| `/appsettings.Production.json` | 200 `application/json` |
| `/appsettings.Development.json` | 200 `text/html` (SPA fallback, not 404) |

For `/appsettings.Development.json`: the body contains 0 occurrence of `ApiBaseUrl` and one `<!DOCTYPE`, so it is `index.html`, not a configuration file. The file is not in the publish folder.

Headers on every response (`Cache-Control: no-cache`, `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, CSP) and, on `/`:

```
Referrer-Policy: no-referrer
Permissions-Policy: camera=(), microphone=(), geolocation=()
```

Served CSP:

```
default-src 'self'; script-src 'self' 'wasm-unsafe-eval' 'sha256-oW+3IccTLsL4xJ3s2rKPxyyczlz2dfeyKhTyG6r3Zkc='; style-src 'self' https://fonts.googleapis.com; font-src 'self' https://fonts.gstatic.com; img-src 'self' data:; connect-src 'self' https://api.mentorapp.fr; frame-ancestors 'none'; base-uri 'self'; form-action 'self'; object-src 'none'
```

Container stopped with `docker stop mentora-admin-smoke` (approved). Proofs after the stop:

- `Get-NetTCPConnection -LocalPort 8088 -State Listen`: empty (no listener). A first call without the state filter still showed 2 closing connections (FinWait2); 5 seconds later `Get-NetTCPConnection -LocalPort 8088` returned 0 entries.
- `docker ps -a --filter name=mentora-admin-smoke`: header only, no row (the container was started with `--rm`).

### D5. Full build again

`dotnet build --no-incremental`: 19 warnings (CS1573), 0 error, Mentora.Admin 0 warning.

### Runtime versions

- `dotnet --list-sdks`: `8.0.200`.
- Runtime files of the publish (`_framework`): `dotnet.native.8.0.2.hvvh4qt9of.js`, `dotnet.runtime.8.0.2.ie5ojzdh26.js`; the wasm file is `dotnet.native.wasm`.
- The WebAssembly runtime comes from the SDK (8.0.200, runtime 8.0.2), not from the 8.0.33 Blazor packages, and `dotnet list package --vulnerable` does not see it.
- Decision (François, 2026-11-10): accepted for the Beta, backlog: runtime 8.0.2 of the local SDK, fixed by the .NET migration (ticket 865). No SDK installed or changed.

## 5. Browser test (Claude in Chrome)

Run by Claude in Chrome (own tab, closed after), 2026-11-10 around 00:30, on http://localhost:8088.

1. `/` and `/exercices` both render the login page (`/connexion`), title "Connexion · Mentora Admin", logo `img/logo-mentora.png` loaded, Inter 400/600/700 loaded from Google Fonts.
2. The theme button cycles Auto, Clair, Sombre. After a reload the dark theme is applied (html class `theme-dark`, stored value "dark"). Reset to "auto" at the end.
3. Console: no message at all.
4. CSP: the sha256 of the single inline script, computed in the browser, equals the hash in the served header. Enforcement proven: a fetch to `https://example.com` is blocked (connect-src violation) and `eval` is blocked (script-src violation). A fetch to `https://api.mentorapp.fr/api/v1/health` is not blocked by the CSP and fails at network level (API not deployed yet), as expected.
5. The only style attribute in the DOM is on `<html>`, set by Blazor through the CSSOM (`--blazor-load-percentage` variables), which the CSP allows: no `'unsafe-inline'` needed.
6. Headers seen by the browser: Referrer-Policy `no-referrer`, Permissions-Policy `camera=(), microphone=(), geolocation=()`, X-Frame-Options `DENY`, X-Content-Type-Options `nosniff`, Cache-Control `no-cache`. No HSTS (expected on the HTTP test conf).
7. Requests: `appsettings.json` 200 and `appsettings.Production.json` 200. `appsettings.Development.json` was never requested. No refresh token stored.
8. Open point: runtime files 8.0.2 from the local SDK 8.0.200, see "Runtime versions" above (accepted for the Beta).

## 6. Leak checks

Explicit list of files checked: the four files of section 3. Each pattern was first proven on a known sample string (result True for all six), then run on each file.

| Pattern | Looks for | Files with a hit |
|---|---|---|
| jwt | `eyJ...` token shape | none |
| email | e-mail address | none |
| secret | `password`, `secret`, `apikey`, `token` followed by `=` or `:` and a long value | none |
| connstr | `Host=...;...Password=` | `prod-runbook.md`, 1 hit, line 96, see below |
| emdash | U+2014 | none |
| endash | U+2013 | none |

The single connstr hit is in the 12.5a part of the runbook (line 96, the `api-prod` compose block): its password is the placeholder `${POSTGRES_PROD_PASSWORD}`, not a value. It is not in the "Admin (12.5b)" section and was already committed in `2a54701`.

The text `mentora@87.106.108.91` in the runbook (VPS user and IP) is not matched by the email pattern and is not a secret.
