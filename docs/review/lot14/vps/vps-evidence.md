# Lot 14 wave 1: VPS deployment evidence (23/11/2026)

Lot 14 wave 1 (14.1 member added by the coach, 14.1b optional fields, 14.2 login code protection, 14.3 `[OTP]` console line in Development only, review wave 1 fixes) deployed on 23/11/2026 to api-demo, then to PROD (api-prod).

Branch `feature/lot_14`, HEAD `9910e4b` at build time. Procedure: section "Lot 14 vague 1 (21/11)" of `docs/review/lot12/12.5a/prod-runbook.md`, with the image, backup and switch steps of the "Lot 13" section. Every command was typed by François on his PC (PowerShell) or on the VPS (SSH); every output was checked before the next step. Times are UTC (VPS clock). No secret, token, login code or real email address in this file.

## 1. Image

- Pre-checks on the PC: PowerShell not running as administrator (`False`), clean tree, HEAD `9910e4b`, branch `feature/lot_14`, no `.git\index.lock`, ports 5243, 4200 and 8088 free, `.dockerignore` contains `docs` and `docs/review`.
- `docker build --platform linux/amd64 -f Mentora.API/Dockerfile -t mentora-api:lot14w1 .`: `FINISHED` in 68.3 s, `naming to docker.io/library/mentora-api:lot14w1`, no error.
- Archive: `docker save` to `%TEMP%`, then GZipStream (Windows PowerShell 5.1): `mentora-api-lot14w1.tar.gz`, 255,169,022 bytes. `scp`: 100 %, 243 MB, 02:37.
- VPS disk before sending: `/dev/vda1 232G, 14G used, 218G available, 6%`. No old image archive left in the home folder.
- `docker load -i ~/mentora-api-lot14w1.tar.gz`: `Loaded image: mentora-api:lot14w1`, ID `a48dfaadf545` (same as the manifest list built on the PC), created 2026-11-23 01:03:04 UTC. Previous image of both API services: `mentora-api:lot13bis` (`175c55cd791c`).

## 2. Backups (01:28 to 01:29)

- api-demo database: `~/backup-20261123-0128.dump`, 567K. PROD database: `~/backup-prod-20261123-0129.dump`, 423K.
- Voice message folders: `~/backup-media-demo-20261123-0129.tgz`, 180K; `~/backup-media-prod-20261123-0129.tgz`, 766K.
- Compose: `docker-compose.yml.pre-lot14w1`.

## 3. Read-only prechecks before the migrations

Queries of `docs/review/lot14/14.1/precheck.sql` and `docs/review/lot14/14.2/precheck.sql`, each in `BEGIN READ ONLY` ... `ROLLBACK`.

| Query | api-demo (`mentora_db`) | PROD (`mentora_prod`) |
|---|---|---|
| Last applied migration | `20261117234001_Lot13_2_VoiceMessages` | `20261117234001_Lot13_2_VoiceMessages` |
| `case_collisions` | 0 | 0 |
| `emails_with_spaces` | 0 | 0 |
| `auth_otps_rows` (informational) | 229 | 16 |
| `active_codes` (informational) | 0 | 0 |
| `attempt_column_exists` | 0 | 0 |

## 4. api-demo switch

