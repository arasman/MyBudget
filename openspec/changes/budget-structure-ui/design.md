# Design: Budget Structure UI

## Technical Approach

Feature module at `Project/frontend/src/features/budget-structure/` following the existing flat-feature pattern (`stores/auth.store.ts`, `api/axios.ts`). Two layout components wrap all routes. A single `budgetStructure.store` Pinia store manages entity state; thin API functions call the 23 backend endpoints. Role gating via a `useRoleGate` composable. Drag-and-drop reorder via `vue-draggable-plus`. Six chained PRs keep each diff under ~400 lines.

## Architecture Decisions

### ADR-BSUI-01: Drag-and-drop library

| Option | Tradeoff | Decision |
|--------|----------|----------|
| Native HTML5 Drag API | Zero deps; verbose, poor mobile/touch, no animated transitions | Rejected |
| SortableJS via `@vueuse/integrations` | Adds `@vueuse/integrations` + `sortablejs`; vueuse already unused in project | Rejected — unnecessary transitive dep |
| `vue-draggable-plus` | ~8KB, Vue 3 native, wraps SortableJS, `<script setup>` friendly, touch support | **Chosen** |
| Manual up/down buttons | Simplest; poor UX for long lists | Fallback only |

**Rationale**: `vue-draggable-plus` provides the best Vue 3 integration with minimal bundle impact. It supports `<script setup>`, emits model updates directly, and handles touch/mobile. If the dependency proves problematic, manual up/down buttons can replace it without API changes since the same `PUT .../order` endpoints accept a reordered ID array.

### ADR-BSUI-02: layoutStore design

**Choice**: Pinia store with reactive `pageActions` array. Views register actions via `setPageActions(actions[])` in `onMounted` and clear via `clearPageActions()` in `onUnmounted`. AppLayout reads the array and renders buttons; collapses to a `dropdown` on mobile (`sm:` breakpoint).

**Shape**:
```ts
interface PageAction {
  key: string           // unique ID for :key binding
  label: string         // i18n key
  icon?: string         // optional icon name
  action: () => void    // callback
  variant?: 'primary' | 'ghost' | 'error'
  disabled?: boolean
  requiresRole?: 'admin' | 'operator'  // role-gate at render
}
```

**Alternatives rejected**: (a) Provide/inject — breaks when navigating between views without explicit cleanup; (b) Slot-based — requires deep coupling between layout and view templates.

### ADR-BSUI-03: notificationStore design

**Choice**: Infrastructure-only Pinia store. Holds an array of `Notification` objects with `id`, `type` (info/success/warning/error), `title`, `message`, `read`, `createdAt`. Bell icon in AppLayout shows unread count badge. Dropdown panel lists recent notifications. No backend — `push()` action adds notifications locally.

**Rationale**: Provides the UI contract so future features (budget invitations, period status changes) can push notifications without refactoring layout.

### ADR-BSUI-04: DateOnly string handling

**Choice**: TypeScript branded type alias `type DateString = string & { __brand: 'DateOnly' }` with helper `toDateString(y, m, d): DateString` and `formatDate(d: DateString, locale): string` using `Intl.DateTimeFormat`. No `new Date()` parsing of API strings.

**Alternatives rejected**: (a) Raw untyped strings — loses type safety; (b) `dayjs`/`date-fns` — unnecessary dependency for display-only formatting of `YYYY-MM-DD` strings.

### ADR-BSUI-05: Role gating

**Choice**: Composable `useRoleGate(budgetId: string)` returning `{ isAdmin, isOperator, canWriteStructure, canWriteLines }` as computed refs. Reads `authStore.user.memberships` for the active budget. Views use these computeds to show/hide action buttons and disable inputs.

**Alternatives rejected**: (a) Custom directive `v-role` — harder to test, less explicit; (b) Inline computed per view — duplicated logic.

## Data Flow

```
View (onMounted)
  │
  ├── layoutStore.setPageActions([...])
  ├── budgetStructureStore.loadCycles(budgetId)
  │       │
  │       └── cyclesApi.list(budgetId)
  │               │
  │               └── http.get(`/api/budgets/${budgetId}/cycles`)
  │                       │
  │                       └── Axios → Backend → JSON response
  │
  └── useRoleGate(budgetId) → { isAdmin, canWriteStructure }
          │
          └── authStore.user.memberships.find(m => m.budgetId === budgetId)
```

