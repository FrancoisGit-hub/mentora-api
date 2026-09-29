-- Lot 9.6 - exercise names that literally contain '%' or '_', and the 'squat' baseline (all rows)
SELECT count(*) FILTER (WHERE "EXERCISE_NAME" LIKE '%\%%')       AS name_contains_percent,
       count(*) FILTER (WHERE "EXERCISE_NAME" LIKE '%\_%')       AS name_contains_underscore,
       count(*) FILTER (WHERE "EXERCISE_NAME" ILIKE '%squat%')   AS name_ilike_squat
FROM "EXERCISES";
