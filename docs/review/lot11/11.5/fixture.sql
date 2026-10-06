-- Lot 11.5 runtime fixtures. ONE transaction, fixed UUIDs, every label/code prefixed "l115-".
-- Coach B has no password and no login secret: its JWT comes from an OTP login (code read from the
-- local API log without printing it), same rule as the earlier lots.
-- After the COMMIT, start the API once so the startup seeders create coach B's parameter rows.
--
-- UUIDs:  user B     11500000-0000-4000-8000-0000000000b1
--         coach B    11500000-0000-4000-8000-0000000000b2
--         exercise B 11500000-0000-4000-8000-0000000000e1
--         templates  11500000-0000-4000-8000-0000000000a1 (BRAS), a2 (JAMBES), a3 (OFF, inactive)
-- Catalog exercises (real rows, EXERCISE_COACH_ID NULL, active), copied from the DB:
--   72b9431c-9086-46ab-ba0e-2671f69702da  Abduction hanche à la poulie
--   7062b1c9-35e8-436e-9992-6c646af29851  Abduction hanche allongée sur le côté
--   97eb234d-05f5-4de8-b91a-e3ee3546eeab  Abduction hanche avec élastique debout

BEGIN;

-- Coach B: user (shape of coach A's row: role 'Coach', enabled) + coach.
INSERT INTO "USERS" ("USER_ID", "USER_CREATED_DATE", "USER_MODIFICATION_DATE", "USER_EMAIL", "USER_PSW",
                     "USER_LOGIN", "USER_IS_ENABLED", "USER_DISABLED_DATE", "USER_ROLE",
                     "USER_DELETION_REASON", "USER_DELETION_REQUESTED_DATE")
VALUES ('11500000-0000-4000-8000-0000000000b1', now(), NULL, 'l115-coachb@mentora.fr', NULL,
        'l115-coachb', true, NULL, 'Coach', NULL, NULL);

INSERT INTO "COACHES" ("COACH_ID", "COACH_FIRST_NAME", "COACH_LAST_NAME", "COACH_PHONE", "COACH_CREATED_DATE",
                       "COACH_IS_ACTIVE", "USER_ID", "COACH_IN_PERSON_LOCATIONS", "COACH_OFFERS_IN_PERSON_SOLO",
                       "COACH_OFFERS_VISIO")
VALUES ('11500000-0000-4000-8000-0000000000b2', 'l115-Coach', 'B', NULL, now(), true,
        '11500000-0000-4000-8000-0000000000b1', '[]'::jsonb, false, false);

-- One exercise owned by coach B (catalog-like row with EXERCISE_COACH_ID = coach B).
INSERT INTO "EXERCISES" ("EXERCISE_ID", "EXERCISE_COACH_ID", "EXERCISE_NAME", "EXERCISE_MUSCLE_GROUP",
                         "EXERCISE_EQUIPMENT", "EXERCISE_IS_POLYARTICULAR", "EXERCISE_IS_ACTIVE",
                         "EXERCISE_CREATED_DATE", "EXERCISE_UPDATED_DATE")
VALUES ('11500000-0000-4000-8000-0000000000e1', '11500000-0000-4000-8000-0000000000b2',
        'l115-Exercice coach B', 'GLUTES', 'BODYWEIGHT', false, true, now(), now());

-- Platform templates (COACH_ID NULL, SOURCE_CODE set), circuits referencing Mentora catalog exercises only.
INSERT INTO "SESSION_TEMPLATES" ("SESSION_TEMPLATE_ID", "COACH_ID", "SESSION_TEMPLATE_SOURCE_CODE",
                                 "SESSION_TEMPLATE_NAME", "SESSION_TEMPLATE_DESCRIPTION",
                                 "SESSION_TEMPLATE_NATURE", "SESSION_TEMPLATE_SESSION_TYPE",
                                 "SESSION_TEMPLATE_CONTENT", "SESSION_TEMPLATE_IS_ACTIVE",
                                 "SESSION_TEMPLATE_CREATED_DATE", "SESSION_TEMPLATE_UPDATED_DATE")
VALUES
-- BRAS: accented name, 1 STANDARD circuit with 2 exercises (superset).
('11500000-0000-4000-8000-0000000000a1', NULL, 'l115-BRAS',
 'l115-Séance épaules', 'l115 platform fixture', 'STANDARD', 'PRESENTIEL_SOLO',
 '[{"name":"l115-Circuit A","position":1,"mode":"STANDARD","rounds":null,"restBetweenRoundsSeconds":null,"restAfterSeconds":60,"note":null,"exercises":[
     {"exerciseId":"72b9431c-9086-46ab-ba0e-2671f69702da","position":1,"loadType":"BODYWEIGHT","prescribedSets":3,"prescribedReps":12,"prescribedWeightKg":null,"restSeconds":45,"workSeconds":null,"restWorkSeconds":null,"customNote":null},
     {"exerciseId":"7062b1c9-35e8-436e-9992-6c646af29851","position":2,"loadType":"BODYWEIGHT","prescribedSets":3,"prescribedReps":12,"prescribedWeightKg":null,"restSeconds":45,"workSeconds":null,"restWorkSeconds":null,"customNote":null}]}]'::jsonb,
 true, now(), now()),
-- JAMBES: 2 circuits (one STANDARD, one INTERVAL), EVALUATION nature.
('11500000-0000-4000-8000-0000000000a2', NULL, 'l115-JAMBES',
 'l115-Séance jambes', 'l115 platform fixture', 'EVALUATION', 'AUTONOME',
 '[{"name":"l115-Circuit A","position":1,"mode":"STANDARD","rounds":null,"restBetweenRoundsSeconds":null,"restAfterSeconds":90,"note":null,"exercises":[
     {"exerciseId":"72b9431c-9086-46ab-ba0e-2671f69702da","position":1,"loadType":"KG","prescribedSets":4,"prescribedReps":8,"prescribedWeightKg":20,"restSeconds":90,"workSeconds":null,"restWorkSeconds":null,"customNote":null}]},
   {"name":"l115-Circuit B","position":2,"mode":"INTERVAL","rounds":3,"restBetweenRoundsSeconds":30,"restAfterSeconds":0,"note":null,"exercises":[
     {"exerciseId":"97eb234d-05f5-4de8-b91a-e3ee3546eeab","position":1,"loadType":"BODYWEIGHT","prescribedSets":null,"prescribedReps":null,"prescribedWeightKg":null,"restSeconds":null,"workSeconds":40,"restWorkSeconds":20,"customNote":null}]}]'::jsonb,
 true, now(), now()),
-- OFF: inactive platform template, must never be visible.
('11500000-0000-4000-8000-0000000000a3', NULL, 'l115-OFF',
 'l115-Séance inactive', 'l115 platform fixture (inactive)', 'STANDARD', 'VISIO',
 '[{"name":"l115-Circuit A","position":1,"mode":"STANDARD","rounds":null,"restBetweenRoundsSeconds":null,"restAfterSeconds":60,"note":null,"exercises":[
     {"exerciseId":"72b9431c-9086-46ab-ba0e-2671f69702da","position":1,"loadType":"BODYWEIGHT","prescribedSets":2,"prescribedReps":10,"prescribedWeightKg":null,"restSeconds":30,"workSeconds":null,"restWorkSeconds":null,"customNote":null}]}]'::jsonb,
 false, now(), now());

COMMIT;
