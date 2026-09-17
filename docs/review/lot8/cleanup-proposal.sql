-- Lot 8.2 cleanup proposal - NOT EXECUTED. Awaiting explicit confirmation.
-- Run start (UTC): 2026-09-17T15:09:24Z
-- Every predicate is bounded by BOTH the exact fixture id(s)/user id(s) from
-- fixtures-manifest.txt (or the two test users) AND a created-date predicate >= run start,
-- as a double safety bound. Order respects FK dependencies (children before parents).
--
-- Correction vs. the first draft: Lot 7 DID clean its own AUTH_OTPS/AUTH_REFRESH_TOKENS test
-- residue (commit bf54aba) - that precedent was misquoted before. Steps 7-8 below add the
-- matching cleanup for the two test accounts used here (coach@mentora.fr, member@mentora.fr).
--
-- Verification: every SELECT COUNT(*) below uses the EXACT SAME WHERE clause as the DELETE
-- that follows it, run against the live DB before any deletion. Actual counts obtained:
--   SESSION_PARTICIPANTS=1  SESSIONS=7  SESSION_VOUCHERS=6  SESSION_SLOTS=1
--   ORDER_ITEMS=1  ORDERS=2  AUTH_REFRESH_TOKENS=2  AUTH_OTPS=2
-- All match the expected counts exactly.

