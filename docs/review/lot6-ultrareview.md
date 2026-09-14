# Lot 6 Ultrareview

**Commit range:** `main` (c9d9618) → `feature/lot_6` HEAD (6be3cf6), 7 commits:
`209ed59` (6.1), `59db6f3` (6.2), `7d82a7a` (6.3), `46572b8` (6.4), `00890af` (6.5),
`e53869c` (6.6), `6be3cf6` (route-collision fix).

**Files reviewed:** all 122 files in `git diff main..HEAD --stat` (18,716 insertions / 92
deletions), read in full — every new controller, service, DTO, validator, entity,
configuration, migration (`Up`/`Down`), the two seeders, and every pre-existing file the
stat touched (`Program.cs`, `CoachMeService`, `ProductService`, `SessionSlotService`,
`SessionConfiguration`, `EnumMappings`, etc.). `dotnet build` was run to confirm the branch
compiles (0 errors, only pre-existing/unrelated XML-doc warnings). No SQL was run against the
local database — schema conclusions below come from reading the migration files, which is
sufficient to reason about `Up`/`Down` correctness and constraint audits without touching data.

Read-only throughout: no file was modified, no migration was run, nothing was written to any
database, the API was not started.

---

## 1. Findings

### F1 — MAJOR — Axis 4.4 (Migrations)
**`Mentora.Infrastructure/Persistence/Migrations/20260812095011_Lot6_4_GroupSessions.cs:161-189`**

`Down()` reverts `SESSION_MEMBER_ID`, `SESSION_VOUCHER_ID` and `SESSION_PRODUCT_ID` from
nullable back to `NOT NULL` using `AlterColumn<Guid>(..., nullable: false, defaultValue: new
Guid("00000000-0000-0000-0000-000000000000"), ...)`. EF's `AlterColumn` `defaultValue`
parameter only sets the column's `DEFAULT` clause for future inserts — it does not backfill
existing rows. Any group session created under Lot 6.4 (which by design has
`SESSION_MEMBER_ID`/`SESSION_VOUCHER_ID` = `NULL`) will still have `NULL` in that column when
`Down()` runs, and Postgres's `ALTER COLUMN ... SET NOT NULL` fails outright on a column that
currently contains `NULL`.

**Why it matters at runtime:** the moment a single group session has ever been created,
`dotnet ef database update <previous>` (or any rollback of this migration) throws a Postgres
error (`column "SESSION_MEMBER_ID" of relation "SESSIONS" contains null values`) and the
rollback aborts mid-transaction. `Down` does not correctly reverse `Up` in the presence of the
very data this lot introduces.

**Minimal fix:** before each `AlterColumn` in `Down()`, add a
`migrationBuilder.Sql("UPDATE \"SESSIONS\" SET \"SESSION_MEMBER_ID\" = '00000000-...' WHERE \"SESSION_MEMBER_ID\" IS NULL;")`
(and the same for voucher/product) — or, more honestly, document that this migration is not
safely reversible once group sessions exist and drop the offending group session rows first.

**Verification:**
```sql
-- after creating at least one group session, then attempting the rollback:
ALTER TABLE "SESSIONS" ALTER COLUMN "SESSION_MEMBER_ID" SET NOT NULL;
-- ERROR:  column "SESSION_MEMBER_ID" of relation "SESSIONS" contains null values
```

---

### F2 — MINOR — Axis 4.3 (EF Core / generated SQL)
**`Mentora.Infrastructure/Services/ProgramService.cs:741-784`** (`ResolveLastPerformedAsync`)

The "last performed" lookup defines `occurrences` as a `from...join...select` query over
`ProgramExercises ⋈ ProgramCircuits ⋈ ProgramSessions`, then uses that same query object both
as the outer projection source and, per outer row, as the source of a correlated inner
subquery (`occurrences.Where(y => ...).OrderByDescending(...).Select(...).FirstOrDefault()`).
This is exactly the shape EF Core needs to emit a Postgres `LATERAL`/correlated-subquery join,
and it generally works on recent EF Core + Npgsql — but it is also a pattern that has
historically been fragile (silently falling back to client evaluation, or throwing
"could not be translated" for some anonymous-type combinations) depending on the exact EF Core
minor version pinned in this project. This method runs on **every** program-tree read
(`BuildResponseAsync`) and every single-session completion response, so a translation
regression would 500 the whole program feature, not degrade gracefully.

