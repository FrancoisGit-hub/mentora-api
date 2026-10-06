-- Read only: rows created on or after the run start (2026-10-06 07:58:35.064101+00), every table with a *_CREATED_DATE column.
BEGIN TRANSACTION READ ONLY;

SELECT count(*) AS created_date_columns
FROM information_schema.columns
WHERE table_schema = 'public' AND column_name LIKE '%\_CREATED\_DATE' AND table_name IN (SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE');

SELECT c.table_name, c.column_name,
       (xpath('/row/n/text()', query_to_xml(format('SELECT count(*) AS n FROM %I WHERE %I >= %L', c.table_name, c.column_name, '2026-10-06 07:58:35.064101+00'), false, true, '')))[1]::text::int AS rows_since_run_start
FROM information_schema.columns c
JOIN information_schema.tables t ON t.table_schema = c.table_schema AND t.table_name = c.table_name AND t.table_type = 'BASE TABLE'
WHERE c.table_schema = 'public' AND c.column_name LIKE '%\_CREATED\_DATE'
ORDER BY rows_since_run_start DESC, c.table_name;

ROLLBACK;
