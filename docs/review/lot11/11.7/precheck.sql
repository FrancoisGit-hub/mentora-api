-- Lot 11.7 pre-check, READ ONLY. Run BEFORE applying Lot11_7_RestFieldsJsonKeys.
BEGIN TRANSACTION READ ONLY;

-- 1. Token used as a JSON string VALUE (not a key) anywhere in the stores. Expected: 0 and 0.
SELECT 'PROGRAM_TEMPLATES' AS store, count(*) AS rows_with_token_as_value
FROM "PROGRAM_TEMPLATES"
WHERE jsonb_path_exists("PROGRAM_TEMPLATE_BODY",
  'strict $.** ? (@.type() == "string" && (@ == "restSeconds" || @ == "restAfterSeconds" || @ == "restSecondsAfterSeries" || @ == "restSecondsAfterExercices"))')
UNION ALL
SELECT 'SESSION_TEMPLATES', count(*)
FROM "SESSION_TEMPLATES"
WHERE jsonb_path_exists("SESSION_TEMPLATE_CONTENT",
  'strict $.** ? (@.type() == "string" && (@ == "restSeconds" || @ == "restAfterSeconds" || @ == "restSecondsAfterSeries" || @ == "restSecondsAfterExercices"))');

-- 2. Rows and occurrences per store. Expected before applying: new keys = 0.
SELECT 'PROGRAM_TEMPLATES' AS store,
  count(*) AS row_count,
  coalesce(sum((length(t) - length(replace(t, '"restSeconds"', ''))) / length('"restSeconds"')), 0) AS old_restSeconds,
  coalesce(sum((length(t) - length(replace(t, '"restAfterSeconds"', ''))) / length('"restAfterSeconds"')), 0) AS old_restAfterSeconds,
  coalesce(sum((length(t) - length(replace(t, '"restSecondsAfterSeries"', ''))) / length('"restSecondsAfterSeries"')), 0) AS new_restSecondsAfterSeries,
  coalesce(sum((length(t) - length(replace(t, '"restSecondsAfterExercices"', ''))) / length('"restSecondsAfterExercices"')), 0) AS new_restSecondsAfterExercices
FROM (SELECT "PROGRAM_TEMPLATE_BODY"::text AS t FROM "PROGRAM_TEMPLATES") s
UNION ALL
SELECT 'SESSION_TEMPLATES',
  count(*),
  coalesce(sum((length(t) - length(replace(t, '"restSeconds"', ''))) / length('"restSeconds"')), 0),
  coalesce(sum((length(t) - length(replace(t, '"restAfterSeconds"', ''))) / length('"restAfterSeconds"')), 0),
  coalesce(sum((length(t) - length(replace(t, '"restSecondsAfterSeries"', ''))) / length('"restSecondsAfterSeries"')), 0),
  coalesce(sum((length(t) - length(replace(t, '"restSecondsAfterExercices"', ''))) / length('"restSecondsAfterExercices"')), 0)
FROM (SELECT "SESSION_TEMPLATE_CONTENT"::text AS t FROM "SESSION_TEMPLATES") s;

ROLLBACK;
