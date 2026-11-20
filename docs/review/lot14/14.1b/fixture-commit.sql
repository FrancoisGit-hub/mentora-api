-- Lot 14.1b fixture (local DB only). Fixed ids 141b0000-...-b1 and b2, prefix l141b-.
-- Coach B (user + coach profile, so the startup seeders create its parameters and programs).
-- Run start (read in the database before the first write): 2026-11-20 18:36:17 UTC.
BEGIN;

INSERT INTO "USERS"
  ("USER_ID", "USER_CREATED_DATE", "USER_EMAIL", "USER_LOGIN", "USER_IS_ENABLED", "USER_ROLE")
VALUES
  ('141b0000-0000-4000-8000-0000000000b1', now(), 'l141b-coachb@mentora.local', 'l141b-coachb@mentora.local', true, 'Coach');

INSERT INTO "COACHES"
  ("COACH_ID", "COACH_FIRST_NAME", "COACH_LAST_NAME", "COACH_CREATED_DATE", "COACH_IS_ACTIVE", "USER_ID")
VALUES
  ('141b0000-0000-4000-8000-0000000000b2', 'l141b-Coach', 'l141b-B', now(), true, '141b0000-0000-4000-8000-0000000000b1');

SELECT 'USERS' AS tbl, count(*) FROM "USERS" WHERE "USER_ID" = '141b0000-0000-4000-8000-0000000000b1'
UNION ALL SELECT 'COACHES', count(*) FROM "COACHES" WHERE "COACH_ID" = '141b0000-0000-4000-8000-0000000000b2';

COMMIT;
