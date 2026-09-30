# Lot 10.1 - Runtime evidence

Local only (`mentora-postgres-local` port 5433, API port 5243, Development). Branch feature/lot_10, base fd3c69c.
Run start: see `run-start.txt` (2026-09-30 09:03:10 UTC). No endpoint in this sub-lot.
API logs (`api-run-*.log`, `*.err.log`) are gitignored (`*.log`) because they can contain OTP codes.

## Build
`dotnet build --no-incremental`: **0 errors, 19 warnings** (baseline before the change: 0 errors, 19 warnings, unchanged).
`dotnet ef migrations add Lot10_Quiz` printed no warning mentioning any Quiz entity (QuizQuestionIsActive included).

## 7a. First run
- Migration `20260930085059_Lot10_Quiz` applied at startup.
- `QuizQuestionSeeder: created 900, existing 0, filled 0, skipped 74, warnings 74.`
- One aggregated warning line: 74 placeholder rows (only Statut filled), CSV lines 902 to 975.

## 7b. Second run
- `QuizQuestionSeeder: created 0, existing 900, filled 0, skipped 74, warnings 74.`

## 7c. Read-only SQL (`v1-readonly.sql`, output `v1-readonly-output.txt`)
| Check | Result |
|---|---|
| Rows QUIZ_QUESTIONS / QUIZ_SESSIONS / QUIZ_SESSION_QUESTIONS / QUIZ_DEVICE_CLAIMS | 900 / 0 / 0 / 0 |
| By SOURCE_STATUS | Brouillon 900 |
| By CORRECT_ANSWER | A 324, B 232, C 189, D 155 |
| POINTS sum / min / max | 7825 / 5 / 15 |
| Samples | QUE-001 B 5; QUE-002 C 5; QUE-003 B 10 (accents and typographic apostrophes intact) |
| CSV codes not skipped found in table | 900 of 900, missing 0 |
| Table codes absent from the CSV | 0 |

## 7d. CHECK proof (`v2-checks.sql`, output `v2-checks-output.txt`)
One transaction ending with ROLLBACK, one SAVEPOINT per attempt.
| Attempt | Result |
|---|---|
| Question with correct answer 'E' | `CK_QUIZ_QUESTIONS_CORRECT_ANSWER` |
| Question with points -1 | `CK_QUIZ_QUESTIONS_POINTS` |
| Session with neither coach nor device | `CK_QUIZ_SESSIONS_OWNER` |
| COMPLETED session without score | `CK_QUIZ_SESSIONS_COMPLETION` |
| Valid IN_PROGRESS device session | inserted (1 row) |
| Session question with chosen answer but IS_CORRECT NULL | `CK_QUIZ_SESSION_QUESTIONS_ANSWER_STATE` |
| After ROLLBACK | 0 test rows left |
