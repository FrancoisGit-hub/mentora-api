-- Lot 10.2 - fixture cleanup, DRY RUN (ends with ROLLBACK). cleanup.sql is identical except the last line (COMMIT).
-- Bounds: device ids starting with 'l102-', the coaches used (coach A, coach B) AND created date >= run start
-- (2026-09-30T10:55:07Z, see run-start.txt). Coach B = user l102-coach-b@test.local + its coach row and every row
-- referencing them. Kept on purpose: coach A / member accounts and their login artifacts (AUTH_OTPS /
-- AUTH_REFRESH_TOKENS), as in lots 9.x; QUIZ_QUESTIONS (product data).
-- Coach A = e268a512-84d6-4e27-b5c0-1d6c08a47190
-- Coach B = 5b4e9c1a-0c1d-4a55-9d3e-102200000002  (user 5b4e9c1a-0c1d-4a55-9d3e-102200000001)

BEGIN;

-- Counts before
SELECT
  (SELECT count(*) FROM "QUIZ_SESSIONS" WHERE ("QUIZ_SESSION_DEVICE_ID" LIKE 'l102-%' OR "QUIZ_SESSION_COACH_ID" IN ('e268a512-84d6-4e27-b5c0-1d6c08a47190','5b4e9c1a-0c1d-4a55-9d3e-102200000002')) AND "QUIZ_SESSION_CREATED_DATE" >= '2026-09-30T10:55:07Z') AS fixture_sessions,
  (SELECT count(*) FROM "QUIZ_SESSIONS") AS sessions_total,
  (SELECT count(*) FROM "QUIZ_DEVICE_CLAIMS" WHERE "QUIZ_DEVICE_CLAIM_DEVICE_ID" LIKE 'l102-%' AND "QUIZ_DEVICE_CLAIM_CREATED_DATE" >= '2026-09-30T10:55:07Z') AS fixture_claims,
  (SELECT count(*) FROM "QUIZ_DEVICE_CLAIMS") AS claims_total,
  (SELECT count(*) FROM "QUIZ_QUESTIONS") AS questions_total,
  (SELECT count(*) FROM "USERS" WHERE "USER_EMAIL" = 'l102-coach-b@test.local') AS fixture_users;

-- 1. QUIZ_SESSION_QUESTIONS of the fixture sessions
WITH d AS (
  DELETE FROM "QUIZ_SESSION_QUESTIONS"
  WHERE "QUIZ_SESSION_QUESTION_SESSION_ID" IN (
      SELECT "QUIZ_SESSION_ID" FROM "QUIZ_SESSIONS"
      WHERE ("QUIZ_SESSION_DEVICE_ID" LIKE 'l102-%' OR "QUIZ_SESSION_COACH_ID" IN ('e268a512-84d6-4e27-b5c0-1d6c08a47190','5b4e9c1a-0c1d-4a55-9d3e-102200000002'))
        AND "QUIZ_SESSION_CREATED_DATE" >= '2026-09-30T10:55:07Z')
  RETURNING 1)
SELECT 'QUIZ_SESSION_QUESTIONS' AS deleted_from, count(*) AS n FROM d;

-- 2. QUIZ_SESSIONS
WITH d AS (
  DELETE FROM "QUIZ_SESSIONS"
  WHERE ("QUIZ_SESSION_DEVICE_ID" LIKE 'l102-%' OR "QUIZ_SESSION_COACH_ID" IN ('e268a512-84d6-4e27-b5c0-1d6c08a47190','5b4e9c1a-0c1d-4a55-9d3e-102200000002'))
    AND "QUIZ_SESSION_CREATED_DATE" >= '2026-09-30T10:55:07Z'
  RETURNING 1)
SELECT 'QUIZ_SESSIONS' AS deleted_from, count(*) AS n FROM d;

