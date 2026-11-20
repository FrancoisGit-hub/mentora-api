-- Lot 14.2 precheck (READ ONLY). Run on PROD and api-demo before deploying the migration Lot14_2_OtpAttempts.
-- The migration adds AUTH_OTPS.AUTH_OTP_ATTEMPT_COUNT (integer NOT NULL default 0) and a CHECK (>= 0).
-- Existing rows get 0, so nothing can violate the CHECK; this only shows how many rows are touched.
-- Run it in a read-only transaction (BEGIN READ ONLY ... ROLLBACK is applied below).
BEGIN READ ONLY;

SELECT count(*) AS auth_otps_rows FROM "AUTH_OTPS";

SELECT count(*) AS active_codes
FROM "AUTH_OTPS"
WHERE NOT "AUTH_OTP_IS_USED" AND "AUTH_OTP_EXPIRATION_DATE" > now() AT TIME ZONE 'utc';

-- The column must not exist yet (0 before the migration, 1 after).
SELECT count(*) AS attempt_column_exists
FROM information_schema.columns
WHERE table_name = 'AUTH_OTPS' AND column_name = 'AUTH_OTP_ATTEMPT_COUNT';

ROLLBACK;
