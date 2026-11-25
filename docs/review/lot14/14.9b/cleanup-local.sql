-- Lot 14.9b: cleanup of the local database (mentora_db) after the runtime proofs. DRY RUN: the last line is ROLLBACK.
-- Run start: 2026-11-25T00:51:37.717Z. Every id below is a literal collected by read-only queries before this script was written.
-- Every DELETE is bounded by explicit ids AND by the created date (or sent / started date) >= run start, except the
-- backdated notification d28e0b00-b5a2-43d4-b3ed-6f53fc8cee8d (created date moved 25 hours back by proof 5.4 e), matched by id only.
-- Authentication rows of coach@mentora.fr and member@mentora.fr created in the run are NOT deleted (counts are printed).
\pset format unaligned
\pset tuples_only on
\pset fieldsep ' | '

BEGIN;

\echo === BEFORE: rows created at or after the run start, one line per table that has a *_CREATED_DATE column
SELECT table_name, (xpath('/row/c/text()', query_to_xml(format('select count(*) as c from %I where %I >= timestamptz ''2026-11-25T00:51:37.717Z''', table_name, column_name), false, true, '')))[1]::text::int
FROM information_schema.columns WHERE table_schema = 'public' AND column_name LIKE '%CREATED_DATE' ORDER BY table_name;

\echo === BEFORE: other counts
SELECT 'MESSAGES sent at or after run start', count(*) FROM "MESSAGES" WHERE "MESSAGE_SENT_DATE" >= timestamptz '2026-11-25T00:51:37.717Z';
SELECT 'MEMBER_COACHES started at or after run start', count(*) FROM "MEMBER_COACHES" WHERE "STARTED_AT" >= timestamptz '2026-11-25T00:51:37.717Z';
SELECT 'ORDER_ITEMS of orders created at or after run start', count(*) FROM "ORDER_ITEMS" WHERE "ORDER_ITEM_ORDER_ID" IN (SELECT "ORDER_ID" FROM "ORDERS" WHERE "ORDER_CREATED_DATE" >= timestamptz '2026-11-25T00:51:37.717Z');
SELECT 'NOTIFICATIONS total', count(*) FROM "NOTIFICATIONS";
SELECT 'USER_DEVICES total', count(*) FROM "USER_DEVICES";
SELECT 'auth OTPS of coach/member@mentora.fr created in run (kept)', count(*) FROM "AUTH_OTPS" o JOIN "USERS" u ON u."USER_ID" = o."USER_ID" WHERE lower(u."USER_EMAIL") IN ('coach@mentora.fr', 'member@mentora.fr') AND o."AUTH_OTP_CREATED_DATE" >= timestamptz '2026-11-25T00:51:37.717Z';
SELECT 'auth refresh tokens of coach/member@mentora.fr created in run (kept)', count(*) FROM "AUTH_REFRESH_TOKENS" t JOIN "USERS" u ON u."USER_ID" = t."USER_ID" WHERE lower(u."USER_EMAIL") IN ('coach@mentora.fr', 'member@mentora.fr') AND t."AUTH_REFRESH_TOKEN_CREATED_DATE" >= timestamptz '2026-11-25T00:51:37.717Z';
SELECT 'conversation last message date is null', ("CONVERSATION_LAST_MESSAGE_DATE" IS NULL)::text FROM "CONVERSATIONS" WHERE "CONVERSATION_ID" = '58d5c27c-d823-48a8-bc68-59f053b02252';
SELECT 'member@mentora.fr NotifMessages', "MEMBER_PARAMETER_NOTIF_MESSAGES"::text FROM "MEMBER_PARAMETERS" WHERE "MEMBER_ID" = '38b05ed2-e577-4cf7-81e9-365eaafab626';

\echo === DELETES (row count of each statement)

