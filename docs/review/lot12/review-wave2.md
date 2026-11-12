# Lot 12, wave 2 code review (MUX videos: 12.3.0, 12.3a, 12.3b)

Branch `feature/lot_12`, diff `3b9d876...HEAD` (commits `e935a4f`, `6e04b63`, `f35dd41`, `3228082`). Read-only review: no build, no run, no database, no network. Skipped as instructed: `*.Designer.cs`, `MentoraDbContextModelSnapshot.cs`, `docs/review/**` except the three evidence files.

No BLOCKER was found. Two SHOULD findings concern the upload path. The other findings are BACKLOG.

---

## Findings

### V1 (SHOULD): the chunked upload ignores the `Range` header of a 308 and assumes the whole chunk arrived

- File: `Mentora.Admin/wwwroot/js/admin.js:130-135`
- What happens: after a non-final chunk, any 308 is taken as "the whole chunk was persisted", and `offset = end` moves on. MUX direct uploads follow the resumable upload protocol: the 308 carries `Range: bytes=0-N`, and N can be lower than `end - 1` (the server may persist only part of a chunk, for example after a slow or broken connection). The script never reads that header:
  ```js
  var good = last
      ? (result.status === 200 || result.status === 201)
      : (result.status === 308);
  if (!good) return { ok: false, status: result.status, network: false };
  offset = end;
  ```
- Scenario: Federico uploads a 1.5 GB file on hotel Wi-Fi. Chunk 40 is only partly persisted, and MUX answers 308 with `Range: bytes=0-327155711`. The script sends chunk 41 from byte 335544320, which leaves a gap. MUX does not complete the upload: it answers 308 again, or 4xx, on the final chunk. The final chunk then fails the `200 || 201` test, the page shows "Le service vidéo a refusé l'envoi (code 308)", and he has to start again from zero. The asset is never corrupted (MUX refuses the gap), but the upload fails where it could have resumed.
- Smallest fix: on a 308, read `xhr.getResponseHeader("Range")`. When it matches `bytes=0-(\d+)`, set `offset = N + 1`. When the header is missing, set `offset = 0` if it was the first chunk, otherwise keep `offset` and retry the chunk. Check first that MUX exposes `Range` through `Access-Control-Expose-Headers`; if it does not, `getResponseHeader` returns null in the browser, and the safe fallback is "retry the same chunk" rather than "advance".

### V2 (SHOULD): replacing or removing an UPLOADING video whose file already reached MUX leaves an orphan asset at MUX

- Files: `Mentora.Infrastructure/Services/Mux/AdminExerciseVideoService.cs:115-116` (replace) and `:231-232` (remove), together with `MuxVideoRules.cs:49-50` and `:152-154`.
- What happens: cleanup decides what to delete from the database row only:
  ```csharp
  await BestEffortAsync("replace", video.ExerciseVideoId, previousAssetId,
      previousStatus == ExerciseVideoStatus.Uploading ? previousUploadId : null);
  ```
  When the row is still UPLOADING but MUX has already created the asset (`video.upload.asset_created` not received yet, or not processed yet locally where no webhook arrives), `previousAssetId` is null. Only `CancelUploadAsync` runs, and MUX refuses to cancel an upload that is already `asset_created` (a non-404 error, logged as "not cancelled"). The late `video.upload.asset_created` and `video.asset.ready` events then match the row by passthrough and are ignored ("upload replaced" / "asset of another upload"). Nothing ever deletes that asset.
- Scenario: the file finishes uploading, and within the next few seconds the admin clicks "Retirer la vidéo" (or "Recommencer" after a wrong file, or opens the form in a second tab). The row is deleted or replaced, and the asset stays at MUX: it keeps being billed for storage and counts toward the free plan's 10-asset limit used for the tests. On api-demo, the same window opens whenever webhook delivery is slow.
- Smallest fix: in `BestEffortAsync`, before cancelling an upload, call `GetUploadAsync(uploadId)`. When it answers `asset_created` with an `asset_id`, call `DeleteAssetAsync(asset_id)` instead of cancelling. This stays best effort and runs after the commit, and the webhook still never calls MUX.

