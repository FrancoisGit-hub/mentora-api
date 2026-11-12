# Lot 12, wave 2 review fixes: runtime evidence

Branch `feature/lot_12`, base HEAD `3228082`. Findings fixed: V1 (upload Range), V2 (orphan asset), webhook secret length (from the "Not proven" section of `docs/review/lot12/review-wave2.md`). Nothing else touched: `index.html`, the Flutter apps and the other findings are unchanged.

## 1. Preflight

Container `mentora-postgres-local` up, ports 5243 and 4200 free. Environment (values never printed): TestRecipients True, AdminEmails True, AdminSecretLength 64, MuxTokenId True, MuxTokenSecret True, MuxWebhookSecret True, MuxCors `http://localhost:4200`. As expected.

## 2. Files changed (commit 1)

- `Mentora.Admin/wwwroot/js/admin.js`: V1.
- `Mentora.Infrastructure/Services/Mux/AdminExerciseVideoService.cs`: V2, plus a bounded retry (section 5).
- `Mentora.Core/Options/MuxOptions.cs`, `Mentora.Infrastructure/Services/Mux/MuxOptionsValidator.cs`: webhook secret rule and comments.

The three fixes were already in the working tree (uncommitted, written by a session that ended with the PC restart). They were read against the brief before any test, and the retry in `AdminExerciseVideoService.cs` was added after proof 3 showed it was needed.

## 3. P1 Build

```
Mentora.API   : 19 warnings (all CS1573, baseline), 0 errors   (dotnet build --no-incremental)
Mentora.Admin : 0 warnings, 0 errors                            (dotnet build --no-incremental)
git diff --stat on Mentora.Admin/wwwroot/index.html: empty (unchanged)
```

## 4. P2 Startup outside Development

Production, `--no-launch-profile`, Email provider Logging, a CORS origin set, connection string to a database that does not exist (`mentora_p2_does_not_exist`, fake user), secrets generated in the command:

```
webhook secret of 20 characters : [MUX] configured=True corsOrigin=https://admin.p2.invalid apiBase=api.mux.com
                                  then Npgsql.PostgresException 28P01 password authentication failed (refusal from the database, after MUX validation)
webhook secret empty            : OptionsValidationException: Mux:WebhookSecret must be set as an environment variable outside Development.  (no value in the message)
port 5243 after both runs: free
```

The first attempts stopped earlier on other startup guards (email provider, CORS origins) and were repeated with those variables set; they say nothing about MUX.

## 5. P3 V2 at runtime (MUX DEMO environment)

API in Development with the real `Mux__` variables, Email provider Logging, log in `api.log` (gitignored). Admin login with the code read from the log into a variable (never printed). Test file `mentora-mux-test.mp4`, exercise EX001 (`1194fd80-be00-4630-907e-80609ef0f331`), no video row before.

### 5.1 First run: the fix as written did NOT work

Both scenarios (PUT then DELETE; PUT then replace) reached the new code path: GET upload found an `asset_id`, then the asset delete got `status=400` from MUX, the helper logged "not cleaned up (MuxUnavailableException)" and gave up. Two assets were left at MUX (GET /video/v1/assets listed 2). They were deleted by hand (DELETE answered 204 and 404, then the list showed 0). The log of that run is kept locally as `api-run1-before-retry.log`.

Cause, reproduced with direct calls to MUX (upload created at MUX, file PUT, asset deleted the moment `asset_id` appears):

```
run 1: DELETE 400 "Can't delete a preparing asset"   then 2 s later DELETE 204
run 2: DELETE 204 at once
```

MUX refuses to delete an asset during its first moments, and the window is short and not always present. A single attempt is therefore not enough.

### 5.2 Change made (outside the written spec, flagged)

`DeleteAssetWithRetryAsync` in `AdminExerciseVideoService.cs`: up to 4 attempts, 2 s apart, on any `MuxUnavailableException` (the client does not expose the status code, and `MuxClient` is not in the list of files to touch). The last attempt rethrows to the existing catch, which logs the exception type only. It is used by both branches (row with an asset id, and pending upload that has an asset). Cost: a request can take up to 6 s longer in the worst case; in the observed case it added 2 s. Still best effort, after the commit, no secret in the logs.

### 5.3 Final run

