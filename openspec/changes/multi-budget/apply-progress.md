# Apply Progress — multi-budget (all batches)

**Batch**: PR 1 (Backend) + PR 2 (Frontend)
**Mode**: Standard (TDD OFF)
**Last updated**: 2026-07-16

## Completed Tasks

### Phase 1: Backend Foundation

- [x] 1.1 — `Budget.cs`: added `IsDeleted`, `DeletedAt`, `Rename()`, `SoftDelete()`, `Restore()`
- [x] 1.2 — `BudgetConfiguration.cs`: mapped `IsDeleted` (`HasDefaultValue(false)`, `IsRequired()`) and `DeletedAt` (nullable `timestamptz`)
- [x] 1.3 — Migration `AddBudgetSoftDelete` generated at `20260717013425_AddBudgetSoftDelete.cs`
- [x] 1.4 — `BudgetMembershipDto`: added `bool IsDeleted` as 4th positional parameter
- [x] 1.5 — `AuthorizationPolicyExtensions`: added `"budget:owner"` policy → `BudgetRequirement(BudgetRole.Owner)`
- [x] 1.6 — `BudgetAuthorizationHandler`: JOIN `Budgets` on `IsDeleted = false`; soft-deleted budget sets `budget-not-found` flag
- [x] 1.7 — `GetCurrentUserHandler`: joined `b."IsDeleted"` into `MembershipRow`; mapped to `BudgetMembershipDto.IsDeleted`

### Phase 2: Backend Vertical Slices

- [x] 2.1–2.4 — `CreateBudget` slice (Command, Validator, Handler, Endpoint) — `POST /api/budgets`
- [x] 2.5–2.8 — `RenameBudget` slice (Command, Validator, Handler, Endpoint) — `PUT /api/budgets/{id}`
- [x] 2.9–2.11 — `DeleteBudget` slice (Command, Handler, Endpoint) — `DELETE /api/budgets/{id}`
- [x] 2.12–2.14 — `RestoreBudget` slice (Command, Handler, Endpoint) — `POST /api/budgets/{id}/restore`

### Phase 3: Frontend

- [x] 3.1 — `budgets.api.ts`: created with `createBudget`, `renameBudget`, `deleteBudget`, `restoreBudget` using `http` from `@/api/axios`
- [x] 3.2 — `auth.store.ts` `BudgetMembershipDto`: added `isDeleted: boolean` field
- [x] 3.3 — `CreateBudgetModal.vue`: name input (required, max 200), inline validation, loading state, server error display, emits `created` event
- [x] 3.4 — `BudgetSelectionView.vue`: "New Budget" button, `CreateBudgetModal`, "Show deleted" toggle, Restore/Delete action buttons, corrected auto-redirect logic (only redirects when exactly 1 active membership)
- [x] 3.5 — `router/index.ts`: added `beforeEach` guard — if `budgetId` param found in memberships with `isDeleted: true`, redirect to `/`
- [x] 3.6 — `AppLayout.vue`: `onMounted` restores `activeBudgetName` from memberships when `layoutStore.activeBudgetName` is null and route has `budgetId`
- [x] 3.7 — `en.json` + `es.json`: added 10 keys under `budgetStructure.selection` (createBudget, createBudgetTitle, budgetNameLabel, budgetNamePlaceholder, budgetNameRequired, budgetNameTooLong, createSuccess, deleteBudget, confirmDelete, restoreBudget, restoreSuccess, showDeleted, deletedBadge, deleteSuccess)

### Phase 4: Tests

- [x] 4.1 — `BudgetDomainTests.cs` — 11 unit tests for `Rename`/`SoftDelete`/`Restore` domain methods
- [x] 4.2 — `CreateBudgetValidatorTests.cs` — 5 validator unit tests
- [x] 4.3 — `RenameBudgetValidatorTests.cs` — 5 validator unit tests
- [x] 4.4 — `BudgetAuthorizationHandlerTests.cs` extended — cache miss / no-write scenario
- [x] 4.5 — `CreateBudgetTests.cs` — 4 integration scenarios (201, 422 empty, 422 long, 401)
- [x] 4.6 — `RenameBudgetTests.cs` — 4 integration scenarios (200, 403 operator, 404, 422)
- [x] 4.7 — `DeleteBudgetTests.cs` — 4 integration scenarios (204, 403 admin, 404 already deleted, 404 not found)
- [x] 4.8 — `RestoreBudgetTests.cs` — 3 integration scenarios (200, 404 not deleted, 403 admin)
- [x] 4.9 — `LogoutAndMeTests.cs` extended — 2 scenarios for `isDeleted` field
- [x] 4.10 — `CreateBudgetModal.spec.ts` — 5 component tests (renders, empty validation, max-length validation, loading state, success emit, server error)
- [x] 4.11 — `BudgetSelectionView.spec.ts` — 7 component tests (hide deleted by default, show on toggle, restore button visibility, delete button owner-only, no auto-redirect for sole deleted, auto-redirect for one active, restore action)

## Remaining Tasks

- [ ] 5.1 — Run `dotnet test` — requires Docker stack for integration tests
- [ ] 5.2 — Run `pnpm test` in `Project/frontend/` — **211 tests passing** (verified in PR 2 batch)
- [ ] 5.3 — Apply migration + E2E smoke test against dev DB

## Test Results

### Backend (PR 1)
Unit tests: **306 passed, 0 failed**
Integration tests: require live Postgres — not run (require Docker stack)

### Frontend (PR 2)
`pnpm test`: **211 passed, 0 failed** (31 test files)

## Files Changed