### V3 (BACKLOG): `OnAssetErrored` can move a READY row to ERRORED for the same asset

- File: `Mentora.Infrastructure/Services/Mux/MuxVideoRules.cs:96-103`
- What happens: `IsCurrentAsset` passes for the row's own asset whatever the status, so a `video.asset.errored` for the current asset turns READY into ERRORED, and the apps lose the video (2A). MUX normally sends `asset.errored` only during processing, so this needs an unusual or late event. The class comment promises "never move backwards".
- Smallest fix: in `OnAssetErrored`, return `new(false, "video ... already READY")` when `v.ExerciseVideoStatus == Ready && v.ExerciseVideoMuxAssetId == asset.Id`.

### V4 (BACKLOG): the MUX HTTP call runs inside the transaction while the exercise row is locked

- File: `Mentora.Infrastructure/Services/Mux/AdminExerciseVideoService.cs:43-62`
- What happens: `CreateDirectUploadAsync` (15 s timeout) runs after `SELECT ... FOR UPDATE`. A slow MUX keeps the lock and a database connection for up to 15 s, and webhook events and syncs for the same exercise wait behind it. This was chosen on purpose: it serialises two uploads (P8), and an orphan upload expires on its own. It is acceptable at current volume.
- Smallest fix (later): create the MUX upload before opening the transaction, then cancel it when the locked section fails or sees that another upload won.

### V5 (BACKLOG): the XHR has no timeout, so a stalled chunk hangs forever

- File: `Mentora.Admin/wwwroot/js/admin.js:79-87`
- What happens: `xhr.timeout` is never set, so `ontimeout` never fires. A connection that stalls without being reset keeps the progress bar frozen until the admin clicks "Annuler l'envoi". The retry logic never runs for this case.
- Smallest fix: `xhr.timeout = 120000;` (per 8 MiB chunk). The existing `ontimeout` then counts as a network error and is retried.

### V6 (BACKLOG): on dispose, `_selfRef` is disposed before the asynchronous JS abort has run

- File: `Mentora.Admin/Pages/Exercices/Form.razor:1106` and `:1120`
- What happens: `ResetVideoState` calls `AbortUploadNow()`, which is fire-and-forget (`_ = SafeAbortAsync()`), then disposes `_selfRef` synchronously. A progress event in between calls `invokeMethodAsync` on a disposed reference. The `try` in `report` does not catch it, because the promise is not awaited, so the console shows an unhandled promise rejection. Nothing breaks, but the console is no longer clean.
- Smallest fix: in `report`, use `dotNetRef.invokeMethodAsync(...).catch(function () { })`.

### V7 (BACKLOG): reopening the form right after a successful upload can show "L'envoi de la vidéo n'a pas été terminé"

- File: `Mentora.Admin/Pages/Exercices/Form.razor:1269-1290` and `:1047-1051`
- What happens: `ResumeVideoAsync` runs one sync for an UPLOADING row and starts polling only when the answer is WAITING. If MUX has not yet switched the upload to `asset_created` (a few seconds after the last chunk), the sync answers UPLOADING, `IsInterrupted` is true, and the admin is told to start again even though the file arrived.
- Smallest fix: when the sync still answers UPLOADING and `UploadExpiresAt` is in the future, call `BeginProcessing(id)` (polling, 10 minutes at most) instead of showing the interrupted state.

### V8 (BACKLOG): the `[MUX]` startup line prints the CORS origin value

- File: `Mentora.API/Program.cs:504-507`
- What happens: the brief says no value may appear in the `[MUX]` line, and the validator says its messages never contain "an origin", but the line logs `corsOrigin=<value>`. An origin is public, not a secret, and `[CORS] origin {Origin}` already logs origins, so the risk is nil. It is a consistency point only.
- Smallest fix: log `corsOriginSet=True` instead, or record in CLAUDE.md that public origins are allowed in startup lines.

### V9 (BACKLOG): every bad webhook signature writes a Warning, and the route has no rate limit

