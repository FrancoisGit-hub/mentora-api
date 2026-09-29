# Lot 9.1 — Read-only inventory

Facts only, gathered by reading the repository at HEAD `a6ef161` on branch `feature/lot_9`. No proposals.

## 2.1 Exercises

**Entity**: `Mentora.Core/Entities/Exercise.cs` → table `EXERCISES` (`Mentora.Infrastructure/Persistence/Configurations/ExerciseConfiguration.cs`).

Columns:

| Column | Type | Nullable |
|---|---|---|
| `EXERCISE_ID` | uuid, PK, `gen_random_uuid()` | no |
| `EXERCISE_COACH_ID` | uuid | **yes** — NULL means "Mentora catalogue entry" (no coach owns the row) |
| `EXERCISE_NAME` | varchar(120) | no |
| `EXERCISE_DESCRIPTION` | text | yes |
| `EXERCISE_INSTRUCTIONS` | text | yes |
| `EXERCISE_VIDEO_URL` | varchar(500) | yes |
| `EXERCISE_IMAGE_URL` | varchar(500) | yes |
| `EXERCISE_MUSCLE_GROUP` | varchar(40), enum | no |
| `EXERCISE_EQUIPMENT` | varchar(40), enum | no |
| `EXERCISE_IS_POLYARTICULAR` | bool, default false | no |
| `EXERCISE_IS_ACTIVE` | bool, default true | no |
| `EXERCISE_CREATED_DATE` | timestamp | no |
| `EXERCISE_UPDATED_DATE` | timestamp | no |

Ownership model: `ExerciseCoachId` (Guid?) FK → Coach, `OnDelete(Restrict)`. NULL = shared Mentora catalogue entry visible to everyone; non-null = private to that coach. Unique index `(EXERCISE_COACH_ID, EXERCISE_NAME)` with `NULLS NOT DISTINCT` created via raw SQL in the migration (not expressible via `HasIndex().IsUnique()`). Secondary index `IX_EXERCISES_COACH_ACTIVE` on `(EXERCISE_COACH_ID, EXERCISE_IS_ACTIVE)`.

Enums used:
- `MuscleGroup` (`Mentora.Core/Enums/MuscleGroup.cs`) — DB values: `CHEST, BACK, SHOULDERS, BICEPS, TRICEPS, FOREARMS, ABS, QUADRICEPS, HAMSTRINGS, GLUTES, CALVES, FULL_BODY, CARDIO`.
- `Equipment` (`Mentora.Core/Enums/Equipment.cs`) — DB values: `BODYWEIGHT, BARBELL, DUMBBELL, KETTLEBELL, MACHINE, CABLE, ELASTIC, OTHER`.

Seeder: `Mentora.Infrastructure/Seeding/ExerciseSeeder.cs`, invoked from `Program.cs` at startup (`ExerciseSeeder.SeedAsync`). Seeds **45** default exercises (`ExerciseCoachId = null`), idempotent by name among coach-null rows. Names (in seed order): Développé couché barre, Développé couché haltères, Écarté couché haltères, Pompes, Développé incliné haltères, Soulevé de terre, Traction / tirage vertical, Rowing barre buste penché, Rowing haltère unilatéral, Tirage horizontal poulie basse, Développé épaules barre, Élévations latérales haltères, Élévations frontales haltères, Oiseau (élévations arrière), Curl biceps barre, Curl biceps haltères alterné, Curl marteau haltères, Dips, Extension triceps poulie haute, Barre au front, Curl poignet barre, Extension poignet haltère, Suspension à la barre, Crunch, Gainage planche, Relevé de jambes suspendu, Rotation russe, Squat, Fentes avant, Presse à cuisses, Leg extension, Leg curl allongé, Soulevé de terre jambes tendues, Good morning barre, Hip thrust barre, Fentes bulgares, Kickback à la poulie, Mollets debout, Mollets assis, Burpees, Kettlebell swing, Thruster haltères, Corde à sauter, Course à pied (tapis), Rameur.