Mutations follow the same path: View calls store action → store calls API function → API calls `http.post/put/delete` → store updates local state on success.

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `src/layouts/AppLayout.vue` | Create | Auth shell: navbar, budget switcher, page-actions, notification bell, user dropdown |
| `src/layouts/PublicLayout.vue` | Create | Card-centered layout for login/register/invitation |
| `src/stores/layout.store.ts` | Create | `pageActions`, `activeBudgetId`, `activeBudgetName` |
| `src/stores/notification.store.ts` | Create | `notifications[]`, `push()`, `markRead()`, `unreadCount` |
| `src/features/budget-structure/api/` | Create | `cycles.api.ts`, `periods.api.ts`, `categoryGroups.api.ts`, `categories.api.ts`, `budgetLines.api.ts` |
| `src/features/budget-structure/types.ts` | Create | All TS interfaces/types for API shapes + `DateString` branded type |
| `src/features/budget-structure/store.ts` | Create | Single Pinia store: cycles, periods, groups, categories, lines |
| `src/features/budget-structure/composables/useRoleGate.ts` | Create | Role gating composable |
| `src/features/budget-structure/views/BudgetSelectionView.vue` | Create | Budget list / auto-redirect |
| `src/features/budget-structure/views/CycleListView.vue` | Create | Cycle CRUD + set active |
| `src/features/budget-structure/views/CycleDetailView.vue` | Create | Periods list within cycle |
| `src/features/budget-structure/views/CategoryTreeView.vue` | Create | Groups + categories tree with drag-and-drop |
| `src/features/budget-structure/views/BudgetLinesView.vue` | Create | Lines table with inline create + edit modal |
| `src/features/budget-structure/components/CycleForm.vue` | Create | Create/edit cycle modal form |
| `src/features/budget-structure/components/PeriodForm.vue` | Create | Create/edit period modal form |
| `src/features/budget-structure/components/CategoryGroupForm.vue` | Create | Create/edit group modal |
| `src/features/budget-structure/components/CategoryForm.vue` | Create | Create/edit category modal |
| `src/features/budget-structure/components/BudgetLineModal.vue` | Create | Full edit modal for budget line |
| `src/features/budget-structure/components/BudgetLineRow.vue` | Create | Table row with inline create mode |
| `src/features/budget-structure/components/EmptyState.vue` | Create | Reusable guided empty-state prompt |
| `src/features/budget-structure/components/BudgetTabs.vue` | Create | Cycles / Categories tab navigation |
| `src/router/index.ts` | Modify | Nested routes under layout wrappers |
| `src/views/HomeView.vue` | Modify | Remove inline navbar; redirect to budget selection |
| `src/views/LoginView.vue` | Modify | Remove outer div shell (PublicLayout provides it) |
| `src/views/RegisterView.vue` | Modify | Remove outer div shell + fix hardcoded "Language" label |
| `src/i18n/locales/en.json` | Modify | Add `budgetStructure.*` keys + `{'@'}` fix |
| `src/i18n/locales/es.json` | Modify | Add `budgetStructure.*` keys + `{'@'}` fix |
| `Project/src/MyBudget.Api/Program.cs` | Modify | Add `app.MapScalarApiReference()` |

**Totals**: ~22 new files, ~7 modified files.

## Interfaces / Contracts

```ts
// types.ts — key interfaces (abbreviated)
type DateString = string & { __brand: 'DateOnly' }
type LineType = 'Income' | 'Expense'

interface CycleListItem {
  id: string; name: string; startDate: DateString
  endDate: DateString; isActive: boolean; periodCount: number
}

interface CycleDetail extends Omit<CycleListItem, 'periodCount'> {
  periods: PeriodSummary[]
}

interface PeriodSummary {
  id: string; name: string; startDate: DateString
  endDate: DateString; status: string
}

interface CategoryGroupResponse {
  id: string; name: string; displayOrder: number
  categories: CategoryItem[]
}

interface CategoryItem {
  id: string; name: string; displayOrder: number
}

interface BudgetLineResponse {
  id: string; name: string; lineType: LineType
  isRecurring: boolean; categoryGroupId: string
  categoryId?: string; budgetedAmount?: number
  currency?: string; revisedAt?: DateString; note?: string
}
```

```ts
// layoutStore shape
interface LayoutState {
  activeBudgetId: string | null
  activeBudgetName: string | null
  pageActions: PageAction[]
}
```

