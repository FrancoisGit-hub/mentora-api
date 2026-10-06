# Lot 11 wave 1 review fixes (S1, S2): runtime evidence (local)

Run start (UTC, `SELECT now()` read before the first write): **2026-10-06 07:58:35.064101+00** (`run-start.txt`). Local PostgreSQL 16 (port 5433), API on port 5243 with `ASPNETCORE_ENVIRONMENT=Development` and the Logging email provider taken from the shell. The raw API log stayed in the gitignored `api.log`; OTP codes only in a shell variable, tokens only in the gitignored `tokens.json`; none of them is recorded here. Fixtures: program `e5441b48-399a-409c-82f5-e3061ea730a4` (assigned from the "Remise en forme" template to member `0d5608a9-...`, coach A, 4 weeks, 4 microcycles, 12 sessions, 16 circuits, 36 exercises; coach A had no other program, so nothing was archived), renamed `l11fix-main` at the first writing PUT, and one session template `l11fix-template` (`6585da24-...`). Helper scripts: `put.js` (tree helper), `proofs.js` (the runs below).

## Build

`dotnet build --no-incremental`: 0 errors, 19 warnings (all CS1573, same count as before, none on a touched file), after the code change and again after the message change (`build.out`).

## Fixture: realised values (SQL, approved)

`fixture-actuals-dryrun.sql` (ROLLBACK, `fixture-actuals-dryrun.out`), then `fixture-actuals-commit.sql` (written with Write; diff against the dry run: last line only), `UPDATE 4` bounded by the program id and the 4 exercise ids. Values copied from the 11.2c fixture row (3 x 8, 22.5 kg, RPE 8).

| Exercise | Load type | Sets | Reps | kg | RPE | Role |
|---|---|---|---|---|---|---|
| X1 `78aca3c2` | KG | 3 | 8 | 22.50 | 8 | S2-a |
| X2 `7d6d6bd4` | KG | 3 | 8 | 22.50 | 8 | S2-b |
| X3 `cc674e8c` | KG | 3 | 8 | 22.50 | 8 | S2-c |
| X4 `38a272c1` | BODYWEIGHT | 3 | 8 | none | 8 | S2-f delete (last of its circuit, so positions stay contiguous and the 409 is reached, not the position 422) |

N1 `65402458` and N2 `7f6b6efb` (no realised values, same session) are used for S2-d and the reorder.

## S1-a: baseline on the unchanged code (before any edit)

`PUT /api/v1/coach/training-programs/{id}`, valid tree echoed with ids, one list replaced by `[null]` per request: **500 x 4** (`blocks`, `sessions`, `circuits`, `exercises`). `api.log` showed 4 `NullReferenceException`, raised while FluentValidation ran the rule on `x.Body.ToTemplateBody()` (`baseline-s1a.out`).

## Non-writing proofs on the fixed code (`proofs-nowrite.out`)

Fingerprint before and after: count and md5 of the row text plus `xmin` of `PROGRAMS` (coach A, created after run start), the 4 tree tables of the program, `SESSION_TEMPLATES` and `PROGRAM_TEMPLATES` of coach A.

```
BEFORE                                          AFTER (identical, compared by script: true)
PROGRAM_BLOCKS|4|2f6fddd4533e65836a9fbf367a576456
PROGRAM_CIRCUITS|16|ff046f3a82e73c7d250cc0a04b57ea48
PROGRAM_EXERCISES|36|1e9c35b635771d6ccbd3aa3f229844c1
PROGRAMS|1|443519079fcbe30667fea9a33b275177
PROGRAM_SESSIONS|12|72def20e08043fefc1f5794db16afbcd
PROGRAM_TEMPLATES|0|d41d8cd98f00b204e9800998ecf8427e
SESSION_TEMPLATES|0|d41d8cd98f00b204e9800998ecf8427e
```

