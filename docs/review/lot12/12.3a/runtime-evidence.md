# Lot 12, sub-lot 12.3a: MUX video API (direct upload, signed webhook, sync, removal)

Branch `feature/lot_12`, base `e935a4f`. Local run only (Docker PostgreSQL 16 on 5433, API on 5243, `Email__Provider=Logging`). The app test accounts are "the coach" and "the member", the admin is admin #1. No token, one-time code, signed upload URL, secret, connection string or address appears below: codes were read from the API log into a variable, tokens stayed in a gitignored `tokens.json`, the upload URL stayed in a variable (host printed only) and the webhook HMAC was computed from `Mux__WebhookSecret` inside the command. MUX playback, asset and upload ids are not secrets and appear as they are. API logs (`*.log`) and JSON captures (`*-response.json`) stayed local, gitignored.

Run start read from the database before the first write: `2026-11-10 09:49:11.312393+00` (bound of the fixture cleanup).

Test video: `mentora-mux-test.mp4`, 33.24 MB, one file in the OneDrive test folder (decided by François, replaces the path of the brief).

---

## 1. Pre-flight

```
TestRecipients=True AdminEmails=True AdminSecretLength=64 MuxTokenId=True MuxTokenSecret=True MuxWebhookSecret=True MuxCorsOrigin=http://localhost:4200 TestVideo=True
git: clean, HEAD e935a4f | docker: mentora-postgres-local Up | port 5243 free
last applied migration 20261109160428_Lot12_2b_AdminAuditLog = last migration file
```

## 2. Decisions applied

12.I, 1A, 2A, 3A and "one video per exercise" as in the brief. Interpretations accepted by François after the build report:
1. `HasSentinel(-1)` on `EXERCISE_VIDEO_STATUS`: `Uploading` became the CLR default (0); without a sentinel EF would omit the column on insert and the database default would store `WAITING`.
2. Down of the migration: no `''` back-fill of the asset id (it would collide in `UX_EXERCISE_VIDEOS_MUX_ASSET`); a DO block first raises when UPLOADING rows, rows without an asset id or `EXERCISE_VIDEO_*` audit rows exist.
3. An asset event applies to a row without asset id only when the asset comes from the row's current upload (the passthrough, which is the row id, survives replacements).
4. Sync when MUX answers 404: ERRORED with `UPLOAD_NOT_FOUND` or `ASSET_NOT_FOUND`.
5. The admin detail uses the admin DTO and shows every status; the READY-only rule applies to the app routes and program trees.
6. `MuxNotConfiguredException` (503 `MUX_NOT_CONFIGURED`) next to `MuxUnavailableException` (502 `MUX_UNAVAILABLE`); webhook event row and video change in one `SaveChanges`; 413 above 1 MB; the video writes refuse a disabled admin (401) like the other admin writes; DELETE answers 200 with `data` null following `UserDevicesController.cs:42`.

Fix made during P6: the webhook 401 carried a ProblemDetails body (`[ApiController]` maps a bare `StatusCode(401)`); it now answers with an empty body (`EmptyResult`). P6 was rerun on the fixed build.

## 3. Files

New:
- `Mentora.Core`: `Entities/MuxWebhookEvent.cs`, `Enums/MuxWebhookEventStatus.cs`, `Exceptions/MuxUnavailableException.cs`, `Exceptions/MuxNotConfiguredException.cs`, `Options/MuxOptions.cs`, `DTOs/AdminExercise/AdminExerciseVideoDtos.cs`, `DTOs/Mux/MuxWebhookResult.cs`, `Interfaces/IAdminExerciseVideoService.cs`, `Interfaces/IMuxWebhookService.cs`
- `Mentora.Infrastructure/Services/Mux/`: `MuxClient.cs` (typed client, no SDK), `MuxModels.cs` (shared JSON parsing), `MuxOptionsValidator.cs`, `MuxWebhookSignature.cs`, `MuxVideoRules.cs` (state rules shared by webhook and sync, exercise row lock), `ExerciseVideoMapping.cs` (app and admin views), `AdminExerciseVideoService.cs`, `MuxWebhookService.cs`
- `Mentora.Infrastructure/Persistence/Configurations/MuxWebhookEventConfiguration.cs`
- `Mentora.Infrastructure/Persistence/Migrations/20261110104601_Lot12_3a_MuxVideos.cs` (+ `.Designer.cs`)
- `Mentora.API/Controllers/Admin/AdminExerciseVideosController.cs`, `Mentora.API/Controllers/Internal/MuxWebhookController.cs`

