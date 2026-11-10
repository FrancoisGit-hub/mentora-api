# Lot 12, wave 1: VPS deployment evidence (PROD and api-demo), 2026-11-10

Branch `feature/lot_12`, HEAD `0ef3c6d` (wave 1 review fixes included). Deployed by François over SSH, guided step by step by Claude (chat); commands as in `prod-runbook.md` (updated with what was really run). Times are VPS time (UTC). No secret, token, OTP or e-mail address appears below: secrets were generated on the server straight into `~/mentora/.env` and only their names were checked.

## 1. Before

- DNS: `api.mentorapp.fr` and `admin.mentorapp.fr` resolve to `87.106.108.91` (the first lookup failed from the local DNS cache, the next ones answered).
- VPS state: containers `mentora-api` (`mentora-api:latest`), `mentora-nginx`, `mentora-livekit`, `mentora-postgres`; services `api`, `livekit`, `nginx`, `postgres`; network `mentora-network`; certbot installed on the host (`/usr/bin/certbot`), not in a container.
- Compose read with values masked (only names and `${...}` references shown). Finding: the `postgres` service still publishes `"5432:5432"`. A test from the PC (`Test-NetConnection 87.106.108.91 -Port 5432`) returns `TcpTestSucceeded False`: the IONOS firewall already blocks it from the Internet. Decision (François, 2026-11-10): the port mapping stays until lot 13 at least; accepted risk, backlog.
- api-demo database backup: `~/backup-20261110-0545.dump`, 476K (previous: 467K on 2026-10-06).

## 2. Image

- PC: tree clean, HEAD `0ef3c6d`; `.dockerignore` holds `docs` and `docs/review`.
- `docker build --platform linux/amd64 -f Mentora.API/Dockerfile -t mentora-api:lot12 .`: finished, image `mentora-api:lot12` (manifest list `daf1f0666fe6`).
- `docker save`, `scp` (96MB), `docker load` on the VPS: `mentora-api:lot12 daf1f0666fe6`, `mentora-api:latest e4534e9f5977` (api-demo) unchanged at that point.
- Archives removed: PC `Test-Path` False, VPS `rm` done. Old archives left on the VPS from previous lots (backlog cleanup): `~/backup-image-prelot2-20260506-170445.tar`, `~/mentora-api-backup-20260706-2313.tar`, `~/mentora-api-lot3.tar`, `~/mentora/mentora-api-task3.tar`.
- Local `api.log` files of 12.4a and 12.4b (they hold admin codes): ignored by git (`.gitignore:98:*.log`).

## 3. PROD database, secrets, service

- `.env` backup `.env.bak-20261110-055106`; trailing newline ensured.
- `POSTGRES_PROD_PASSWORD` generated (hex), then `CREATE ROLE`, `CREATE DATABASE`, `REVOKE` (CONNECT on `mentora_prod` revoked from PUBLIC). The password was never displayed.
- `JWT_SECRET_PROD`, `ADMIN_JWT_SECRET_PROD`, `ADMIN_JWT_SECRET_DEMO` (hex, 64 characters, all different) and `ADMIN_EMAILS` added: name check gives 5 times `present`.
- Compose backup `docker-compose.yml.pre-lot12`; service `api-prod` added (image `mentora-api:lot12`, `ASPNETCORE_ENVIRONMENT=Production`, connection to `mentora_prod`, own app and admin secrets, `Cors__AllowedOrigins=https://admin.mentorapp.fr`, same SMTP password, test recipients and LiveKit keys as `api`, no `ports:`). `docker compose config --quiet`: valid; service listed.

## 4. Certificate

- Temporary HTTP block for both names, `nginx -t` ok, reload.
- `sudo certbot certonly --webroot -w /var/www/certbot -d api.mentorapp.fr -d admin.mentorapp.fr --cert-name api.mentorapp.fr`: `Successfully received certificate`, `/etc/letsencrypt/live/api.mentorapp.fr/`, expires 2027-02-08.
- Renewal: timer `snap.certbot.renew.timer` active; `renewal-hooks/deploy` was empty, so nginx would have kept the old certificate after a renewal. Hook added: `reload-nginx.sh` (`docker exec mentora-nginx nginx -s reload`), root, executable.

