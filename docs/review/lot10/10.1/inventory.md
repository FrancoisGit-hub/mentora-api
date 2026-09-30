# Lot 10.1 - Inventory (read-only preflight)

## 2a. Repository state
- Branch: `feature/lot_10`
- `git status --short`: only `?? Mentora.Infrastructure/Seeding/Quiz/` (expected, QUESTIONS.csv committed in this sub-lot)
- `git log --oneline -1`: `fd3c69c docs(lot9): 9.6 review fixes runtime evidence`

## 2b. QUESTIONS.csv
- Encoding: UTF-8 **without BOM** (first bytes `ID `), strict-UTF-8 valid
- Delimiter: `,` (quoted fields, CRLF line endings, no trailing newline)
- Header row: `ID question,ID matière,Matière,ID chapitre,Nom du chapitre,ID fiche,Titre de la fiche,Question,Réponse A,Réponse B,Réponse C,Réponse D,Bonne réponse,Explication,Difficulté,XP,Statut`
- Data rows: **974** (as expected). 900 are real questions (`QUE-...`), 74 are placeholder rows
  containing only `Statut = Brouillon` (every other column empty, empty code).

## 2c. Header mapping: CSV header -> target column (table QUIZ_QUESTIONS)
| CSV header | Target column | Nullable |
|---|---|---|
| ID question | QUIZ_QUESTION_SOURCE_CODE | no |
| ID matière | QUIZ_QUESTION_SUBJECT_CODE | yes |
| Matière | QUIZ_QUESTION_SUBJECT | yes |
| ID chapitre | QUIZ_QUESTION_CHAPTER_CODE | yes |
| Nom du chapitre | QUIZ_QUESTION_CHAPTER | yes |
| ID fiche | QUIZ_QUESTION_CARD_CODE | yes |
| Titre de la fiche | QUIZ_QUESTION_CARD_TITLE | yes |
| Question | QUIZ_QUESTION_TEXT | no |
| Réponse A | QUIZ_QUESTION_ANSWER_A | no |
| Réponse B | QUIZ_QUESTION_ANSWER_B | no |
| Réponse C | QUIZ_QUESTION_ANSWER_C | no |
| Réponse D | QUIZ_QUESTION_ANSWER_D | no |
| Bonne réponse | QUIZ_QUESTION_CORRECT_ANSWER | no |
| Explication | QUIZ_QUESTION_EXPLANATION | yes |
| Difficulté | QUIZ_QUESTION_DIFFICULTY | yes |
| XP | QUIZ_QUESTION_POINTS | no |
| Statut | QUIZ_QUESTION_SOURCE_STATUS | yes |

17 headers, 17 distinct target columns: every column of 4a fed by the file has exactly one header,
and no two headers can match the same column. Not from the file: ID, IS_ACTIVE, created/updated dates.
No stop condition.

## 2d. Data profile (974 rows)
| Check | Result |
|---|---|
| Duplicate non-empty "ID question" | 0 |
| Empty "ID question" | 74 (the placeholder rows) |
| Empty question / empty answer A, B, C, D | 74 each (same 74 rows) |
| "Bonne réponse" outside A-D | 74 (empty, same rows); A 324, B 232, C 189, D 155 |
| "XP" not an integer >= 0 | 74 (empty, same rows); 5: 381, 10: 373, 15: 146 |
| Empty "Explication" | 74 (same rows) |
| Difficulté | Facile 381, Intermédiaire 373, Difficile 146, empty 74 |
| Statut | Brouillon 974 (single value) |
| ID matière / Matière | MAT-001 Méthodologie de l’entraînement 134; MAT-002 Physiologie 246; MAT-003 Anatomie 260; MAT-004 Sciences humaines 260; empty 74 |
| Distinct chapters | 186 (by ID and by name) |
| Distinct cards (ID fiche) | 209 |
| Max observed length vs column limit | see table below (nothing exceeds its limit) |
| Multi-line cells | 0 today (seeder still keeps line breaks) |

Maximum length observed per varchar column (trimmed, over the 900 non-placeholder rows):

| CSV header | Column | Max observed | Limit |
|---|---|---|---|
| ID question | QUIZ_QUESTION_SOURCE_CODE | 7 | 20 |
| ID matière | QUIZ_QUESTION_SUBJECT_CODE | 7 | 20 |
| Matière | QUIZ_QUESTION_SUBJECT | 30 | 200 |
| ID chapitre | QUIZ_QUESTION_CHAPTER_CODE | 7 | 20 |
| Nom du chapitre | QUIZ_QUESTION_CHAPTER | 53 | 200 |
| ID fiche | QUIZ_QUESTION_CARD_CODE | 7 | 20 |
| Titre de la fiche | QUIZ_QUESTION_CARD_TITLE | 53 | 300 |
| Difficulté | QUIZ_QUESTION_DIFFICULTY | 13 | 50 |
| Statut | QUIZ_QUESTION_SOURCE_STATUS | 9 | 50 |

Expected seeder result on first run: created 900, existing 0, filled 0, skipped 74, warnings 74
(74 placeholder rows, one aggregated log line). Second run: created 0, existing 900, filled 0, skipped 74, warnings 74.

## 2e. Collision check
`grep -i quiz` over `*.cs, *.csproj, *.json, *.md`: no match. No existing Quiz code, table or route.

## 2f. Baseline build (`dotnet build --no-incremental`)
- Errors: **0**
- Warnings: **19** (reference for section 6)