### PR 1 — Backend
| File | Action |
|------|--------|
| `Project/src/MyBudget.Features/SharedKernel/Entities/Budget.cs` | Modified |
| `Project/src/MyBudget.Features/SharedKernel/Persistence/Configurations/BudgetConfiguration.cs` | Modified |
| `Project/src/MyBudget.Features/Migrations/20260717013425_AddBudgetSoftDelete.cs` | Created |
| `Project/src/MyBudget.Features/Migrations/20260717013425_AddBudgetSoftDelete.Designer.cs` | Created |
| `Project/src/MyBudget.Features/SharedKernel/Auth/BudgetMembershipDto.cs` | Modified |
| `Project/src/MyBudget.Features/SharedKernel/Auth/Authorization/AuthorizationPolicyExtensions.cs` | Modified |
| `Project/src/MyBudget.Features/SharedKernel/Auth/Authorization/BudgetAuthorizationHandler.cs` | Modified |
| `Project/src/MyBudget.Features/Features/Auth/GetCurrentUser/GetCurrentUserHandler.cs` | Modified |
| `Project/src/MyBudget.Features/Features/Budgets/CreateBudget/` (4 files) | Created |
| `Project/src/MyBudget.Features/Features/Budgets/RenameBudget/` (4 files) | Created |
| `Project/src/MyBudget.Features/Features/Budgets/DeleteBudget/` (3 files) | Created |
| `Project/src/MyBudget.Features/Features/Budgets/RestoreBudget/` (3 files) | Created |
| `Project/tests/MyBudget.Features.Tests/SharedKernel/Entities/BudgetDomainTests.cs` | Created |
| `Project/tests/MyBudget.Features.Tests/Features/Budgets/CreateBudget/CreateBudgetValidatorTests.cs` | Created |
| `Project/tests/MyBudget.Features.Tests/Features/Budgets/RenameBudget/RenameBudgetValidatorTests.cs` | Created |
| `Project/tests/MyBudget.Features.Tests/SharedKernel/Auth/BudgetAuthorizationHandlerTests.cs` | Modified |
| `Project/tests/MyBudget.Integration.Tests/Features/Budgets/CreateBudgetTests.cs` | Created |
| `Project/tests/MyBudget.Integration.Tests/Features/Budgets/RenameBudgetTests.cs` | Created |
| `Project/tests/MyBudget.Integration.Tests/Features/Budgets/DeleteBudgetTests.cs` | Created |
| `Project/tests/MyBudget.Integration.Tests/Features/Budgets/RestoreBudgetTests.cs` | Created |
| `Project/tests/MyBudget.Integration.Tests/Features/Auth/LogoutAndMeTests.cs` | Modified |

### PR 2 — Frontend
| File | Action |
|------|--------|
| `Project/frontend/src/features/budget-structure/api/budgets.api.ts` | Created |
| `Project/frontend/src/stores/auth.store.ts` | Modified (added `isDeleted` to `BudgetMembershipDto`) |
| `Project/frontend/src/features/budget-structure/components/CreateBudgetModal.vue` | Created |
| `Project/frontend/src/features/budget-structure/views/BudgetSelectionView.vue` | Modified |
| `Project/frontend/src/router/index.ts` | Modified (added deleted-budget guard) |
| `Project/frontend/src/layouts/AppLayout.vue` | Modified (added `onMounted` + `useRoute`) |
| `Project/frontend/src/i18n/locales/en.json` | Modified |
| `Project/frontend/src/i18n/locales/es.json` | Modified |
| `Project/frontend/src/features/budget-structure/components/__tests__/CreateBudgetModal.spec.ts` | Created |
| `Project/frontend/src/features/budget-structure/views/__tests__/BudgetSelectionView.spec.ts` | Created |
| `openspec/changes/multi-budget/tasks.md` | Updated (checkboxes for 3.1–3.7, 4.10–4.11) |
| `openspec/changes/multi-budget/apply-progress.md` | Updated (merged all batches) |

## Notes / Deviations from Design

### PR 1 (Backend)
1. **BudgetAuthorizationHandler**: Added a second DB query to check `IsDeleted` when no role is returned. This ensures a soft-deleted budget still sets the `budget-not-found` flag.
2. **RestoreBudget**: Uses simple `FirstOrDefault(b => b.Id == cmd.BudgetId)` — no EF global query filter exists on `Budget`, so the query naturally returns both active and deleted budgets.
3. **BudgetMembershipDto breaking change**: Adding `IsDeleted` as 4th positional parameter required updating `GetCurrentUserHandler`. No other callers of this constructor exist.

### PR 2 (Frontend)
1. **Task numbering mismatch**: Prompt described task 3.2 as modifying `auth.store.ts` (addng `isDeleted` to interface), but tasks.md listed a separate task 3.2 for this. Both were completed as a single logical change — the interface change is trivial and the fetchMe() API response automatically includes the field from the backend.
2. **`budgets.api.ts` return types**: `createBudget` returns `{ id, name }` (backend returns `{ id, name }` from CreateBudgetEndpoint). `renameBudget` also returns `{ id, name }`. Both match the actual backend contract; the design's TypeScript interface showing `{ budgetId }` was a draft-level mismatch — the endpoint returns `{ id, name }`.
3. **i18n keys**: Added `budgetNameRequired` and `budgetNameTooLong` as additional keys needed by `CreateBudgetModal.vue` for client-side validation messages (not in the original 10-key list but required for the component).
4. **Delete button label**: Used `m.role === 'owner'` — the backend uses `BudgetRole.Owner` which maps to the string `"owner"` in the membership DTO.
