# Exploration: budget-execution-ui (Matrix View — Full Scope)

## Executive Summary

The `budget-execution-ui` change is a full **operational budget matrix view** — a multi-period, hierarchically-collapsible matrix showing Real (budgeted) vs Ejecutado (executed) amounts per BudgetLine per Period. The authoritative design is `AnalisisInicial/MyBudget-Design-v1.0.png`. This is NOT a simple inline-expansion approach; it is a substantially larger scope requiring a new dedicated view at a new route, a new Pinia store, and approximately 12-15 new components. Delivery in 6 PRs is recommended.

---

## Design Reference Analysis (`MyBudget-Design-v1.0.png`)

### Header Controls
- **Cycle name + date range**: "Ciclo Nombre / De: Fecha Inicial Hasta: Fecha Final"
- **Incluir eliminados** checkbox — when checked, soft-deleted groups/categories/lines/execution records become visible (read-only, cannot be edited)
- **Currency toggle**: GTQ / USD radio buttons with exchange rate display ("7.5 GTQ per 1 USD")
- **Top summary badges**: colored totals for Total de Ahorro Largo Plazo (green), Total de Ahorro Preventivo (orange), Total de Gasto (red)

### Period Navigation
- Horizontal sliding window showing **3 periods at a time**
- "← Ir a Período anterior" button (left nav)
- "Ir a siguiente Período →" button (right nav)
- Each visible period shows: "Período N / De: Fecha Inicial / Hasta: Fecha Final" as column header
- Two sub-columns per period: **Presupuesto** (Real) + **Ejecutado**

### Row Hierarchy
```
CategoryGroup (Grupo)
  ├── Controls: trash, collapse/expand toggle, up/down arrows
  ├── Totals: Total Real x Grupo | Total Ejecutado x Grupo (per period column)
  └── Category (Categoría)
        ├── Controls: collapse/expand, up/down arrows, "Insertar Categoría"
        ├── Totals: Total Real x Categoría | Total Ejecutado x Categoría (per period column)
        └── BudgetLine (Línea)
              ├── Controls: up/down arrows, "Insertar Línea"
              ├── Real | Total Ejecutado (per period column)
              └── Presupuesto Estimado sub-row: Estimado - Real | Real - Total Ejecutado
```

### Cell Interactions
- **Double-click on Ejecutado cell** (BudgetLine × Period) → opens `ExecutionListModal` for that line+period combination
- **Double-click on Total (bottom summary) cell** → opens modal with all execution records for that period+type
- Single click on collapse/expand icons for groups and categories
- **"Insertar Línea"** link below each category's last line, within the period column — opens inline insert for that category
- **"Insertar Categoría"** link at the bottom of each group section
- **"Insertar Grupo"** link at the bottom of the group list

### Bottom Summary Rows (colored)
Three fixed rows at the bottom, one per `LineType`:

| Row | Color | Columns per period |
|---|---|---|
| Total de Ahorro Largo Plazo | Green | Total Estimado \| Total Real \| Total Ejecutado |
| Total de Ahorro Preventivo | Orange | Total Estimado \| Total Real \| Total Ejecutado |
| Total de Gasto | Red | Total Estimado \| Total Real \| Total Ejecutado |

### Drag-and-Drop
- Groups and categories are reorderable via drag-and-drop (same `vue-draggable-plus` pattern as `CategoryTreeView.vue`)
- Lines can also be reordered within their category (drag-and-drop on rows)
- `DisplayOrder` attribute controls ordering

### Currency Display
- **GTQ**: default currency (symbol Q or code GTQ)
- **USD**: alternate currency — amounts are converted using the cycle's `exchangeRate`
- Toggle affects all amount cells simultaneously
- Exchange rate is stored per Cycle: `currentCycle.exchangeRate` (GTQ per 1 USD)
- Currency conversion: `amount_usd = amount_gtq / exchangeRate`

