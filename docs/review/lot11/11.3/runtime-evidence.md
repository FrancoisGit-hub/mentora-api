# Lot 11.3 runtime evidence (coach session preferences)

Run start (DB time): `2026-10-06 00:16:56.408066+00` (`run-start.txt`). Local Development DB, API on port 5243, `Email__Provider=Logging`.

## 1. Migration `20261006001014_Lot11_3_CoachSessionPreferences` and CHECK proofs

Applied with `dotnet ef database update` (Development).

Post-migration column check (read only):

```
         column_name         | data_type | is_nullable | column_default
-----------------------------+-----------+-------------+----------------
 COACH_IN_PERSON_LOCATIONS   | jsonb     | NO          | '[]'::jsonb
 COACH_OFFERS_IN_PERSON_SOLO | boolean   | NO          | false
 COACH_OFFERS_VISIO          | boolean   | NO          | false
```

CHECK definition:

```
CK_COACHES_IN_PERSON_LOCATIONS | CHECK (((jsonb_typeof("COACH_IN_PERSON_LOCATIONS") = 'array'::text) AND (jsonb_array_length("COACH_IN_PERSON_LOCATIONS") <= 10) AND ("COACH_OFFERS_IN_PERSON_SOLO" OR (jsonb_array_length("COACH_IN_PERSON_LOCATIONS") = 0))))
```

Existing rows: `COACH_OFFERS_VISIO f | COACH_OFFERS_IN_PERSON_SOLO f | COACH_IN_PERSON_LOCATIONS [] | count 1`.

CHECK proofs, each statement sent as `BEGIN; <update>; ROLLBACK;` (the error aborts the transaction, nothing is written):

1. Locations while solo is false:
   `UPDATE "COACHES" SET "COACH_IN_PERSON_LOCATIONS"='["a"]'::jsonb`
   `ERROR:  new row for relation "COACHES" violates check constraint "CK_COACHES_IN_PERSON_LOCATIONS"`
2. Eleven items with solo true:
   `UPDATE "COACHES" SET "COACH_OFFERS_IN_PERSON_SOLO"=true, "COACH_IN_PERSON_LOCATIONS"='["1",...,"11"]'::jsonb`
   `ERROR:  new row for relation "COACHES" violates check constraint "CK_COACHES_IN_PERSON_LOCATIONS"`
3. JSON object with solo true:
   `UPDATE "COACHES" SET "COACH_OFFERS_IN_PERSON_SOLO"=true, "COACH_IN_PERSON_LOCATIONS"='{"a":1}'::jsonb`
   `ERROR:  new row for relation "COACHES" violates check constraint "CK_COACHES_IN_PERSON_LOCATIONS"`

After the three: `f | f | []` (unchanged).

API startup, filtered on `Applying migration`, `Seeder`, `Now listening`, `fail:`, `crit:`, `Exception`: no `Applying migration` line, seeders created 0 rows, no `fail:`, `crit:` or exception, `Now listening on: http://localhost:5243`.

## 2. HTTP results (coach@mentora.fr as coach A)

First run (cases 2 and 6 failed, explained below), then the corrected run.

