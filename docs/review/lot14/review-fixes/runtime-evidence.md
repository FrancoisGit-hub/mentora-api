# Lot 14 review wave 1: fixes and runtime evidence

- Date: 2026-11-21 (local run)
- Branch: feature/lot_14, base 3c3e7ff. Review report: `docs/review/lot14/review-wave1.md`.
- Environment: local API (Development, `Email__Provider=Logging`, port 5243 free before each start, single API process, `dotnet run --no-build --no-launch-profile`, `ASPNETCORE_URLS=http://localhost:5243`), local PostgreSQL (port 5433).
- No token, login code, password or connection string appears in this file. Login codes were read from the API log into variables by the scripts and never printed. No `[EMAIL-LOG]` line is quoted.
- Run start date, read in the database BEFORE the first write: **2026-11-21 09:36:53 UTC** (used by the cleanup).

## 1. What was fixed

### F1. Welcome mail of the added member (request from PJ, 21/11)

`Mentora.Infrastructure/Services/Email/MemberAddedEmailBuilder.cs` only. The app asks no role any more, so the step list of both the HTML and the text body is now:

With `MemberInvite:InstallUrl` set:
1. Installez l'application Mentora avec le bouton ci-dessous.
2. Ouvrez-la, saisissez votre adresse email ({normalized member email}) et appuyez sur « Connexion ».
3. Entrez le code à 6 chiffres que vous recevez par email.

Then the install button (HTML: same bordered button on a white background; text: the `Installer l'application : {url}` line), then « Ne passez pas par « Créez un compte » : votre compte est déjà prêt. »

Without it: step 1 is « Votre coach vous enverra le lien pour installer l'application Mentora. », no button, same steps 2 and 3, same last line.

- Every mention of a role choice is gone (« élève », « choisissez »). Subject and `[INVITE]` startup line unchanged.
- The email is normalized (`EmailNormalizer.Normalize`, idempotent for new accounts; it also lowercases the stored value of an old row, see W8) and stays HTML encoded in the HTML body. Fixed sentences go through `WebUtility.HtmlEncode` as the former `NoLinkSentence` did; the « Connexion » literal is written in the template like the former « élève ».
- `git grep` of the old wording on the whole branch outside `docs/review/lot14/14.*`: the only hits of the role step were `MemberAddedEmailBuilder.cs:47` and `:73`. Other « élève » hits are unrelated (admin exercise form placeholder, older review docs). The runbook does not quote the mail steps.

### W2. The attempt is claimed before the code is checked

`Mentora.Infrastructure/Services/AuthService.cs`, `VerifyOtpAsync`.

- Before: candidates loaded (`count < 5`), BCrypt checked in memory, then one increment on every loaded candidate for a wrong code. Parallel requests could each evaluate a guess before the first increment.
- After: candidates are selected exactly as before. For each candidate, one conditional `ExecuteUpdate` sets `count = count + 1 WHERE id = candidate AND NOT used AND count < 5 AND not expired`. It runs before the consumption transaction opens, so it commits on its own and is kept even when the guess is wrong. 0 row updated: the candidate is not evaluated, and with no candidate left the answer is the usual wrong code answer (400, same body). Only after a successful claim is BCrypt checked. Wrong: same 400. Right: the existing atomic consumption.
- One deliberate change in the consumption: its bound moved from `count < 5` to `count <= 5`, because the attempt being consumed is already counted. Without it the 5th attempt could never succeed, which the spec keeps ("the 5th may still succeed").
- Meaning: at most 5 verifications are evaluated per code, whatever their result. In practice there is a single candidate (a new request invalidates earlier unused codes); with several, each candidate tried uses one attempt.
- No migration. `CK_AUTH_OTPS_ATTEMPT_COUNT` is `>= 0` with no upper bound; the claim only adds 1 under `count < 5`, so the value stays in 0..5 and can never break it.

### W1, W5 and the runbook corrections

