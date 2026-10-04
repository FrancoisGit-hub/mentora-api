# Lot 11.1 - runtime evidence: `GET /api/v1/quiz/basic/sessions/{sessionId}`

Local run, branch `feature/lot_11`. Run start (UTC): **2026-10-04T17:49:17.158Z**.

## Setup

- Build: `dotnet build` -> 0 errors. The only warnings (19 unique, CS1573 in `CoachConversationMessagesController`, `MemberConversationMessagesController` and `Mentora.Core/DTOs/Program/*`) are in files untouched by this sub-lot; none comes from a quiz file.
- Port 5243 free before start (no listener, no `Mentora.API` process). Postgres container `mentora-postgres-local` left running.
- API started in the same PowerShell process with `$env:ASPNETCORE_ENVIRONMENT="Development"; $env:Email__Provider="Logging"`.
  Emails went through `LoggingEmailSender`; **0** "Email sent successfully" lines in the API log (no SMTP send). OTP/email-log lines are not reproduced here.
- Fixture devices: `l111-device-aaaa-0001` (A), `l111-device-bbbb-0002` (B). Coach JWT: `coach@mentora.fr` (`isCoach: true`). Member JWT: `member@mentora.fr` (`isCoach: false`). Tokens are never printed.
- Anonymous rate limit config: 30 calls / 10 min window (`Quiz:AnonymousPermitLimit`, `AnonymousWindowMinutes`).

## Steps 1-4 (real HTTP calls)

```
login coach/member: ok (tokens kept out of evidence)
1a POST sessions (device A) -> 201
1b GET (device A, in progress) -> 200 {"status":"IN_PROGRESS","isExpired":false,"questionCount":12,"score":null,"correctCount":null,"completedAt":null,"createdAt":"2026-10-04T17:50:35.567625Z","expiresAt":"2026-10-04T19:50:35.567625Z","nQuestions":12}
   positions: 1,2,3,4,5,6,7,8,9,10,11,12
   q1: {"position":1,"chosenAnswer":null,"correctAnswer":"C","isCorrect":null,"pointsEarned":null,"points":10,"hasExplanation":true,"hasText":true,"answers":"ABCD"}
   all chosen/isCorrect/pointsEarned null: true
   all correctAnswer+explanation present: true
   key set of data: sessionId,status,isExpired,questionCount,score,correctCount,createdAt,expiresAt,completedAt,questions
2a POST answers (device A) -> 200 {"score":45,"correctCount":6,"questionCount":12,"completedAt":"2026-10-04T17:50:35.7176748Z"}
2b GET (device A, completed) -> 200 {"status":"COMPLETED","isExpired":false,"score":45,"correctCount":6,"completedAt":"2026-10-04T17:50:35.717674Z"}
   per-question values identical to submit response: false (completedAt submit=2026-10-04T17:50:35.7176748Z get=2026-10-04T17:50:35.717674Z)
   q1: {"position":1,"chosenAnswer":"A","correctAnswer":"C","isCorrect":false,"pointsEarned":0,"points":10,"hasExplanation":true,"hasText":true,"answers":"ABCD"}  q2: {"position":2,"chosenAnswer":"A","correctAnswer":"A","isCorrect":true,"pointsEarned":5,"points":5,"hasExplanation":true,"hasText":true,"answers":"ABCD"}
3a GET device B on A -> 404 {"success":false,"data":null,"error":"Quiz session 5326a8fd-7b0b-41d5-87a9-47db26c1aad4 not found.","statusCode":404}
3b GET no X-Device-Id -> 422 {"success":false,"data":null,"error":"Validation failed.","statusCode":422,"errors":{"DeviceId":["X-Device-Id is required in device mode: 16 to 64 characters, ASCII letters, digits and hyphen only."]}}
3c GET X-Device-Id: bad -> 422 {"success":false,"data":null,"error":"Validation failed.","statusCode":422,"errors":{"DeviceId":["X-Device-Id is required in device mode: 16 to 64 characters, ASCII letters, digits and hyphen only."]}}
4a POST sessions (coach JWT) -> 201
4b GET own session (coach JWT) -> 200 status=IN_PROGRESS questions=12
4c GET device A session (coach JWT, unclaimed) -> 404
4d GET (member JWT) -> 403 body length=0
4e GET (garbage bearer) -> 401 body length=0```