### "Incluir eliminados" Behavior
- Soft-deleted Groups, Categories, Lines, and ExecutionRecords become visible when checked
- Eliminated items are displayed in gray, collapsed state
- Items in eliminated state cannot be edited (read-only)
- Consistent with the existing restore pattern in the budget-execution backend

### General Notes from Design
- **Real**: The latest `BudgetLineRevision.BudgetedAmount` — the planned amount
- **Total Ejecutado**: Net execution = Σ(Expense + DebitNote) − Σ(CreditNote)
- **Estimado - Real** sub-row: shows the difference between estimated and actual budgeted amount
- **Real - Total Ejecutado** sub-row: variance (remaining budget)
- Period `status` = Closed → refresh icon visible per column; cells not editable
- Per-period refresh icon for on-demand re-fetch of execution totals

---

## Current Frontend Architecture Map

```
Project/frontend/src/
├── api/
│   └── axios.ts                         — Axios instance with JWT + CorrelationId interceptors
├── features/
│   └── budget-structure/                — All existing CRUD views
│       ├── api/
│       │   ├── budgetLines.api.ts       — GET/POST/PUT/DELETE /periods/:id/lines
│       │   ├── categories.api.ts
│       │   ├── categoryGroups.api.ts
│       │   ├── currencies.api.ts        — GET /budgets/:id/currencies
│       │   ├── cycles.api.ts
│       │   └── periods.api.ts
│       ├── components/
│       │   ├── BudgetLineModal.vue      — Create/edit modal for budget lines
│       │   ├── BudgetLineRow.vue        — Table row with inline-edit (single period, flat)
│       │   ├── BudgetTabs.vue           — Tab nav: Cycles | Categories
│       │   ├── CategoryForm.vue
│       │   ├── CategoryGroupForm.vue
│       │   ├── CycleForm.vue
│       │   ├── EmptyState.vue           — Reusable empty state component
│       │   └── PeriodForm.vue
│       ├── composables/
│       │   └── useRoleGate.ts           — isAdmin, isOperator, canWriteStructure, canWriteLines
│       ├── views/
│       │   ├── BudgetLinesView.vue      — Current per-period lines table (maintenance view)
│       │   ├── BudgetSelectionView.vue
│       │   ├── CategoryTreeView.vue     — VueDraggable groups/categories tree
│       │   ├── CycleDetailView.vue      — Periods management
│       │   └── CycleListView.vue
│       ├── store.ts                     — useBudgetStructureStore (397 lines)
│       └── types.ts                     — CycleDetail, PeriodSummary, CategoryGroupResponse, etc.
├── i18n/
│   ├── index.ts                         — createI18n, legacy:false, en+es
│   └── locales/
│       ├── en.json                      — budgetStructure.*, common.*, auth.*, invitation.*
│       └── es.json
├── layouts/
│   ├── AppLayout.vue                    — Navbar with layoutStore.pageActions, budget switcher
│   └── PublicLayout.vue
├── router/
│   └── index.ts                         — Nested routes under /budgets/:budgetId/
├── stores/
│   ├── auth.store.ts                    — JWT, user profile, memberships
│   ├── layout.store.ts                  — activeBudgetId, pageActions
│   ├── locale.store.ts
│   └── notification.store.ts
└── views/                               — Auth views (Login, Register, etc.)
```

---

## Component Reuse Inventory

| Existing Component | Reusable in Matrix | Notes |
|---|---|---|
| `useRoleGate.ts` | Yes — direct reuse | `canWriteLines`, `canWriteStructure`, `isAdmin` |
| `EmptyState.vue` | Yes | When no groups/lines exist |
| `BudgetTabs.vue` | Extend | Add "Matrix" tab; needs optional `cycleId` prop |
| `BudgetLineModal.vue` | No | Matrix uses different cell-click UX |
| `BudgetLineRow.vue` | No | Matrix rows are multi-column per period |
| `CategoryGroupForm.vue` | Yes — modal reuse | "Insertar Grupo" flow |
| `CategoryForm.vue` | Yes — modal reuse | "Insertar Categoría" flow |
| `VueDraggable` (vue-draggable-plus) | Yes — already installed | Groups, categories, lines reordering |
| `lucide-vue-next` icons | Yes — already installed | Expand/collapse/delete/refresh icons |
| `useBudgetStructureStore` | Partial | Reuse `categoryGroups`, `currentCycle`, `periods`, `loadGroups`, `loadCycleDetail` |
| `types.ts` (`CycleDetail`, `PeriodSummary`, etc.) | Yes | Matrix reads cycle currency info |