-- 1. NOTIFICATIONS: 12 rows created in the run, plus the backdated one by id only (expected 13)
WITH d AS (
  DELETE FROM "NOTIFICATIONS"
  WHERE ("NOTIFICATION_ID" IN (
      '564f5aef-73ec-4a10-86ae-c26719c8b000', 'e26c30f3-37f8-4882-804d-ef560860c451', 'afdfbc43-a16e-404a-8838-611fac611e86',
      '9417e2e7-d601-4abb-bca3-abfab717dc68', '6b4873a0-9ac5-4238-9c3d-863ff1e471c5', 'd4b90f26-a31c-49a4-ae10-f3c262ae5c97',
      '26d462fc-e14b-4b7d-b81c-35b971ebfd3d', '4c8de205-4a4f-4414-b328-333cc4b374b8', 'f3efdb16-ba4d-49f5-ad3b-8f3b77e7eae0',
      'd342667b-b6da-48c8-8d93-196c6a729da2', 'a3671b70-00c2-4387-9a54-0378fe902fd6', '22c9a9a2-11d5-4aab-9d3c-a764b07c4152')
      AND "NOTIFICATION_CREATED_DATE" >= timestamptz '2026-11-25T00:51:37.717Z')
     OR "NOTIFICATION_ID" = 'd28e0b00-b5a2-43d4-b3ed-6f53fc8cee8d'
  RETURNING 1)
SELECT 'deleted NOTIFICATIONS (expected 13)', count(*) FROM d;

-- 2. MESSAGES: the 7 l149b messages of the member@mentora.fr / coach@mentora.fr conversation (expected 7)
WITH d AS (
  DELETE FROM "MESSAGES"
  WHERE "MESSAGE_ID" IN (
      '90f4ca4b-dd8f-4db5-a592-142821e5d6bb', '7430bde6-d7ae-4405-831c-51f2438ad3e7', '21010704-128e-4d4c-b291-a834f18eeea2',
      'e701e631-caf3-4ce8-af82-3ecaee59eea9', '75abd498-e9e1-4491-a406-835daf93c110', 'fae93a48-8d0b-4ed6-8074-cbb45ea709c8',
      '89232c3d-2eae-49d7-834a-2a10e45d49de')
    AND "CONVERSATION_ID" = '58d5c27c-d823-48a8-bc68-59f053b02252'
    AND "MESSAGE_SENT_DATE" >= timestamptz '2026-11-25T00:51:37.717Z'
  RETURNING 1)
SELECT 'deleted MESSAGES (expected 7)', count(*) FROM d;

-- 3. Conversation: the only column written by the send paths (ConversationService.cs:142 and :222) goes back to NULL,
-- only if no message is left for it. The original NULL is inferred from "0 messages before the run".
WITH u AS (
  UPDATE "CONVERSATIONS" SET "CONVERSATION_LAST_MESSAGE_DATE" = NULL
  WHERE "CONVERSATION_ID" = '58d5c27c-d823-48a8-bc68-59f053b02252'
    AND NOT EXISTS (SELECT 1 FROM "MESSAGES" WHERE "CONVERSATION_ID" = '58d5c27c-d823-48a8-bc68-59f053b02252')
  RETURNING 1)
SELECT 'updated CONVERSATIONS last message date to NULL (expected 1)', count(*) FROM u;
SELECT 'messages left for that conversation (expected 0)', count(*) FROM "MESSAGES" WHERE "CONVERSATION_ID" = '58d5c27c-d823-48a8-bc68-59f053b02252';
SELECT 'conversation last message date is null (expected true)', ("CONVERSATION_LAST_MESSAGE_DATE" IS NULL)::text FROM "CONVERSATIONS" WHERE "CONVERSATION_ID" = '58d5c27c-d823-48a8-bc68-59f053b02252';

