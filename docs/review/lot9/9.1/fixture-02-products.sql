-- Lot 9.1 - fixture: products for the voucher-grant HTTP matrix, plus one session slot
-- for the V9 "book with a granted voucher" scenario.
--
-- Fixed UUIDs:
--   PA1  (coach A, VISIO, DRAFT/private, 30 min, eligible) = eb56b9b5-a9c3-4082-a858-357d6284d595
--   PB1  (coach B, VISIO, PUBLISHED, 30 min)                = c8821d7f-d9d5-4f25-a666-fcee2562a0cf
--   PA2  (coach A, VISIO_GROUPE, PUBLISHED, 60 min)         = ca2796f7-10d6-4497-b9e2-9a9359c87751
--       -- inserted directly via SQL: VISIO_GROUPE is rejected by ProductRequestValidator at the
--       -- API layer (see OfferType.cs), so it cannot be created through the normal create-product
--       -- endpoint. This row exists only to exercise the PRODUCT_NOT_ELIGIBLE branch in V7.
--   SLOT1 (coach A, VISIO, 30 min, available, +2 days)      = 089f79ca-6ad9-4732-9863-41b0ef57f9ae

INSERT INTO "PRODUCTS" (
    "PRODUCT_ID", "PRODUCT_NAME", "PRODUCT_OFFER_TYPE", "PRODUCT_OFFER_NATURE",
    "PRODUCT_DURATION_MINUTES", "PRODUCT_PRICE_EUROS", "PRODUCT_SPORT", "PRODUCT_STATUS",
    "PRODUCT_CREATED_DATE", "PRODUCT_UPDATED_DATE", "OFFER_PROGRAM_ID", "COACH_ID"
) VALUES (
    'eb56b9b5-a9c3-4082-a858-357d6284d595', 'Lot9 - Coaching visio A (privé)', 'VISIO', 'CLASSIQUE',
    30, 40.00, 'TRAINING', 'DRAFT',
    now(), now(), 'a4cc9289-9c94-4866-8fa1-4675f3cfb07d', 'e268a512-84d6-4e27-b5c0-1d6c08a47190'
), (
    'c8821d7f-d9d5-4f25-a666-fcee2562a0cf', 'Lot9 - Coaching visio B', 'VISIO', 'CLASSIQUE',
    30, 40.00, 'TRAINING', 'PUBLISHED',
    now(), now(), '9d2072e9-9144-4c99-abdc-9d108c32b012', 'c0a01743-e580-4597-a834-54011da05a17'
), (
    'ca2796f7-10d6-4497-b9e2-9a9359c87751', 'Lot9 - Visio groupe A (fixture non-eligible)', 'VISIO_GROUPE', 'CLASSIQUE',
    60, 20.00, 'TRAINING', 'PUBLISHED',
    now(), now(), 'a4cc9289-9c94-4866-8fa1-4675f3cfb07d', 'e268a512-84d6-4e27-b5c0-1d6c08a47190'
);

INSERT INTO "SESSION_SLOTS" (
    "SESSION_SLOT_ID", "SESSION_SLOT_START_DATE", "SESSION_SLOT_END_DATE", "SESSION_SLOT_OFFER_TYPE",
    "SESSION_SLOT_DURATION_MINUTES", "SESSION_SLOT_IS_AVAILABLE", "SESSION_SLOT_CREATED_DATE", "COACH_ID"
) VALUES (
    '089f79ca-6ad9-4732-9863-41b0ef57f9ae', now() + interval '2 days', now() + interval '2 days 30 minutes',
    'VISIO', 30, true, now(), 'e268a512-84d6-4e27-b5c0-1d6c08a47190'
);

-- Confirm
SELECT "PRODUCT_ID", "PRODUCT_NAME", "PRODUCT_OFFER_TYPE", "PRODUCT_STATUS", "COACH_ID" FROM "PRODUCTS"
WHERE "PRODUCT_ID" IN ('eb56b9b5-a9c3-4082-a858-357d6284d595','c8821d7f-d9d5-4f25-a666-fcee2562a0cf','ca2796f7-10d6-4497-b9e2-9a9359c87751');
SELECT "SESSION_SLOT_ID", "SESSION_SLOT_OFFER_TYPE", "SESSION_SLOT_DURATION_MINUTES", "SESSION_SLOT_IS_AVAILABLE" FROM "SESSION_SLOTS"
WHERE "SESSION_SLOT_ID" = '089f79ca-6ad9-4732-9863-41b0ef57f9ae';
