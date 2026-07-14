# Tasks: Budget Execution Matrix UI

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~1,950 (6 PRs, ~325 avg) |
| 400-line budget risk | Low per PR (confirmed by user) |
| Chained PRs recommended | Yes |
| Suggested split | PR1 → PR2 → PR3 → PR4 → PR5 → PR6 |
| Delivery strategy | auto-chain |
| Chain strategy | feature-branch-chain |

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: feature-branch-chain
400-line budget risk: Low

### Suggested Work Units

| Unit | Goal | Likely PR | Base Branch |
|------|------|-----------|-------------|
| 1 | API layer types + functions | PR1 | `feat/budget-matrix-api` ← main |
| 2 | Store + composables | PR2 | `feat/budget-matrix-store` ← PR1 |
| 3 | Skeleton view + routing | PR3 | `feat/budget-matrix-skeleton` ← PR2 |
| 4 | Execution modal CRUD | PR4 | `feat/budget-matrix-execution` ← PR3 |
| 5 | Summary rows + controls + full i18n | PR5 | `feat/budget-matrix-summary` ← PR4 |
| 6 | Playwright E2E specs | PR6 | `feat/budget-matrix-e2e` ← PR5 |

---

## PR1 — API Layer (`feat/budget-matrix-api`)

- [x] T-1.1: Create `src/features/budget-execution/types.ts` — define `ExecutionRecordDto`, `PeriodTotalsDto`, `LineTotalDto`, `CategoryTotalDto`, `EntryType` as per design interfaces/contracts. _(REQ-MATRIX-EXEC, REQ-MATRIX-TOTALS)_
  - Acceptance: File compiles; all 5 types exported; `entryType` typed as `number` (1/2/3).

- [x] T-1.2: Create `src/features/budget-execution/api/executions.api.ts` — export `list`, `create`, `update`, `remove`, `restore` using the Axios instance at `src/api/axios.ts`. Routes: `PUT /api/budgets/{id}/periods/{periodId}/budget-lines/{lineId}/execution-records/{execId}` etc. _(REQ-MATRIX-EXEC)_
  - Acceptance: 5 exported async functions; all use `budgetId` mapped to `{id}` param.

- [x] T-1.3: Create `src/features/budget-execution/api/executionTotals.api.ts` — export `getPeriodTotals(budgetId, periodId): Promise<PeriodTotalsDto>`. Route: `GET /api/budgets/{id}/periods/{periodId}/execution-totals`. _(REQ-MATRIX-TOTALS, REQ-MATRIX-REFRESH)_
  - Acceptance: Single exported function; param named `id` in URL.

- [x] T-1.4: Write Vitest unit tests for `executions.api.ts` — mock `axios.ts`, verify 5 functions build correct URLs and forward payloads. _(Test Coverage Requirements)_
  - Acceptance: Tests pass; `vi.hoisted()` pattern used; ≥5 test cases.

- [x] T-1.5: Write Vitest unit tests for `executionTotals.api.ts` — mock `axios.ts`, verify URL construction. _(Test Coverage Requirements)_
  - Acceptance: Tests pass; ≥2 test cases (success + error path).

---

## PR2 — Store + Composables (`feat/budget-matrix-store`)

- [ ] T-2.1: Create `src/features/budget-execution/store.ts` — `useBudgetMatrixStore` with state shape from AD-2 (all `Record<string, T>` state, no Maps). Implement `initMatrix`, `loadVisiblePeriods`, `loadPeriodTotals`, `navigatePrev`, `navigateNext`, `openExecutionModal`, `closeExecutionModal`, `setDisplayCurrency`, `setShowDeleted`, `refreshPeriod`. _(REQ-MATRIX-NAV, REQ-MATRIX-EXEC, REQ-MATRIX-CURRENCY, REQ-MATRIX-DELETED)_
  - Acceptance: Store compiles; `collapsedGroupIds` / `collapsedCategoryIds` use `ref(new Set())`; `periodTotals` and `executionRecords` use `Record<string, T>`.

- [ ] T-2.2: Create `src/features/budget-execution/composables/useMatrixNavigation.ts` — `visiblePeriods` (3-window slice), `canGoPrev`, `canGoNext`, `goPrev`, `goNext`. Boundary: offset clamped to `[0, allPeriods.length - 3]`. _(REQ-MATRIX-NAV)_
  - Acceptance: Edge cases covered (fewer than 3 periods; exactly 3; offset at 0 and max).