```
login: otp request 200, otp verify 200

A: PUT then DELETE (no sync)
  POST video-uploads 201 | curl PUT 200 | DELETE video 200
  [MUX] delete asset failed status=400 requestId=...
  [MUX] asset 7ke00IUmy9idwxKWYV5NedwYqHGi7oiAQyPUpt1CEfig not deletable yet, attempt 1 of 4
  [MUX] remove video=c2c2fd25-2662-4baf-9377-3ec016440f78 upload N3FbNWzjMU9oBvQS1K1cPFv00ZEgNJaihnz14uqEeIPM found asset 7ke00IUmy9idwxKWYV5NedwYqHGi7oiAQyPUpt1CEfig, asset deleted
  direct GET asset at MUX: 404
  direct GET upload at MUX: 200

B: PUT, then replace, then DELETE
  upload 1 MgbLrx5jtkzStSMp8jwe9lOLLzpxOVWixnj201T009irU: curl PUT 200
  replace: POST video-uploads again, upload 2 Zgj2q02A9syYZNH9an02JQ3IMFIwqJZomnq02wCQFHA1OA
  [MUX] delete asset failed status=400 requestId=...
  [MUX] asset A93QDGtGP4zzmOBDO400wj2lhGDZcOcH6ogdYV2uJItU not deletable yet, attempt 1 of 4
  [MUX] replace video=28e7a2f6-20eb-488e-86c6-1bab5106291c upload MgbLrx5jtkzStSMp8jwe9lOLLzpxOVWixnj201T009irU found asset A93QDGtGP4zzmOBDO400wj2lhGDZcOcH6ogdYV2uJItU, asset deleted
  direct GET asset A93QDGtGP4zzmOBDO400wj2lhGDZcOcH6ogdYV2uJItU at MUX: 404
  DELETE video 200
  [MUX] remove video=28e7a2f6-20eb-488e-86c6-1bab5106291c upload Zgj2q02A9syYZNH9an02JQ3IMFIwqJZomnq02wCQFHA1OA without asset, cancelled
  direct GET upload 1 and upload 2 at MUX: 200, 200 (the upload objects remain readable, cancelled or asset_created)

ids created in the final run
  uploads: N3FbNWzjMU9oBvQS1K1cPFv00ZEgNJaihnz14uqEeIPM, MgbLrx5jtkzStSMp8jwe9lOLLzpxOVWixnj201T009irU, Zgj2q02A9syYZNH9an02JQ3IMFIwqJZomnq02wCQFHA1OA
  assets : 7ke00IUmy9idwxKWYV5NedwYqHGi7oiAQyPUpt1CEfig, A93QDGtGP4zzmOBDO400wj2lhGDZcOcH6ogdYV2uJItU (both deleted by the API, GET 404)
GET /video/v1/assets: 0 assets
EXERCISE_VIDEOS: 0 | MUX_WEBHOOK_EVENTS: 0
API stopped, port 5243 free (port 4200 free)
```

Also created and removed in this session while diagnosing: the two assets of the first run (deleted by hand) and three probe uploads created directly at MUX with their assets (each deleted at once, 204). No asset was left at the end.

## 6. P4 V1 (no runtime test possible without a partial 308)

The final code of the 308 branch is in the report of this step (`admin.js`, function `upload`, and the helper `nextFromRange`). The three cases:

- Range readable and lower than the chunk end (`bytes=0-N`, N+1 below `end`): the next offset is N+1, so the rest of the chunk is sent again from N+1.
- Range readable and equal to or higher than the chunk end: the offset is capped at `end`, the upload continues from the chunk end. If that was the last chunk, a 308 is a refusal (it must finish with 200 or 201), as before.
- Range absent or unreadable (CORS may hide it): the whole chunk is taken as received (`end`), the previous behaviour.

Guard: when the new offset does not move forward three times in a row, the upload fails with status 308 and code `HTTP_308_STALLED`. Chunk size (8 MiB), the 3 retries with backoff and the progress reports are unchanged.

## 7. P5 Leak checks

Patterns proven first on known files: JWT pattern on `docs/review/lot7/claims-after-4a.json` = 2; console OTP marker (literal) on `Mentora.Infrastructure/Services/AuthService.cs` = 1; mail log marker (literal) on `Mentora.Infrastructure/Services/Email/LoggingEmailSender.cs` = 1; address pattern on `docs/review/lot11/11.4/runtime-evidence.md` = 14; URL with query string on `Mentora.Admin/wwwroot/index.html` = 1.

On the explicit list of files to commit (the 4 code files, `review-wave2.md` and this file): 0 match for JWT, console OTP marker, mail log marker, address and URL with query, in every file. Dashes: 0 in `admin.js`, `MuxOptions.cs`, `MuxOptionsValidator.cs` and in the added lines of `AdminExerciseVideoService.cs`. Two pre-existing exceptions, not introduced here: the class summary of `AdminExerciseVideoService.cs` (line 16, 12.3a, finding V11) holds 1 em dash, and `review-wave2.md` holds 6 because V11 quotes the character.

Local files in this folder, all gitignored and not committed: the API logs (`*.log`, including the first run `api-run1-before-retry.log`).
