-- Lot 14.8a cleanup, DRY RUN (ends with ROLLBACK). The commit file is identical except for the last line.
-- Scope: bounded by the run start read in the database before the first write (2026-11-23 04:40:28.649562+00).
\set ON_ERROR_STOP on
BEGIN;

\echo == 1. ADMIN_AUDIT_LOG rows about the coaches of the l148 users created after the start
DELETE FROM "ADMIN_AUDIT_LOG"
WHERE "ADMIN_AUDIT_LOG_ENTITY_TYPE" = 'COACH'
  AND "ADMIN_AUDIT_LOG_ENTITY_ID" IN (
      SELECT c."COACH_ID"
      FROM "COACHES" c
      JOIN "USERS" u ON u."USER_ID" = c."USER_ID"
      WHERE lower(u."USER_EMAIL") LIKE 'l148%'
        AND u."USER_CREATED_DATE" >= '2026-11-23 04:40:28.649562+00'::timestamptz);

\echo == 2. l148 USERS created after the start (cascades: COACHES, COACH_PARAMETERS, OFFER_PROGRAMS, MEMBERS, MEMBER_COACHES, AUTH_*)
DELETE FROM "USERS"
WHERE lower("USER_EMAIL") LIKE 'l148%'
  AND "USER_CREATED_DATE" >= '2026-11-23 04:40:28.649562+00'::timestamptz;

\echo == 3. AUTH_OTPS and AUTH_REFRESH_TOKENS of coach@mentora.fr created after the start
DELETE FROM "AUTH_OTPS"
WHERE "AUTH_OTP_CREATED_DATE" >= '2026-11-23 04:40:28.649562+00'::timestamptz
  AND "USER_ID" IN (SELECT "USER_ID" FROM "USERS" WHERE lower("USER_EMAIL") = 'coach@mentora.fr');
DELETE FROM "AUTH_REFRESH_TOKENS"
WHERE "AUTH_REFRESH_TOKEN_CREATED_DATE" >= '2026-11-23 04:40:28.649562+00'::timestamptz
  AND "USER_ID" IN (SELECT "USER_ID" FROM "USERS" WHERE lower("USER_EMAIL") = 'coach@mentora.fr');

\echo == 4. ADMIN_OTPS and ADMIN_REFRESH_TOKENS created after the start
DELETE FROM "ADMIN_OTPS"
WHERE "ADMIN_OTP_CREATED_DATE" >= '2026-11-23 04:40:28.649562+00'::timestamptz;
DELETE FROM "ADMIN_REFRESH_TOKENS"
WHERE "ADMIN_REFRESH_TOKEN_CREATED_DATE" >= '2026-11-23 04:40:28.649562+00'::timestamptz;

\echo == 5. Leftovers created after the start, every table with a *_CREATED_DATE column (0 expected)
SELECT string_agg(
         format('SELECT %L AS tbl, count(*) AS n FROM %I WHERE %I >= %L::timestamptz',
                table_name, table_name, column_name, '2026-11-23 04:40:28.649562+00'),
         ' UNION ALL ') AS q
FROM information_schema.columns
WHERE table_schema = 'public' AND column_name LIKE '%\_CREATED\_DATE' \gset
SELECT count(DISTINCT table_name) AS tables_scanned
FROM information_schema.columns
WHERE table_schema = 'public' AND column_name LIKE '%\_CREATED\_DATE';
SELECT coalesce(sum(n), 0) AS leftovers_total FROM (:q) x;
SELECT * FROM (:q) x WHERE n > 0 ORDER BY tbl;
SELECT count(*) AS users_left FROM "USERS";
SELECT count(*) AS l148_users_left FROM "USERS" WHERE lower("USER_EMAIL") LIKE 'l148%';

ROLLBACK;
