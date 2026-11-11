# Lot 12, sub-lot 12.3b: exercise video block and CSP for MUX

Branch `feature/lot_12`, base `f35dd41` (12.3a code `6e04b63`). Local run only: Docker PostgreSQL 16 on 5433, API on 5243 (`Development`, `Email__Provider=Logging`), admin dev server on 4200 (the CORS origin registered at MUX). No token, one-time code, signed upload URL or secret appears below. API and admin logs (`*.log`) stayed local, gitignored.

Test video: `mentora-mux-test.mp4`, 33 MB, picked by François in the native file dialog.

---

## 1. Files

Modified, `Mentora.Admin` and nginx only:
- `Api/ExerciseModels.cs`: `AdminExerciseVideo` (replaces `ExerciseVideo`), `VideoUploadResponse`, `VideoSyncResponse`, `VideoStatus` on the list item.
- `Api/ExercisesApi.cs`: `CreateVideoUploadAsync`, `SyncVideoAsync`, `RemoveVideoAsync`.
- `wwwroot/js/admin.js`: `uploadVideo` (8 MiB chunks, `Content-Range`, 308 = continue, 200 or 201 = done, 3 retries per chunk, progress to .NET every 500 ms at most, no cookie, no `Authorization`), `abortVideoUpload`, `getSelectedVideo`, `clearSelectedVideo`, `openFilePicker`, `beforeunload` during the upload.
- `Pages/Exercices/Form.razor`: block "Vidéo de démonstration" between "L'essentiel" and "Muscles et objectifs", independent of the form dirty state; confirmations "Remplacer la vidéo ?", "Retirer la vidéo ?" and "Quitter pendant l'envoi ?".
- `Pages/Exercices/List.razor`: badges Avec vidéo, En traitement, Vidéo en erreur, Sans vidéo.
- `wwwroot/css/app.css`: video block, `badge-error`.
- `nginx/conf.d/mentora-admin.conf.example`: `connect-src` + `https://*.mux.com`, `img-src` + `https://image.mux.com`, comment line 23 rewritten.

Decisions: the upload uses `XMLHttpRequest` with `withCredentials = false` (same as `credentials: "omit"`, and it gives progress inside a chunk); the progress bar is a native `<progress>` because the CSP has no `style-src 'unsafe-inline'`; "Annuler l'envoi" does not call DELETE (accepted); "Remplacer la vidéo" goes through a ConfirmDialog first, "Choisir une vidéo" and "Recommencer" open the picker directly.

## 2. Checks before the run

```
dotnet build Mentora.Admin: 0 Avertissement(s), 0 Erreur(s)
dotnet build (solution): 0 Erreur(s)
git status Mentora.Admin/wwwroot/index.html: empty (unchanged, CSP inline script hash unchanged)
em or en dash in added lines of the diff: 0
```

Environment, without values: `TestRecipients` set, `Mux__TokenId`, `Mux__TokenSecret`, `Mux__WebhookSecret` set, `Mux__CorsOrigin=http://localhost:4200`, admin emails set. Ports 5243 and 4200 free before the start.

```
[EMAIL] routing mode=TEST provider=Logging testRecipients=4 productionBcc=0
[CORS] origins=0 localhost=True
[MUX] configured=True corsOrigin=http://localhost:4200 apiBase=api.mux.com
[ADMIN] auth emails=4 accessMinutes=15 refreshHours=12
[ADMIN] accounts listed=4 created=0 reenabled=0 disabled=0
Application started. Press Ctrl+C to shut down.
```

```
GET http://localhost:4200/                  -> 200
GET http://localhost:4200/js/admin.js       -> 200, contains uploadVideo
GET http://localhost:5243/api/v1/health     -> 200
```

## 3. Chrome test (done by Claude in the chat, admin #1, exercise EX001)

- Video block between "L'essentiel" and "Muscles et objectifs".
- No video: "Aucune vidéo pour cet exercice" and "Choisir une vidéo".
- File picked in the native dialog: progress bar with file name and size, then "Vidéo prête" within 10 s.
- Thumbnail loaded from `image.mux.com` (480x480), duration 54 s.
- Link "Voir la vidéo (nouvel onglet)" to `player.mux.com` with the same playback id, `target="_blank"`, `rel="noopener noreferrer"`.
- List badge: "Sans vidéo" before, "Avec vidéo" after.
- "Remplacer la vidéo" opens "Remplacer la vidéo ?"; "Annuler" keeps the video READY.
- Leaving the form after the upload asked no "Quitter sans enregistrer ?": the video does not make the form dirty.
- "Retirer la vidéo" opens its confirmation; "Retirer" shows "Vidéo retirée." and the empty state.
- Browser console: no error, no CSP violation. The dev server serves no CSP header: the nginx CSP is checked in the PROD Chrome test.
- Tab closed.

Not exercised in this run: interrupted or expired upload ("Recommencer"), ERRORED, 502 and 503 messages, network loss and retries, "Annuler l'envoi", "Quitter pendant l'envoi ?".

## 4. Cleanup

Both servers stopped (pids of the API and of the admin dev server), ports 5243 and 4200 no longer listening.

```
EXERCISE_VIDEOS=0
MUX_WEBHOOK_EVENTS=0
GET https://api.mux.com/video/v1/assets?limit=100 -> 200, items=0
GET https://api.mux.com/video/v1/uploads -> 404 (MUX has no list route for direct uploads; the one upload of the test was used, none is pending)
```

## 5. Leak checks

Run on the 7 modified files and on this file (8 files): none of the values of `Mux__TokenId`, `Mux__TokenSecret`, `Mux__WebhookSecret`, the admin emails or the test recipients; no JWT (`eyJ...`), no `Bearer` value, no signed upload URL host, no `[OTP]` or `[EMAIL-LOG]` line, no `Password=`, no em or en dash. Result: 0 hit.

`git status`: 7 modified files plus `docs/review/lot12/12.3b/` (this file; `api.log`, `api.err.log`, `admin.log` and `admin.err.log` are gitignored by `*.log`). Nothing committed yet.
