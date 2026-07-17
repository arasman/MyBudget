# Explore — multi-budget

**Change**: multi-budget
**Date**: 2026-07-16
**Status**: complete

## Goal

Enable users to own and belong to multiple budgets: create new budgets, rename them, switch between them in the UI, and optionally delete them.

## Key Findings

### 1. Auto-budget creation on registration

`RegisterUserHandler.cs` (step 4) calls:
```csharp
var budget = Budget.Create($"{cmd.FirstName.Trim()}'s Budget", user.Id);
_db.Budgets.Add(budget);
var membership = BudgetMembership.Create(budget.Id, user.Id, BudgetRole.Owner);
_db.BudgetMemberships.Add(membership);
await _db.SaveChangesAsync(ct);
```
Creates exactly one budget in the same transaction as user creation. No separate "create budget" hook exists.

### 2. BudgetAuthorizationHandler — budget resolution per request

- Reads `userId` from JWT `sub` claim.
- Reads `budgetId` from **route value `id`** (`httpContext.Request.RouteValues["id"]`).
- Queries `BudgetMemberships` via Dapper; caches in `IMemoryCache` with key `budget-membership:{userId}:{budgetId}` (TTL 5 min).
- If `role >= requirement.MinimumRole` → `context.Succeed()`.
- If budget not found → sets `httpContext.Items["budget-not-found"] = true` → middleware returns 404.
- **Critical**: `CreateBudget` (no `:id` in route) MUST NOT use a `budget:*` policy — handler extracts null budgetId and fails immediately.

### 3. BudgetMembership data model

```
BudgetMembership
  Id (Guid PK)
  BudgetId (FK → Budgets, cascade delete)
  UserId (FK → Users, restrict delete)
  Role (int, enum BudgetRole)
  JoinedAt (DateTime)
  UNIQUE(BudgetId, UserId)
```

`BudgetRole` enum: `ReadOnly=10, Operator=20, Admin=30, Owner=40`.

### 4. Frontend — how "active budget" is resolved

- `layoutStore.activeBudgetId` — in-memory Pinia, NOT persisted to localStorage. Resets on page reload.
- Set by `BudgetSelectionView.selectBudget()` or `AppLayout.switchBudget()`.
- Route URL contains `/budgets/:budgetId` — views read `useRoute().params.budgetId` for API calls.
- On page reload, `layoutStore.activeBudgetName` is null → AppLayout shows "MyBudget" instead of budget name. Minor UX gap.

### 5. Budget switcher in AppLayout

Fully implemented. Reads `authStore.user.memberships`, renders a dropdown with all memberships. `switchBudget(id, name)` calls `layoutStore.setActiveBudget()` and navigates to `/budgets/${budgetId}/cycles`. **Already works for multiple budgets — no changes needed here**, except label restoration on page reload.

### 6. Existing Budget CRUD endpoints

| Endpoint | Exists |
|---|---|
| GET memberships | via `/api/auth/me` (returns all memberships) |
| POST /api/budgets (create) | **NO** |
| PUT /api/budgets/:id (rename) | **NO** |
| DELETE /api/budgets/:id | **NO** |
| POST /api/budgets/:id/invite | YES |
| POST /api/auth/invitations/accept | YES |

### 7. Frontend routes

- `/` → `BudgetSelectionView` — lists all user memberships. Auto-redirects if `memberships.length === 1`.
- `/budgets/:budgetId/cycles` → `CycleListView`
- `/budgets/:budgetId/categories` → `CategoryTreeView`
- No `/budgets/new` or `/budgets/:budgetId/settings` route.

### 8. i18n

Single-file locale: `frontend/src/i18n/locales/en.json` and `es.json`. Existing relevant keys:
- `budgetStructure.selection.title`, `singleRedirect`, `noBudgets`, `selectBudget`

Missing keys needed:
- `budgetStructure.selection.createBudget` (button label)
- `budgetStructure.selection.createBudgetTitle` (modal title)
- `budgetStructure.selection.budgetNameLabel`
- `budgetStructure.selection.createSuccess`
- `budgetStructure.selection.deleteBudget`, `confirmDelete` (if delete included)

## Approach Options

| Option | Scope | Effort | Notes |
|---|---|---|---|
| A — Create only | Add `CreateBudget` slice + "New Budget" button on selection view | Low | No rename/delete. Sufficient to unblock multi-budget. |
| B — Create + Rename | A + `RenameBudget` slice + rename affordance in AppLayout dropdown | Medium | Rename requires cache eviction for all members. |
| C — Create + Rename + Delete (soft) | B + `DeleteBudget` soft-delete + confirm modal | High | Soft-delete avoids data loss. Requires `IsDeleted` flag on Budget. |
| D — Full CRUD (hard delete) | Create + Rename + hard-delete | Medium | Data loss risk; cascade behavior must be audited across all child entities. |

**Recommendation**: Option B — Create + Rename. Delete can be a follow-up change. Hard-delete is risky given cascading child entities (cycles, periods, categories, budget lines, executions).

## Affected Areas

### Backend — new slices
```
Features/Budgets/
  CreateBudget/   (POST /api/budgets — RequireAuthorization() only, no budget policy)
  RenameBudget/   (PUT /api/budgets/:id — budget:admin policy)
```

### Backend — no changes needed
- `RegisterUserHandler` — keep auto-budget creation.
- `BudgetAuthorizationHandler` — no changes needed.
- `GetCurrentUserHandler` — already returns all memberships.
- DB schema — no migration needed (Budget table already has Name, OwnerId).

### Frontend — changes
- `BudgetSelectionView.vue` — add "New Budget" button + `CreateBudgetModal.vue`.
- New API function: `budgets.api.ts` → `createBudget(name: string)`.
- After create: `authStore.fetchMe()` to refresh memberships → auto-navigate to new budget.
- AppLayout: restore `activeBudgetName` on page reload when route contains `budgetId` but store name is null.
- Optional: rename affordance in AppLayout dropdown.
- i18n keys in `en.json` + `es.json`.

### Cache
- `CreateBudget`: no cache eviction needed (new budget → no existing entry).
- `RenameBudget`: must evict `budget-membership:{userId}:{budgetId}` for all current members. Handler queries member userIds from `BudgetMemberships`, then `_cache.Remove(key)` per member.

## Constraints

1. `CreateBudget` MUST use `RequireAuthorization()` without a `budget:*` policy.
2. `Budget.OwnerId` must be set to caller's userId. `BudgetMembership` role must be `BudgetRole.Owner`.
3. Budget name max length is 200 chars (per `BudgetConfiguration`).
4. `BudgetSelectionView` auto-redirect on `memberships.length === 1` must be preserved.

## Files Affected

### Backend
- `Project/src/MyBudget.Features/Features/Budgets/CreateBudget/` (new slice)
- `Project/src/MyBudget.Features/Features/Budgets/RenameBudget/` (new slice)
- `Project/src/MyBudget.Features/SharedKernel/Entities/Budget.cs` — add `Rename(string name)` method
- Tests: `Project/tests/MyBudget.Features.Tests/Features/Budgets/` (new per slice)

### Frontend
- `Project/frontend/src/features/budget-structure/api/budgets.api.ts` (new)
- `Project/frontend/src/features/budget-structure/components/CreateBudgetModal.vue` (new)
- `Project/frontend/src/features/budget-structure/views/BudgetSelectionView.vue` (extend)
- `Project/frontend/src/layouts/AppLayout.vue` (activeBudgetName restore on reload)
- `Project/frontend/src/i18n/locales/en.json` + `es.json` (new keys)
- Tests: component test for CreateBudgetModal
