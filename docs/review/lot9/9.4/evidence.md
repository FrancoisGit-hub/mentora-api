# Lot 9.4 — Exercise catalog read endpoints: runtime evidence

Local only (`mentora-postgres-local` 5433, API 5243). No schema change, no migration. Run start (UTC): `2026-09-29T18:21:40Z`.
Scripts and raw outputs are in this folder: `verify-9.4.ps1`, `verify-sql-count.ps1`, `get-tokens.ps1`, `expected-counts.sql`, `verify-9.4-seeder.sql`, `swagger-diff.py`, `fixture-01-coach-b.sql`, `cleanup*.sql`.

## Preflight facts
- Branch `feature/lot_9`, HEAD `fb7d5df`, tree clean, `dotnet build`: 19 warnings, 0 errors.
- Before: `MemberExercisesController` `GET /` (no params, `ListForMemberAsync(memberId)`) and `GET /{exerciseId:guid}`, policy `MemberOnly`, group `mobile`; visibility = active AND (coach NULL OR coach in the member's `MEMBER_COACHES`). `CoachExercisesController`: `GET /` (search, muscleGroup, equipment, scope=ALL, includeInactive), `GET /{id}`, POST, PUT, DELETE, policy `CoachOnly`; visibility = coach NULL or own.
- `GET coach/exercises` returned `data` = a JSON array of 13-key items (no pagination).
- Mirrored cursor pattern: `OrderListResponseDto(IReadOnlyList<OrderSummaryDto> Orders, string? NextCursor)`; limit default 20, range 1..100 → `InvalidOperationException`; cursor = base64 of JSON, `limit+1` fetch. Applied as `ExerciseListResponseDto(IReadOnlyList<ExerciseSummaryDto> Items, string? NextCursor)`.
- `member@mentora.fr` is attached to `coach@mentora.fr` in `MEMBER_COACHES` (is_primary = t). Baseline swagger (mobile, coach) saved before any code change.
- Auth side effect (technical rows, not deleted): `AUTH_OTPS` 43 → 57, `AUTH_REFRESH_TOKENS` 42 → 49 (14 and 7 created since the run start, for `coach@mentora.fr` / `member@mentora.fr`).

## Fixtures
Coach B (`5b4e9c1a-…0002`, user `…0001`) with one active exercise (`…0003`), `fixture-01-coach-b.sql`; coach A exercises created through `POST api/v1/coach/exercises` (active `1eebb781-3f3c-42ec-8842-b109b0d5405f`, and `41f04f69-cb37-47c5-9148-45ba401f7e50` soft-deleted through `DELETE`). The POST response is the extended shape (32 keys), see V7.

## V1 — member list, walk
```
page 1..5 -> 200, 50 items each, nextCursor set; page 6 -> 200, 26 items, nextCursor null
total walked = 276; SQL visible = 276; duplicates = 0; missing = 0; extra = 0
decoded cursor: {"Name":"Curl poulie unilat\u00E9ral","ExerciseId":"5b3cb685-d906-45df-9c8c-8da8a4862872"}
```
First call: 200, 50 items, nextCursor set. 276 = 275 Mentora + coach A's active exercise.

## V2 to V5, V7, V8 — API run on the final build (`verify-9.4.ps1`, full output)
```
﻿## V2 - filters (member list, walked with limit=100)
Pectoraux (muscleGroupId)                                  API =   30  SQL =   30  OK
Haltères (equipmentId, primary OR secondary)               API =   50  SQL =   50  OK
Sans matériel (equipmentId)                                API =   49  SQL =   49  OK
Banc (equipmentId)                                         API =   12  SQL =   12  OK
objective Force (objectiveId)                              API =   39  SQL =   39  OK
level=BEGINNER                                             API =  125  SQL =  125  OK
level=INTERMEDIATE (>= BEGINNER)                           API =  231  SQL =  231  OK
search=squat                                               API =    9  SQL =    9  OK
combined Pectoraux + Haltères                              API =    8  SQL =    8  OK
combined Force + level=INTERMEDIATE                        API =   37  SQL =   37  OK

## V2 - 'Sans matériel': exact count 49; 10 first names:
  - Abduction hanche allongée sur le côté  [primary: Sans matériel; secondary: Tapis de sol]
  - Bear crawl  [primary: Sans matériel; secondary: Tapis de sol]
  - Bird dog  [primary: Sans matériel; secondary: Tapis de sol]
  - Burpee  [primary: Sans matériel; secondary: ]
  - Burpees  [primary: Sans matériel; secondary: ]
  - Course à pied  [primary: Sans matériel; secondary: ]
  - Crab walk  [primary: Sans matériel; secondary: Tapis de sol]
  - Crunch  [primary: Sans matériel; secondary: ]
  - Crunch au sol  [primary: Sans matériel; secondary: Tapis de sol]
  - Crunch vélo  [primary: Sans matériel; secondary: Tapis de sol]
  matched to a sheet row by name: 44; not in the sheet (historical): 5 -> Burpees | Crunch | Dips | Gainage planche | Suspension à la barre
  sheet exercises whose materiel_principal is Banc / Barre / Corde / Poignée simple: 0
  secondary equipment shown for the 'Sans matériel' results (distinct): Banc, Haltères, Mur, Plots, Tapis de sol

## V3 - visibility
member list has coach A's active exercise (1eebb781-3f3c-42ec-8842-b109b0d5405f): True
member list has coach B's exercise (5b4e9c1a-0c1d-4a55-9d3e-940400000003): False
member list has coach A's inactive exercise (41f04f69-cb37-47c5-9148-45ba401f7e50): False
GET member/exercises/1eebb781-3f3c-42ec-8842-b109b0d5405f [coach A active] -> 200
GET member/exercises/5b4e9c1a-0c1d-4a55-9d3e-940400000003 [coach B] -> 404
GET member/exercises/41f04f69-cb37-47c5-9148-45ba401f7e50 [coach A inactive] -> 404
GET member/exercises/1feab1a9-b6cc-446b-bc34-d6d6695cb754 [unknown guid] -> 404

## V4 - errors
GET member/exercises?level=EXPERT -> 400  {"success":false,"data":null,"error":"Invalid level 'EXPERT'. Accepted values: BEGINNER, INTERMEDIATE, ADVANCED.","statusCode":400}
GET member/exercises?limit=0 -> 400  {"success":false,"data":null,"error":"limit must be between 1 and 100 (got 0).","statusCode":400}
GET member/exercises?limit=101 -> 400  {"success":false,"data":null,"error":"limit must be between 1 and 100 (got 101).","statusCode":400}
GET member/exercises?cursor=garbage -> 400  {"success":false,"data":null,"error":"Invalid cursor format.","statusCode":400}
GET member/exercises?search=a -> 400  {"success":false,"data":null,"error":"search must be between 2 and 60 characters (got 1).","statusCode":400}
GET member/exercises?search=xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx... -> 400  {"success":false,"data":null,"error":"search must be between 2 and 60 characters (got 61).","statusCode":400}
GET member/exercises?search=%20%20 -> 200  {"success":true,"data":{"items":[{"exerciseId":"72b9431c-9086-46ab-ba0e-2671f69702da","name":"Abduction hanche à la poulie","level":"INTERMEDIATE","primaryEquipment":{"equipmentId":"be895cf6-28d3-4354-a9fc-9c3ef1bcfe18","name":"Poulie basse"},"secondaryEquipment":{"equipmentId":"d5858376-071d-47eb-a826-22abdbe2034d","name":"Sangle cheville"}},{"exerciseId":"7062b1c9-35e8-436e-9992-6c646af29851","name":"Abduction hanche allongée sur le côté","level":"BEGINNER","primaryEquipment":{"equipmentId":"944fec76-b2b3-4c93-9e7e-1506666fe773","name":"Sans matériel"},"secondaryEquipment":{"equipmentId":"28d9f8cb-7a79-407f-aaf9-cb75d37c607e","name":"Tapis de sol"}},{"exerciseId":"97eb234d-05f5-4de8-b91a-e3ee3546eeab","name":"Abduction hanche avec élastique debout","level":"BEGINNER","primaryEquipment":{"equipmentId":"984ad195-1d85-437d-9cb2-3c31b53722a6","name":"Élastique"},"secondaryEquipment":null},{"exerciseId":"f1609e43-f870-401e-a975-d7cf2ca4043d","name":"Ab wheel rollout","level":"ADVANCED","primaryEquipment":{"equipmentId":"d5f758b2-16c0-4073-94bd-9564912adf0e","name":"Roue abdominale"},"secondaryEquipment":{"equipmentId":"28d9f8cb-7a79-407f-aaf9-cb75d37c607e","name":"Tapis de sol"}},{"exerciseId":"d1be0690-59c8-4bab-ba9d-bbbb182f772f","name":"Adduction hanche à la poulie","level":"INTERMEDIATE","primaryEquipment":{"equipmentId":"be895cf6-28d3-4354-a9fc-9c3ef1bcfe18","name":"Poulie basse"},"secondaryEquipment":{"equipmentId":"d5858376-071d-47eb-a826-22abdbe2034d","name":"Sangle cheville"}},{"exerciseId":"d3fb04aa-bb04-44b7-9532-c251591c4634","name":"Air bike","level":"BEGINNER","primaryEquipment":{"equipmentId":"128d265a-a59a-4ea9-a620-c500b3c52195","name":"Air bike"},"secondaryEquipment":null},{"exerciseId":"c3e03857-bd04-41e4-ab6b-25f60e601b87","name":"Arnold press","level":"INTERMEDIATE","primaryEquipment":{"equipmentId":"63b68ad4-a46f-44c0-89ba-23c56be801e5","name":"Haltères"},"secondaryEquipment":{"equipmentId":"57a46241-6f14-446d-aaf9-41106c35908f","name":"Banc réglable"}},{"exerciseId":"5f9b7aed-c425-4845-b0b7-015471a6a21d","name":"Barre au front","level":"INTERMEDIATE","primaryEquipment":{"equipmentId":"37d3855a-2292-431a-bfcb-da99a471c2c3","name":"Barre EZ"},"secondaryEquipment":{"equipmentId":"74895d1d-ccb9-4458-b37e-fd899f146dce","name":"Banc"}},{"exerciseId":"e45bfa59-4b09-4740-acc6-5ebbec6ba90e","name":"Battle rope vagues alternées","level":"BEGINNER","primaryEquipment":{"equipmentId":"bc42bee5-5aaa-4923-8361-4a3c0d49e108","name":"Battle rope"},"secondaryEquipment":null},{"exerciseId":"195631e0-97b5-484e-b41d-5f52a16c6607","name":"Battle rope vagues simultanées","level":"BEGINNER","primaryEquipment":{"equipmentId":"bc42bee5-5aaa-4923-8361-4a3c0d49e108","name":"Battle rope"},"secondaryEquipment":null},{"exerciseId":"abf2a874-0942-466f-894e-a96791ee9344","name":"Bear crawl","level":"INTERMEDIATE","primaryEquipment":{"equipmentId":"944fec76-b2b3-4c93-9e7e-1506666fe773","name":"Sans matériel"},"secondaryEquipment":{"equipmentId":"28d9f8cb-7a79-407f-aaf9-cb75d37c607e","name":"Tapis de sol"}},{"exerciseId":"cb66d720-bf8a-4927-970e-e63ac793fe16","name":"Bird dog","level":"BEGINNER","primaryEquipment":{"equipmentId":"944fec76-b2b3-4c93-9e7e-1506666fe773","name":"Sans matériel"},"secondaryEquipment":{"equipmentId":"28d9f8cb-7a79-407f-aaf9-cb75d37c607e","name":"Tapis de sol"}},{"exerciseId":"99f58860-a11d-459b-ad4c-0a268c8486c0","name":"Box jump","level":"INTERMEDIATE","primaryEquipment":{"equipmentId":"d56ee0f1-7d81-47ac-a851-e54406f3979c","name":"Box pliométrique"},"secondaryEquipment":null},{"exerciseId":"bf0b8e11-dfe4-4e10-9cf1-169b5b9b6c98","name":"Bulgarian split squat","level":"INTERMEDIATE","primaryEquipment":{"equipmentId":"63b68ad4-a46f-44c0-89ba-23c56be801e5","name":"Haltères"},"secondaryEquipment":{"equipmentId":"74895d1d-ccb9-4458-b37e-fd899f146dce","name":"Banc"}},{"exerciseId":"aa0ed253-6969-4f2f-86d1-5ffbfb8a5695","name":"Burpee","level":"INTERMEDIATE","primaryEquipment":{"equipmentId":"944fec76-b2b3-4c93-9e7e-1506666fe773","name":"Sans matériel"},"secondaryEquipment":null},{"exerciseId":"18323604-d35c-48ec-8715-4d94da7dd217","name":"Burpees","level":null,"primaryEquipment":{"equipmentId":"944fec76-b2b3-4c93-9e7e-1506666fe773","name":"Sans matériel"},"secondaryEquipment":null},{"exerciseId":"99e3f182-d840-4dd5-af77-6fe847b422fb","name":"Cable hip thrust","level":"INTERMEDIATE","primaryEquipment":{"equipmentId":"be895cf6-28d3-4354-a9fc-9c3ef1bcfe18","name":"Poulie basse"},"secondaryEquipment":{"equipmentId":"74895d1d-ccb9-4458-b37e-fd899f146dce","name":"Banc"}},{"exerciseId":"5841b9d8-a996-4ea1-8592-bff1986e1f85","name":"Chest press machine","level":"BEGINNER","primaryEquipment":{"equipmentId":"31994d77-8028-497c-ba64-6509e2844d06","name":"Chest press"},"secondaryEquipment":null},{"exerciseId":"13d9832d-dc17-41cd-aeb7-ba40285befb9","name":"Chest press unilatérale","level":"INTERMEDIATE","primaryEquipment":{"equipmentId":"31994d77-8028-497c-ba64-6509e2844d06","name":"Chest press"},"secondaryEquipment":null},{"exerciseId":"21ec31b7-9dca-4fb4-9ea0-6d4774b8a3be","name":"Clamshell","level":"BEGINNER","primaryEquipment":{"equipmentId":"9cf8ca5a-5029-4ffa-b73a-ef21cbaaf786","name":"Mini-band"},"secondaryEquipment":{"equipmentId":"28d9f8cb-7a79-407f-aaf9-cb75d37c607e","name":"Tapis de sol"}},{"exerciseId":"404b6cf7-7950-4805-850d-65107758534c","name":"Corde à sauter","level":"BEGINNER","primaryEquipment":{"equipmentId":"43036179-4fef-4c86-9f2d-0dbebfb0ad92","name":"Corde à sauter"},"secondaryEquipment":null},{"exerciseId":"99ddbd55-5a00-4bcc-b101-820c29a2f89c","name":"Course à pied","level":"BEGINNER","primaryEquipment":{"equipmentId":"944fec76-b2b3-4c93-9e7e-1506666fe773","name":"Sans matériel"},"secondaryEquipment":null},{"exerciseId":"e5d4adb6-b943-47cd-9b87-69bff972eccc","name":"Course à pied (tapis)","level":null,"primaryEquipment":null,"secondaryEquipment":null},{"exerciseId":"6d98e0a8-2dfe-4665-8f23-5fcc57f2892a","name":"Crab walk","level":"INTERMEDIATE","primaryEquipment":{"equipmentId":"944fec76-b2b3-4c93-9e7e-1506666fe773","name":"Sans matériel"},"secondaryEquipment":{"equipmentId":"28d9f8cb-7a79-407f-aaf9-cb75d37c607e","name":"Tapis de sol"}},{"exerciseId":"a7ad108d-f029-4470-aedd-c9bd3bdab652","name":"Crossover poulie basse","level":"INTERMEDIATE","primaryEquipment":{"equipmentId":"f0c32467-eda7-4686-a01c-e6b7f1c9a99e","name":"Vis-à-vis"},"secondaryEquipment":null},{"exerciseId":"d9281c9a-45d8-41b3-a8bc-11281024c561","name":"Crossover poulie haute","level":"INTERMEDIATE","primaryEquipment":{"equipmentId":"f0c32467-eda7-4686-a01c-e6b7f1c9a99e","name":"Vis-à-vis"},"secondaryEquipment":null},{"exerciseId":"50847180-2747-4bf3-9d87-26e846cd3a4b","name":"Crunch","level":null,"primaryEquipment":{"equipmentId":"944fec76-b2b3-4c93-9e7e-1506666fe773","name":"Sans matériel"},"secondaryEquipment":null},{"exerciseId":"8019daf3-5b62-4b6c-aeaa-1627dd55e813","name":"Crunch à la poulie","level":"INTERMEDIATE","primaryEquipment":{"equipmentId":"f3675675-5b23-490a-a6fd-7f2a16d85cf4","name":"Poulie haute"},"secondaryEquipment":{"equipmentId":"6263bcef-2d6d-4d6f-a1ec-e53e239576b8","name":"Corde"}},{"exerciseId":"9699d215-4232-438c-bd41-805e56770240","name":"Crunch au sol","level":"BEGINNER","primaryEquipment":{"equipmentId":"944fec76-b2b3-4c93-9e7e-1506666fe773","name":"Sans matériel"},"secondaryEquipment":{"equipmentId":"28d9f8cb-7a79-407f-aaf9-cb75d37c607e","name":"Tapis de sol"}},{"exerciseId":"bad5cff9-aba7-44d0-a975-609d405dc9f3","name":"Crunch lesté","level":"INTERMEDIATE","primaryEquipment":{"equipmentId":"5e90e554-60f5-419b-a4b8-e53308d9a766","name":"Disque ou haltère"},"secondaryEquipment":{"equipmentId":"28d9f8cb-7a79-407f-aaf9-cb75d37c607e","name":"Tapis de sol"}},{"exerciseId":"955ac6ae-d6cc-4993-9f8e-3e61d525b178","name":"Crunch vélo","level":"BEGINNER","primaryEquipment":{"equipmentId":"944fec76-b2b3-4c93-9e7e-1506666fe773","name":"Sans matériel"},"secondaryEquipment":{"equipmentId":"28d9f8cb-7a79-407f-aaf9-cb75d37c607e","name":"Tapis de sol"}},{"exerciseId":"5cdc7c05-d097-4679-85c1-111a01038a97","name":"Cuban press haltères","level":"ADVANCED","primaryEquipment":{"equipmentId":"63b68ad4-a46f-44c0-89ba-23c56be801e5","name":"Haltères"},"secondaryEquipment":null},{"exerciseId":"c2ff6721-89b6-4711-973f-937c17f4a6e1","name":"Curl barre","level":"BEGINNER","primaryEquipment":{"equipmentId":"04dbd52c-185c-4fab-928b-19d904473e25","name":"Barre olympique"},"secondaryEquipment":null},{"exerciseId":"806ce255-2732-4c63-8e01-dcef2f3490cf","name":"Curl barre EZ","level":"BEGINNER","primaryEquipment":{"equipmentId":"37d3855a-2292-431a-bfcb-da99a471c2c3","name":"Barre EZ"},"secondaryEquipment":null},{"exerciseId":"261a6701-64ec-4048-a508-29eec1d034fd","name":"Curl Bayesian","level":"INTERMEDIATE","primaryEquipment":{"equipmentId":"be895cf6-28d3-4354-a9fc-9c3ef1bcfe18","name":"Poulie basse"},"secondaryEquipment":{"equipmentId":"71e4b3b6-155b-4721-b7aa-4829259ffbe7","name":"Poignée simple"}},{"exerciseId":"de6b8a62-6708-4596-a708-e002566c14df","name":"Curl biceps barre","level":null,"primaryEquipment":null,"secondaryEquipment":null},{"exerciseId":"79265c3f-211a-4636-80ae-cda18a6bcde1","name":"Curl biceps haltères alterné","level":null,"primaryEquipment":null,"secondaryEquipment":null},{"exerciseId":"7d6dacba-0ccb-4146-a651-5d3c917ef77c","name":"Curl concentration","level":"BEGINNER","primaryEquipment":{"equipmentId":"63b68ad4-a46f-44c0-89ba-23c56be801e5","name":"Haltères"},"secondaryEquipment":null},{"exerciseId":"9e8ef044-f2a9-4735-9dc9-ee479aae27d4","name":"Curl haltères","level":"BEGINNER","primaryEquipment":{"equipmentId":"63b68ad4-a46f-44c0-89ba-23c56be801e5","name":"Haltères"},"secondaryEquipment":null},{"exerciseId":"227f343c-ff05-48e4-9312-5cca8b857bdc","name":"Curl incliné haltères","level":"INTERMEDIATE","primaryEquipment":{"equipmentId":"63b68ad4-a46f-44c0-89ba-23c56be801e5","name":"Haltères"},"secondaryEquipment":{"equipmentId":"57a46241-6f14-446d-aaf9-41106c35908f","name":"Banc réglable"}},{"exerciseId":"b8b00f8a-7664-44fd-a2b6-ceda77f6bbe2","name":"Curl inversé barre","level":"INTERMEDIATE","primaryEquipment":{"equipmentId":"8cc0f0ba-b46d-4c61-9cb1-b819d87262e6","name":"Barre"},"secondaryEquipment":null},{"exerciseId":"991755ab-ec71-4f4a-83a5-34096797e124","name":"Curl ischio sliders","level":"INTERMEDIATE","primaryEquipment":{"equipmentId":"622f650b-68a8-4b9c-a057-8cc1aeaf3f1c","name":"Sliders"},"secondaryEquipment":{"equipmentId":"28d9f8cb-7a79-407f-aaf9-cb75d37c607e","name":"Tapis de sol"}},{"exerciseId":"6ac06381-83af-42f8-83b8-b7fa2a760f3c","name":"Curl ischio sur ballon suisse","level":"INTERMEDIATE","primaryEquipment":{"equipmentId":"ea914f45-c488-4ba1-9eda-b16dd8fc74aa","name":"Ballon suisse"},"secondaryEquipment":{"equipmentId":"28d9f8cb-7a79-407f-aaf9-cb75d37c607e","name":"Tapis de sol"}},{"exerciseId":"f38edda5-1b15-4e03-a6b2-fba31bfb9ac4","name":"Curl marteau","level":"BEGINNER","primaryEquipment":{"equipmentId":"63b68ad4-a46f-44c0-89ba-23c56be801e5","name":"Haltères"},"secondaryEquipment":null},{"exerciseId":"299e617f-544d-4044-a8be-920743d06c9b","name":"Curl marteau alterné","level":"BEGINNER","primaryEquipment":{"equipmentId":"63b68ad4-a46f-44c0-89ba-23c56be801e5","name":"Haltères"},"secondaryEquipment":null},{"exerciseId":"03e7c5f3-f39e-4e48-a734-4e46f991eabd","name":"Curl marteau haltères","level":null,"primaryEquipment":null,"secondaryEquipment":null},{"exerciseId":"01ed62fb-3db6-4c3f-9879-e76560163205","name":"Curl poignet barre","level":null,"primaryEquipment":null,"secondaryEquipment":null},{"exerciseId":"6297adc8-a038-4d8a-b103-d8fd46f4227a","name":"Curl poignets haltères","level":"BEGINNER","primaryEquipment":{"equipmentId":"63b68ad4-a46f-44c0-89ba-23c56be801e5","name":"Haltères"},"secondaryEquipment":null},{"exerciseId":"312deeb7-e065-49e4-9381-92293b31b731","name":"Curl poulie basse","level":"BEGINNER","primaryEquipment":{"equipmentId":"be895cf6-28d3-4354-a9fc-9c3ef1bcfe18","name":"Poulie basse"},"secondaryEquipment":{"equipmentId":"8cc0f0ba-b46d-4c61-9cb1-b819d87262e6","name":"Barre"}},{"exerciseId":"5b3cb685-d906-45df-9c8c-8da8a4862872","name":"Curl poulie unilatéral","level":"INTERMEDIATE","primaryEquipment":{"equipmentId":"be895cf6-28d3-4354-a9fc-9c3ef1bcfe18","name":"Poulie basse"},"secondaryEquipment":{"equipmentId":"71e4b3b6-155b-4721-b7aa-4829259ffbe7","name":"Poignée simple"}}],"nextCursor":"eyJOYW1lIjoiQ3VybCBwb3VsaWUgdW5pbGF0XHUwMEU5cmFsIiwiRXhlcmNpc2VJZCI6IjViM2NiNjg1LWQ5MDYtNDVkZi05YzhjLThkYThhNDg2Mjg3MiJ9"},"error":null,"statusCode":200}
GET member/exercises?cursor=eyJmb28iOjF9 -> 400  {"success":false,"data":null,"error":"Invalid cursor.","statusCode":400}
unknown muscleGroupId -> 200, items 0, nextCursor = 
search='__' (LIKE wildcards escaped, must not match everything) -> 200, items 0
search='%%' -> 200, items 0
search=' SQUAT ' (trimmed, case-insensitive) -> 200, items 9

## V5 - detail of EX001 (member)
status 200; video is null: True
{
    "exerciseId":  "1194fd80-be00-4630-907e-80609ef0f331",
    "coachId":  null,
    "name":  "Développé couché barre",
    "description":  "Exercice polyarticulaire de référence pour les pectoraux.",
    "instructions":  "Allongé sur un banc, saisir la barre à largeur épaules, descendre jusqu\u0027à effleurer la poitrine puis pousser jusqu\u0027à extension complète des bras.",
    "videoUrl":  null,
    "imageUrl":  null,
    "muscleGroup":  "CHEST",
    "equipment":  "BARBELL",
    "isPolyarticular":  true,
    "isActive":  true,
    "createdDate":  "2026-08-11T11:53:42.833197Z",
    "updatedDate":  "2026-09-29T18:58:57.654289Z",
    "sourceCode":  "EX001",
    "nameEn":  "Barbell Bench Press",
    "category":  "Musculation",
    "movementPattern":  "Poussée horizontale",
    "exerciseType":  "Polyarticulaire",
    "laterality":  "Bilatéral",
    "startingPosition":  "Couché sur banc",
    "gripType":  "Pronée",
    "primaryMuscleGroup":  {
                               "muscleGroupId":  "d6660410-7110-4ac5-8f08-c90ac7f1288d",
                               "name":  "Pectoraux"
                           },
    "secondaryMuscles":  [
                             "Triceps",
                             "deltoïde antérieur"
                         ],
    "primaryEquipment":  {
                             "equipmentId":  "04dbd52c-185c-4fab-928b-19d904473e25",
                             "name":  "Barre olympique"
                         },
    "secondaryEquipment":  {
                               "equipmentId":  "74895d1d-ccb9-4458-b37e-fd899f146dce",
                               "name":  "Banc"
                           },
    "level":  "INTERMEDIATE",
    "breathing":  "Inspirer à la descente ; expirer lors de la poussée",
    "commonMistakes":  [
                           "Décoller les épaules",
                           "rebondir sur la poitrine",
                           "poignets cassés",
                           "pieds instables"
                       ],
    "precautions":  "Adapter l\u0027amplitude en cas d\u0027inconfort à l\u0027épaule",
    "coachTip":  null,
    "objectives":  [
                       {
                           "objectiveId":  "88e02f18-6783-4826-9056-24c4e3d4914b",
                           "name":  "Force"
                       },
                       {
                           "objectiveId":  "d13ad9a3-ca3c-42c6-87ef-ab2567a289cf",
                           "name":  "Hypertrophie"
                       }
                   ],
    "video":  null
}

## V7 - coach
GET coach/exercises -> 200; data is an array: True; items: 276
legacy keys present and unchanged in order: True
new keys present: True; total keys: 32
every item has the same 32 keys: True
muscleGroup=CHEST -> 200, items 32, all legacy muscleGroup CHEST: True
muscleGroupId=Pectoraux -> 200, items 30, all primaryMuscleGroup Pectoraux: True
equipmentId=Haltères -> 200, items 50
equipment=DUMBBELL (legacy) -> 200, items 63
objectiveId=Force -> 200, items 39
level=BEGINNER -> 200, items 125
level=EXPERT -> 400 {"success":false,"data":null,"error":"Invalid level 'EXPERT'. Accepted values: BEGINNER, INTERMEDIATE, ADVANCED.","statusCode":400}
GET coach/exercises/{coach A exercise} -> 200; keys: 32
GET coach/exercises/EX001 -> 200; level INTERMEDIATE; primaryEquipment Barre olympique; objectives Force ; Hypertrophie
GET coach/exercises/{coach B exercise} -> 404

## V8 - filter endpoints
GET /api/v1/member/exercise-filters -> 200: muscleGroups 12, equipment 91, objectives 52, levels BEGINNER,INTERMEDIATE,ADVANCED
  equipment first 4: Sans matériel | Air bike | Anneaux gymnastique | Ballon suisse
  muscleGroups (sortOrder): Pectoraux(1), Dos(2), Épaules(3), Bras(4), Avant-bras(5), Quadriceps(6), Ischio-jambiers(7), Fessiers(8), Adducteurs(9), Mollets(10), Tibial(11), Abdominaux(12)
  objectives first 5: Activation | Agilité | Anti-inclinaison | Anti-rotation | Apprentissage
  sample equipment item: {"equipmentId":"944fec76-b2b3-4c93-9e7e-1506666fe773","name":"Sans matériel","category":"Sans matériel"}
GET /api/v1/coach/exercise-filters -> 200: muscleGroups 12, equipment 91, objectives 52, levels BEGINNER,INTERMEDIATE,ADVANCED
  equipment first 4: Sans matériel | Air bike | Anneaux gymnastique | Ballon suisse
  muscleGroups (sortOrder): Pectoraux(1), Dos(2), Épaules(3), Bras(4), Avant-bras(5), Quadriceps(6), Ischio-jambiers(7), Fessiers(8), Adducteurs(9), Mollets(10), Tibial(11), Abdominaux(12)
  objectives first 5: Activation | Agilité | Anti-inclinaison | Anti-rotation | Apprentissage
  sample equipment item: {"equipmentId":"944fec76-b2b3-4c93-9e7e-1506666fe773","name":"Sans matériel","category":"Sans matériel"}
member token on coach/exercise-filters -> 403
no token on member/exercise-filters -> 401
no token on coach/exercise-filters -> 401
no token on member/exercises/{id} -> 401
coach token (COACH, has member profile) on member/exercise-filters -> 403

```

## V6 — SQL commands per call (EF Command log, `verify-sql-count.ps1`)
```
﻿member list no filter                                      HTTP 200  SQL commands = 1   (member_list_no_filter.log)
member list 2 filters (muscleGroup+equipment)              HTTP 200  SQL commands = 1   (member_list_2_filters_muscleGroup_equipment_.log)
member list all 5 filters + search                         HTTP 200  SQL commands = 1   (member_list_all_5_filters_search.log)
member list page 2 (cursor)                                HTTP 200  SQL commands = 1   (member_list_page_2_cursor_.log)
member detail EX001                                        HTTP 200  SQL commands = 1   (member_detail_EX001.log)
member detail 404                                          HTTP 404  SQL commands = 1   (member_detail_404.log)
member exercise-filters                                    HTTP 200  SQL commands = 3   (member_exercise_filters.log)
coach exercise-filters                                     HTTP 200  SQL commands = 3   (coach_exercise_filters.log)
coach list                                                 HTTP 200  SQL commands = 2   (coach_list.log)
coach detail EX001                                         HTTP 200  SQL commands = 1   (coach_detail_EX001.log)

```

SQL of the list call with two filters (muscleGroupId + equipmentId), one command:
```
﻿      Executed DbCommand (7ms) [Parameters=[@__memberId_0='?' (DbType = Guid), @__mg_1='?' (DbType = Guid), @__eq_2='?' (DbType = Guid), @__p_3='?' (DbType = Int32)], CommandType='Text', CommandTimeout='30']
      SELECT t."EXERCISE_ID" AS "ExerciseId", t."EXERCISE_NAME" AS "ExerciseName", t."EXERCISE_MIN_LEVEL" AS "ExerciseMinLevel", t."EXERCISE_PRIMARY_EQUIPMENT_ID" AS "PrimaryId", e0."EQUIPMENT_NAME" AS "PrimaryName", t."EXERCISE_SECONDARY_EQUIPMENT_ID" AS "SecondaryId", e1."EQUIPMENT_NAME" AS "SecondaryName"
      FROM (
          SELECT e."EXERCISE_ID", e."EXERCISE_MIN_LEVEL", e."EXERCISE_NAME", e."EXERCISE_PRIMARY_EQUIPMENT_ID", e."EXERCISE_SECONDARY_EQUIPMENT_ID"
          FROM "EXERCISES" AS e
          WHERE e."EXERCISE_IS_ACTIVE" AND (e."EXERCISE_COACH_ID" IS NULL OR EXISTS (
              SELECT 1
              FROM "MEMBER_COACHES" AS m
              WHERE m."MEMBER_ID" = @__memberId_0 AND m."COACH_ID" = e."EXERCISE_COACH_ID")) AND e."EXERCISE_PRIMARY_MUSCLE_GROUP_ID" = @__mg_1 AND (e."EXERCISE_PRIMARY_EQUIPMENT_ID" = @__eq_2 OR e."EXERCISE_SECONDARY_EQUIPMENT_ID" = @__eq_2)
          ORDER BY e."EXERCISE_NAME", e."EXERCISE_ID"
          LIMIT @__p_3
      ) AS t
      LEFT JOIN "EQUIPMENT" AS e0 ON t."EXERCISE_PRIMARY_EQUIPMENT_ID" = e0."EQUIPMENT_ID"
      LEFT JOIN "EQUIPMENT" AS e1 ON t."EXERCISE_SECONDARY_EQUIPMENT_ID" = e1."EQUIPMENT_ID"
      ORDER BY t."EXERCISE_NAME", t."EXERCISE_ID"


```

Level is an inline `IN` list (call with 5 filters): `e."EXERCISE_MIN_LEVEL" IN ('BEGINNER', 'INTERMEDIATE')`, see `sql-member-list-5-filters.log`.

## V9 — seeder
First start (after the code change), summaries from the API log:
```
MuscleGroupSeeder: created 0 muscle group(s).
CatalogSeeder no-equipment row: created 1, existing 0.
CatalogSeeder equipment: created 0, existing 70, skipped 0, warnings 0.
CatalogSeeder objectives: created 0, existing 53, skipped 0, warnings 0.
CatalogSeeder primary groups: from muscles 248, from label 0, unresolved 0, mismatches 138, warnings 0.
CatalogSeeder extra equipment: created 47, warnings 0.
[CATALOG] equipment created from exercises: 'Banc'
[CATALOG] equipment created from exercises: 'Rack'
[CATALOG] equipment created from exercises: 'Corde / barre'
[CATALOG] equipment created from exercises: 'Vélo'
[CATALOG] equipment created from exercises: 'Poignée simple'
[CATALOG] equipment created from exercises: 'Barre longue'
[CATALOG] equipment created from exercises: 'Poignée neutre'
[CATALOG] equipment created from exercises: 'Poignées parallèles'
[CATALOG] equipment created from exercises: 'Machine à tractions assistées'
[CATALOG] equipment created from exercises: 'Barre large'
[CATALOG] equipment created from exercises: 'T-bar'
[CATALOG] equipment created from exercises: 'Landmine'
[CATALOG] equipment created from exercises: 'Barre fixe'
[CATALOG] equipment created from exercises: 'Anneaux gymnastique'
[CATALOG] equipment created from exercises: 'Barre ou corde'
[CATALOG] equipment created from exercises: 'Machine pullover'
[CATALOG] equipment created from exercises: 'Corde'
[CATALOG] equipment created from exercises: 'Banc lombaire'
[CATALOG] equipment created from exercises: 'Machine élévations latérales'
[CATALOG] equipment created from exercises: 'Support fixe'
[CATALOG] equipment created from exercises: 'Banc ou tapis'
[CATALOG] equipment created from exercises: 'Barre'
[CATALOG] equipment created from exercises: 'Banc pupitre'
[CATALOG] equipment created from exercises: 'Machine à dips assistés'
[CATALOG] equipment created from exercises: 'Haltères ou kettlebells'
[CATALOG] equipment created from exercises: 'Kettlebell ou haltère'
[CATALOG] equipment created from exercises: 'Leg curl'
[CATALOG] equipment created from exercises: 'Support de chevilles'
[CATALOG] equipment created from exercises: 'Sliders'
[CATALOG] equipment created from exercises: 'Sangle cheville'
[CATALOG] equipment created from exercises: 'Machine mollets'
[CATALOG] equipment created from exercises: 'Machine mollets assis'
[CATALOG] equipment created from exercises: 'Mur'
[CATALOG] equipment created from exercises: 'Barre olympique ou haltère'
[CATALOG] equipment created from exercises: 'Hip thrust machine'
[CATALOG] equipment created from exercises: 'Banc ou machine reverse hyper'
[CATALOG] equipment created from exercises: 'Disque ou haltère'
[CATALOG] equipment created from exercises: 'Chaise romaine'
[CATALOG] equipment created from exercises: 'Poignée'
[CATALOG] equipment created from exercises: 'Poids du corps ou medicine ball'
[CATALOG] equipment created from exercises: 'Sliders ou TRX'
[CATALOG] equipment created from exercises: 'Roue abdominale'
[CATALOG] equipment created from exercises: 'Barres parallèles ou parallettes'
[CATALOG] equipment created from exercises: 'Vélo spinning'
[CATALOG] equipment created from exercises: 'Corde ou sangles'
[CATALOG] equipment created from exercises: 'Sangles'
[CATALOG] equipment created from exercises: 'Escaliers'
CatalogSeeder exercises: created 0, adopted 0, already imported 248, filled 138, historical no-equipment 5, skipped 2, objective links added 0, plural-only equipment matches 3, warnings 1.

```

Second start:
```
MuscleGroupSeeder: created 0 muscle group(s).
CatalogSeeder no-equipment row: created 0, existing 1.
CatalogSeeder equipment: created 0, existing 70, skipped 0, warnings 0.
CatalogSeeder objectives: created 0, existing 53, skipped 0, warnings 0.
CatalogSeeder primary groups: from muscles 248, from label 0, unresolved 0, mismatches 138, warnings 0.
CatalogSeeder extra equipment: created 0, warnings 0.
CatalogSeeder exercises: created 0, adopted 0, already imported 248, filled 0, historical no-equipment 0, skipped 2, objective links added 0, plural-only equipment matches 3, warnings 1.

```

SQL state (`verify-9.4-seeder.sql`), run after the cleanup, so the fixture exercises are gone (coach-owned block empty, `ex` 275); the same query before the cleanup gave `ex 278` and the three Lot94 rows with NULL equipment and source code:
```
== "Sans matériel" row
             EQUIPMENT_ID             | EQUIPMENT_SOURCE_CODE | EQUIPMENT_NAME | EQUIPMENT_CATEGORY | EQUIPMENT_SUBCATEGORY | EQUIPMENT_LEGACY_CODE | EQUIPMENT_IS_ACTIVE 
--------------------------------------+-----------------------+----------------+--------------------+-----------------------+-----------------------+---------------------
 944fec76-b2b3-4c93-9e7e-1506666fe773 |                       | Sans matériel  | Sans matériel      |                       | BODYWEIGHT            | t
(1 row)

== EQUIPMENT rows by origin
            origin            | n  
------------------------------+----
 created from exercises (7.2) | 47
 MATERIELS sheet              | 70
 Sans matériel (7.1)          |  1
(3 rows)

== EQUIPMENT rows created by 7.2 (category Autre)
          EQUIPMENT_NAME          | EQUIPMENT_LEGACY_CODE | exercises_using_it 
----------------------------------+-----------------------+--------------------
 Anneaux gymnastique              | OTHER                 |                  1
 Banc                             | OTHER                 |                 12
 Banc lombaire                    | OTHER                 |                  2
 Banc ou machine reverse hyper    | MACHINE               |                  1
 Banc ou tapis                    | OTHER                 |                  1
 Banc pupitre                     | OTHER                 |                  2
 Barre                            | BARBELL               |                  6
 Barre fixe                       | BARBELL               |                  1
 Barre large                      | BARBELL               |                  1
 Barre longue                     | BARBELL               |                  2
 Barre olympique ou haltère       | DUMBBELL              |                  1
 Barre ou corde                   | BARBELL               |                  1
 Barres parallèles ou parallettes | BARBELL               |                  1
 Chaise romaine                   | OTHER                 |                  1
 Corde                            | OTHER                 |                  4
 Corde / barre                    | BARBELL               |                  1
 Corde ou sangles                 | OTHER                 |                  1
 Disque ou haltère                | DUMBBELL              |                  1
 Escaliers                        | OTHER                 |                  1
 Haltères ou kettlebells          | DUMBBELL              |                  6
 Hip thrust machine               | MACHINE               |                  1
 Kettlebell ou haltère            | DUMBBELL              |                  2
 Landmine                         | OTHER                 |                  3
 Leg curl                         | OTHER                 |                  1
 Machine à dips assistés          | MACHINE               |                  1
 Machine à tractions assistées    | MACHINE               |                  1
 Machine élévations latérales     | MACHINE               |                  1
 Machine mollets                  | MACHINE               |                  1
 Machine mollets assis            | MACHINE               |                  1
 Machine pullover                 | MACHINE               |                  1
 Mur                              | OTHER                 |                  1
 Poids du corps ou medicine ball  | BODYWEIGHT            |                  1
 Poignée                          | OTHER                 |                  2
 Poignée neutre                   | OTHER                 |                  2
 Poignée simple                   | OTHER                 |                 12
 Poignées parallèles              | OTHER                 |                  1
 Rack                             | OTHER                 |                  2
 Roue abdominale                  | OTHER                 |                  1
 Sangle cheville                  | OTHER                 |                  3
 Sangles                          | OTHER                 |                  1
 Sliders                          | OTHER                 |                  1
 Sliders ou TRX                   | OTHER                 |                  1
 Support de chevilles             | OTHER                 |                  1
 Support fixe                     | OTHER                 |                  4
 T-bar                            | OTHER                 |                  1
 Vélo                             | OTHER                 |                  1
 Vélo spinning                    | OTHER                 |                  1
(47 rows)

== Exercises linked to "Sans matériel" (primary), split
                   kind                   | n  
------------------------------------------+----
 historical BODYWEIGHT (not in the sheet) |  5
 sheet exercises                          | 44
(2 rows)

== "Sans matériel" as secondary equipment (must be 0)
 n 
---
 0
(1 row)

== Active sheet exercises with NULL primary equipment (must be 0)
 n 
---
 0
(1 row)

== Historical Mentora exercises (no source code): legacy equipment vs primary equipment
 legacy_equipment | n | primary_null | primary_sans_materiel 
------------------+---+--------------+-----------------------
 BARBELL          | 5 |            5 |                     0
 BODYWEIGHT       | 5 |            0 |                     5
 CABLE            | 4 |            4 |                     0
 DUMBBELL         | 9 |            9 |                     0
 ELASTIC          | 1 |            1 |                     0
 KETTLEBELL       | 1 |            1 |                     0
 MACHINE          | 2 |            2 |                     0
(7 rows)

== Coach-owned exercises: catalog columns untouched
 EXERCISE_NAME | primary_equipment_null | source_code_null 
---------------+------------------------+------------------
(0 rows)

== Counts
 ex  | eq  | obj | exobj | mg 
-----+-----+-----+-------+----
 275 | 118 |  53 |   561 | 14
(1 row)

== Integrity
           chk           | n 
-------------------------+---
 dup equipment name (ci) | 0
 dup source code         | 0
(2 rows)


```

EX001 test: `EXERCISE_NAME_EN` set to NULL and committed (pre-approved), then restart:
```
before: EX001 | Barbell Bench Press
after UPDATE: EX001 | (NULL)

start 3: CatalogSeeder exercises: created 0, adopted 0, already imported 248, filled 1, historical no-equipment 0, skipped 2, objective links added 0, plural-only equipment matches 3, warnings 1.
value after start 3: EX001 | Barbell Bench Press | updated 2026-09-29 18:58:57+00
start 4: CatalogSeeder exercises: created 0, adopted 0, already imported 248, filled 0, historical no-equipment 0, skipped 2, objective links added 0, plural-only equipment matches 3, warnings 1.

```

Later restarts (5, 6, needed for token retrieval) also logged `filled 0`, `created 0`.

## V10 — Swagger diff against the preflight baseline
Mobile document:
```
Added paths:
  + GET /api/v1/member/exercise-filters  [Member � Exercises]
Removed paths:
  (none)
Changed operations:
  GET /api/v1/member/exercises
    + query/param cursor (string)
    + query/param equipmentId (string(uuid))
    + query/param level (string)
    + query/param limit (integer(int32))
    + query/param muscleGroupId (string(uuid))
    + query/param objectiveId (string(uuid))
    + query/param search (string)
    ~ responses ['200', '401', '403'] -> ['200', '400', '401', '403']
Schemas:

```

Coach document:
```
Added paths:
  + GET /api/v1/coach/exercise-filters  [Coach � Exercises]
Removed paths:
  (none)
Changed operations:
  GET /api/v1/coach/exercises
    + query/param equipmentId (string(uuid))
    + query/param level (string)
    + query/param muscleGroupId (string(uuid))
    + query/param objectiveId (string(uuid))
    ~ param equipment: deprecated False -> True
    ~ param muscleGroup: deprecated False -> True
Schemas:

```

Response bodies are typed `object` in Swagger (existing convention), so no response schema appears in the diff. Field-level changes for the mobile developer:
- `GET member/exercises`: **breaking change of shape.** Was `data: ExerciseResponse[]` (all 275 items, 13 keys); now `data: { items: [{ exerciseId, name, level, primaryEquipment, secondaryEquipment }], nextCursor }`, default 50 per page. `level` is `BEGINNER|INTERMEDIATE|ADVANCED|null`; equipment is `{ equipmentId, name } | null`.
- `GET member/exercises/{id}`, `GET coach/exercises` items, `GET coach/exercises/{id}`, POST/PUT coach: the 13 legacy keys unchanged (same order); 19 keys added: `sourceCode, nameEn, category, movementPattern, exerciseType, laterality, startingPosition, gripType, primaryMuscleGroup{muscleGroupId,name}|null, secondaryMuscles[], primaryEquipment{equipmentId,name}|null, secondaryEquipment|null, level|null, breathing, commonMistakes[], precautions, coachTip, objectives[{objectiveId,name}], video|null`. `muscleGroup` and `equipment` are deprecated (removed in Lot 10).
- New: `GET member/exercise-filters`, `GET coach/exercise-filters`. Removed fields: none.

## V11 — build
`dotnet build --no-incremental`: 19 warnings (baseline 19), 0 errors.

## Fixture cleanup
Dry run (`cleanup-dry-run.sql`, ROLLBACK) reviewed, then `cleanup.sql` (only comments and ROLLBACK→COMMIT differ, checked with `git diff --no-index`). Full output (`cleanup-output.txt`):
```
BEGIN
             EXERCISE_ID              |         EXERCISE_NAME          |          EXERCISE_COACH_ID           
--------------------------------------+--------------------------------+--------------------------------------
 1eebb781-3f3c-42ec-8842-b109b0d5405f | Lot94 exercice coach A         | e268a512-84d6-4e27-b5c0-1d6c08a47190
 41f04f69-cb37-47c5-9148-45ba401f7e50 | Lot94 exercice coach A inactif | e268a512-84d6-4e27-b5c0-1d6c08a47190
 5b4e9c1a-0c1d-4a55-9d3e-940400000003 | Lot94 exercice coach B         | 5b4e9c1a-0c1d-4a55-9d3e-940400000002
(3 rows)

DELETE 3
           OFFER_PROGRAM_ID           
--------------------------------------
 00403477-5749-476a-9afe-cf1d3d0722c2
 496d3471-962c-4147-8fb4-9ef2a26372aa
 2d6ba7c8-f605-4431-95ba-785d98a7482e
 58e3f45b-61ff-4bbe-a493-b8a3886da1fe
(4 rows)

DELETE 4
          COACH_PARAMETER_ID          
--------------------------------------
 3b1c0a41-9527-4759-b007-c475f3e0fc65
(1 row)

DELETE 1
               COACH_ID               
--------------------------------------
 5b4e9c1a-0c1d-4a55-9d3e-940400000002
(1 row)

DELETE 1
               USER_ID                |       USER_EMAIL        
--------------------------------------+-------------------------
 5b4e9c1a-0c1d-4a55-9d3e-940400000001 | coachb.lot94@mentora.fr
(1 row)

DELETE 1
 fixture_users | fixture_coaches | fixture_exercises | exercises_total | equipment_total | users_total 
---------------+-----------------+-------------------+-----------------+-----------------+-------------
             0 |               0 |                 0 |             275 |             118 |           2
(1 row)

COMMIT
 fixture_users | fixture_exercises | exercises_total 
---------------+-------------------+-----------------
             0 |                 0 |             275
(1 row)


```

Kept as product data: the "Sans matériel" row, the 47 equipment rows created by 7.2 and their links (EQUIPMENT 118).

## Deviations
- Whitespace-only or empty `search` is bound to null by ASP.NET and treated as absent (no 400); `search=a` and 61 characters give 400.
- `levels` in the filter endpoints is the constant three-value list, not restricted to levels in use.
- 7.2 reads the eligible exercises only, so EX135 (skipped, duplicate id) creates no equipment. Plural variants merge into the first-seen name (e.g. "Haltère ou kettlebell" into "Haltères ou kettlebells"), giving 47 rows.
- 7.5 counts an exercise as "filled" when a column or an objective link was added (first run: 138).
- The level filter uses inline array literals (EF Core 8 has no `EF.Constant`) to obtain a constant `IN` list.
- Malformed GUID query values (e.g. `muscleGroupId=abc`) are rejected by ASP.NET model binding with its standard 400, not the `{ success, … }` envelope.
- Swagger `level` parameter is constrained to its three values; no response schemas (see V10).
- Auth rows created by the OTP flow were not deleted (as in 9.2 and 9.3).
