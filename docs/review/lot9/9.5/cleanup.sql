-- Lot 9.5 - fixture cleanup (member M3 + the programs created in V3).
-- Usage:  psql -v end_tx=ROLLBACK -f cleanup.sql     (dry run)
--         psql -v end_tx=COMMIT   -f cleanup.sql     (real run, only after the dry run was approved)
--
-- Bounds (every delete uses them):
--   M3 USER_ID        = 5b4e9c1a-0c1d-4a55-9d3e-950500000001
--   M3 MEMBER_ID      = 5b4e9c1a-0c1d-4a55-9d3e-950500000002
--   MEMBER_COACH_ID   = 5b4e9c1a-0c1d-4a55-9d3e-950500000003
--   V3 program ids    = 2e89cf55-a572-466a-9b1b-c3c71aecdb0c, b4bedede-88f2-43bf-b6bf-70d8077d3d61
--   run start         = 2026-09-29 19:54:15 UTC (programs must be created at or after it)
-- Children are deleted bottom-up: RESTRICT foreign keys exist between blocks, sessions, circuits and exercises.
\set ON_ERROR_STOP on
BEGIN;

CREATE TEMP TABLE _v3_programs AS
SELECT "PROGRAM_ID" AS id FROM "PROGRAMS"
WHERE "PROGRAM_ID" IN ('2e89cf55-a572-466a-9b1b-c3c71aecdb0c', 'b4bedede-88f2-43bf-b6bf-70d8077d3d61')
  AND "PROGRAM_MEMBER_ID" = '5b4e9c1a-0c1d-4a55-9d3e-950500000002'
  AND "PROGRAM_CREATED_DATE" >= timestamptz '2026-09-29 19:54:15+00';

\echo '--- targets before'
SELECT (SELECT count(*) FROM _v3_programs) AS programs_selected,
       (SELECT count(*) FROM "PROGRAMS" WHERE "PROGRAM_MEMBER_ID" = '5b4e9c1a-0c1d-4a55-9d3e-950500000002') AS programs_of_m3,
       (SELECT count(*) FROM "PROGRAM_BLOCKS" WHERE "PROGRAM_BLOCK_PROGRAM_ID" IN (SELECT id FROM _v3_programs)) AS blocks,
       (SELECT count(*) FROM "PROGRAM_SESSIONS" WHERE "PROGRAM_SESSION_PROGRAM_ID" IN (SELECT id FROM _v3_programs)) AS sessions,
       (SELECT count(*) FROM "PROGRAM_CIRCUITS" WHERE "PROGRAM_CIRCUIT_PROGRAM_ID" IN (SELECT id FROM _v3_programs)) AS circuits,
       (SELECT count(*) FROM "PROGRAM_EXERCISES" WHERE "PROGRAM_EXERCISE_PROGRAM_ID" IN (SELECT id FROM _v3_programs)) AS exercises,
       (SELECT count(*) FROM "MEMBER_COACHES" WHERE "MEMBER_COACH_ID" = '5b4e9c1a-0c1d-4a55-9d3e-950500000003') AS member_coaches,
       (SELECT count(*) FROM "MEMBERS" WHERE "MEMBER_ID" = '5b4e9c1a-0c1d-4a55-9d3e-950500000002') AS members,
       (SELECT count(*) FROM "USERS" WHERE "USER_ID" = '5b4e9c1a-0c1d-4a55-9d3e-950500000001') AS users;

\echo '--- other rows hanging on M3 (should all be 0, they would cascade with the member)'
SELECT (SELECT count(*) FROM "CONVERSATIONS" WHERE "MEMBER_ID" = '5b4e9c1a-0c1d-4a55-9d3e-950500000002') AS conversations,
       (SELECT count(*) FROM "MEMBER_PARAMETERS" WHERE "MEMBER_ID" = '5b4e9c1a-0c1d-4a55-9d3e-950500000002') AS member_parameters,
       (SELECT count(*) FROM "AUTH_OTPS" WHERE "USER_ID" = '5b4e9c1a-0c1d-4a55-9d3e-950500000001') AS auth_otps,
       (SELECT count(*) FROM "AUTH_REFRESH_TOKENS" WHERE "USER_ID" = '5b4e9c1a-0c1d-4a55-9d3e-950500000001') AS auth_refresh_tokens;

