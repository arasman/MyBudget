# Design: Budget Execution Matrix UI

## Technical Approach

Feature folder `src/features/budget-execution/` with independent `useBudgetMatrixStore`, 3 composables, 13 components, and `BudgetMatrixView`. Reads structural data from `useBudgetStructureStore` (groups/categories/cycle), owns all execution state independently. Native HTML `<table>` with CSS sticky left column + horizontal scroll. Per-period progressive loading with skeleton columns.

## Architecture Decisions

### AD-1: Table DOM Structure for Sticky Left Column

| Option | Pros | Cons |
|--------|------|------|
| A: Native `<table>` + `position: sticky; left: 0` | Semantic HTML; `colspan` for period sub-columns; matches existing daisyUI `table` class usage in project | Requires `overflow-x: auto` on wrapper div, `border-collapse: separate` to avoid z-index paint issues |
| B: CSS Grid | Flexible layout; no `colspan` hacks | No `colspan`; period header spanning requires manual span tracking; breaks from project table convention |
| C: Two synchronized tables | Perfect sticky behavior | Scroll sync is fragile; duplicate DOM; accessibility nightmare |

**Decision**: Option A. The project already uses `<table class="table table-zebra">` in BudgetLinesView, CycleListView, CycleDetailView. Sticky works with `border-collapse: separate; border-spacing: 0` and `position: sticky; left: 0; z-index: 10; background: oklch(var(--b1))` on the first `<td>`/`<th>` of each row. The wrapper div gets `overflow-x: auto`.

**Implementation**: No daisyUI `table-zebra` on the matrix table (zebra conflicts with group/category row colors). Use plain `table` class + Tailwind utilities for borders.

### AD-2: Store Reactive State for Map Types

| Option | Pros | Cons |
|--------|------|------|
| A: `ref(new Map())` with `.value` reassignment | Real Map API; familiar | Must reassign `map.value = new Map(map.value)` after mutation to trigger reactivity; easy to forget |
| B: `reactive({})` keyed by string | Fully reactive via Vue proxy; `Record<string, T>` is natural for Pinia | No `.size`, no `.has()` with Map API; key-value lookup via `obj[key]` which is fine for string keys |
| C: `shallowRef` + `triggerRef` | Fine-grained control | Verbose; must remember `triggerRef` after every mutation |

**Decision**: Option B. Use `Record<string, T>` (plain objects) instead of `Map`. Pinia's `reactive()` makes object properties automatically reactive. The keys are string UUIDs (`periodId`) and composite keys (`${lineId}:${periodId}`), both natural as object keys. This matches how the existing store uses arrays and refs.

**State shape** (TypeScript):
```typescript
interface BudgetMatrixStoreState {
  cycleId: string | null
  allPeriods: PeriodSummary[]
  visiblePeriodOffset: number
  periodTotals: Record<string, PeriodTotalsDto>       // periodId -> totals
  loadingPeriods: Record<string, boolean>              // periodId -> loading
  collapsedGroupIds: Set<string>                       // Set is fine for boolean membership
  collapsedCategoryIds: Set<string>
  showDeleted: boolean
  displayCurrency: 'default' | 'alternate'
  openModalLineId: string | null
  openModalPeriodId: string | null
  executionRecords: Record<string, ExecutionRecordDto[]> // `${lineId}:${periodId}` -> records
  loadingExecutions: Record<string, boolean>
  loading: boolean
  error: string | null
}
```

Note: `Set<string>` for collapsed IDs is acceptable because these are only read via `.has()` and toggled via `.add()`/`.delete()` — they never need deep reactive tracking on individual elements. Use `ref(new Set())` and reassign on mutation.

### AD-3: Progressive Loading Skeleton

**Decision**: Each period column shows a daisyUI `skeleton` component (`<div class="skeleton h-4 w-20">`) in every cell while `loadingPeriods[periodId]` is true. The label column (left sticky) renders immediately from `categoryGroups` data (already loaded via `budgetStructureStore`).

- Mount: `BudgetMatrixView.onMounted` calls `initMatrix(budgetId, cycleId)` which loads cycle detail + groups, then calls `loadVisiblePeriods()`.
- `loadVisiblePeriods()` fires 3 parallel `loadPeriodTotals(periodId)` calls. Each sets `loadingPeriods[periodId] = true` before fetch, `false` after.
- Partially-loaded state is natural: each column independently transitions from skeleton to data as its fetch resolves. No blocking on all 3.
- Navigating prev/next: new periods that are not yet in `periodTotals` show skeleton; already-cached periods render instantly.

