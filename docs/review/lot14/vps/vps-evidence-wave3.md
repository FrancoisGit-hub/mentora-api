# Lot 14 wave 3: VPS deployment evidence (25/11/2026)

Lot 14 wave 3 (14.9a: the 12 block types of the mobile app, with the data migration of `STANDARD` to `SERIE_SIMPLE`; 14.9b: push notifications through Firebase Cloud Messaging for `CHAT_MESSAGE`, `SESSION_BOOKED`, `SESSION_CANCELLED` and `MEMBER_JOINED`; review fix N1) deployed on 25/11/2026 to api-demo, then to PROD (api-prod). The admin did not change and was not republished.

Branch `feature/lot_14`. The image holds the code up to `2788d0a` (fix N1); commit `83edf40` and the commit of this file only add files under `docs/review`, which `.dockerignore` excludes from the image. Procedure: section "Lot 14 vague 3 (25/11)" of `docs/review/lot12/12.5a/prod-runbook.md`. Every command was typed by François on his PC (PowerShell) or on the VPS (SSH); every output was checked before the next step. Times are UTC (VPS clock). No secret, token, login code, Firebase key content or real email address in this file.

Sources of the values:

- "(re-read)": output of read-only commands run on the VPS after the second start of api-prod (18:13), when this file was prepared.
- "(live)": output read during the run itself, that can no longer be read again: each API container was recreated for its second start (Docker keeps the logs of the current container only), and the test rows were deleted by the cleanup.

## 1. Image (re-read)

- `mentora-api:lot14w3`, ID `812f425c8d98`, 943MB. `mentora-api:latest` (api-demo) has the same ID.
- Previous image of both API services: `mentora-api:lot14w2`, ID `36be7e8ba5d6`, also tagged `mentora-api:pre-lot14w3`.
- Sent as a compressed archive `~/mentora-api-lot14w3.tar.gz` (244M, 17:28), then `docker load`. Before the build, a search for the Firebase key file in the Docker build context found nothing (live).
- VPS disk: `/dev/vda1 232G, 14G used, 218G available, 6%`.

## 2. Backups at 17:30 (re-read)

- api-demo database (`mentora_db`): `~/backup-20261125-1730.dump`, 568K. PROD database (`mentora_prod`): `~/backup-prod-20261125-1730.dump`, 461K.
- Voice message folders: `~/backup-media-demo-20261125-1730.tgz`, 180K; `~/backup-media-prod-20261125-1730.tgz`, 766K.
- Compose: `~/mentora/docker-compose.yml.pre-lot14w3`, 3.8K.

## 3. Read-only prechecks before the migration (re-read)

`docs/review/lot14/14.9a/precheck.sql` run on both databases (`BEGIN` ... `ROLLBACK`), outputs kept in `~/w3-precheck-demo-avant.txt` and `~/w3-precheck-prod-avant.txt` (17:31).

| Location | api-demo before | PROD before |
|---|---|---|
| `PROGRAM_CIRCUITS.PROGRAM_CIRCUIT_MODE` | `INTERVAL` 8, `STANDARD` 56 | 0 rows |
| `PROGRAM_TEMPLATES.PROGRAM_TEMPLATE_BODY` | 23 rows, 4 with mode `STANDARD`; tokens: `STANDARD` 34, `SERIE_SIMPLE` 0, `INTERVAL` 4 | 3 rows, 2 with mode `STANDARD`; tokens: `STANDARD` 28, `SERIE_SIMPLE` 0, `INTERVAL` 4 |
| `SESSION_TEMPLATES.SESSION_TEMPLATE_CONTENT` | 2 rows, 2 with mode `STANDARD`; tokens: `STANDARD` 2, `SERIE_SIMPLE` 0, `INTERVAL` 0 | 0 rows |
| `SESSION_TEMPLATES.SESSION_TEMPLATE_NATURE` | `STANDARD` 2 | 0 rows |

Session nature tokens inside the jsonb (`STANDARD`, `EVALUATION`): 0 everywhere. The session nature column is not touched by the migration.

## 4. Firebase key (re-read, never opened)

