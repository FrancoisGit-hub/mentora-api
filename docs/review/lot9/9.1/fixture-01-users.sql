-- Lot 9.1 - fixture: coach B (no product/OTP signup path exists, so inserted directly)
-- and member M2 (not attached to coach A).
--
-- Fixed UUIDs:
--   Coach B USER_ID   = 1bbac370-5329-4ff9-a9dc-7170eb419104
--   Coach B COACH_ID  = c0a01743-e580-4597-a834-54011da05a17  (coachb.lot9@mentora.fr)
--   Member M2 USER_ID = de53e500-f220-42fd-a7d7-7f34ad55d480
--   Member M2 MEMBER_ID = ef9296e7-1f7b-47ec-b97e-7dbf7c5d2495  (memberb.lot9@mentora.fr, NOT attached to coach A)

INSERT INTO "USERS" (
    "USER_ID", "USER_CREATED_DATE", "USER_EMAIL", "USER_IS_ENABLED", "USER_ROLE"
) VALUES (
    '1bbac370-5329-4ff9-a9dc-7170eb419104', now(), 'coachb.lot9@mentora.fr', true, 'COACH'
), (
    'de53e500-f220-42fd-a7d7-7f34ad55d480', now(), 'memberb.lot9@mentora.fr', true, 'MEMBER'
);

INSERT INTO "COACHES" (
    "COACH_ID", "COACH_FIRST_NAME", "COACH_LAST_NAME", "COACH_CREATED_DATE", "COACH_IS_ACTIVE", "USER_ID"
) VALUES (
    'c0a01743-e580-4597-a834-54011da05a17', 'Lot9', 'CoachB', now(), true, '1bbac370-5329-4ff9-a9dc-7170eb419104'
);

INSERT INTO "MEMBERS" (
    "MEMBER_ID", "MEMBER_FIRST_NAME", "MEMBER_LAST_NAME", "MEMBER_CREATED_DATE",
    "MEMBER_IS_ACTIVE", "MEMBER_HAS_ACTIVATED", "USER_ID"
) VALUES (
    'ef9296e7-1f7b-47ec-b97e-7dbf7c5d2495', 'Lot9', 'MemberB', now(), true, true, 'de53e500-f220-42fd-a7d7-7f34ad55d480'
);

-- Confirm
SELECT "COACH_ID", "COACH_FIRST_NAME", "COACH_LAST_NAME" FROM "COACHES" WHERE "COACH_ID" = 'c0a01743-e580-4597-a834-54011da05a17';
SELECT "MEMBER_ID", "MEMBER_FIRST_NAME", "MEMBER_LAST_NAME" FROM "MEMBERS" WHERE "MEMBER_ID" = 'ef9296e7-1f7b-47ec-b97e-7dbf7c5d2495';
