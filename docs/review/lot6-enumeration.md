# Lot 6 read-only enumeration (main..HEAD, c9d9618..6be3cf6)

## 1. ROUTE TABLE

| HTTP method | full route template | Controller.Action | Swagger doc group | authorization policy |
|---|---|---|---|---|
| DELETE | api/v1/account/deletion-request | AccountController.CancelDeletion | mobile | Authorize (no policy) |
| GET | api/v1/account/deletion-request | AccountController.GetDeletionStatus | mobile | Authorize (no policy) |
| POST | api/v1/account/deletion-request | AccountController.RequestDeletion | mobile | Authorize (no policy) |
| POST | api/v1/auth/logout | AuthController.Logout | mobile | Authorize (no policy) |
| POST | api/v1/auth/otp/request | AuthController.RequestOtp | mobile | none |
| POST | api/v1/auth/otp/verify | AuthController.VerifyOtp | mobile | none |
| POST | api/v1/auth/token/refresh | AuthController.RefreshToken | mobile | none |
| GET | api/v1/coach/agenda | CoachAgendaController.Get | coach | CoachOnly |
| GET | api/v1/coach/conversations | CoachConversationsController.GetInbox | coach | CoachOnly |
| GET | api/v1/coach/exercises | CoachExercisesController.GetAll | coach | CoachOnly |
| POST | api/v1/coach/exercises | CoachExercisesController.Create | coach | CoachOnly |
| DELETE | api/v1/coach/exercises/{exerciseId:guid} | CoachExercisesController.Delete | coach | CoachOnly |
| GET | api/v1/coach/exercises/{exerciseId:guid} | CoachExercisesController.GetById | coach | CoachOnly |
| PUT | api/v1/coach/exercises/{exerciseId:guid} | CoachExercisesController.Update | coach | CoachOnly |
| GET | api/v1/coach/me | CoachMeController.GetMe | coach | CoachOnly |
| GET | api/v1/coach/members/{memberId:guid}/conversation | CoachConversationController.Get | coach | CoachOnly |
| GET | api/v1/coach/members/{memberId:guid}/conversation/messages | CoachConversationMessagesController.List | coach | CoachOnly |
| POST | api/v1/coach/members/{memberId:guid}/conversation/messages | CoachConversationMessagesController.Send | coach | CoachOnly |
| PATCH | api/v1/coach/members/{memberId:guid}/conversation/messages/read | CoachConversationMessagesController.MarkAsRead | coach | CoachOnly |
| GET | api/v1/coach/members/{memberId:guid}/parameters | CoachMemberSettingsController.Get | coach | CoachOnly |
| PUT | api/v1/coach/members/{memberId:guid}/parameters | CoachMemberSettingsController.Update | coach | CoachOnly |
| GET | api/v1/coach/members/{memberId:guid}/training-programs | CoachProgramsController.GetAllForMember | coach | CoachOnly |
| POST | api/v1/coach/members/{memberId:guid}/training-programs | CoachProgramsController.Assign | coach | CoachOnly |
| GET | api/v1/coach/parameters | CoachParametersController.Get | coach | CoachOnly |
| PUT | api/v1/coach/parameters | CoachParametersController.Update | coach | CoachOnly |
| GET | api/v1/coach/product-packs | CoachProductPacksController.GetAll | coach | CoachOnly |
| POST | api/v1/coach/product-packs | CoachProductPacksController.Create | coach | CoachOnly |
| DELETE | api/v1/coach/product-packs/{packId:guid} | CoachProductPacksController.Delete | coach | CoachOnly |
| GET | api/v1/coach/product-packs/{packId:guid} | CoachProductPacksController.GetById | coach | CoachOnly |
| PUT | api/v1/coach/product-packs/{packId:guid} | CoachProductPacksController.Update | coach | CoachOnly |
| POST | api/v1/coach/product-packs/{packId:guid}/publish | CoachProductPacksController.Publish | coach | CoachOnly |
| POST | api/v1/coach/product-packs/{packId:guid}/unpublish | CoachProductPacksController.Unpublish | coach | CoachOnly |
| GET | api/v1/coach/products | CoachProductsController.GetAll | coach | CoachOnly |
| POST | api/v1/coach/products | CoachProductsController.Create | coach | CoachOnly |
| DELETE | api/v1/coach/products/{productId:guid} | CoachProductsController.Delete | coach | CoachOnly |
| GET | api/v1/coach/products/{productId:guid} | CoachProductsController.GetById | coach | CoachOnly |
| PUT | api/v1/coach/products/{productId:guid} | CoachProductsController.Update | coach | CoachOnly |
| POST | api/v1/coach/products/{productId:guid}/publish | CoachProductsController.Publish | coach | CoachOnly |
| POST | api/v1/coach/products/{productId:guid}/unpublish | CoachProductsController.Unpublish | coach | CoachOnly |
| PUT | api/v1/coach/program-sessions/{programSessionId:guid}/booking | CoachProgramSessionsController.UpdateBooking | coach | CoachOnly |
| PUT | api/v1/coach/program-sessions/{programSessionId:guid}/completion | CoachProgramSessionsController.UpdateCompletion | coach | CoachOnly |
| GET | api/v1/coach/program-templates | CoachProgramTemplatesController.GetAll | coach | CoachOnly |
| POST | api/v1/coach/program-templates | CoachProgramTemplatesController.Create | coach | CoachOnly |
| DELETE | api/v1/coach/program-templates/{programTemplateId:guid} | CoachProgramTemplatesController.Delete | coach | CoachOnly |
| GET | api/v1/coach/program-templates/{programTemplateId:guid} | CoachProgramTemplatesController.GetById | coach | CoachOnly |
| PUT | api/v1/coach/program-templates/{programTemplateId:guid} | CoachProgramTemplatesController.Update | coach | CoachOnly |
| GET | api/v1/coach/programs | OfferProgramsController.GetAll | coach | CoachOnly |
| POST | api/v1/coach/programs | OfferProgramsController.Create | coach | CoachOnly |
| DELETE | api/v1/coach/programs/{id:guid} | OfferProgramsController.Delete | coach | CoachOnly |
| PUT | api/v1/coach/programs/{id:guid} | OfferProgramsController.Update | coach | CoachOnly |
| GET | api/v1/coach/session-slots | SessionSlotsController.GetAll | coach | CoachOnly |
| POST | api/v1/coach/session-slots | SessionSlotsController.Create | coach | CoachOnly |
| DELETE | api/v1/coach/session-slots/{id:guid} | SessionSlotsController.Delete | coach | CoachOnly |
| PUT | api/v1/coach/session-slots/{id:guid} | SessionSlotsController.Update | coach | CoachOnly |
| GET | api/v1/coach/sessions | CoachSessionsController.GetAll | coach | CoachOnly |
| POST | api/v1/coach/sessions/group | CoachGroupSessionsController.CreateGroupSession | coach | CoachOnly |
| GET | api/v1/coach/sessions/{sessionId:guid} | CoachSessionsController.GetById | coach | CoachOnly |
| POST | api/v1/coach/sessions/{sessionId:guid}/cancel | CoachSessionsController.Cancel | coach | CoachOnly |
| GET | api/v1/coach/sessions/{sessionId:guid}/participants | CoachGroupSessionsController.ListParticipants | coach | CoachOnly |
| POST | api/v1/coach/sessions/{sessionId:guid}/participants | CoachGroupSessionsController.RegisterParticipant | coach | CoachOnly |
| DELETE | api/v1/coach/sessions/{sessionId:guid}/participants/{memberId:guid} | CoachGroupSessionsController.UnregisterParticipant | coach | CoachOnly |
| DELETE | api/v1/coach/training-programs/{programId:guid} | CoachProgramsController.Delete | coach | CoachOnly |
| GET | api/v1/coach/training-programs/{programId:guid} | CoachProgramsController.GetById | coach | CoachOnly |
| PUT | api/v1/coach/training-programs/{programId:guid} | CoachProgramsController.Update | coach | CoachOnly |
| DELETE | api/v1/devices | UserDevicesController.Delete | mobile | Authorize (no policy) |
| POST | api/v1/devices | UserDevicesController.Register | mobile | Authorize (no policy) |
| GET | api/v1/internal/stripe/fake-checkout/{sessionId} | InternalStripeWebhookController.FakeCheckout | internal | none |
| POST | api/v1/internal/stripe/simulate-webhook | InternalStripeWebhookController.SimulateWebhook | internal | none |
| POST | api/v1/internal/stripe/webhook | InternalStripeWebhookController.ReceiveWebhook | internal | none |
| GET | api/v1/member/agenda | MemberAgendaController.Get | mobile | MemberOnly |
| GET | api/v1/member/coaches/{coachId:guid}/cart | MemberCartController.Get | mobile | MemberOnly |
| POST | api/v1/member/coaches/{coachId:guid}/cart/checkout | MemberCartController.Checkout | mobile | MemberOnly |
| DELETE | api/v1/member/coaches/{coachId:guid}/cart/items | MemberCartController.Clear | mobile | MemberOnly |
| POST | api/v1/member/coaches/{coachId:guid}/cart/items | MemberCartController.AddItem | mobile | MemberOnly |
| DELETE | api/v1/member/coaches/{coachId:guid}/cart/items/{cartItemId:guid} | MemberCartController.RemoveItem | mobile | MemberOnly |
| PUT | api/v1/member/coaches/{coachId:guid}/cart/items/{cartItemId:guid} | MemberCartController.UpdateItemQuantity | mobile | MemberOnly |
| GET | api/v1/member/coaches/{coachId:guid}/catalog | MemberCatalogController.GetCatalog | mobile | MemberOnly |
| GET | api/v1/member/coaches/{coachId:guid}/conversation | MemberConversationController.Get | mobile | MemberOnly |
| GET | api/v1/member/coaches/{coachId:guid}/conversation/messages | MemberConversationMessagesController.List | mobile | MemberOnly |
| POST | api/v1/member/coaches/{coachId:guid}/conversation/messages | MemberConversationMessagesController.Send | mobile | MemberOnly |
| PATCH | api/v1/member/coaches/{coachId:guid}/conversation/messages/read | MemberConversationMessagesController.MarkAsRead | mobile | MemberOnly |
| GET | api/v1/member/exercises | MemberExercisesController.GetAll | mobile | MemberOnly |
| GET | api/v1/member/exercises/{exerciseId:guid} | MemberExercisesController.GetById | mobile | MemberOnly |
| GET | api/v1/member/me | MemberMeController.GetMe | mobile | MemberOnly |
| GET | api/v1/member/orders | MemberOrdersController.GetAll | mobile | MemberOnly |
| GET | api/v1/member/orders/{orderId:guid} | MemberOrdersController.GetById | mobile | MemberOnly |
| GET | api/v1/member/parameters | MemberParametersController.Get | mobile | MemberOnly |
| PUT | api/v1/member/parameters | MemberParametersController.Update | mobile | MemberOnly |
| PUT | api/v1/member/program-sessions/{programSessionId:guid}/completion | MemberProgramSessionsController.UpdateCompletion | mobile | MemberOnly |
| GET | api/v1/member/programs/current | MemberProgramsController.GetCurrent | mobile | MemberOnly |
| GET | api/v1/member/programs/{programId:guid} | MemberProgramsController.GetById | mobile | MemberOnly |
| GET | api/v1/member/session-slots | MemberSessionSlotsController.GetAvailable | mobile | MemberOnly |
| GET | api/v1/member/sessions | MemberSessionsController.GetAll | mobile | MemberOnly |
| POST | api/v1/member/sessions | MemberSessionsController.Reserve | mobile | MemberOnly |
| GET | api/v1/member/sessions/{sessionId:guid} | MemberSessionsController.GetById | mobile | MemberOnly |
| POST | api/v1/member/sessions/{sessionId:guid}/cancel | MemberSessionsController.Cancel | mobile | MemberOnly |
| GET | api/v1/member/vouchers | MemberVouchersController.GetAll | mobile | MemberOnly |
| GET | api/v1/member/vouchers/{voucherId:guid} | MemberVouchersController.GetById | mobile | MemberOnly |