- Copied by François with `scp` from his PC (2379 bytes transferred) to `~/mentora/secrets/service-account-firebase.json`.
- `~/mentora/secrets`: `drwx------ mentora mentora`. The file: `-rw------- mentora mentora 2379` (17:32). It was never opened, printed or hashed.
- The API containers run as root (`id -u` = 0, live) and read the file through the bind mount.
- Mounted read-only at `/etc/mentora/firebase-service-account.json` in both API containers (sections 7 and 10: `RW=false`). Not in the image, not in the repository, not in a folder served by nginx.

## 5. Compose (re-read)

Lines inserted with `awk` after a numbered check of the target lines (live: `inserted=4` for the four Firebase lines, then `replaced=1` for the PROD image line). `diff docker-compose.yml.pre-lot14w3 docker-compose.yml`:

```
42a43
>       - Firebase__ServiceAccountPath=/etc/mentora/firebase-service-account.json
44a46
>       - ./secrets/service-account-firebase.json:/etc/mentora/firebase-service-account.json:ro
51c53
<     image: mentora-api:lot14w2
---
>     image: mentora-api:lot14w3
71a74
>       - Firebase__ServiceAccountPath=/etc/mentora/firebase-service-account.json
74a78
>       - ./secrets/service-account-firebase.json:/etc/mentora/firebase-service-account.json:ro
```

Lines 43 and 46 belong to the service `api` (api-demo), lines 74 and 78 to the service `api-prod`. `docker compose config --quiet`: valid.

## 6. api-demo switch

- `docker tag mentora-api:latest mentora-api:pre-lot14w3`, `docker tag mentora-api:lot14w3 mentora-api:latest`, `docker compose up -d --no-deps --force-recreate api`.
- Migrations applied (live): `20261124221921_Lot14_9a_CircuitModes`, then `20261124234929_Lot14_9b_Notifications`. No `fail:` or `crit:` line (live).
- Tagged startup lines before (`~/w3-lignes-demo-avant.txt`, 9 lines) and after (`~/w3-lignes-demo-apres.txt`, 10 lines). `diff` (re-read) shows one added line only: `9a10`, the `[PUSH]` line. Lines after (re-read, leading spaces removed):

```
[ADMIN] accounts listed=4 created=0 reenabled=0 disabled=0
[ADMIN] auth emails=4 accessMinutes=15 refreshHours=12
[AUDIO] configured=True ffmpeg=True ffprobe=True storage=True maxConcurrent=2 timeoutSeconds=10
[AUTH] otpRateLimit permitLimit=30 windowMinutes=15
[CORS] debugOrigins=0
[CORS] origins=0 localhost=True
[EMAIL] routing mode=TEST provider=Smtp testRecipients=4 productionBcc=0
[INVITE] installUrl=False permitLimit=30 windowMinutes=60
[MUX] configured=True corsOrigin=http://localhost:4200 apiBase=api.mux.com
[PUSH] configured=True projectId=mentora-69209 pollSeconds=3 maxAttempts=3
```

## 7. api-demo after the migration (re-read)

Precheck after (`~/w3-precheck-demo-apres.txt`, 17:51):

| Location | api-demo after |
|---|---|
| `PROGRAM_CIRCUITS.PROGRAM_CIRCUIT_MODE` | `INTERVAL` 8, `SERIE_SIMPLE` 56 |
| `PROGRAM_TEMPLATES.PROGRAM_TEMPLATE_BODY` | 23 rows, 0 with mode `STANDARD`; tokens: `STANDARD` 0, `SERIE_SIMPLE` 34, `INTERVAL` 4 |
| `SESSION_TEMPLATES.SESSION_TEMPLATE_CONTENT` | 2 rows, 0 with mode `STANDARD`; tokens: `STANDARD` 0, `SERIE_SIMPLE` 2, `INTERVAL` 0 |
| `SESSION_TEMPLATES.SESSION_TEMPLATE_NATURE` | `STANDARD` 2 (unchanged) |

Every `STANDARD` block mode became `SERIE_SIMPLE`; `INTERVAL` and the session natures did not move.

