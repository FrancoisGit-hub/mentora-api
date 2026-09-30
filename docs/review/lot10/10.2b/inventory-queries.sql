-- Lot 10.2b preflight, READ-ONLY.
-- fold(x) = lower(translate(x, accented letters, ascii letters)): no unaccent extension is installed.
\echo == database locale / extensions
SELECT datname, datcollate, datctype FROM pg_database WHERE datname = current_database();
SELECT extname FROM pg_extension ORDER BY 1;

\echo == 2c: fields matched by current member search for Chest press machine: ILIKE %dév% accent-sensitive, and folded %dev%
SELECT
  "EXERCISE_NAME", "EXERCISE_NAME_EN", "EXERCISE_SEARCH_TAGS",
  "EXERCISE_NAME"        ILIKE '%dév%' AS name_ilike_dev_accent,
  "EXERCISE_NAME_EN"     ILIKE '%dév%' AS name_en_ilike_dev_accent,
  "EXERCISE_SEARCH_TAGS" ILIKE '%dév%' AS tags_ilike_dev_accent,
  lower(translate("EXERCISE_NAME",        'éèêëÉÈÊËàâäÀÂÄîïÎÏôöÔÖùûüÙÛÜçÇ','eeeeEEEEaaaAAAiiIIooOOuuuUUUcC')) LIKE '%dev%' AS name_folded,
  lower(translate(coalesce("EXERCISE_NAME_EN",''),     'éèêëÉÈÊËàâäÀÂÄîïÎÏôöÔÖùûüÙÛÜçÇ','eeeeEEEEaaaAAAiiIIooOOuuuUUUcC')) LIKE '%dev%' AS name_en_folded,
  lower(translate(coalesce("EXERCISE_SEARCH_TAGS",''), 'éèêëÉÈÊËàâäÀÂÄîïÎÏôöÔÖùûüÙÛÜçÇ','eeeeEEEEaaaAAAiiIIooOOuuuUUUcC')) LIKE '%dev%' AS tags_folded
FROM "EXERCISES" WHERE "EXERCISE_NAME" = 'Chest press machine';

\echo == 2c: current member search=dév ILIKE on name, name_en, tags; first rows by name (current order)
SELECT "EXERCISE_NAME",
       "EXERCISE_NAME" ILIKE '%dév%' AS in_name,
       coalesce("EXERCISE_NAME_EN" ILIKE '%dév%', false) AS in_name_en,
       coalesce("EXERCISE_SEARCH_TAGS" ILIKE '%dév%', false) AS in_tags
FROM "EXERCISES"
WHERE "EXERCISE_IS_ACTIVE" AND ("EXERCISE_NAME" ILIKE '%dév%' OR "EXERCISE_NAME_EN" ILIKE '%dév%' OR "EXERCISE_SEARCH_TAGS" ILIKE '%dév%')
ORDER BY "EXERCISE_NAME", "EXERCISE_ID" LIMIT 12;

\echo == 2d: how many exercises are matched by tags only for 'dév'
SELECT count(*) AS matched_today,
       count(*) FILTER (WHERE "EXERCISE_NAME" ILIKE '%dév%') AS by_name,
       count(*) FILTER (WHERE "EXERCISE_NAME" NOT ILIKE '%dév%') AS not_by_name
FROM "EXERCISES"
WHERE "EXERCISE_IS_ACTIVE" AND ("EXERCISE_NAME" ILIKE '%dév%' OR "EXERCISE_NAME_EN" ILIKE '%dév%' OR "EXERCISE_SEARCH_TAGS" ILIKE '%dév%');

\echo == 2d: what search tags contain (most frequent tokens)
SELECT trim(t) AS tag, count(*) FROM "EXERCISES", unnest(string_to_array("EXERCISE_SEARCH_TAGS", ';')) t
GROUP BY 1 ORDER BY 2 DESC, 1 LIMIT 25;
\echo == 2d: number of exercises by number of tags
SELECT array_length(string_to_array("EXERCISE_SEARCH_TAGS", ';'),1) AS n_tags, count(*) FROM "EXERCISES" GROUP BY 1 ORDER BY 1;