Controllers:
- `Mentora.API/Controllers/Coach/CoachExercisesController.cs` — route `api/v1/coach/exercises`, policy `CoachOnly`, Swagger group `coach`, tag `"Coach — Exercises"`. `GET /` (list, filters: search/muscleGroup/equipment/scope/includeInactive), `GET /{exerciseId:guid}`, `POST /` (201, Location `/api/v1/coach/exercises/{id}`), `PUT /{exerciseId:guid}`, `DELETE /{exerciseId:guid}` (soft-delete → 204).
- `Mentora.API/Controllers/Member/MemberExercisesController.cs` — route `api/v1/member/exercises`, policy `MemberOnly`, Swagger group `mobile`, tag `"Member — Exercises"`. `GET /` (list), `GET /{exerciseId:guid}`. Read-only.

Service: `Mentora.Infrastructure/Services/ExerciseService.cs` implementing `Mentora.Core/Interfaces/IExerciseService.cs`.

DTOs (`Mentora.Core/DTOs/Exercise/`):
- `ExerciseRequest(Name, Description?, Instructions?, VideoUrl?, ImageUrl?, MuscleGroup, Equipment, IsPolyarticular)` — used for both create and full replace.
- `ExerciseResponse(ExerciseId, CoachId?, Name, Description?, Instructions?, VideoUrl?, ImageUrl?, MuscleGroup, Equipment, IsPolyarticular, IsActive, CreatedDate, UpdatedDate)`.

Validator: `Mentora.Core/Validators/Exercise/ExerciseRequestValidator.cs`.

## 2.2 Programs

Two distinct controllers collide on the word "programs" but not on route (already fixed pre-Lot 9):

- **`OfferProgramsController`** (`Mentora.API/Controllers/Coach/OfferProgramsController.cs`) — route `api/v1/coach/programs` (this is the route Lot 9.1 renames). Policy `CoachOnly`, Swagger group `coach`, tag `"Coach — Programs"`. Entity: `OfferProgram` (`Mentora.Core/Entities/OfferProgram.cs`) → table `OFFER_PROGRAMS` (per `CoachParameterSeeder`/`OfferProgramSeeder` mentioned in CLAUDE.md). `OfferProgram` has `OfferProgramId, OfferProgramName, OfferProgramIsActive, OfferProgramCreatedDate, CoachId, Products` (1:N to `Product`). Endpoints: `GET /` (list), `POST /` (create, returns `StatusCode(201, …)` — **no** `CreatedAtAction`/Location header), `PUT /{id:guid}`, `DELETE /{id:guid}` (returns 200 with `data: null`, not 204). Service: `IOfferProgramService`/`OfferProgramService`. Seeded by `OfferProgramSeeder` (4 default programs per coach with zero active `OFFER_PROGRAMS` rows, per CLAUDE.md).

- **`CoachProgramsController`** (`Mentora.API/Controllers/Coach/CoachProgramsController.cs`) — route base `api/v1/coach`, sub-routes `members/{memberId:guid}/training-programs` and `training-programs/{programId:guid}` (i.e. served at `api/v1/coach/members/{memberId}/training-programs` and `api/v1/coach/training-programs/{programId}`). Policy `CoachOnly`, Swagger group `coach`, tag `"Coach — Training programs"`. The controller's own XML doc comment states it was deliberately routed under "training-programs" instead of "programs" specifically to avoid colliding with `OfferProgramsController`'s `api/v1/coach/programs` (Lot 6.3 route-collision fix, already resolved before Lot 9). Entity: `Program` (`Mentora.Core/Entities/Program.cs`) → table (via `ProgramConfiguration`, not re-read in full — entity confirmed) with `ProgramId, ProgramCoachId, ProgramMemberId, ProgramTemplateId? (weak ref, no FK), ProgramName, ProgramGoal, ProgramStartDate, ProgramDurationWeeks, ProgramWeekOffset (unused until Lot 6.5), ProgramStatus, ProgramCreatedDate, ProgramUpdatedDate`. Endpoints: `GET members/{memberId}/training-programs` (list headers), `GET training-programs/{programId}` (full tree), `POST members/{memberId}/training-programs` (assign — 201, Location `/api/v1/coach/training-programs/{id}`), `PUT training-programs/{programId}` (full replace), `DELETE training-programs/{programId}` (archive → 204). Service: `IProgramService`/`ProgramService`.