## 5. PROD API start

- `docker compose up -d --no-deps api-prod`: started; `Applying migration` lines: 24 (fresh database).
- Startup lines: `[EMAIL] routing mode=TEST provider=Smtp testRecipients=4 productionBcc=0`; `[CORS] origins=1 localhost=False`; `[CORS] origin https://admin.mentorapp.fr`; `[ADMIN] auth emails=4 accessMinutes=15 refreshHours=12`; `MuscleGroupSeeder: created 14`; `[PATCH] Lot12_2a_LegacyExerciseCodes applied: codes given=0`; `CatalogSeeder` equipment 70 + 47 extra + no-equipment row, objectives 53, exercises created 248 (skipped 2, objective links 561); `ExerciseSeeder: created 27, superseded 18`; `[PATCH] Lot12_2a_ReferenceNormalization applied: category 248/0, type 246/2, laterality 248/0, movement_pattern 218/30, starting_position 238/10, grip_type 160/0, secondary links created=381, secondary elements dropped=6` (same figures as the R5 parity proof of 12.2a); `ProgramTemplateSeeder: seeded 2`; `QuizQuestionSeeder: created 900, skipped 74`; `[ADMIN] accounts listed=4 created=4 reenabled=0 disabled=0`; `Now listening on: http://[::]:8080`; `Application started`. No `fail:`, `crit:` or `Exception` line.
- No user account is created by the seeders on a fresh database (checked in the code: the coach parameter and offer program seeders only complete existing coaches). Beta tester accounts are created by the team.
- `docker ps`: `mentora-api-prod Up 8080/tcp` (port not published).
- Temporary HTTP block removed, `mentora-prod.conf` enabled after api-prod was running (nginx resolves `api-prod` at load time), `nginx -t` ok, reload.
- From the PC: `GET https://api.mentorapp.fr/api/v1/health` 200; `GET /api/v1/admin/exercises` without token 401; preflight `OPTIONS /api/v1/admin/auth/otp/request` with `Origin: https://admin.mentorapp.fr`: `204 https://admin.mentorapp.fr`.

## 6. Admin online

- PC: publish folder emptied, `dotnet publish Mentora.Admin -c Release` (WebAssembly runtime 8.0.2 from the local SDK 8.0.200: accepted for the Beta, fixed by the .NET migration); published settings: `appsettings.json` and `appsettings.Production.json` only, `ApiBaseUrl` `https://api.mentorapp.fr`, `EnvironmentLabel` empty. Archive `admin-www.tgz` (9178KB) sent by `scp`.
- VPS: `~/mentora/admin-www` holds `_framework`, `appsettings.Production.json`, `appsettings.json`, `css`, `img`, `index.html`, `js`; archive removed.
- Compose backup `docker-compose.yml.pre-admin`; volume `./admin-www:/var/www/mentora-admin:ro` added to `nginx` (line 73); `docker compose up -d --no-deps nginx`: recreated; `mime.types` holds `application/wasm wasm;`.
- `mentora-admin.conf` enabled, `nginx -t` ok, reload.
- Headers from the PC (`curl -I`): `/`, `/exercices`, `/_framework/dotnet.native.wasm` (`application/wasm`) and `/appsettings.Development.json` (200 `text/html`, SPA fallback, no JSON) all carry `Cache-Control: no-cache`, `Strict-Transport-Security: max-age=15768000`, `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer` and the CSP with `sha256-oW+3IccTLsL4xJ3s2rKPxyyczlz2dfeyKhTyG6r3Zkc=` and `connect-src 'self' https://api.mentorapp.fr`. `http://admin.mentorapp.fr/`: `301` to `https://admin.mentorapp.fr/`.

## 7. Browser test on PROD (Claude in Chrome, own tab, closed after)

