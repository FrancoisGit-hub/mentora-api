# Lot 14 wave 2: VPS deployment evidence (24/11/2026)

Lot 14 wave 2 (14.8a admin API for coaches: `GET` and `POST /api/v1/admin/coaches` with the coach welcome mail; 14.8b "Coachs" screen of the admin: list and creation) deployed on 24/11/2026 to api-demo, then to PROD (api-prod), and the admin republished on admin.mentorapp.fr.

Branch `feature/lot_14`. The image holds the 14.8a and 14.8b code (last code commit `5bf7864`); every later commit only adds files under `docs/review`, which `.dockerignore` excludes from the image. Procedure: section "Lot 14 vague 2 (24/11)" of `docs/review/lot12/12.5a/prod-runbook.md` (written after the deployment, committed with this file), with the image, backup and switch steps of the "Lot 13" section and the steps of the "Admin (12.5b)" section. Every command was typed by François on his PC (PowerShell) or on the VPS (SSH); every output was checked before the next step. Times are UTC (VPS clock). No secret, token, login code or real email address in this file.

## 1. Image

- `mentora-api:lot14w2`, ID `36be7e8ba5d6`, 943MB on the VPS. Sent as a compressed archive (`mentora-api-lot14w2.tar.gz`, 244M on the VPS), then `docker load`.
- VPS disk: `/dev/vda1 232G, 14G used, 218G available, 6%`.
- Previous image of both API services: `mentora-api:lot14w1` (`a48dfaadf545`), also tagged `mentora-api:pre-lot14w2`.

## 2. Backups (02:49)

- api-demo database: `~/backup-20261124-0249.dump`, 568K. PROD database: `~/backup-prod-20261124-0249.dump`, 423K.
- Voice message folders: `~/backup-media-demo-20261124-0249.tgz`, 180K; `~/backup-media-prod-20261124-0249.tgz`, 766K.
- Compose: `docker-compose.yml.pre-lot14w2`.

## 3. Read-only prechecks before the migration

Both databases, in `BEGIN READ ONLY` ... `ROLLBACK`.

- Last applied migration: `20261120193442_Lot14_2_OtpAttempts` on `mentora_db` (api-demo) and on `mentora_prod`.
- Constraints `CK_ADMIN_AUDIT_LOG_ACTION` and `CK_ADMIN_AUDIT_LOG_ENTITY_TYPE` present on both databases (the migration drops them by name, then recreates them wider).
- PROD: `ADMIN_AUDIT_LOG` holds only `EXERCISE` rows (12); 4 coaches.
- No precheck file: the migration only widens two CHECK constraints (no unique index, no new column).

## 4. api-demo switch

- `docker tag mentora-api:latest mentora-api:pre-lot14w2`, `docker tag mentora-api:lot14w2 mentora-api:latest`, `docker compose up -d --no-deps --force-recreate api`.
- Startup lines (lines with the OTP or EMAIL-LOG markers filtered out):

```
[EMAIL] routing mode=TEST provider=Smtp testRecipients=4 productionBcc=0
[INVITE] installUrl=False permitLimit=30 windowMinutes=60
[AUTH] otpRateLimit permitLimit=30 windowMinutes=15
[CORS] origins=0 localhost=True
[CORS] debugOrigins=0
[MUX] configured=True corsOrigin=http://localhost:4200 apiBase=api.mux.com
[AUDIO] configured=True ffmpeg=True ffprobe=True storage=True maxConcurrent=2 timeoutSeconds=10
[ADMIN] auth emails=4 accessMinutes=15 refreshHours=12
Applying migration '20261123042911_Lot14_8_CoachCreatedAudit'.
[PATCH] Lot12_2a_LegacyExerciseCodes skipped (already applied)
[PATCH] Lot12_2a_ReferenceNormalization skipped (already applied)
[PATCH] Lot13bis_SplitArms skipped (already applied)
[ADMIN] accounts listed=4 created=0 reenabled=0 disabled=0
Application started. Press Ctrl+C to shut down.
```

No `fail:` or `crit:` line.

## 5. PROD switch

- Compose: line 51 (service api-prod) `image: mentora-api:lot14w2`; line 23 (service api) stays `image: mentora-api:latest`.
- `docker compose up -d --no-deps --force-recreate api-prod`.
- Startup lines (lines with the OTP or EMAIL-LOG markers filtered out):