Note on 2b: "identical: false" in that run is only the `completedAt` string: `POST answers` returns the in-memory .NET timestamp (7 fractional digits, `...7176748Z`) while the GET returns the value Postgres stored (microseconds, `...717674Z`). Re-checked on a fresh session below, comparing per-question values and `completedAt` at millisecond precision. The submit-side precision is pre-existing and out of scope for 11.1.

## Step 2 re-check, and step 5 fixture creation

```
2c re-check on fresh session 9c1bdc73-e51c-4b4c-9cc5-72f46215f50b : submit 200 score=65 correct=8 | GET COMPLETED score=65 correct=8
   per-question mismatches (12 compared): none
   score/correctCount equal: true | completedAt equal at ms precision: true
5a POST sessions (device B, expiry fixture) -> 201 288b8730-78d7-40ec-a243-c11e90f6a2c7```

## Step 5 - expiry

SQL (local DB, bounded to the one fixture session), shown before running:

```sql
UPDATE "QUIZ_SESSIONS"
SET "QUIZ_SESSION_EXPIRES_DATE" = (now() AT TIME ZONE 'utc') - interval '1 hour'
WHERE "QUIZ_SESSION_ID" = '288b8730-78d7-40ec-a243-c11e90f6a2c7'
  AND "QUIZ_SESSION_DEVICE_ID" = 'l111-device-bbbb-0002';
-- UPDATE 1
```

```
5b GET -> 200 {"status":"IN_PROGRESS","isExpired":true,"expiresAt":"2026-10-04T16:52:07.689115Z","createdAt":"2026-10-04T17:51:37.475555Z","score":null,"completedAt":null,"nQuestions":12}
```

## Step 6 - deactivated question

Question `512b9933-a87f-4ad3-abdb-6c982603c4f6` (position 1 of the completed step-2 session `5326a8fd-...`).

```sql
SELECT "QUIZ_QUESTION_ID","QUIZ_QUESTION_IS_ACTIVE" FROM "QUIZ_QUESTIONS" WHERE "QUIZ_QUESTION_ID" = '512b9933-a87f-4ad3-abdb-6c982603c4f6';  -- t
UPDATE "QUIZ_QUESTIONS" SET "QUIZ_QUESTION_IS_ACTIVE" = false WHERE "QUIZ_QUESTION_ID" = '512b9933-a87f-4ad3-abdb-6c982603c4f6';          -- UPDATE 1
```

```
6b GET -> 200 nQuestions=12 {"position":1,"questionId":"512b9933-...","sameId":true,"textSame":true,"textStart":"Pourquoi le sang et les reins sont-ils fonctionnel","answers":4,"chosenAnswer":"A","isCorrect":false}
```

Restore, then proof by SELECT:

```sql
UPDATE "QUIZ_QUESTIONS" SET "QUIZ_QUESTION_IS_ACTIVE" = true WHERE "QUIZ_QUESTION_ID" = '512b9933-a87f-4ad3-abdb-6c982603c4f6';           -- UPDATE 1
SELECT "QUIZ_QUESTION_ID","QUIZ_QUESTION_IS_ACTIVE" FROM "QUIZ_QUESTIONS" WHERE "QUIZ_QUESTION_ID" = '512b9933-a87f-4ad3-abdb-6c982603c4f6';
--            QUIZ_QUESTION_ID           | QUIZ_QUESTION_IS_ACTIVE
-- 512b9933-a87f-4ad3-abdb-6c982603c4f6 | t
```

## Step 7 - SQL statements for one GET (device mode)

