-- Lot 9.6 - READ-ONLY check for the VPS (not run by Claude anywhere). Months are UTC, like CoachMeService.
-- Row 'ALL': total vouchers, vouchers with a NULL order item, vouchers whose order is COACH_GRANT.
-- One row per coach: old sold count (all vouchers) vs new (MEMBER_PURCHASE only), current and previous month.
WITH bounds AS (
  SELECT date_trunc('month', now() AT TIME ZONE 'UTC')                        AS month_start,
         date_trunc('month', now() AT TIME ZONE 'UTC') - interval '1 month'   AS prev_month_start
),
v AS (
  SELECT s."VOUCHER_COACH_ID" AS coach_id, s."VOUCHER_CREATED_DATE" AS created, s."VOUCHER_ORDER_ITEM_ID" AS order_item_id,
         o."ORDER_ORIGIN" AS origin
  FROM "SESSION_VOUCHERS" s
  LEFT JOIN "ORDER_ITEMS" oi ON oi."ORDER_ITEM_ID" = s."VOUCHER_ORDER_ITEM_ID"
  LEFT JOIN "ORDERS" o       ON o."ORDER_ID"       = oi."ORDER_ITEM_ORDER_ID"
)
SELECT 'ALL' AS coach_id,
       count(*)                                          AS total_vouchers,
       count(*) FILTER (WHERE v.order_item_id IS NULL)   AS null_order_item,
       count(*) FILTER (WHERE v.origin = 'COACH_GRANT')  AS coach_grant_vouchers,
       NULL::bigint AS sold_this_month_old, NULL::bigint AS sold_this_month_new,
       NULL::bigint AS sold_prev_month_old, NULL::bigint AS sold_prev_month_new
FROM v
UNION ALL
SELECT v.coach_id::text,
       count(*),
       count(*) FILTER (WHERE v.order_item_id IS NULL),
       count(*) FILTER (WHERE v.origin = 'COACH_GRANT'),
       count(*) FILTER (WHERE v.created >= b.month_start),
       count(*) FILTER (WHERE v.created >= b.month_start AND v.origin = 'MEMBER_PURCHASE'),
       count(*) FILTER (WHERE v.created >= b.prev_month_start AND v.created < b.month_start),
       count(*) FILTER (WHERE v.created >= b.prev_month_start AND v.created < b.month_start AND v.origin = 'MEMBER_PURCHASE')
FROM v CROSS JOIN bounds b
GROUP BY v.coach_id
ORDER BY 1;
