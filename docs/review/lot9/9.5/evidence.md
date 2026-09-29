# Lot 9.5 — runtime evidence

Local only (`mentora-postgres-local` port 5433, API port 5243). Branch feature/lot_9, base HEAD e93776f. No schema change, no migration, no endpoint change. Templates inventory: `templates.md`.

Scripts and raw outputs in this folder: `expected-templates.sql/.txt`, `verify-9.5-v3.ps1`, `v3-output.txt`, `v4-member-filter.ps1`, `v4-pectoraux-before/after.txt`, `v1-historical-before/after.txt`, `v2-seeder-summaries.txt`, `fixture-01-member-m3.sql`, `cleanup.sql`, `cleanup-dry-run-output.txt`, `cleanup-output.txt`. The raw API logs (`api-run-*.log`) contain one-time codes and are not committed. Tokens were kept outside the repo.

## Code change (rule 7.6, `CatalogSeeder.cs`)
Historical Mentora exercises (`EXERCISE_SOURCE_CODE` NULL, `EXERCISE_COACH_ID` NULL) with a NULL `EXERCISE_PRIMARY_MUSCLE_GROUP_ID` get their group from the legacy `EXERCISE_MUSCLE_GROUP` code (CHEST Pectoraux, BACK Dos, SHOULDERS Épaules, BICEPS/TRICEPS Bras, FOREARMS Avant-bras, ABS Abdominaux, QUADRICEPS Quadriceps, HAMSTRINGS Ischio-jambiers, GLUTES Fessiers, CALVES Mollets). FULL_BODY and CARDIO stay NULL. Fill-only-NULL; coach-owned rows are not in the loaded set. Logs `CatalogSeeder historical groups filled: N.`

## V1 — first start
Before (SQL, historical rows): all 27 had a NULL group (`v1-historical-before.txt`).
```
CatalogSeeder no-equipment row: created 0, existing 1.
CatalogSeeder equipment: created 0, existing 70, skipped 0, warnings 0.
CatalogSeeder objectives: created 0, existing 53, skipped 0, warnings 0.
CatalogSeeder primary groups: from muscles 248, from label 0, unresolved 0, mismatches 138, warnings 0.
CatalogSeeder extra equipment: created 0, warnings 0.
CatalogSeeder historical groups filled: 23.
CatalogSeeder exercises: created 0, adopted 0, already imported 248, filled 0, historical no-equipment 0, skipped 2, ...
```
After: 23 historical exercises have a group, matching their legacy code (ABS 3 Abdominaux, BACK 2 Dos, BICEPS 3 + TRICEPS 2 Bras, CALVES 1 Mollets, CHEST 1 Pectoraux, FOREARMS 3 Avant-bras, GLUTES 2 Fessiers, HAMSTRINGS 1 Ischio-jambiers, QUADRICEPS 2 Quadriceps, SHOULDERS 3 Épaules). Still NULL (4, as expected):
```
 Burpees               | FULL_BODY
 Course à pied (tapis) | CARDIO
 Kettlebell swing      | FULL_BODY
 Thruster haltères     | FULL_BODY
```

## V2 — second start
```
CatalogSeeder no-equipment row: created 0, existing 1.
CatalogSeeder equipment: created 0, existing 70, skipped 0, warnings 0.
CatalogSeeder objectives: created 0, existing 53, skipped 0, warnings 0.
CatalogSeeder extra equipment: created 0, warnings 0.
CatalogSeeder historical groups filled: 0.
CatalogSeeder exercises: created 0, adopted 0, already imported 248, filled 0, historical no-equipment 0, skipped 2, objective links added 0, ...
```
Every created / filled count is 0.

## V3 — every active Mentora template, real coach A JWT (OTP flow on coach@mentora.fr)
Fixture: member M3 (`5b4e9c1a-0c1d-4a55-9d3e-950500000002`) attached to coach A by a MEMBER_COACHES row (`fixture-01-member-m3.sql`). `member@mentora.fr` not used. Full output in `v3-output.txt`; result `ALL OK`:

| template | POST | GET | blocks | sessions | circuits | exercises | ids identical to body | unresolved |
|---|---|---|---|---|---|---|---|---|
| Prise de masse — débutant | 201 | 200 | 6 = 6 | 12 = 12 | 16 = 16 | 52 = 52 | yes | 0 |
| Remise en forme — débutant | 201 | 200 | 4 = 4 | 12 = 12 | 16 = 16 | 36 = 36 | yes | 0 |

Expectations come from SQL on the template bodies (`expected-templates.sql`). The second assignment archived the first program on M3 (`archivedProgramId` returned), as designed.

## V4 — member filter, chest (`muscleGroupId` = Pectoraux)
`GET api/v1/member/exercises?muscleGroupId=d6660410-7110-4ac5-8f08-c90ac7f1288d`, member JWT:
- before 7.6 (old build): 30
- after 7.6: 31 (SQL: 31 active coach-less exercises with group Pectoraux)
- difference: `Écarté couché haltères`, the only historical CHEST exercise.

## V5 — build
`dotnet build --no-incremental`: 0 errors, 19 warnings (unchanged from the 19 at preflight).

## Fixture cleanup
Dry run (`ROLLBACK`, `cleanup-dry-run-output.txt`), then real run after approval (`cleanup-output.txt`). Deleted: 88 program exercises, 32 circuits, 24 sessions, 10 blocks (8 micro, 1 meso, 1 macro), 2 programs, 1 MEMBER_COACHES, 1 MEMBERS, 1 USERS; all bounded by the fixed UUIDs, the two V3 program ids and creation date ≥ 2026-09-29 19:54:15 UTC. After COMMIT: 0 programs, blocks, sessions, circuits and program exercises; M3, its user and its link gone; `PROGRAM_TEMPLATES` still 2.

## Notes
- Auth rows from the OTP flow (`AUTH_OTPS`, `AUTH_REFRESH_TOKENS`) were not deleted, as in 9.2 to 9.4.
- For the V4 baseline the change was stashed, the old build run, then the stash restored.
- Group mapping is by the French names given in the brief; a name missing from MUSCLE_GROUPS would leave the row NULL silently (all 11 names resolved in this run).
