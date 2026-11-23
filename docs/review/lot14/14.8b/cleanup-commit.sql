-- Lot 14.8b cleanup. cleanup-dryrun.sql ends with ROLLBACK, cleanup-commit.sql ends with COMMIT, the two files are otherwise identical.
-- Scope: users whose email starts with l148b- AND created after the run start read in the database (2026-11-23 07:52:40+00).
\set ON_ERROR_STOP on
BEGIN;

\echo == 0. Before (1, 1, 1, 4, 1 expected)
SELECT
  (SELECT count(*) FROM "USERS" u WHERE lower(u."USER_EMAIL") LIKE 'l148b-%' AND u."USER_CREATED_DATE" >= '2026-11-23 07:52:40+00'::timestamptz) AS users,
  (SELECT count(*) FROM "COACHES" c JOIN "USERS" u ON u."USER_ID" = c."USER_ID" WHERE lower(u."USER_EMAIL") LIKE 'l148b-%' AND u."USER_CREATED_DATE" >= '2026-11-23 07:52:40+00'::timestamptz) AS coaches,
  (SELECT count(*) FROM "COACH_PARAMETERS" p JOIN "COACHES" c ON c."COACH_ID" = p."COACH_ID" JOIN "USERS" u ON u."USER_ID" = c."USER_ID" WHERE lower(u."USER_EMAIL") LIKE 'l148b-%' AND u."USER_CREATED_DATE" >= '2026-11-23 07:52:40+00'::timestamptz) AS coach_parameters,
  (SELECT count(*) FROM "OFFER_PROGRAMS" p JOIN "COACHES" c ON c."COACH_ID" = p."COACH_ID" JOIN "USERS" u ON u."USER_ID" = c."USER_ID" WHERE lower(u."USER_EMAIL") LIKE 'l148b-%' AND u."USER_CREATED_DATE" >= '2026-11-23 07:52:40+00'::timestamptz) AS offer_programs,
  (SELECT count(*) FROM "USERS") AS users_total;

\echo == 1. ADMIN_AUDIT_LOG rows about this coach (1 expected)
DELETE FROM "ADMIN_AUDIT_LOG"
WHERE "ADMIN_AUDIT_LOG_ENTITY_TYPE" = 'COACH'
  AND "ADMIN_AUDIT_LOG_ENTITY_ID" IN (
      SELECT c."COACH_ID"
      FROM "COACHES" c
      JOIN "USERS" u ON u."USER_ID" = c."USER_ID"
      WHERE lower(u."USER_EMAIL") LIKE 'l148b-%'
        AND u."USER_CREATED_DATE" >= '2026-11-23 07:52:40+00'::timestamptz);

\echo == 2. OFFER_PROGRAMS (4 expected)
DELETE FROM "OFFER_PROGRAMS"
WHERE "COACH_ID" IN (
      SELECT c."COACH_ID"
      FROM "COACHES" c
      JOIN "USERS" u ON u."USER_ID" = c."USER_ID"
      WHERE lower(u."USER_EMAIL") LIKE 'l148b-%'
        AND u."USER_CREATED_DATE" >= '2026-11-23 07:52:40+00'::timestamptz);

\echo == 3. COACH_PARAMETERS (1 expected)
DELETE FROM "COACH_PARAMETERS"
WHERE "COACH_ID" IN (
      SELECT c."COACH_ID"
      FROM "COACHES" c
      JOIN "USERS" u ON u."USER_ID" = c."USER_ID"
      WHERE lower(u."USER_EMAIL") LIKE 'l148b-%'
        AND u."USER_CREATED_DATE" >= '2026-11-23 07:52:40+00'::timestamptz);

\echo == 4. COACHES (1 expected)
DELETE FROM "COACHES"
WHERE "USER_ID" IN (
      SELECT u."USER_ID"
      FROM "USERS" u
      WHERE lower(u."USER_EMAIL") LIKE 'l148b-%'
        AND u."USER_CREATED_DATE" >= '2026-11-23 07:52:40+00'::timestamptz);

\echo == 5. USERS (1 expected)
DELETE FROM "USERS"
WHERE lower("USER_EMAIL") LIKE 'l148b-%'
  AND "USER_CREATED_DATE" >= '2026-11-23 07:52:40+00'::timestamptz;

\echo == 6. After (0, 0, 0, 0 expected, 2 users in total)
SELECT
  (SELECT count(*) FROM "USERS" u WHERE lower(u."USER_EMAIL") LIKE 'l148b-%') AS l148b_users_left,
  (SELECT count(*) FROM "ADMIN_AUDIT_LOG" WHERE "ADMIN_AUDIT_LOG_ACTION" = 'COACH_CREATED' AND "ADMIN_AUDIT_LOG_ENTITY_TYPE" = 'COACH' AND "ADMIN_AUDIT_LOG_CREATED_DATE" >= '2026-11-23 07:52:40+00'::timestamptz) AS audit_left,
  (SELECT count(*) FROM "USERS") AS users_total;

COMMIT;