**Why it matters at runtime:** if the query does not translate as expected, either (a) an
`InvalidOperationException` bubbles up as a 500 on every `GET .../training-programs/{id}` and
every completion write, or (b) it silently degrades to N+1 (one round-trip per exercise) under
load, which won't show up in a quick manual test but will show up in the first program with a
non-trivial number of exercises across weeks.

**Minimal fix:** none needed if verified — this is a "confirm before merge" item, not a known
bug. If verification below fails, materialize `occurrences` once via a `GroupBy` +
`OrderByDescending().Take(1)` per exercise instead of self-referencing the query object twice.

**Verification:** capture the generated SQL (`query.ToQueryString()` in a throwaway unit test,
or enable `Microsoft.EntityFrameworkCore.Database.Command` logging) for
`GET /api/v1/coach/training-programs/{id}` on a program with ≥2 weeks and ≥1 completed session,
and confirm it is a single `SELECT` with a correlated subquery/`LATERAL`, not N queries.

---

### F3 — MINOR — Axis 4.4 (Migrations)
**`Mentora.Infrastructure/Persistence/Migrations/20260812095011_Lot6_4_GroupSessions.cs:114-126`**

`CK_SESSIONS_GROUP_HAS_NO_MEMBER` is added via `ALTER TABLE "SESSIONS" ADD CONSTRAINT ...` —
`SESSIONS` is a pre-existing table (created in an earlier lot), not one created by this
migration. Per the review brief, any CHECK added to an existing table needs an explicit
pre-migration data-audit callout, since Postgres validates the constraint against every
existing row at `ADD CONSTRAINT` time and a demo/production database could contain rows that
violate it.

Concretely: the constraint requires that every row have either (a) a group offer type with
`MEMBER_ID`/`VOUCHER_ID` both null, or (b) a non-group offer type with both non-null. Every
row that existed before Lot 6.4 was created when `PRESENTIEL_GROUPE`/`VISIO_GROUPE` group
offer types did not exist and `SESSION_MEMBER_ID`/`SESSION_VOUCHER_ID` were `NOT NULL`
columns, so branch (b) holds for all of them by construction. Analysis says this is safe, but
it was not observed to have been verified against the actual demo database before merge, which
the review brief calls for explicitly.

**Why it matters at runtime:** if any historical row exists with an unexpected combination
(e.g. a hand-patched row, or a state left over from a bug in an earlier lot), the migration's
`Up()` fails outright at deploy time with a constraint-violation error, blocking every
subsequent deploy until fixed by hand.

**Minimal fix:** none — this is a pre-deploy verification step, not a code change.

**Verification (run on the demo DB before applying this migration):**
```sql
SELECT COUNT(*) FROM "SESSIONS" WHERE NOT (
  ("SESSION_OFFER_TYPE" IN ('PRESENTIEL_GROUPE','VISIO_GROUPE')
     AND "SESSION_MEMBER_ID" IS NULL AND "SESSION_VOUCHER_ID" IS NULL)
  OR
  ("SESSION_OFFER_TYPE" NOT IN ('PRESENTIEL_GROUPE','VISIO_GROUPE')
     AND "SESSION_MEMBER_ID" IS NOT NULL AND "SESSION_VOUCHER_ID" IS NOT NULL)
);
-- must return 0 before Up() is applied
```

---

### F4 — MINOR — Axis 4.8 (API contract)
**`Mentora.Core/DTOs/Catalog/ProductRequest.cs:6-10`**

