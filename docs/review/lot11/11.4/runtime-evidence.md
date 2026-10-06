# Lot 11.4 runtime evidence (emails: 11.O, 11.Z, 11.AB)

Run on 2026-10-06, local API, `ASPNETCORE_ENVIRONMENT=Development`, `Email__Provider=Logging` (nothing was really sent). Routing variables were set in the PowerShell session only and cleared between runs. Run start recorded in `run-start.txt`: `SELECT now()` = `2026-10-06 01:27:14.888849+00`.

Log rule: only lines starting with `[EMAIL] ` were extracted. `[EMAIL-LOG]` lines and the OTP console lines (they hold the code) were never printed or copied. The `[EMAIL]` lines below contain the four associate addresses and the two local test accounts, which is accepted for this evidence.

## 1. Startup refusals (5.2)

Port 5243 free before each start; each process exited by itself, port 5243 free after. "Application started" and "Now listening" never appeared.

| Run | EmailRouting variables set (names) | Failure line |
|---|---|---|
| 5.2a | none | `OptionsValidationException: EmailRouting:TestRecipients must contain at least one address.` |
| 5.2b | `EmailRouting__TestRecipients` (value `not-an-address`) | `OptionsValidationException: EmailRouting:TestRecipients item #1 is not a valid email address.` |
| 5.2c | `EmailRouting__Mode` (`Production`), `EmailRouting__TestRecipients` (one valid placeholder address) | `OptionsValidationException: EmailRouting:ProductionBcc must contain at least one address when EmailRouting:Mode is Production.` |

The messages name the setting and an item number only, never an address.

## 2. TEST mode (5.3)

Variables: `EmailRouting__TestRecipients` = the four addresses; no Mode, no ProductionBcc.

Startup line: `[EMAIL] routing mode=TEST provider=Logging testRecipients=4 productionBcc=0`

| # | Request | Status | Body | `[EMAIL]` line |
|---|---|---|---|---|
| 1 | `coach@mentora.fr`, isCoach true | 200 | `{"success":true,"data":null,"error":null,"statusCode":200}` | `mode=TEST to=<the four addresses> bcc= subject=[TEST pour coach@mentora.fr] Federico, votre code Mentora` |
| 2 | `member@mentora.fr`, isCoach false | 200 | same | `mode=TEST to=<the four addresses> bcc= subject=[TEST pour member@mentora.fr] Test, votre code Mentora` |
| 3a | unknown email | 400 | `{"success":false,"data":null,"error":"User not found or account is disabled.","statusCode":400}` | none |
| 3b | `member@mentora.fr`, isCoach true | 400 | `{"success":false,"data":null,"error":"No coach account for this email.","statusCode":400}` | none |
| 3c | `coach@mentora.fr` (BOTH), no isCoach | 409 | `{"success":false,"data":null,"error":"This email has both a coach and a member account. Please specify isCoach explicitly.","statusCode":409}` | none |

Comparison with the inventory baseline (Q4.1, Q4.2): status and body identical for 3a, 3b, 3c and for the 200 body. Not called: "isCoach false on a coach-only account" (the local DB has no coach-only account, same as the inventory).

Empty first name (5.3.4): read-only check, 0 of 1 coach and 0 of 2 members have an empty first name after trim. No such account exists, none created, so the "unchanged subject" fallback is covered by code only (`Votre code Mentora`).

Expiry text (5.3.5), template source lines in `AuthService.cs` (HTML and text builders), the number comes from the `OtpLifetimeMinutes` constant (10) that also sets `AddMinutes`:

```
358: <p>Ce code expire dans {lifetimeMinutes} minutes.</p>
371: Ce code expire dans {lifetimeMinutes} minutes.
```

## 3. PRODUCTION mode (5.4)

Variables: `EmailRouting__TestRecipients` (four), `EmailRouting__Mode=Production`, `EmailRouting__ProductionBcc` (two gmail addresses).

- Startup: `[EMAIL] routing mode=PRODUCTION provider=Logging testRecipients=4 productionBcc=2`
- `member@mentora.fr`, isCoach false: 200, same body. `[EMAIL] mode=PRODUCTION to=member@mentora.fr bcc=gittenait.francois@gmail.com,pierrejordans@gmail.com subject=Test, votre code Mentora` (no TEST prefix).

## 4. Mode typo "Prod" (5.5)

Variables: `EmailRouting__TestRecipients` (four), `EmailRouting__Mode=Prod`.

- Startup warning: `[EMAIL] EmailRouting:Mode is set but is not "Production": TEST routing applies.`
- Startup line: `[EMAIL] routing mode=TEST provider=Logging testRecipients=4 productionBcc=0`
- `member@mentora.fr`, isCoach false: 200. `[EMAIL] mode=TEST to=<the four addresses> bcc= subject=[TEST pour member@mentora.fr] Test, votre code Mentora`

After each run: API process gone, port 5243 had 0 listeners.

## 5. Leftovers (5.6)

Read-only scan of the 30 tables that have a `*_CREATED_DATE` column, rows created after the run start:

- `AUTH_OTPS`: 4 rows. `coach@mentora.fr` 1 (run 5.3); `member@mentora.fr` 3 (runs 5.3, 5.4, 5.5), of which 2 are marked used (each newer request invalidates the previous unexpired code, as in inventory Q4.1).
- `AUTH_REFRESH_TOKENS`: 0.
- All other tables: 0.

No cleanup needed, no write SQL was run.

## 6. Backlog

- `scripts/seed-test-members.sql` holds the four associate gmail addresses in a versioned seed script (pre-existing, commit `910b404`). 11.Z targets configuration, so left unchanged.
- Bcc hiding is relied on from MailKit 4.17.0 (`FormatOptions.HiddenHeaders`); it has not been proven end to end. Prove it on Alpha day by checking the headers of a real Production mail.
- The `[EMAIL-LOG]` line of the Logging provider logs the text body, so the OTP. Unchanged in 11.4, tied to the Alpha prerequisite on OTPs in logs (the OTP console line at `AuthService.cs:58` too).
