-- =============================================================================
-- seed-test-members.sql
-- Purpose  : Insert 4 additional test MEMBERS linked to coach Federico Orsi
--            so the team can test the real OTP email flow with personal inboxes.
-- Created  : 2026-06-19
-- Branch   : feature/lot-3.0_member-catalog
-- Idempotent: YES — each INSERT uses ON CONFLICT … DO NOTHING.
--             Safe to re-run on both local and VPS without creating duplicates
--             or raising errors.
-- Environments:
--   Local : Host=localhost  Port=5433  Database=mentora_db
--   VPS   : Host=87.106.108.91  Port=5432  Database=mentora_db (or mentora_prod — confirm with François)
-- Do NOT execute from code — run manually via DBeaver.
-- =============================================================================
--
-- MEMBER_COACHES note:
--   The MEMBER_COACHES table has its own UUID PK (MEMBER_COACH_ID) AND a unique
--   index on (MEMBER_ID, COACH_ID).  We supply explicit UUIDs for MEMBER_COACH_ID
--   and use ON CONFLICT ("MEMBER_ID", "COACH_ID") DO NOTHING as the conflict
--   target (the unique index), which is the correct idempotency guard.
-- =============================================================================

BEGIN;

-- ---------------------------------------------------------------------------
-- 1. USERS
-- ---------------------------------------------------------------------------

INSERT INTO "USERS"
    ("USER_ID", "USER_EMAIL", "USER_ROLE", "USER_IS_ENABLED", "USER_CREATED_DATE",
     "USER_MODIFICATION_DATE", "USER_PSW", "USER_LOGIN", "USER_DISABLED_DATE")
VALUES
    ('dddddddd-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
     'gittenait.francois@gmail.com',
     'MEMBER', true, NOW(), NULL, NULL, NULL, NULL)
ON CONFLICT ("USER_EMAIL") DO NOTHING;

INSERT INTO "USERS"
    ("USER_ID", "USER_EMAIL", "USER_ROLE", "USER_IS_ENABLED", "USER_CREATED_DATE",
     "USER_MODIFICATION_DATE", "USER_PSW", "USER_LOGIN", "USER_DISABLED_DATE")
VALUES
    ('dddddddd-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
     'pierrejordans@gmail.com',
     'MEMBER', true, NOW(), NULL, NULL, NULL, NULL)
ON CONFLICT ("USER_EMAIL") DO NOTHING;

INSERT INTO "USERS"
    ("USER_ID", "USER_EMAIL", "USER_ROLE", "USER_IS_ENABLED", "USER_CREATED_DATE",
     "USER_MODIFICATION_DATE", "USER_PSW", "USER_LOGIN", "USER_DISABLED_DATE")
VALUES
    ('dddddddd-cccc-cccc-cccc-cccccccccccc',
     'federico.orsi4@gmail.com',
     'MEMBER', true, NOW(), NULL, NULL, NULL, NULL)
ON CONFLICT ("USER_EMAIL") DO NOTHING;

INSERT INTO "USERS"
    ("USER_ID", "USER_EMAIL", "USER_ROLE", "USER_IS_ENABLED", "USER_CREATED_DATE",
     "USER_MODIFICATION_DATE", "USER_PSW", "USER_LOGIN", "USER_DISABLED_DATE")
VALUES
    ('dddddddd-eeee-eeee-eeee-eeeeeeeeeeee',
     'francoispietri.cor@gmail.com',
     'MEMBER', true, NOW(), NULL, NULL, NULL, NULL)
ON CONFLICT ("USER_EMAIL") DO NOTHING;

-- ---------------------------------------------------------------------------
-- 2. MEMBERS
-- ---------------------------------------------------------------------------

INSERT INTO "MEMBERS"
    ("MEMBER_ID", "USER_ID", "MEMBER_FIRST_NAME", "MEMBER_LAST_NAME",
     "MEMBER_IS_ACTIVE", "MEMBER_HAS_ACTIVATED", "MEMBER_CREATED_DATE",
     "MEMBER_PHONE", "MEMBER_ACTIVATION_DATE")
VALUES
    ('dddddddd-aaaa-bbbb-aaaa-aaaaaaaaaaaa',
     'dddddddd-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
     'François', 'Gittenait',
     true, true, NOW(), NULL, NULL)
ON CONFLICT ("MEMBER_ID") DO NOTHING;

INSERT INTO "MEMBERS"
    ("MEMBER_ID", "USER_ID", "MEMBER_FIRST_NAME", "MEMBER_LAST_NAME",
     "MEMBER_IS_ACTIVE", "MEMBER_HAS_ACTIVATED", "MEMBER_CREATED_DATE",
     "MEMBER_PHONE", "MEMBER_ACTIVATION_DATE")
VALUES
    ('dddddddd-bbbb-cccc-bbbb-bbbbbbbbbbbb',
     'dddddddd-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
     'Pierre-Jordan', 'Mentora',
     true, true, NOW(), NULL, NULL)
ON CONFLICT ("MEMBER_ID") DO NOTHING;

INSERT INTO "MEMBERS"
    ("MEMBER_ID", "USER_ID", "MEMBER_FIRST_NAME", "MEMBER_LAST_NAME",
     "MEMBER_IS_ACTIVE", "MEMBER_HAS_ACTIVATED", "MEMBER_CREATED_DATE",
     "MEMBER_PHONE", "MEMBER_ACTIVATION_DATE")