```
[EMAIL] routing mode=TEST provider=Smtp testRecipients=4 productionBcc=0
[INVITE] installUrl=False permitLimit=30 windowMinutes=60
[AUTH] otpRateLimit permitLimit=30 windowMinutes=15
[CORS] origins=1 localhost=False
[CORS] origin https://admin.mentorapp.fr
[CORS] debugOrigins=1
[CORS] debug origin http://localhost:8100
[MUX] configured=True corsOrigin=https://admin.mentorapp.fr apiBase=api.mux.com
[AUDIO] configured=True ffmpeg=True ffprobe=True storage=True maxConcurrent=2 timeoutSeconds=10
[ADMIN] auth emails=4 accessMinutes=15 refreshHours=12
Applying migration '20261123042911_Lot14_8_CoachCreatedAudit'.
[PATCH] Lot12_2a_LegacyExerciseCodes skipped (already applied)
[PATCH] Lot12_2a_ReferenceNormalization skipped (already applied)
[PATCH] Lot13bis_SplitArms skipped (already applied)
[ADMIN] accounts listed=4 created=0 reenabled=0 disabled=0
Application started. Press Ctrl+C to shut down.
```

No `fail:` or `crit:` line. `provider=Smtp` (never `Logging`, which would write mail bodies to the log).

- Applied migrations afterwards, both databases: `20261123042911_Lot14_8_CoachCreatedAudit`, then `20261120193442_Lot14_2_OtpAttempts`.
- Running containers: `mentora-api-prod` on `mentora-api:lot14w2`, `mentora-api` on `mentora-api:latest` (same ID `36be7e8ba5d6`).
- Without token: `GET https://api.mentorapp.fr/api/v1/admin/coaches` returns 401 (the route exists). CORS preflight from `https://admin.mentorapp.fr` accepted.

## 6. Admin

- Release publish of `Mentora.Admin`, only the `wwwroot` folder sent and installed in `~/mentora/admin-www` (previous version kept in `~/mentora/admin-www.prev`), then nginx restarted.
- `index.html` unchanged: sha256 starting with `7715b84a1ca90f39` in `admin-www` and in `admin-www.prev`, so the CSP hash of the inline theme script is still valid.
- `_framework/blazor.boot.json`: sha256 starting with `d6c53979e6ef3563` on the host and inside the `mentora-nginx` container (nginx serves the new files).
- `https://admin.mentorapp.fr` and `https://admin.mentorapp.fr/coachs`: 200.

## 7. Logs since 01:45 (both API containers)

- `fail:` or `crit:`: 0. `[OTP]` lines: 0. `[EMAIL-LOG]` lines: 0.
- Warnings: 131 per container, the same set in both:
  - 114 `Mentora.Infrastructure.Persistence.MentoraDbContext`: reference-data seeders (`[CATALOG]`, `[QUIZ]`), known since lot 9;
  - 15 `Microsoft.EntityFrameworkCore.Model.Validation[20601]`: bool column with a database default and no sentinel, pre-existing (the wave 2 migration is an empty model diff completed with SQL, the EF model does not change);
  - 1 `Microsoft.AspNetCore.DataProtection.KeyManagement.XmlKeyManager[35]` and 1 `Microsoft.AspNetCore.DataProtection.Repositories.FileSystemXmlRepository[60]`: data protection keys not kept outside the container, pre-existing; the API authenticates with Bearer tokens and uses no cookie, no known effect.

## 8. Functional proof in PROD (browser)

Claude in Chrome, in its own tab, with François's agreement for each input.

- Login to admin.mentorapp.fr with a team admin account: address typed by Claude, login code read by François in his mailbox and typed by him (never in the chat).
- Menu, in the decided order: Exercices et vidéos (Étape 1), Coachs (Étape 2), Questions du quiz (Étape 3), Séances types (Étape 4), Utilisateurs (Étape 5), Fiches coach (Étape 6), Tableau de bord (Étape 7), then Journal des actions, Abonnements, Journal des mails (À venir).
- Coachs list: the 4 PROD coaches, each with 1 member; columns Coach, Email, Créé le, État, Élèves; footer « 4 coachs affichés ».
- Creation of a test coach: first name `L148c`, last name `Test`, email `l148c-coach1@example.com` (fictional). Banner « Coach créé : L148c Test (l148c-coach1@example.com). Un mail d'accueil lui est envoyé. »; list of 5 coaches, the new one first with 0 members.
- Console: 0 CSP violation (`securitypolicyviolation` listener), no error message.
- Welcome mail: 1 `[EMAIL]` line for the test address in the api-prod log; mail received in the associates' test inboxes (TEST mode, 4 recipients), sent from the Mentora contact address, subject `[TEST pour l148c-coach1@example.com] L148c, votre compte coach Mentora est prêt`.
- SQL read at the start of the cleanup transaction: `USER_ROLE` `COACH`, `USER_IS_ENABLED` `t`, `COACH_IS_ACTIVE` `t`, created `2026-11-24 03:02:35.285789+00`; one `ADMIN_AUDIT_LOG` row `COACH_CREATED` / `COACH` with the same timestamp.

