-- Lot 8.3 - VPS demo DB cleanup for fixture-vps.sql.
-- Run with: psql -1 -v ON_ERROR_STOP=1 -v run_start='2026-01-01T00:00:00Z' -f cleanup-vps.sql
-- (replace run_start with the actual UTC ISO timestamp recorded when fixture-vps.sql was run)
--
-- Every predicate is bounded by BOTH the fixed fixture UUID(s) AND a created-date predicate
-- >= :'run_start' where the table has a created-date column (double safety bound, same
-- convention as docs/review/lot8/cleanup-proposal.sql). Order respects FK dependencies
-- (children before parents).

\if :{?run_start}
\else
\echo 'ERROR: -v run_start=... is required'
\quit 1
\endif

\echo Using run_start = :'run_start'

-- 1. SESSIONS (the one fixture session) - expected 1
SELECT COUNT(*) FROM "SESSIONS"
WHERE "SESSION_ID" = '1f08159f-f09a-4eb2-b2a8-1234b527914e'
  AND "SESSION_CREATED_DATE" >= :'run_start';

DELETE FROM "SESSIONS"
WHERE "SESSION_ID" = '1f08159f-f09a-4eb2-b2a8-1234b527914e'
  AND "SESSION_CREATED_DATE" >= :'run_start';

-- 2. SESSION_VOUCHERS - expected 1
SELECT COUNT(*) FROM "SESSION_VOUCHERS"
WHERE "VOUCHER_ID" = '14f55ccc-60b7-47c3-9f9f-5b2e1b2a9f39'
  AND "VOUCHER_CREATED_DATE" >= :'run_start';

DELETE FROM "SESSION_VOUCHERS"
WHERE "VOUCHER_ID" = '14f55ccc-60b7-47c3-9f9f-5b2e1b2a9f39'
  AND "VOUCHER_CREATED_DATE" >= :'run_start';

-- 3. SESSION_SLOTS - expected 1
SELECT COUNT(*) FROM "SESSION_SLOTS"
WHERE "SESSION_SLOT_ID" = '2fb87a30-429e-410f-b52d-201a5790abeb'
  AND "SESSION_SLOT_CREATED_DATE" >= :'run_start';

DELETE FROM "SESSION_SLOTS"
WHERE "SESSION_SLOT_ID" = '2fb87a30-429e-410f-b52d-201a5790abeb'
  AND "SESSION_SLOT_CREATED_DATE" >= :'run_start';

-- 4. ORDER_ITEMS - expected 1
-- NOTE: OrderItem has no created-date column in this schema, so this predicate is bounded
-- by id only (still exact - single row, fixed PK).
SELECT COUNT(*) FROM "ORDER_ITEMS"
WHERE "ORDER_ITEM_ID" = '28ea9bba-81a0-4457-a671-2665235cba23';

DELETE FROM "ORDER_ITEMS"
WHERE "ORDER_ITEM_ID" = '28ea9bba-81a0-4457-a671-2665235cba23';

-- 5. ORDERS - expected 1
SELECT COUNT(*) FROM "ORDERS"
WHERE "ORDER_ID" = '54b27daa-c8f4-4f18-abb4-8c4267c29d5a'
  AND "ORDER_CREATED_DATE" >= :'run_start';

DELETE FROM "ORDERS"
WHERE "ORDER_ID" = '54b27daa-c8f4-4f18-abb4-8c4267c29d5a'
  AND "ORDER_CREATED_DATE" >= :'run_start';

-- 6. AUTH_REFRESH_TOKENS for admin@web2success.fr and coach2@mentorapp.fr
SELECT COUNT(*) FROM "AUTH_REFRESH_TOKENS"
WHERE "USER_ID" IN (
    SELECT "USER_ID" FROM "USERS" WHERE "USER_EMAIL" IN ('admin@web2success.fr', 'coach2@mentorapp.fr')
)
  AND "AUTH_REFRESH_TOKEN_CREATED_DATE" >= :'run_start';

DELETE FROM "AUTH_REFRESH_TOKENS"
WHERE "USER_ID" IN (
    SELECT "USER_ID" FROM "USERS" WHERE "USER_EMAIL" IN ('admin@web2success.fr', 'coach2@mentorapp.fr')
)
  AND "AUTH_REFRESH_TOKEN_CREATED_DATE" >= :'run_start';

-- 7. AUTH_OTPS for admin@web2success.fr and coach2@mentorapp.fr
SELECT COUNT(*) FROM "AUTH_OTPS"
WHERE "USER_ID" IN (
    SELECT "USER_ID" FROM "USERS" WHERE "USER_EMAIL" IN ('admin@web2success.fr', 'coach2@mentorapp.fr')
)
  AND "AUTH_OTP_CREATED_DATE" >= :'run_start';

DELETE FROM "AUTH_OTPS"
WHERE "USER_ID" IN (
    SELECT "USER_ID" FROM "USERS" WHERE "USER_EMAIL" IN ('admin@web2success.fr', 'coach2@mentorapp.fr')
)
  AND "AUTH_OTP_CREATED_DATE" >= :'run_start';