VALUES
    ('dddddddd-cccc-dddd-cccc-cccccccccccc',
     'dddddddd-cccc-cccc-cccc-cccccccccccc',
     'Federico', 'OrsiPerso',
     true, true, NOW(), NULL, NULL)
ON CONFLICT ("MEMBER_ID") DO NOTHING;

INSERT INTO "MEMBERS"
    ("MEMBER_ID", "USER_ID", "MEMBER_FIRST_NAME", "MEMBER_LAST_NAME",
     "MEMBER_IS_ACTIVE", "MEMBER_HAS_ACTIVATED", "MEMBER_CREATED_DATE",
     "MEMBER_PHONE", "MEMBER_ACTIVATION_DATE")
VALUES
    ('dddddddd-eeee-ffff-eeee-eeeeeeeeeeee',
     'dddddddd-eeee-eeee-eeee-eeeeeeeeeeee',
     'François', 'Pietri',
     true, true, NOW(), NULL, NULL)
ON CONFLICT ("MEMBER_ID") DO NOTHING;

-- ---------------------------------------------------------------------------
-- 3. MEMBER_COACHES  (all linked to Federico Orsi as primary coach)
-- ---------------------------------------------------------------------------

INSERT INTO "MEMBER_COACHES"
    ("MEMBER_COACH_ID", "MEMBER_ID", "COACH_ID", "IS_PRIMARY", "STARTED_AT")
VALUES
    ('dddddddd-aaaa-cccc-aaaa-aaaaaaaaaaaa',
     'dddddddd-aaaa-bbbb-aaaa-aaaaaaaaaaaa',
     'cebb9b4d-00ee-4826-a8fe-58e8ab3b325b',
     true, NOW())
ON CONFLICT ("MEMBER_ID", "COACH_ID") DO NOTHING;

INSERT INTO "MEMBER_COACHES"
    ("MEMBER_COACH_ID", "MEMBER_ID", "COACH_ID", "IS_PRIMARY", "STARTED_AT")
VALUES
    ('dddddddd-bbbb-dddd-bbbb-bbbbbbbbbbbb',
     'dddddddd-bbbb-cccc-bbbb-bbbbbbbbbbbb',
     'cebb9b4d-00ee-4826-a8fe-58e8ab3b325b',
     true, NOW())
ON CONFLICT ("MEMBER_ID", "COACH_ID") DO NOTHING;

INSERT INTO "MEMBER_COACHES"
    ("MEMBER_COACH_ID", "MEMBER_ID", "COACH_ID", "IS_PRIMARY", "STARTED_AT")
VALUES
    ('dddddddd-cccc-eeee-cccc-cccccccccccc',
     'dddddddd-cccc-dddd-cccc-cccccccccccc',
     'cebb9b4d-00ee-4826-a8fe-58e8ab3b325b',
     true, NOW())
ON CONFLICT ("MEMBER_ID", "COACH_ID") DO NOTHING;

INSERT INTO "MEMBER_COACHES"
    ("MEMBER_COACH_ID", "MEMBER_ID", "COACH_ID", "IS_PRIMARY", "STARTED_AT")
VALUES
    ('dddddddd-eeee-aaaa-eeee-eeeeeeeeeeee',
     'dddddddd-eeee-ffff-eeee-eeeeeeeeeeee',
     'cebb9b4d-00ee-4826-a8fe-58e8ab3b325b',
     true, NOW())
ON CONFLICT ("MEMBER_ID", "COACH_ID") DO NOTHING;

COMMIT;

-- =============================================================================
-- Verification queries — paste into DBeaver after running the script
-- =============================================================================

-- 1. Check USERS
-- SELECT "USER_ID", "USER_EMAIL", "USER_ROLE", "USER_IS_ENABLED"
--   FROM "USERS"
--  WHERE "USER_EMAIL" IN (
--          'gittenait.francois@gmail.com',
--          'pierrejordans@gmail.com',
--          'federico.orsi4@gmail.com',
--          'francoispietri.cor@gmail.com'
--        )
--  ORDER BY "USER_EMAIL";

-- 2. Check MEMBERS
-- SELECT m."MEMBER_ID", m."MEMBER_FIRST_NAME", m."MEMBER_LAST_NAME",
--        m."MEMBER_IS_ACTIVE", m."MEMBER_HAS_ACTIVATED", u."USER_EMAIL"
--   FROM "MEMBERS" m
--   JOIN "USERS" u ON u."USER_ID" = m."USER_ID"
--  WHERE u."USER_EMAIL" IN (
--          'gittenait.francois@gmail.com',
--          'pierrejordans@gmail.com',
--          'federico.orsi4@gmail.com',
--          'francoispietri.cor@gmail.com'
--        )
--  ORDER BY u."USER_EMAIL";

-- 3. Check MEMBER_COACHES
-- SELECT mc."MEMBER_COACH_ID", mc."MEMBER_ID", mc."COACH_ID",
--        mc."IS_PRIMARY", mc."STARTED_AT", u."USER_EMAIL"
--   FROM "MEMBER_COACHES" mc
--   JOIN "MEMBERS" m ON m."MEMBER_ID" = mc."MEMBER_ID"
--   JOIN "USERS"   u ON u."USER_ID"   = m."USER_ID"
--  WHERE u."USER_EMAIL" IN (
--          'gittenait.francois@gmail.com',
--          'pierrejordans@gmail.com',
--          'federico.orsi4@gmail.com',
--          'francoispietri.cor@gmail.com'
--        )
--    AND mc."COACH_ID" = 'cebb9b4d-00ee-4826-a8fe-58e8ab3b325b'
--  ORDER BY u."USER_EMAIL";