- Last migrations of `mentora_db`: `20261124234929_Lot14_9b_Notifications`, `20261124221921_Lot14_9a_CircuitModes`, `20261123042911_Lot14_8_CoachCreatedAudit`.
- Constraints of `NOTIFICATIONS`: `CK_NOTIFICATIONS_ATTEMPT_COUNT`, `CK_NOTIFICATIONS_DATA`, `CK_NOTIFICATIONS_STATUS`, `CK_NOTIFICATIONS_TYPE`, `FK_NOTIFICATIONS_USERS`, `PK_NOTIFICATIONS`.
- Indexes of `NOTIFICATIONS`: `IX_NOTIFICATIONS_STATUS_NEXT_ATTEMPT`, `IX_NOTIFICATIONS_USER_ID`, `PK_NOTIFICATIONS`.
- Mounts of `mentora-api`: `/app/media RW=true`, `/etc/mentora/firebase-service-account.json RW=false`.

## 8. Network proof on api-demo (FCM) and cleanup

Goal: prove from inside the container that the mounted key gets a Google OAuth token and that the FCM v1 call goes through, without a real phone.

- One fake device (`USER_DEVICE_ID` `14930000-0000-4000-8000-000000000001`, fake token starting with `l149v-fake-`) and one pending `CHAT_MESSAGE` notification (`NOTIFICATION_ID` `14930000-0000-4000-8000-000000000002`) for the permanent test member account of api-demo, inserted with fixed identifiers (live).
- Sender log lines (live):

```
[PUSH] device removed deviceId=14930000-0000-4000-8000-000000000001 reason=INVALID_ARGUMENT
[PUSH] sent notificationId=14930000-0000-4000-8000-000000000002 type=CHAT_MESSAGE status=FAILED devices=1 ok=0 removed=1
```

- Notification row afterwards (live): status `FAILED`, error code `ALL_TOKENS_INVALID`, 1 attempt.
- Reading: from inside the container, the API obtained a Google OAuth token with the mounted key and reached FCM v1, which answered about the fake token with `INVALID_ARGUMENT`; the guard then removed the device. No token is written in the logs, only identifiers.
- Cleanup script: `docs/review/lot14/vps/cleanup-l149v-demo.sql`, the exact file run on the VPS (`~/w3-l149v-cleanup.sql`, 7 lines, MD5 `403f4974d093464f825cd52e4a16a779`, ends with `ROLLBACK`).
- Dry run, then a commit copy made with `sed` on the last line only (`~/w3-l149v-cleanup-commit.sql`, MD5 `6e4a3b920106fbdcd3e347f95dfdda9b`); `diff` showed `7c7` (live).
- Real run (live): `DELETE 1` (the notification), `DELETE 0` (the device, already removed by the sender), both leftover counts 0, `COMMIT`.
- Re-read on both databases: 0 test notification, 0 test device, 0 row in `NOTIFICATIONS`.

## 9. PROD switch

- Compose: line 53 (service api-prod) `image: mentora-api:lot14w3` (section 5), then `docker compose up -d --no-deps --force-recreate api-prod`.
- Migrations applied (live): `20261124221921_Lot14_9a_CircuitModes`, then `20261124234929_Lot14_9b_Notifications`. No `fail:` or `crit:` line (live).
- Tagged startup lines before (`~/w3-lignes-prod-avant.txt`, 11 lines) and after (`~/w3-lignes-prod-apres.txt`, 12 lines). `diff` (re-read) shows one added line only: `11a12`, the `[PUSH]` line. Lines after (re-read, leading spaces removed):

```
[ADMIN] accounts listed=4 created=0 reenabled=0 disabled=0
[ADMIN] auth emails=4 accessMinutes=15 refreshHours=12
[AUDIO] configured=True ffmpeg=True ffprobe=True storage=True maxConcurrent=2 timeoutSeconds=10
[AUTH] otpRateLimit permitLimit=30 windowMinutes=15
[CORS] debug origin http://localhost:8100
[CORS] debugOrigins=1
[CORS] origin https://admin.mentorapp.fr
[CORS] origins=1 localhost=False
[EMAIL] routing mode=PRODUCTION provider=Smtp testRecipients=4 productionBcc=2
[INVITE] installUrl=False permitLimit=30 windowMinutes=60
[MUX] configured=True corsOrigin=https://admin.mentorapp.fr apiBase=api.mux.com
[PUSH] configured=True projectId=mentora-69209 pollSeconds=3 maxAttempts=3
```

The mail routing stays in Production mode with its two hidden copies, as since 24/11.

## 10. PROD after the migration (re-read)

Precheck after (`~/w3-precheck-prod-apres.txt`, 18:12):

