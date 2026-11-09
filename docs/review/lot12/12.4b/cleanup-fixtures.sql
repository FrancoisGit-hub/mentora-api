-- Lot 12.4b: removal of the browser test fixtures (exercises named 'l124b %' created after the run start date).
-- Deletes are bounded by the fixture ids (temporary table) AND by the run start date.
BEGIN;

CREATE TEMP TABLE fx ON COMMIT DROP AS
SELECT "EXERCISE_ID" AS id, "EXERCISE_SOURCE_CODE" AS code, "EXERCISE_NAME" AS name
FROM "EXERCISES"
WHERE "EXERCISE_COACH_ID" IS NULL
  AND "EXERCISE_NAME" ILIKE 'l124b %'
  AND "EXERCISE_CREATED_DATE" >= TIMESTAMPTZ '2026-11-09 21:14:57.33+00';

SELECT code, name FROM fx ORDER BY code;

DO $$
DECLARE
  n_fx int; n_audit int; n_obj int; n_sec int; n_ex int;
BEGIN
  SELECT count(*) INTO n_fx FROM fx;
  IF n_fx <> 1 THEN
    RAISE EXCEPTION 'expected exactly 1 fixture exercise, found %', n_fx;
  END IF;

  DELETE FROM "ADMIN_AUDIT_LOG"
  WHERE "ADMIN_AUDIT_LOG_ENTITY_ID" IN (SELECT id FROM fx)
    AND "ADMIN_AUDIT_LOG_CREATED_DATE" >= TIMESTAMPTZ '2026-11-09 21:14:57.33+00';
  GET DIAGNOSTICS n_audit = ROW_COUNT;

  DELETE FROM "EXERCISE_OBJECTIVES"
  WHERE "EXERCISE_OBJECTIVE_EXERCISE_ID" IN (SELECT id FROM fx);
  GET DIAGNOSTICS n_obj = ROW_COUNT;

  DELETE FROM "EXERCISE_SECONDARY_MUSCLE_GROUPS"
  WHERE "EXERCISE_SECONDARY_MUSCLE_GROUP_EXERCISE_ID" IN (SELECT id FROM fx);
  GET DIAGNOSTICS n_sec = ROW_COUNT;

  DELETE FROM "EXERCISES"
  WHERE "EXERCISE_ID" IN (SELECT id FROM fx)
    AND "EXERCISE_CREATED_DATE" >= TIMESTAMPTZ '2026-11-09 21:14:57.33+00';
  GET DIAGNOSTICS n_ex = ROW_COUNT;

  RAISE NOTICE 'deleted: audit % | objective links % | secondary links % | exercises %', n_audit, n_obj, n_sec, n_ex;
  IF n_ex <> 1 THEN
    RAISE EXCEPTION 'expected exactly 1 exercise deleted, deleted %', n_ex;
  END IF;
END $$;

SELECT
  (SELECT count(*) FROM "EXERCISES" WHERE "EXERCISE_COACH_ID" IS NULL) AS mentora_exercises,
  (SELECT count(*) FROM "EXERCISES" WHERE "EXERCISE_CREATED_DATE" >= TIMESTAMPTZ '2026-11-09 21:14:57.33+00') AS exercises_after_start,
  (SELECT count(*) FROM "EXERCISES" WHERE "EXERCISE_NAME" ILIKE 'l124b %') AS fixtures_left,
  (SELECT count(*) FROM "ADMIN_AUDIT_LOG" WHERE "ADMIN_AUDIT_LOG_CREATED_DATE" >= TIMESTAMPTZ '2026-11-09 21:14:57.33+00') AS audit_after_start,
  (SELECT count(*) FROM "ADMIN_AUDIT_LOG") AS audit_total,
  (SELECT count(*) FROM "EXERCISE_OBJECTIVES") AS objective_links,
  (SELECT count(*) FROM "EXERCISE_SECONDARY_MUSCLE_GROUPS") AS secondary_links;

ROLLBACK;
