-- Lot 9.1 - fixture cleanup DRY RUN (BEGIN ... ROLLBACK). Deletes only rows created by this
-- run: bounded by the fixed fixture UUIDs, plus AUTH_OTPS/AUTH_REFRESH_TOKENS for the four
-- test users with CREATED_DATE >= run start (2026-09-29T16:08:19Z).
-- Existing accounts (coach@mentora.fr / member@mentora.fr) themselves are NEVER deleted -
-- only their login-artifact rows created during this run.

BEGIN;

-- 1. SESSIONS (V9 booking) - delete first: nulls SESSION_VOUCHERS.VOUCHER_RESERVED_SESSION_ID via SetNull
DELETE FROM "SESSIONS"
WHERE "SESSION_ID" = '0425c28f-d9bc-4d21-b2b3-0a7950cd2b85'
RETURNING "SESSION_ID";

-- 2. SESSION_VOUCHERS - V1's 3 granted vouchers + V10's vouchers (via their order items)
DELETE FROM "SESSION_VOUCHERS"
WHERE "VOUCHER_ID" IN ('9b6ad063-0731-4bf3-a724-e582a99b7c57','1023a2bf-9deb-46cf-8422-d24982d7e964','c04cbe7a-1850-4ad8-adcd-9547572699e6')
   OR "VOUCHER_ORDER_ITEM_ID" IN ('1b9465c4-5b87-4d29-aadb-0fde7533abd4','b888f349-e632-4c1a-afd3-6d6f48734ebb')
RETURNING "VOUCHER_ID";

-- 3. SESSION_SLOTS (V9 fixture slot)
DELETE FROM "SESSION_SLOTS"
WHERE "SESSION_SLOT_ID" = '089f79ca-6ad9-4732-9863-41b0ef57f9ae'
RETURNING "SESSION_SLOT_ID";

-- 4. ORDER_ITEMS (V1 grant + V10 purchase)
DELETE FROM "ORDER_ITEMS"
WHERE "ORDER_ITEM_ID" IN ('1b9465c4-5b87-4d29-aadb-0fde7533abd4','b888f349-e632-4c1a-afd3-6d6f48734ebb')
RETURNING "ORDER_ITEM_ID";

-- 5. ORDERS (V1 grant + V10 purchase)
DELETE FROM "ORDERS"
WHERE "ORDER_ID" IN ('ee57a8c5-998a-4147-bdf8-dd2f17e6ccce','b39c64bb-81f7-4303-86bf-e18d8dbc5a28')
RETURNING "ORDER_ID";

-- 6. PRODUCTS (PA1, PB1, PA2-ineligible, PA3-published)
DELETE FROM "PRODUCTS"
WHERE "PRODUCT_ID" IN ('eb56b9b5-a9c3-4082-a858-357d6284d595','c8821d7f-d9d5-4f25-a666-fcee2562a0cf','ca2796f7-10d6-4497-b9e2-9a9359c87751','5d0adc24-dbf7-481c-b147-7361953365f0')
RETURNING "PRODUCT_ID";

-- 7. OFFER_PROGRAMS (auto-seeded for coach B on first startup after fixture-01)
DELETE FROM "OFFER_PROGRAMS"
WHERE "COACH_ID" = 'c0a01743-e580-4597-a834-54011da05a17'
RETURNING "OFFER_PROGRAM_ID";

-- 8. COACH_PARAMETERS (auto-seeded for coach B)
DELETE FROM "COACH_PARAMETERS"
WHERE "COACH_ID" = 'c0a01743-e580-4597-a834-54011da05a17'
RETURNING "COACH_PARAMETER_ID";

-- 9. AUTH_OTPS - login artifacts for all 4 test actors, this run only
DELETE FROM "AUTH_OTPS"
WHERE "USER_ID" IN (SELECT "USER_ID" FROM "USERS" WHERE "USER_EMAIL" IN ('coach@mentora.fr','coachb.lot9@mentora.fr','member@mentora.fr','memberb.lot9@mentora.fr'))
  AND "AUTH_OTP_CREATED_DATE" >= '2026-09-29T16:08:19Z'
RETURNING "AUTH_OTP_ID";

-- 10. AUTH_REFRESH_TOKENS - login artifacts for all 4 test actors, this run only
DELETE FROM "AUTH_REFRESH_TOKENS"
WHERE "USER_ID" IN (SELECT "USER_ID" FROM "USERS" WHERE "USER_EMAIL" IN ('coach@mentora.fr','coachb.lot9@mentora.fr','member@mentora.fr','memberb.lot9@mentora.fr'))
  AND "AUTH_REFRESH_TOKEN_CREATED_DATE" >= '2026-09-29T16:08:19Z'
RETURNING "AUTH_REFRESH_TOKEN_ID";

-- 11. COACHES (coach B)
DELETE FROM "COACHES"
WHERE "COACH_ID" = 'c0a01743-e580-4597-a834-54011da05a17'
RETURNING "COACH_ID";

-- 12. MEMBERS (member M2)
DELETE FROM "MEMBERS"
WHERE "MEMBER_ID" = 'ef9296e7-1f7b-47ec-b97e-7dbf7c5d2495'
RETURNING "MEMBER_ID";

-- 13. USERS (coach B's user + member M2's user only - NEVER the pre-existing accounts)
DELETE FROM "USERS"
WHERE "USER_ID" IN ('1bbac370-5329-4ff9-a9dc-7170eb419104','de53e500-f220-42fd-a7d7-7f34ad55d480')
RETURNING "USER_ID", "USER_EMAIL";

-- Counts check (should all be 0 inside this still-open transaction after the deletes above)
SELECT
  (SELECT count(*) FROM "USERS" WHERE "USER_EMAIL" IN ('coachb.lot9@mentora.fr','memberb.lot9@mentora.fr')) AS remaining_fixture_users,
  (SELECT count(*) FROM "COACHES" WHERE "COACH_ID" = 'c0a01743-e580-4597-a834-54011da05a17') AS remaining_coach_b,
  (SELECT count(*) FROM "PRODUCTS" WHERE "COACH_ID" IN ('c0a01743-e580-4597-a834-54011da05a17','e268a512-84d6-4e27-b5c0-1d6c08a47190') AND "PRODUCT_NAME" LIKE 'Lot9%') AS remaining_products;

ROLLBACK;
