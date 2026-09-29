-- Lot 9.5 V3 expectations: one line per exercise reference of every active Mentora template.
-- Output (psql -At): template_id|template_name|exercise_id|blocks|sessions|circuits
WITH t AS (
  SELECT "PROGRAM_TEMPLATE_ID" id, "PROGRAM_TEMPLATE_NAME" nm, "PROGRAM_TEMPLATE_BODY"::jsonb b
  FROM "PROGRAM_TEMPLATES" WHERE "PROGRAM_TEMPLATE_COACH_ID" IS NULL AND "PROGRAM_TEMPLATE_IS_ACTIVE"
)
SELECT t.id, t.nm, x #>> '{}',
  (SELECT count(*) FROM jsonb_path_query(t.b,'strict $.**.blocks[*]')),
  (SELECT count(*) FROM jsonb_path_query(t.b,'strict $.**.sessions[*]')),
  (SELECT count(*) FROM jsonb_path_query(t.b,'strict $.**.circuits[*]'))
FROM t, jsonb_path_query(t.b,'strict $.**.exerciseId') x
ORDER BY 2, 3;