Template ("trame") entity: `ProgramTemplate` (`Mentora.Core/Entities/ProgramTemplate.cs`) → table `PROGRAM_TEMPLATES` (via `Mentora.API/Controllers/Coach/CoachProgramTemplatesController.cs`, route `api/v1/coach/program-templates`, tag `"Coach — Program templates"`, policy `CoachOnly`). Fields: `ProgramTemplateId, ProgramTemplateCoachId? (NULL = "Mentora template"), ProgramTemplateName, ProgramTemplateDescription?, ProgramTemplateGoal (ProgramGoal enum), ProgramTemplateDurationWeeks, ProgramTemplateBody (string — jsonb, recursive block tree, cannot use EF owned types because they can't be recursive; serialized/deserialized by the service via System.Text.Json), ProgramTemplateIsActive, ProgramTemplateCreatedDate, ProgramTemplateUpdatedDate`.

**A template can exist without a coach**: `ProgramTemplateCoachId` is nullable Guid; NULL = "Mentora template" (shared), confirmed both in the entity comment and in `ProgramService.AssignAsync`'s template lookup (`t.ProgramTemplateCoachId == null || t.ProgramTemplateCoachId == coachId`).

**Deep-copy mechanism** (`Mentora.Infrastructure/Services/ProgramService.cs`, `AssignAsync`): when `request.TemplateId` is supplied, the template's `ProgramTemplateBody` JSON is deserialized into a `ProgramTemplateBody` DTO tree (`Mentora.Core/DTOs/ProgramTemplate/Body/`). A new `Program` row is created, then `CopyBlockTree(...)` walks the deserialized block tree and materializes new `ProgramBlock`, `ProgramSession`, `ProgramCircuit`, `ProgramExercise` rows with brand-new GUIDs, all inside one `IsolationLevel.Serializable` transaction, added via `AddRange` and saved in a single `SaveChangesAsync` (no per-row round trips). Only `ExerciseId` pointers are copied into `ProgramExercise.ProgramExerciseExerciseId` — never the exercise's own content — so later edits/soft-deletes to the source template or to an `Exercise` row have zero effect on an already-assigned program. If the member already has an `Active` program, it is archived (`ProgramStatus = Archived`) in the same transaction before the new one is inserted; the archived program's id is returned in the response.

## 2.3 Prescription

Entity: `ProgramExercise` (`Mentora.Core/Entities/ProgramExercise.cs`) → table `PROGRAM_EXERCISES`.

Prescribed / performed columns present on the entity:
- `ProgramExercisePrescribedSets` (int?)
- `ProgramExercisePrescribedReps` (int?)
- `ProgramExercisePrescribedWeightKg` (decimal? numeric(6,2))
- `ProgramExerciseRestSeconds` (int?)
- `ProgramExerciseWorkSeconds` (int?)
- `ProgramExerciseRestWorkSeconds` (int?)
- `ProgramExerciseActualSets` (int?) — unwritten until Lot 6.6 per code comment
- `ProgramExerciseActualReps` (int?)
- `ProgramExerciseActualWeightKg` (decimal? numeric(6,2))
- `ProgramExerciseActualRpe` (int?)
- `ProgramExerciseMemberFeedback` (string?, text)

Also present but not "prescription": `ProgramExerciseLoadType` (string, `KG | BODYWEIGHT | ELASTIC`), `ProgramExerciseCustomNote` (string?, text), `ProgramExercisePosition` (int).

Product-list check (present / absent on `ProgramExercise`):