- File: `Mentora.Infrastructure/Services/Mux/MuxWebhookService.cs:41`, `Mentora.API/Controllers/Internal/MuxWebhookController.cs:27-28`
- What happens: the route is anonymous. Each unsigned request costs at most an HMAC over 1 MiB and one Warning line, and nothing touches the database. Flooding it fills the logs. The inventory left the rate-limit question open (`inventory.md:475`).
- Smallest fix: a per-IP fixed-window policy on this route, generous enough for MUX retries, or log rejections at Debug after the first N per minute.

### V10 (BACKLOG): dead code

- `Mentora.Infrastructure/Services/Mux/MuxModels.cs:121`: `MuxJson.Invariant` is never called.
- `Mentora.Admin/wwwroot/js/admin.js:101,108`: `lastSent` is written and never read.
- Smallest fix: delete both.

### V11 (BACKLOG): em dashes in the new backend comments and in a Swagger tag

- Files: every new 12.3a backend file starts with `/// Lot 12.3a — ...` (for example `MuxWebhookService.cs:17`, `MuxVideoRules.cs:12`, `AdminExerciseVideosController.cs:10`, `GlobalExceptionMiddleware.cs` added catch, `Program.cs:267`), and `MuxWebhookController.cs:16` has `[Tags("System — MUX")]`.
- What happens: this follows the existing backend habit (`Lot 12.2b — ...`, `[Tags("System — Stripe")]`). The 12.3b front code has none (proven in the 12.3b evidence). It breaks the "no em or en dash in new code" rule only for the 12.3a files.
- Smallest fix: replace `—` with `:` or `,` in the 12.3a added lines; keep `System — MUX` only if the Swagger tag must match the Stripe one.

### V12 (BACKLOG): the sync route does not check that the admin account is enabled

- File: `Mentora.Infrastructure/Services/Mux/AdminExerciseVideoService.cs:127-131`
- What happens: `CreateUploadAsync` and `RemoveAsync` call `EnsureAdminAsync`, but `SyncAsync`, which also writes the row, does not. A disabled admin whose token has not expired can still trigger syncs. The impact is nil: a sync only copies MUX state through the same guards.
- Smallest fix: pass the admin id and call `EnsureAdminAsync` for consistency, or document the exception.

---

## Verified and fine

Section 3.1, webhook security
- Signature computed on the exact raw bytes before any parsing: the body is copied to a `byte[]` (`MuxWebhookController.cs:39-49`), verified (`MuxWebhookService.cs:37-43`), and only then parsed (`:48`). The prefix uses the timestamp string exactly as received (`MuxWebhookSignature.cs:51`).
- Constant-time comparison: `CryptographicOperations.FixedTimeEquals` on decoded bytes with a length check, and every candidate is compared without early exit (`MuxWebhookSignature.cs:57-72`).
- Several `v1` entries are accepted, and invalid hex candidates are skipped.
- Timestamp tolerance applies both ways with `Math.Abs` (`:48`); `NumberStyles.None` refuses signs and spaces; tolerance is bounded to 60 to 900 s.
- Body size: `[RequestSizeLimit(1_048_576)]`, with the 413 caught around the read. No model binding or other middleware reads the body first.
- Logs: only `reason=`, event type and result. No header, signature, secret or payload is logged.
- MUX not configured: 503 before the body is read.
- Idempotence under concurrency: the pre-check `AnyAsync` plus the unique index `UX_MUX_WEBHOOK_EVENTS_MUX_ID`. A concurrent twin waits on the exercise row lock, re-reads, inserts, hits the unique violation filtered on `SqlState` and `ConstraintName`, clears the tracker and answers 200 duplicate (`MuxWebhookService.cs:100-109`). The transaction is disposed (rolled back) before the catch, because it is scoped to the `try`. The same path works for events that match no row.
- No MUX call from the webhook: `MuxWebhookService` has no `IMuxClient` dependency.

