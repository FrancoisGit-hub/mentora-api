-- Lot 14.9b, proof 5.2: every NOT NULL column set to NULL, an unknown type, an unknown status, attempt count 11
-- and -1, data as a JSON array and as a JSON string. Each statement must fail with the expected constraint or column.
-- One transaction, ends with ROLLBACK (nothing is kept).
BEGIN;

CREATE FUNCTION pg_temp.ins(u text, ty text, ti text, bo text, da text, st text, ac text, nx text, cr text, up text)
RETURNS text LANGUAGE sql AS $f$
  SELECT format('INSERT INTO "NOTIFICATIONS" ("NOTIFICATION_USER_ID","NOTIFICATION_TYPE","NOTIFICATION_TITLE","NOTIFICATION_BODY","NOTIFICATION_DATA","NOTIFICATION_STATUS","NOTIFICATION_ATTEMPT_COUNT","NOTIFICATION_NEXT_ATTEMPT_DATE","NOTIFICATION_CREATED_DATE","NOTIFICATION_UPDATED_DATE") VALUES (%s,%s,%s,%s,%s,%s,%s,%s,%s,%s)',
                 u, ty, ti, bo, da, st, ac, nx, cr, up)
$f$;

CREATE FUNCTION pg_temp.try_sql(label text, stmt text) RETURNS void LANGUAGE plpgsql AS $f$
DECLARE c text; col text;
BEGIN
  EXECUTE stmt;
  RAISE NOTICE '% => NO ERROR (unexpected)', label;
EXCEPTION WHEN OTHERS THEN
  GET STACKED DIAGNOSTICS c = CONSTRAINT_NAME, col = COLUMN_NAME;
  RAISE NOTICE '% => sqlstate=% constraint=% column=%', label, SQLSTATE, coalesce(c, '-'), coalesce(col, '-');
END
$f$;

