-- Lot 9.4 - fixture: coach B with one active exercise (visibility tests V3).
-- Coach B is NOT attached to any member, so member@mentora.fr must never see its exercise.
--
-- Fixed UUIDs:
--   Coach B USER_ID     = 5b4e9c1a-0c1d-4a55-9d3e-940400000001  (coachb.lot94@mentora.fr)
--   Coach B COACH_ID    = 5b4e9c1a-0c1d-4a55-9d3e-940400000002
--   Coach B EXERCISE_ID = 5b4e9c1a-0c1d-4a55-9d3e-940400000003  ("Lot94 exercice coach B")

INSERT INTO "USERS" (
    "USER_ID", "USER_CREATED_DATE", "USER_EMAIL", "USER_IS_ENABLED", "USER_ROLE"
) VALUES (
    '5b4e9c1a-0c1d-4a55-9d3e-940400000001', now(), 'coachb.lot94@mentora.fr', true, 'COACH'
);

INSERT INTO "COACHES" (
    "COACH_ID", "COACH_FIRST_NAME", "COACH_LAST_NAME", "COACH_CREATED_DATE", "COACH_IS_ACTIVE", "USER_ID"
) VALUES (
    '5b4e9c1a-0c1d-4a55-9d3e-940400000002', 'Lot94', 'CoachB', now(), true, '5b4e9c1a-0c1d-4a55-9d3e-940400000001'
);

INSERT INTO "EXERCISES" (
    "EXERCISE_ID", "EXERCISE_COACH_ID", "EXERCISE_NAME", "EXERCISE_MUSCLE_GROUP", "EXERCISE_EQUIPMENT",
    "EXERCISE_IS_POLYARTICULAR", "EXERCISE_IS_ACTIVE", "EXERCISE_CREATED_DATE", "EXERCISE_UPDATED_DATE"
) VALUES (
    '5b4e9c1a-0c1d-4a55-9d3e-940400000003', '5b4e9c1a-0c1d-4a55-9d3e-940400000002', 'Lot94 exercice coach B',
    'CHEST', 'BODYWEIGHT', false, true, now(), now()
);

SELECT "COACH_ID", "COACH_FIRST_NAME" FROM "COACHES" WHERE "COACH_ID" = '5b4e9c1a-0c1d-4a55-9d3e-940400000002';
SELECT "EXERCISE_ID", "EXERCISE_NAME", "EXERCISE_COACH_ID" FROM "EXERCISES" WHERE "EXERCISE_ID" = '5b4e9c1a-0c1d-4a55-9d3e-940400000003';