| # | Case | Code | Body |
|---|---|---|---|
| 1 | GET | 200 | `{"success":true,"data":{"offersVisio":false,"offersInPersonSolo":false,"inPersonLocations":[]},"error":null,"statusCode":200}` |
| 2 (first run) | PUT with accented label | **400** | `{"success":false,"data":null,"error":"Request body is required.","statusCode":400}` |
| 2 (re-run) | PUT `{true,true,["  Salle Fitness Park Antibes  ","Domicile de l'élève"]}` | 200 | `{"success":true,"data":{"offersVisio":true,"offersInPersonSolo":true,"inPersonLocations":["Salle Fitness Park Antibes","Domicile de l'élève"]},"error":null,"statusCode":200}` |
| 2 GET | GET after | 200 | same data as the PUT response |
| 2 SQL | | | `t \| t \| ["Salle Fitness Park Antibes", "Domicile de l'élève"]` |
| 3 | PUT `{true,false,["X"]}` | 422 | `{"success":false,"data":null,"error":"Validation failed.","statusCode":422,"errors":{"InPersonLocations":["Locations must be empty when in-person solo is not offered."]}}` |
| 4 | PUT 11 labels, solo true | 422 | `{"success":false,"data":null,"error":"Validation failed.","statusCode":422,"errors":{"InPersonLocations":["At most 10 locations are allowed."]}}` |
| 5a | PUT 101-character label | 422 | `{"success":false,"data":null,"error":"Validation failed.","statusCode":422,"errors":{"InPersonLocations[0]":["Each location must be 1 to 100 characters once trimmed."]}}` |
| 5b | PUT label `"   "` | 422 | same body as 5a |
| 6 (first run) | PUT `["Salle Été","salle ete"]` | **400** | `{"success":false,"data":null,"error":"Request body is required.","statusCode":400}` |
| 6 (re-run) | same with `\u` escapes | 422 | `{"success":false,"data":null,"error":"Validation failed.","statusCode":422,"errors":{"InPersonLocations[1]":["Duplicate location (case and accents ignored)."]}}` |
| 6b | `["Salle","SALLE"]` | 422 | same body as 6 |
| 7a | PUT body without `offersVisio` (sent `{"offersSolo":1,"offersInPersonSolo":false,"inPersonLocations":[]}`) | 422 | `{"success":false,"data":null,"error":"Validation failed.","statusCode":422,"errors":{"OffersVisio":["'Offers Visio' ne doit pas avoir la valeur null."]}}` |
| 7b | PUT `inPersonLocations` null | 422 | `{"success":false,"data":null,"error":"Validation failed.","statusCode":422,"errors":{"InPersonLocations":["'In Person Locations' ne doit pas avoir la valeur null."]}}` |
| 8 | PUT `{false,false,[]}` | 200 | `{"success":true,"data":{"offersVisio":false,"offersInPersonSolo":false,"inPersonLocations":[]},"error":null,"statusCode":200}` |
| 9 | PUT `{true,false,[]}` | 200 | `{"success":true,"data":{"offersVisio":true,"offersInPersonSolo":false,"inPersonLocations":[]},"error":null,"statusCode":200}` |
| 10a | no token | 401 | empty |
| 10b | member token | 403 | empty |
| 11 | `GET /coach/parameters` before and after the preference PUTs of the re-run | 200 | hash of the body with the `updatedAt` property removed identical (`c7583fb2d0d5de37` both times); `updatedAt` identical (`2026-10-06T00:29:39.041459Z`) |
| 12 | `PUT /coach/parameters` with the current values (see point 3), then GET session-preferences | 200 | GET and SQL still `true, true, [two labels]` (`t \| t \| ["Salle Fitness Park Antibes", "Domicile de l'élève"]`) |

First failed run: cases 2 and 6 returned 400 "Request body is required". The accented characters (é, è, É) went through Git Bash `curl -d` in the wrong encoding, which produced an invalid UTF-8 body that the JSON binder rejected. It was a shell quoting/encoding problem, not an API problem. Both were re-run with `é`-style escapes. Cases 3, 4, 5, 7, 8, 9, 10 were unaffected.

## 3. COACH_PARAMETERS (`PUT /api/v1/coach/parameters`) — what is and is not proved

Two PUTs were sent to `/coach/parameters`, each with a body built by taking the GET body and cutting the `{"success":true,"data":` wrapper and the `,"error"...` tail (so the body is the `data` object):
- first run: during the run where cases 2 and 6 failed (200);
- second run: case 12 (200).

