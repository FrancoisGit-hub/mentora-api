# Lot 13 bis: VPS deployment evidence (19/11/2026)

Lot 13 bis (Biceps / Triceps split, "Dupliquer" button in the admin) deployed on 19/11/2026 to api-demo, then to PROD (api-prod and https://admin.mentorapp.fr). Every command was typed by François on the VPS (SSH) or on his PC (PowerShell); every output was checked before the next step. VPS clock in UTC.

## 1. Image and admin build

- Branch feature/lot_13bis at 22beb30, tree clean.
- Image mentora-api:lot13bis built with `docker build --platform linux/amd64 -f Mentora.API/Dockerfile`, saved and gzip compressed in %TEMP% (255 148 047 bytes), sent by scp, loaded on the VPS: `mentora-api:lot13bis 175c55cd791c 2026-11-19 08:53:44 UTC`.
- Admin published in Release (docs/review/lot12/12.5b/publish, ignored by git): exactly appsettings.json and appsettings.Production.json, ApiBaseUrl https://api.mentorapp.fr; `git diff --stat origin/main -- Mentora.Admin/wwwroot/index.html` empty, so the CSP hash in the nginx conf stays valid. Archive admin-www.tgz sent by scp.
- VPS disk before deployment: 218 GB free.

## 2. Read-only state before deployment

Same script on PROD (mentora_prod) and api-demo (mentora_db), BEGIN READ ONLY ... ROLLBACK.

| Check | PROD | api-demo |
|---|---|---|
| Muscle groups | 14, "Bras" sort 4 BICEPS | 14, "Bras" sort 4 BICEPS |
| Marker Lot13bis_SplitArms | 0 | 0 |
| Name clash (Biceps, Triceps, other "bras") | 0 | 0 |
| Mentora exercises with primary "Bras" | 32 | 32 |
| Secondary links to "Bras" | 72 | 71 |
| Mirrors containing "Bras" | 72 | 71 |
| Coach exercises on "Bras" | 0 | 0 |
| Listed codes in an unexpected state (missing, not on Bras, already 6 links) | 0 rows | 0 rows |
| Mirror mismatches over all Mentora exercises | 0 of 277 | 0 of 275 |
| Mentora exercises updated since 17/11 | 4 (LG28, EX168, EX251, EX252) | 0 |

Unlisted exercises on "Bras" (they stay on the renamed row, Biceps): the 18 curls as primary group (EX008, EX104 to EX117, LG15 to LG17) and 25 secondary links on PROD (24 on api-demo): EX004, EX005, EX045 to EX064, EX097, EX240, plus on PROD only EX252 "Thruster", created by the product owner on 18/11 with "Bras" as a secondary muscle (reported to him after the deployment).

Former "Bras" ids: PROD 6c02bf37-1b5c-4424-abfc-1159ef011bc8, api-demo 5ff4cdbd-6463-4f00-909f-e210df46f8df.

## 3. Backups (VPS, 09:22 UTC)

- backup-20261119-0922.dump (api-demo, 565K), backup-prod-20261119-0922.dump (PROD, 419K).
- backup-media-demo-20261119-0922.tgz, backup-media-prod-20261119-0922.tgz.
- docker-compose.yml.pre-lot13bis (copy before the image change of api-prod).

## 4. api-demo

- Previous image kept as mentora-api:pre-lot13bis (= mentora-api:lot13, cbe814730a92); mentora-api:lot13bis tagged latest; `docker compose up -d --no-deps --force-recreate api`.
- Startup: `[EMAIL] routing mode=TEST`, `[CORS] origins=0 localhost=True`, `[CORS] debugOrigins=0`, `[MUX] configured=True`, `[AUDIO] configured=True ffmpeg=True ffprobe=True storage=True maxConcurrent=2 timeoutSeconds=10`, no "Applying migration", no fail or crit line, "Application started".
- Patch: 61 per exercise lines (14 primary, 42 secondary, 5 both), then `[PATCH] Lot13bis_SplitArms applied: renamed=1 created=1 primaryToTriceps=14 secondaryToTriceps=42 secondaryBoth=5 skipped=0 mirrorsRewritten=71 warnings=0`.
- Known pre-existing log noise, unchanged: `[CATALOG] group mismatch` lines (lot 9) and `[CATALOG] exercise EX135: source code used by 2 active rows`.
- Second startup (`docker compose restart api`): `[PATCH] Lot13bis_SplitArms skipped (already applied)`.

## 5. api-prod

- Compose image line changed from mentora-api:lot13 to mentora-api:lot13bis (sed on the exact line, backup first), `docker compose config --quiet` valid; `docker compose up -d --no-deps --force-recreate api-prod`.
- Startup: `[CORS] origins=1 localhost=False`, `[CORS] origin https://admin.mentorapp.fr`, `[CORS] debugOrigins=1`, `[CORS] debug origin http://localhost:8100`, `[MUX] configured=True corsOrigin=https://admin.mentorapp.fr`, `[AUDIO] configured=True ...`, `[ADMIN] accounts listed=4`, no "Applying migration", no fail or crit line, "Application started".
- Patch: `[PATCH] Lot13bis_SplitArms applied: renamed=1 created=1 primaryToTriceps=14 secondaryToTriceps=42 secondaryBoth=5 skipped=0 mirrorsRewritten=72 warnings=0` (72: EX252 included).

## 6. Read-only state after deployment

| Check | PROD | api-demo |
|---|---|---|
| Muscle groups | 15, no "Bras" | 15, no "Bras" |
| Biceps id (former "Bras" id kept) | 6c02bf37-1b5c-4424-abfc-1159ef011bc8 | 5ff4cdbd-6463-4f00-909f-e210df46f8df |
| Triceps (sort 4, TRICEPS) | fbe20e3d-64f8-4245-88d3-8a562e329aac | e0f93eba-d26f-49fe-9ac0-be423bcc807d |
| Marker | 1 | 1 |
| Primary Biceps / Triceps | 18 / 14 | 18 / 14 |
| Secondary Biceps / Triceps | 30 / 47 | 29 / 47 |
| Mirrors containing "Bras" | 0 | 0 |
| Coach exercises on Biceps or Triceps | 0 | 0 |
| Listed codes not in the expected state | 0 rows | 0 rows |
| Mirror mismatches / link equal to primary / more than 6 links | 0 / 0 / 0 | 0 / 0 / 0 |
| Exercises updated since 17/11 | same 4 rows, same timestamps | 0 |

PROD EX252 "Thruster" secondary muscles after deployment: ["Épaules", "Biceps", "Ischio-jambiers", "Fessiers", "Mollets"].

## 7. External checks (from the VPS, through nginx)

- https://api-demo.mentorapp.fr: health 200, coach/exercise-filters without token 401.
- https://api.mentorapp.fr: health 200, coach/exercise-filters without token 401.

## 8. Admin

- admin-www moved to admin-www.prev, new archive extracted (index.html, _framework, css, js, img, appsettings.json, appsettings.Production.json), archive removed, `docker compose restart nginx`; files seen by nginx: _framework dated 19/11, index.html unchanged (9/11).

## 9. Browser test in PROD (Claude in Chrome, nothing saved)

- Login with the FG admin account, code typed by François.
- Exercise list: the "Groupe musculaire" filter offers Biceps and Triceps, no "Bras"; filter Triceps shows 14 exercises (EX009, EX118 to EX128, LG18, LG19).
- EX009 page: primary group Triceps, "Dupliquer" button present.
- "Dupliquer": /exercices/nouveau?depuis=..., banner "Copie de EX009 « Extension triceps à la poulie ». Changez le nom si besoin, vérifiez les champs, puis enregistrez. La vidéo n'est pas copiée.", name "Extension triceps à la poulie (copie)", primary Triceps, no video card. "Retour aux exercices" opened "Quitter sans enregistrer ?", "Quitter" returned to the list; nothing created.
- EX252 "Thruster" page: secondary muscles Épaules, Biceps, Ischio-jambiers, Fessiers, Mollets.
- Console: no error, warning or CSP violation.

## 10. Rollback (not used)

Procedure of docs/review/lot13bis/review-wave1.md section 5.5: switch the image back (api-demo: tag mentora-api:pre-lot13bis as latest; api-prod: compose image line back to mentora-api:lot13), never restore the dump unless the patch wrote wrong data; the old image recreates an empty "Bras" group, to delete after a read-only check once lot 13 bis is redeployed (finding F6). Admin: swap admin-www and admin-www.prev, then restart nginx.
