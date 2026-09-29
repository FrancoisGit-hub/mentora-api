# Lot 9.2 — Exercise catalog schema: runtime evidence

Local only (`mentora-postgres-local`, port 5433; API on 5243). Migration `20260929171450_Lot9_ExerciseCatalog`.

## Preflight
- Branch `feature/lot_9`, HEAD `4f5a1c0`, working tree clean, `dotnet build`: 19 warnings, 0 errors.
- Baseline: `SELECT count(*), count(*) FILTER (WHERE "EXERCISE_COACH_ID" IS NULL) FROM "EXERCISES"` → `45 | 45`.

## Naming collisions
No class/enum named `Objective`, `ExerciseVideo`, `ExerciseObjective`, `ExerciseLevel`, `ExerciseVideoStatus`, `VideoPlaybackPolicy`, `MuscleGroupItem`, `EquipmentItem` existed (regex search on `*.cs`). No equivalent level enum in `Mentora.Core.Enums`, so `ExerciseLevel` was created.

## V1 — first start
```
MuscleGroupSeeder: created 14 muscle group(s).
count = 14
 1 Pectoraux CHEST | 2 Dos BACK | 3 Épaules SHOULDERS | 4 Bras BICEPS | 5 Avant-bras FOREARMS
 6 Quadriceps QUADRICEPS | 7 Ischio-jambiers HAMSTRINGS | 8 Fessiers GLUTES | 9 Adducteurs GLUTES
 10 Mollets CALVES | 11 Tibial CALVES | 12 Abdominaux ABS | 13 Centre du corps ABS | 14 Hanche GLUTES
```

## V2 — restart
```
MuscleGroupSeeder: created 0 muscle group(s).
count = 14
```

## V3 — EXERCISES untouched
`total 45, coach_null 45`, and every new column has `count(*) FILTER (WHERE col IS NOT NULL) = 0` (18 columns).

## V4 — constraints and indexes
CHECKs: `CK_EXERCISES_MIN_LEVEL`, `CK_EQUIPMENT_LEGACY_CODE`, `CK_EXERCISE_VIDEOS_POLICY`, `CK_EXERCISE_VIDEOS_STATUS`, `CK_MUSCLE_GROUPS_LEGACY_CODE`.
Partial unique indexes: `UX_EXERCISES_SOURCE_CODE`, `UX_EQUIPMENT_SOURCE_CODE` (`WHERE ... IS NOT NULL`).
Other new indexes: `IX_EXERCISES_MIN_LEVEL`, `IX_EXERCISES_PRIMARY_EQUIPMENT`, `IX_EXERCISES_PRIMARY_MUSCLE_GROUP`, `IX_EXERCISES_SECONDARY_EQUIPMENT`, `IX_EXERCISE_OBJECTIVES_OBJECTIVE`, `UX_EQUIPMENT_NAME`, `UX_EXERCISE_VIDEOS_EXERCISE`, `UX_EXERCISE_VIDEOS_MUX_ASSET`, `UX_MUSCLE_GROUPS_NAME`, `UX_OBJECTIVES_NAME`, plus the five PKs. The pre-existing `IX_EXERCISES_COACH_NAME` (`NULLS NOT DISTINCT`) is intact.

## V5 — CHECKs, rolled-back transactions
```
ERROR:  new row for relation "EXERCISE_VIDEOS" violates check constraint "CK_EXERCISE_VIDEOS_STATUS"
ERROR:  new row for relation "EXERCISES" violates check constraint "CK_EXERCISES_MIN_LEVEL"
videos = 0, levels_set = 0 after both ROLLBACKs
```

## V6 — public API unchanged
```
GET /api/v1/coach/exercises  -> HTTP 200; envelope: success,data,error,statusCode; items: 45
  keys: exerciseId,coachId,name,description,instructions,videoUrl,imageUrl,muscleGroup,equipment,isPolyarticular,isActive,createdDate,updatedDate
GET /api/v1/member/exercises -> HTTP 200; envelope: success,data,error,statusCode; items: 45
  keys: (identical)
Test-Path token file: False
```
Tokens obtained through the real OTP flow for the existing `coach@mentora.fr` / `member@mentora.fr`. Side effect: AUTH_OTPS 39→41 and AUTH_REFRESH_TOKENS 38→40 (technical auth rows); USERS stays 2. No fixture rows.

## V7 — build
`dotnet build --no-incremental`: 19 warnings (same as the 9.1 baseline), 0 errors.

## Final row counts
`EXERCISES 45, EXERCISE_VIDEOS 0, EQUIPMENT 0, OBJECTIVES 0, MUSCLE_GROUPS 14`.
