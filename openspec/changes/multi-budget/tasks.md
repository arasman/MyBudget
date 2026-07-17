# Tasks: Multi-Budget Management

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 600–800 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 (Backend Foundation + Slices) → PR 2 (Frontend) |
| Delivery strategy | ask-on-risk |
| Chain strategy | feature-branch-chain |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: feature-branch-chain
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 — Backend | Entity changes, migration, auth wiring, 4 vertical slices | PR 1 | Base = `feature/multi-budget`; all backend tests included |
| 2 — Frontend | API module, CreateBudgetModal, BudgetSelectionView, router guard, AppLayout, i18n | PR 2 | Base = PR 1 branch; all frontend tests included |

---

## Phase 1: Backend Foundation

- [x] 1.1 — Modify `Project/src/MyBudget.Features/SharedKernel/Entities/Budget.cs`: add `IsDeleted (bool, default false)`, `DeletedAt (DateTimeOffset?)`, `Rename(string)`, `SoftDelete()`, `Restore()` domain methods. Acceptance: all three methods mutate fields as specified in design contracts; `UpdatedAt` is set on each call.

- [x] 1.2 — Modify `Project/src/MyBudget.Features/SharedKernel/Persistence/Configurations/BudgetConfiguration.cs`: map `IsDeleted` (`HasDefaultValue(false)`, `IsRequired()`) and `DeletedAt` (nullable `timestamptz`). Acceptance: EF scaffold produces correct column types.

- [x] 1.3 — Generate EF Core migration `AddBudgetSoftDelete` via `dotnet ef migrations add AddBudgetSoftDelete`. Verify migration adds `IsDeleted bool NOT NULL DEFAULT false` and `DeletedAt timestamptz NULL`; existing rows unaffected. Refs: BM-MIGRATION-01.

- [x] 1.4 — Modify `Project/src/MyBudget.Features/SharedKernel/Auth/BudgetMembershipDto.cs`: add `bool IsDeleted` to the record. Acceptance: record signature matches design contract `BudgetMembershipDto(Guid, string, string, bool)`.

- [x] 1.5 — Modify `Project/src/MyBudget.Features/SharedKernel/Auth/Authorization/AuthorizationPolicyExtensions.cs`: add `"budget:owner"` policy → `new BudgetRequirement(BudgetRole.Owner)` alongside existing policies. Acceptance: policy is registered; `DeleteBudget` can reference it.

- [x] 1.6 — Modify `Project/src/MyBudget.Features/SharedKernel/Auth/Authorization/BudgetAuthorizationHandler.cs`: extend Dapper membership query to `JOIN "Budgets" b ON b."Id" = bm."BudgetId" WHERE b."IsDeleted" = false`; null result triggers existing `budget-not-found` 404 path; do not write cache entry for deleted budget. Refs: AUTHZ-1, SC-BM-07.

- [x] 1.7 — Modify `Project/src/MyBudget.Features/Features/Auth/GetCurrentUser/GetCurrentUserHandler.cs`: join `b."IsDeleted"` into the MembershipRow Dapper query; map to `BudgetMembershipDto.IsDeleted`; include all memberships (deleted and active). Refs: ME-1.

---

## Phase 2: Backend Vertical Slices

- [x] 2.1 — Create `Project/src/MyBudget.Features/Features/Budgets/CreateBudget/CreateBudgetCommand.cs`: `sealed record CreateBudgetCommand(string Name, Guid UserId) : IRequest<Result<CreateBudgetResponse>>` and `CreateBudgetResponse(Guid BudgetId)`.

- [x] 2.2 — Create `Project/src/MyBudget.Features/Features/Budgets/CreateBudget/CreateBudgetValidator.cs`: validate `Name` not empty/whitespace (`BUDGET_NAME_REQUIRED`) and max 200 chars (`BUDGET_NAME_TOO_LONG`). Refs: BM-01, SC-BM-05.

- [x] 2.3 — Create `Project/src/MyBudget.Features/Features/Budgets/CreateBudget/CreateBudgetHandler.cs`: EF — create `Budget` + `BudgetMembership(BudgetRole.Owner)` in one transaction; `IsDeleted = false`. Return `CreateBudgetResponse(budget.Id)`. Refs: BM-01.

- [x] 2.4 — Create `Project/src/MyBudget.Features/Features/Budgets/CreateBudget/CreateBudgetEndpoint.cs`: `POST /api/budgets`, `RequireAuthorization()` (no budget policy), returns 201 with `{ id, name }`. Refs: SC-BM-04.

