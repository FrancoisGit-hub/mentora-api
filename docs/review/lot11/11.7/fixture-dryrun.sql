-- Lot 11.7 migration fixtures: two rows carrying the OLD keys "restSeconds" and "restAfterSeconds".
-- DRY RUN: identical to fixture.sql except the last line (ROLLBACK instead of COMMIT).
-- Run start date read from the database before the first write: 2026-10-06 09:28:00.636468+00
-- UUIDs: program template 11700000-0000-4000-8000-0000000000a1
--        session template 11700000-0000-4000-8000-0000000000a2
-- Coach A (only coach in the local DB): e268a512-84d6-4e27-b5c0-1d6c08a47190
-- Catalog exercises (real rows, EXERCISE_COACH_ID NULL, active):
--   72b9431c-9086-46ab-ba0e-2671f69702da  Abduction hanche à la poulie
--   7062b1c9-35e8-436e-9992-6c646af29851  Abduction hanche allongée sur le côté
--   97eb234d-05f5-4de8-b91a-e3ee3546eeab  Abduction hanche avec élastique debout

BEGIN;

-- Program template: shape of the seeded bodies (bodyVersion, MACROCYCLE > MESOCYCLE > MICROCYCLE > sessions),
-- plus circuit-level "restAfterSeconds" (a circuit written after Lot 11.2). The INTERVAL circuit carries
-- "restBetweenRoundsSeconds", which must stay untouched.
INSERT INTO "PROGRAM_TEMPLATES" ("PROGRAM_TEMPLATE_ID", "PROGRAM_TEMPLATE_COACH_ID", "PROGRAM_TEMPLATE_NAME",
                                 "PROGRAM_TEMPLATE_DESCRIPTION", "PROGRAM_TEMPLATE_GOAL",
                                 "PROGRAM_TEMPLATE_DURATION_WEEKS", "PROGRAM_TEMPLATE_BODY",
                                 "PROGRAM_TEMPLATE_IS_ACTIVE", "PROGRAM_TEMPLATE_CREATED_DATE",
                                 "PROGRAM_TEMPLATE_UPDATED_DATE")
VALUES ('11700000-0000-4000-8000-0000000000a1', 'e268a512-84d6-4e27-b5c0-1d6c08a47190',
        'l11rn-Template ancien format', 'l11rn fixture (old rest keys)', 'GENERAL_FITNESS', 1,
        '{"bodyVersion":1,"blocks":[{"name":"l11rn-Cycle","level":"MACROCYCLE","position":1,"weekNumber":null,"sessions":[],"blocks":[
  {"name":"l11rn-Bloc 1","level":"MESOCYCLE","position":1,"weekNumber":null,"sessions":[],"blocks":[
   {"name":"l11rn-Semaine 1","level":"MICROCYCLE","position":1,"weekNumber":1,"blocks":[],"sessions":[
    {"name":"l11rn-Séance A","type":"PRESENTIEL_SOLO","position":1,"dayOfWeek":1,"circuits":[
     {"name":"l11rn-Circuit A","position":1,"mode":"STANDARD","rounds":null,"restBetweenRoundsSeconds":null,"restAfterSeconds":60,"note":null,"exercises":[
      {"exerciseId":"72b9431c-9086-46ab-ba0e-2671f69702da","position":1,"loadType":"BODYWEIGHT","prescribedSets":3,"prescribedReps":12,"prescribedWeightKg":null,"restSeconds":45,"workSeconds":null,"restWorkSeconds":null,"customNote":null},
      {"exerciseId":"7062b1c9-35e8-436e-9992-6c646af29851","position":2,"loadType":"KG","prescribedSets":4,"prescribedReps":8,"prescribedWeightKg":20,"restSeconds":90,"workSeconds":null,"restWorkSeconds":null,"customNote":null}]},
     {"name":"l11rn-Circuit B","position":2,"mode":"INTERVAL","rounds":3,"restBetweenRoundsSeconds":30,"restAfterSeconds":120,"note":null,"exercises":[
      {"exerciseId":"97eb234d-05f5-4de8-b91a-e3ee3546eeab","position":1,"loadType":"BODYWEIGHT","prescribedSets":null,"prescribedReps":null,"prescribedWeightKg":null,"restSeconds":null,"workSeconds":40,"restWorkSeconds":20,"customNote":null}]}]}
   ]}]}]}]}'::jsonb,
        true, now(), now());

