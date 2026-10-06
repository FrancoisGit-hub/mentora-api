# Lot 11.4 preflight (emails: 11.O, 11.Z, 11.AB)

Read only. Branch `feature/lot_11`, HEAD `7209636 docs(lot11): 11.3 preflight and runtime evidence`, working tree clean.

## 3.1 IEmailSender, implementations, provider choice, call sites

- Interface: `Mentora.Core/Interfaces/IEmailSender.cs:3-16`. One method:
  `Task<bool> SendAsync(string toAddress, string subject, string htmlBody, string textBody, CancellationToken ct)` (`:15`). **One recipient, no BCC.** Contract (`:5-8`): must not throw, failures logged and swallowed.
- Implementations (only two):
  - `Mentora.Infrastructure/Services/Email/LoggingEmailSender.cs:6-15`. Logs and returns true.
  - `Mentora.Infrastructure/Services/Email/SmtpEmailSender.cs:11-83`. MailKit; `BuildMessage` (`:67-82`) does `msg.To.Add(MailboxAddress.Parse(toAddress))` (`:71`), no `Bcc`. Retry loop `:21-59`, returns false after the last attempt (`:61-64`).
- Provider choice: `Mentora.API/Program.cs:302-307`:
  `Configure<EmailOptions>(...)` (`:302`), `var emailProvider = builder.Configuration["Email:Provider"] ?? "Logging";` (`:303`), `Smtp` (OrdinalIgnoreCase) => `AddScoped<IEmailSender, SmtpEmailSender>()` (`:305`), anything else => `LoggingEmailSender` (`:307`). Raw configuration is read, not `EmailOptions.Provider`.
- Call sites: **one**. `Mentora.Infrastructure/Services/AuthService.cs:61` (`emailSender` injected at `:23`). Grep over all `*.cs` for `IEmailSender|SendAsync(`: the other `SendAsync` hits are SignalR in `Mentora.API/Hubs/ChatHub.cs` (2) and MailKit inside `SmtpEmailSender`. **The OTP is the only mail sent today.** No test project references `IEmailSender`.

- Leak today: the OTP code is in the subject, so `SmtpEmailSender`'s Information logs (To and Subject, `:38-40`, `:49-51`, `:61-63`) leak the code on the VPS. 11.4 removes it from the subject. `[EMAIL-LOG]` stays unchanged as decided; those lines are never printed or saved.

## 3.2 Existing email options

- `Mentora.Core/Options/EmailOptions.cs:3-27`, section `"Email"` (`:5`). Fields: `Provider` (default "Logging", comment "Smtp" or "Logging"), `FromAddress`, `FromDisplayName` ("Mentora"), `Smtp{Host, Port=587, UseStartTls=true, Username, Password}`, `Retry{MaxAttempts=2, DelaySeconds=2}`.
- Binding: `Program.cs:302` `Configure<EmailOptions>(GetSection("Email"))`. **Nothing is validated at startup** (no `Validate`, no `ValidateOnStart`).
- Model to follow: `LiveKitOptions` lives in `Mentora.Core/Options/LiveKitOptions.cs:3-12`, bound in `Program.cs:265-273` with `AddOptions<T>().Bind().Validate(...).ValidateOnStart()`. `QuizOptions` the same (`:277-283`).
- Provider values: only `Smtp` and `Logging` exist; any other string silently means Logging (`Program.cs:304-307`).
- `Mentora.API/appsettings.Development.json`: **not tracked** (`git ls-files` prints nothing) and **ignored** (`.gitignore:487: **/appsettings.Development.json`). Content not printed.
- Versioned `appsettings.json:23-` has an `Email` section: Provider `Smtp`, FromAddress, Host, Username set, Password empty (secret comes from user secrets / env). `appsettings.Development.json` and `appsettings.Production.json` have no `Email` section (grep count: only `appsettings.json` matches).

## 3.3 The OTP mail

- Subject (`AuthService.cs:60`): `$"Votre code Mentora : {plainOtp}"`. **The code is in the subject today.**
- HTML body `BuildOtpHtmlBody(string otpCode)` (`:324-340`): title "Mentora", "Bonjour,", "Voici votre code de connexion :", the code, `<p>Ce code expire dans 15 minutes.</p>` (`:335`), "Si vous n'avez pas demandé ce code, ignorez ce message."
- Text body `BuildOtpTextBody(string otpCode)` (`:342-350`): same, "Ce code expire dans 15 minutes." (`:348`).
- The "15" is a hard-coded literal in the two templates. The real expiry is a hard-coded literal too: `AuthService.cs:52` `DateTime.UtcNow.AddMinutes(10)`. No constant, no option. Fix: add a `private const int OtpLifetimeMinutes = 10;` in `AuthService`, use it at `:52` and pass it into both builders.

## 3.4 First name source (checked in code)

`RequestOtpAsync` (`AuthService.cs:29-67`) already loads `db.Users.Include(u => u.Coach).Include(u => u.Member)` (`:32-35`), so **no extra query**.

| Case | Entity and column | Null/empty |
|---|---|---|
| COACH (isCoach true) | `user.Coach.CoachFirstName` (`COACHES.COACH_FIRST_NAME`, NOT NULL, max 100, per inventory Q4.4) | not null; can be whitespace only |
| MEMBER (isCoach false) | `user.Member.MemberFirstName` (`MEMBERS.MEMBER_FIRST_NAME`, NOT NULL, max 100) | same |
| BOTH, isCoach true | Coach profile | same |
| BOTH, isCoach false | Member profile | same |
| BOTH, isCoach absent | 409 before any write (`:123-130`), no mail | n/a |