\echo '--- deletes'
DELETE FROM "PROGRAM_EXERCISES" WHERE "PROGRAM_EXERCISE_PROGRAM_ID" IN (SELECT id FROM _v3_programs);
DELETE FROM "PROGRAM_CIRCUITS"  WHERE "PROGRAM_CIRCUIT_PROGRAM_ID"  IN (SELECT id FROM _v3_programs);
DELETE FROM "PROGRAM_SESSIONS"  WHERE "PROGRAM_SESSION_PROGRAM_ID"  IN (SELECT id FROM _v3_programs);
DELETE FROM "PROGRAM_BLOCKS"    WHERE "PROGRAM_BLOCK_PROGRAM_ID"    IN (SELECT id FROM _v3_programs) AND "PROGRAM_BLOCK_LEVEL" = 'MICROCYCLE';
DELETE FROM "PROGRAM_BLOCKS"    WHERE "PROGRAM_BLOCK_PROGRAM_ID"    IN (SELECT id FROM _v3_programs) AND "PROGRAM_BLOCK_LEVEL" = 'MESOCYCLE';
DELETE FROM "PROGRAM_BLOCKS"    WHERE "PROGRAM_BLOCK_PROGRAM_ID"    IN (SELECT id FROM _v3_programs) AND "PROGRAM_BLOCK_LEVEL" = 'MACROCYCLE';
DELETE FROM "PROGRAMS"          WHERE "PROGRAM_ID" IN (SELECT id FROM _v3_programs);
DELETE FROM "MEMBER_COACHES"    WHERE "MEMBER_COACH_ID" = '5b4e9c1a-0c1d-4a55-9d3e-950500000003';
DELETE FROM "MEMBERS"           WHERE "MEMBER_ID" = '5b4e9c1a-0c1d-4a55-9d3e-950500000002';
DELETE FROM "USERS"             WHERE "USER_ID" = '5b4e9c1a-0c1d-4a55-9d3e-950500000001';

\echo '--- targets after deletes (inside the transaction: all 0 expected)'
SELECT (SELECT count(*) FROM "PROGRAMS" WHERE "PROGRAM_MEMBER_ID" = '5b4e9c1a-0c1d-4a55-9d3e-950500000002') AS programs_of_m3,
       (SELECT count(*) FROM "PROGRAM_BLOCKS" WHERE "PROGRAM_BLOCK_PROGRAM_ID" IN (SELECT id FROM _v3_programs)) AS blocks,
       (SELECT count(*) FROM "PROGRAM_SESSIONS" WHERE "PROGRAM_SESSION_PROGRAM_ID" IN (SELECT id FROM _v3_programs)) AS sessions,
       (SELECT count(*) FROM "PROGRAM_CIRCUITS" WHERE "PROGRAM_CIRCUIT_PROGRAM_ID" IN (SELECT id FROM _v3_programs)) AS circuits,
       (SELECT count(*) FROM "PROGRAM_EXERCISES" WHERE "PROGRAM_EXERCISE_PROGRAM_ID" IN (SELECT id FROM _v3_programs)) AS exercises,
       (SELECT count(*) FROM "MEMBER_COACHES" WHERE "MEMBER_COACH_ID" = '5b4e9c1a-0c1d-4a55-9d3e-950500000003') AS member_coaches,
       (SELECT count(*) FROM "MEMBERS" WHERE "MEMBER_ID" = '5b4e9c1a-0c1d-4a55-9d3e-950500000002') AS members,
       (SELECT count(*) FROM "USERS" WHERE "USER_ID" = '5b4e9c1a-0c1d-4a55-9d3e-950500000001') AS users;

:end_tx;