-- 4. SESSION_PARTICIPANTS (expected 1)
WITH d AS (
  DELETE FROM "SESSION_PARTICIPANTS"
  WHERE "SESSION_PARTICIPANT_ID" = '611460db-39d7-4c93-915d-ad78fd2070a7'
    AND "SESSION_PARTICIPANT_CREATED_DATE" >= timestamptz '2026-11-25T00:51:37.717Z'
  RETURNING 1)
SELECT 'deleted SESSION_PARTICIPANTS (expected 1)', count(*) FROM d;

-- 5. SESSIONS (expected 3)
WITH d AS (
  DELETE FROM "SESSIONS"
  WHERE "SESSION_ID" IN ('13b8cd6c-efdc-4728-abbf-4baafacbba04', '22356963-0c15-48b3-9acc-936fde23f956', '9b4bd3f9-679e-4f2c-a6dd-5a6760a33001')
    AND "SESSION_CREATED_DATE" >= timestamptz '2026-11-25T00:51:37.717Z'
  RETURNING 1)
SELECT 'deleted SESSIONS (expected 3)', count(*) FROM d;

-- 6. SESSION_VOUCHERS (expected 3)
WITH d AS (
  DELETE FROM "SESSION_VOUCHERS"
  WHERE "VOUCHER_ID" IN ('355e22ae-f853-4a9b-bc25-c264addf287f', '8ae6595c-d280-4252-8e0d-291a8f51bd5d', 'e73af24e-2d4a-425e-9a90-835bd69a959e')
    AND "VOUCHER_CREATED_DATE" >= timestamptz '2026-11-25T00:51:37.717Z'
  RETURNING 1)
SELECT 'deleted SESSION_VOUCHERS (expected 3)', count(*) FROM d;

-- 7. ORDER_ITEMS of the two orders created by the voucher grants (expected 2; the table has no created date, bounded by the parent order)
WITH d AS (
  DELETE FROM "ORDER_ITEMS"
  WHERE "ORDER_ITEM_ID" IN ('322ec62a-d210-43ad-aca3-89dfbeef705f', 'afc4fd53-631c-4915-8b5c-374467908cb4')
    AND "ORDER_ITEM_ORDER_ID" IN (SELECT "ORDER_ID" FROM "ORDERS"
        WHERE "ORDER_ID" IN ('3bd215d5-79b4-4f27-afc2-84b489b36f42', 'ac5673ce-4f1e-4694-b415-b0b410ccce40')
          AND "ORDER_CREATED_DATE" >= timestamptz '2026-11-25T00:51:37.717Z')
  RETURNING 1)
SELECT 'deleted ORDER_ITEMS (expected 2)', count(*) FROM d;

-- 8. ORDERS (expected 2)
WITH d AS (
  DELETE FROM "ORDERS"
  WHERE "ORDER_ID" IN ('3bd215d5-79b4-4f27-afc2-84b489b36f42', 'ac5673ce-4f1e-4694-b415-b0b410ccce40')
    AND "ORDER_CREATED_DATE" >= timestamptz '2026-11-25T00:51:37.717Z'
  RETURNING 1)
SELECT 'deleted ORDERS (expected 2)', count(*) FROM d;

-- 9. SESSION_SLOTS (expected 3)
WITH d AS (
  DELETE FROM "SESSION_SLOTS"
  WHERE "SESSION_SLOT_ID" IN ('241328ee-b302-4da4-8b05-7ce7fa9b195c', '2fc30935-63fc-4a89-bfaf-b00249b9294b', 'fcf88a5a-cef5-46a0-a9ac-136fd446bf28')
    AND "SESSION_SLOT_CREATED_DATE" >= timestamptz '2026-11-25T00:51:37.717Z'
  RETURNING 1)
SELECT 'deleted SESSION_SLOTS (expected 3)', count(*) FROM d;

-- 10. PRODUCTS (expected 2)
WITH d AS (
  DELETE FROM "PRODUCTS"
  WHERE "PRODUCT_ID" IN ('a4a26af7-eabe-4bd9-bd89-1b81f38e8e06', 'c8d2d0ef-28ff-443a-bdee-10a3faa6ce33')
    AND "PRODUCT_CREATED_DATE" >= timestamptz '2026-11-25T00:51:37.717Z'
  RETURNING 1)