- `https://admin.mentorapp.fr` loads the login page: logo and Inter fonts loaded, console empty, 0 CSP violation, only `appsettings.json` and `appsettings.Production.json` requested, only external resource Google Fonts.
- Code requested for admin #1 (with François's approval): neutral message, code field shown; the request went to the PROD API (CORS works for real). François read the code in his mailbox (TEST mode, sent to the 4 associates) and typed it himself in the tab.
- Signed in: Exercises screen, other menus greyed out "Bientôt disponible". Reload keeps the session: `GET /api/v1/admin/exercise-references` 200, `GET /api/v1/admin/exercises?limit=50` 200.
- Search "squat": URL becomes `?q=squat`, relevant exercises listed. Exercise card EX002 opened (usage: 0 program, 0 template, 0 session template); nothing saved. 0 CSP violation; refresh key present while signed in.
- Sign out: `POST /api/v1/admin/auth/logout` 200, back to `/connexion`, refresh key removed. Console empty throughout.
- Minor: a status filter typed by hand in the URL (`?etat=inactif`) was not applied; the dropdown works. Backlog.

## 8. api-demo

- Duplicate Mentora exercise names (read only, `BEGIN READ ONLY ... ROLLBACK`): `(0 rows)` for `lower(btrim(...))` and `(0 rows)` with accents folded too.
- Compose backup `docker-compose.yml.pre-demo-lot12`; `AdminAuth__JwtSecret=${ADMIN_JWT_SECRET_DEMO}` and `AdminAuth__Emails=${ADMIN_EMAILS}` added to service `api`.
- Old image kept as `mentora-api:pre-lot12`, `mentora-api:lot12` tagged `latest`, `docker compose up -d --no-deps --force-recreate api`: `Applying migration` lines: 3 (Lot12_1, Lot12_2a, Lot12_2b).
- Startup lines: `[EMAIL] routing mode=TEST provider=Smtp testRecipients=4 productionBcc=0`; `[CORS] origins=0 localhost=True` (Development); `[ADMIN] auth emails=4`; `MuscleGroupSeeder: created 0`; `[PATCH] Lot12_2a_LegacyExerciseCodes applied: codes given=27 candidates=27 code already used=0`; `CatalogSeeder` all `existing` (created 0); `ExerciseSeeder: created 0, existing 27`; `[PATCH] Lot12_2a_ReferenceNormalization applied` with the same figures as PROD; `QuizQuestionSeeder: created 0, existing 900`; `[ADMIN] accounts listed=4 created=4`; `Application started`. No `fail:`, `crit:` or `Exception` line.
- From the PC: `GET https://api-demo.mentorapp.fr/api/v1/health` 200; `GET /api/v1/admin/exercises` without token 401.

## 9. Runbook corrections made after this deployment

- Dedicated image tag `mentora-api:lot12` and `--no-deps` on every `docker compose up` (a plain `up` could recreate api-demo before its duplicate check).
- `mentora-prod.conf.example` sent by `scp` (the VPS folder is not a git clone).
- Order: certificate, then api-prod, then `mentora-prod.conf` (nginx resolves the upstream name at load time).
- certbot run on the host with `sudo`; deploy hook to reload nginx after renewals.
- Real variable names of the `.env` and of the `api` service; secrets in hex; `REVOKE CONNECT`; trailing newline check of `.env`.
- api-demo: `ADMIN_JWT_SECRET_DEMO`, `ADMIN_EMAILS`, image switch with `pre-lot12` kept for rollback.
- Admin: publish folder emptied first, `--no-deps` for nginx, named compose backup `pre-admin`, browser test done by Claude.

## 10. Backlog opened by this deployment

- Port 5432 still mapped in the compose (blocked by the IONOS firewall): remove the mapping, lot 13 at least.
- Old image archives on the VPS to delete.
- Status filter of the Exercises screen not read from a hand-typed URL.
- VPS: system updates and restart pending; SSH still accepts passwords (Beta prerequisites).