-- Session template: coach A owns it, so SOURCE_CODE is NULL (CK_SESSION_TEMPLATES_SOURCE_CODE).
-- Nature STANDARD, type PRESENTIEL_SOLO, content = non-empty array (CK_SESSION_TEMPLATES_*).
INSERT INTO "SESSION_TEMPLATES" ("SESSION_TEMPLATE_ID", "COACH_ID", "SESSION_TEMPLATE_SOURCE_CODE",
                                 "SESSION_TEMPLATE_NAME", "SESSION_TEMPLATE_DESCRIPTION",
                                 "SESSION_TEMPLATE_NATURE", "SESSION_TEMPLATE_SESSION_TYPE",
                                 "SESSION_TEMPLATE_CONTENT", "SESSION_TEMPLATE_IS_ACTIVE",
                                 "SESSION_TEMPLATE_CREATED_DATE", "SESSION_TEMPLATE_UPDATED_DATE")
VALUES ('11700000-0000-4000-8000-0000000000a2', 'e268a512-84d6-4e27-b5c0-1d6c08a47190', NULL,
        'l11rn-Séance ancien format', 'l11rn fixture (old rest keys)', 'STANDARD', 'PRESENTIEL_SOLO',
        '[{"name":"l11rn-Circuit A","position":1,"mode":"STANDARD","rounds":null,"restBetweenRoundsSeconds":null,"restAfterSeconds":90,"note":null,"exercises":[
    {"exerciseId":"72b9431c-9086-46ab-ba0e-2671f69702da","position":1,"loadType":"KG","prescribedSets":4,"prescribedReps":8,"prescribedWeightKg":20,"restSeconds":90,"workSeconds":null,"restWorkSeconds":null,"customNote":null},
    {"exerciseId":"7062b1c9-35e8-436e-9992-6c646af29851","position":2,"loadType":"BODYWEIGHT","prescribedSets":3,"prescribedReps":12,"prescribedWeightKg":null,"restSeconds":45,"workSeconds":null,"restWorkSeconds":null,"customNote":null}]},
  {"name":"l11rn-Circuit B","position":2,"mode":"INTERVAL","rounds":3,"restBetweenRoundsSeconds":30,"restAfterSeconds":0,"note":null,"exercises":[
    {"exerciseId":"97eb234d-05f5-4de8-b91a-e3ee3546eeab","position":1,"loadType":"BODYWEIGHT","prescribedSets":null,"prescribedReps":null,"prescribedWeightKg":null,"restSeconds":null,"workSeconds":40,"restWorkSeconds":20,"customNote":null}]}]'::jsonb,
        true, now(), now());

-- Proof: md5 of each body text and count of each old / new key, per fixture row.
SELECT 'PROGRAM_TEMPLATES' AS store, "PROGRAM_TEMPLATE_ID" AS id, md5(t) AS md5_body,
       (length(t) - length(replace(t, '"restSeconds"', ''))) / length('"restSeconds"') AS old_restSeconds,
       (length(t) - length(replace(t, '"restAfterSeconds"', ''))) / length('"restAfterSeconds"') AS old_restAfterSeconds,
       (length(t) - length(replace(t, '"restSecondsAfterSeries"', ''))) / length('"restSecondsAfterSeries"') AS new_afterSeries,
       (length(t) - length(replace(t, '"restSecondsAfterExercices"', ''))) / length('"restSecondsAfterExercices"') AS new_afterExercices,
       (length(t) - length(replace(t, '"restBetweenRoundsSeconds"', ''))) / length('"restBetweenRoundsSeconds"') AS between_rounds
FROM (SELECT "PROGRAM_TEMPLATE_ID", "PROGRAM_TEMPLATE_BODY"::text AS t FROM "PROGRAM_TEMPLATES"
      WHERE "PROGRAM_TEMPLATE_ID" = '11700000-0000-4000-8000-0000000000a1') s
UNION ALL
SELECT 'SESSION_TEMPLATES', "SESSION_TEMPLATE_ID", md5(t),
       (length(t) - length(replace(t, '"restSeconds"', ''))) / length('"restSeconds"'),
       (length(t) - length(replace(t, '"restAfterSeconds"', ''))) / length('"restAfterSeconds"'),
       (length(t) - length(replace(t, '"restSecondsAfterSeries"', ''))) / length('"restSecondsAfterSeries"'),
       (length(t) - length(replace(t, '"restSecondsAfterExercices"', ''))) / length('"restSecondsAfterExercices"'),
       (length(t) - length(replace(t, '"restBetweenRoundsSeconds"', ''))) / length('"restBetweenRoundsSeconds"')
FROM (SELECT "SESSION_TEMPLATE_ID", "SESSION_TEMPLATE_CONTENT"::text AS t FROM "SESSION_TEMPLATES"
      WHERE "SESSION_TEMPLATE_ID" = '11700000-0000-4000-8000-0000000000a2') s;

ROLLBACK;