---

## New Component Decomposition

```
src/features/budget-execution/             ← NEW feature folder
├── types.ts                               — ExecutionRecordDto, PeriodTotalsDto, MatrixCellData, etc.
├── api/
│   ├── executions.api.ts                  — CRUD + restore for ExecutionRecord
│   └── executionTotals.api.ts             — GET /periods/:id/execution-totals
├── store.ts                               — useBudgetMatrixStore
├── composables/
│   ├── useMatrixNavigation.ts             — period sliding window (visiblePeriods, prev/next)
│   ├── usePeriodData.ts                   — per-period fetch + local cache
│   └── useCurrencyDisplay.ts             — GTQ/USD amount formatting with exchangeRate
└── components/
    ├── MatrixControls.vue                 — Header: cycle name, Incluir eliminados, currency toggle
    ├── MatrixPeriodHeader.vue             — Period column header (dates + sub-column labels)
    ├── MatrixGroupRow.vue                 — CategoryGroup row: name, controls, period totals
    ├── MatrixCategoryRow.vue             — Category row: name, controls, period totals
    ├── MatrixLineRow.vue                  — BudgetLine row: Real + Ejecutado per period (double-click)
    ├── MatrixEstimatedRow.vue             — Presupuesto Estimado sub-row (variance)
    ├── MatrixSummaryRow.vue               — Bottom totals by LineType (colored)
    ├── MatrixCell.vue                     — Reusable cell: Real amount + Ejecutado amount
    ├── MatrixRefreshIcon.vue              — Per-period refresh button (closed periods)
    ├── ExecutionListModal.vue             — Modal: list of ExecutionRecords for line×period
    ├── ExecutionRecordRow.vue             — Single record row inside modal (edit/delete/restore)
    └── ExecutionRecordForm.vue            — Create/edit form (entryType, amount, note, currency)

src/features/budget-execution/views/
└── BudgetMatrixView.vue                   — Page-level view: layout, period nav, matrix table
```

**Changes to existing files:**
- `src/router/index.ts` — add `cycles/:cycleId/matrix` → `BudgetMatrixView`
- `src/features/budget-structure/components/BudgetTabs.vue` — add "Matrix" tab (needs `cycleId` prop)
- `src/i18n/locales/en.json` — add `budgetMatrix.*` and `budgetExecution.*` namespaces
- `src/i18n/locales/es.json` — same

---

## Store Strategy

The existing `useBudgetStructureStore` (397 lines) handles cycles/periods/groups/categories/lines for CRUD maintenance views. Adding matrix state would mix concerns and push it past 600 lines. Execution state MUST live in a dedicated `useBudgetMatrixStore`.

**State shape:**
```typescript
interface BudgetMatrixState {
  cycleId: string | null
  allPeriods: PeriodSummary[]
  visiblePeriodOffset: number          // index of first visible period (window of 3)
  visibleWindowSize: number            // 3 by default
  periodTotals: Map<string, PeriodTotalsDto>   // periodId → totals
  loadingPeriods: Set<string>
  collapsedGroupIds: Set<string>
  collapsedCategoryIds: Set<string>
  showDeleted: boolean
  displayCurrency: 'default' | 'alternate'
  openModalLineId: string | null
  openModalPeriodId: string | null
  executionRecords: Map<string, ExecutionRecordDto[]>   // `${lineId}:${periodId}` → records
  loadingExecutions: Set<string>
  loading: boolean
  error: string | null
}
```

