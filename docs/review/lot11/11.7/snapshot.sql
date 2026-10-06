-- Lot 11.7 state snapshot, READ ONLY. Run at: baseline, after Up, after Down, after the second Up.
BEGIN TRANSACTION READ ONLY;

-- 1. Per store: row count, occurrences of each old key, new key and restBetweenRoundsSeconds,
--    and the aggregate md5 over ALL rows (string_agg of the body text ordered by primary key).
WITH t AS (
  SELECT 'PROGRAM_TEMPLATES' AS store, "PROGRAM_TEMPLATE_ID"::text AS pk, "PROGRAM_TEMPLATE_BODY"::text AS body
  FROM "PROGRAM_TEMPLATES"
  UNION ALL
  SELECT 'SESSION_TEMPLATES', "SESSION_TEMPLATE_ID"::text, "SESSION_TEMPLATE_CONTENT"::text
  FROM "SESSION_TEMPLATES"
)
SELECT store,
  count(*) AS row_count,
  coalesce(sum((length(body) - length(replace(body, '"restSeconds"', ''))) / length('"restSeconds"')), 0) AS old_restSeconds,
  coalesce(sum((length(body) - length(replace(body, '"restAfterSeconds"', ''))) / length('"restAfterSeconds"')), 0) AS old_restAfterSeconds,
  coalesce(sum((length(body) - length(replace(body, '"restSecondsAfterSeries"', ''))) / length('"restSecondsAfterSeries"')), 0) AS new_afterSeries,
  coalesce(sum((length(body) - length(replace(body, '"restSecondsAfterExercices"', ''))) / length('"restSecondsAfterExercices"')), 0) AS new_afterExercices,
  coalesce(sum((length(body) - length(replace(body, '"restBetweenRoundsSeconds"', ''))) / length('"restBetweenRoundsSeconds"')), 0) AS between_rounds,
  md5(coalesce(string_agg(body, '|' ORDER BY pk), '')) AS aggregate_md5
FROM t GROUP BY store ORDER BY store;

-- 2. md5 and key counts of the two l11rn- fixture bodies.
SELECT 'PROGRAM_TEMPLATES' AS store, "PROGRAM_TEMPLATE_ID" AS id, md5(t) AS md5_body,
       (length(t) - length(replace(t, '"restSeconds"', ''))) / length('"restSeconds"') AS old_restSeconds,
       (length(t) - length(replace(t, '"restAfterSeconds"', ''))) / length('"restAfterSeconds"') AS old_restAfterSeconds,
       (length(t) - length(replace(t, '"restSecondsAfterSeries"', ''))) / length('"restSecondsAfterSeries"') AS new_afterSeries,
       (length(t) - length(replace(t, '"restSecondsAfterExercices"', ''))) / length('"restSecondsAfterExercices"') AS new_afterExercices,
       (length(t) - length(replace(t, '"restBetweenRoundsSeconds"', ''))) / length('"restBetweenRoundsSeconds"') AS between_rounds
FROM (SELECT "PROGRAM_TEMPLATE_ID", "PROGRAM_TEMPLATE_BODY"::text AS t FROM "PROGRAM_TEMPLATES"
      WHERE "PROGRAM_TEMPLATE_ID" = '11700000-0000-4000-8000-0000000000a1') s
UNION ALL
SELECT 'SESSION_TEMPLATES', "SESSION_TEMPLATE_ID", md5(t),
       (length(t) - length(replace(t, '"restSeconds"', ''))) / length('"restSeconds"'),
       (length(t) - length(replace(t, '"restAfterSeconds"', ''))) / length('"restAfterSeconds"'),
       (length(t) - length(replace(t, '"restSecondsAfterSeries"', ''))) / length('"restSecondsAfterSeries"'),
       (length(t) - length(replace(t, '"restSecondsAfterExercices"', ''))) / length('"restSecondsAfterExercices"'),
       (length(t) - length(replace(t, '"restBetweenRoundsSeconds"', ''))) / length('"restBetweenRoundsSeconds"')
FROM (SELECT "SESSION_TEMPLATE_ID", "SESSION_TEMPLATE_CONTENT"::text AS t FROM "SESSION_TEMPLATES"
      WHERE "SESSION_TEMPLATE_ID" = '11700000-0000-4000-8000-0000000000a2') s;

-- 3. Rest values of one seeded template ('Prise de masse', first by name), read from its body under
--    whichever key name is currently stored. Ordered by traversal, so identical before and after.
SELECT "PROGRAM_TEMPLATE_NAME" AS seeded_template,
       jsonb_path_query_array("PROGRAM_TEMPLATE_BODY", 'strict $.**.restSeconds', '{}', true) AS old_exercise_rest_values,
       jsonb_path_query_array("PROGRAM_TEMPLATE_BODY", 'strict $.**.restSecondsAfterSeries', '{}', true) AS new_exercise_rest_values,
       jsonb_path_query_array("PROGRAM_TEMPLATE_BODY", 'strict $.**.restAfterSeconds', '{}', true) AS old_circuit_rest_values,
       jsonb_path_query_array("PROGRAM_TEMPLATE_BODY", 'strict $.**.restSecondsAfterExercices', '{}', true) AS new_circuit_rest_values
FROM "PROGRAM_TEMPLATES"
WHERE "PROGRAM_TEMPLATE_NAME" LIKE 'Prise%';

-- 4. Typed rest columns must not move: sums over the PROGRAM_* tables.
SELECT (SELECT count(*) FROM "PROGRAM_EXERCISES") AS exercises_rows,
       (SELECT coalesce(sum("PROGRAM_EXERCISE_REST_SECONDS"), 0) FROM "PROGRAM_EXERCISES") AS exercise_rest_sum,
       (SELECT count(*) FROM "PROGRAM_CIRCUITS") AS circuits_rows,
       (SELECT coalesce(sum("PROGRAM_CIRCUIT_REST_AFTER_SECONDS"), 0) FROM "PROGRAM_CIRCUITS") AS circuit_rest_sum;

ROLLBACK;
