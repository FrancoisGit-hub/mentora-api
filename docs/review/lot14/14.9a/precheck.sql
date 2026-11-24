-- Lot 14.9a precheck: counts only, read only, no row content, no local-only assumption.
-- Run on mentora_db (local) and on mentora_prod (VPS) with the same script.
BEGIN READ ONLY;

-- 1. Circuit mode column, count by value.
SELECT 'PROGRAM_CIRCUITS.PROGRAM_CIRCUIT_MODE' AS "location",
       c."PROGRAM_CIRCUIT_MODE" AS "value",
       count(*) AS "n"
FROM "PROGRAM_CIRCUITS" c
GROUP BY c."PROGRAM_CIRCUIT_MODE"
ORDER BY 2;

-- 2. Rows and tokens in the two jsonb columns (jsonb::text form, key "mode").
SELECT 'PROGRAM_TEMPLATES.PROGRAM_TEMPLATE_BODY' AS "location",
       count(*) AS "rows",
       count(*) FILTER (WHERE t."PROGRAM_TEMPLATE_BODY"::text LIKE '%"mode": "STANDARD"%') AS "rows_with_mode_standard",
       sum((length(t."PROGRAM_TEMPLATE_BODY"::text) - length(replace(t."PROGRAM_TEMPLATE_BODY"::text, '"mode": "STANDARD"', ''))) / length('"mode": "STANDARD"')) AS "mode_standard_tokens",
       sum((length(t."PROGRAM_TEMPLATE_BODY"::text) - length(replace(t."PROGRAM_TEMPLATE_BODY"::text, '"mode": "SERIE_SIMPLE"', ''))) / length('"mode": "SERIE_SIMPLE"')) AS "mode_serie_simple_tokens",
       sum((length(t."PROGRAM_TEMPLATE_BODY"::text) - length(replace(t."PROGRAM_TEMPLATE_BODY"::text, '"mode": "INTERVAL"', ''))) / length('"mode": "INTERVAL"')) AS "mode_interval_tokens",
       sum((length(t."PROGRAM_TEMPLATE_BODY"::text) - length(replace(t."PROGRAM_TEMPLATE_BODY"::text, '"nature": "STANDARD"', ''))) / length('"nature": "STANDARD"')) AS "nature_standard_tokens",
       sum((length(t."PROGRAM_TEMPLATE_BODY"::text) - length(replace(t."PROGRAM_TEMPLATE_BODY"::text, '"nature": "EVALUATION"', ''))) / length('"nature": "EVALUATION"')) AS "nature_evaluation_tokens"
FROM "PROGRAM_TEMPLATES" t

UNION ALL

SELECT 'SESSION_TEMPLATES.SESSION_TEMPLATE_CONTENT',
       count(*),
       count(*) FILTER (WHERE s."SESSION_TEMPLATE_CONTENT"::text LIKE '%"mode": "STANDARD"%'),
       sum((length(s."SESSION_TEMPLATE_CONTENT"::text) - length(replace(s."SESSION_TEMPLATE_CONTENT"::text, '"mode": "STANDARD"', ''))) / length('"mode": "STANDARD"')),
       sum((length(s."SESSION_TEMPLATE_CONTENT"::text) - length(replace(s."SESSION_TEMPLATE_CONTENT"::text, '"mode": "SERIE_SIMPLE"', ''))) / length('"mode": "SERIE_SIMPLE"')),
       sum((length(s."SESSION_TEMPLATE_CONTENT"::text) - length(replace(s."SESSION_TEMPLATE_CONTENT"::text, '"mode": "INTERVAL"', ''))) / length('"mode": "INTERVAL"')),
       sum((length(s."SESSION_TEMPLATE_CONTENT"::text) - length(replace(s."SESSION_TEMPLATE_CONTENT"::text, '"nature": "STANDARD"', ''))) / length('"nature": "STANDARD"')),
       sum((length(s."SESSION_TEMPLATE_CONTENT"::text) - length(replace(s."SESSION_TEMPLATE_CONTENT"::text, '"nature": "EVALUATION"', ''))) / length('"nature": "EVALUATION"'))
FROM "SESSION_TEMPLATES" s;

-- 3. Session nature of session templates is a column, not a jsonb key: counts by value, must never change.
SELECT 'SESSION_TEMPLATES.SESSION_TEMPLATE_NATURE' AS "location",
       s."SESSION_TEMPLATE_NATURE" AS "value",
       count(*) AS "n"
FROM "SESSION_TEMPLATES" s
GROUP BY s."SESSION_TEMPLATE_NATURE"
ORDER BY 2;

ROLLBACK;
