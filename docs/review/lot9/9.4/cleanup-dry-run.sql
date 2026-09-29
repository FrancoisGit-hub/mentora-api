-- Lot 9.4 - fixture cleanup, DRY RUN (ends with ROLLBACK). cleanup.sql is identical except COMMIT.
-- Deletes only rows created by this run: fixed UUIDs AND created date >= run start (2026-09-29T18:21:40Z).
-- Kept on purpose (product data, not fixtures): the "Sans matériel" row, the 47 equipment rows created by
-- the seeder (7.2) and their links. Login artifacts of coach@/member@ (AUTH_OTPS / AUTH_REFRESH_TOKENS)
-- are technical rows, reported in the evidence and not deleted, as in 9.2 and 9.3.

BEGIN;

-- 1. EXERCISES: coach B's exercise + coach A's two test exercises
DELETE FROM "EXERCISES"
WHERE "EXERCISE_ID" IN (
        '5b4e9c1a-0c1d-4a55-9d3e-940400000003',   -- coach B fixture
        '1eebb781-3f3c-42ec-8842-b109b0d5405f',   -- coach A active test exercise
        '41f04f69-cb37-47c5-9148-45ba401f7e50')   -- coach A soft-deleted test exercise
  AND "EXERCISE_CREATED_DATE" >= '2026-09-29T18:21:40Z'
RETURNING "EXERCISE_ID", "EXERCISE_NAME", "EXERCISE_COACH_ID";

-- 2. OFFER_PROGRAMS auto-seeded for coach B at API start
DELETE FROM "OFFER_PROGRAMS"
WHERE "COACH_ID" = '5b4e9c1a-0c1d-4a55-9d3e-940400000002'
RETURNING "OFFER_PROGRAM_ID";

-- 3. COACH_PARAMETERS auto-seeded for coach B at API start
DELETE FROM "COACH_PARAMETERS"
WHERE "COACH_ID" = '5b4e9c1a-0c1d-4a55-9d3e-940400000002'
RETURNING "COACH_PARAMETER_ID";

-- 4. COACHES: coach B
DELETE FROM "COACHES"
WHERE "COACH_ID" = '5b4e9c1a-0c1d-4a55-9d3e-940400000002'
RETURNING "COACH_ID";

-- 5. USERS: coach B's user only (never the pre-existing accounts)
DELETE FROM "USERS"
WHERE "USER_ID" = '5b4e9c1a-0c1d-4a55-9d3e-940400000001'
RETURNING "USER_ID", "USER_EMAIL";

-- Counts inside the transaction (all fixture counts 0, EXERCISES back to 275)
SELECT
  (SELECT count(*) FROM "USERS" WHERE "USER_EMAIL" = 'coachb.lot94@mentora.fr') AS fixture_users,
  (SELECT count(*) FROM "COACHES" WHERE "COACH_ID" = '5b4e9c1a-0c1d-4a55-9d3e-940400000002') AS fixture_coaches,
  (SELECT count(*) FROM "EXERCISES" WHERE "EXERCISE_NAME" LIKE 'Lot94%') AS fixture_exercises,
  (SELECT count(*) FROM "EXERCISES") AS exercises_total,
  (SELECT count(*) FROM "EQUIPMENT") AS equipment_total,
  (SELECT count(*) FROM "USERS") AS users_total;

ROLLBACK;

-- After the ROLLBACK: nothing changed
SELECT
  (SELECT count(*) FROM "USERS" WHERE "USER_EMAIL" = 'coachb.lot94@mentora.fr') AS fixture_users,
  (SELECT count(*) FROM "EXERCISES" WHERE "EXERCISE_NAME" LIKE 'Lot94%') AS fixture_exercises,
  (SELECT count(*) FROM "EXERCISES") AS exercises_total;
