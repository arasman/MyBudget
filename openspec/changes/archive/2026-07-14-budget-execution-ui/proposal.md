# Proposal: Budget Execution Matrix UI

## Intent

Users currently manage budget execution through individual maintenance views that show one period at a time. The matrix view delivers the main operational experience: a multi-period spreadsheet showing Real (budgeted) vs Ejecutado (actual) for all budget lines across the active cycle, matching the original Excel model. Without it, cross-period comparison requires manual navigation between periods.

## Scope

### In Scope
- New route `cycles/:cycleId/matrix` with `BudgetMatrixView`
- `useBudgetMatrixStore` (independent Pinia store)
- 3 composables: `useMatrixNavigation`, `usePeriodData`, `useCurrencyDisplay`
- 13 new Vue components under `src/features/budget-execution/`
- `BudgetTabs` extended with "Matrix" tab
- `ExecutionListModal` + `ExecutionRecordForm` for inline CRUD
- Line reordering: up/down arrows AND drag-and-drop (`vue-draggable-plus`)
- Structural inserts reuse existing `CategoryForm.vue` / `CategoryGroupForm.vue`
- i18n: `budgetMatrix.*` + `budgetExecution.*` namespaces (EN + ES)
- Vitest unit/component tests; 8 Playwright E2E specs
- 3-period visible window, progressive per-period loading

### Out of Scope
- Existing maintenance views (CycleListView, CycleDetailView, CategoryTreeView, BudgetLinesView) — unchanged
- Redis caching for closed periods (backend concern)
- Mobile responsiveness (desktop-only tool)
- `AccountId` / `PaymentMethodId` fields on ExecutionRecordForm (deferred to `current-situation`)
- Export/print (deferred to `import-export`, MVP B)

## Capabilities

### New Capabilities
- `budget-execution-ui`: Multi-period budget matrix view with execution CRUD, period navigation, currency toggle, include-deleted toggle, and summary rows

### Modified Capabilities
- `budget-structure-ui`: BudgetTabs gains a "Matrix" tab (requires `cycleId` prop)

## Approach

- Feature folder `src/features/budget-execution/` mirroring `budget-structure/`
- Independent `useBudgetMatrixStore` — does NOT extend `useBudgetStructureStore`
- Sticky left column (CSS `position: sticky`) + horizontal overflow for period columns
- Per-period progressive loading via `loadVisiblePeriods()`; skeleton per column
- Currency toggle is client-side conversion using `currentCycle.exchangeRate`
- Write operations respect `period.IsClosed` — closed cells are read-only
- Structural inserts (line/category/group) reuse existing modals from `budget-structure/`

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/features/budget-execution/` | New | Feature folder: types, api, store, composables, 13 components, view |
| `src/features/budget-structure/components/BudgetTabs.vue` | Modified | Add "Matrix" tab with optional `cycleId` prop |
| `src/router/index.ts` | Modified | Add `cycles/:cycleId/matrix` route |
| `src/i18n/locales/{en,es}.json` | Modified | Add `budgetMatrix.*` and `budgetExecution.*` namespaces |
| `e2e/budget-matrix/` | New | 8 Playwright spec files + helpers |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Sticky left column CSS conflicts with daisyUI table | High | Prototype in PR3; custom `<table>` structure if needed |
| "Incluir eliminados" toggle requires coordinated re-fetch | Med | `setShowDeleted()` clears `periodTotals` and calls `loadVisiblePeriods()` |
| Largest frontend change (~1,950 lines / 6 PRs) | Med | Feature-branch-chain; each PR under 400 lines |

## Rollback Plan

Revert the feature branch merge. All changes are additive (new feature folder + route + tab). Existing maintenance views are untouched, so reverting removes only the matrix without side effects.

## Dependencies

- Backend `budget-execution` endpoints (confirmed, already implemented and tested)
- `vue-draggable-plus` (already installed)
- `lucide-vue-next` icons (already installed)

## Success Criteria

- [ ] Matrix view renders all groups/categories/lines with Real + Ejecutado per visible period
- [ ] Period navigation shows 3 periods at a time with prev/next controls
- [ ] Double-click on Ejecutado cell opens execution modal; CRUD operations update totals
- [ ] Currency toggle converts all amounts using cycle exchange rate
- [ ] Closed-period cells are read-only; write attempts blocked in UI
- [ ] "Incluir eliminados" toggle shows/hides soft-deleted items
- [ ] All Vitest unit/component tests pass; all 8 Playwright E2E specs pass