SELECT 'deleted PRODUCTS (expected 2)', count(*) FROM d;

-- 11. The l149b member l149b-member1@example.com, in foreign key order
WITH d AS (
  DELETE FROM "AUTH_OTPS"
  WHERE "AUTH_OTP_ID" IN ('00821918-57ac-4c3d-b676-9f6ebe11741f', '36d7578b-e582-473f-bac0-2c406e3337ec')
    AND "USER_ID" = 'fa03cd08-9173-444b-9187-301f06a03fd2'
    AND "AUTH_OTP_CREATED_DATE" >= timestamptz '2026-11-25T00:51:37.717Z'
  RETURNING 1)
SELECT 'deleted AUTH_OTPS of the l149b member (expected 2)', count(*) FROM d;

WITH d AS (
  DELETE FROM "AUTH_REFRESH_TOKENS"
  WHERE "AUTH_REFRESH_TOKEN_ID" IN ('02d16925-a166-4bbe-ab83-331191b86073', 'd565e3dc-8a0b-46ab-a960-f9b319fda006')
    AND "USER_ID" = 'fa03cd08-9173-444b-9187-301f06a03fd2'
    AND "AUTH_REFRESH_TOKEN_CREATED_DATE" >= timestamptz '2026-11-25T00:51:37.717Z'
  RETURNING 1)
SELECT 'deleted AUTH_REFRESH_TOKENS of the l149b member (expected 2)', count(*) FROM d;

WITH d AS (
  DELETE FROM "USER_DEVICES"
  WHERE "USER_ID" = 'fa03cd08-9173-444b-9187-301f06a03fd2'
    AND "USER_DEVICE_CREATED_DATE" >= timestamptz '2026-11-25T00:51:37.717Z'
  RETURNING 1)
SELECT 'deleted USER_DEVICES of the l149b member (expected 0)', count(*) FROM d;

WITH d AS (
  DELETE FROM "MEMBER_PARAMETERS"
  WHERE "MEMBER_ID" = '0946e1e8-654c-4cc0-bbe1-3e9da4edb9bd'
    AND "MEMBER_PARAMETER_CREATED_DATE" >= timestamptz '2026-11-25T00:51:37.717Z'
  RETURNING 1)
SELECT 'deleted MEMBER_PARAMETERS of the l149b member (expected 0)', count(*) FROM d;

WITH d AS (
  DELETE FROM "MEMBER_COACHES"
  WHERE "MEMBER_COACH_ID" = 'a1f2831f-bc21-4839-854f-1d900600b3ce'
    AND "MEMBER_ID" = '0946e1e8-654c-4cc0-bbe1-3e9da4edb9bd'
    AND "STARTED_AT" >= timestamptz '2026-11-25T00:51:37.717Z'
  RETURNING 1)
SELECT 'deleted MEMBER_COACHES of the l149b member (expected 1)', count(*) FROM d;

WITH d AS (
  DELETE FROM "CONVERSATIONS"
  WHERE "MEMBER_ID" = '0946e1e8-654c-4cc0-bbe1-3e9da4edb9bd'
    AND "CONVERSATION_CREATED_DATE" >= timestamptz '2026-11-25T00:51:37.717Z'
  RETURNING 1)
SELECT 'deleted CONVERSATIONS of the l149b member (expected 0)', count(*) FROM d;

WITH d AS (
  DELETE FROM "MEMBERS"
  WHERE "MEMBER_ID" = '0946e1e8-654c-4cc0-bbe1-3e9da4edb9bd'
    AND "USER_ID" = 'fa03cd08-9173-444b-9187-301f06a03fd2'
    AND "MEMBER_CREATED_DATE" >= timestamptz '2026-11-25T00:51:37.717Z'
  RETURNING 1)