- [ ] T-2.3: Create `src/features/budget-execution/composables/usePeriodData.ts` — `loadPeriodTotals(periodId)` sets `loadingPeriods[periodId]`, calls `executionTotals.api.ts`, stores result in `periodTotals`; `loadVisiblePeriods(periodIds)` fires 3 parallel calls via `Promise.all`. _(REQ-MATRIX-NAV, REQ-MATRIX-REFRESH)_
  - Acceptance: Parallel loading; each period independently transitions from loading to data.

- [ ] T-2.4: Create `src/features/budget-execution/composables/useCurrencyDisplay.ts` — `convert(amount, mode)`: when `alternate` and `exchangeRate` is truthy → `Math.round(amount / exchangeRate * 100) / 100`; when `default` → returns `amount`. `formatAmount(amount, currencyCode)` wraps `Intl.NumberFormat`. _(REQ-MATRIX-CURRENCY)_
  - Acceptance: `convert(750, 'alternate')` with rate 7.5 → 100.00; back-toggle returns original.

- [ ] T-2.5: Vitest tests for `useBudgetMatrixStore` — test `initMatrix` populates `cycleId`; `navigatePrev/Next` clamps correctly; `setDisplayCurrency` toggles state; `setShowDeleted` toggles state; `openExecutionModal` sets modal IDs. _(Test Coverage Requirements)_
  - Acceptance: `createPinia()` setup; `vi.mock` api modules; ≥8 test cases.

- [ ] T-2.6: Vitest tests for `useMatrixNavigation` — test offset clamping at 0, at max, with fewer than 3 periods. _(Test Coverage Requirements)_
  - Acceptance: ≥4 test cases; no external deps needed.

- [ ] T-2.7: Vitest tests for `useCurrencyDisplay` — test `convert` with null exchangeRate (no-op), with valid rate both directions, rounding precision. _(Test Coverage Requirements)_
  - Acceptance: ≥4 test cases.

---

## PR3 — Skeleton View + Routing (`feat/budget-matrix-skeleton`)

- [ ] T-3.1: Modify `src/router/index.ts` — add route `/budgets/:id/cycles/:cycleId/matrix` named `BudgetMatrix` mapped to lazy-import `BudgetMatrixView`. Add navigation guard: if no `cycleId`, redirect to `CycleListView`. _(REQ-MATRIX-ROUTE)_
  - Acceptance: Route resolves; missing `cycleId` redirects; param name is `id` (not `budgetId`).

- [ ] T-3.2: Modify `src/features/budget-structure/components/BudgetTabs.vue` — add optional `cycleId?: string` prop; add Matrix tab with `v-if="cycleId"` linking to `{ name: 'BudgetMatrix', params: { id: budgetId, cycleId } }`; Matrix tab has its own active state (not grouped with CYCLE_ROUTE_NAMES). _(REQ-MATRIX-ROUTE, Delta BudgetTabs)_
  - Acceptance: Matrix tab visible only when `cycleId` is truthy; active class applied on `BudgetMatrix` route.

- [ ] T-3.3: Create `src/features/budget-execution/views/BudgetMatrixView.vue` — `onMounted`: load cycle + groups via `budgetStructureStore`, then call `budgetMatrixStore.initMatrix(budgetId, cycleId)`. Render: `MatrixControls`, period nav buttons (prev/next), `<table>` wrapper with sticky overflow, `MatrixPeriodHeader`, group rows loop, `EmptyState` when no groups. 403 response → redirect to `BudgetSelectionView`. _(REQ-MATRIX-NAV, REQ-MATRIX-STRUCT, REQ-MATRIX-RBAC)_
  - Acceptance: View mounts and calls initMatrix; 403 redirects; empty state renders.

- [ ] T-3.4: Create `src/features/budget-execution/components/MatrixPeriodHeader.vue` — renders one `<tr>` with sticky label `<th>` and 3×2 period sub-columns (Presupuesto / Ejecutado). Shows skeleton when `loadingPeriods[periodId]` is true. _(REQ-MATRIX-NAV, REQ-MATRIX-STRUCT)_
  - Acceptance: 3 visible period headers; skeleton per period; sticky th has `position: sticky; left: 0`.

- [ ] T-3.5: Create `src/features/budget-execution/components/MatrixGroupRow.vue` — `<tr>` with sticky name cell, collapse/expand toggle, up/down arrows (call `reorderGroups`), per-period aggregated Real + Ejecutado totals. Emits `toggle-collapse`. _(REQ-MATRIX-STRUCT, REQ-MATRIX-REORDER)_
  - Acceptance: Collapse hides child rows; up/down arrows disabled at first/last position.