Section 3.2, state rules
- No backwards move from READY to WAITING: `OnUploadAssetCreated` acts only on UPLOADING (`MuxVideoRules.cs:51-57`). READY to ERRORED is covered in V3.
- Events for a replaced upload or asset are ignored (`:49-50`, `:108-111`, `:143-155`). The passthrough only locates the row, and ownership is decided by asset id or upload id.
- Out-of-order `asset.ready` before `asset_created` is applied through the upload id, and the late `asset_created` then answers "unchanged".
- `video.asset.deleted` removes the row only when the row's current asset id matches (`MuxWebhookService.cs:155-163`).
- ERRORED codes (`UPLOAD_ERRORED/CANCELLED/TIMED_OUT/NOT_FOUND`, `ASSET_ERRORED/NOT_FOUND`) are truncated to 500 characters, and identical errors are not rewritten.
- Webhook against sync: both take `SELECT ... FOR UPDATE` on the exercise and re-read the row under the lock. The sync reads MUX outside the lock and applies the facts under the lock with the same guards, so there is no lost update. No concurrency token is needed.
- Replace against a late `video.asset.ready` of the old asset: replace sets the asset id to null and switches the upload id. The late event is found by passthrough, then ignored as "asset of another upload".

Section 3.3, admin routes
- `[Authorize(Policy = AdminAuthDefaults.Policy)]` on the controller covers the three routes (P3: coach and member tokens get 401).
- Coach-owned and unknown ids return 404 through `LockExerciseAsync` (`EXERCISE_COACH_ID IS NULL`) and `EnsureMentoraExerciseAsync`.
- The row lock is taken before reading the video in create, sync and remove.
- MUX upload created and then a DB failure: the inner catch cancels the new upload with `CancellationToken.None` and rethrows. A MUX failure before the `try` leaves nothing behind.
- Best-effort cleanup runs after commit. Exceptions are swallowed and logged with type name and ids only, with no URL, no message and no secret.
- Audit rows `EXERCISE_VIDEO_UPLOAD_CREATED` and `EXERCISE_VIDEO_REMOVED` are written in the same `SaveChanges` as the change.
- `MuxUnavailableException` maps to 502 `MUX_UNAVAILABLE` and `MuxNotConfiguredException` to 503 `MUX_NOT_CONFIGURED`, both caught before `InvalidOperationException` (`GlobalExceptionMiddleware.cs`).

Section 3.4, MUX client
- Basic auth is built per request from the options (`MuxClient.cs:111-118`). The 15 s timeout is set at registration, and timeouts are told apart from caller cancellation.
- No token in logs or exceptions: the exception text is `"MUX <operation> failed."`, and logs carry the operation, status and `x-request-id`.
- 404 maps to null or false on get, cancel and delete.
- `meta.title` is truncated to 512 and `external_id` to 128 code points without splitting surrogates.
- `cors_origin` comes from `Options.NormalizedCorsOrigin` only.

Section 3.5, options and startup
- Outside Development, any missing value among the four refuses startup with a message that holds no value. Startup also refuses a webhook secret under 32 characters, an origin that is not https or holds a wildcard or a path, and an API base that is not https.
- `.Value` is read at `Program.cs:503` before `MigrateAsync` at `:528` (P2 proves it).
- In Development with none of the four values, MUX is disabled and all routes answer 503 (P2). Partial values refuse startup.
- No secret appears in messages. The origin printed in the `[MUX]` line is covered in V8.

Section 3.6, migration
- Up and Down are symmetric. The CHECKs are named (`CK_EXERCISE_VIDEOS_STATUS`, `CK_EXERCISE_VIDEOS_MUX_REF`, `CK_MUX_WEBHOOK_EVENTS_STATUS`, `CK_ADMIN_AUDIT_LOG_ACTION`).
- The Down guard raises before any change, and Down has no `''` back-fill.
- On PROD and api-demo, `EXERCISE_VIDEOS` is empty: `ALTER ... DROP NOT NULL` loses nothing, and the audit CHECK keeps the four existing values.
- `HasSentinel(-1)` only changes when EF omits the column on insert. Every real value differs from -1, so the status is always sent, and queries are unaffected. The entity initializer (`Waiting`) and the DB default `'WAITING'` are now unused on insert but harmless.

Section 3.7, mobile contract
- `ExerciseVideoMapping.ForApps` returns a video only for READY with a playback id. It is used by `ExerciseService.ToResponse`, which covers the coach list, coach detail and member detail through `WithDetail` with `Include(e => e.Video)`, and by `ProgramService` for both trees through `Include(e => e.Video)` in the one exercise query, so there is no N+1.
- `ExerciseVideoResponse` is unchanged. `ProgramExerciseResponse.Video` is the last key (1A).
- P5 shows no removed key and no order change.