**Key actions:**
- `initMatrix(budgetId, cycleId)` — loads cycle detail + periods + groups via budgetStructure.store
- `loadPeriodTotals(budgetId, periodId)` — fetches execution-totals for one period column
- `loadVisiblePeriods(budgetId)` — loads totals for the 3 currently visible periods
- `navigatePrev()` / `navigateNext()` — shifts offset, triggers loadVisiblePeriods
- `openExecutionModal(lineId, periodId)` — fetches records if not cached; sets modal IDs
- `closeExecutionModal()`
- `createExecution(...)` / `updateExecution(...)` / `deleteExecution(...)` / `restoreExecution(...)`
- `toggleGroupCollapse(groupId)` / `toggleCategoryCollapse(categoryId)`
- `setShowDeleted(value)` — triggers matrix reload
- `setDisplayCurrency(currency)` — reactive toggle, no API call needed

**Coordination:** `BudgetMatrixView` calls `budgetStructureStore.loadCycleDetail(budgetId, cycleId)` for currency info first, then `budgetMatrixStore.initMatrix(...)`.

---

## Router Plan

New route under the existing `budgets/:budgetId/` children array in `router/index.ts`:

```typescript
{
  path: 'cycles/:cycleId/matrix',
  name: 'BudgetMatrix',
  component: () => import('@/features/budget-execution/views/BudgetMatrixView.vue'),
}
```

**BudgetTabs update:**
- Add third tab "Matrix" linking to `{ name: 'BudgetMatrix', params: { budgetId, cycleId } }`
- `BudgetTabs` needs optional `cycleId?: string` prop
- Matrix tab only renders when `cycleId` is provided (CycleDetail/BudgetLines/BudgetMatrix routes)

---

## i18n Key Plan

New namespaces in `en.json` and `es.json`:

```json
"budgetMatrix": {
  "title": "Budget Matrix",
  "controls": {
    "includeDeleted": "Include deleted",
    "currency": "Currency",
    "refresh": "Refresh period"
  },
  "navigation": {
    "prevPeriod": "Previous period",
    "nextPeriod": "Next period"
  },
  "columns": {
    "budgeted": "Budgeted",
    "executed": "Executed",
    "estimatedVariance": "Estimated - Budgeted",
    "executedVariance": "Budgeted - Executed"
  },
  "rows": {
    "insertLine": "Insert Line",
    "insertCategory": "Insert Category",
    "insertGroup": "Insert Group"
  },
  "summary": {
    "expenseTotal": "Total Expenses",
    "longTermSavingsTotal": "Total Long-term Savings",
    "preventiveSavingsTotal": "Total Preventive Savings",
    "totalEstimated": "Total Estimated",
    "totalReal": "Total Budgeted",
    "totalExecuted": "Total Executed"
  },
  "loading": "Loading period data...",
  "empty": {
    "title": "No budget structure yet",
    "description": "Add groups and categories to start planning.",
    "action": "Go to Categories"
  }
},
"budgetExecution": {
  "title": "Execution Entries",
  "modal": { "title": "Executions: {lineName} — {periodName}" },
  "addEntry": "Add Entry",
  "entryTypes": {
    "expense": "Expense",
    "creditNote": "Credit Note",
    "debitNote": "Debit Note"
  },
  "columns": {
    "type": "Type", "amount": "Amount", "currency": "Currency",
    "note": "Note", "date": "Date", "actions": "Actions"
  },
  "form": {
    "entryType": "Entry Type",
    "amount": "Amount",
    "currency": "Currency",
    "note": "Note",
    "noteRequired": "Note is required for Credit Note and Debit Note"
  },
  "messages": {
    "periodClosed": "Period is closed. No changes allowed.",
    "noEntries": "No entries yet. Click to add the first one.",
    "confirmDelete": "Delete this execution record?",
    "confirmRestore": "Restore this deleted record?"
  },
  "restore": "Restore"
}
```

---

## Test Strategy

