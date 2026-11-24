-- Lot 14.9a: cleanup of the local l149a fixtures. LOCAL database only (mentora_db), never the VPS.
-- Deletes, in one transaction, only:
--   * the session templates of coach@mentora.fr named 'l149a %', created at or after the run start;
--   * the program template 'l149a template' of coach@mentora.fr, created at or after the run start;
--   * the training programs of member@mentora.fr created by this run (coach coach@mentora.fr, name
--     'l149a template', created at or after the run start) with their exercises, circuits, sessions, blocks.
-- Run start read from the database before the first fixture: 2026-11-24 22:24:46.569129+00.
-- Counts only, no row content. This copy ends with ROLLBACK (dry run).
BEGIN;

CREATE TEMP TABLE fx_run (run_start timestamptz) ON COMMIT DROP;
INSERT INTO fx_run VALUES ('2026-11-24 22:24:46.569129+00');

CREATE TEMP TABLE fx_coach ON COMMIT DROP AS
SELECT c."COACH_ID" AS coach_id
FROM "COACHES" c JOIN "USERS" u ON u."USER_ID" = c."USER_ID"
WHERE u."USER_EMAIL" = 'coach@mentora.fr';

CREATE TEMP TABLE fx_member ON COMMIT DROP AS
SELECT m."MEMBER_ID" AS member_id
FROM "MEMBERS" m JOIN "USERS" u ON u."USER_ID" = m."USER_ID"
WHERE u."USER_EMAIL" = 'member@mentora.fr';

CREATE TEMP TABLE fx_session_templates ON COMMIT DROP AS
SELECT s."SESSION_TEMPLATE_ID" AS id
FROM "SESSION_TEMPLATES" s, fx_run r
WHERE s."COACH_ID" IN (SELECT coach_id FROM fx_coach)
  AND s."SESSION_TEMPLATE_NAME" LIKE 'l149a %'
  AND s."SESSION_TEMPLATE_CREATED_DATE" >= r.run_start;

CREATE TEMP TABLE fx_program_templates ON COMMIT DROP AS
SELECT t."PROGRAM_TEMPLATE_ID" AS id
FROM "PROGRAM_TEMPLATES" t, fx_run r
WHERE t."PROGRAM_TEMPLATE_COACH_ID" IN (SELECT coach_id FROM fx_coach)
  AND t."PROGRAM_TEMPLATE_NAME" = 'l149a template'
  AND t."PROGRAM_TEMPLATE_CREATED_DATE" >= r.run_start;

CREATE TEMP TABLE fx_programs ON COMMIT DROP AS
SELECT p."PROGRAM_ID" AS id
FROM "PROGRAMS" p, fx_run r
WHERE p."PROGRAM_MEMBER_ID" IN (SELECT member_id FROM fx_member)
  AND p."PROGRAM_COACH_ID" IN (SELECT coach_id FROM fx_coach)
  AND p."PROGRAM_NAME" = 'l149a template'
  AND p."PROGRAM_CREATED_DATE" >= r.run_start;

SELECT 'TO DELETE' AS "step",
       (SELECT count(*) FROM fx_session_templates) AS "session_templates",
       (SELECT count(*) FROM fx_program_templates) AS "program_templates",
       (SELECT count(*) FROM fx_programs) AS "programs",
       (SELECT count(*) FROM "PROGRAM_BLOCKS" WHERE "PROGRAM_BLOCK_PROGRAM_ID" IN (SELECT id FROM fx_programs)) AS "blocks",
       (SELECT count(*) FROM "PROGRAM_SESSIONS" WHERE "PROGRAM_SESSION_PROGRAM_ID" IN (SELECT id FROM fx_programs)) AS "sessions",
       (SELECT count(*) FROM "PROGRAM_CIRCUITS" WHERE "PROGRAM_CIRCUIT_PROGRAM_ID" IN (SELECT id FROM fx_programs)) AS "circuits",
       (SELECT count(*) FROM "PROGRAM_EXERCISES" WHERE "PROGRAM_EXERCISE_PROGRAM_ID" IN (SELECT id FROM fx_programs)) AS "exercises";

DELETE FROM "PROGRAM_EXERCISES" WHERE "PROGRAM_EXERCISE_PROGRAM_ID" IN (SELECT id FROM fx_programs);
DELETE FROM "PROGRAM_CIRCUITS"  WHERE "PROGRAM_CIRCUIT_PROGRAM_ID"  IN (SELECT id FROM fx_programs);
DELETE FROM "PROGRAM_SESSIONS"  WHERE "PROGRAM_SESSION_PROGRAM_ID"  IN (SELECT id FROM fx_programs);
DELETE FROM "PROGRAM_BLOCKS"    WHERE "PROGRAM_BLOCK_PROGRAM_ID"    IN (SELECT id FROM fx_programs);
DELETE FROM "PROGRAMS"          WHERE "PROGRAM_ID"                  IN (SELECT id FROM fx_programs);
DELETE FROM "PROGRAM_TEMPLATES" WHERE "PROGRAM_TEMPLATE_ID"         IN (SELECT id FROM fx_program_templates);
DELETE FROM "SESSION_TEMPLATES" WHERE "SESSION_TEMPLATE_ID"         IN (SELECT id FROM fx_session_templates);

SELECT 'AFTER DELETE (inside the transaction)' AS "step",
       (SELECT count(*) FROM "SESSION_TEMPLATES") AS "session_templates_left",
       (SELECT count(*) FROM "PROGRAM_TEMPLATES") AS "program_templates_left",
       (SELECT count(*) FROM "PROGRAMS") AS "programs_left",
       (SELECT count(*) FROM "PROGRAM_BLOCKS") AS "blocks_left",
       (SELECT count(*) FROM "PROGRAM_SESSIONS") AS "sessions_left",
       (SELECT count(*) FROM "PROGRAM_CIRCUITS") AS "circuits_left",
       (SELECT count(*) FROM "PROGRAM_EXERCISES") AS "exercises_left",
       (SELECT count(*) FROM "PROGRAM_TEMPLATES" WHERE "PROGRAM_TEMPLATE_NAME" LIKE 'l149a%') AS "l149a_program_templates_left",
       (SELECT count(*) FROM "SESSION_TEMPLATES" WHERE "SESSION_TEMPLATE_NAME" LIKE 'l149a%') AS "l149a_session_templates_left";

COMMIT;
