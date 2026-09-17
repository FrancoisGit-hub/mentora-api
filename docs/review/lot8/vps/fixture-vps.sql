-- Lot 8.3 - VPS demo DB fixture: ONE individual VISIO session owned by coach A.
-- Run with: psql -1 -v ON_ERROR_STOP=1 -f fixture-vps.sql
--
-- Fixed hardcoded UUIDs (generated once, listed here so cleanup never depends on RETURNING):
--   ORDER_ID              = 54b27daa-c8f4-4f18-abb4-8c4267c29d5a
--   ORDER_ITEM_ID          = 28ea9bba-81a0-4457-a671-2665235cba23
--   ORDER_ITEM_PRODUCT_ID  = c372cd47-248e-463d-8747-83785dccff8b   (weak ref, no FK)
--   SESSION_SLOT_ID        = 2fb87a30-429e-410f-b52d-201a5790abeb
--   VOUCHER_ID             = 14f55ccc-60b7-47c3-9f9f-5b2e1b2a9f39
--   VOUCHER_PRODUCT_ID     = a17d6694-d351-48a3-9cc5-c279fa004a5a   (weak ref, no FK)
--   SESSION_ID             = 1f08159f-f09a-4eb2-b2a8-1234b527914e
--
-- Coach A (owner) = cebb9b4d-00ee-4826-a8fe-58e8ab3b325b (admin@web2success.fr)
-- Member: the member of coach A returned by
--   SELECT "MEMBER_ID" FROM "MEMBER_COACHES" WHERE "COACH_ID" = 'cebb9b4d-00ee-4826-a8fe-58e8ab3b325b'
--   ORDER BY "MEMBER_ID" LIMIT 1
-- resolved once below into the psql variable :member_id so every INSERT uses the same member.

SELECT "MEMBER_ID" AS member_id
FROM "MEMBER_COACHES"
WHERE "COACH_ID" = 'cebb9b4d-00ee-4826-a8fe-58e8ab3b325b'
ORDER BY "MEMBER_ID"
LIMIT 1 \gset

\echo Resolved member_id: :member_id

-- 1. ORDERS
INSERT INTO "ORDERS" (
    "ORDER_ID", "ORDER_MEMBER_ID", "ORDER_COACH_ID", "ORDER_STATUS",
    "ORDER_TOTAL_EUROS", "ORDER_CREATED_DATE", "ORDER_UPDATED_DATE"
)
VALUES (
    '54b27daa-c8f4-4f18-abb4-8c4267c29d5a',
    :'member_id',
    'cebb9b4d-00ee-4826-a8fe-58e8ab3b325b',
    'PAID',
    60.00,
    now(),
    now()
);

-- 2. ORDER_ITEMS
INSERT INTO "ORDER_ITEMS" (
    "ORDER_ITEM_ID", "ORDER_ITEM_ORDER_ID", "ORDER_ITEM_PRODUCT_ID", "ORDER_ITEM_PRODUCT_NAME",
    "ORDER_ITEM_UNIT_PRICE_EUROS", "ORDER_ITEM_QUANTITY", "ORDER_ITEM_LINE_TOTAL_EUROS",
    "ORDER_ITEM_OFFER_TYPE", "ORDER_ITEM_DURATION_MINUTES"
)
VALUES (
    '28ea9bba-81a0-4457-a671-2665235cba23',
    '54b27daa-c8f4-4f18-abb4-8c4267c29d5a',
    'c372cd47-248e-463d-8747-83785dccff8b',
    'Lot8 VPS fixture item',
    60.00,
    1,
    60.00,
    'VISIO',
    60
);

-- 3. SESSION_SLOTS
INSERT INTO "SESSION_SLOTS" (
    "SESSION_SLOT_ID", "SESSION_SLOT_START_DATE", "SESSION_SLOT_END_DATE", "SESSION_SLOT_OFFER_TYPE",
    "SESSION_SLOT_DURATION_MINUTES", "SESSION_SLOT_IS_AVAILABLE", "SESSION_SLOT_CREATED_DATE", "COACH_ID"
)
VALUES (
    '2fb87a30-429e-410f-b52d-201a5790abeb',
    now(),
    now() + interval '60 minutes',
    'VISIO',
    60,
    false,
    now(),
    'cebb9b4d-00ee-4826-a8fe-58e8ab3b325b'
);

-- 4. SESSION_VOUCHERS
INSERT INTO "SESSION_VOUCHERS" (
    "VOUCHER_ID", "VOUCHER_ORDER_ITEM_ID", "VOUCHER_MEMBER_ID", "VOUCHER_COACH_ID", "VOUCHER_PRODUCT_ID",
    "VOUCHER_OFFER_TYPE", "VOUCHER_DURATION_MINUTES", "VOUCHER_STATUS", "VOUCHER_CREATED_DATE", "VOUCHER_UPDATED_DATE"
)
VALUES (
    '14f55ccc-60b7-47c3-9f9f-5b2e1b2a9f39',
    '28ea9bba-81a0-4457-a671-2665235cba23',
    :'member_id',
    'cebb9b4d-00ee-4826-a8fe-58e8ab3b325b',
    'a17d6694-d351-48a3-9cc5-c279fa004a5a',
    'VISIO',
    60,
    'RESERVED',
    now(),
    now()
);

-- 5. SESSIONS - individual VISIO session, SCHEDULED, +5 minutes from now
INSERT INTO "SESSIONS" (
    "SESSION_ID", "SESSION_VOUCHER_ID", "SESSION_SLOT_ID", "SESSION_MEMBER_ID", "SESSION_COACH_ID",
    "SESSION_OFFER_TYPE", "SESSION_DURATION_MINUTES", "SESSION_SPORT", "SESSION_SCHEDULED_AT",
    "SESSION_STATUS", "SESSION_CREATED_DATE", "SESSION_UPDATED_DATE"
)
VALUES (
    '1f08159f-f09a-4eb2-b2a8-1234b527914e',
    '14f55ccc-60b7-47c3-9f9f-5b2e1b2a9f39',
    '2fb87a30-429e-410f-b52d-201a5790abeb',
    :'member_id',
    'cebb9b4d-00ee-4826-a8fe-58e8ab3b325b',
    'VISIO',
    60,
    'TRAINING',
    now() + interval '5 minutes',
    'SCHEDULED',
    now(),
    now()
);

-- Confirm
SELECT "SESSION_ID", "SESSION_OFFER_TYPE", "SESSION_MEMBER_ID", "SESSION_STATUS", "SESSION_SCHEDULED_AT"
FROM "SESSIONS"
WHERE "SESSION_ID" = '1f08159f-f09a-4eb2-b2a8-1234b527914e';