-- defaults: u = a real user, type CHAT_MESSAGE, title t, body b, data {"type":"CHAT_MESSAGE"}, PENDING, 0, now() x3
SELECT pg_temp.try_sql('USER_ID null',         pg_temp.ins('NULL', '''CHAT_MESSAGE''', '''t''', '''b''', '''{"type":"CHAT_MESSAGE"}''::jsonb', '''PENDING''', '0', 'now()', 'now()', 'now()'));
SELECT pg_temp.try_sql('TYPE null',            pg_temp.ins('(select "USER_ID" from "USERS" limit 1)', 'NULL', '''t''', '''b''', '''{"type":"CHAT_MESSAGE"}''::jsonb', '''PENDING''', '0', 'now()', 'now()', 'now()'));
SELECT pg_temp.try_sql('TITLE null',           pg_temp.ins('(select "USER_ID" from "USERS" limit 1)', '''CHAT_MESSAGE''', 'NULL', '''b''', '''{"type":"CHAT_MESSAGE"}''::jsonb', '''PENDING''', '0', 'now()', 'now()', 'now()'));
SELECT pg_temp.try_sql('BODY null',            pg_temp.ins('(select "USER_ID" from "USERS" limit 1)', '''CHAT_MESSAGE''', '''t''', 'NULL', '''{"type":"CHAT_MESSAGE"}''::jsonb', '''PENDING''', '0', 'now()', 'now()', 'now()'));
SELECT pg_temp.try_sql('DATA null',            pg_temp.ins('(select "USER_ID" from "USERS" limit 1)', '''CHAT_MESSAGE''', '''t''', '''b''', 'NULL', '''PENDING''', '0', 'now()', 'now()', 'now()'));
SELECT pg_temp.try_sql('STATUS null',          pg_temp.ins('(select "USER_ID" from "USERS" limit 1)', '''CHAT_MESSAGE''', '''t''', '''b''', '''{"type":"CHAT_MESSAGE"}''::jsonb', 'NULL', '0', 'now()', 'now()', 'now()'));
SELECT pg_temp.try_sql('ATTEMPT_COUNT null',   pg_temp.ins('(select "USER_ID" from "USERS" limit 1)', '''CHAT_MESSAGE''', '''t''', '''b''', '''{"type":"CHAT_MESSAGE"}''::jsonb', '''PENDING''', 'NULL', 'now()', 'now()', 'now()'));
SELECT pg_temp.try_sql('NEXT_ATTEMPT_DATE null', pg_temp.ins('(select "USER_ID" from "USERS" limit 1)', '''CHAT_MESSAGE''', '''t''', '''b''', '''{"type":"CHAT_MESSAGE"}''::jsonb', '''PENDING''', '0', 'NULL', 'now()', 'now()'));
SELECT pg_temp.try_sql('CREATED_DATE null',    pg_temp.ins('(select "USER_ID" from "USERS" limit 1)', '''CHAT_MESSAGE''', '''t''', '''b''', '''{"type":"CHAT_MESSAGE"}''::jsonb', '''PENDING''', '0', 'now()', 'NULL', 'now()'));
SELECT pg_temp.try_sql('UPDATED_DATE null',    pg_temp.ins('(select "USER_ID" from "USERS" limit 1)', '''CHAT_MESSAGE''', '''t''', '''b''', '''{"type":"CHAT_MESSAGE"}''::jsonb', '''PENDING''', '0', 'now()', 'now()', 'NULL'));
SELECT pg_temp.try_sql('unknown TYPE',         pg_temp.ins('(select "USER_ID" from "USERS" limit 1)', '''PUSH_TEST''', '''t''', '''b''', '''{"type":"CHAT_MESSAGE"}''::jsonb', '''PENDING''', '0', 'now()', 'now()', 'now()'));
SELECT pg_temp.try_sql('unknown STATUS',       pg_temp.ins('(select "USER_ID" from "USERS" limit 1)', '''CHAT_MESSAGE''', '''t''', '''b''', '''{"type":"CHAT_MESSAGE"}''::jsonb', '''SKIPPED''', '0', 'now()', 'now()', 'now()'));
SELECT pg_temp.try_sql('ATTEMPT_COUNT 11',     pg_temp.ins('(select "USER_ID" from "USERS" limit 1)', '''CHAT_MESSAGE''', '''t''', '''b''', '''{"type":"CHAT_MESSAGE"}''::jsonb', '''PENDING''', '11', 'now()', 'now()', 'now()'));
SELECT pg_temp.try_sql('ATTEMPT_COUNT -1',     pg_temp.ins('(select "USER_ID" from "USERS" limit 1)', '''CHAT_MESSAGE''', '''t''', '''b''', '''{"type":"CHAT_MESSAGE"}''::jsonb', '''PENDING''', '-1', 'now()', 'now()', 'now()'));
SELECT pg_temp.try_sql('DATA json array',      pg_temp.ins('(select "USER_ID" from "USERS" limit 1)', '''CHAT_MESSAGE''', '''t''', '''b''', '''[1,2]''::jsonb', '''PENDING''', '0', 'now()', 'now()', 'now()'));
SELECT pg_temp.try_sql('DATA json string',     pg_temp.ins('(select "USER_ID" from "USERS" limit 1)', '''CHAT_MESSAGE''', '''t''', '''b''', '''"x"''::jsonb', '''PENDING''', '0', 'now()', 'now()', 'now()'));
-- control: a valid row is accepted (and rolled back below)
SELECT pg_temp.try_sql('CONTROL valid row',    pg_temp.ins('(select "USER_ID" from "USERS" limit 1)', '''CHAT_MESSAGE''', '''t''', '''b''', '''{"type":"CHAT_MESSAGE"}''::jsonb', '''PENDING''', '0', 'now()', 'now()', 'now()'));

ROLLBACK;
SELECT 'notifications_rows_after_rollback=' || count(*) FROM "NOTIFICATIONS";
