-- Lot 14.1 precheck (READ ONLY). Run on PROD and api-demo BEFORE deploying migration Lot14_1_UserEmailCi.
-- The whole file runs in a read-only transaction (BEGIN READ ONLY ... ROLLBACK). No query prints a stored email.
-- Query 0 returns the last applied migration. Expected before deployment: 20261117234001_Lot13_2_VoiceMessages
--   (anything else: stop). On a base already migrated to lot 14 it returns a later lot 14 migration.
-- Query 1 returns one count, case_collisions, which must be 0: the unique index UX_USERS_EMAIL_CI on
--   lower("USER_EMAIL") cannot be created while two USERS rows differ only by case (the migration would
--   fail and the API would not start). Not 0: stop the deployment and run query 1b.
-- Query 1b lists only the USER_ID values of the colliding rows. It is commented out: run it only if
--   query 1 is not 0.
-- Query 2 returns one count, emails_with_spaces, expected 0 (informational): an email stored with a leading
--   or trailing space would never match a login, because every lookup now compares lower("USER_EMAIL")
--   with the trimmed, lowercased value.
BEGIN READ ONLY;

-- 0. Last applied migration (expected before deployment: 20261117234001_Lot13_2_VoiceMessages)
SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1;

-- 1. Emails duplicated case-insensitively (expected: 0)
SELECT count(*) AS case_collisions
FROM (SELECT lower("USER_EMAIL") FROM "USERS" GROUP BY 1 HAVING count(*) > 1) t;

-- 1b. Only if query 1 is not 0: USER_ID of the colliding rows (no email value printed)
-- SELECT "USER_ID"
-- FROM "USERS"
-- WHERE lower("USER_EMAIL") IN (SELECT lower("USER_EMAIL") FROM "USERS" GROUP BY 1 HAVING count(*) > 1)
-- ORDER BY lower("USER_EMAIL"), "USER_ID";

-- 2. Emails with surrounding whitespace (expected: 0)
SELECT count(*) AS emails_with_spaces
FROM "USERS"
WHERE "USER_EMAIL" <> btrim("USER_EMAIL");

ROLLBACK;