The 16 editable fields of `UpdateCoachParameterRequest` (`Mentora.Core/DTOs/Lot2/UpdateCoachParameterRequest.cs`): FirstName, LastName, Phone, HourlyRateEuros, CancellationDelayHours, MinBookingNoticeHours, MaxBookingHorizonDays, LateCancellationRefunds, IsAcceptingNewBookings, DefaultSessionDurationMinutes, MissedSessionBehavior, Language, NotifMessages, NotifNewBooking, NotifBookingCancelled, NotifMarketing.

`CoachParameterService.ToDto` returns those same 16 properties plus `email` and `updatedAt`, so a body cut from the GET data object contains all 16 plus the two read-only ones (ignored by the binder). That is by construction: **the bodies actually sent were not saved**, and I did not capture a GET body after either PUT. So I cannot prove from recorded output that the 16 sent values equal the values returned by the GET just before, nor show a post-PUT GET body.

What is recorded:
- hash of the GET body before the first run: `42e7ba3021ab2d5d`; after the first-run PUT: `6f3ed65288dca7d7` (different, as `updatedAt` is bumped by every PUT; the body itself was not kept, so that the difference is only `updatedAt` is not proved);
- the second run's GET before case 12 had `updatedAt` `2026-10-06T00:29:39.041459Z`; the second PUT then set `COACH_PARAMETER_UPDATED_DATE` to `2026-10-06 00:31:31.878712+00`;
- read-only SQL after the run shows the COACH_PARAMETERS row with: hourly rate 50.00, cancellation delay 24, min notice 24, max horizon 90, late cancellation refunds false, accepting new bookings true, default session duration 60, missed session behavior SKIP, language FR, notif messages true, new booking true, booking cancelled true, marketing false. These are the seeder defaults (50 €/h, 24 h), consistent with no value drift, but there is no recorded "before" row to compare field by field.

If a strict proof is required, it needs a new run that saves the GET body, the sent body and the GET after.

## 4. Restore and leftovers

Restore: PUT `{false,false,[]}`: 200, body `{"success":true,"data":{"offersVisio":false,"offersInPersonSolo":false,"inPersonLocations":[]},"error":null,"statusCode":200}`. SQL: `f | f | []`. API stopped; port 5243 has 0 listeners.

Leftover scan (rows with `*_CREATED_DATE` after `2026-10-06 00:16:56.408066+00`):

```
 AUTH_OTPS              | AUTH_OTP_CREATED_DATE              | 2
 AUTH_REFRESH_TOKENS    | AUTH_REFRESH_TOKEN_CREATED_DATE    | 2
 AGENT_TOKENS, CARTS, COACHES, COACH_PARAMETERS, CONVERSATIONS, EQUIPMENT, EXERCISES,
 EXERCISE_VIDEOS, MEMBERS, MEMBER_PARAMETERS, MUSCLE_GROUPS, OBJECTIVES, OFFER_PROGRAMS,
 ORDERS, PRODUCTS, PRODUCT_PACKS, PROGRAMS, PROGRAM_TEMPLATES, QUIZ_DEVICE_CLAIMS,
 QUIZ_QUESTIONS, QUIZ_SESSIONS, QUIZ_SESSION_QUESTIONS, SESSIONS, SESSION_PARTICIPANTS,
 SESSION_SLOTS, SESSION_VOUCHERS, USERS, USER_DEVICES  | 0 each (28 tables)
```

Allowed leftovers: the 2 AUTH_OTPS and 2 AUTH_REFRESH_TOKENS rows of coach@ and member@. No cleanup needed.

## Backlog
- 422 default FluentValidation messages follow the server culture (French locally, likely English in the Linux container); clients must rely on the error keys, not the messages.

## Hygiene
Log and token files deleted (`Test-Path` False for `api.log`, `coach/tokens.json`, `member/tokens.json`, `coach`, `member`). `grep -l "\[OTP\]"` on this folder: no match. The three-segment JWT grep flags `preflight.md` line 26 only: the C# identifier `CultureInfo.InvariantCulture.CompareInfo`, not a token.