| Item | Present? | Column |
|---|---|---|
| sets | present | `ProgramExercisePrescribedSets` / `ProgramExerciseActualSets` |
| reps | present | `ProgramExercisePrescribedReps` / `ProgramExerciseActualReps` |
| rep range | absent | — |
| load | present | `ProgramExercisePrescribedWeightKg` / `ProgramExerciseActualWeightKg` (kg only) |
| %1RM | absent | — |
| RPE | present (actual only) | `ProgramExerciseActualRpe` — no prescribed-RPE column |
| RIR | absent | — |
| rest | present | `ProgramExerciseRestSeconds` (prescribed only, no actual-rest column) |
| tempo | absent | — |
| work duration | present | `ProgramExerciseWorkSeconds` (prescribed only) |
| distance | absent | — |
| calories | absent | — |
| rounds | absent | — |
| %VMA | absent | — |
| speed | absent | — |
| target HR / %HRmax | absent | — |
| power | absent | — |
| cadence | absent | — |
| incline | absent | — |
| side | absent | — |
| coach note | present | `ProgramExerciseCustomNote` (coach-authored, prescribed side) — `ProgramExerciseMemberFeedback` is the member-authored counterpart on the actual side |

Note: `ProgramExerciseRestWorkSeconds` exists in addition to `RestSeconds`/`WorkSeconds`; its exact semantics (e.g. interval-training rest-between-work-bouts) were not further documented in the entity/configuration and are not covered by the product-list terms above.

DTOs: `ProgramExerciseResponse` (`Mentora.Core/DTOs/Program/`), `ProgramExerciseActualRequest` (`Mentora.Core/DTOs/Program/ProgramExerciseActualRequest.cs` — carries `ActualSets, ActualReps, ActualWeightKg, ActualRpe, MemberFeedback` for the Lot 6.6 completion flow), `LastPerformedResponse`.

## 2.4 Purchases and vouchers

**Purchase entity**: `Order` (`Mentora.Core/Entities/Order.cs`) → table `ORDERS` (`OrderConfiguration.cs`). Column prefix: `ORDER_`.

Columns: `ORDER_ID` (PK uuid), `ORDER_MEMBER_ID` (uuid, required, FK→Member Restrict), `ORDER_COACH_ID` (uuid, required, FK→Coach Restrict), `ORDER_STATUS` (varchar(20), enum, default `'PENDING'`), `ORDER_TOTAL_EUROS` (numeric(10,2)), `ORDER_STRIPE_SESSION_ID` (varchar(255), nullable, unique partial index where not null), `ORDER_STRIPE_CHECKOUT_URL` (text, nullable), `ORDER_CREATED_DATE`, `ORDER_UPDATED_DATE`, `ORDER_PAID_AT` (nullable). No origin/source column exists yet.

`OrderStatus` enum (`Mentora.Core/Enums/OrderStatus.cs`): `Pending, Paid, Expired, Failed` → DB `PENDING, PAID, EXPIRED, FAILED`. There is no `Completed` state — `Paid` is the terminal "purchase succeeded" status.

**Line-item entity**: `OrderItem` (`Mentora.Core/Entities/OrderItem.cs`) → table `ORDER_ITEMS`, prefix `ORDER_ITEM_`. Weak reference `ProductId` (no FK — snapshot semantics), `Quantity` (int, required), snapshot fields `ProductName, UnitPriceEuros, OrderItemOriginalUnitPriceEuros, OrderItemPackDiscountPercentApplied?, LineTotalEuros, OfferType, DurationMinutes, Sport`.

**`SESSION_VOUCHERS` entity**: `SessionVoucher` (`Mentora.Core/Entities/SessionVoucher.cs`), prefix `VOUCHER_`. Columns: `VOUCHER_ID` (PK), `VOUCHER_ORDER_ITEM_ID` (FK→OrderItem, Restrict), `VOUCHER_MEMBER_ID` (FK→Member, Restrict), `VOUCHER_COACH_ID` (FK→Coach, Restrict), `VOUCHER_PRODUCT_ID` (weak ref, no FK), `VOUCHER_OFFER_TYPE`, `VOUCHER_DURATION_MINUTES`, `VOUCHER_SPORT`, `VOUCHER_STATUS` (default `'AVAILABLE'`), `VOUCHER_RESERVED_SESSION_ID` (nullable FK→Session, `SetNull` on delete, added retroactively in Lot 2.4.1), `VOUCHER_CREATED_DATE`, `VOUCHER_UPDATED_DATE`. Index `IX_SESSION_VOUCHERS_MEMBER_STATUS` on `(MemberId, Status)`.

