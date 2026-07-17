# Proposal: soft-delete-ux

## Intent

Soft-delete, restore, and feedback UX is inconsistent across entities. Budget has full soft-delete UX (confirm, toggle, restore); Cycle/Period/CategoryGroup/Category/BudgetLine lack show-deleted toggles, restore actions, and success feedback. ExecutionRecord delete has no confirmation at all. No ephemeral toast system exists -- the notification bell is a persistent inbox, unsuitable for transient action feedback.

This change unifies the soft-delete/restore UX across all entities and adds an ephemeral toast layer.

## Scope

### In Scope
- Ephemeral toast composable (`useToast`) + `AppToast.vue` component (DaisyUI toast+alert classes, stacking, 3s auto-dismiss, close button)
- Show-deleted toggle for Cycle, Period, CategoryGroup, Category, BudgetLine list views (session-persisted via Pinia, default OFF)
- Restore actions for Cycle, Period, CategoryGroup, Category, BudgetLine in their list views
- Cascade disclosure warning on Period restore (child BudgetLines restored)
- Two-step delete confirmation for ExecutionRecord (MatrixLineRow pattern); fallback to nested `<dialog>` if UX feels wrong post-implementation
- Success toasts on delete/restore for all entities
- i18n keys (EN+ES): `deleteSuccess`, `restoreSuccess`, `showDeleted` for all entity namespaces (~20 keys)
- Backend: `includeDeleted` query param on `GET /cycles` (currently hardcoded `WHERE DeletedAt IS NULL`)
- Backend: `RestorePeriod` endpoint (`POST /periods/{id}/restore`)

### Out of Scope
- Bell notification store changes (toasts are ephemeral-only, not stored)
- Undo/redo pattern (future enhancement)
- Bulk delete/restore
- Hard-delete functionality
- Cycle restore cascade to ExecutionRecords (already exists via `?includeExecutionRecords` param)

## Capabilities

### New Capabilities
- `ephemeral-toast`: Auto-dismiss toast overlay system (composable + root component)

### Modified Capabilities
- `budget-structure-ui`: Add show-deleted toggles, restore actions, cascade warnings, success toasts to Cycle/Period/CategoryGroup/Category/BudgetLine views
- `budget-execution`: Add two-step delete confirmation for ExecutionRecord; success toasts on delete/restore
- `budget-structure`: Add `RestorePeriod` endpoint; add `includeDeleted` flag to `ListCycles`

## Product Decisions

| # | Decision | Detail |
|---|----------|--------|
| 1 | Toast stacking | All visible simultaneously, 3s auto-dismiss, each has close button |
| 2 | Show-deleted persistence | Session-only (Pinia state), defaults OFF on first load |
| 3 | Period restore cascade | Disclosure warning that child BudgetLines will also be restored |
| 4 | ExecutionRecord confirm | Two-step button (MatrixLineRow pattern); review post-implementation, fallback to nested `<dialog>` |
| 5 | Bell exclusion | Auto-dismiss toasts NOT stored in notification store; bell stays for persistent notifications only |

## Approach

Three chained PRs to stay within the 400-line review budget:

**PR 1 -- Toast infrastructure + i18n foundation**
- New `useToast` composable (separate from notification store) backed by a lightweight Pinia store or module-level ref
- `AppToast.vue` mounted in `AppLayout.vue`, renders stacking toasts at bottom-right using DaisyUI `toast` + `alert` classes
- Wire success toasts on existing Budget delete/restore flows as proof-of-concept
- Add all missing i18n keys for delete/restore/showDeleted across entity namespaces

**PR 2 -- Structure entity soft-delete UX (Cycle, Period, CategoryGroup, Category, BudgetLine)**
- Backend: add `includeDeleted` query param to `ListCyclesHandler`; add `RestorePeriod` endpoint
- Frontend: show-deleted toggles + restore actions in CycleListView, CycleDetailView, CategoryTreeView, BudgetLinesView
- Period restore cascade disclosure warning
- Success toasts on all delete/restore actions

**PR 3 -- ExecutionRecord confirmation + cleanup**
- Two-step delete confirmation in `ExecutionRecordRow` (following MatrixLineRow two-step pattern)
- Success toasts on ExecutionRecord delete/restore
- Post-implementation UX review checkpoint: confirm two-step vs. nested `<dialog>` decision

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `frontend/src/composables/useToast.ts` | New | Ephemeral toast composable |
| `frontend/src/components/AppToast.vue` | New | Toast overlay component |
| `frontend/src/components/AppLayout.vue` | Modified | Mount AppToast |
| `frontend/src/features/budget-structure/views/CycleListView.vue` | Modified | Show-deleted toggle + restore |
| `frontend/src/features/budget-structure/views/CycleDetailView.vue` | Modified | Show-deleted toggle + Period restore |
| `frontend/src/features/budget-structure/views/CategoryTreeView.vue` | Modified | Show-deleted toggle + restore |
| `frontend/src/features/budget-structure/views/BudgetLinesView.vue` | Modified | Show-deleted toggle + restore |
| `frontend/src/features/budget-execution/components/ExecutionRecordRow.vue` | Modified | Two-step delete |
| `frontend/src/features/budget-structure/api/cycles.api.ts` | Modified | includeDeleted param + restore |
| `frontend/src/features/budget-structure/api/periods.api.ts` | Modified | restore function |
| `frontend/src/features/budget-structure/store.ts` | Modified | Toggle state, restore actions |
| `frontend/src/i18n/locales/en.json` | Modified | ~20 new keys |
| `frontend/src/i18n/locales/es.json` | Modified | ~20 new keys |
| `src/MyBudget.Features/.../ListCyclesHandler.cs` | Modified | includeDeleted SQL branch |
| `src/MyBudget.Features/.../RestorePeriod/` | New | 4-file VSA slice |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Period restore backend gap blocks PR 2 | High | Build RestorePeriod endpoint as first task in PR 2 |
| Two-step pattern feels wrong for ExecutionRecord context | Med | Decision 4 reserves fallback to nested `<dialog>`; review after implementation |
| Cycles `includeDeleted` requires Dapper SQL change | Low | Simple conditional WHERE clause; covered by integration tests |
| Toast z-index conflicts with existing modals | Low | Use DaisyUI's built-in toast positioning; test with open modals |

## Rollback Plan

Each PR is independently revertable. PR 1 (toast infra) is additive-only. PR 2/3 can be reverted without losing toast infrastructure. No database migrations involved -- all changes are frontend + query-level backend.

## Dependencies

- No external library additions (DaisyUI toast classes already available)
- RestorePeriod backend endpoint must land before Period restore UI in PR 2

## Success Criteria

- [ ] All entities (Budget, Cycle, Period, CategoryGroup, Category, BudgetLine, ExecutionRecord) show success toast on delete and restore
- [ ] All list views with soft-deletable items have a show-deleted toggle (session-persisted, default OFF)
- [ ] Restore action available for all soft-deleted entities in their respective views
- [ ] Period restore shows cascade disclosure warning
- [ ] ExecutionRecord delete requires two-step confirmation
- [ ] Toasts stack, auto-dismiss at 3s, and have close buttons
- [ ] Toasts do NOT appear in the bell notification dropdown
- [ ] i18n keys present in both EN and ES for all new user-facing strings
- [ ] No security impact -- all restore/delete endpoints already role-gated