The XML doc on `OfferType` states: *"Accepted values: `VISIO` ... or `PRESENTIEL_SOLO` ...
`PRESENTIEL_GROUPE` is reserved for V2 and is currently rejected by validation."* This is
false as of this lot: `ProductRequestValidator` (`Mentora.Core/Validators/Catalog/
ProductRequestValidator.cs:15-20`) only rejects `VISIO_GROUPE`; `PRESENTIEL_GROUPE` passes
validation and in fact **must** be accepted, since `CoachGroupSessionsController
.CreateGroupSession` → `SessionService.CreateGroupSessionAsync` requires the product's
`ProductOfferType` to be a group type (`GroupOfferTypes.Contains(product.ProductOfferType)`) —
a coach cannot create a group-session product at all if this doc comment were true.
`SessionSlotService.ParseOfferType` was updated in this same lot to accept
`PresentielGroupe` too, confirming the doc is simply stale, left over from before Lot 6.4.

**Why it matters at runtime:** this doc string ships into the generated Swagger schema
(`IncludeXmlComments` is wired in `Program.cs`). An API consumer (or the coach back-office
frontend team) reading `/swagger/coach` will be told a value is rejected when the server
actually accepts and requires it for group offers — a direct contract/documentation
contradiction visible to anyone building against the published spec.

**Minimal fix:** update the doc comment to say `PRESENTIEL_GROUPE` is accepted (required for
group-session products) and only `VISIO_GROUPE` is rejected in V1.