-- 3. QUIZ_DEVICE_CLAIMS
WITH d AS (
  DELETE FROM "QUIZ_DEVICE_CLAIMS"
  WHERE "QUIZ_DEVICE_CLAIM_DEVICE_ID" LIKE 'l102-%' AND "QUIZ_DEVICE_CLAIM_CREATED_DATE" >= '2026-09-30T10:55:07Z'
  RETURNING 1)
SELECT 'QUIZ_DEVICE_CLAIMS' AS deleted_from, count(*) AS n FROM d;

-- 4. Coach B: rows auto-seeded at API start, login artifacts, then coach and user
WITH d AS (DELETE FROM "OFFER_PROGRAMS" WHERE "COACH_ID" = '5b4e9c1a-0c1d-4a55-9d3e-102200000002' RETURNING 1)
SELECT 'OFFER_PROGRAMS (coach B)' AS deleted_from, count(*) AS n FROM d;
WITH d AS (DELETE FROM "COACH_PARAMETERS" WHERE "COACH_ID" = '5b4e9c1a-0c1d-4a55-9d3e-102200000002' RETURNING 1)
SELECT 'COACH_PARAMETERS (coach B)' AS deleted_from, count(*) AS n FROM d;
WITH d AS (DELETE FROM "AUTH_OTPS" WHERE "USER_ID" = '5b4e9c1a-0c1d-4a55-9d3e-102200000001' RETURNING 1)
SELECT 'AUTH_OTPS (coach B user)' AS deleted_from, count(*) AS n FROM d;
WITH d AS (DELETE FROM "AUTH_REFRESH_TOKENS" WHERE "USER_ID" = '5b4e9c1a-0c1d-4a55-9d3e-102200000001' RETURNING 1)
SELECT 'AUTH_REFRESH_TOKENS (coach B user)' AS deleted_from, count(*) AS n FROM d;
WITH d AS (DELETE FROM "COACHES" WHERE "COACH_ID" = '5b4e9c1a-0c1d-4a55-9d3e-102200000002' AND "COACH_CREATED_DATE" >= '2026-09-30T10:55:07Z' RETURNING 1)
SELECT 'COACHES (coach B)' AS deleted_from, count(*) AS n FROM d;
WITH d AS (DELETE FROM "USERS" WHERE "USER_ID" = '5b4e9c1a-0c1d-4a55-9d3e-102200000001' AND "USER_EMAIL" = 'l102-coach-b@test.local' AND "USER_CREATED_DATE" >= '2026-09-30T10:55:07Z' RETURNING 1)
SELECT 'USERS (coach B)' AS deleted_from, count(*) AS n FROM d;

-- Counts after (fixtures 0, questions unchanged)
SELECT
  (SELECT count(*) FROM "QUIZ_SESSIONS" WHERE "QUIZ_SESSION_DEVICE_ID" LIKE 'l102-%' OR "QUIZ_SESSION_COACH_ID" IN ('e268a512-84d6-4e27-b5c0-1d6c08a47190','5b4e9c1a-0c1d-4a55-9d3e-102200000002')) AS fixture_sessions,
  (SELECT count(*) FROM "QUIZ_SESSIONS") AS sessions_total,
  (SELECT count(*) FROM "QUIZ_SESSION_QUESTIONS") AS session_questions_total,
  (SELECT count(*) FROM "QUIZ_DEVICE_CLAIMS") AS claims_total,
  (SELECT count(*) FROM "QUIZ_QUESTIONS") AS questions_total,
  (SELECT count(*) FROM "USERS" WHERE "USER_EMAIL" = 'l102-coach-b@test.local') AS fixture_users,
  (SELECT count(*) FROM "COACHES" WHERE "COACH_ID" = '5b4e9c1a-0c1d-4a55-9d3e-102200000002') AS fixture_coaches,
  (SELECT count(*) FROM "OFFER_PROGRAMS" WHERE "COACH_ID" = '5b4e9c1a-0c1d-4a55-9d3e-102200000002') AS fixture_offer_programs,
  (SELECT count(*) FROM "USERS") AS users_total;

ROLLBACK;