- [ ] T-3.6: Create `src/features/budget-execution/components/MatrixCategoryRow.vue` — `<tr>` with sticky name cell, collapse/expand toggle, up/down arrows (call `reorderCategories`), per-period aggregated totals. Renders `vue-draggable-plus` wrapper for its lines. _(REQ-MATRIX-STRUCT, REQ-MATRIX-REORDER)_
  - Acceptance: `@end` drag handler calls `reorderLines` with new order; arrows disabled at boundaries.

- [ ] T-3.7: Create `src/features/budget-execution/components/MatrixLineRow.vue` — `<tr>` with sticky name cell, up/down arrows, per-period `MatrixCell` pairs (Real / Ejecutado). Ejecutado cell emits `dblclick` → `openExecutionModal`. _(REQ-MATRIX-STRUCT, REQ-MATRIX-EXEC)_
  - Acceptance: `MatrixCell` dblclick on Ejecutado cell triggers modal open.

- [ ] T-3.8: Create `src/features/budget-execution/components/MatrixEstimatedRow.vue` — variance sub-row: `Estimado - Real` and `Real - Total Ejecutado` per visible period. _(REQ-MATRIX-TOTALS)_
  - Acceptance: Values are computed from store data; row renders below its parent `MatrixLineRow`.

- [ ] T-3.9: Create `src/features/budget-execution/components/MatrixCell.vue` — renders formatted amount (uses `useCurrencyDisplay.convert()`); emits `dblclick` when `@dblclick` fires; shows skeleton when `loading` prop is true; applies gray styling when `deleted` prop is true. _(REQ-MATRIX-EXEC, REQ-MATRIX-DELETED)_
  - Acceptance: `dblclick` emit verifiable; skeleton shown when loading; currency conversion applied.

- [ ] T-3.10: Add i18n skeleton keys only — `src/i18n/locales/en.json` + `es.json`: add `budgetMatrix` namespace with keys for nav labels, column headers (Presupuesto, Ejecutado), empty state. Spanish values may be placeholders marked `TODO`. Full translations deferred to PR5. _(i18n Requirements)_
  - Acceptance: All component `t('budgetMatrix.*')` keys exist; app does not throw missing-key warnings.

---

## PR4 — Execution Modal + CRUD (`feat/budget-matrix-execution`)

- [ ] T-4.1: Create `src/features/budget-execution/components/ExecutionListModal.vue` — daisyUI `<dialog>` modal; reads `store.executionRecords[key]` where `key = ${lineId}:${periodId}`; shows list of `ExecutionRecordRow`; shows `ExecutionRecordForm` only when `period.status !== 'Closed'`; fetches records via `store.openExecutionModal()` on open. _(REQ-MATRIX-EXEC)_
  - Acceptance: Form hidden when period closed; records listed in `createdAt` asc order.

- [ ] T-4.2: Create `src/features/budget-execution/components/ExecutionRecordRow.vue` — renders one record: `entryType`, `amount`, `note`; shows Edit + Delete buttons (operator); shows Restore button for deleted records; deleted records render in gray; read-only when period closed or `budget:read` role. _(REQ-MATRIX-EXEC, REQ-MATRIX-RBAC)_
  - Acceptance: No Edit/Delete visible for `budget:read` role; Restore visible only on deleted record.

- [ ] T-4.3: Create `src/features/budget-execution/components/ExecutionRecordForm.vue` — create/edit form: `EntryType` select (Expense/CreditNote/DebitNote), `Amount` number input (positive), `Note` text input. Validation: Note required when type is CreditNote or DebitNote. On submit: call `executions.api.create/update` → update `executionRecords` cache → delete `periodTotals[periodId]` → call `loadPeriodTotals(periodId)`. _(REQ-MATRIX-EXEC)_
  - Acceptance: Note validation error blocks submit; success refreshes Ejecutado cell total.

- [ ] T-4.4: Create `src/features/budget-execution/components/MatrixRefreshIcon.vue` — shows refresh icon (lucide `RefreshCw`) in period column header only when `period.status === 'Closed'`; on click calls `store.refreshPeriod(periodId)`; shows spinner while `loadingPeriods[periodId]` is true. _(REQ-MATRIX-REFRESH)_
  - Acceptance: Icon absent on open periods; spinner shows during fetch; amounts update after.