Modified: `ExerciseVideo.cs`, `ExerciseVideoStatus.cs`, `EnumMappings.cs`, `AdminAuditAction.cs`, `AdminExerciseDtos.cs` (list `VideoStatus`, detail `Video` type), `ProgramExerciseResponse.cs` (last property `Video`), `ExerciseVideoConfiguration.cs`, `MentoraDbContext.cs`, model snapshot, `ExerciseService.cs`, `ProgramService.cs` (`Include(e => e.Video)` on both exercise detail loads: one statement, no N+1), `AdminExerciseService.cs`, `GlobalExceptionMiddleware.cs`, `Program.cs`.

No NuGet package added. `Mentora.Admin`, nginx and Flutter untouched (`git status` on `Mentora.Admin`: empty).

## 4. Migration `20261110104601_Lot12_3a_MuxVideos`

Up: asset id nullable; columns `EXERCISE_VIDEO_MUX_UPLOAD_ID` varchar(100), `EXERCISE_VIDEO_UPLOAD_EXPIRES_DATE` timestamptz, `EXERCISE_VIDEO_ERROR` varchar(500); table `MUX_WEBHOOK_EVENTS`; indexes `UX_EXERCISE_VIDEOS_MUX_UPLOAD`, `UX_MUX_WEBHOOK_EVENTS_MUX_ID`, `IX_MUX_WEBHOOK_EVENTS_CREATED_DATE`; CHECKs by `migrationBuilder.Sql`: `CK_EXERCISE_VIDEOS_STATUS` re-created with UPLOADING, WAITING, READY, ERRORED; new `CK_EXERCISE_VIDEOS_MUX_REF`; new `CK_MUX_WEBHOOK_EVENTS_STATUS`; `CK_ADMIN_AUDIT_LOG_ACTION` re-created with six values.
Down: guard DO block, then the inverse of each step (CHECKs restored to three and four values), asset id NOT NULL without back-fill.

Build before applying: `19 Avertissement(s)` (all CS1573), `0 Erreur(s)`; `Mentora.Admin` `0 Avertissement(s)`, `0 Erreur(s)`; `has-pending-model-changes`: `No changes have been made to the model since the last migration.` (30 warn lines; 32 at 12.2b, the status sentinel warning disappeared).

## 5. Runtime proofs

### P0 Contract before any change (e935a4f)

Startup: no migration applied, 0 fail/crit lines. The local database had no program, so a fixture program was assigned to the member by the coach API from the Mentora template "Prise de masse — débutant" (program `5647065a-797e-43db-9830-e59bfe998303`, removed in P10).

```
member program tree: 200, 52 exercises, 13 distinct
program exercise keys: programExerciseId,exerciseId,exerciseName,exerciseInstructions,exerciseVideoUrl,exerciseImageUrl,exerciseMuscleGroup,exerciseEquipment,position,loadType,customNote,prescribedSets,prescribedReps,prescribedWeightKg,restSecondsAfterSeries,workSeconds,restWorkSeconds,actualSets,actualReps,actualWeightKg,actualRpe,memberFeedback,lastPerformed
EX001 Développé couché barre (1194fd80-be00-4630-907e-80609ef0f331): coach detail 200 video=null | member detail 200 video=null (32 keys, video last)
coach list: 200, 275 items, 32 keys per item, items with a video: 0
API stopped, port 5243 in use: False
```

### P1 Migration applied (Development, Mux vars set)

```
[EMAIL] routing mode=TEST provider=Logging testRecipients=4 productionBcc=0
[MUX] configured=True corsOrigin=http://localhost:4200 apiBase=api.mux.com
Applying migration '20261110104601_Lot12_3a_MuxVideos'.
```

Schema read in `BEGIN READ ONLY ... ROLLBACK`:

```
EXERCISE_VIDEOS: EXERCISE_VIDEO_MUX_ASSET_ID varchar(100) NULL | EXERCISE_VIDEO_ERROR varchar(500) NULL | EXERCISE_VIDEO_MUX_UPLOAD_ID varchar(100) NULL | EXERCISE_VIDEO_UPLOAD_EXPIRES_DATE timestamptz NULL
MUX_WEBHOOK_EVENTS: ID uuid | MUX_ID varchar(100) NOT NULL | TYPE varchar(80) NOT NULL | OBJECT_ID varchar(100) | STATUS varchar(10) NOT NULL | RESULT varchar(200) | PAYLOAD text NOT NULL | CREATED_DATE timestamptz NOT NULL
CK_EXERCISE_VIDEOS_STATUS     IN ('UPLOADING','WAITING','READY','ERRORED')
CK_EXERCISE_VIDEOS_MUX_REF    upload id IS NOT NULL OR asset id IS NOT NULL
CK_MUX_WEBHOOK_EVENTS_STATUS  IN ('PROCESSED','IGNORED')
CK_ADMIN_AUDIT_LOG_ACTION     EXERCISE_CREATED, EXERCISE_UPDATED, EXERCISE_ACTIVATED, EXERCISE_DEACTIVATED, EXERCISE_VIDEO_UPLOAD_CREATED, EXERCISE_VIDEO_REMOVED
indexes: UX_EXERCISE_VIDEOS_EXERCISE, UX_EXERCISE_VIDEOS_MUX_ASSET, UX_EXERCISE_VIDEOS_MUX_UPLOAD (new), UX_MUX_WEBHOOK_EVENTS_MUX_ID, IX_MUX_WEBHOOK_EVENTS_CREATED_DATE
counts: EXERCISE_VIDEOS 0 | MUX_WEBHOOK_EVENTS 0 | ADMIN_AUDIT_LOG 4 (12.2b)
```

### P2 Startup

Production, `--no-launch-profile`, generated secrets, connection string to a database that does not exist (`mentora_p2_does_not_exist`, fake user):

```
without Mux vars: Unhandled exception. Microsoft.Extensions.Options.OptionsValidationException: Mux:TokenId must be set as an environment variable outside Development.
with generated Mux vars (https CorsOrigin): [MUX] configured=True corsOrigin=https://admin.p2.invalid apiBase=api.mux.com
                                            then Npgsql.PostgresException 28P01: password authentication failed for user "p2_fake_user"  (refusal from the database, after MUX validation)
Development without Mux vars: [MUX] configured=False corsOrigin=none apiBase=api.mux.com
   POST video-uploads 503 MUX_NOT_CONFIGURED | POST video/sync 503 MUX_NOT_CONFIGURED | DELETE video 503 MUX_NOT_CONFIGURED | POST /api/v1/webhooks/mux 503 MUX_NOT_CONFIGURED
Development with Mux vars: see P1 ([MUX] configured=True corsOrigin=http://localhost:4200)
```

### P3 Authorization and scope

A coach-owned exercise did not exist locally: one was created through the coach API (`l123a coach fixture`, `531c4331-2790-4bed-9af3-afef3c742b3c`, removed in P10).

```
POST   .../video-uploads | no token 401 | coach token 401 | member token 401 | unknown id 404 | coach exercise 404
POST   .../video/sync    | no token 401 | coach token 401 | member token 401 | unknown id 404 | coach exercise 404
DELETE .../video         | no token 401 | coach token 401 | member token 401 | unknown id 404 | coach exercise 404
sync and DELETE on EX001 without a video row: 404 "Exercise ... has no video."
```

### P4 Real MUX upload (EX001)

```
POST video-uploads -> 201 | data keys: uploadId,uploadUrl,expiresAt,video
uploadId am1PNpf7Dww02ELN00G7zj6d6Iwn00uyUfh2E00k2ddV4SI | upload URL host: direct-uploads-oci-us-ashburn-1-vop1.mux.com | expiresAt 2026-11-10T14:01:25Z (now + 3600 s)
video: {"status":"UPLOADING","playbackId":null,"durationSeconds":null,"aspectRatio":null,"error":null,...}
curl PUT (33.24 MB, Content-Type video/mp4) -> HTTP 200 in 10 s
sync #1 (17 s) -> 200 READY  (UPLOADING -> asset created -> asset ready in one sync)
row: id 7eda3a51-981e-4a62-8100-807809b42cc6 | READY | upload am1PNpf7Dww02ELN00G7zj6d6Iwn00uyUfh2E00k2ddV4SI | asset UPuVZmahn4pHE3S2Y2MNWs8f8L5xu024fDxCjNQn0143c
     playback ushAtXsHMz018q018n02NUimJ00xjA02OPklfDuizmv6u5XY | PUBLIC | 54.42 s | 1:1 | error null | xmin 2266
coach detail EX001: video {"playbackId":"ushAtXsHMz018q018n02NUimJ00xjA02OPklfDuizmv6u5XY","status":"READY","durationSeconds":54.42,"aspectRatio":"1:1"}
member detail EX001: same video
program tree: EX001 is in the fixture program, its 4 entries carry the READY video; the 48 other entries carry "video": null
```

### P5 Contract after (P0 captures replayed, key by key)