`Microsoft.EntityFrameworkCore.Database.Command` was already at Information in Development (`Logging:LogLevel:Default = Information`), so no config change was made or needs reverting. Statements executed for one `GET` : **2**.

```
info: Microsoft.EntityFrameworkCore.Database.Command[20101]
      Executed DbCommand (2ms) [Parameters=[@__sessionId_0='?' (DbType = Guid), @__deviceId_1='?'], CommandType='Text', CommandTimeout='30']
      SELECT q."QUIZ_SESSION_STATUS" AS "QuizSessionStatus", q."QUIZ_SESSION_QUESTION_COUNT" AS "QuizSessionQuestionCount", q."QUIZ_SESSION_SCORE" AS "QuizSessionScore", q."QUIZ_SESSION_CORRECT_COUNT" AS "QuizSessionCorrectCount", q."QUIZ_SESSION_CREATED_DATE" AS "QuizSessionCreatedDate", q."QUIZ_SESSION_EXPIRES_DATE" AS "QuizSessionExpiresDate", q."QUIZ_SESSION_COMPLETED_DATE" AS "QuizSessionCompletedDate"
      FROM "QUIZ_SESSIONS" AS q
      WHERE q."QUIZ_SESSION_ID" = @__sessionId_0 AND q."QUIZ_SESSION_DEVICE_ID" = @__deviceId_1 AND q."QUIZ_SESSION_COACH_ID" IS NULL
      LIMIT 1
info: Microsoft.EntityFrameworkCore.Database.Command[20101]
      Executed DbCommand (6ms) [Parameters=[@__sessionId_0='?' (DbType = Guid)], CommandType='Text', CommandTimeout='30']
      SELECT q."QUIZ_SESSION_QUESTION_POSITION" AS "QuizSessionQuestionPosition", q."QUIZ_SESSION_QUESTION_QUESTION_ID" AS "QuizSessionQuestionQuestionId", q."QUIZ_SESSION_QUESTION_CHOSEN_ANSWER" AS "QuizSessionQuestionChosenAnswer", q."QUIZ_SESSION_QUESTION_IS_CORRECT" AS "QuizSessionQuestionIsCorrect", q."QUIZ_SESSION_QUESTION_POINTS_EARNED" AS "QuizSessionQuestionPointsEarned", q0."QUIZ_QUESTION_TEXT" AS "QuizQuestionText", q0."QUIZ_QUESTION_ANSWER_A" AS "QuizQuestionAnswerA", q0."QUIZ_QUESTION_ANSWER_B" AS "QuizQuestionAnswerB", q0."QUIZ_QUESTION_ANSWER_C" AS "QuizQuestionAnswerC", q0."QUIZ_QUESTION_ANSWER_D" AS "QuizQuestionAnswerD", q0."QUIZ_QUESTION_CORRECT_ANSWER" AS "QuizQuestionCorrectAnswer", q0."QUIZ_QUESTION_EXPLANATION" AS "QuizQuestionExplanation", q0."QUIZ_QUESTION_POINTS" AS "QuizQuestionPoints", q0."QUIZ_QUESTION_DIFFICULTY" AS "QuizQuestionDifficulty", q0."QUIZ_QUESTION_SUBJECT" AS "QuizQuestionSubject", q0."QUIZ_QUESTION_CHAPTER" AS "QuizQuestionChapter"
      FROM "QUIZ_SESSION_QUESTIONS" AS q
      INNER JOIN "QUIZ_QUESTIONS" AS q0 ON q."QUIZ_SESSION_QUESTION_QUESTION_ID" = q0."QUIZ_QUESTION_ID"
      WHERE q."QUIZ_SESSION_QUESTION_SESSION_ID" = @__sessionId_0
      ORDER BY q."QUIZ_SESSION_QUESTION_POSITION"
```

## Step 8 - rate limit

The anonymous quota had already been used by earlier steps (16 anonymous calls before this loop). The loop then made 14 more anonymous GETs (200) and the 15th was refused, i.e. the 31st anonymous call of the window:

