-- Lot 14.9a: proof of the Up SQL on the two locations that had no local data before the migration
-- (PROGRAM_CIRCUITS column, SESSION_TEMPLATES jsonb). Local only, always ends with ROLLBACK.
-- Counts and booleans only, no row content.
BEGIN;

-- Setup inside the transaction: one l149a circuit goes back to STANDARD, one l149a session template
-- content gets one circuit with the exact token "mode": "STANDARD".
UPDATE "PROGRAM_CIRCUITS" SET "PROGRAM_CIRCUIT_MODE" = 'STANDARD' WHERE "PROGRAM_CIRCUIT_NAME" = 'l149a amrap';

UPDATE "SESSION_TEMPLATES"
SET "SESSION_TEMPLATE_CONTENT" = replace("SESSION_TEMPLATE_CONTENT"::text, '"mode": "LIBRE"', '"mode": "STANDARD"')::jsonb
WHERE "SESSION_TEMPLATE_NAME" = 'l149a again LIBRE bare';

SELECT 'BEFORE UP' AS "step",
       (SELECT count(*) FROM "PROGRAM_CIRCUITS" WHERE "PROGRAM_CIRCUIT_MODE" = 'STANDARD') AS "circuits_standard",
       (SELECT count(*) FROM "SESSION_TEMPLATES" WHERE "SESSION_TEMPLATE_CONTENT"::text LIKE '%"mode": "STANDARD"%') AS "session_templates_with_standard_token",
       (SELECT count(*) FROM "SESSION_TEMPLATES" WHERE "SESSION_TEMPLATE_NATURE" = 'STANDARD') AS "session_nature_standard",
       (SELECT count(*) FROM "SESSION_TEMPLATES" WHERE "SESSION_TEMPLATE_CONTENT"::text LIKE '%"nature": "STANDARD"%') AS "nature_tokens_rows";

-- The three UPDATE statements copied verbatim from Up() of 20261124221921_Lot14_9a_CircuitModes.cs.
UPDATE "PROGRAM_CIRCUITS"
SET "PROGRAM_CIRCUIT_MODE" = 'SERIE_SIMPLE'
WHERE "PROGRAM_CIRCUIT_MODE" = 'STANDARD';

UPDATE "PROGRAM_TEMPLATES"
SET "PROGRAM_TEMPLATE_BODY" = replace("PROGRAM_TEMPLATE_BODY"::text,
        '"mode": "STANDARD"', '"mode": "SERIE_SIMPLE"')::jsonb
WHERE "PROGRAM_TEMPLATE_BODY"::text LIKE '%"mode": "STANDARD"%';

UPDATE "SESSION_TEMPLATES"
SET "SESSION_TEMPLATE_CONTENT" = replace("SESSION_TEMPLATE_CONTENT"::text,
        '"mode": "STANDARD"', '"mode": "SERIE_SIMPLE"')::jsonb
WHERE "SESSION_TEMPLATE_CONTENT"::text LIKE '%"mode": "STANDARD"%';

SELECT 'AFTER UP' AS "step",
       (SELECT count(*) FROM "PROGRAM_CIRCUITS" WHERE "PROGRAM_CIRCUIT_MODE" = 'STANDARD') AS "circuits_standard",
       (SELECT count(*) FROM "SESSION_TEMPLATES" WHERE "SESSION_TEMPLATE_CONTENT"::text LIKE '%"mode": "STANDARD"%') AS "session_templates_with_standard_token",
       (SELECT count(*) FROM "SESSION_TEMPLATES" WHERE "SESSION_TEMPLATE_NATURE" = 'STANDARD') AS "session_nature_standard",
       (SELECT count(*) FROM "SESSION_TEMPLATES" WHERE "SESSION_TEMPLATE_CONTENT"::text LIKE '%"nature": "STANDARD"%') AS "nature_tokens_rows";

SELECT 'the two rewritten rows' AS "check",
       (SELECT "PROGRAM_CIRCUIT_MODE" FROM "PROGRAM_CIRCUITS" WHERE "PROGRAM_CIRCUIT_NAME" = 'l149a amrap') AS "circuit_mode",
       (SELECT "SESSION_TEMPLATE_CONTENT"::text LIKE '%"mode": "SERIE_SIMPLE"%' FROM "SESSION_TEMPLATES" WHERE "SESSION_TEMPLATE_NAME" = 'l149a again LIBRE bare') AS "session_template_has_serie_simple_token",
       (SELECT "SESSION_TEMPLATE_CONTENT"::text LIKE '%"mode": "LIBRE"%' FROM "SESSION_TEMPLATES" WHERE "SESSION_TEMPLATE_NAME" = 'l149a again LIBRE bare') AS "session_template_still_libre";

ROLLBACK;