```
member-program : 52 differences
   x4  ADDED $.data.blocks[].blocks[].blocks[].sessions[].circuits[].exercises[].video (position 24 of 24) = READY video of EX001
   x48 ADDED $.data.blocks[].blocks[].blocks[].sessions[].circuits[].exercises[].video (position 24 of 24) = null
coach-detail   : 1 difference  CHANGED $.data.video null -> READY video
member-detail  : 1 difference  CHANGED $.data.video null -> READY video
coach-list     : items 275 -> 276, only after: the P3 coach fixture | common items: 1 difference (EX001 video null -> READY), same order True
no REMOVED key, no ORDER difference anywhere
```

### P6 Webhook (synthetic, signed in the command)

```
events before: 3 (see note) | row before: xmin 2266 READY 2026-11-10 13:01:44.48586+00
1 valid video.asset.ready (P4 row)   -> 200 {"processed":true}   row after: xmin 2266 (unchanged values: no write)
2 same event id again                -> 200 {"duplicate":true}   row after: xmin 2266 | event rows with that id: 1
3 bad signature (other key)          -> 401 (empty body)
4 t 10 minutes old, correctly signed -> 401 (empty body)
5 missing Mux-Signature header       -> 401 (empty body)
5b header without v1                 -> 401 (empty body)
6 unknown asset, correctly signed    -> 200 {"ignored":true}
7 invalid JSON, correctly signed     -> 400 "Invalid JSON payload."
8 unhandled type, correctly signed   -> 200 {"ignored":true}
log: [MUX] event ... READY (unchanged) | duplicate | webhook rejected reason=signature | stale | missing | format | no matching video | body is not JSON | type not handled
MUX_WEBHOOK_EVENTS: video.asset.ready IGNORED "no matching video" 2 | IGNORED "payload without asset id" 1 | PROCESSED "video 7eda3a51-... READY (unchanged)" 1 | video.asset.static_renditions.ready IGNORED "type not handled" 2
```

Note: a first P6 attempt read the P4 row with broken quoting, so its first event had no asset id (IGNORED "payload without asset id"); it also showed the ProblemDetails 401 body fixed above. Its 3 event rows are counted above and were removed in P10.

### P7 Replacement (EX001 while READY)

```
before: 1 row | 7eda3a51-... | READY | upload am1PNpf7... | asset UPuVZmahn4... 
POST video-uploads -> 201 | new uploadId GYAIti5pFNYajPvPTnNHHLrlo6DHioJj01ERI9X3fe4w | url host direct-uploads-oci-us-phoenix-1-vop1.mux.com
after:  1 row | 7eda3a51-... (same id) | UPLOADING | upload GYAIti5p... | asset null | playback null
MUX GET previous asset UPuVZmahn4... -> HTTP 404 | MUX GET new upload -> 200 waiting
audit: {"uploadId": "GYAIti5p...", "previousAssetId": "UPuVZmahn4...", "previousUploadId": "am1PNpf7..."}
log: [MUX] replace video=7eda3a51-... asset UPuVZmahn4... deleted
```

### P8 Concurrency (EX002, d4e29166-4101-4fae-bac6-7281a082c7f2)

```
rows before 0
parallel POST -> 201 uploadId scLlQC6Ei4ULjTA1ug1P1lM9JhO9eLSs25xzsmviP54 UPLOADING
parallel POST -> 201 uploadId u2v01lEX0002uydr9NE2LyY9Tq10059cYOF02WLx01c00JLyek UPLOADING
after: 1 row | upload scLlQC6E... | UPLOADING
MUX upload scLlQC6E... -> 200 waiting (kept) | MUX upload u2v01lEX... -> 200 cancelled (losing upload)
log: upload created u2v01lEX... then upload created scLlQC6E... then replace ... upload u2v01lEX... cancelled (serialised by the exercise row lock)
```

### P9 Removal

```
EX001 (UPLOADING GYAIti5p...): DELETE -> 200 {"success":true,"data":null,...} | rows 0 | MUX upload -> cancelled | DELETE again -> 404 "has no video."
EX002 (UPLOADING scLlQC6E...): DELETE -> 200 | rows 0 | MUX upload -> cancelled | DELETE again -> 404
audit: EXERCISE_VIDEO_UPLOAD_CREATED 4 | EXERCISE_VIDEO_REMOVED 2 ({"assetId": null, "uploadId": ...} each)
```

The asset-delete branch of DELETE was not hit (both rows UPLOADING); the same best-effort helper deleted a real asset in P7.

### P10 Cleanup

MUX (every id created by these tests, direct GET, status only):