| Location | PROD after |
|---|---|
| `PROGRAM_CIRCUITS.PROGRAM_CIRCUIT_MODE` | 0 rows |
| `PROGRAM_TEMPLATES.PROGRAM_TEMPLATE_BODY` | 3 rows, 0 with mode `STANDARD`; tokens: `STANDARD` 0, `SERIE_SIMPLE` 28, `INTERVAL` 4 |
| `SESSION_TEMPLATES.SESSION_TEMPLATE_CONTENT` | 0 rows |
| `SESSION_TEMPLATES.SESSION_TEMPLATE_NATURE` | 0 rows |

Only change: the 28 `STANDARD` tokens of the 2 program templates that had them became `SERIE_SIMPLE`.

- Last migrations of `mentora_prod`: `20261124234929_Lot14_9b_Notifications`, `20261124221921_Lot14_9a_CircuitModes`, `20261123042911_Lot14_8_CoachCreatedAudit`.
- Constraints and indexes of `NOTIFICATIONS`: the same 6 and 3 names as on api-demo (section 7).
- `NOTIFICATIONS`: 0 rows.
- Mounts of `mentora-api-prod`: `/app/media RW=true`, `/etc/mentora/firebase-service-account.json RW=false`.

## 11. Second start and current state (re-read)

Each API container was recreated once more after its checks, with the same image and compose.

| Container | Image | Image ID | Started |
|---|---|---|---|
| `mentora-api` | `mentora-api:latest` | `sha256:812f425c8d98efbb...` | `2026-11-25T17:53:04Z` |
| `mentora-api-prod` | `mentora-api:lot14w3` | `sha256:812f425c8d98efbb...` | `2026-11-25T18:13:16Z` |

- `Applying migration` lines in the logs of the current containers: 0 for `api`, 0 for `api-prod`.
- Both current containers log `[PUSH] configured=True projectId=mentora-69209 pollSeconds=3 maxAttempts=3`.
- Logs read with `--since 2026-11-25T17:30:00Z`, which covers the whole life of each current container:

| Count | api | api-prod |
|---|---|---|
| `fail:` or `crit:` | 0 | 0 |
| `[OTP]` | 0 | 0 |
| `[EMAIL-LOG]` | 0 | 0 |
| `warn:` | 131 | 131 |

131 is the known count of one start since wave 2 (seeders, EF Core 20601, data protection; see `vps-evidence-wave2.md`, section 7).

- `GET https://api.mentorapp.fr/api/v1/health`: 200. `GET https://api-demo.mentorapp.fr/api/v1/health`: 200.

## 12. Archives

- VPS: `~/mentora-api-lot14w3.tar.gz` and the copy of the precheck `~/precheck-149a.sql` removed (`ls`: no such file).
- PC: `%TEMP%\mentora-api-lot14w3.tar.gz` removed (`Test-Path`: False).
- Kept until the merge: `~/w3-*`, `docker-compose.yml.pre-lot14w3`, the images `pre-lot14w3` and `lot14w2`, the four backups of 17:30.

## 13. Rollback (not used)

- api-demo: `docker tag mentora-api:pre-lot14w3 mentora-api:latest`; api-prod: `cp docker-compose.yml.pre-lot14w3 docker-compose.yml` (it already holds the Production mail lines of 24/11, see the diff in section 5); then recreate `api` and `api-prod`, each with `--no-deps --force-recreate`.
- No migration `Down`. Limits, detailed in the section "Lot 14 vague 3 (25/11)" of the runbook: the previous image refuses the new block modes in writing, and the `Down` of `Lot14_9a_CircuitModes` refuses to run as soon as a mode other than `SERIE_SIMPLE`, `INTERVAL` or `STANDARD` exists.

## 14. Still to prove

- Real delivery on a phone: on api-demo, with PJ's Android test phone, once the app registers its FCM token through `POST /api/v1/devices`. To do before the Beta starts.
- Notification of a voice message on api-demo (not proven on a server yet).

## 15. Backlog opened by this deployment

- N2 of the wave 3 review (one transaction per notification): lot 16.
- No purge of sent or failed `NOTIFICATIONS` rows yet.
- Docker keeps the logs of the current container only: before recreating a container for a second start, save the proof lines to a file.
- Old API image tags on the VPS: prune after the merge, keeping the last two lots.
