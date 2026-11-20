-- Lot 14.1 cleanup (local DB only). Run start (read in the database before the first write): 2026-11-19 21:46:18 UTC.
-- Scope: USERS whose email starts with l141 AND created after the run start (fixture users and users created by the
-- runs); their MEMBERS, COACHES, MEMBER_COACHES, COACH_PARAMETERS, OFFER_PROGRAMS, AUTH_* rows go by cascade.
-- Plus the OTPs and refresh tokens created after the run start by the two pre-existing users (coach and member).
BEGIN;

\echo '--- before: rows in scope'
SELECT count(*) AS l141_users FROM "USERS" WHERE "USER_EMAIL" LIKE 'l141%' AND "USER_CREATED_DATE" > '2026-11-19 21:46:18';
SELECT count(*) AS preexisting_users_kept FROM "USERS" WHERE "USER_EMAIL" NOT LIKE 'l141%';

DELETE FROM "AUTH_OTPS"
WHERE "AUTH_OTP_CREATED_DATE" > '2026-11-19 21:46:18'
  AND "USER_ID" IN (SELECT "USER_ID" FROM "USERS" WHERE "USER_EMAIL" IN ('coach@mentora.fr', 'member@mentora.fr'));

DELETE FROM "AUTH_REFRESH_TOKENS"
WHERE "AUTH_REFRESH_TOKEN_CREATED_DATE" > '2026-11-19 21:46:18'
  AND "USER_ID" IN (SELECT "USER_ID" FROM "USERS" WHERE "USER_EMAIL" IN ('coach@mentora.fr', 'member@mentora.fr'));

DELETE FROM "USERS" WHERE "USER_EMAIL" LIKE 'l141%' AND "USER_CREATED_DATE" > '2026-11-19 21:46:18';

\echo '--- after: leftovers per table created after the run start (expected: no row)'
SELECT table_name, n FROM (
  SELECT table_name,
         (xpath('/row/c/text()', query_to_xml(format('select count(*) as c from %I where %I > %L', table_name, column_name, '2026-11-19 21:46:18'), false, true, '')))[1]::text::int AS n
  FROM information_schema.columns
  WHERE table_schema = 'public' AND column_name LIKE '%\_CREATED\_DATE' ESCAPE '\'
) t WHERE n > 0 ORDER BY 1;
SELECT count(*) AS member_coaches_total_after FROM "MEMBER_COACHES";
SELECT count(*) AS users_left_total FROM "USERS";

ROLLBACK;
