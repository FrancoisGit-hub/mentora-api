# Lot 14 wave 3: review fix evidence (N1) and runbook section

Date: 2026-11-25. Branch `feature/lot_14`, on top of 7853dd7. Source: `docs/review/lot14/review-wave3.md`.

## 1. What was fixed

### N1 (SHOULD): surrogate-safe cut in `NotificationTexts.Fit`

`Mentora.Core/Notifications/NotificationTexts.cs`: `Fit` is now a block body with the same rule as `ChatBody`: return the value when it fits, else cut at `maxLength`, and step back by one character when the last kept character is a high surrogate. The XML summary says it never splits a surrogate pair. Nothing else changed in the file.

### Runbook section

`docs/review/lot12/12.5a/prod-runbook.md`: new section "Lot 14 vague 3 (25/11)" appended at the end (301 lines added, 0 removed), copied from the section 5 of the review report with five changes:

- E1: point b also backs up the two audio folders (the two `tar` lines of step 2 of the lot 13 section, copied as they are); the expected result lists the two archives.
- E2: point e no longer changes the `image:` line of api-prod (the current image ignores the variable and the mount); the line changes at point k.
- E3: point k edits the `image:` line of api-prod first, with a check of the compose.
- E4: point c, name lengths are for information (N1 fixed before the deployment).
- E5: a paragraph says N1 is included, N2 is moved to lot 16, and what that means during the Beta.

## 2. Build

`dotnet build --no-incremental`, log outside the repository:

```
    19 Avertissement(s)
    0 Erreur(s)
```

No warning in `NotificationTexts.cs` (search of the log for the file name: no match).

## 3. Proof of N1 (throwaway console project, no API, no database)

Project outside the repository, referencing `Mentora.Core`, deleted afterwards (`Test-Path` False). Output:

```
name length = 101
old: length=100 endsWithHighSurrogate=True strictEncoder=EncoderFallbackException
new: length=99 endsWithHighSurrogate=False strictEncoder=ok
body: rawLength=201 fitLength=200 lengthOk=True endsWithHighSurrogate=False strictEncoder=ok
plain: Fit(150 chars, 100) length=100
```

Reading:

- Title case (98 letters plus one emoji, full name of 101 characters): the old cut `name[..100]` ends with a lone high surrogate and the strict UTF-8 encoder (the behaviour Npgsql relies on) throws `EncoderFallbackException`. The new `Fit` gives 99 characters, no lone surrogate, encoder succeeds.
- Body case (MEMBER_JOINED, first name of 165 letters plus one emoji): raw body of 201 characters, cut at 200, encoder succeeds. In this exact input the emoji sits before the cut, so this case checks the length and the encoding of the body path rather than a split pair; the split pair itself is proven by the title case.
- A string without surrogate (150 characters, limit 100) keeps the old behaviour: length 100.

## 4. Column and table names used by the runbook SQL (report section 5)

All names checked against the EF configurations (`Mentora.Infrastructure/Persistence/Configurations`) and the migration of 14.9b. None differs.

| Table or column | Source |
|---|---|
| `USER_DEVICES` | `UserDeviceConfiguration.cs:13` |
| `MEMBERS` | `MemberConfiguration.cs:13` |
| `MEMBER_HAS_ACTIVATED`, `MEMBER_FIRST_NAME`, `MEMBER_LAST_NAME` | `MemberConfiguration.cs:25`, `:20`, `:21` |
| `MEMBER_COACHES`, `MEMBER_ID` | `MemberCoachConfiguration.cs:11`, `:18` |
| `COACHES`, `COACH_FIRST_NAME`, `COACH_LAST_NAME` | `CoachConfiguration.cs:11`, `:18`, `:19` |
| `PROGRAM_TEMPLATES`, `PROGRAM_TEMPLATE_BODY` | `ProgramTemplateConfiguration.cs:13`, `:50` |
| `SESSION_TEMPLATES`, `SESSION_TEMPLATE_CONTENT` | `SessionTemplateConfiguration.cs:11`, `:48` |
| `PROGRAM_CIRCUITS`, `PROGRAM_CIRCUIT_MODE` | `ProgramCircuitConfiguration.cs:11`, `:37` |
| `NOTIFICATIONS` | `NotificationConfiguration.cs:16` |
| `NOTIFICATION_TYPE`, `NOTIFICATION_STATUS`, `NOTIFICATION_BODY`, `NOTIFICATION_CREATED_DATE` | `NotificationConfiguration.cs:26`, `:38`, `:32`, `:54` |

## 5. N2 moved to lot 16

Decision of François on 25/11: N2 (a batch interrupted after FCM accepted a message is sent again in full) is not fixed in lot 14 and moves to lot 16. Consequence during the Beta: if the API stops during a send round (restart, deployment), the notifications already sent in that round (20 at most) can be delivered a second time; nothing is written wrongly in the database.

## 6. Findings left to the backlog

- N3: a 401 or 403 that concerns one device is handled as a token problem or kept forever (cost, not correctness).
- N4: the batch transaction stays open during the HTTP calls (comes with the per-row transaction of N2).
- N5: an OAuth failure is retried on every device and never backed off.
- N6: MEMBER_JOINED is sent for existing members who were never marked activated (the runbook precheck counts them).
- N7: FK Restrict on USERS breaks the team cleanup scripts until they delete NOTIFICATIONS rows first.
- N8: the device list is read once per batch (a moved or removed token can be tried once more).
- N9: the seeder still holds the literal "INTERVAL" instead of `CircuitModes.Interval`.
- N10: key file patterns in `.gitignore` and `.dockerignore` only cover the default names.
- N11: default HttpClient logging adds lines for every FCM and OAuth call (log volume only).
