-- Lot 9.5 - fixture: member M3 attached to coach A (coach@mentora.fr), used to assign every
-- Mentora template (V3). member@mentora.fr is NOT used: assigning archives the member's active program.
--
-- Fixed UUIDs:
--   M3 USER_ID          = 5b4e9c1a-0c1d-4a55-9d3e-950500000001  (memberm3.lot95@mentora.fr)
--   M3 MEMBER_ID        = 5b4e9c1a-0c1d-4a55-9d3e-950500000002
--   MEMBER_COACH_ID     = 5b4e9c1a-0c1d-4a55-9d3e-950500000003
--   Coach A COACH_ID    = e268a512-84d6-4e27-b5c0-1d6c08a47190  (existing, coach@mentora.fr)

INSERT INTO "USERS" (
    "USER_ID", "USER_CREATED_DATE", "USER_EMAIL", "USER_IS_ENABLED", "USER_ROLE"
) VALUES (
    '5b4e9c1a-0c1d-4a55-9d3e-950500000001', now(), 'memberm3.lot95@mentora.fr', true, 'MEMBER'
);

INSERT INTO "MEMBERS" (
    "MEMBER_ID", "MEMBER_FIRST_NAME", "MEMBER_LAST_NAME", "MEMBER_CREATED_DATE",
    "MEMBER_IS_ACTIVE", "MEMBER_HAS_ACTIVATED", "USER_ID"
) VALUES (
    '5b4e9c1a-0c1d-4a55-9d3e-950500000002', 'Lot95', 'MemberM3', now(), true, true, '5b4e9c1a-0c1d-4a55-9d3e-950500000001'
);

INSERT INTO "MEMBER_COACHES" (
    "MEMBER_COACH_ID", "MEMBER_ID", "COACH_ID", "IS_PRIMARY", "STARTED_AT"
) VALUES (
    '5b4e9c1a-0c1d-4a55-9d3e-950500000003', '5b4e9c1a-0c1d-4a55-9d3e-950500000002',
    'e268a512-84d6-4e27-b5c0-1d6c08a47190', true, now()
);

SELECT "MEMBER_ID", "MEMBER_FIRST_NAME", "MEMBER_LAST_NAME" FROM "MEMBERS" WHERE "MEMBER_ID" = '5b4e9c1a-0c1d-4a55-9d3e-950500000002';
SELECT "MEMBER_COACH_ID", "MEMBER_ID", "COACH_ID", "IS_PRIMARY" FROM "MEMBER_COACHES" WHERE "MEMBER_COACH_ID" = '5b4e9c1a-0c1d-4a55-9d3e-950500000003';