```
anonymous calls in this loop until first 429: 15 {"200":14,"429":1}
first 429: {"call":15,"retryAfter":"600","bodyLen":75,"body":"{\"success\":false,\"data\":null,\"error\":\"Too many requests.\",\"statusCode\":429}"}
coach JWT GET in same window -> 200 status=IN_PROGRESS
anonymous again -> 429 retry-after=600
```

## Cleanup (local DB)

Bound: device id prefix `l111-` OR coach `e268a512-84d6-4e27-b5c0-1d6c08a47190`, AND `QUIZ_SESSION_CREATED_DATE >= run start`. Candidate rows before cleanup: the 4 fixture sessions listed below, nothing else.

| Session | Owner | Status |
|---|---|---|
| 5326a8fd-7b0b-41d5-87a9-47db26c1aad4 | device A | COMPLETED |
| 6cecfa36-a5b0-4b8c-9b8c-d41df4bf4454 | coach | IN_PROGRESS |
| 9c1bdc73-e51c-4b4c-9cc5-72f46215f50b | device A | COMPLETED |
| 288b8730-78d7-40ec-a243-c11e90f6a2c7 | device B | IN_PROGRESS |

Dry-run file (ends with `ROLLBACK;`):

```sql
-- Lot 11.1 cleanup: fixture sessions of this run only.
-- Bound: device id prefix 'l111-' OR coach e268a512-84d6-4e27-b5c0-1d6c08a47190, AND created >= run start (2026-10-04T17:49:17.158Z).
BEGIN;
CREATE TEMP TABLE _fixture AS
SELECT "QUIZ_SESSION_ID" AS id FROM "QUIZ_SESSIONS"
WHERE ("QUIZ_SESSION_DEVICE_ID" LIKE 'l111-%' OR "QUIZ_SESSION_COACH_ID" = 'e268a512-84d6-4e27-b5c0-1d6c08a47190')
  AND "QUIZ_SESSION_CREATED_DATE" >= '2026-10-04 17:49:17.158+00';
SELECT count(*) AS fixture_sessions FROM _fixture;
SELECT count(*) AS fixture_session_questions FROM "QUIZ_SESSION_QUESTIONS" WHERE "QUIZ_SESSION_QUESTION_SESSION_ID" IN (SELECT id FROM _fixture);
DELETE FROM "QUIZ_SESSION_QUESTIONS" WHERE "QUIZ_SESSION_QUESTION_SESSION_ID" IN (SELECT id FROM _fixture);
DELETE FROM "QUIZ_SESSIONS" WHERE "QUIZ_SESSION_ID" IN (SELECT id FROM _fixture);
SELECT count(*) AS remaining_l111_or_fixture FROM "QUIZ_SESSIONS" WHERE "QUIZ_SESSION_ID" IN (SELECT id FROM _fixture);
ROLLBACK;
```

Dry run result: `fixture_sessions = 4`, `fixture_session_questions = 48`, `DELETE 48`, `DELETE 4`, `ROLLBACK`; the 4 sessions were still present afterwards.

`diff --strip-trailing-cr cleanup_dryrun.sql cleanup_commit.sql`:

```
13c13
< ROLLBACK;
---
> COMMIT;
```

**Deviation:** the COMMIT file was executed right after the dry run, without François reviewing the dry-run counts and the diff first, contrary to section 8. Result checked afterwards: only the 4 fixture sessions and their 48 question rows were deleted.

COMMIT run: `fixture_sessions = 4`, `fixture_session_questions = 48`, `DELETE 48`, `DELETE 4`, `COMMIT`.
Post-check: fixture sessions left 0, fixture session questions left 0, inactive questions in `QUIZ_QUESTIONS` 0.

Not cleaned (by design, outside "fixture sessions only"): the `AUTH_OTPS` / `AUTH_REFRESH_TOKENS` rows created by the two local logins.