- [x] 2.5 — Create `Project/src/MyBudget.Features/Features/Budgets/RenameBudget/RenameBudgetCommand.cs`: `sealed record RenameBudgetCommand(Guid BudgetId, string NewName, Guid UserId) : IRequest<Result<Unit>>`.

- [x] 2.6 — Create `Project/src/MyBudget.Features/Features/Budgets/RenameBudget/RenameBudgetValidator.cs`: validate `NewName` not empty and max 200 chars. Refs: BM-02, SC-BM-05.

- [x] 2.7 — Create `Project/src/MyBudget.Features/Features/Budgets/RenameBudget/RenameBudgetHandler.cs`: EF — load `Budget`, call `budget.Rename(newName)`, evict `budget-membership:{userId}:{budgetId}` for all current members via Dapper query. Return 200 with `{ id, name }`. Refs: BM-02.

- [x] 2.8 — Create `Project/src/MyBudget.Features/Features/Budgets/RenameBudget/RenameBudgetEndpoint.cs`: `PUT /api/budgets/{id}`, `"budget:admin"` policy, returns 200. Refs: SC-BM-03.

- [x] 2.9 — Create `Project/src/MyBudget.Features/Features/Budgets/DeleteBudget/DeleteBudgetCommand.cs`: `sealed record DeleteBudgetCommand(Guid BudgetId, Guid UserId) : IRequest<Result<Unit>>`.

- [x] 2.10 — Create `Project/src/MyBudget.Features/Features/Budgets/DeleteBudget/DeleteBudgetHandler.cs`: EF — load `Budget`, call `budget.SoftDelete()`, evict all member cache keys via Dapper. Returns 204. Refs: BM-03, SC-BM-06, SC-BM-07.

- [x] 2.11 — Create `Project/src/MyBudget.Features/Features/Budgets/DeleteBudget/DeleteBudgetEndpoint.cs`: `DELETE /api/budgets/{id}`, `"budget:owner"` policy, returns 204. Refs: SC-BM-02.

- [x] 2.12 — Create `Project/src/MyBudget.Features/Features/Budgets/RestoreBudget/RestoreBudgetCommand.cs`: `sealed record RestoreBudgetCommand(Guid BudgetId, Guid UserId) : IRequest<Result<Unit>>`.

- [x] 2.13 — Create `Project/src/MyBudget.Features/Features/Budgets/RestoreBudget/RestoreBudgetHandler.cs`: load budget via `IgnoreQueryFilters()`, verify caller has `BudgetRole.Owner` via Dapper (manual check — handler bypasses deleted-budget 404 path), call `budget.Restore()`, evict all member cache keys, return 200. Refs: BM-04.

- [x] 2.14 — Create `Project/src/MyBudget.Features/Features/Budgets/RestoreBudget/RestoreBudgetEndpoint.cs`: `POST /api/budgets/{id}/restore`, `RequireAuthorization()` only (manual ownership check in handler), returns 200. Refs: decision #7.

---

## Phase 3: Frontend

- [x] 3.1 — Create `Project/frontend/src/features/budget-structure/api/budgets.api.ts`: export `createBudget(name)`, `renameBudget(budgetId, newName)`, `deleteBudget(budgetId)`, `restoreBudget(budgetId)` using the existing axios instance. Match TypeScript signatures from design contracts.

- [x] 3.2 — Modify `Project/frontend/src/stores/auth.store.ts`: add `isDeleted: boolean` to `BudgetMembershipDto` interface. Verify `fetchMe()` maps the new field from the API response. Refs: ME-1.

- [x] 3.3 — Create `Project/frontend/src/features/budget-structure/components/CreateBudgetModal.vue`: name input (max 200, required), submit/cancel, inline validation error, disable submit while in-flight, inline API error on 4xx. Refs: BM-FE-02.

- [x] 3.4 — Modify `Project/frontend/src/features/budget-structure/views/BudgetSelectionView.vue`: add "New Budget" button (opens `CreateBudgetModal`), after creation call `authStore.fetchMe()` and navigate to `/budgets/{newId}/cycles`; add "Show deleted" toggle that reveals deleted memberships with a "Restore" button; skip auto-redirect when sole membership is `isDeleted: true`. Refs: BM-FE-01.

- [x] 3.5 — Modify `Project/frontend/src/router/index.ts`: add `beforeEach` guard — for routes with `budgetId` param, find matching membership in `authStore.user.memberships`; if `isDeleted: true`, redirect to `/`. Refs: BM-FE-03.