- [ ] T-4.5: Wire double-click in `MatrixLineRow.vue` → `store.openExecutionModal(lineId, periodId)` and wire `ExecutionListModal` visibility to `store.openModalLineId !== null`. _(REQ-MATRIX-EXEC)_
  - Acceptance: Double-click on Ejecutado cell opens modal with correct lineId + periodId.

- [ ] T-4.6: Implement line reorder API calls in `MatrixCategoryRow.vue` — up/down arrows and `vue-draggable-plus` `@end` handler call `PUT /api/budgets/{id}/periods/{periodId}/budget-lines/order` with `{ orderedIds: Guid[] }` for EACH visible period that has lines in that category (N calls, one per period). Optimistic update: revert on error. _(REQ-MATRIX-REORDER)_
  - Acceptance: Reorder calls fired for all visible periods; local order reverted on API error.

- [ ] T-4.7: Vitest component tests for `MatrixCell.vue` — test dblclick emits event; test skeleton shows when loading prop true; test gray class when deleted prop true. _(Test Coverage Requirements)_
  - Acceptance: ≥3 test cases using `@testing-library/vue`.

- [ ] T-4.8: Vitest component tests for `ExecutionRecordForm.vue` — test Note validation for CreditNote (error shown); test Note not required for Expense; test submit calls store action. _(Test Coverage Requirements)_
  - Acceptance: ≥4 test cases; store mocked via `createPinia()`.

- [ ] T-4.9: Vitest component tests for `ExecutionListModal.vue` — test form hidden when period is closed; test form visible when period open; test records displayed in order. _(Test Coverage Requirements)_
  - Acceptance: ≥3 test cases; period status passed via store `$patch`.

---

## PR5 — Summary Rows + Controls + Full i18n (`feat/budget-matrix-summary`)

- [ ] T-5.1: Create `src/features/budget-execution/components/MatrixSummaryRow.vue` — one row per `LineType` (Expense=red, LongTermSavings=green, PreventiveSavings=orange); shows Total Estimado | Total Real | Total Ejecutado per visible period; values computed from `periodTotals` + `categoryTotals`. _(REQ-MATRIX-TOTALS)_
  - Acceptance: 3 rows rendered; color classes applied (`text-error`, `text-success`, `text-warning`).

- [ ] T-5.2: Create `src/features/budget-execution/components/MatrixControls.vue` — renders: cycle name, exchange rate display (when alternate currency available), GTQ/USD toggle buttons (alternate disabled when `cycle.alternateCurrencyId` is null), "Incluir eliminados" checkbox. Calls `store.setDisplayCurrency()` and `store.setShowDeleted()`. _(REQ-MATRIX-CURRENCY, REQ-MATRIX-DELETED)_
  - Acceptance: USD toggle disabled when no `alternateCurrencyId`; exchange rate label visible when set.

- [ ] T-5.3: Implement closed-period guard in `ExecutionListModal.vue` — verify `period.status === 'Closed'` hides form AND marks all `ExecutionRecordRow` instances as read-only (no edit/delete). _(REQ-MATRIX-EXEC)_
  - Acceptance: Closed period: form absent; no Edit/Delete buttons in any row.

- [ ] T-5.4: Complete i18n `src/i18n/locales/en.json` — fill all `budgetMatrix.*` keys (nav, headers, empty state, reorder labels, refresh, summary row labels) and all `budgetExecution.*` keys (modal title, form labels, entry type options, validation messages). _(i18n Requirements)_
  - Acceptance: Zero missing-key console warnings; all matrix text comes from i18n.

- [ ] T-5.5: Complete i18n `src/i18n/locales/es.json` — same key set as EN with proper Spanish translations (neutral/professional). _(i18n Requirements)_
  - Acceptance: Locale switch EN → ES renders all labels in Spanish.

- [ ] T-5.6: Vitest component tests for `MatrixSummaryRow.vue` — test Expense row applies red color class; test totals computed correctly from mocked `periodTotals` data; test zero amounts display correctly. _(Test Coverage Requirements)_
  - Acceptance: ≥3 test cases.

---

## PR6 — Playwright E2E (`feat/budget-matrix-e2e`)

- [ ] T-6.1: Create `e2e/budget-matrix/helpers.ts` — seed helper: `seedBudgetMatrixFixture(page)` that calls backend API to create budget + cycle + groups + categories + lines + open periods. Auth helper reuses existing login pattern. _(Test Coverage Requirements)_
  - Acceptance: Helper usable in all 8 specs; idempotent if run twice.

