# Lot 10.2 scenarios - run start 2026-09-30T10:55:07Z, executed 2026-09-30T11:08:21Z

coach A = e268a512-84d6-4e27-b5c0-1d6c08a47190, coach B = 5b4e9c1a-0c1d-4a55-9d3e-102200000002

fixture devices: l102-b10eaeced2aa113d, l102-0c90a36541ffdbc5, l102-b940ac7418f14182, l102-f6ece8086797f7e3, l102-70872acb9ca2212d, l102-7c8e056c9885e6f4

## H1 device mode POST sessions
- **H1** `POST /api/v1/quiz/basic/sessions` (no Authorization, X-Device-Id=l102-b10eaeced2aa113d) -> **201** (expected 201) OK; EF queries: 2
  - body: `{"sessionId": "764477ca-c922-4f0a-a6b5-27c24dbdffa7", "expiresAt": "2026-09-30T13:08:24.254525Z", "questionCount": 12, "positions": [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12], "firstQuestionKeys": ["A", "B", "C", "D"]}`
  - check **12 questions**: OK 
  - check **distinct questionIds**: OK 
  - check **positions 1..12 in order**: OK 
  - check **answers A-D in order**: OK 
  - SQL session row: `IN_PROGRESS|12|l102-b10eaeced2aa113d|NULL|120.000`
  - check **SQL expires - created = 120 min**: OK 
  - check **SQL device set, coach NULL**: OK 
  - SQL session questions (count|distinct|min pos|max pos): `12|12|1|12`
  - check **SQL 12 distinct, positions 1-12**: OK 

## H2 / H3 device id missing or invalid
- **H2** `POST /api/v1/quiz/basic/sessions` (no Authorization) -> **422** (expected 422) OK; EF queries: 0
  - body: `{"success": false, "data": null, "error": "Validation failed.", "statusCode": 422, "errors": {"DeviceId": ["X-Device-Id is required in device mode: 16 to 64 characters, ASCII letters, digits and hyphen only."]}}`
  - check **errors key DeviceId**: OK 
- **H3** `POST /api/v1/quiz/basic/sessions` (no Authorization, X-Device-Id=abc) -> **422** (expected 422) OK; EF queries: 0
  - body: `{"success": false, "data": null, "error": "Validation failed.", "statusCode": 422, "errors": {"DeviceId": ["X-Device-Id is required in device mode: 16 to 64 characters, ASCII letters, digits and hyphen only."]}}`
  - check **errors key DeviceId**: OK 

## H4 garbage bearer / H5 member JWT (status only: 401/403 keep the project's empty-body behaviour)
- **H4** `POST /api/v1/quiz/basic/sessions` (JWT, X-Device-Id=l102-b10eaeced2aa113d) -> **401** (expected 401) OK; EF queries: 0
- **H5** `POST /api/v1/quiz/basic/sessions` (JWT, X-Device-Id=l102-b10eaeced2aa113d) -> **403** (expected 403) OK; EF queries: 0

## H6 coach A POST sessions
- **H6** `POST /api/v1/quiz/basic/sessions` (JWT, X-Device-Id=l102-b10eaeced2aa113d) -> **201** (expected 201) OK; EF queries: 2
  - body: `{"sessionId": "3c6628ee-dbda-4ab6-a6e9-f5cfd665b1b7"}`
  - SQL (coach_id|device_id): `e268a512-84d6-4e27-b5c0-1d6c08a47190|NULL` (X-Device-Id sent but must be ignored in coach mode)
  - check **COACH_ID set, DEVICE_ID null**: OK 

## H7 submit coach A session with the correct answers
- **H7** `POST /api/v1/quiz/basic/sessions/3c6628ee-dbda-4ab6-a6e9-f5cfd665b1b7/answers` (JWT) -> **200** (expected 200) OK; EF queries: 4
  - body: `{"score": 120, "correctCount": 12, "questionCount": 12, "totalPoints": 120, "completedAt": "2026-09-30T11:08:29.1203588Z"}`
  - check **score = sum of points**: OK (120 vs 120)
  - check **correctCount 12**: OK 
  - check **results ordered by position**: OK 
  - SQL session: `COMPLETED|120|12`; SQL question sums (points|correct): `120|12`
  - check **SQL agrees**: OK 
  - check **totalPoints = SQL owner sum**: OK 