SELECT 'deleted MEMBERS (expected 1)', count(*) FROM d;

WITH d AS (
  DELETE FROM "USERS"
  WHERE "USER_ID" = 'fa03cd08-9173-444b-9187-301f06a03fd2'
    AND "USER_CREATED_DATE" >= timestamptz '2026-11-25T00:51:37.717Z'
  RETURNING 1)
SELECT 'deleted USERS (expected 1)', count(*) FROM d;

\echo === AFTER: rows created at or after the run start (expected 0 everywhere except AUTH_OTPS and AUTH_REFRESH_TOKENS of coach/member@mentora.fr)
SELECT table_name, (xpath('/row/c/text()', query_to_xml(format('select count(*) as c from %I where %I >= timestamptz ''2026-11-25T00:51:37.717Z''', table_name, column_name), false, true, '')))[1]::text::int
FROM information_schema.columns WHERE table_schema = 'public' AND column_name LIKE '%CREATED_DATE' ORDER BY table_name;

\echo === AFTER: other counts
SELECT 'MESSAGES sent at or after run start (expected 0)', count(*) FROM "MESSAGES" WHERE "MESSAGE_SENT_DATE" >= timestamptz '2026-11-25T00:51:37.717Z';
SELECT 'MEMBER_COACHES started at or after run start (expected 0)', count(*) FROM "MEMBER_COACHES" WHERE "STARTED_AT" >= timestamptz '2026-11-25T00:51:37.717Z';
SELECT 'ORDER_ITEMS of orders created at or after run start (expected 0)', count(*) FROM "ORDER_ITEMS" WHERE "ORDER_ITEM_ORDER_ID" IN (SELECT "ORDER_ID" FROM "ORDERS" WHERE "ORDER_CREATED_DATE" >= timestamptz '2026-11-25T00:51:37.717Z');
SELECT 'NOTIFICATIONS total (expected 0)', count(*) FROM "NOTIFICATIONS";
SELECT 'rows matching l149b (expected 0)',
       (SELECT count(*) FROM "PRODUCTS" WHERE "PRODUCT_NAME" LIKE 'l149b%')
     + (SELECT count(*) FROM "MESSAGES" WHERE "MESSAGE_CONTENT" LIKE 'l149b%')
     + (SELECT count(*) FROM "USERS" WHERE lower("USER_EMAIL") LIKE 'l149b%')
     + (SELECT count(*) FROM "SESSIONS" WHERE "SESSION_CANCELLATION_REASON" LIKE 'l149b%');
SELECT 'USER_DEVICES total (expected 0, same as before the run)', count(*) FROM "USER_DEVICES";
SELECT 'member@mentora.fr NotifMessages (expected true, its original value)', "MEMBER_PARAMETER_NOTIF_MESSAGES"::text FROM "MEMBER_PARAMETERS" WHERE "MEMBER_ID" = '38b05ed2-e577-4cf7-81e9-365eaafab626';
SELECT 'auth OTPS of coach/member@mentora.fr created in run (kept)', count(*) FROM "AUTH_OTPS" o JOIN "USERS" u ON u."USER_ID" = o."USER_ID" WHERE lower(u."USER_EMAIL") IN ('coach@mentora.fr', 'member@mentora.fr') AND o."AUTH_OTP_CREATED_DATE" >= timestamptz '2026-11-25T00:51:37.717Z';
SELECT 'auth refresh tokens of coach/member@mentora.fr created in run (kept)', count(*) FROM "AUTH_REFRESH_TOKENS" t JOIN "USERS" u ON u."USER_ID" = t."USER_ID" WHERE lower(u."USER_EMAIL") IN ('coach@mentora.fr', 'member@mentora.fr') AND t."AUTH_REFRESH_TOKEN_CREATED_DATE" >= timestamptz '2026-11-25T00:51:37.717Z';

ROLLBACK;
