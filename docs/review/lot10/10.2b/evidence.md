# Lot 10.2b - evidence: exercise search restricted to names and ranked by relevance

Branch `feature/lot_10`, on top of `246e2bd`. No migration (`dotnet ef migrations has-pending-model-changes`: "No changes have been made to the model since the last migration"). No response DTO changed. Local only (postgres 5433, API 5243, Development). Nothing written to the business data: only GET requests and SELECTs; the only rows written are the technical login rows (OTP / refresh token) of `coach@mentora.fr` and `member@mentora.fr`, as in lots 9.x.

## Change
- `ExerciseSearch.WithRank` (new, `Mentora.Infrastructure/Services/ExerciseSearch.cs`): one SQL expression, used by both lists. Both the columns and the term go through the same `lower(translate(x, accented, ascii))`, so case and accents are ignored on both sides. Rank 0: name starts with the term; 1: a word of the name starts with it (after space, `-`, `'`, `’`, `(`); 2: name contains it; 3: only the English name or the search tags contain it. `LIKE` metacharacters of the term are escaped.
- `MentoraDbFunctions.Translate` mapped to PostgreSQL's built-in `translate()` in `MentoraDbContext` (`HasDbFunction(...).IsBuiltIn()`): no `unaccent` extension, no schema change.
- Member list (`ListForMemberAsync`): with a search, `ORDER BY rank, name, id`, cursor `{"Rank","Name","ExerciseId"}`; without a search the code path, order and cursor format are unchanged. Filters and visibility rules are applied before the ranking, exactly as before.
- Coach list (`ListForCoachAsync`): same matching and ranking when `search` is present (rank, name, id); without search unchanged. The coach list was name-only and unpaginated; it now also matches the English name and tags at rank 3 so that both endpoints follow the same final decision. Flagged because it widens the coach result set (22 instead of 20 results for `dév`).
- Swagger: the `search` parameter description of both endpoints states name + alternative names only, case/accent insensitive, ranked by relevance (`swagger-search-param.txt`, read from the live swagger docs).

## Finding that differs from the brief
Accent insensitivity did not exist (ILIKE only; `search=dev` returned 0 results before). S2 (dev = DÉV = dév) therefore required adding accent folding, done with `translate()` (32 accented letters `àâäÀÂÄéèêëÉÈÊËîïÎÏôöÔÖùûüÙÛÜÿŸçÇ`). Ligatures (`œ`, `æ`) are not folded. Search results are a superset of before for the same term (accented terms return the same set, unaccented terms now return results).

## Build
Baseline **0 errors, 19 warnings**; after the change **0 errors, 19 warnings**.

## Runtime proof (`verify-output.md`, script `verify-10.2b.py`, captured responses in `baseline/` and `after/`)
Result: **ALL CHECKS OK**. Baseline run start `2026-09-30T14:28:30Z`, after run `2026-09-30T14:40:12Z`.

| Scenario | Result |
|---|---|
| S1 `search=dév` (member) | 22 results, list and order equal to an independent SQL implementation of the ranking. 17 names start with "Dév" (rank 0), 3 more contain it as a word (`Smith machine développé ...`, rank 1), then 2 rank-3 results: `Chest press machine` (position 21) and `Shoulder press machine` (22). Before: `Chest press machine` was first. Why it is still returned: its name and English name do not contain dév/dev; only `EXERCISE_SEARCH_TAGS` (`développé machine`) does, i.e. an alias, so it is rank 3, after every name match. |
| S2 `dev`, `DÉV` | byte-identical to `dév` (baseline: `dev` returned 0, `DÉV` 22) |
| S3 `dév` limit=2 | 11 pages; concatenation == limit=50 list, no duplicate, none missing, same order; last page `nextCursor` null. Cursor: `{"Rank": 0, "Name": "...", "ExerciseId": "..."}` |
| S4 no search | `m_nosearch` (limit 50, 6 pages, 275 items), limit 7 (40 pages) and blank search `"   "` (treated as no search): responses identical to the baseline on every page, including `nextCursor` (cursor format unchanged: `{"Name","ExerciseId"}`) |
| S5 `dév` + equipmentId Haltères | 9 results equal to SQL; same set as the baseline (order now ranked) |
| S6 | no match: `items: []`, `nextCursor: null`; malformed cursor (with and without search) and wrong-shape cursor `{}`: same status and body as the baseline (400 `Invalid cursor format.` / `Invalid cursor.`); an old-format (name, id) cursor sent with a search: 400 `Invalid cursor.`; `%%`, `de_`, `\\` are literal (0 results) |
| S7 | member search: **1 query** per request (`translate`/`CASE` rank, `ORDER BY`, `LIMIT` in SQL, `sql-generated.md`); member without search: 1 query (unchanged); coach search: 2 queries (split query: main + objectives) |
| S8 coach | list equals SQL ranking (22 results), names starting with Dév first, `dev`/`DÉV` identical, the 20 items returned before are byte-identical (details intact), no search / blank search identical to the baseline (275 items), no match empty, `dév` + Haltères equals SQL. S3 not applicable (the coach list has no pagination). |

## Files
`inventory.md`, `evidence.md`, `inventory-queries.sql` + `-output.txt`, `capture.py`, `verify-10.2b.py`, `verify-output.md`, `sql-generated.md`, `swagger-search-param.txt`, `baseline/` and `after/` (captured JSON responses, no token inside), `get-tokens.ps1`, `start-api.sh`, `baseline-run-start.txt`, `after-run-start.txt`. The API logs (`api-run-*.log`, OTP codes) are gitignored and not committed; the tokens file lived outside the repository.