## H8 resubmit
- **H8** `POST /api/v1/quiz/basic/sessions/3c6628ee-dbda-4ab6-a6e9-f5cfd665b1b7/answers` (JWT) -> **409** (expected 409) OK; EF queries: 1
  - body: `{"success": false, "data": null, "error": "Quiz session already submitted.", "details": {"reason": "ALREADY_SUBMITTED"}, "statusCode": 409}`
  - check **details.reason ALREADY_SUBMITTED**: OK 

## H9-H12 invalid bodies on a fresh in-progress coach A session
- **H9 (11 answers)** `POST /api/v1/quiz/basic/sessions/1a87da10-934d-43d2-a4a3-5de40bc08f7d/answers` (JWT) -> **422** (expected 422) OK; EF queries: 1
  - body: `{"Answers": ["Exactly 12 answers are required (got 11)."]}`
- **H10 (chosenAnswer E)** `POST /api/v1/quiz/basic/sessions/1a87da10-934d-43d2-a4a3-5de40bc08f7d/answers` (JWT) -> **422** (expected 422) OK; EF queries: 1
  - body: `{"Answers": ["chosenAnswer must be one of A, B, C, D (uppercase)."]}`
- **H11 (duplicate questionId)** `POST /api/v1/quiz/basic/sessions/1a87da10-934d-43d2-a4a3-5de40bc08f7d/answers` (JWT) -> **422** (expected 422) OK; EF queries: 1
  - body: `{"Answers": ["Each questionId must appear only once."]}`
- **H12 (question not drawn)** `POST /api/v1/quiz/basic/sessions/1a87da10-934d-43d2-a4a3-5de40bc08f7d/answers` (JWT) -> **422** (expected 422) OK; EF queries: 2
  - body: `{"Answers": ["Answers must cover exactly the questions drawn in the session."]}`
  - check **errors key Answers**: OK 
  - check **session still IN_PROGRESS after the 422s**: OK 

## H13-H15 ownership
- **H13 coach B submits coach A session** `POST /api/v1/quiz/basic/sessions/1a87da10-934d-43d2-a4a3-5de40bc08f7d/answers` (JWT) -> **404** (expected 404) OK; EF queries: 1
  - body: `{"success": false, "data": null, "error": "Quiz session 1a87da10-934d-43d2-a4a3-5de40bc08f7d not found.", "statusCode": 404}`
- **H14 other device submits H1 session** `POST /api/v1/quiz/basic/sessions/764477ca-c922-4f0a-a6b5-27c24dbdffa7/answers` (no Authorization, X-Device-Id=l102-0c90a36541ffdbc5) -> **404** (expected 404) OK; EF queries: 1
  - body: `{"success": false, "data": null, "error": "Quiz session 764477ca-c922-4f0a-a6b5-27c24dbdffa7 not found.", "statusCode": 404}`
- **H15 unknown session id** `POST /api/v1/quiz/basic/sessions/298d235b-1918-4df9-bd86-a6b0476faca8/answers` (no Authorization, X-Device-Id=l102-0c90a36541ffdbc5) -> **404** (expected 404) OK; EF queries: 1
  - body: `{"success": false, "data": null, "error": "Quiz session 298d235b-1918-4df9-bd86-a6b0476faca8 not found.", "statusCode": 404}`
  - check **coach A session untouched by coach B**: OK 

## H16 expired
- **H16** `POST /api/v1/quiz/basic/sessions/7a3209d0-c282-4b65-aac7-14469f84863b/answers` (no Authorization, X-Device-Id=l102-b940ac7418f14182) -> **409** (expected 409) OK; EF queries: 1
  - body: `{"success": false, "data": null, "error": "Quiz session expired.", "details": {"reason": "EXPIRED"}, "statusCode": 409}`
  - check **details.reason EXPIRED**: OK 

## H17 two concurrent submissions
- **H17** statuses: [200, 409]; 409 body: `[{"success": false, "data": null, "error": "Quiz session already submitted.", "details": {"reason": "ALREADY_SUBMITTED"}, "statusCode": 409}]`
  - check **exactly one 200 and one 409**: OK 
  - check **409 is ALREADY_SUBMITTED**: OK 
  - check **SQL one completed session, score set once**: OK 

## H18 GET me
- **H18 coach A** `GET /api/v1/quiz/basic/me` (JWT) -> **200** (expected 200) OK; EF queries: 2
  - body: `{"totalPoints": 240, "completedCount": 3, "items": 3, "nextCursor": null}`
  - SQL (sum|count): `240|3`
  - check **coach A totals = SQL**: OK 
