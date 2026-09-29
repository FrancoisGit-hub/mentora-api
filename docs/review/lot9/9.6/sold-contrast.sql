-- Lot 9.6 - demo coach, current month: old predicate (all vouchers) vs new predicate (MEMBER_PURCHASE only)
SELECT
  (SELECT count(*) FROM "SESSION_VOUCHERS" s
    WHERE s."VOUCHER_COACH_ID" = 'e268a512-84d6-4e27-b5c0-1d6c08a47190'
      AND s."VOUCHER_CREATED_DATE" >= date_trunc('month', now() AT TIME ZONE 'UTC'))                   AS sold_old_predicate,
  (SELECT count(*) FROM "SESSION_VOUCHERS" s
     JOIN "ORDER_ITEMS" oi ON oi."ORDER_ITEM_ID" = s."VOUCHER_ORDER_ITEM_ID"
     JOIN "ORDERS" o       ON o."ORDER_ID"       = oi."ORDER_ITEM_ORDER_ID"
    WHERE s."VOUCHER_COACH_ID" = 'e268a512-84d6-4e27-b5c0-1d6c08a47190'
      AND s."VOUCHER_CREATED_DATE" >= date_trunc('month', now() AT TIME ZONE 'UTC')
      AND o."ORDER_ORIGIN" = 'MEMBER_PURCHASE')                                                         AS sold_new_predicate;
SELECT "ORDER_ID", "ORDER_ORIGIN", "ORDER_STATUS", "ORDER_TOTAL_EUROS", "ORDER_CREATED_DATE"
FROM "ORDERS" WHERE "ORDER_ID" = '31fd024d-fa8b-447e-be59-5e44cb3ea841';
