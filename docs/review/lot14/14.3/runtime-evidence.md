# Lot 14.3 runtime evidence

Run start date, read in the local database before the first write: 2026-11-20 20:19:33 UTC.
Local API in Development, `Email__Provider=Logging`, port 5243 free before each start, one API process at a time, stopped at the end. Build: 0 error, 19 warnings.

## 1. Development, default routing: `[OTP]` console line still written

Startup line: `[EMAIL] routing mode=TEST provider=Logging testRecipients=4 productionBcc=0`.
One OTP request for member@mentora.fr: HTTP 200. Count of `[OTP]` lines in the log: 0 before, 1 after (counts only, the line was not printed).

## 2. Code reading

The guard is `Mentora.Infrastructure/Services/AuthService.cs:72`: `if (environment.IsDevelopment())` around the single `Console.WriteLine($"[OTP] ...")`. `IHostEnvironment` is injected through the primary constructor. Nothing replaces the line in other environments. No other change to `RequestOtpAsync`.
The non-Development proof is done on the VPS (runbook, section "Lot 14 vague 1", step d), because a local non-Development start needs ffmpeg and real secrets.

## 3. Production routing (process-only variables)

`EmailRouting__Mode=Production`, `EmailRouting__ProductionBcc` = two example.com addresses.
Startup line: `[EMAIL] routing mode=PRODUCTION provider=Logging testRecipients=4 productionBcc=2`.
OTP request for member@mentora.fr: HTTP 200. Routing line:

```
[EMAIL] mode=PRODUCTION to=member@mentora.fr bcc=l143.bcc1@example.com,l143.bcc2@example.com subject=Test, votre code Mentora
```

Real recipient, no `[TEST pour ...]` prefix, the two Bcc.

## 4. Coach adds a member (same process)

Coach A (coach@mentora.fr, isCoach true) logged in (token length 405, never printed), `POST /api/v1/coach/members` for l143.member@example.com: HTTP 201. Routing line:

```
[EMAIL] mode=PRODUCTION to=l143.member@example.com bcc=l143.bcc1@example.com,l143.bcc2@example.com subject=Votre coach Federico vous a ajouté sur Mentora
```

## 5. Production without ProductionBcc

The API refuses to start: `EmailRouting:ProductionBcc must contain at least one address when EmailRouting:Mode is Production.` No `Applying migration` line, no `Application started`.

## 6. Default routing again

Startup line: `[EMAIL] routing mode=TEST provider=Logging testRecipients=4 productionBcc=0`.

## Cleanup

Dry run (`cleanup-dryrun.sql`, ends with ROLLBACK): in scope 1 l143 user, 3 OTPs and 1 refresh token of the pre-existing users after the run start; 2 pre-existing users kept. After the deletes: no table with a `*_CREATED_DATE` after the run start, 2 MEMBER_COACHES, 2 USERS left. `cleanup-commit.sql` differs from the dry run only on the last line (`COMMIT;`). The COMMIT has not been run yet.