| Table | Rows of the test coach |
|---|---|
| `ADMIN_AUDIT_LOG` | 1 |
| `OFFER_PROGRAMS` | 4 |
| `COACH_PARAMETERS` | 1 |
| `MEMBER_COACHES` | 0 |
| `AUTH_OTPS` | 0 |
| `AUTH_REFRESH_TOKENS` | 0 |
| `USER_DEVICES` | 0 |
| `COACHES` | 1 |
| `USERS` | 1 |

## 9. Cleanup of the test coach

- Script: `docs/review/lot14/vps/cleanup-l148c-prod.sql`, the exact file run on the VPS (75 lines, MD5 `5ad76295ea1aa6e9287f9ec03d76eea9`, ends with `ROLLBACK`). One target only, found by email and creation date (on or after 02:55, the run start); the script stops unless it finds exactly one target. Deletion order: `ADMIN_AUDIT_LOG` (entity id of the coach, type `COACH`), `OFFER_PROGRAMS`, `COACH_PARAMETERS`, `AUTH_OTPS`, `AUTH_REFRESH_TOKENS`, `USER_DEVICES`, `COACHES`, `USERS`. Then a leftover scan on every table with a `*_CREATED_DATE` column.
- Dry run (03:35:20): `DELETE 1`, `DELETE 4`, `DELETE 1`, `DELETE 0`, `DELETE 0`, `DELETE 0`, `DELETE 1`, `DELETE 1`; every count 0 afterwards; `coaches_total` 4; `l148c_users_left` 0; rows created since the run start: `ADMIN_OTPS` 1 and `ADMIN_REFRESH_TOKENS` 1 (the admin login of this test, kept); `ROLLBACK`.
- Commit file made with `sed` on the last line only; `diff` shows `75c75`, `< ROLLBACK;`, `> COMMIT;`; 75 lines in both files.
- Real run (03:39:10), after François's yes: the same counts and lines, then `COMMIT`.
- Check: the Coachs screen in PROD lists the 4 coaches again. Both SQL files removed from the VPS.

## 10. Second start

Counts read before and after (users, coaches, coach_parameters, offer_programs, admin_audit_log):

| Database | Before | After |
|---|---|---|
| api-demo (`mentora_db`) | `8 2 2 8 0` | `8 2 2 8 0` |
| PROD (`mentora_prod`) | `8 4 4 16 12` | `8 4 4 16 12` |

`api-prod`, then `api`, recreated. On each: no `Applying migration` line, then `[EMAIL] routing mode=TEST provider=Smtp testRecipients=4 productionBcc=0`, `[INVITE] installUrl=False permitLimit=30 windowMinutes=60`, `[AUTH] otpRateLimit permitLimit=30 windowMinutes=15`, `[ADMIN] accounts listed=4 created=0 reenabled=0 disabled=0`, `Application started`; no `fail:` or `crit:` line. The coach parameter and offer seeders (now sharing `CoachDefaults` with the coach creation) wrote nothing.

## 11. Archives

- VPS: `~/mentora-api-lot14w2.tar.gz` removed (`ls`: no such file); the wave 1 archive was already gone.
- PC: the image archive in `%TEMP%` and the admin archive in the ignored publish folder removed before this commit.

## 12. Rollback (not used)

- api-demo: `docker tag mentora-api:pre-lot14w2 mentora-api:latest`, then `docker compose up -d --no-deps --force-recreate api`.
- api-prod: `cp docker-compose.yml.pre-lot14w2 docker-compose.yml`, then `docker compose up -d --no-deps --force-recreate api-prod`.
- Admin: put `admin-www.prev` back (step K of the "Admin (12.5b)" section), then restart nginx.
- No migration `Down`: the wider constraints accept everything the previous image writes. The `Down` refuses to run as soon as a `COACH` row exists in `ADMIN_AUDIT_LOG`. Tags `mentora-api:pre-lot14w2` and `mentora-api:lot14w1` are kept until the merge of the wave into main.

## 13. Backlog opened by this deployment

- Two ASP.NET Core data protection warnings at each start (keys not kept outside the container): mount a folder for the key ring, or silence them, before the API uses any cookie.
- 15 EF Core 20601 warnings at each start, on top of the 114 seeder lines: add sentinels or silence the event, so that a new `warn:` line stands out.
- 17 old API image tags on the VPS (back to June 2026): prune after the merge, keeping the last two lots.