**Verification:** `POST /api/v1/coach/products` with `offerType: "PRESENTIEL_GROUPE"` →
201 Created (contradicts the doc's claim of rejection); `offerType: "VISIO_GROUPE"` → 422.

---

### F5 — MINOR — Axis 4.7 / 4.8 (Validation / API contract)
**`Mentora.Infrastructure/Services/ProgramService.cs:82-102`** (`AssignAsync`)

When `AssignProgramRequest.TemplateId` is absent (manual body assignment), `Name`, `Goal` and
`DurationWeeks` are validated with hand-written `if (...) throw new InvalidOperationException(...)`
checks, which `GlobalExceptionMiddleware` maps to **400** with a plain `error` string. Every
other request-shape validation failure in this same lot (and the rest of the API) goes through
FluentValidation and comes back as **422** with the `{ errors: { Field: [...] } }` shape
(see `UpdateProgramRequestValidator`, which validates the exact same three fields — `Name`,
`Goal`, `DurationWeeks` — for the sibling `PUT .../training-programs/{id}` endpoint, and does
return 422). Only the body tree itself (`ProgramTemplateBodyValidator`) is wired through
FluentValidation inside `AssignAsync`; the flat scalar fields are not.

**Why it matters at runtime:** a client that omits `name` on `POST
/coach/members/{id}/training-programs` gets a 400 with no `errors` dictionary, while the
identical mistake on `PUT /coach/training-programs/{id}` gets a 422 with a structured `errors`
dictionary. Any frontend error-handling code written against the documented 422 `errors`
contract (which is how every other validation failure behaves) will not surface a field-level
error for this one endpoint's manual-body path.

**Minimal fix:** move the `Name`/`Goal`/`DurationWeeks` checks in the manual-body branch of
`AssignAsync` into a small `AssignProgramRequestValidator` (FluentValidation), reusing the same
`RootContextData["CoachId"]`/`["DurationWeeks"]` pattern already used by the sibling
validators, so the error shape matches the rest of the API.

**Verification:** `POST /api/v1/coach/members/{memberId}/training-programs` with no
`templateId` and `name: null` → currently 400 `{ error: "Name is required..." }`; compare with
`PUT /api/v1/coach/training-programs/{programId}` with `name: ""` → 422
`{ errors: { Name: [...] } }`.

---

### F6 — INFO — Axis 4.9 (Leftovers)
**`Mentora.Infrastructure/Services/AgendaService.cs:283-286`**

The comment on `ComputeShiftWeeks` reads: *"Until Lot 6.6 ships session completion, nothing
can ever reach DONE, so every fully-elapsed week counts as empty... Expected, not a bug:
there's no way to mark a session DONE yet."* This was accurate when Lot 6.5 was written, but
Lot 6.6 (session completion) has since shipped in this same branch — `ProgramService
.ApplyCompletionAsync` now sets `ProgramSessionStatus.Done`. The comment is stale and, left
as-is, will mislead a future reader into thinking `weeksWithDone` is still always empty when
it is not.

**Why it matters:** purely a documentation-drift risk (no runtime effect) — a reviewer of a
future SHIFT-behavior bug could waste time trusting an assumption that's no longer true.

**Minimal fix:** delete or update the comment now that 6.6 has landed in the same branch.

**Verification:** n/a (comment-only).

---

## 2. Axes that yielded nothing

- **4.1 Multi-tenant scope/authorization:** every Lot 6 endpoint carrying a `memberId` in its
  route (`GET/POST /coach/members/{memberId}/training-programs`,
  `DELETE /coach/sessions/{sessionId}/participants/{memberId}`) verifies the `MEMBER_COACHES`
  link (or an equivalent ownership-derived check) and throws `NotFoundException` → 404, never
  `ForbiddenException`/403. Every other coach/member write folds ownership into the query
  filter itself. No endpoint reaches an entity without a `COACH_ID`/`MemberId` filter, including
  through `V_SESSION_MEMBERS` (see 4.6 below). `CoachOnly`/`MemberOnly` policies are correctly
  applied on every new controller (verified against `Program.cs`'s policy definitions). No
  findings.
- **4.2 Route collisions:** every `(HTTP method, route template)` pair added in Lot 6 was
  enumerated (`grep` across all 31 controllers, old and new) — no duplicates found beyond the
  one already fixed in `6be3cf6` (`CoachProgramsController` moved off `api/v1/coach/programs`,
  which collides with the pre-existing `OfferProgramsController`, onto
  `api/v1/coach/training-programs`). `CoachSessionsController` and `CoachGroupSessionsController`
  both declare `[Route("api/v1/coach/sessions")]` but their action templates never overlap
  (`""`, `{sessionId}`, `{sessionId}/cancel` vs. `group`, `{sessionId}/participants`,
  `{sessionId}/participants/{memberId}`) and use disjoint HTTP verbs where paths could
  theoretically collide — no ambiguity at runtime.
- **4.5 Seeders:** both `ExerciseSeeder` and `ProgramTemplateSeeder` match strictly by name
  against the existing Mentora rows (`ExerciseCoachId == null` / `ProgramTemplateCoachId ==
  null`) and only insert the missing subset — never a global "any rows exist → skip" guard. A
  new catalogue entry appended to `DefaultExercises`/`Templates` later would be seeded on the
  next startup. `CoachParameterService`'s pre-existing get-or-create pattern was extended
  cleanly to cover `CoachParameterMissedSessionBehavior` — no throw path.
- **4.6 Business rules:** prescribed/actual columns are never touched by the opposite write
  path; `MissedSessionBehavior.Shift` is computed only at read time in `AgendaService`
  (nothing persists a shifted date); group-session member-side booking is explicitly rejected
  in `SessionService.ReserveAsync`; capacity checks read `Session.SessionMaxParticipants` (the
  snapshot), never `Product.ProductMaxParticipants` live; `V_SESSION_MEMBERS` carries no
  `CoachId` column but every call site joins/filters it against an already coach- or
  member-scoped `Sessions`/caller-identity query, so no cross-tenant leak was found; the
  automatic booking↔program-session link (`LinkToProgramSessionAsync`) is a pure best-effort
  no-op when no active program or no matching candidate exists, and only ever runs once inside
  the same transaction as the booking it's attached to.
- **4.4 (remaining sub-points):** every `HasDefaultValue`/`HasDefaultValueSql` added in this
  lot (`Program.ProgramWeekOffset`, `Program.ProgramStatus`, `CoachParameter
  .CoachParameterMissedSessionBehavior`, `Exercise.ExerciseIsActive`/`IsPolyarticular`,
  `ProgramTemplate.ProgramTemplateIsActive`, `ProgramSession.ProgramSessionStatus`,
  `SessionParticipant.SessionParticipantStatus`) has a matching C# property initializer — the
  `CoachParameter` sentinel-value bug called out in the brief was not reproduced anywhere in
  Lot 6. `NULLS NOT DISTINCT` unique indexes (`EXERCISES`, `PROGRAM_TEMPLATES`) and partial
  unique indexes (`PROGRAMS` one-active-per-member, `PROGRAM_SESSIONS` booking-member,
  `SESSION_PARTICIPANTS` active-registration) are all raw SQL in the migration, matching the
  existing project convention, each with a corresponding `DROP INDEX`/`DROP TABLE` in `Down`.
- **4.6 (nullable FK / Restrict vs SetNull):** `EXERCISE_COACH_ID` and
  `PROGRAM_TEMPLATE_COACH_ID` — the two FKs whose `NULL` carries the "visible to all coaches"
  business meaning — are both `Restrict`. `PROGRAM_SESSION_SESSION_ID`'s `SetNull` is the one
  nullable FK that does *not* fit that pattern (its `NULL` just means "not yet booked", not
  "public"), so `SetNull` there is correct, not a violation.
- **4.7 (non-editable fields absent from request DTOs):** `AssignProgramRequest`,
  `UpdateProgramRequest`, `ProgramTemplateRequest`, `ExerciseRequest`,
  `CreateGroupSessionRequest`, `RegisterParticipantRequest`,
  `UpdateProgramSessionBookingRequest` and `UpdateProgramSessionCompletionRequest` were all
  read field-by-field — none exposes an id, `COACH_ID`, a snapshot field, or a status the
  caller shouldn't control. `ProgramTemplateBodyValidator`'s tree checks (depth ≤ 3, no cycles
  via strict level-descent, non-empty circuits, STANDARD vs INTERVAL field homogeneity) are
  wired through both `ProgramTemplateRequestValidator` and `UpdateProgramRequestValidator` via
  `SetValidator`, and actually enforced (not just declared) — confirmed by reading the
  recursive validation logic itself, not just its registration.

## 3. Summary table

| ID | Severity | Axis | Title |
|----|----------|------|-------|
| F1 | MAJOR | 4.4 | `Lot6_4_GroupSessions` migration `Down()` fails once any group session exists (NOT NULL revert has no backfill) |
| F2 | MINOR | 4.3 | `ResolveLastPerformedAsync`'s self-correlated subquery needs an EF-translation check before relying on it under load |
| F3 | MINOR | 4.4 | `CK_SESSIONS_GROUP_HAS_NO_MEMBER` added to the pre-existing `SESSIONS` table — needs a pre-deploy data audit (analysis says safe, not yet verified against the demo DB) |
| F4 | MINOR | 4.8 | `ProductRequest.OfferType` doc comment falsely claims `PRESENTIEL_GROUPE` is rejected; it's required for group-session products |
| F5 | MINOR | 4.7/4.8 | `AssignAsync`'s manual-body scalar fields (`Name`/`Goal`/`DurationWeeks`) 400 instead of 422, inconsistent with the identical fields on `UpdateProgramRequest` |
| F6 | INFO | 4.9 | Stale "until Lot 6.6 ships" comment in `AgendaService.cs` — 6.6 already shipped in this branch |

## 4. Proposed triage

- **Backlog:** F1 — doesn't block this merge (the forward `Up()` path is unaffected and this
  is a fast-forward merge, not a rollback), but must be fixed before anyone ever needs to roll
  back Lot 6.4 in an environment that has real group sessions.
- **Backlog:** F2 — no evidence of an actual failure; needs one `ToQueryString()`/SQL-logging
  check to close out, not a code change unless that check fails.
- **Fix before merge (cheap, zero-risk):** F3 — run the one-line audit query against the demo
  database before this migration is applied there; if it returns 0 (expected), no code change
  needed and this can be closed immediately.
- **Backlog:** F4 — doc-only fix, no behavior change, but worth doing before the coach
  back-office team builds against the currently-wrong Swagger description.
- **Backlog:** F5 — real but low-severity API inconsistency; only visible to a client that
  mis-fills the manual-body assignment form specifically.
- **Backlog:** F6 — comment-only, zero runtime impact.

No BLOCKER-severity findings: the branch compiles, no ambiguous routes remain, and every
multi-tenant/authorization check audited under axis 4.1 was present and correctly shaped
(404-not-403, ownership folded into query filters).