- [ ] T-6.2: Create `e2e/budget-matrix/navigation.spec.ts` — test: initial load shows 3 periods; next period shifts window; prev disabled at start; next disabled at end. _(REQ-MATRIX-NAV)_
  - Acceptance: 4 test cases; buttons checked for disabled state via `toBeDisabled()`.

- [ ] T-6.3: Create `e2e/budget-matrix/collapse.spec.ts` — test: expand/collapse group hides/shows category rows; collapse category hides line rows. _(REQ-MATRIX-STRUCT)_
  - Acceptance: 2 test cases; row visibility checked via `toBeVisible()` / `toBeHidden()`.

- [ ] T-6.4: Create `e2e/budget-matrix/execution-crud.spec.ts` — test: double-click Ejecutado cell opens modal; create Expense record → total updates; delete record → total updates. _(REQ-MATRIX-EXEC, REQ-MATRIX-TOTALS)_
  - Acceptance: 3 test cases; cell amount before/after compared numerically.

- [ ] T-6.5: Create `e2e/budget-matrix/note-validation.spec.ts` — test: CreditNote submit without note shows validation error; Expense submit without note succeeds. _(REQ-MATRIX-EXEC)_
  - Acceptance: 2 test cases; error message text matches i18n key output.

- [ ] T-6.6: Create `e2e/budget-matrix/currency-toggle.spec.ts` — test: default loads GTQ; toggle to USD converts amounts; exchange rate label visible; toggle back restores GTQ. _(REQ-MATRIX-CURRENCY)_
  - Acceptance: 3 test cases; amounts compared before/after toggle using `page.textContent()`.

- [ ] T-6.7: Create `e2e/budget-matrix/include-deleted.spec.ts` — test: deleted group hidden by default; check "Incluir eliminados" shows group in gray; uncheck hides it again. _(REQ-MATRIX-DELETED)_
  - Acceptance: 2 test cases; gray styling verified via class assertion.

- [ ] T-6.8: Create `e2e/budget-matrix/closed-period.spec.ts` — test: closed period column shows refresh icon; open period does not; double-click closed Ejecutado cell → modal opens read-only (no form). _(REQ-MATRIX-EXEC, REQ-MATRIX-REFRESH)_
  - Acceptance: 3 test cases.

- [ ] T-6.9: Create `e2e/budget-matrix/rbac.spec.ts` — test: `budget:read` user sees no CRUD controls in modal; `budget:operator` sees form; non-member navigating to matrix route gets redirected. _(REQ-MATRIX-RBAC)_
  - Acceptance: 3 test cases; each seeds a different user role.

---

## Dependency Graph

```
T-1.1 ──► T-1.2, T-1.3
T-1.2 ──► T-1.4
T-1.3 ──► T-1.5
T-1.1, T-1.2, T-1.3 ──► [PR2 start]

T-2.2, T-2.3, T-2.4 ──► T-2.1 (store uses composables)
T-2.1 ──► T-2.5
T-2.2 ──► T-2.6
T-2.4 ──► T-2.7
[PR2 complete] ──► [PR3 start]

T-3.1, T-3.2 can run in parallel (router + BudgetTabs are independent)
T-3.3 depends on T-3.1
T-3.4..T-3.9 depend on T-3.3 (all inside BudgetMatrixView)
T-3.9 (MatrixCell) is a dependency for T-3.7 (MatrixLineRow uses it)
T-3.10 (i18n skeleton) can run in parallel with T-3.4..T-3.9
[PR3 complete] ──► [PR4 start]

T-4.1 depends on T-4.3, T-4.2 (modal contains form + rows)
T-4.5 depends on T-4.1
T-4.6 is independent (wires existing MatrixCategoryRow)
T-4.7 depends on T-3.9 (MatrixCell exists)
T-4.8 depends on T-4.3
T-4.9 depends on T-4.1
[PR4 complete] ──► [PR5 start]

T-5.1, T-5.2 independent of each other
T-5.3 is a hardening of T-4.1 (no new file)
T-5.4, T-5.5 depend on all components being final (PR4 complete)
T-5.6 depends on T-5.1
[PR5 complete] ──► [PR6 start]

T-6.1 (helpers) must complete before T-6.2..T-6.9
T-6.2..T-6.9 can run in parallel once T-6.1 is done
```

**Bottleneck**: `MatrixCell.vue` (T-3.9) is a dependency for `MatrixLineRow.vue` (T-3.7) — implement MatrixCell first within PR3.

**Critical path**: T-1.1 → T-2.1 → T-3.3 → T-4.1 → T-5.3 → T-6.4
