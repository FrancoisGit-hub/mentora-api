# Lot 13: VPS deployment evidence (18/11/2026)

Branch `feature/lot_13`, HEAD `9882892` at build time. Procedure: section "Lot 13" of `docs/review/lot12/12.5a/prod-runbook.md`. Times are UTC (VPS clock). No secret, token, login code or email address in this file.

## 1. Image

- Pre-checks on the PC: clean tree, HEAD `9882892`, `.dockerignore` contains `docs` and `docs/review`.
- `docker build --platform linux/amd64 -f Mentora.API/Dockerfile -t mentora-api:lot13 .`: finished in 243.4 s, `naming to docker.io/library/mentora-api:lot13`, no error.
- Archive: `docker save` to `%TEMP%`, then GZipStream (Windows PowerShell 5.1): `mentora-api-lot13.tar.gz`, 255,144,835 bytes. `scp`: 100 %, 243 MB, 01:10.
- VPS disk before loading: `/dev/vda1 232G, 14G used, 219G available, 6%`.
- `docker load -i ~/mentora-api-lot13.tar.gz`: `Loaded image: mentora-api:lot13`, ID `cbe814730a92`, created 2026-11-18 09:44:13 UTC. Previous image of both API services: `mentora-api:lot12-v2` (`502f131bb3ae`).

## 2. Backups (09:49)

- api-demo: `~/backup-20261118-0949.dump`, 563K. PROD: `~/backup-prod-20261118-0949.dump`, 410K. No media folder yet (first deployment).
- Compose: `docker-compose.yml.pre-lot13`. nginx: `~/mentora.conf.pre-lot13`, `~/mentora-prod.conf.pre-lot13`.

## 3. Read-only check before the migration (runbook step 3)

| | api-demo (`mentora_db`) | PROD (`mentora_prod`) |
|---|---|---|
| Last applied migration | `20261110104601_Lot12_3a_MuxVideos` | `20261110104601_Lot12_3a_MuxVideos` |
| Six new MESSAGES columns | 0 rows | 0 rows |
| `CK_MESSAGES_TYPE`, `CK_MESSAGES_AUDIO_FIELDS` | 0 rows | 0 rows |
| total_rows | 25 | 0 |
| ck_type_violations / ck_audio_violations / null_content | 0 / 0 / 0 | 0 / 0 / 0 |
| empty_content_info_only | 0 | 0 |
| MESSAGES size | 48 kB | 24 kB |

## 4. Host folders and compose (09:50)

- `~/mentora/media-demo` and `~/mentora/media-prod`: `drwxr-x--- mentora mentora`.
- `docker inspect --format '{{.Config.Image}}' mentora-api-prod`: `mentora-api:lot12-v2` (rollback tag of api-prod).
- The compose was changed by an `awk` script writing a new file, checked with `diff` before being copied in place (no value displayed). `diff` output:

    41a42,44
    >       - Audio__StoragePath=/app/media
    >     volumes:
    >       - ./media-demo:/app/media
    48c51
    <     image: mentora-api:lot12-v2
    ---
    >     image: mentora-api:lot13
    65a69,72
    >       - Audio__StoragePath=/app/media
    >       - Cors__DebugOrigins=http://localhost:8100
    >     volumes:
    >       - ./media-prod:/app/media

- `docker compose config --quiet`: `compose valide`. Filtered `docker compose config`: `Audio__StoragePath: /app/media` and `image: mentora-api:latest` with source `/home/mentora/mentora/media-demo`, target `/app/media` (api); `Audio__StoragePath: /app/media`, `Cors__DebugOrigins: http://localhost:8100` and `image: mentora-api:lot13` with source `/home/mentora/mentora/media-prod`, target `/app/media` (api-prod).

## 5. nginx

- Before: `mentora.conf:40` and `mentora-prod.conf:36`: `client_max_body_size 25M;`. After `sed`: `client_max_body_size 8m;` in both. `nginx -t`: syntax ok, test successful; reload at 10:06:45.
- Incident: the runbook rollback command (copy of the `.pre-lot13` files) was then run by mistake, although `nginx -t` had succeeded. The files went back to 25M on disk; the running configuration stayed at 8m (no reload in between). Corrected at once: `sed` again (both files at `8m`), `nginx -t` ok, reload at 10:07:29. Final state: 8m on disk and in memory. Runbook step 6 now marks the rollback command "only if `nginx -t` fails".

## 6. api-demo

- `docker tag mentora-api:latest mentora-api:pre-lot13`, `docker tag mentora-api:lot13 mentora-api:latest`, `docker compose up -d --no-deps --force-recreate api`: `Container mentora-api Started`. `Applying migration` count: 1.
- Startup lines (login code and email body lines excluded):

    [EMAIL] routing mode=TEST provider=Smtp testRecipients=4 productionBcc=0
    [CORS] origins=0 localhost=True
    [CORS] debugOrigins=0
    [MUX] configured=True corsOrigin=http://localhost:4200 apiBase=api.mux.com
    [AUDIO] configured=True ffmpeg=True ffprobe=True storage=True maxConcurrent=2 timeoutSeconds=10
    [ADMIN] auth emails=4 accessMinutes=15 refreshHours=12
    Applying migration '20261117234001_Lot13_2_VoiceMessages'.
    [ADMIN] accounts listed=4 created=0 reenabled=0 disabled=0
    Application started. Press Ctrl+C to shut down.

  No `fail:`, `crit:` or `Exception` line.