`ResolveEffectiveIsCoach` (`:111-138`) already returns the effective role, but `:38` discards it. I will keep it (`var effectiveIsCoach = ...`) and read the first name **after `SaveChangesAsync`, at the mail step (`:60`)**, so every error path (unknown/disabled email `:36`, wrong role `:116-119`, 409 `:128`, no profile `:137`) is untouched: same exceptions, same bodies, same timing.

Queries on the OTP request path: today 3 (user with both includes, previous OTPs `ToListAsync` `:41-43`, one `SaveChanges` `:56` for updates plus insert). After: **3**, unchanged.

## 3.5 OTP console line and Logging provider

- OTP console line: `AuthService.cs:58` `Console.WriteLine(...)` (tag in square brackets, email, arrow, plain code), stdout, valid path only, after `SaveChanges`. **Not changed.**
- Logging provider today: `LoggingEmailSender.cs:10-12` `"[EMAIL-LOG] To={To} Subject={Subject} TextBody={TextBody}"`. **TextBody contains the OTP code.** Not changed in this sub-lot. Consequences for the evidence: the new per-mail line `[EMAIL] mode=...` never has the body, but `[EMAIL-LOG]` lines do, so I will never print or save them (filter logs on the `[EMAIL] ` prefix with the space, and `[EMAIL] routing`). Also `SmtpEmailSender` logs `To` and `Subject` at Information (`:38-40`, `:49-51`, `:61-63`), no body.

## 3.6 Docker side

- `Mentora.API/Dockerfile`: only `EXPOSE 8080`; no Email/SMTP/ENV line.
- `docker-compose.local.yml`: PostgreSQL only, no Email setting.
- No other versioned compose. The only versioned Email settings are in `appsettings.json` (3.2). The API reads `Email__Provider`, `Email__FromAddress`, `Email__Smtp__*`, `Email__Retry__*` and the new `EmailRouting__*` from environment variables (standard ASP.NET Core env binding), supplied by the unversioned VPS compose.

## 3.7 Proposed formats and types

### Subject (11.O)

- With first name: `{firstName}, votre code Mentora`  (example `Camille, votre code Mentora`).
- Without (unknown after sanitizing): `Votre code Mentora`.

**Conflict to decide:** 11.O says "else the current subject is used unchanged", but the current subject is `Votre code Mentora : <code>`, and 11.O also says "The OTP code is never in the subject". Both cannot hold. I propose to apply the explicit "never" rule: fallback = `Votre code Mentora` (the current subject without ` : <code>`). Tell me if you want the literal old subject kept instead (not recommended: the code would leak into mail subjects, the TEST prefix log line and the `[EMAIL]` log line).

Sanitizing: `Trim()`, remove every `char.IsControl`, trim again, truncate to 50 characters (`Substring(0, 50)` then `TrimEnd()`); empty means unknown. It is done in a small static helper in `AuthService`.

### Envelope

`Mentora.Core/Interfaces/EmailMessage.cs` (next to `IEmailSender`):

```csharp
public sealed record EmailMessage(
    IReadOnlyList<string> To,
    IReadOnlyList<string> Bcc,
    string Subject,
    string HtmlBody,
    string TextBody);
```

`IEmailSender.SendAsync(EmailMessage message, CancellationToken ct)` (same Task<bool> and no-throw contract). `LoggingEmailSender` and `SmtpEmailSender` adapted; `SmtpEmailSender.BuildMessage` adds every `To` and every `Bcc` (`msg.Bcc.Add(MailboxAddress.Parse(...))`), MailKit sends BCC recipients in the SMTP envelope and does not write the Bcc header to the wire.

### Routing and DI

- `EmailRoutingOptions` in `Mentora.Core/Options/` (same folder as `LiveKitOptions`): `Mode`, `TestRecipients`, `ProductionBcc` as strings, plus `GetTestRecipients()`, `GetProductionBcc()`, `IsProduction`, and a static validator.
- `RoutingEmailSender : IEmailSender` in `Mentora.Infrastructure/Services/Email/`, wraps the concrete provider.
- Registration: `AddScoped<SmtpEmailSender>()` or `AddScoped<LoggingEmailSender>()` (concrete), then `AddScoped<IEmailSender>(sp => new RoutingEmailSender(<provider>, options, logger))`. No new package.
- Startup: `AddOptions<EmailRoutingOptions>().Bind(...).Validate(...).ValidateOnStart()`, same for `EmailOptions` (11.AB). Startup log lines written after `Build()` from the validated options. Mode warning logged there once.

### Other points

- `Email:Provider` today accepts any string (unknown = Logging). For 11.AB I will validate it as `Smtp` or `Logging` (case insensitive), which makes a typo like `Smpt` fail at startup instead of silently logging mails. Small behaviour change, tell me if you prefer to keep the lenient behaviour.
- Real-expiry source: new constant `OtpLifetimeMinutes = 10` in `AuthService` (3.3).
- No migration, no change to the `AuthController` or to `AUTH_OTPS` writes.

STOP: waiting for your approval (and your answer on the subject fallback and the Provider validation) before Phase B.
