-- Lot 14.2 precheck (READ ONLY). Run on PROD and api-demo before deploying the migration Lot14_2_OtpAttempts.
-- The whole file runs in a read-only transaction (BEGIN READ ONLY ... ROLLBACK). Counts only.
-- The migration adds AUTH_OTPS.AUTH_OTP_ATTEMPT_COUNT (integer NOT NULL default 0) and a CHECK (>= 0).
-- Existing rows get 0, so nothing can violate the CHECK; queries 1 and 2 only show how many rows are touched.
-- Query 0 returns the last applied migration. Expected before deployment: 20261117234001_Lot13_2_VoiceMessages
--   (anything else: stop). On a base already migrated to lot 14 it returns a later lot 14 migration.
-- Query 1 returns auth_otps_rows (informational), query 2 active_codes (informational).
-- Query 3 returns attempt_column_exists: 0 before the migration, 1 after.
BEGIN READ ONLY;

-- 0. Last applied migration (expected before deployment: 20261117234001_Lot13_2_VoiceMessages)
SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1;

-- 1. Rows in AUTH_OTPS (informational)
SELECT count(*) AS auth_otps_rows FROM "AUTH_OTPS";

-- 2. Unused, not expired codes (informational)
SELECT count(*) AS active_codes
FROM "AUTH_OTPS"
WHERE NOT "AUTH_OTP_IS_USED" AND "AUTH_OTP_EXPIRATION_DATE" > now() AT TIME ZONE 'utc';

-- 3. The column must not exist yet (0 before the migration, 1 after).
SELECT count(*) AS attempt_column_exists
FROM information_schema.columns
WHERE table_name = 'AUTH_OTPS' AND column_name = 'AUTH_OTP_ATTEMPT_COUNT';

ROLLBACK;