- After the migration: `MESSAGE_TYPE` `TEXT` 25 rows; constraints `CK_MESSAGES_AUDIO_FIELDS` and `CK_MESSAGES_TYPE` present.
- Checks without token (PowerShell on the PC): health 200; both upload routes without body: 415 and 415; both upload routes with a multipart body and no token: 401 and 401; listening route: 401; preflight from `http://localhost:8100`: `204 [http://localhost:8100]` (allowed by the Development mode).
- Finding: without a multipart content type, the upload routes answer 415 before the token check. The `[Consumes]` constraint is evaluated when the route is selected, before authorization. No information disclosed, no access. A multipart request without token gets 401. Runbook step 9 corrected accordingly.

## 7. Functional proof on api-demo

- Account: the demo member account of the demo data (not a real mailbox), its primary coach, conversation `ce4523fe-6a74-4a08-a55d-316ae519f4a2` with 8 messages, last message 2026-09-14 16:14:17.920295+00.
- `POST /api/v1/auth/otp/request`: 200. Login code read from the container logs: 6 digits (never printed). `otp/verify`: access token obtained (never printed).
- Upload of `tone-3s.aac` (AAC ADTS, 3 s, 25,395 bytes, test file of 13.4) on `POST /api/v1/member/coaches/{coachId}/conversation/messages/audio`: 201, `"messageType":"AUDIO"`, `"durationSeconds":3`, `conversationId` equal to the conversation, `mediaUrl` `/api/v1/conversations/ce4523fe-6a74-4a08-a55d-316ae519f4a2/messages/dc90cfef-d538-40d4-a0a0-b43f43601cb5/audio`.
- Listening on that `mediaUrl`: 200. SHA256 of the sent file and of the downloaded file: both `3e0cb40295bb2e6b81b32796330f6f092d9e77c00db0f59d70ec000d6fda5b9c`.
- Host file: `~/mentora/media-demo/ce4523fe-.../dc90cfef-d538-40d4-a0a0-b43f43601cb5.aac`, 25,395 bytes, `root root`, `-rw-r--r--`, 10:18.
- Cleanup, one SQL file. Dry run ending with ROLLBACK: one `AUDIO` row with path `ce4523fe-6a74-4a08-a55d-316ae519f4a2/dc90cfef-d538-40d4-a0a0-b43f43601cb5.aac`, `DELETE 1`, `UPDATE 1`, `audio_restant = 0`, `ROLLBACK`. Same file ending with COMMIT: same output, `COMMIT`.
- File removed by the container: `removed '/app/media/ce4523fe-.../dc90cfef-....aac'`; `test -e`: `ABSENT`; host folder `total 0`; listening again: 404.
- After cleanup: `MESSAGE_TYPE` `TEXT` 25 only; conversation back to 8 messages and last message date 2026-09-14 16:14:17.920295+00. Test files on the VPS removed (3 of 3 "No such file"). Empty conversation folder removed by the container (`rmdir: removing directory`, 10:24); `media-demo` keeps only `.incoming` (`root root`, `drwxr-xr-x`).
- Not proven on the VPS: the SignalR broadcast (proven locally in 13.4, Q1 and Q2).

## 8. PROD

- `docker compose up -d --no-deps --force-recreate api-prod` (image `mentora-api:lot13` from the compose): `Container mentora-api-prod Started`. `Applying migration` count: 1.
- Startup lines:

    [EMAIL] routing mode=TEST provider=Smtp testRecipients=4 productionBcc=0
    [CORS] origins=1 localhost=False
    [CORS] origin https://admin.mentorapp.fr
    [CORS] debugOrigins=1
    [CORS] debug origin http://localhost:8100
    [MUX] configured=True corsOrigin=https://admin.mentorapp.fr apiBase=api.mux.com
    [AUDIO] configured=True ffmpeg=True ffprobe=True storage=True maxConcurrent=2 timeoutSeconds=10
    [ADMIN] auth emails=4 accessMinutes=15 refreshHours=12
    Applying migration '20261117234001_Lot13_2_VoiceMessages'.
    [ADMIN] accounts listed=4 created=0 reenabled=0 disabled=0
    Application started. Press Ctrl+C to shut down.

  No `fail:`, `crit:` or `Exception` line.
- After the migration: last migration `20261117234001_Lot13_2_VoiceMessages`; MESSAGES 0 rows; both constraints present.
- Checks without token (curl from the VPS on the public host, through nginx): `health 200`; `envoi member/coaches 401`; `envoi coach/members 401`; `ecoute 401`; preflight from `http://localhost:8100`: `HTTP/2 204` and `access-control-allow-origin: http://localhost:8100`; preflight from `http://localhost:3000`: `HTTP/2 204` without `access-control-allow-origin`.
- No voice message sent on PROD. Functional tests on PROD are done by PJ with his own accounts.

## 9. Rollback ready

- api-demo: `mentora-api:pre-lot13` (`502f131bb3ae`). api-prod: `mentora-api:lot12-v2`, to put back on the `image:` line of the compose. The Down migration is never needed (runbook step 11). Both tags are kept until the merge.

## 10. Archives

- VPS: `rm ~/mentora-api-lot13.tar.gz`, then `ls`: `No such file or directory`.
- PC: `Remove-Item` of `%TEMP%\mentora-api-lot13.tar.gz`, then `Test-Path`: False (`.tar.gz`) and False (`.tar`).
