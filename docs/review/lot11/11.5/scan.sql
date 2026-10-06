-- Read-only: counts, per table with a *_CREATED_DATE column, the rows created after the 11.5 run start.
DO $$
DECLARE r record; n bigint;
BEGIN
  FOR r IN SELECT table_name, column_name FROM information_schema.columns
           WHERE table_schema = 'public' AND column_name LIKE '%\_CREATED\_DATE' ESCAPE '\' ORDER BY 1 LOOP
    EXECUTE format('SELECT count(*) FROM %I WHERE %I > %L::timestamptz', r.table_name, r.column_name, '2026-10-06 07:12:31.789409+00') INTO n;
    IF n > 0 THEN RAISE NOTICE '% (%): %', r.table_name, r.column_name, n; END IF;
  END LOOP;
END $$;
