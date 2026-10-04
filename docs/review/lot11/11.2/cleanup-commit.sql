-- Lot 11.2 local cleanup. Scope: name prefix 'l112-' AND created >= run start (UTC).
-- No previous member program existed (PROGRAMS count was 0 before the run), so nothing to restore.
BEGIN;
\echo before
SELECT (SELECT count(*) FROM "PROGRAMS" WHERE "PROGRAM_NAME" LIKE 'l112-%' AND "PROGRAM_CREATED_DATE" >= '2026-10-04T18:12:05Z') AS fixture_programs,
       (SELECT count(*) FROM "PROGRAM_TEMPLATES" WHERE "PROGRAM_TEMPLATE_NAME" LIKE 'l112-%' AND "PROGRAM_TEMPLATE_CREATED_DATE" >= '2026-10-04T18:12:05Z') AS fixture_templates,
       (SELECT count(*) FROM "PROGRAMS") AS programs_total, (SELECT count(*) FROM "PROGRAM_TEMPLATES") AS templates_total;
DELETE FROM "PROGRAMS" WHERE "PROGRAM_NAME" LIKE 'l112-%' AND "PROGRAM_CREATED_DATE" >= '2026-10-04T18:12:05Z';
DELETE FROM "PROGRAM_TEMPLATES" WHERE "PROGRAM_TEMPLATE_NAME" LIKE 'l112-%' AND "PROGRAM_TEMPLATE_CREATED_DATE" >= '2026-10-04T18:12:05Z';
\echo after
SELECT (SELECT count(*) FROM "PROGRAMS") AS programs_total, (SELECT count(*) FROM "PROGRAM_TEMPLATES") AS templates_total,
       (SELECT count(*) FROM "PROGRAM_BLOCKS") AS blocks, (SELECT count(*) FROM "PROGRAM_SESSIONS") AS sessions,
       (SELECT count(*) FROM "PROGRAM_CIRCUITS") AS circuits, (SELECT count(*) FROM "PROGRAM_EXERCISES") AS exercises;
COMMIT;
