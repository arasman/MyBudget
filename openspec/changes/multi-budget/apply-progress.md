# Apply Progress — multi-budget PR 1 (Backend)

**Batch**: PR 1 — Backend only
**Mode**: Standard (TDD OFF)
**Date**: 2026-07-17

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

### Phase 4: Backend Tests

- [x] 4.1 — `BudgetDomainTests.cs` — 11 unit tests for `Rename`/`SoftDelete`/`Restore` domain methods
- [x] 4.2 — `CreateBudgetValidatorTests.cs` — 5 validator unit tests
- [x] 4.3 — `RenameBudgetValidatorTests.cs` — 5 validator unit tests
- [x] 4.4 — `BudgetAuthorizationHandlerTests.cs` extended — cache miss / no-write scenario
- [x] 4.5 — `CreateBudgetTests.cs` — 4 integration scenarios (201, 422 empty, 422 long, 401)
- [x] 4.6 — `RenameBudgetTests.cs` — 4 integration scenarios (200, 403 operator, 404, 422)
- [x] 4.7 — `DeleteBudgetTests.cs` — 4 integration scenarios (204, 403 admin, 404 already deleted, 404 not found)
- [x] 4.8 — `RestoreBudgetTests.cs` — 3 integration scenarios (200, 404 not deleted, 403 admin)
- [x] 4.9 — `LogoutAndMeTests.cs` extended — 2 scenarios for `isDeleted` field

## Remaining Tasks (PR 2 scope)

- [ ] 3.1–3.7 — Frontend slices (Vue 3 components, store, router, i18n)
- [ ] 4.10–4.11 — Frontend component tests
- [ ] 5.1–5.3 — Verification

## Test Results

Unit tests: **306 passed, 0 failed**
Integration tests: require live Postgres — not run in this batch (require Docker stack)

## Files Changed

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
| `openspec/changes/multi-budget/tasks.md` | Updated (checkboxes) |
| `openspec/changes/multi-budget/apply-progress.md` | Created |

## Notes / Deviations from Design

1. **BudgetAuthorizationHandler**: Added a second DB query to check `IsDeleted` when no role is returned. This ensures that a soft-deleted budget (where a membership exists but the JOIN on `IsDeleted=false` filters it out) still sets the `budget-not-found` flag. Could be optimized into a single combined query but preserves the existing separation of concerns.

2. **RestoreBudget**: Uses `FirstOrDefault(b => b.Id == cmd.BudgetId)` without `IgnoreQueryFilters()` because the `Budget` entity has **no global EF query filter** configured. The simple query returns both active and deleted budgets, which is the correct behavior for restore.

3. **BudgetMembershipDto breaking change**: Adding `IsDeleted` as the 4th positional parameter required updating `GetCurrentUserHandler`. No other callers of this constructor exist in the codebase.