`docs/review/lot12/12.5a/prod-runbook.md`, section « Lot 14 vague 1 (21/11) »:
1. Step d: `docker compose logs --since 5m api-prod | grep -c "\[EMAIL-LOG\]"` added next to the `[OTP]` count, expected `0` (never print the lines). Step c: the `[EMAIL] routing` line of api-prod must show `provider=Smtp`. Closes W1.
2. Step a: query 0 (last applied migration, expected `20261117234001_Lot13_2_VoiceMessages`), 14.1 query 1 is now a count that must be 0, query 1b only if not 0; no query prints an email.
3. Step c: second startup shows the same `[INVITE]`, `[AUTH]`, `[EMAIL]` lines and no `Applying migration` for the two lot 14 migrations.
4. Step c: port check (W7) with `docker compose ps api api-prod --format "{{.Service}} {{.Ports}}"`, expected no `0.0.0.0:` nor `[::]:`.

Prechecks (W5):
- `docs/review/lot14/14.1/precheck.sql`: wrapped in `BEGIN READ ONLY; ... ROLLBACK;`, query 0 (last migration), query 1 returns `case_collisions` (must be 0), query 1b (commented) lists only `USER_ID` values of colliding rows, query 2 returns the count `emails_with_spaces`. No stored email is printed any more.
- `docs/review/lot14/14.2/precheck.sql`: query 0 added, header lists what each query returns (it already had the read-only wrapper).

## 2. Prechecks on the local base (section 5.3)

Read only, `docker exec -i mentora-postgres-local sh -c 'psql ... -v ON_ERROR_STOP=1'` with the SQL on standard input. Locally the last migration is the 14.2 one, as expected.

```
=== docs/review/lot14/14.1/precheck.sql
BEGIN
 MigrationId: 20261120193442_Lot14_2_OtpAttempts
 case_collisions: 0
 emails_with_spaces: 0
ROLLBACK
=== docs/review/lot14/14.2/precheck.sql
BEGIN
 MigrationId: 20261120193442_Lot14_2_OtpAttempts
 auth_otps_rows: 125
 active_codes: 0
 attempt_column_exists: 1
ROLLBACK
```

## 3. Build (section 6)

`dotnet build --no-incremental` (output to a build.log file, then read):

```
    19 Avertissement(s)
    0 Erreur(s)
```

0 warning in `AuthService.cs` or `MemberAddedEmailBuilder.cs` (all 19 are the inherited XML comment warnings, such as CS1573 in controllers).

## 4. Runtime proofs (section 7)

### Startup 1 (`AuthRateLimit__PermitLimit=1000` for this process only)

Environment check before the start: `EmailProvider=Logging TestRecipients=True AdminEmails=True JwtSecretLength=64`, port 5243 free. No migration applied.

```
[EMAIL] routing mode=TEST provider=Logging testRecipients=4 productionBcc=0
[INVITE] installUrl=False permitLimit=30 windowMinutes=60
[AUTH] otpRateLimit permitLimit=1000 windowMinutes=15
Now listening on: http://localhost:5243
```

### 7.1 F1

- Local seed coach account login (`isCoach true`): request 200, verify 200.
- `POST /api/v1/coach/members` for `l14f.member@example.com` (first name L14f, last name Member): **201**, `isNewAccount true`, `isPrimary true`, optional fields null.
- Mail line, mode and subject only (recipients hidden): `[EMAIL] mode=TEST to=(hidden) bcc=(hidden) subject=[TEST pour l14f.member@example.com] Votre coach Federico vous a ajouté sur Mentora`.
- The final HTML and text templates are in the code diff of `MemberAddedEmailBuilder.cs` (section 1).

### 7.2 W2 (on `l14f.member@example.com`, a fresh code per case)

| Case | Scenario | HTTP results | Stored row afterwards |
|---|---|---|---|
| a | correct code at the first try | **200** (keys `success,data,error,statusCode`, data `accessToken,refreshToken,expiresIn,isCoach`, `isCoach false`, `expiresIn 3600`) | attempts 1, used t |
| b | 4 wrong codes then the correct one | 400 x4 (wrong code body), then **200** | attempts 5, used t |
| c | 5 wrong codes then the correct one | 400 x5, then **400** with the wrong code body | attempts 5, used f |
| d | 20 wrong verifications in parallel (curl `--parallel`) | **400 x20**, all 20 bodies equal to the wrong code body | attempts **5** exactly, used f; then the correct code: **400**, still 5, used f |
| e | 2 parallel verifications with the correct code | **one 200, one 400** (wrong code body) | attempts 2, used t |