| Proof | Request | Status | Keys / details |
|---|---|---|---|
| S1-b | PUT program `blocks:[null]` | 422 | `Body.Blocks`: "Entries must not be null."; also `Body`: the existing week-number failure (accepted side effect) |
| S1-b | PUT program `sessions:[null]` | 422 | `Body.Blocks[1].Sessions`: "Entries must not be null." |
| S1-b | PUT program `circuits:[null]` | 422 | `Body.Blocks[1].Sessions[1].Circuits`: "Entries must not be null." |
| S1-b | PUT program `exercises:[null]` | 422 | `Body.Blocks[1].Sessions[1].Circuits[1].Exercises`: "Entries must not be null." |
| S1-b | POST session-templates `circuits:[null]` | 422 | `Circuits`: "Entries must not be null." |
| S1-b | POST session-templates `exercises:[null]` | 422 | `Circuits[1].Exercises`: "Entries must not be null." (`blocks` and `sessions` do not exist on this body) |
| S1-c | POST program-templates `blocks:[null]` (shared validator) | 422 | `Body.Blocks`: "Entries must not be null."; also `Body` week-number failure |
| S2-a | PUT, X1 (actuals) keeps its id, `exerciseId` changed | 409 | `{"reason":"PROGRAM_HAS_HISTORY","programSessionIds":[],"programExerciseIds":["78aca3c2-f528-489f-9c1e-5203e2f5db77"]}` |
| S2-b | PUT, X2 (actuals) `loadType` KG to BODYWEIGHT | 409 | `{"reason":"PROGRAM_HAS_HISTORY","programSessionIds":[],"programExerciseIds":["7d6d6bd4-b2c3-4e26-8692-a78ceaeb58a8"]}` |
| S2-f | PUT omitting X4 (actuals), regression of 11.2c | 409 | `{"reason":"PROGRAM_HAS_HISTORY","programSessionIds":[],"programExerciseIds":["38a272c1-1e32-49db-8afc-74b44028d088"]}` |

S2-e: the fingerprints above are identical before and after the three 409s, the six 422 of S1-b, and the 422 of S1-c (nothing written, no session template or program template created).

## Writing proofs on the fixed code (`proofs-write.out`)

| Proof | Request | Status | Check |
|---|---|---|---|
| S1-d | PUT the current tree unchanged (X1 to X4 keep their actuals and their exercise and load type), name `l11fix-main` | 200 | no false positive from the swap check; program renamed (`l11fix-main`, ACTIVE) |
| S1-d | POST session-templates, valid body | 201 | row `6585da24-...` `l11fix-template` |
| S2-c | PUT, X3 same exercise, `prescribedReps` 15 to 16 | 200 | prescribed reps 16 in the DB; ACTUAL_* rows of X1 to X4 identical before and after (script compare: true) |
| S2-d | PUT, N1 (no realised values) `exerciseId` changed | 200 | N1 carries the new exercise id |
| S2-f | PUT, clean reorder (N1 and N2 positions swapped) | 200 | positions now N1 = 2, N2 = 1 |

Actuals before and after the whole write group: identical (X1 to X3 3 / 8 / 22.50 / 8, X4 3 / 8 / none / 8; member feedback empty).

## API log

`api.log` of this run: 0 `NullReferenceException`, 0 `fail:` or `crit:` line.

## Cleanup

`cleanup-dryrun.sql` (ROLLBACK, `cleanup-dryrun.out`) then `cleanup-commit.sql` (written with Write; `git diff --no-index --ignore-cr-at-eol`: last line only, `ROLLBACK;` to `COMMIT;`; output in `cleanup-commit.out`). One transaction, bounded by the fixture ids, the `l11fix-` names and `*_CREATED_DATE >= 2026-10-06 07:58:35.064101+00`. Counts (dry run and COMMIT identical):

| Table | Before | After | Deleted |
|---|---|---|---|
| PROGRAMS | 1 | 0 | 1 |
| PROGRAM_BLOCKS / PROGRAM_SESSIONS / PROGRAM_CIRCUITS / PROGRAM_EXERCISES | 4 / 12 / 16 / 36 | 0 / 0 / 0 / 0 | 4 / 12 / 16 / 36 (cascade) |
| SESSION_TEMPLATES | 1 | 0 | 1 |
| AUTH_OTPS (coach A user, the 2 logins of this run) | 2 | 0 | 2 |
| AUTH_REFRESH_TOKENS (coach A user, the 2 logins of this run) | 2 | 0 | 2 |

**COMMIT executed after François's explicit approval.**

### Leftover check (read only, `leftover-scan.sql`, `leftover-scan.out`)

31 tables with a `*_CREATED_DATE` column scanned for rows created on or after the run start: **0 rows in every table** (`AUTH_OTPS` and `AUTH_REFRESH_TOKENS` included, unlike 11.2c, since the logins of this run were part of the bounded cleanup). The four tree tables carry no created date; their rows for the fixture program were counted 0 after the COMMIT.

The API was stopped (owner of port 5243 killed, `netstat` shows the port free). The helper files that held a token, an OTP line or the assign response (`tokens.json`, `api.log`, `assign.json`) were deleted from disk at the end.
