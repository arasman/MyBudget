# Proposal: Multi-Budget Management

## Intent

Users are locked into a single budget auto-created at registration. The data model already supports multiple budgets via `BudgetMembership`, but no endpoints or UI exist to create, rename, or remove additional budgets. This change adds Create, Rename, Soft-Delete, and Restore operations so users can manage multiple budgets and organize finances across different contexts (personal, household, project).

## Scope

### In Scope
- `CreateBudget` backend slice (POST /api/budgets, RequireAuthorization() only, no budget:* policy)
- `RenameBudget` backend slice (PUT /api/budgets/:id, budget:admin policy)
- `DeleteBudget` backend slice (DELETE /api/budgets/:id, Owner-only soft-delete)
- `RestoreBudget` backend slice (POST /api/budgets/:id/restore, Owner-only)
- EF migration: `Budget` entity gets `IsDeleted` (bool) + `DeletedAt` (DateTimeOffset?)
- `/api/auth/me` memberships: include deleted budgets with `isDeleted: true` flag
- `BudgetAuthorizationHandler`: soft-deleted budget returns 404
- Frontend: BudgetSelectionView "New Budget" button + CreateBudgetModal
- Frontend: BudgetSelectionView "show deleted" toggle + restore action
- Frontend: Navigation guard — redirect to `/` when budget is soft-deleted
- Frontend: AppLayout — restore activeBudgetName on reload from route budgetId
- i18n: ~8-10 new keys in en.json + es.json

### Out of Scope
- Hard delete / cascade delete
- Budget templates or cloning
- Transfer ownership
- Budget settings page

## Capabilities

### New Capabilities
- `budget-management`: Create, Rename, Soft-Delete, Restore operations on Budget entity

### Modified Capabilities
- `auth`: `/api/auth/me` response adds `isDeleted` flag to membership entries
- `budget-structure`: `BudgetAuthorizationHandler` treats soft-deleted budget as 404

## Approach

Four vertical slices following existing VSA patterns. `CreateBudget` uses `RequireAuthorization()` without budget policy (no budgetId in route). `DeleteBudget`/`RestoreBudget` restricted to `BudgetRole.Owner` (40). Soft-delete is a flag on Budget only — no cascade to children. `BudgetAuthorizationHandler` adds an `IsDeleted` check before membership lookup, returning 404 for deleted budgets. Frontend reads memberships from `/api/auth/me` (already available) and adds UI controls to BudgetSelectionView.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `Features/Budgets/CreateBudget/` | New | 4-file slice: command, validator, handler, endpoint |
| `Features/Budgets/RenameBudget/` | New | 4-file slice |
| `Features/Budgets/DeleteBudget/` | New | 4-file slice, Owner-only |
| `Features/Budgets/RestoreBudget/` | New | 4-file slice, Owner-only |
| `SharedKernel/Entities/Budget.cs` | Modified | Add `IsDeleted`, `DeletedAt`, `Rename()`, `SoftDelete()`, `Restore()` |
| `Features/Auth/GetCurrentUser/` | Modified | Include `isDeleted` in membership DTO |
| `Features/Budgets/Authorization/` | Modified | 404 on soft-deleted budget |
| `frontend/src/features/budget-structure/views/BudgetSelectionView.vue` | Modified | New budget button, deleted toggle, restore |
| `frontend/src/features/budget-structure/components/CreateBudgetModal.vue` | New | Modal with name input |
| `frontend/src/features/budget-structure/api/budgets.api.ts` | New | API functions for CRUD |
| `frontend/src/layouts/AppLayout.vue` | Modified | Restore activeBudgetName on reload |
| `frontend/src/router/index.ts` | Modified | Navigation guard for deleted budgets |
| `frontend/src/i18n/locales/en.json` | Modified | ~8-10 new keys |
| `frontend/src/i18n/locales/es.json` | Modified | ~8-10 new keys |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Cache stale after soft-delete | Medium | Evict `budget-membership:*` entries for all members on delete/restore |
| Owner deletes only budget | Low | Frontend warns; backend allows (user sees empty BudgetSelectionView) |
| Race between delete and active operations | Low | Authz handler checks IsDeleted before membership; concurrent requests get 404 |

## Rollback Plan

Revert migration (drop `IsDeleted`/`DeletedAt` columns from Budget). Remove four slices. Revert frontend components. No data loss — soft-delete flag is additive.

## Dependencies

- None. All prerequisite infrastructure (BudgetMembership, BudgetAuthorizationHandler, BudgetSelectionView) already exists.

## Success Criteria

- [ ] Users can create new budgets and immediately switch to them
- [ ] Budget name can be renamed by admin+ members
- [ ] Owner can soft-delete a budget; all members lose access (404)
- [ ] Owner can restore a soft-deleted budget from BudgetSelectionView
- [ ] Deleted budgets appear in `/api/auth/me` with `isDeleted: true`
- [ ] Navigation to a deleted budget's routes redirects to `/`
- [ ] activeBudgetName survives page reload