Section 3.8, admin front
- The upload URL goes only from the API response to `uploadVideo`. It is never printed, never sent back to .NET, and never sent to the API. The admin has no logger or console writes on that path.
- `withCredentials = false` and no `Authorization` header on the PUT.
- Chunk size is 8 MiB, which is 32 × 256 KiB.
- 3 retries per chunk with 1, 2 and 3 s backoff on network errors, 5xx, 408 and 429.
- Abort is honoured during the XHR and during the backoff.
- `beforeunload` is added before the loop and removed in `finally`.
- Polling stops on dispose, READY, ERRORED, 404, 401, 503 and after 10 minutes. `DotNetObjectReference` is disposed in `ResetVideoState`.
- "Annuler l'envoi" makes no DELETE call (accepted).
- Replace goes through a confirmation and only then opens the picker. Remove asks for confirmation.
- The video stays outside `Snapshot()`, so the form never becomes dirty (12.3b Chrome test).
- Leaving during an upload asks first, then the dirty-form dialog follows.
- The file picker is unreachable while busy, so the module-level `file` cannot change during an upload.
- The French texts are simple and the evidence counts 0 dashes.

Section 3.9, CSP
- `connect-src` adds only `https://*.mux.com` and `img-src` adds only `https://image.mux.com`.
- The script hash is unchanged, and `index.html` is not in the diff.
- The player opens in a new tab with `rel="noopener noreferrer"`, so no `media-src` or `frame-src` is needed, and `frame-ancestors 'none'` stays.

Section 3.10, conventions
- If-chains are used in every enum helper (`EnumMappings`, configuration converters, `AdminAuditMappings`). The `EnumMappings` mirror has `MuxWebhookEventStatusMapping`.
- `CK_` names are used, and the column is `MUX_WEBHOOK_EVENT_CREATED_DATE`.
- No empty `if` blocks. The `@switch` in `List.razor` and `VideoFailure` switch over a string and an int, not over an enum helper.
- Dead code and dashes are covered in V10 and V11.

---

## Not proven at runtime

Neither `12.3a/runtime-evidence.md` nor `12.3b/runtime-evidence.md` shows any of the following:
- A real MUX webhook delivery. Every webhook proof (P6) was signed by the test command itself, so the real `Mux-Signature` format, real payload shapes (`video.upload.asset_created`, `video.upload.errored` and `cancelled`, `video.asset.errored`, `video.asset.deleted`) and MUX retries were never received. Locally, progress came only from sync.
- Webhook state transitions other than READY "unchanged": `asset_created` to WAITING, ERRORED codes, `asset.deleted` removing a row, and events ignored for a replaced asset or upload.
- Two identical events at the same moment (the unique-violation path), a future timestamp, and the 413 above 1 MiB.
- Webhook and sync racing on the same row, and replace racing a late `asset.ready` of the old asset.
- The orphan-asset window of V2, and the asset-delete branch of DELETE (P9 removed only UPLOADING rows; the helper deleted an asset only in P7).
- MUX 5xx or timeout leading to 502 `MUX_UNAVAILABLE`, and a DB failure after the MUX upload was created (the rollback cancel).
- Startup outside Development with the real MUX webhook secret: the 32-character minimum was checked only with generated values (P2), never with the real `Mux__WebhookSecret`, so api-demo or PROD could refuse to start if MUX's secret is shorter.
- Front (listed as not exercised in 12.3b): interrupted or expired upload ("Recommencer"), ERRORED display, 502 and 503 messages, network loss and retries, a 308 with a partial `Range`, "Annuler l'envoi", "Quitter pendant l'envoi ?", the 10-minute polling stop, and `beforeunload`.
- The nginx CSP in a browser: the dev server sends no CSP, so `connect-src https://*.mux.com` and `img-src https://image.mux.com` are still to be checked in the PROD Chrome test.
- MUX exposing the `Range` header to browser JavaScript (needed for the V1 fix).

---

## Count

| Severity | Count |
|---|---|
| BLOCKER | 0 |
| SHOULD | 2 |
| BACKLOG | 10 |