### AD-4: Currency Conversion

**Decision**: Pure client-side conversion. No new API call.

- `useBudgetMatrixStore.displayCurrency` stores `'default' | 'alternate'`.
- `useCurrencyDisplay` composable: `function convertAmount(amount: number, exchangeRate: number | null, mode: 'default' | 'alternate'): number`. When `mode === 'alternate'` and `exchangeRate` is truthy: `Math.round(amount / exchangeRate * 100) / 100`. When `mode === 'default'`: returns `amount` unchanged.
- Template: `convertAmount(cell.realAmount, currentCycle.exchangeRate, store.displayCurrency)`.
- When `currentCycle.alternateCurrencyId` is null, the toggle is disabled (no alternate currency configured for this cycle).

### AD-5: ExecutionListModal Data Flow

**Decision**: Store-driven modal with cache-first fetch.

- `store.openExecutionModal(lineId, periodId)`: sets `openModalLineId` + `openModalPeriodId`. If `executionRecords[${lineId}:${periodId}]` exists, skip fetch. Otherwise fetch and populate.
- `store.closeExecutionModal()`: clears modal IDs. Does NOT clear cached records.
- After CRUD mutation (create/update/delete/restore): update `executionRecords[key]` locally AND delete `periodTotals[periodId]` from cache, then re-fetch that single period's totals via `loadPeriodTotals(periodId)`. This ensures the Ejecutado cell updates.
- Modal receives `lineId`, `periodId`, `budgetId` as props. Reads records from `store.executionRecords[key]`. Shows form only when `period.status !== 'Closed'`.

### AD-6: "Incluir eliminados" Re-fetch

**Decision**: The backend `GET /periods/{periodId}/execution-totals` does NOT support an `includeDeleted` param. The SQL filters `DeletedAt IS NULL` on both ExecutionRecords and BudgetLines. Likewise, `ListExecutionRecords` filters `DeletedAt IS NULL`.

- **Scope note**: "Incluir eliminados" in the matrix is a **UI-layer-only** feature for the `ExecutionListModal`. When `showDeleted = true`, the modal shows soft-deleted records that are already fetched. The totals row values (Ejecutado in the matrix cells) always exclude deleted records because the backend enforces it.
- `setShowDeleted(true)` does NOT re-fetch period totals. It only affects `ExecutionListModal` display: a separate API call with `?includeDeleted=true` would need a backend change. For now, the modal fetches all records (including deleted) only when `showDeleted` is toggled, requiring a **new query parameter** on the list endpoint. If the backend does not support it yet, the toggle is visible but only affects visibility of already-loaded records.
- **Decision**: defer backend `includeDeleted` param to a follow-up. The toggle controls local filter state on the modal's record list. Records fetched always exclude deleted unless backend adds the param.

### AD-7: BudgetTabs Extension

**Decision**: Add optional `cycleId?: string` prop. Conditionally render Matrix tab.

- `BudgetTabs.vue` gets `cycleId?: string` prop.
- Matrix tab: `<RouterLink :to="{ name: 'BudgetMatrix', params: { budgetId, cycleId } }" v-if="cycleId">Matrix</RouterLink>`.
- `isActive` function: add `MATRIX_ROUTE_NAMES = new Set(['BudgetMatrix'])` and extend the function signature to accept `'BudgetMatrix'` tab. Matrix tab is considered part of the Cycles tab family for active-highlighting, OR is its own tab (decision: own tab, separate highlight).
- `CYCLE_ROUTE_NAMES` does NOT include `BudgetMatrix` — the Matrix tab has its own active state.

### AD-8: Line Reorder (Arrows + Drag-and-Drop)

**Decision**: Both mechanisms produce the same output: an ordered array of line IDs sent to `ReorderBudgetLines`.

- **Arrow movement**: `MatrixCategoryRow` maintains an ordered `lineIds` array derived from the store's group/category data. Up arrow: swap `lineIds[idx]` with `lineIds[idx-1]`. Down arrow: swap `lineIds[idx]` with `lineIds[idx+1]`. Then call `reorderLines(budgetId, periodId, categoryId, lineIds)`.
- **Drag-and-drop**: `vue-draggable-plus` `@end` handler reads the new order from the draggable model and calls the same `reorderLines()`.
- **Optimistic update**: update the local `lineIds` array immediately. On API error, revert to previous order and show notification.
- Group and category reorder: already handled by `budgetStructureStore.reorderGroups()` / `reorderCategories()`. Matrix reuses these.