- `docker tag mentora-api:latest mentora-api:pre-lot14w1`, `docker tag mentora-api:lot14w1 mentora-api:latest`, `docker compose up -d --no-deps --force-recreate api`: `Applying migration` count `2`.
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
Applying migration '20261119221223_Lot14_1_UserEmailCi'.
Applying migration '20261120193442_Lot14_2_OtpAttempts'.
[PATCH] Lot12_2a_LegacyExerciseCodes skipped (already applied)
[PATCH] Lot12_2a_ReferenceNormalization skipped (already applied)
[PATCH] Lot13bis_SplitArms skipped (already applied)
[ADMIN] accounts listed=4 created=0 reenabled=0 disabled=0
Application started. Press Ctrl+C to shut down.
```

No `fail:` or `crit:` line.

## 5. Functional proof on api-demo

All in one SSH session; the login code and the access token were kept in shell variables and never printed.

- `GET /api/v1/health`: 200. `POST /api/v1/coach/members` without token: 401 (the route exists).
- Run start read in the database before the first write: `2026-11-23 01:51:46`.
- Demo coach account (fictional, not a real mailbox): `otp/request` with `isCoach` true: 200; code read from the api-demo log (6 digits, never printed); `otp/verify`: access token read (yes), code variable unset.
- `POST /api/v1/coach/members` with email `"  L14V.Member@Example.com "`, first name `Lea`, last name `Vps`: **201**, `"email":"l14v.member@example.com"`, `"isNewAccount":true`, `"isPrimary":true`.
- Same member, email in upper case: **409**, `"reason":"ALREADY_ATTACHED"` (no duplicate account).
- `[EMAIL]` line (mode and subject only): `mode=TEST`, `subject=[TEST pour l14v.member@example.com] Votre coach Coach vous a ajouté sur Mentora`.
- Welcome mail received in a test inbox at 01:52:16 UTC, text of fix F1: no install URL configured, so step 1 is « Votre coach vous enverra le lien pour installer l'application Mentora. »; step 2 asks to type the email (shown in bold) and tap « Connexion »; step 3 asks for the 6 digit code; then « Ne passez pas par « Créez un compte » : votre compte est déjà prêt. ». No role choice. Dark text on a white background.
- Read-only SQL: email stored in lower case `t`, `USER_IS_ENABLED` `t`, `MEMBER_HAS_ACTIVATED` `f`, `IS_PRIMARY` `t`; index `UX_USERS_EMAIL_CI` present; column `AUTH_OTP_ATTEMPT_COUNT` present (count 1).
- Cleanup: one SQL file run first with `ROLLBACK`, then the same file with `COMMIT` (only the last line differs), bounded by the `l14v` prefix and the run start:
  - `l14v_users` 1, `l14v_member_coaches` 1;
  - `DELETE 1` (OTP of the demo coach after the run start), `DELETE 1` (refresh token of the demo coach after the run start), `DELETE 1` (the `l14v` user, member and link by cascade);
  - leftovers on every table with a `*_CREATED_DATE` column after the run start: `(0 rows)`;
  - `l14v_users_after` 0, then `COMMIT`.
- Check after the cleanup: 0 `l14v` user. SQL file removed, shell variables unset.

## 6. PROD switch

- Image of `mentora-api-prod` before the switch (`docker inspect`): `mentora-api:lot13bis` (rollback image).
- Compose: `sed` on the api-prod image line, then `diff docker-compose.yml.pre-lot14w1 docker-compose.yml`:

```
51c51
<     image: mentora-api:lot13bis
---
>     image: mentora-api:lot14w1
```

  `docker compose config --quiet`: valid.
- `docker compose up -d --no-deps --force-recreate api-prod`: `Applying migration` count `2`.
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
Applying migration '20261119221223_Lot14_1_UserEmailCi'.
Applying migration '20261120193442_Lot14_2_OtpAttempts'.
[PATCH] Lot12_2a_LegacyExerciseCodes skipped (already applied)
[PATCH] Lot12_2a_ReferenceNormalization skipped (already applied)
[PATCH] Lot13bis_SplitArms skipped (already applied)
[ADMIN] accounts listed=4 created=0 reenabled=0 disabled=0
Application started. Press Ctrl+C to shut down.
```

No `fail:` or `crit:` line. `provider=Smtp` (never `Logging`, which would write mail bodies to the log).

## 7. PROD checks

- From the VPS, through nginx: `health 200`; `POST /api/v1/coach/members` without token: 401; preflight from `http://localhost:8100`: `HTTP/2 204` with `access-control-allow-origin: http://localhost:8100`; preflight from `http://localhost:3000`: `HTTP/2 204` without the header.
- Login code request for a team coach account (`isCoach` true): 200 (the code was never used). Counts only, on the last 5 minutes of the api-prod log: `[OTP]` 0, `[EMAIL-LOG]` 0, `[EMAIL] mode=` 1 (the mail was sent, nothing of the code is in the log). api-demo (Development), last 60 minutes: `[OTP]` 1, as expected.
- Ports (W7): `api 8080/tcp`, `api-prod 8080/tcp`, nothing published on `0.0.0.0` or `[::]`.
- Docker log driver (informational): `json-file` with no option (`{}`) on `mentora-api-prod`, `mentora-api`, `mentora-nginx` and `mentora-postgres`; no `/etc/docker/daemon.json`. Container logs have no size limit and no rotation; they are dropped when the container is recreated.

## 8. Second start

Both services recreated (`api-prod`, then `api`). On each: no `Applying migration` line, then `[EMAIL] routing mode=TEST provider=Smtp testRecipients=4 productionBcc=0`, `[INVITE] installUrl=False permitLimit=30 windowMinutes=60`, `[AUTH] otpRateLimit permitLimit=30 windowMinutes=15`, `Application started`.

## 9. Rollback (not used)

- api-demo: `docker tag mentora-api:pre-lot14w1 mentora-api:latest`, then `docker compose up -d --no-deps --force-recreate api`.
- api-prod: `cp docker-compose.yml.pre-lot14w1 docker-compose.yml`, then `docker compose up -d --no-deps --force-recreate api-prod`.
- No migration `Down`: both migrations are additive (unique index on `lower("USER_EMAIL")`, column with default 0) and the previous image runs with them. Tags `mentora-api:pre-lot14w1` and `mentora-api:lot13bis` are kept until the merge of the wave into main.

## 10. Backlog opened by this deployment

- Docker container logs have no rotation and no size limit (`json-file`, no `max-size` or `max-file`, no `daemon.json`). Set a rotation (compose `logging` options or `daemon.json`) before the privacy policy states a retention period for technical logs.