### Vitest (unit + component)

| Target | What to test |
|---|---|
| `useBudgetMatrixStore` | `initMatrix`, `loadPeriodTotals`, `navigatePrev/Next`, `openExecutionModal`, currency toggle; mock API with `vi.mock` |
| `useMatrixNavigation` | sliding window logic — offset clamping, prev/next disabled at edges |
| `useCurrencyDisplay` | GTQ→USD conversion with `exchangeRate`, `Intl.NumberFormat` output |
| `MatrixCell.vue` | renders Real + Ejecutado amounts correctly; `@dblclick` emits event |
| `ExecutionRecordForm.vue` | validation: note required for CreditNote/DebitNote; amount > 0 |
| `ExecutionListModal.vue` | renders records list; empty state; closed period → form hidden |
| `MatrixSummaryRow.vue` | correct totals per LineType; correct color CSS classes |

Pattern: `vi.mock` for all API modules, `createPinia()` + `setActivePinia()`, `store.$patch()` for state seeding, `@testing-library/vue` render for component tests. Follows existing pattern in `budget-structure/__tests__/`.

### Playwright E2E (UI-layer)

New directory: `e2e/budget-matrix/`

| File | Coverage |
|---|---|
| `helpers.ts` | `seedMatrixContext(page)` — registers user, creates cycle+periods+groups+categories+lines |
| `budget-matrix-navigation.spec.ts` | navigate to matrix; see 3 periods; prev/next; disabled state at edges |
| `budget-matrix-collapse.spec.ts` | collapse/expand group; collapse/expand category; rows hide/show |
| `budget-matrix-execution.spec.ts` | double-click Ejecutado cell → modal; create execution → total updates |
| `budget-matrix-currency.spec.ts` | toggle GTQ/USD → amounts recalculate; exchange rate shown |
| `budget-matrix-include-deleted.spec.ts` | check "Incluir eliminados" → deleted row appears; unchecked → hidden |
| `budget-matrix-period-closed.spec.ts` | closed period column → modal shows read-only; no Add Entry button |
| `budget-matrix-rbac.spec.ts` | operator sees add entry; non-member cannot (403/404) |

**Existing infrastructure to extend:**
- `seedOwnerAndLogin(page, prefix)` from `e2e/budget-structure/helpers.ts`
- `seedBudgetContext(request, prefix)` from `e2e/budget-execution/helpers.ts`
- Config: baseURL `http://localhost:5173`, sequential, chromium, requires full Docker stack

---

## Delivery Plan (6 Chained PRs)

| PR | Branch | Content | Est. Lines |
|---|---|---|---|
| PR1 | `feat/budget-matrix-api` | `types.ts`, `api/executions.api.ts`, `api/executionTotals.api.ts`; Vitest for api layer | ~200 |
| PR2 | `feat/budget-matrix-store` | `useBudgetMatrixStore`, `useMatrixNavigation`, `usePeriodData`, `useCurrencyDisplay`; Vitest | ~350 |
| PR3 | `feat/budget-matrix-skeleton` | `BudgetMatrixView`, `MatrixPeriodHeader`, `MatrixGroupRow`, `MatrixCategoryRow`, `MatrixLineRow`, `MatrixEstimatedRow`, `MatrixCell`, period nav; router + BudgetTabs update; i18n skeleton | ~400 |
| PR4 | `feat/budget-matrix-execution` | `ExecutionListModal`, `ExecutionRecordRow`, `ExecutionRecordForm`, `MatrixRefreshIcon`; double-click wiring; CRUD flow; Vitest component tests | ~380 |
| PR5 | `feat/budget-matrix-summary` | `MatrixSummaryRow`, `MatrixControls` (currency toggle + incluir eliminados); full i18n EN+ES; closed-period guard | ~320 |
| PR6 | `feat/budget-matrix-e2e` | Playwright UI-layer specs + `helpers.ts` | ~300 |

Total: ~1,950 lines across 6 PRs. All PRs under 400 lines changed.

