-- Lot 14 review wave 1 fixes cleanup (local DB only). Run start (read in the database before the first write): 2026-11-21 09:36:53 UTC.
-- Scope: USERS whose email starts with l14f AND created after the run start (the member created through the coach route);
-- their MEMBERS, MEMBER_COACHES, AUTH_* rows go by cascade (counted below before the delete).
-- Plus the OTPs and refresh tokens created after the run start by the pre-existing users (coach and member).
BEGIN;

\echo '--- before: rows in scope'
SELECT count(*) AS l14f_users FROM "USERS" WHERE "USER_EMAIL" LIKE 'l14f%' AND "USER_CREATED_DATE" > '2026-11-21 09:36:53';
SELECT count(*) AS l14f_members FROM "MEMBERS"
WHERE "USER_ID" IN (SELECT "USER_ID" FROM "USERS" WHERE "USER_EMAIL" LIKE 'l14f%' AND "USER_CREATED_DATE" > '2026-11-21 09:36:53');
SELECT count(*) AS l14f_member_coaches FROM "MEMBER_COACHES"
WHERE "MEMBER_ID" IN (SELECT m."MEMBER_ID" FROM "MEMBERS" m JOIN "USERS" u ON u."USER_ID" = m."USER_ID"
                      WHERE u."USER_EMAIL" LIKE 'l14f%' AND u."USER_CREATED_DATE" > '2026-11-21 09:36:53');
SELECT count(*) AS l14f_otps FROM "AUTH_OTPS"
WHERE "USER_ID" IN (SELECT "USER_ID" FROM "USERS" WHERE "USER_EMAIL" LIKE 'l14f%' AND "USER_CREATED_DATE" > '2026-11-21 09:36:53');
SELECT count(*) AS l14f_refresh_tokens FROM "AUTH_REFRESH_TOKENS"
WHERE "USER_ID" IN (SELECT "USER_ID" FROM "USERS" WHERE "USER_EMAIL" LIKE 'l14f%' AND "USER_CREATED_DATE" > '2026-11-21 09:36:53');
SELECT count(*) AS preexisting_users_kept FROM "USERS" WHERE "USER_EMAIL" NOT LIKE 'l14f%';
SELECT count(*) AS otps_preexisting_users FROM "AUTH_OTPS"
WHERE "AUTH_OTP_CREATED_DATE" > '2026-11-21 09:36:53'
  AND "USER_ID" IN (SELECT "USER_ID" FROM "USERS" WHERE "USER_EMAIL" IN ('coach@mentora.fr', 'member@mentora.fr'));
SELECT count(*) AS refresh_tokens_preexisting_users FROM "AUTH_REFRESH_TOKENS"
WHERE "AUTH_REFRESH_TOKEN_CREATED_DATE" > '2026-11-21 09:36:53'
  AND "USER_ID" IN (SELECT "USER_ID" FROM "USERS" WHERE "USER_EMAIL" IN ('coach@mentora.fr', 'member@mentora.fr'));

DELETE FROM "AUTH_OTPS"
WHERE "AUTH_OTP_CREATED_DATE" > '2026-11-21 09:36:53'
  AND "USER_ID" IN (SELECT "USER_ID" FROM "USERS" WHERE "USER_EMAIL" IN ('coach@mentora.fr', 'member@mentora.fr'));

DELETE FROM "AUTH_REFRESH_TOKENS"
WHERE "AUTH_REFRESH_TOKEN_CREATED_DATE" > '2026-11-21 09:36:53'
  AND "USER_ID" IN (SELECT "USER_ID" FROM "USERS" WHERE "USER_EMAIL" IN ('coach@mentora.fr', 'member@mentora.fr'));

DELETE FROM "USERS" WHERE "USER_EMAIL" LIKE 'l14f%' AND "USER_CREATED_DATE" > '2026-11-21 09:36:53';

\echo '--- after: leftovers per table created after the run start (expected: no row)'
SELECT table_name, n FROM (
  SELECT table_name,
         (xpath('/row/c/text()', query_to_xml(format('select count(*) as c from %I where %I > %L', table_name, column_name, '2026-11-21 09:36:53'), false, true, '')))[1]::text::int AS n
  FROM information_schema.columns
  WHERE table_schema = 'public' AND column_name LIKE '%\_CREATED\_DATE' ESCAPE '\'
) t WHERE n > 0 ORDER BY 1;
SELECT count(*) AS l14f_users_after FROM "USERS" WHERE "USER_EMAIL" LIKE 'l14f%';
SELECT count(*) AS member_coaches_total_after FROM "MEMBER_COACHES";
SELECT count(*) AS users_left_total FROM "USERS";

ROLLBACK;
