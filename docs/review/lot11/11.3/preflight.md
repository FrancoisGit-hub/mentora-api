# Lot 11.3 preflight (read only)

HEAD c7dc884, branch feature/lot_11, working tree clean at start.

## 3.1 COACHES entity and jsonb pattern
- Entity `Mentora.Core/Entities/Coach.cs:3-25`: 7 columns (id, first name, last name, phone, created date, is active, user id). **No `COACH_UPDATED_DATE`**, so 4.5 "set updated date" does not apply.
- Configuration `Mentora.Infrastructure/Persistence/Configurations/CoachConfiguration.cs:9-29` (`ToTable("COACHES")`, `HasColumnName` per property).
- jsonb `List<string>` pattern to copy: `ProductConfiguration.cs:77-79` (`HasColumnName("PRODUCT_TAGS").HasColumnType("jsonb")`) with `Product.cs:16`. `EnableDynamicJson()` at `Mentora.API/Program.cs:130`. That column is nullable and has no default, so the NOT NULL + `HasDefaultValueSql("'[]'::jsonb")` part is new. Bool defaults follow the `CoachParameterConfiguration` pattern (`HasDefaultValue(false)` plus a C# initializer).
- Migrations folder: `Mentora.Infrastructure/Persistence/Migrations/` (last: `20261004181109_Lot11_2_ProgramTreeFields`).

## 3.2 Routes and controller
- No `/api/v1/coach/settings` route exists. The existing coach settings endpoints are `GET/PUT /api/v1/coach/parameters` (`CoachParametersController.cs:9,15,23`). Member-side: `/api/v1/coach/members/{memberId}/parameters`. No collision.
- Host: a **new** controller `CoachSessionPreferencesController` in `Mentora.API/Controllers/Coach/`, route `api/v1/coach/settings/session-preferences`. Reason: `CoachParametersController` is routed at `coach/parameters`, and the service is the COACH_PARAMETERS one that must stay untouched.
- Note: "the existing coach settings PUT" in the brief = `PUT /api/v1/coach/parameters` (16 fields, `CoachParameterService.cs:55-74`).

## 3.3 Writers of COACHES
- `CoachParameterService.UpdateAsync` (`CoachParameterService.cs:39-77`) loads the tracked entity and sets only `CoachFirstName`, `CoachLastName`, `CoachPhone`. Tracked property assignment means EF updates only the changed columns, so it **cannot overwrite** the new columns.
- No `new Coach`, `Coaches.Add`, `Coaches.Update` or `Entry(...)` anywhere in `*.cs`. Seeders (`CoachParameterSeeder`, `OfferProgramSeeder`) only read COACHES. Other services (`CoachMeService`, `AgendaService`, `VoucherService`, `MemberMeService`, ...) only read. `AuthService` does not touch COACHES.
- Raw SQL users (`ConversationService`, `Quiz*Service`) were not opened for COACHES references in this pass (grep for `COACHES` in `*.cs` outside migrations found none); recheck in Phase B.
- Coaches are only created by SQL fixtures (docs/review/**/fixture-*.sql). Those inserts take column defaults, so they satisfy the new CHECK.
- Conclusion: no fix needed (4.7 and test 5.5.12 use `PUT /api/v1/coach/parameters` with current values).

## 3.4 Validator pattern and accent-insensitive comparison
- Validators live in `Mentora.Core/Validators/<Area>/`, registered by `AddValidatorsFromAssemblyContaining` (`Program.cs:205`), injected as `IValidator<T>` and called with `await validator.ValidateAndThrowAsync(request, ct)` at the top of the service method (`CoachParameterService.cs:12-14,37`; `CoachMemberSettingsService.cs:11-13,23`).
- 422 keys: `GlobalExceptionMiddleware.cs:34-55` emits FluentValidation `PropertyName` unchanged (PascalCase, e.g. `InPersonLocations[1]`).
- No existing accent-insensitive helper. Use `CultureInfo.InvariantCulture.CompareInfo.GetSortKey(s, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace)` (or `Compare(...)==0`), or `Normalize(FormD)` with non-spacing marks stripped plus `ToLowerInvariant`. Known limitation: ligatures such as œ, and ß, are not folded (backlog). Check that InvariantGlobalization is off in the csproj before relying on `CompareInfo`.
- `CatalogSeeder.cs:876,892` and `QuizQuestionSeeder.cs:265` only use `Normalize(FormC)`.

## 3.5 Local data
- `SELECT count(*) FROM "COACHES"` in a READ ONLY transaction: **1 row**. Defaults (false, false, '[]') satisfy the CHECK.

## 3.6 Swagger grouping
- Coach controllers use `[ApiExplorerSettings(GroupName = "coach")]` and `[Tags("Coach — <Name>")]`, plus `[Authorize(Policy = "CoachOnly")]`. Docs are declared in `Program.cs:33-45` ("mobile", "coach", "internal").

## Points to confirm before Phase B
1. COACHES has no updated-date column: skip 4.5's date, no new column.
2. The brief's "coach settings" is `/coach/parameters`: a new controller is planned.
