-- Lot 11.7 leftover scan, READ ONLY. Every table with a *_CREATED_DATE column: rows created on or after the run start.
-- Prints only tables with at least one row, then the number of tables scanned.
BEGIN TRANSACTION READ ONLY;

WITH cols AS (
  SELECT c.table_name, c.column_name
  FROM information_schema.columns c
  JOIN information_schema.tables t ON t.table_schema = c.table_schema AND t.table_name = c.table_name AND t.table_type = 'BASE TABLE'
  WHERE c.table_schema = 'public' AND c.column_name LIKE '%\_CREATED\_DATE'
), counts AS (
  SELECT table_name, column_name,
         (xpath('/row/n/text()', query_to_xml(format('SELECT count(*) AS n FROM public.%I WHERE %I >= %L::timestamptz', table_name, column_name, '2026-10-06 09:28:00.636468+00'), false, true, '')))[1]::text::int AS n
  FROM cols
)
SELECT table_name, column_name, n AS rows_since_run_start FROM counts WHERE n > 0 ORDER BY table_name;

SELECT count(*) AS tables_scanned FROM information_schema.columns c
JOIN information_schema.tables t ON t.table_schema = c.table_schema AND t.table_name = c.table_name AND t.table_type = 'BASE TABLE'
WHERE c.table_schema = 'public' AND c.column_name LIKE '%\_CREATED\_DATE';

ROLLBACK;