## Data Flow

```
BudgetMatrixView
  |-- onMounted: budgetStructureStore.loadCycleDetail() + .loadGroups()
  |-- then: budgetMatrixStore.initMatrix() --> loadVisiblePeriods()
  |
  |-- MatrixControls (currency toggle, includeDeleted, cycle info)
  |     |-- setDisplayCurrency() --> reactive conversion in cells
  |     |-- setShowDeleted() --> filter toggle
  |
  |-- Period Nav (prev/next buttons)
  |     |-- navigatePrev/Next() --> loadVisiblePeriods() for new window
  |
  |-- <table>
  |     |-- MatrixPeriodHeader (3 visible period columns, 2 sub-cols each)
  |     |-- for each group:
  |     |     |-- MatrixGroupRow (name + per-period group totals)
  |     |     |-- for each category:
  |     |     |     |-- MatrixCategoryRow (name + per-period category totals)
  |     |     |     |-- for each line:
  |     |     |     |     |-- MatrixLineRow (Real + Ejecutado cells)
  |     |     |     |     |-- MatrixEstimatedRow (variance sub-row)
  |     |-- MatrixSummaryRow x3 (by LineType: green/orange/red)
  |
  |-- ExecutionListModal (triggered by dblclick on Ejecutado cell)
        |-- ExecutionRecordRow (per record: edit/delete/restore)
        |-- ExecutionRecordForm (create/edit)
        |-- CRUD --> update executionRecords cache + re-fetch periodTotals
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `src/features/budget-execution/types.ts` | Create | ExecutionRecordDto, PeriodTotalsDto, LineTotalDto, CategoryTotalDto |
| `src/features/budget-execution/api/executions.api.ts` | Create | CRUD + restore for execution records |
| `src/features/budget-execution/api/executionTotals.api.ts` | Create | GET period execution totals |
| `src/features/budget-execution/store.ts` | Create | useBudgetMatrixStore with Record-based state |
| `src/features/budget-execution/composables/useMatrixNavigation.ts` | Create | Period sliding window logic |
| `src/features/budget-execution/composables/usePeriodData.ts` | Create | Per-period fetch + cache orchestration |
| `src/features/budget-execution/composables/useCurrencyDisplay.ts` | Create | Amount conversion composable |
| `src/features/budget-execution/components/MatrixControls.vue` | Create | Header controls |
| `src/features/budget-execution/components/MatrixPeriodHeader.vue` | Create | Period column headers |
| `src/features/budget-execution/components/MatrixGroupRow.vue` | Create | Group row with totals |
| `src/features/budget-execution/components/MatrixCategoryRow.vue` | Create | Category row with totals |
| `src/features/budget-execution/components/MatrixLineRow.vue` | Create | Line row: Real + Ejecutado |
| `src/features/budget-execution/components/MatrixEstimatedRow.vue` | Create | Variance sub-row |
| `src/features/budget-execution/components/MatrixSummaryRow.vue` | Create | Bottom totals by LineType |
| `src/features/budget-execution/components/MatrixCell.vue` | Create | Reusable amount cell |
| `src/features/budget-execution/components/MatrixRefreshIcon.vue` | Create | Per-period refresh button |
| `src/features/budget-execution/components/ExecutionListModal.vue` | Create | Records list modal |
| `src/features/budget-execution/components/ExecutionRecordRow.vue` | Create | Single record row |
| `src/features/budget-execution/components/ExecutionRecordForm.vue` | Create | Create/edit form |
| `src/features/budget-execution/views/BudgetMatrixView.vue` | Create | Page-level view |
| `src/router/index.ts` | Modify | Add `cycles/:cycleId/matrix` route |
| `src/features/budget-structure/components/BudgetTabs.vue` | Modify | Add optional `cycleId` prop + Matrix tab |
| `src/i18n/locales/en.json` | Modify | Add `budgetMatrix.*` + `budgetExecution.*` |
| `src/i18n/locales/es.json` | Modify | Add `budgetMatrix.*` + `budgetExecution.*` |

## Interfaces / Contracts

```typescript
// types.ts
export interface LineTotalDto {
  budgetLineId: string
  budgetLineName: string
  totalExpenses: number
  totalCreditNotes: number
  totalDebitNotes: number
  netTotal: number
}