- [x] 3.6 — Modify `Project/frontend/src/layouts/AppLayout.vue`: on mount, if `layoutStore.activeBudgetName` is null and route has `budgetId`, look up name from `authStore.user.memberships` and call `layoutStore.setActiveBudget(id, name)`. Refs: BM-FE-04.

- [x] 3.7 — Modify `Project/frontend/src/i18n/locales/en.json` and `es.json`: add 10 keys under `budgetStructure.selection` (`newBudget`, `createBudgetTitle`, `budgetNameLabel`, `budgetNamePlaceholder`, `createSuccess`, `showDeleted`, `restore`, `restoreSuccess`, `confirmDelete`, `deleteSuccess`). Refs: BM-FE-05.

---

## Phase 4: Tests

- [x] 4.1 — Unit tests `Project/tests/MyBudget.Features.Tests/SharedKernel/Entities/BudgetDomainTests.cs`: test `Rename()`, `SoftDelete()`, `Restore()` each set correct fields; `UpdatedAt` updated.

- [x] 4.2 — Unit tests `Project/tests/MyBudget.Features.Tests/Features/Budgets/CreateBudget/CreateBudgetValidatorTests.cs`: empty name → `BUDGET_NAME_REQUIRED`; name > 200 chars → `BUDGET_NAME_TOO_LONG`; valid name passes. Refs: BM-01 scenarios.

- [x] 4.3 — Unit tests `Project/tests/MyBudget.Features.Tests/Features/Budgets/RenameBudget/RenameBudgetValidatorTests.cs`: same constraints as 4.2. Refs: BM-02.

- [x] 4.4 — Unit tests `Project/tests/MyBudget.Features.Tests/SharedKernel/Auth/BudgetAuthorizationHandlerTests.cs` (extend existing): add scenario — deleted budget query returns null → `budget-not-found` is set, no cache write. Refs: AUTHZ-1.

- [x] 4.5 — Integration tests `Project/tests/MyBudget.Integration.Tests/Features/Budgets/CreateBudgetTests.cs`: happy path 201; 422 name empty; 422 name too long; 401 unauthenticated. Refs: BM-01 scenarios.

- [x] 4.6 — Integration tests `Project/tests/MyBudget.Integration.Tests/Features/Budgets/RenameBudgetTests.cs`: happy path 200; 403 operator role; 404 not found; 422 invalid name. Refs: BM-02 scenarios.

- [x] 4.7 — Integration tests `Project/tests/MyBudget.Integration.Tests/Features/Budgets/DeleteBudgetTests.cs`: happy path 204; 403 admin (non-owner); 404 already deleted; 404 not found. Refs: BM-03 scenarios.

- [x] 4.8 — Integration tests `Project/tests/MyBudget.Integration.Tests/Features/Budgets/RestoreBudgetTests.cs`: happy path 200; 404 not deleted; 403 admin role. Refs: BM-04 scenarios.

- [x] 4.9 — Integration tests (extend) `Project/tests/MyBudget.Integration.Tests/Features/Auth/LogoutAndMeTests.cs`: `GET /api/auth/me` returns `isDeleted: false` for active membership; returns `isDeleted: true` for soft-deleted membership; deleted membership is included (not filtered). Refs: ME-1 scenarios.

- [x] 4.10 — Frontend component test `Project/frontend/src/features/budget-structure/components/__tests__/CreateBudgetModal.spec.ts`: submit with empty name shows inline error (no API call); submit with valid name disables button during request; success closes modal. Refs: BM-FE-02 scenarios.

- [x] 4.11 — Frontend component test `Project/frontend/src/features/budget-structure/views/__tests__/BudgetSelectionView.spec.ts` (extend or create): "show deleted" toggle reveals deleted membership with restore button; auto-redirect skipped when sole membership is deleted. Refs: BM-FE-01 scenarios.

---

## Phase 5: Verification

- [ ] 5.1 — Run `dotnet test` in `Project/tests/`; all tests green including new slices and modified auth tests.

- [ ] 5.2 — Run `npm run test:unit` in `Project/frontend/`; all new component and existing tests pass.

- [ ] 5.3 — Apply migration against dev DB (`dotnet ef database update`) and verify: existing budgets have `IsDeleted = false`; `GET /api/auth/me` includes `isDeleted` on memberships; `POST /api/budgets` creates budget; `DELETE` + `POST .../restore` round-trip; router guard redirects on deleted budget navigation.