---

## Backend Dependency Notes

### Redis Caching for Closed-Period Execution Totals (non-blocking)
Closed periods are immutable — execution totals never change once closed. The backend team should add Redis caching for `GET /periods/{periodId}/execution-totals` when `period.Status == "Closed"`. Suggested cache key: `execution-totals:{periodId}`, TTL: indefinite; invalidate on period restore. This is a performance optimization, NOT a UI blocker.

### Confirmed Backend Endpoints
```
GET    /api/budgets/{id}/periods/{periodId}/budget-lines/{lineId}/executions → ExecutionRecordDto[]
POST   /api/budgets/{id}/periods/{periodId}/budget-lines/{lineId}/executions → 201 { id }
PUT    /api/budgets/{id}/periods/{periodId}/budget-lines/{lineId}/executions/{execId} → 200
DELETE /api/budgets/{id}/periods/{periodId}/budget-lines/{lineId}/executions/{execId} → 204
POST   /api/budgets/{id}/periods/{periodId}/budget-lines/{lineId}/executions/{execId}/restore → 200
GET    /api/budgets/{id}/periods/{periodId}/execution-totals → { lineTotals[], categoryTotals[] }
```

---

## CSS / Layout Concerns

1. **Sticky left column**: Names + controls column must be `position: sticky; left: 0; z-index: 10`
2. **Horizontal overflow**: Wrap matrix in `overflow-x: auto`; period columns need `min-w-[200px]`
3. **Sub-column headers**: Each period column header spans 2 sub-columns (Presupuesto | Ejecutado) via `colspan="2"` on `<th>`
4. **Collapsed rows**: Use `v-show` (not `v-if`) on group/category children — preserves DOM identity for drag-and-drop
5. **Summary row colors**: `bg-green-100`, `bg-orange-100`, `bg-red-100` (Tailwind utilities, not daisyUI classes)
6. **Mobile**: Not a target — desktop financial tool; horizontal scroll is acceptable on small screens

---

## Key Design Questions for Proposal

1. **Line drag-and-drop in matrix**: Design shows up/down arrows on lines. Recommend arrow buttons only in matrix; reserve drag-and-drop for CategoryTree maintenance view.
2. **"Insertar Línea" scope**: Since `BudgetLine` belongs to a Category (not period-specific), inserting from a period column should create a line visible in ALL period columns. Recommendation: create line globally; the period column just provides UX context for which category to insert into.
3. **Period window size**: Hardcode 3 periods visible at once (matches design).

---

## Risks

| Risk | Severity | Mitigation |
|---|---|---|
| Sticky left column CSS conflicts with daisyUI table styles | High | Prototype early in PR3; may need custom `<table>` structure |
| Double-click on Ejecutado cell vs single-click on collapse icon | Medium | Differentiate by target element; dblclick only on amount cells |
| Period sliding window state lost on route navigation | Medium | Store `visiblePeriodOffset` in Pinia (survives within session) |
| "Insertar Línea" per-period-column: identifying which period triggered insert | Medium | Pass `periodId` prop to `MatrixLineRow`; emit `(categoryId, periodId)` to view |
| "Incluir eliminados" toggle requires re-fetching all period data | Medium | `setShowDeleted()` action clears `periodTotals` Map and calls `loadVisiblePeriods()` |
| Currency float precision in GTQ↔USD conversion | Low | `Math.round(amount / exchangeRate * 100) / 100` |
| BudgetTabs Matrix tab unavailable without cycleId | Low | Render Matrix tab conditionally when `cycleId` prop is provided |
| E2E tests require full Docker stack | Low | `seedMatrixContext` handles all setup via API; document Docker prerequisite |
| Scope larger than original estimate | Info | 6 PRs ~1,950 lines vs original 3 PRs ~930 lines |

---

## Ready for Proposal

Yes. Design fully analyzed. Frontend architecture well understood. All backend API contracts confirmed from integration tests. 6-PR delivery plan keeps each PR under 400 lines.