Wrong code body: `{"success":false,"data":null,"error":"Invalid or expired OTP.","statusCode":400}`.

### 7.2 f Contract

Same script and line format as `docs/review/lot14/14.2/contract-after.txt`, on the local seed member account:

```
request existing member (exact case) -> 200 body={"success":true,"data":null,"error":null,"statusCode":200}
request member isCoach=true (no coach profile) -> 400 body={"success":false,"data":null,"error":"No coach account for this email.","statusCode":400}
request unknown email -> 400 body={"success":false,"data":null,"error":"User not found or account is disabled.","statusCode":400}
request CAPITALS (variant) -> 200 body={"success":true,"data":null,"error":null,"statusCode":200}
verify wrong code -> 400 body={"success":false,"data":null,"error":"Invalid or expired OTP.","statusCode":400}
verify unknown email -> 400 body={"success":false,"data":null,"error":"User not found or account is disabled.","statusCode":400}
verify good code -> 200 keys=success,data,error,statusCode / data:accessToken,refreshToken,expiresIn,isCoach isCoach=False expiresIn=3600
```

`Compare-Object` with `contract-after.txt` (case sensitive): **identical (7 lines)**.

### Incident during the run

On the first attempt of case d, the curl parallel config was malformed (a trailing `next`), so no parallel call was sent; the code stayed valid and the final "correct code" check answered 200. The script printed that unexpected body to the console, which showed one access token and one refresh token of the test account `l14f.member@example.com`. Local API only, local signing secret, test account; that refresh token and the account itself were deleted by the cleanup below (counted in `l14f_refresh_tokens 5`). The scripts were fixed (an unexpected answer never prints its body; one write-out per parallel transfer) and cases d and e were run again; the table above shows the clean runs. Nothing was written to a repository file.

### 7.3 Restart without the override

```
[EMAIL] routing mode=TEST provider=Logging testRecipients=4 productionBcc=0
[INVITE] installUrl=False permitLimit=30 windowMinutes=60
[AUTH] otpRateLimit permitLimit=30 windowMinutes=15
```

### 7.4 Cleanup

- `cleanup-dryrun.sql` (ROLLBACK), bounded by the `l14f` prefix AND the run start date, plus the OTPs and refresh tokens created after the run start by the two pre-existing local seed accounts. `cleanup-commit.sql` written separately; `diff` with the dry run changes only the last line (`ROLLBACK;` becomes `COMMIT;`).
- Dry run and COMMIT gave the same output: `l14f_users 1`, `l14f_members 1`, `l14f_member_coaches 1`, `l14f_otps 9`, `l14f_refresh_tokens 5`, `preexisting_users_kept 2`, `otps_preexisting_users 4`, `refresh_tokens_preexisting_users 2`, `DELETE 4`, `DELETE 2`, `DELETE 1`, leftovers `(0 rows)`, `l14f_users_after 0`, `member_coaches_total_after 2`, `users_left_total 2`, then `COMMIT`.
- Final check in a separate read-only session: 42 tables with a `*_CREATED_DATE` column, **0 row** created after 2026-11-21 09:36:53; `l14f` users 0; users total 2.

### 7.5 Stop

API processes stopped; port 5243: 0 listener; 0 Mentora.API process.

## 5. Left to the backlog

- W3: the add route does not check `MEMBER_IS_ACTIVE`; an inactive member can be linked but stays hidden from the coach lists.
- W4: lowercasing differs between C# (`ToLowerInvariant`) and PostgreSQL `lower()` for non ASCII letters, depending on the database locale.
- W6: member names are stored with control characters (cleaned only where they go into a mail).
- W8: for an existing member the 14.1 response returns the stored email, which may contain capitals on old rows (the welcome mail now shows the normalized value).
- W7 answered: the API port 8080 is not published on the VPS, nginx is the only entry point (checked on 10/11); the runbook port check is kept as a guard.
