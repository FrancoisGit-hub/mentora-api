# Lot 10.2b runtime proof - executed 2026-09-30T14:42:30Z
baseline run start: 2026-09-30T14:28:30Z, after run start: 2026-09-30T14:40:12Z

## S1 search=dév (member)
- baseline (before the change): 22 results, first = `Chest press machine` (sorted by name, no relevance)
- after: 22 results, first = `Développé alterné haltères`; ranks from SQL: [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 3, 3]
- **list equals the independent SQL ranking (ids, order)**: OK 
- **every name starting with Dév comes before any other result**: OK (17 names start with Dév, positions 0..16)
- `Chest press machine`: position 21 of 22, rank 3. Its name does not contain dév/dev; the only field that matches is EXERCISE_SEARCH_TAGS (`développé machine`), an alternative-name field, hence rank 3 (after every name match).
- **Chest press machine is last group (rank 3)**: OK 
- returned order: Développé alterné haltères | Développé couché barre | Développé couché haltères | Développé couché prise serrée | Développé couché prise serrée triceps | Développé décliné barre | Développé décliné haltères | Développé épaules barre | Développé épaules haltères assis | Développé épaules haltères debout | Développé épaules unilatéral haltère | Développé haltères prise neutre | Développé incliné barre | Développé incliné haltères | Développé militaire barre assis | Développé militaire barre debout | Développé unilatéral haltère | Smith machine développé couché | Smith machine développé épaules | Smith machine développé incliné | Chest press machine | Shoulder press machine

## S2 dev / DÉV give the same list as dév
- **m_dev_ascii identical to m_dev (after)**: OK 
- **m_DEV_upper identical to m_dev (after)**: OK 
- for reference the baseline gave 0 results for `dev` (accent-sensitive ILIKE) and 22 for `DÉV`

## S3 pagination limit=2
- **concatenated pages == limit=50 list (order, no duplicate, none missing)**: OK (11 pages)
- **last page has nextCursor null, others not**: OK 
- cursor content (search): `{"Rank": 0, "Name": "Développé couché barre", "ExerciseId": "1194fd80-be00-4630-907e-80609ef0f331"}`

## S4 without search: identical to the baseline
- **m_nosearch: responses identical (every page, incl. nextCursor)**: OK (6 pages, 275 items)
- **m_nosearch_limit7: responses identical (every page, incl. nextCursor)**: OK (40 pages, 275 items)
- **m_blank: responses identical (every page, incl. nextCursor)**: OK (6 pages, 275 items)
- no-search cursor format (unchanged): `{"Name":"Curl poulie unilat\u00E9ral","ExerciseId":"5b3cb685-d906-45df-9c8c-8da8a4862872"}`

## S5 search=dév + equipmentId (Haltères)
- **equals SQL**: OK (9 results)
- **same result set as the baseline (only the order may differ)**: OK 
- order: Développé alterné haltères | Développé couché haltères | Développé décliné haltères | Développé épaules haltères assis | Développé épaules haltères debout | Développé épaules unilatéral haltère | Développé haltères prise neutre | Développé incliné haltères | Développé unilatéral haltère

## S6 no match / blank / malformed cursors
- **no match: empty items and nextCursor null**: OK 
- **m_badcursor_nosearch: same status and body as before**: OK (400 {"success": false, "data": null, "error": "Invalid cursor format.", "statusCode": 400})
- **m_badcursor_search: same status and body as before**: OK (400 {"success": false, "data": null, "error": "Invalid cursor format.", "statusCode": 400})
- **m_notjson_cursor_search: same status and body as before**: OK (400 {"success": false, "data": null, "error": "Invalid cursor.", "statusCode": 400})
- **old-format (name,id) cursor with a search: 400 like a malformed cursor**: OK (400 {"success": false, "data": null, "error": "Invalid cursor.", "statusCode": 400})
- **wildcard characters are literal: search='%%'**: OK (200, 0 items)
- **wildcard characters are literal: search='de_'**: OK (200, 0 items)
- **wildcard characters are literal: search='\\\\'**: OK (200, 0 items)

## S7 generated SQL and queries per request (member, search=dév, limit=50)
- status 200, EF queries for this request: **1**
- coach list search=dév: status 200, EF queries: **2** (split query: main query + objectives collection)
- member list without search (unchanged path): EF queries: **1**
- **ranking happens in SQL (CASE ... ORDER BY, translate, LIMIT)**: OK 

## S8 coach endpoint (GET /api/v1/coach/exercises, no pagination: S3 not applicable)
- **S1 on coach: list equals the independent SQL ranking**: OK (22 results; baseline had 20, name only)
- **every name starting with Dév before any other result**: OK 
- **dev and DÉV give the same list as dév**: OK 
- **items also returned before are byte-identical (details/Includes intact)**: OK 
- **S4 coach without search identical to the baseline**: OK (275 items)
- **coach blank search identical to the baseline**: OK 
- **coach no match: empty list**: OK 
- **coach dév + equipmentId equals SQL**: OK 
- coach order: Développé alterné haltères | Développé couché barre | Développé couché haltères | Développé couché prise serrée | Développé couché prise serrée triceps | Développé décliné barre | Développé décliné haltères | Développé épaules barre | Développé épaules haltères assis | Développé épaules haltères debout | Développé épaules unilatéral haltère | Développé haltères prise neutre | Développé incliné barre | Développé incliné haltères | Développé militaire barre assis | Développé militaire barre debout | Développé unilatéral haltère | Smith machine développé couché | Smith machine développé épaules | Smith machine développé incliné | Chest press machine | Shoulder press machine

## Result: ALL CHECKS OK