```
upload P4 am1PNpf7Dww02ELN00G7zj6d6Iwn00uyUfh2E00k2ddV4SI -> 200 asset_created (terminal; its asset is deleted)
asset  P4 UPuVZmahn4pHE3S2Y2MNWs8f8L5xu024fDxCjNQn0143c   -> 404
upload P7 GYAIti5pFNYajPvPTnNHHLrlo6DHioJj01ERI9X3fe4w     -> 200 cancelled
upload P8 scLlQC6Ei4ULjTA1ug1P1lM9JhO9eLSs25xzsmviP54     -> 200 cancelled
upload P8 u2v01lEX0002uydr9NE2LyY9Tq10059cYOF02WLx01c00JLyek -> 200 cancelled
assets in the MUX environment: 0 (free plan: 0 of 10 used)
```

Database: dry run first, then the COMMIT file on approval. The two files have 49 lines each and differ by the last line only (`Compare-Object`: `ROLLBACK;` versus `COMMIT;`). The DO block raises unless exactly these counts are deleted and the audit rows are unchanged; deletes are bounded by the fixture ids and the run start. Both runs printed:

```
NOTICE:  deleted: mux events 6 | program exercises 52 | circuits 16 | sessions 12 | blocks 6 | programs 1 | coach exercises 1 | audit kept 10 -> 10
 exercise_videos 0 | mux_events 0 | audit_total 10 | audit_this_run 6 | programs 0 | program_exercises 0 | exercises 275 | coach_exercises 0
```

Final read-only check after the COMMIT:

```
rows created since run start: ADMIN_AUDIT_LOG 6 (kept, accepted) | ADMIN_OTPS 7 | ADMIN_REFRESH_TOKENS 6 | AUTH_OTPS 8 | AUTH_REFRESH_TOKENS 8 (login side effects, not fixtures, as in previous lots)
EXERCISE_VIDEOS 0 | MUX_WEBHOOK_EVENTS 0 | PROGRAMS 0 | EXERCISES 275 | coach-owned exercises 0
ADMIN_AUDIT_LOG: EXERCISE_UPDATED 4 (12.2b) | EXERCISE_VIDEO_UPLOAD_CREATED 4 (this run) | EXERCISE_VIDEO_REMOVED 2 (this run)
last migration 20261110104601_Lot12_3a_MuxVideos
API stopped, port 5243 in use: False | fail/crit lines in every API log of this run: 0
```

### P11 Build and leak checks

```
API: 19 Avertissement(s) (all CS1573, baseline), 0 Erreur(s) | Mentora.Admin: 0 Avertissement(s), 0 Erreur(s)
has-pending-model-changes: No changes have been made to the model since the last migration.
```

Leak check results: section 7.

## 6. Notes for 12.3b

- CSP `connect-src` for the browser PUT: `https://*.mux.com`. Two different upload hosts were observed in one session (`direct-uploads-oci-us-ashburn-1-vop1.mux.com` in P4, `direct-uploads-oci-us-phoenix-1-vop1.mux.com` in P7), so a single host is not enough.
- `img-src` needs `https://image.mux.com` for the thumbnail (3A); the hosted player link opens outside the admin.
- Admin list: new `videoStatus` key next to `hasVideo`. Admin detail `video` now carries `status, playbackId, durationSeconds, aspectRatio, error, uploadExpiresAt, updatedDate`; the current admin models still deserialize it (build green).
- The admin polls `POST .../video/sync` while a video is UPLOADING or WAITING; locally this is the only way to progress (MUX cannot reach localhost).
- The admin origin sent as `cors_origin` comes from `Mux__CorsOrigin` (api-demo and PROD runbooks need `Mux__TokenId`, `Mux__TokenSecret`, `Mux__WebhookSecret`, `Mux__CorsOrigin`; outside Development the API refuses to start without them).

## 7. Leak checks

Patterns proven first on known files: JWT pattern on `docs/review/lot7/claims-after-4a.json` = 2; console OTP marker (literal match) on `Mentora.Infrastructure/Services/AuthService.cs` = 1; Logging-provider mail marker (literal match) on `Mentora.Infrastructure/Services/Email/LoggingEmailSender.cs` = 1; address pattern on `docs/review/lot11/11.4/runtime-evidence.md` = 14; URL-with-query pattern `https://[^ ]*[?][^ ]*` on `Mentora.Admin/wwwroot/index.html` = 1.

On the explicit list of files to commit (the 36 files of `git diff --cached --name-only` for commit 1, plus this file for commit 2): 0 match for every pattern, 0 file with any match.

Local files left in this folder, all gitignored and not committed: the API logs (`*.log`) and the JSON captures and MUX id list (`*-response.json`). `tokens.json` was deleted after the proofs (`Test-Path` False).