-- 1. SESSION_PARTICIPANTS (F3's participant row) - expected 1
SELECT COUNT(*) FROM "SESSION_PARTICIPANTS"
WHERE "SESSION_PARTICIPANT_ID" = 'ce588a1f-3061-436f-b7d4-7e437b3e01db'
  AND "SESSION_PARTICIPANT_CREATED_DATE" >= '2026-09-17T15:09:24Z';

DELETE FROM "SESSION_PARTICIPANTS"
WHERE "SESSION_PARTICIPANT_ID" = 'ce588a1f-3061-436f-b7d4-7e437b3e01db'
  AND "SESSION_PARTICIPANT_CREATED_DATE" >= '2026-09-17T15:09:24Z';

-- 2. SESSIONS (F1-F7) - expected 7
SELECT COUNT(*) FROM "SESSIONS"
WHERE "SESSION_ID" IN (
    'cbf19245-783b-4a9d-99fb-e4afe876a917', -- F1
    'a1d7bb16-3c1a-48dc-98d9-6e89870ba5ea', -- F2
    '6a86b6d8-95c8-411c-904d-e11d3339737c', -- F3
    '6482583f-872b-46ac-8fe6-fefee1f63d64', -- F4
    '4a0e28cf-1db3-4ea4-ad88-91d5ee6d850e', -- F5
    '140ede3a-73ab-42c4-933a-ab89c1a69ac0', -- F6
    '13b610ca-6c6d-47d3-ad4d-c4c799cd8195'  -- F7
)
  AND "SESSION_CREATED_DATE" >= '2026-09-17T15:09:24Z';

DELETE FROM "SESSIONS"
WHERE "SESSION_ID" IN (
    'cbf19245-783b-4a9d-99fb-e4afe876a917', -- F1
    'a1d7bb16-3c1a-48dc-98d9-6e89870ba5ea', -- F2
    '6a86b6d8-95c8-411c-904d-e11d3339737c', -- F3
    '6482583f-872b-46ac-8fe6-fefee1f63d64', -- F4
    '4a0e28cf-1db3-4ea4-ad88-91d5ee6d850e', -- F5
    '140ede3a-73ab-42c4-933a-ab89c1a69ac0', -- F6
    '13b610ca-6c6d-47d3-ad4d-c4c799cd8195'  -- F7
)
  AND "SESSION_CREATED_DATE" >= '2026-09-17T15:09:24Z';

-- 3. SESSION_VOUCHERS (6 vouchers backing F1,F2,F4,F5,F6,F7) - expected 6
SELECT COUNT(*) FROM "SESSION_VOUCHERS"
WHERE "VOUCHER_ID" IN (
    '243468a3-7cd6-40f1-ba77-ca43ca9f4a66', -- VOUCHER_F1
    '486e2f9e-28a2-473f-a01a-c2839e28cdf4', -- VOUCHER_F2
    '806cbc95-c7f3-4c47-afe3-be5e8e13683d', -- VOUCHER_F4
    '79b57ab8-e624-49b6-8252-efb60072ae21', -- VOUCHER_F5
    '90f2fcb3-6bf2-4191-b31c-9f980995a433', -- VOUCHER_F6
    'c1a11931-cb81-4828-9ab2-8cf3ab6de104'  -- VOUCHER_F7
)
  AND "VOUCHER_CREATED_DATE" >= '2026-09-17T15:09:24Z';

DELETE FROM "SESSION_VOUCHERS"
WHERE "VOUCHER_ID" IN (
    '243468a3-7cd6-40f1-ba77-ca43ca9f4a66', -- VOUCHER_F1
    '486e2f9e-28a2-473f-a01a-c2839e28cdf4', -- VOUCHER_F2
    '806cbc95-c7f3-4c47-afe3-be5e8e13683d', -- VOUCHER_F4
    '79b57ab8-e624-49b6-8252-efb60072ae21', -- VOUCHER_F5
    '90f2fcb3-6bf2-4191-b31c-9f980995a433', -- VOUCHER_F6
    'c1a11931-cb81-4828-9ab2-8cf3ab6de104'  -- VOUCHER_F7
)
  AND "VOUCHER_CREATED_DATE" >= '2026-09-17T15:09:24Z';

-- 4. SESSION_SLOTS (SLOT1, reused by all 7 sessions) - expected 1
SELECT COUNT(*) FROM "SESSION_SLOTS"
WHERE "SESSION_SLOT_ID" = '338c8c87-e3ff-4828-b2d4-7dbe55fae18b'
  AND "SESSION_SLOT_CREATED_DATE" >= '2026-09-17T15:09:24Z';

DELETE FROM "SESSION_SLOTS"
WHERE "SESSION_SLOT_ID" = '338c8c87-e3ff-4828-b2d4-7dbe55fae18b'
  AND "SESSION_SLOT_CREATED_DATE" >= '2026-09-17T15:09:24Z';

-- 5. ORDER_ITEMS (OI1, reused by all 6 vouchers) - expected 1
-- NOTE: OrderItem has no created-date column in this schema, so this predicate is bounded
-- by id only (still exact - single row, id is a random gen_random_uuid() PK).
SELECT COUNT(*) FROM "ORDER_ITEMS"
WHERE "ORDER_ITEM_ID" = '69a06cbc-1f08-4f90-8a7e-122429cdc56e';

DELETE FROM "ORDER_ITEMS"
WHERE "ORDER_ITEM_ID" = '69a06cbc-1f08-4f90-8a7e-122429cdc56e';

-- 6. ORDERS (ORDER1 + the stray duplicate created by an accidental diagnostic re-run) - expected 2
SELECT COUNT(*) FROM "ORDERS"
WHERE "ORDER_ID" IN (
    '59cc97c8-662b-4823-a484-66794767c0c1', -- ORDER1 (used)
    '0b6d3ee4-8595-4d6e-8e45-38089e8a8908'  -- stray duplicate (unused, created by diagnostic)
)
  AND "ORDER_CREATED_DATE" >= '2026-09-17T15:09:24Z';

DELETE FROM "ORDERS"
WHERE "ORDER_ID" IN (
    '59cc97c8-662b-4823-a484-66794767c0c1', -- ORDER1 (used)
    '0b6d3ee4-8595-4d6e-8e45-38089e8a8908'  -- stray duplicate (unused, created by diagnostic)
)
  AND "ORDER_CREATED_DATE" >= '2026-09-17T15:09:24Z';

-- 7. AUTH_REFRESH_TOKENS for the two test accounts (coach@mentora.fr, member@mentora.fr) - expected 2
-- USER_ID resolved from USERS: coach@mentora.fr = ff2be73b-8a80-46d6-90d8-cd462b876095,
-- member@mentora.fr = 874d7734-cc4f-47ee-9527-3a4c0262f7b3.
SELECT COUNT(*) FROM "AUTH_REFRESH_TOKENS"
WHERE "USER_ID" IN ('ff2be73b-8a80-46d6-90d8-cd462b876095', '874d7734-cc4f-47ee-9527-3a4c0262f7b3')
  AND "AUTH_REFRESH_TOKEN_CREATED_DATE" >= '2026-09-17T15:09:24Z';

DELETE FROM "AUTH_REFRESH_TOKENS"
WHERE "USER_ID" IN ('ff2be73b-8a80-46d6-90d8-cd462b876095', '874d7734-cc4f-47ee-9527-3a4c0262f7b3')
  AND "AUTH_REFRESH_TOKEN_CREATED_DATE" >= '2026-09-17T15:09:24Z';

-- 8. AUTH_OTPS for the two test accounts - expected 2
SELECT COUNT(*) FROM "AUTH_OTPS"
WHERE "USER_ID" IN ('ff2be73b-8a80-46d6-90d8-cd462b876095', '874d7734-cc4f-47ee-9527-3a4c0262f7b3')
  AND "AUTH_OTP_CREATED_DATE" >= '2026-09-17T15:09:24Z';

DELETE FROM "AUTH_OTPS"
WHERE "USER_ID" IN ('ff2be73b-8a80-46d6-90d8-cd462b876095', '874d7734-cc4f-47ee-9527-3a4c0262f7b3')
  AND "AUTH_OTP_CREATED_DATE" >= '2026-09-17T15:09:24Z';