- **H18 device d1** `GET /api/v1/quiz/basic/me` (no Authorization, X-Device-Id=l102-b10eaeced2aa113d) -> **200** (expected 200) OK; EF queries: 2
  - body: `{"totalPoints": 0, "completedCount": 0}`
  - SQL device (sum|count): `0|0` (d1 has one in-progress... no: its H1 session is in progress, so 0|0 expected)
  - check **device totals = SQL**: OK 
- pagination limit=1: 3 pages, ids returned 3, SQL expects 3
  - check **every session once, in order**: OK 
- **H18 bad limit** `GET /api/v1/quiz/basic/me?limit=51` (JWT) -> **400** (expected 400) OK; EF queries: 0
- **H18 bad cursor** `GET /api/v1/quiz/basic/me?cursor=%25%25` (JWT) -> **400** (expected 400) OK; EF queries: 0

## H19 claim
- device d6: 2 completed + 1 in progress; SQL completed score sum = 180; coach A total before = 240
- **H19 claim by coach A** `POST /api/v1/coach/quiz/basic/claim-device` (JWT) -> **200** (expected 200) OK; EF queries: 4
  - body: `{"deviceId": "l102-70872acb9ca2212d", "claimedPoints": 180, "claimedSessions": 2, "totalPoints": 420}`
  - check **claimedPoints = SQL sum**: OK 
  - check **claimedSessions = 2**: OK 
  - check **coach A total increased by claimed points**: OK 
  - SQL after claim: `COMPLETED|t|t ; COMPLETED|t|t ; IN_PROGRESS|t|t` (status|coach=A|device kept)
  - SQL claim row: `t|180`
  - check **claim row points = claimed points**: OK 
- **H19 device d6 GET me after claim** `GET /api/v1/quiz/basic/me` (no Authorization, X-Device-Id=l102-70872acb9ca2212d) -> **200** (expected 200) OK; EF queries: 2
  - body: `{"totalPoints": 0, "completedCount": 0}`
  - check **device total 0**: OK 
- **H19 retry by coach A (idempotent)** `POST /api/v1/coach/quiz/basic/claim-device` (JWT) -> **200** (expected 200) OK; EF queries: 3
  - body: `{"deviceId": "l102-70872acb9ca2212d", "claimedPoints": 180, "claimedSessions": 2, "totalPoints": 420}`
  - check **same values**: OK 
  - check **still one claim row**: OK 
- **H19 coach B** `POST /api/v1/coach/quiz/basic/claim-device` (JWT) -> **409** (expected 409) OK; EF queries: 1
  - body: `{"success": false, "data": null, "error": "Device already claimed.", "details": {"reason": "ALREADY_CLAIMED"}, "statusCode": 409}`
  - check **ALREADY_CLAIMED**: OK 
- **H19 fresh device without session** `POST /api/v1/coach/quiz/basic/claim-device` (JWT) -> **409** (expected 409) OK; EF queries: 3
  - body: `{"success": false, "data": null, "error": "No session to claim for this device.", "details": {"reason": "NOTHING_TO_CLAIM"}, "statusCode": 409}`
  - check **NOTHING_TO_CLAIM**: OK 
  - check **no claim row for d7**: OK 
- **H19 no JWT** `POST /api/v1/coach/quiz/basic/claim-device` (no Authorization) -> **401** (expected 401) OK; EF queries: 0
- **H19 member JWT** `POST /api/v1/coach/quiz/basic/claim-device` (JWT) -> **403** (expected 403) OK; EF queries: 0
- **H19 invalid deviceId** `POST /api/v1/coach/quiz/basic/claim-device` (JWT) -> **422** (expected 422) OK; EF queries: 0
  - body: `{"DeviceId": ["DeviceId must be 16 to 64 characters, ASCII letters, digits and hyphen only."]}`
  - check **errors key DeviceId**: OK 

## H20 in-progress session of the claimed device
- **H20 device mode submit** `POST /api/v1/quiz/basic/sessions/77af2e35-53cc-4eb4-9930-81e047404046/answers` (no Authorization, X-Device-Id=l102-70872acb9ca2212d) -> **404** (expected 404) OK; EF queries: 1
  - body: `{"success": false, "data": null, "error": "Quiz session 77af2e35-53cc-4eb4-9930-81e047404046 not found.", "statusCode": 404}`
- **H20 coach A submit** `POST /api/v1/quiz/basic/sessions/77af2e35-53cc-4eb4-9930-81e047404046/answers` (JWT) -> **200** (expected 200) OK; EF queries: 4
  - body: `{"score": 100, "correctCount": 12, "totalPoints": 520}`
  - check **coach A total = SQL**: OK 

## Result: ALL CHECKS OK
