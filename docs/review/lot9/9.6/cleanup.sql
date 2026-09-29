-- Lot 9.6 - fixture cleanup, DRY RUN (ends with ROLLBACK). The COMMIT version is run only after approval.
-- Deletes only rows created by this run (run start 2026-09-29T20:58:49Z), each bounded by its id AND
-- its creation date:
--   - the voucher(s) of the coach-grant order 31fd024d-fa8b-447e-be59-5e44cb3ea841
--   - that order's item line (no date column: bounded by the order id AND the order's creation date)
--   - the order itself
--   - the DRAFT fixture product 718b91a3-79bd-40d8-b863-93d6c9655662 (created because the local DB had
--     no product; added to the cleanup scope with approval)
-- Not deleted, as in 9.2-9.5: the demo coach's login artifacts (AUTH_OTPS / AUTH_REFRESH_TOKENS).

\echo '== BEFORE'
SELECT
  (SELECT count(*) FROM "SESSION_VOUCHERS" v JOIN "ORDER_ITEMS" oi ON oi."ORDER_ITEM_ID" = v."VOUCHER_ORDER_ITEM_ID"
    WHERE oi."ORDER_ITEM_ORDER_ID" = '31fd024d-fa8b-447e-be59-5e44cb3ea841')                  AS fixture_vouchers,
  (SELECT count(*) FROM "ORDER_ITEMS" WHERE "ORDER_ITEM_ORDER_ID" = '31fd024d-fa8b-447e-be59-5e44cb3ea841') AS fixture_order_items,
  (SELECT count(*) FROM "ORDERS"   WHERE "ORDER_ID"   = '31fd024d-fa8b-447e-be59-5e44cb3ea841') AS fixture_orders,
  (SELECT count(*) FROM "PRODUCTS" WHERE "PRODUCT_ID" = '718b91a3-79bd-40d8-b863-93d6c9655662') AS fixture_products,
  (SELECT count(*) FROM "SESSION_VOUCHERS") AS vouchers_total,
  (SELECT count(*) FROM "ORDER_ITEMS")      AS order_items_total,
  (SELECT count(*) FROM "ORDERS")           AS orders_total,
  (SELECT count(*) FROM "PRODUCTS")         AS products_total;

BEGIN;

\echo '== DELETE'
-- 1. SESSION_VOUCHERS of the grant order
DELETE FROM "SESSION_VOUCHERS" v
USING "ORDER_ITEMS" oi, "ORDERS" o
WHERE v."VOUCHER_ORDER_ITEM_ID" = oi."ORDER_ITEM_ID"
  AND oi."ORDER_ITEM_ORDER_ID"  = o."ORDER_ID"
  AND o."ORDER_ID"              = '31fd024d-fa8b-447e-be59-5e44cb3ea841'
  AND o."ORDER_ORIGIN"          = 'COACH_GRANT'
  AND v."VOUCHER_CREATED_DATE"  >= '2026-09-29T20:58:49Z'
RETURNING v."VOUCHER_ID", v."VOUCHER_CREATED_DATE";

-- 2. ORDER_ITEMS of the grant order
DELETE FROM "ORDER_ITEMS" oi
USING "ORDERS" o
WHERE oi."ORDER_ITEM_ORDER_ID" = o."ORDER_ID"
  AND o."ORDER_ID"             = '31fd024d-fa8b-447e-be59-5e44cb3ea841'
  AND o."ORDER_ORIGIN"         = 'COACH_GRANT'
  AND o."ORDER_CREATED_DATE"   >= '2026-09-29T20:58:49Z'
RETURNING oi."ORDER_ITEM_ID", oi."ORDER_ITEM_PRODUCT_NAME";

-- 3. ORDERS: the grant order
DELETE FROM "ORDERS"
WHERE "ORDER_ID"           = '31fd024d-fa8b-447e-be59-5e44cb3ea841'
  AND "ORDER_ORIGIN"       = 'COACH_GRANT'
  AND "ORDER_CREATED_DATE" >= '2026-09-29T20:58:49Z'
RETURNING "ORDER_ID", "ORDER_ORIGIN", "ORDER_TOTAL_EUROS", "ORDER_CREATED_DATE";

-- 4. PRODUCTS: the DRAFT fixture product
DELETE FROM "PRODUCTS"
WHERE "PRODUCT_ID"           = '718b91a3-79bd-40d8-b863-93d6c9655662'
  AND "PRODUCT_CREATED_DATE" >= '2026-09-29T20:58:49Z'
RETURNING "PRODUCT_ID", "PRODUCT_NAME", "PRODUCT_STATUS", "PRODUCT_CREATED_DATE";

\echo '== AFTER (inside the transaction)'
SELECT
  (SELECT count(*) FROM "ORDER_ITEMS" WHERE "ORDER_ITEM_ORDER_ID" = '31fd024d-fa8b-447e-be59-5e44cb3ea841') AS fixture_order_items,
  (SELECT count(*) FROM "ORDERS"   WHERE "ORDER_ID"   = '31fd024d-fa8b-447e-be59-5e44cb3ea841') AS fixture_orders,
  (SELECT count(*) FROM "PRODUCTS" WHERE "PRODUCT_ID" = '718b91a3-79bd-40d8-b863-93d6c9655662') AS fixture_products,
  (SELECT count(*) FROM "SESSION_VOUCHERS") AS vouchers_total,
  (SELECT count(*) FROM "ORDER_ITEMS")      AS order_items_total,
  (SELECT count(*) FROM "ORDERS")           AS orders_total,
  (SELECT count(*) FROM "PRODUCTS")         AS products_total;

ROLLBACK;