## Route Design

```ts
const routes: RouteRecordRaw[] = [
  {
    path: '/',
    component: AppLayout,
    meta: { requiresAuth: true },
    children: [
      { path: '', name: 'BudgetSelection', component: BudgetSelectionView },
      {
        path: 'budgets/:budgetId',
        children: [
          { path: '', redirect: { name: 'CycleList' } },
          { path: 'cycles', name: 'CycleList', component: CycleListView },
          { path: 'cycles/:cycleId', name: 'CycleDetail', component: CycleDetailView },
          { path: 'categories', name: 'CategoryTree', component: CategoryTreeView },
          { path: 'cycles/:cycleId/periods/:periodId/lines',
            name: 'BudgetLines', component: BudgetLinesView },
        ],
      },
    ],
  },
  {
    path: '/login', component: PublicLayout,
    children: [{ path: '', name: 'Login', component: LoginView, meta: { public: true } }],
  },
  {
    path: '/register', component: PublicLayout,
    children: [{ path: '', name: 'Register', component: RegisterView, meta: { public: true } }],
  },
  {
    path: '/invitations/accept', component: PublicLayout,
    children: [{ path: '', name: 'AcceptInvitation', component: AcceptInvitationView, meta: { public: true } }],
  },
]
```

## i18n Key Schema

```
budgetStructure:
  selection:
    title, singleRedirect, noBudgets, selectBudget
  tabs:
    cycles, categories
  cycles:
    title, create, edit, delete, confirmDelete, setActive
    name, startDate, endDate, active, periodCount
    empty: title, description, action
  periods:
    title, create, edit, delete, confirmDelete, changeStatus
    name, startDate, endDate, status
    empty: title, description, action
  categoryGroups:
    title, create, edit, delete, confirmDelete
    name, reorder
    empty: title, description, action
  categories:
    create, edit, delete, confirmDelete, name, reorder
  budgetLines:
    title, create, edit, delete, confirmDelete
    name, lineType, isRecurring, budgetedAmount, currency, note
    types: income, expense
    empty: title, description, action
  common:
    save, cancel, confirm, actions, noPermission
auth:
  languageLabel   # (new — replaces hardcoded "Language")
```

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| Unit | Store actions (load, create, update, delete), `useRoleGate` composable, `DateString` utils | Vitest + mock `http` |
| Component | Form validation, empty states, role-gated button visibility, drag-and-drop emit | @testing-library/vue + Vitest |
| E2E | Full CRUD flow: create cycle → period → category group → category → budget line | Playwright (deferred — requires full Docker stack) |

## Migration / Rollout

No migration required. All changes are additive frontend files plus one backend line (`MapScalarApiReference`). Rollback = revert merge commits.

## PR Slice Forecast

| PR | Scope | Est. lines |
|----|-------|-----------|
| PR1 — Layout infra + fixes | `AppLayout`, `PublicLayout`, `layoutStore`, `notificationStore`, route restructure, i18n `@` fix, RegisterView label fix, Scalar line | ~350 |
| PR2 — Budget selection + store scaffold | `BudgetSelectionView`, `budgetStructure.store` scaffold, `useRoleGate`, `types.ts`, API files (cycles + groups) | ~350 |
| PR3 — Cycles + Periods CRUD | `CycleListView`, `CycleDetailView`, `CycleForm`, `PeriodForm`, cycles/periods API, i18n keys | ~380 |
| PR4 — Categories tree + drag-and-drop | `CategoryTreeView`, `CategoryGroupForm`, `CategoryForm`, `vue-draggable-plus` integration, reorder API | ~320 |
| PR5 — BudgetLines CRUD | `BudgetLinesView`, `BudgetLineModal`, `BudgetLineRow`, `EmptyState`, lines API, i18n keys | ~350 |
| PR6 — Polish + empty states + tests | Unit tests for store/composables, component tests, empty state refinement | ~300 |

**400-line budget risk**: Medium (PR3 is closest at ~380; all others safely under).
**Chained PRs recommended**: Yes.
**Decision needed before apply**: Yes — confirm chain order and branch strategy.

## Open Questions

- [ ] Confirm `vue-draggable-plus` is acceptable as a new dependency (alternative: manual up/down buttons with zero deps)
- [ ] Confirm PR chain targets: feature branch `feature/budget-structure-ui` with child PRs, or direct to main?
