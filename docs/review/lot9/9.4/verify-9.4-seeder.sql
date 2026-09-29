-- Lot 9.4 - V9 seeder checks (read-only).

\echo '== "Sans matériel" row'
SELECT "EQUIPMENT_ID", "EQUIPMENT_SOURCE_CODE", "EQUIPMENT_NAME", "EQUIPMENT_CATEGORY", "EQUIPMENT_SUBCATEGORY", "EQUIPMENT_LEGACY_CODE", "EQUIPMENT_IS_ACTIVE"
FROM "EQUIPMENT" WHERE "EQUIPMENT_NAME" = 'Sans matériel';

\echo '== EQUIPMENT rows by origin'
SELECT CASE WHEN "EQUIPMENT_SOURCE_CODE" IS NOT NULL THEN 'MATERIELS sheet'
            WHEN "EQUIPMENT_CATEGORY" = 'Autre' THEN 'created from exercises (7.2)'
            ELSE 'Sans matériel (7.1)' END AS origin,
       count(*) AS n
FROM "EQUIPMENT" GROUP BY 1 ORDER BY 1;

\echo '== EQUIPMENT rows created by 7.2 (category Autre)'
SELECT "EQUIPMENT_NAME", "EQUIPMENT_LEGACY_CODE",
       (SELECT count(*) FROM "EXERCISES" x WHERE "EQUIPMENT_ID" IN (x."EXERCISE_PRIMARY_EQUIPMENT_ID", x."EXERCISE_SECONDARY_EQUIPMENT_ID")) AS exercises_using_it
FROM "EQUIPMENT" WHERE "EQUIPMENT_SOURCE_CODE" IS NULL AND "EQUIPMENT_CATEGORY" = 'Autre' ORDER BY "EQUIPMENT_NAME";

\echo '== Exercises linked to "Sans matériel" (primary), split'
SELECT CASE WHEN x."EXERCISE_SOURCE_CODE" IS NOT NULL THEN 'sheet exercises'
            ELSE 'historical BODYWEIGHT (not in the sheet)' END AS kind,
       count(*) AS n
FROM "EXERCISES" x
WHERE x."EXERCISE_PRIMARY_EQUIPMENT_ID" = (SELECT "EQUIPMENT_ID" FROM "EQUIPMENT" WHERE "EQUIPMENT_NAME" = 'Sans matériel')
GROUP BY 1 ORDER BY 1;

\echo '== "Sans matériel" as secondary equipment (must be 0)'
SELECT count(*) AS n FROM "EXERCISES"
WHERE "EXERCISE_SECONDARY_EQUIPMENT_ID" = (SELECT "EQUIPMENT_ID" FROM "EQUIPMENT" WHERE "EQUIPMENT_NAME" = 'Sans matériel');

\echo '== Active sheet exercises with NULL primary equipment (must be 0)'
SELECT count(*) AS n FROM "EXERCISES"
WHERE "EXERCISE_SOURCE_CODE" IS NOT NULL AND "EXERCISE_IS_ACTIVE" AND "EXERCISE_PRIMARY_EQUIPMENT_ID" IS NULL;

\echo '== Historical Mentora exercises (no source code): legacy equipment vs primary equipment'
SELECT "EXERCISE_EQUIPMENT" AS legacy_equipment,
       count(*) AS n,
       count(*) FILTER (WHERE "EXERCISE_PRIMARY_EQUIPMENT_ID" IS NULL) AS primary_null,
       count(*) FILTER (WHERE "EXERCISE_PRIMARY_EQUIPMENT_ID" = (SELECT "EQUIPMENT_ID" FROM "EQUIPMENT" WHERE "EQUIPMENT_NAME" = 'Sans matériel')) AS primary_sans_materiel
FROM "EXERCISES" WHERE "EXERCISE_SOURCE_CODE" IS NULL AND "EXERCISE_COACH_ID" IS NULL
GROUP BY 1 ORDER BY 1;

\echo '== Coach-owned exercises: catalog columns untouched'
SELECT "EXERCISE_NAME", "EXERCISE_PRIMARY_EQUIPMENT_ID" IS NULL AS primary_equipment_null, "EXERCISE_SOURCE_CODE" IS NULL AS source_code_null
FROM "EXERCISES" WHERE "EXERCISE_COACH_ID" IS NOT NULL ORDER BY 1;

\echo '== Counts'
SELECT (SELECT count(*) FROM "EXERCISES") AS ex, (SELECT count(*) FROM "EQUIPMENT") AS eq,
       (SELECT count(*) FROM "OBJECTIVES") AS obj, (SELECT count(*) FROM "EXERCISE_OBJECTIVES") AS exobj,
       (SELECT count(*) FROM "MUSCLE_GROUPS") AS mg;

\echo '== Integrity'
SELECT 'dup equipment name (ci)' AS chk, count(*) AS n FROM (SELECT lower("EQUIPMENT_NAME") FROM "EQUIPMENT" GROUP BY 1 HAVING count(*) > 1) d
UNION ALL SELECT 'dup source code', count(*) FROM (SELECT "EXERCISE_SOURCE_CODE" FROM "EXERCISES" WHERE "EXERCISE_SOURCE_CODE" IS NOT NULL GROUP BY 1 HAVING count(*) > 1) d;
