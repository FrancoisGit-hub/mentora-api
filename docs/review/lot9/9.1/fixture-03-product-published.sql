-- Lot 9.1 - fixture: one PUBLISHED coach-A product, needed for V10 (a real member cart
-- purchase must still work and create MEMBER_PURCHASE-origin vouchers). PA1 (from
-- fixture-02) is intentionally DRAFT to prove publication isn't required for a grant, so
-- it cannot be added to a cart.
--
-- PA3 (coach A, VISIO, PUBLISHED, 45 min) = 5d0adc24-dbf7-481c-b147-7361953365f0

INSERT INTO "PRODUCTS" (
    "PRODUCT_ID", "PRODUCT_NAME", "PRODUCT_OFFER_TYPE", "PRODUCT_OFFER_NATURE",
    "PRODUCT_DURATION_MINUTES", "PRODUCT_PRICE_EUROS", "PRODUCT_SPORT", "PRODUCT_STATUS",
    "PRODUCT_CREATED_DATE", "PRODUCT_UPDATED_DATE", "OFFER_PROGRAM_ID", "COACH_ID"
) VALUES (
    '5d0adc24-dbf7-481c-b147-7361953365f0', 'Lot9 - Coaching visio A (publié, achat M1)', 'VISIO', 'CLASSIQUE',
    45, 25.00, 'TRAINING', 'PUBLISHED',
    now(), now(), 'a4cc9289-9c94-4866-8fa1-4675f3cfb07d', 'e268a512-84d6-4e27-b5c0-1d6c08a47190'
);

SELECT "PRODUCT_ID", "PRODUCT_NAME", "PRODUCT_STATUS" FROM "PRODUCTS" WHERE "PRODUCT_ID" = '5d0adc24-dbf7-481c-b147-7361953365f0';