no duplicates

## 2. TENANT SCOPE TABLE

Scope: every endpoint added in main..HEAD (all ten Lot 6 controller files are pure additions — `git diff --stat main..HEAD -- Mentora.API/Controllers` shows 10 files, 740 insertions(+), 0 deletions(-)) that takes memberId, programId, programTemplateId, exerciseId, sessionId, programSessionId, sessionParticipantId, or productId in its route or body. `sessionParticipantId` does not appear in any route or body anywhere in Mentora.API — no endpoint qualifies on that field.

| method + full template | entity loaded | the exact C# line that filters on COACH_ID or joins MEMBER_COACHES (file:line) | HTTP status actually produced when that filter yields no row |
|---|---|---|---|
| GET api/v1/coach/exercises/{exerciseId:guid} | Exercise | `Mentora.Infrastructure/Services/ExerciseService.cs:55` — `&& (e.ExerciseCoachId == null \|\| e.ExerciseCoachId == coachId), ct)` | 404 (NotFoundException) |
| PUT api/v1/coach/exercises/{exerciseId:guid} | Exercise | `Mentora.Infrastructure/Services/ExerciseService.cs:96` — `.FirstOrDefaultAsync(e => e.ExerciseId == exerciseId && e.ExerciseCoachId == coachId, ct)` | 404 (NotFoundException) |
| DELETE api/v1/coach/exercises/{exerciseId:guid} | Exercise | `Mentora.Infrastructure/Services/ExerciseService.cs:117` — `.FirstOrDefaultAsync(e => e.ExerciseId == exerciseId && e.ExerciseCoachId == coachId, ct)` | 404 (NotFoundException) |
| GET api/v1/member/exercises/{exerciseId:guid} | Exercise | `Mentora.Infrastructure/Services/ExerciseService.cs:129` — `.Where(mc => mc.MemberId == memberId)` (MEMBER_COACHES join) | 404 (NotFoundException) |
| GET api/v1/coach/program-templates/{programTemplateId:guid} | ProgramTemplate | `Mentora.Infrastructure/Services/ProgramTemplateService.cs:58` — `&& (t.ProgramTemplateCoachId == null \|\| t.ProgramTemplateCoachId == coachId), ct)` | 404 (NotFoundException) |
| POST api/v1/coach/program-templates | ProgramTemplate (nested Exercise ids in Body) | `Mentora.Infrastructure/Services/ExerciseService.cs:165` — `&& (e.ExerciseCoachId == null \|\| e.ExerciseCoachId == coachId)` (via ProgramTemplateBodyValidator → ResolveVisibleActiveIdsAsync) | 422 (FluentValidation ValidationException) for an inaccessible exerciseId; new ProgramTemplate row has no coach filter to fail (create, not lookup) |
| PUT api/v1/coach/program-templates/{programTemplateId:guid} | ProgramTemplate + nested Exercise ids in Body | `Mentora.Infrastructure/Services/ProgramTemplateService.cs:97` — `.FirstOrDefaultAsync(t => t.ProgramTemplateId == programTemplateId && t.ProgramTemplateCoachId == coachId, ct)`; nested ids via `Mentora.Infrastructure/Services/ExerciseService.cs:165` | 404 (NotFoundException) for programTemplateId; 422 (ValidationException) for an inaccessible nested exerciseId |
| DELETE api/v1/coach/program-templates/{programTemplateId:guid} | ProgramTemplate | `Mentora.Infrastructure/Services/ProgramTemplateService.cs:115` — `.FirstOrDefaultAsync(t => t.ProgramTemplateId == programTemplateId && t.ProgramTemplateCoachId == coachId, ct)` | 404 (NotFoundException) |
| GET api/v1/coach/members/{memberId:guid}/training-programs | Member (MEMBER_COACHES link) | `Mentora.Infrastructure/Services/ProgramService.cs:31` — `var isLinked = await db.MemberCoaches.AnyAsync(mc => mc.MemberId == memberId && mc.CoachId == coachId, ct);` | 404 (NotFoundException) when not linked; empty 200 when linked with zero programs |
| GET api/v1/coach/training-programs/{programId:guid} | Program | `Mentora.Infrastructure/Services/ProgramService.cs:49` — `.FirstOrDefaultAsync(p => p.ProgramId == programId && p.ProgramCoachId == coachId, ct)` | 404 (NotFoundException) |
| POST api/v1/coach/members/{memberId:guid}/training-programs | Member (MEMBER_COACHES link) + ProgramTemplate (request.TemplateId) + nested Exercise ids in Body | `Mentora.Infrastructure/Services/ProgramService.cs:60` — `var isLinked = await db.MemberCoaches.AnyAsync(mc => mc.MemberId == memberId && mc.CoachId == coachId, ct);`; template: `Mentora.Infrastructure/Services/ProgramService.cs:73` — `&& (t.ProgramTemplateCoachId == null \|\| t.ProgramTemplateCoachId == coachId)`; nested ids via `Mentora.Infrastructure/Services/ExerciseService.cs:165` | 404 (NotFoundException) for memberId not linked or templateId not visible; 422 (ValidationException) for an inaccessible nested exerciseId (only checked when TemplateId is absent) |
| PUT api/v1/coach/training-programs/{programId:guid} | Program + nested Exercise ids in Body | `Mentora.Infrastructure/Services/ProgramService.cs:171` — `.FirstOrDefaultAsync(p => p.ProgramId == programId && p.ProgramCoachId == coachId, ct)`; nested ids via `Mentora.Infrastructure/Services/ExerciseService.cs:165` | 404 (NotFoundException) for programId; 422 (ValidationException) for an inaccessible nested exerciseId |
| DELETE api/v1/coach/training-programs/{programId:guid} | Program | `Mentora.Infrastructure/Services/ProgramService.cs:214` — `.FirstOrDefaultAsync(p => p.ProgramId == programId && p.ProgramCoachId == coachId, ct)` | 404 (NotFoundException) |
| GET api/v1/member/programs/{programId:guid} | Program | `Mentora.Infrastructure/Services/ProgramService.cs:321` — `.FirstOrDefaultAsync(p => p.ProgramId == programId && p.ProgramMemberId == memberId, ct)` | 404 (NotFoundException) |
| POST api/v1/coach/sessions/group | SessionSlot (request.SlotId) + Product (request.ProductId) | slot: `Mentora.Infrastructure/Services/SessionService.cs:394` — `.FirstOrDefaultAsync(s => s.SessionSlotId == request.SlotId && s.CoachId == coachId, ct)`; product: `Mentora.Infrastructure/Services/SessionService.cs:414` — `.FirstOrDefaultAsync(p => p.ProductId == request.ProductId && p.CoachId == coachId, ct)` | 404 (NotFoundException) for either |
| GET api/v1/coach/sessions/{sessionId:guid}/participants | Session (group) | `Mentora.Infrastructure/Services/SessionService.cs:778-782` — `.FirstOrDefaultAsync(s => s.SessionId == sessionId && s.SessionCoachId == coachId && (s.SessionOfferType == OfferType.PresentielGroupe \|\| s.SessionOfferType == OfferType.VisioGroupe), ct)` | 404 (NotFoundException) |
| POST api/v1/coach/sessions/{sessionId:guid}/participants | Session (group) + Member (MEMBER_COACHES link, request.MemberId) | session: `Mentora.Infrastructure/Services/SessionService.cs:778-782` (see above); member: `Mentora.Infrastructure/Services/SessionService.cs:501` — `.AnyAsync(mc => mc.MemberId == request.MemberId && mc.CoachId == coachId, ct)` | 404 (NotFoundException) for either |
| DELETE api/v1/coach/sessions/{sessionId:guid}/participants/{memberId:guid} | Session (group) | `Mentora.Infrastructure/Services/SessionService.cs:778-782` (see above) | 404 (NotFoundException) |
| PUT api/v1/coach/program-sessions/{programSessionId:guid}/booking | ProgramSession + Session (request.SessionId) | programSession: `Mentora.Infrastructure/Services/ProgramService.cs:230` — `.FirstOrDefaultAsync(ps => ps.ProgramSessionId == programSessionId && ps.ProgramSessionCoachId == coachId, ct)`; session: `Mentora.Infrastructure/Services/ProgramService.cs:236` — `.FirstOrDefaultAsync(s => s.SessionId == sessionId && s.SessionCoachId == coachId, ct)` | 404 (NotFoundException) for either |
| PUT api/v1/coach/program-sessions/{programSessionId:guid}/completion | ProgramSession | `Mentora.Infrastructure/Services/ProgramService.cs:292` — `.FirstOrDefaultAsync(ps => ps.ProgramSessionId == programSessionId && ps.ProgramSessionCoachId == coachId, ct)`; then `Mentora.Infrastructure/Services/ProgramService.cs:298` — `.AnyAsync(mc => mc.MemberId == programSession.ProgramSessionMemberId && mc.CoachId == coachId, ct)` | 404 (NotFoundException) for either |
| PUT api/v1/member/program-sessions/{programSessionId:guid}/completion | ProgramSession | `Mentora.Infrastructure/Services/ProgramService.cs:336` — `.FirstOrDefaultAsync(ps => ps.ProgramSessionId == programSessionId && ps.ProgramSessionMemberId == memberId, ct)` | 404 (NotFoundException) |