export interface CategoryTotalDto {
  categoryGroupId: string
  categoryGroupName: string
  categoryId: string | null
  categoryName: string | null
  totalExpenses: number
  totalCreditNotes: number
  totalDebitNotes: number
  netTotal: number
}

export interface PeriodTotalsDto {
  lineTotals: LineTotalDto[]
  categoryTotals: CategoryTotalDto[]
}

export type EntryType = 'Expense' | 'CreditNote' | 'DebitNote'

export interface ExecutionRecordDto {
  id: string
  entryType: number          // 1=Expense, 2=CreditNote, 3=DebitNote
  amount: number
  currencyId: string
  exchangeRate: number | null
  exchangeRateTo: number | null
  accountId: string | null
  paymentMethodId: string | null
  note: string | null
  createdAt: string
  updatedAt: string | null
}

// executions.api.ts
export function list(budgetId: string, periodId: string, lineId: string): Promise<ExecutionRecordDto[]>
export function create(budgetId: string, periodId: string, lineId: string, payload: CreateExecutionPayload): Promise<{ id: string }>
export function update(budgetId: string, periodId: string, lineId: string, execId: string, payload: UpdateExecutionPayload): Promise<void>
export function remove(budgetId: string, periodId: string, lineId: string, execId: string): Promise<void>
export function restore(budgetId: string, periodId: string, lineId: string, execId: string): Promise<void>

// executionTotals.api.ts
export function getPeriodTotals(budgetId: string, periodId: string): Promise<PeriodTotalsDto>

// Composable signatures
export function useMatrixNavigation(allPeriods: Ref<PeriodSummary[]>): {
  visiblePeriods: ComputedRef<PeriodSummary[]>
  offset: Ref<number>
  canGoPrev: ComputedRef<boolean>
  canGoNext: ComputedRef<boolean>
  goPrev: () => void
  goNext: () => void
}

export function usePeriodData(budgetId: Ref<string>): {
  loadPeriodTotals: (periodId: string) => Promise<void>
  loadVisiblePeriods: (periodIds: string[]) => Promise<void>
}

export function useCurrencyDisplay(exchangeRate: Ref<number | null>): {
  convert: (amount: number, mode: 'default' | 'alternate') => number
  formatAmount: (amount: number, currencyCode: string) => string
}
```

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| Unit | `useBudgetMatrixStore` actions, `useMatrixNavigation` edge clamping, `useCurrencyDisplay` conversion | Vitest + `vi.mock` API modules, `createPinia()` |
| Component | `MatrixCell` dblclick emit, `ExecutionRecordForm` validation, `ExecutionListModal` closed-period guard, `MatrixSummaryRow` color classes | `@testing-library/vue` + Pinia `$patch` |
| E2E | 8 Playwright specs: navigation, collapse, execution CRUD, currency, include-deleted, closed-period, RBAC | Full Docker stack, API seeding |

## Migration / Rollout

No migration required. All changes are additive. Rollback = revert the feature branch merge.

## Open Questions

- [x] Backend `includeDeleted` param: confirmed NOT supported. Deferred to follow-up. Toggle is UI-filter-only for now.
- [ ] ReorderBudgetLines endpoint: verify the exact API route and payload shape for line reordering (not seen in current `budgetLines.api.ts` — may need to be added in PR1).

## PR Delivery Sequence

| PR | Branch | Content | Files |
|---|---|---|---|
| PR1 | `feat/budget-matrix-api` | `types.ts`, `executions.api.ts`, `executionTotals.api.ts` + Vitest | 3 new + tests |
| PR2 | `feat/budget-matrix-store` | `store.ts`, `useMatrixNavigation`, `usePeriodData`, `useCurrencyDisplay` + Vitest | 4 new + tests |
| PR3 | `feat/budget-matrix-skeleton` | `BudgetMatrixView`, `MatrixPeriodHeader`, `MatrixGroupRow`, `MatrixCategoryRow`, `MatrixLineRow`, `MatrixEstimatedRow`, `MatrixCell`; router + BudgetTabs + i18n skeleton | 7 new + 4 modified |
| PR4 | `feat/budget-matrix-execution` | `ExecutionListModal`, `ExecutionRecordRow`, `ExecutionRecordForm`, `MatrixRefreshIcon` + Vitest | 4 new + tests |
| PR5 | `feat/budget-matrix-summary` | `MatrixSummaryRow`, `MatrixControls`, full i18n EN+ES, closed-period guard | 2 new + 2 modified |
| PR6 | `feat/budget-matrix-e2e` | 8 Playwright specs + helpers | 9 new |