`VoucherStatus` enum: `Available, Reserved, Consumed` → DB `AVAILABLE, RESERVED, CONSUMED`.

**Voucher-creation code**: `Mentora.Infrastructure/Services/StripeWebhookHandler.cs`, method `HandleCheckoutCompletedAsync` (private, lines ~93–166). Triggered when the webhook receives a `checkout.session.completed` event for a known `StripeSessionId` on a `Pending` order. It transitions the order to `Paid`, sets `PaidAt`, then loops `foreach (var item in order.Items) { for (n = 0; n < item.Quantity; n++) { db.SessionVouchers.Add(new SessionVoucher { … }) } }` — **one voucher per unit of `OrderItem.Quantity`, for every item, unconditionally** (no offer-type filtering: every `OrderItem` currently produces vouchers regardless of `OfferType`). This logic body itself is plain EF Core (no Stripe SDK calls, no HTTP calls to any Stripe-like service) — it only requires an `Order` with `Items` already persisted and can run standalone. It is currently **inline** inside `StripeWebhookHandler`, not extracted into a shared/reusable method.

**What `Quantity` means**: `OrderItem.Quantity` is the number of vouchers (session credits) that purchase line will generate — confirmed by the 1:1 `for (n = 0; n < item.Quantity; n++)` loop that creates exactly `Quantity` `SessionVoucher` rows per item. For a plain product line, cart `Quantity` maps straight to `OrderItem.Quantity`. For a pack line (`CartService.CheckoutAsync`), `OrderItem.Quantity = cartItem.Quantity (packs bought) × packItem.ProductPackItemQuantity (units per pack)` — i.e. still "number of vouchers to generate" after pack expansion.

**Which product types generate vouchers**: currently **all** of them — every `Product` has an `OfferType` (`Visio | PresentielSolo | PresentielGroupe | VisioGroupe`, the last rejected at validation) and every `OrderItem`/pack-expanded line unconditionally produces `Quantity` vouchers in `HandleCheckoutCompletedAsync`. There is no product type, status, or flag in the current code that skips voucher generation for an otherwise-purchasable product.

**Status while Stripe is stubbed**: checkout (`CartService.CheckoutAsync`) always creates the `Order` as `Pending` and calls `IStripeCheckoutService.CreateSessionAsync` (implementation: `StripeCheckoutStub`, `Mentora.Infrastructure/Services/Stripe/StripeCheckoutStub.cs`) which fabricates a fake session id/URL with no real Stripe call. The order only becomes `Paid` when `StripeWebhookHandler.HandleCheckoutCompletedAsync` later runs for a `checkout.session.completed` event with a matching `StripeSessionId` — reached in this stub setup via `Mentora.API/Controllers/Internal/InternalStripeWebhookController.cs` (route `api/v1/internal/stripe`, Swagger group `internal`) hitting the stubbed fake-checkout flow. So "Paid" is the completed-purchase status, but it is not set synchronously at checkout — it requires the (stubbed) webhook step to fire.

**Origin/source column**: none exists yet on `Order` or anywhere else. Lot 9.1 §4 adds it.

## 2.5 Routes

All routes whose template contains "programs", "exercises", "vouchers" or "grants":

| Route | Controller | Policy |
|---|---|---|
| `api/v1/coach/exercises` | `CoachExercisesController` | `CoachOnly` |
| `api/v1/member/exercises` | `MemberExercisesController` | `MemberOnly` |
| `api/v1/coach/programs` | `OfferProgramsController` | `CoachOnly` |
| `api/v1/coach/members/{memberId}/training-programs`, `api/v1/coach/training-programs/{programId}` | `CoachProgramsController` (`[Route("api/v1/coach")]` + per-action route) | `CoachOnly` |
| `api/v1/coach/program-templates` | `CoachProgramTemplatesController` | `CoachOnly` |
| `api/v1/coach/program-sessions` | `CoachProgramSessionsController` | `CoachOnly` |
| `api/v1/member/programs` | `MemberProgramsController` | `MemberOnly` |
| `api/v1/member/program-sessions` | `MemberProgramSessionsController` | `MemberOnly` |
| `api/v1/member/vouchers` | `MemberVouchersController` | `MemberOnly` |

No "grants" route exists yet. **No collision found**: `api/v1/coach/programs` (OfferProgramsController) and `api/v1/coach/…/training-programs` (CoachProgramsController) are already disjoint route templates (the latter was deliberately renamed away from "programs" in Lot 6.3, per the controller's own doc comment) — `program-templates` and `program-sessions` are also disjoint prefixes. Renaming `api/v1/coach/programs` → `api/v1/coach/product-programs` (§3) introduces no new collision against `training-programs`, `program-templates`, or `program-sessions` (all distinct literal segments).

## 2.6 Conventions

**Pagination**: cursor-based, seen on `GET api/v1/member/orders` (`MemberOrdersController` + `MemberOrderService.ListAsync`). Query params: `status?` (optional wire-enum filter), `limit` (default 20, validated range [1,100], `InvalidOperationException` → 400 outside range), `cursor?` (opaque). Response shape: `{ Orders: [...], NextCursor: string? }` (record `OrderListResponseDto`). Cursor implementation: base64 of a JSON `{ CreatedDate, OrderId }` payload; query fetches `limit + 1` rows ordered by `CreatedDate DESC, OrderId DESC` to detect `hasNext` without a separate COUNT.

**Swagger docs / tags / groups**: Three Swagger documents declared in `Program.cs` — `mobile` ("Mentora API — Mobile", member-facing), `coach` ("Mentora API — Coach Back-Office"), `internal` ("Mentora API — Internal"). Assignment is per-controller via `[ApiExplorerSettings(GroupName = "coach"|"mobile"|"internal")]`; endpoints with no explicit `GroupName` default to `internal` (`c.DocInclusionPredicate`, safe-least-exposed default). Tags are assigned via `[Tags("...")]` on the controller (read by `c.TagActionsBy` in `Program.cs`, falling back to the controller class name if no `[Tags]` present). Existing coach-side tag strings follow the pattern `"Coach — <Feature>"` (em dash, capitalized feature name), e.g. `"Coach — Exercises"`, `"Coach — Programs"`, `"Coach — Training programs"`, `"Coach — Program templates"`. Member-side tags follow `"Member — <Feature>"`, e.g. `"Member — Exercises"`, `"Member — Orders"`.

**Custom operationIds**: none configured. `Program.cs`'s `AddSwaggerGen` block does not set `CustomOperationIds`; only `CustomSchemaIds(t => t.FullName)` is configured (schema, not operation, ids). One custom `IOperationFilter` exists as a style reference: `Mentora.API/Swagger/VoucherStatusOperationFilter.cs`, which matches on `context.MethodInfo.DeclaringType`/`Name` and mutates `operation.Parameters`/`operation.Schema` — the pattern to follow for the Lot 9.1 §3.2 deprecation filter (matching instead on `context.ApiDescription.RelativePath` for the route-prefix check).

## Stop condition — verified

a) **True.** Voucher-creation logic (`StripeWebhookHandler.HandleCheckoutCompletedAsync`, lines ~136–157) is plain EF Core: it reads an already-persisted `Order`+`Items` and inserts `SessionVoucher` rows. It contains no Stripe SDK call and no outbound HTTP call — it can run standalone given an `Order` in `Pending` status with `Items`, independent of the (stubbed) Stripe checkout/webhook plumbing around it.

b) **True.** `Product.CoachId` (`Mentora.Core/Entities/Product.cs`) is a required `Guid` FK to `Coach` — every product carries its owning coach.

c) **True.** `MemberCoach` (`Mentora.Core/Entities/MemberCoach.cs` → `MEMBER_COACHES`) has no end/inactive flag; a row's mere existence for `(MemberId, CoachId)` is "currently attached" (confirmed by its use as the sole attachment check in `ProgramService.AssignAsync`, `CartService.EnsureMemberCoachLink`, etc.).

All three hold — continuing to §3.
