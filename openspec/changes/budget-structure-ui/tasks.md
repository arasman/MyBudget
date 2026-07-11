# Tasks: Budget Structure UI

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~2,050 (sum of 6 PR slices) |
| 400-line budget risk | Medium (PR3 ~380 is the highest; all others ≤ 350) |
| Chained PRs recommended | Yes |
| Suggested split | PR1 → PR2 → PR3 → PR4 → PR5 → PR6 |
| Delivery strategy | auto-chain |
| Chain strategy | feature-branch-chain |

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: feature-branch-chain
400-line budget risk: Medium

### Suggested Work Units

| Unit | Goal | Base branch | Notes |
|------|------|-------------|-------|
| PR1 | Layout infra + fixes | `feat/budget-structure-ui` | Foundation for all other PRs |
| PR2 | Budget selection + store scaffold | PR1 branch | Types, composables, and API scaffold used by PR3-5 |
| PR3 | Cycles + Periods CRUD | PR2 branch | Depends on store scaffold and types from PR2 |
| PR4 | Categories tree + drag-and-drop | PR3 branch | Depends on category-groups API stub in PR2 |
| PR5 | BudgetLines CRUD | PR4 branch | Depends on store and types; periods API used from PR3 |
| PR6 | Polish + empty states + tests | PR5 branch | All components available; tests run against completed feature |

---

## PR1 — Layout Infra + Fixes (~350 lines)

Satisfies: LAYOUT-1, LAYOUT-2, LAYOUT-3, NAV-1, NAV-2, NAV-3, NAV-4, REQ-FIX-1, REQ-FIX-2, REQ-FIX-3, REG-I18N-1

- [x] 1.1 Create `frontend/src/layouts/` directory (scaffold only; required by LAYOUT-1, LAYOUT-2)
- [x] 1.2 Create `frontend/src/stores/layout.store.ts` — `activeBudgetId`, `activeBudgetName`, `pageActions[]`, `setPageActions()`, `clearPageActions()` (NAV-2)
- [x] 1.3 Create `frontend/src/stores/notification.store.ts` — `notifications[]`, `push()`, `markRead()`, `unreadCount` computed (NAV-3)
- [x] 1.4 Create `frontend/src/layouts/PublicLayout.vue` — centered card, no navbar, `<RouterView>` slot (LAYOUT-2)
- [x] 1.5 Create `frontend/src/layouts/AppLayout.vue` — top navbar, `<RouterView>`, budget switcher (NAV-1), page-actions bar (NAV-2), notification bell + badge (NAV-3), user dropdown with initials + role badge + logout (NAV-4)
- [x] 1.6 Modify `frontend/src/router/index.ts` — nest authenticated routes under `AppLayout`, public routes (`/login`, `/register`, `/invitations/accept`) under `PublicLayout`; `App.vue` retains only `<RouterView>` (LAYOUT-3)
- [x] 1.7 Modify `frontend/src/views/HomeView.vue` — remove inline navbar; redirect to budget selection route (LAYOUT-3)
- [x] 1.8 Modify `frontend/src/views/LoginView.vue` — remove outer div shell that duplicates `PublicLayout` card (LAYOUT-2, REQ-FIX-3)
- [x] 1.9 Modify `frontend/src/views/RegisterView.vue` — remove outer div shell; replace hardcoded `"Language"` string with `$t('auth.register.languageLabel')` (REQ-FIX-3, REG-I18N-1)
- [x] 1.10 Modify `frontend/src/i18n/locales/en.json` lines 13 and 23 — escape bare `@` as `{'@'}` in `auth.login.emailPlaceholder` and `auth.register.emailPlaceholder`; add `auth.register.languageLabel` key (REQ-FIX-2, REG-I18N-1)
- [x] 1.11 Modify `frontend/src/i18n/locales/es.json` lines 13 and 23 — same `{'@'}` escapes + `auth.register.languageLabel` in Spanish (REQ-FIX-2, REG-I18N-1)
- [x] 1.12 Modify `Project/src/MyBudget.Api/Program.cs` — add `app.MapScalarApiReference()` after `app.MapOpenApi()` (REQ-FIX-1)

---

## PR2 — Budget Selection + Store Scaffold (~350 lines)

Satisfies: BUDSEL-1, BUDSEL-2, REQ-NAV-1, and scaffolding used by PR3-5

- [x] 2.1 Create `frontend/src/features/budget-structure/types.ts` — `DateString` branded type, `toDateString()`, `formatDate()`, all entity interfaces: `CycleListItem`, `CycleDetail`, `PeriodSummary`, `CategoryGroupResponse`, `CategoryItem`, `BudgetLineResponse` (ADR-BSUI-04)
- [x] 2.2 Create `frontend/src/features/budget-structure/composables/useRoleGate.ts` — `useRoleGate(budgetId)` returning `{ isAdmin, isOperator, canWriteStructure, canWriteLines }` computed refs from `authStore` (ADR-BSUI-05)
- [x] 2.3 Create `frontend/src/features/budget-structure/api/cycles.api.ts` — `list(budgetId)`, `get(budgetId, cycleId)`, `create(budgetId, payload)`, `update(...)`, `delete(...)`, `setActive(budgetId)` (REQ-CYC-1 through REQ-CYC-5)
- [x] 2.4 Create `frontend/src/features/budget-structure/api/categoryGroups.api.ts` — `list(budgetId)`, `create(...)`, `update(...)`, `delete(...)`, `reorder(budgetId, ids[])` (REQ-CAT-1 through REQ-CAT-3)
- [x] 2.5 Create `frontend/src/features/budget-structure/store.ts` — single Pinia store scaffold: state slices for cycles, periods, groups, categories, lines; action stubs for load/create/update/delete per entity (all REQ-* actions route through this store)
- [x] 2.6 Create `frontend/src/features/budget-structure/components/BudgetTabs.vue` — "Cycles" and "Categories" tabs, URL-driven active state (REQ-NAV-1)
- [x] 2.7 Create `frontend/src/features/budget-structure/views/BudgetSelectionView.vue` — auto-redirect for single membership (BUDSEL-1); list with click-to-navigate for multiple memberships (BUDSEL-2)
- [x] 2.8 Modify `frontend/src/router/index.ts` — add `BudgetSelection` route at `/`, `CycleList` and `CategoryTree` under `/budgets/:budgetId` (LAYOUT-3, REQ-NAV-1)

---

## PR3 — Cycles + Periods CRUD (~380 lines)

Satisfies: REQ-CYC-1, REQ-CYC-2, REQ-CYC-3, REQ-CYC-4, REQ-CYC-5, REQ-PER-1, REQ-PER-2, REQ-PER-3, REQ-PER-4, REQ-PER-5, REQ-I18N-1 (cycles/periods keys)

- [x] 3.1 Create `frontend/src/features/budget-structure/api/periods.api.ts` — `list(budgetId, cycleId)`, `create(...)`, `update(...)`, `patchStatus(...)`, `delete(...)` (REQ-PER-2 through REQ-PER-5)
- [x] 3.2 Extend `store.ts` — implement cycles actions: `loadCycles`, `createCycle`, `updateCycle`, `deleteCycle`, `setActiveCycle`; implement periods actions: `loadPeriods`, `createPeriod`, `updatePeriod`, `patchPeriodStatus`, `deletePeriod` (REQ-CYC-1 through REQ-PER-5)
- [x] 3.3 Create `frontend/src/features/budget-structure/components/CycleForm.vue` — modal form with name, startDate, endDate fields; validates and emits `submit` event (REQ-CYC-2, REQ-CYC-3)
- [x] 3.4 Create `frontend/src/features/budget-structure/components/PeriodForm.vue` — modal form with name, startDate, endDate, status fields; validates and emits `submit` event (REQ-PER-2, REQ-PER-3, REQ-PER-4)
- [x] 3.5 Create `frontend/src/features/budget-structure/views/CycleListView.vue` — list with name/dates/period-count/active badge; registers "New Cycle" page action via `layoutStore` (admin only via `useRoleGate`); edit/delete/set-active row actions; calls `CycleForm` modal; confirmation dialog for delete (REQ-CYC-1 through REQ-CYC-5)
- [x] 3.6 Create `frontend/src/features/budget-structure/views/CycleDetailView.vue` — breadcrumb (Budget > Cycles > [name]); list periods from `CycleDetail.periods`; "New Period" action (admin only); edit/delete/change-status per row; calls `PeriodForm` modal; link to BudgetLines per period (REQ-PER-1 through REQ-PER-5)
- [x] 3.7 Add `budgetStructure.cycles.*` and `budgetStructure.periods.*` i18n keys to `en.json` and `es.json` (REQ-I18N-1)

---

## PR4 — Categories Tree + Drag-and-Drop (~320 lines)

Satisfies: REQ-CAT-1, REQ-CAT-2, REQ-CAT-3, REQ-CAT-4, REQ-CAT-5, REQ-I18N-1 (categories keys)

- [x] 4.1 Install `vue-draggable-plus` dependency in `frontend/package.json` (ADR-BSUI-01)
- [x] 4.2 Create `frontend/src/features/budget-structure/api/categories.api.ts` — `create(budgetId, groupId, payload)`, `update(...)`, `delete(...)`, `reorder(budgetId, groupId, ids[])` (REQ-CAT-4, REQ-CAT-5)
- [x] 4.3 Extend `store.ts` — implement groups actions: `loadGroups`, `createGroup`, `updateGroup`, `deleteGroup`, `reorderGroups`; categories actions: `createCategory`, `updateCategory`, `deleteCategory`, `reorderCategories` (REQ-CAT-1 through REQ-CAT-5)
- [x] 4.4 Create `frontend/src/features/budget-structure/components/CategoryGroupForm.vue` — modal form with name field; create and edit mode; emits `submit` (REQ-CAT-2)
- [x] 4.5 Create `frontend/src/features/budget-structure/components/CategoryForm.vue` — modal form with name field; create and edit mode; emits `submit` (REQ-CAT-4)
- [x] 4.6 Create `frontend/src/features/budget-structure/views/CategoryTreeView.vue` — groups sorted by `displayOrder`; `vue-draggable-plus` wrapping groups list (admin only, calls `reorderGroups` on drop); each group expanded to nested categories list with its own `vue-draggable-plus` (admin only, calls `reorderCategories` on drop); "New Group" page action via `layoutStore`; group and category CRUD via `CategoryGroupForm` / `CategoryForm` modals; confirmation for delete (REQ-CAT-1 through REQ-CAT-5)
- [x] 4.7 Add `budgetStructure.categoryGroups.*` and `budgetStructure.categories.*` i18n keys to `en.json` and `es.json` (REQ-I18N-1)

---

## PR5 — BudgetLines CRUD (~350 lines)

Satisfies: REQ-BL-1, REQ-BL-2, REQ-BL-3, REQ-BL-4, REQ-I18N-1 (budget lines keys)

- [x] 5.1 Create `frontend/src/features/budget-structure/api/budgetLines.api.ts` — `list(budgetId, periodId)`, `create(...)`, `update(...)`, `delete(...)` (REQ-BL-1 through REQ-BL-4)
- [x] 5.2 Extend `store.ts` — implement lines actions: `loadLines`, `createLine`, `updateLine`, `deleteLine` (REQ-BL-1 through REQ-BL-4)
- [x] 5.3 Create `frontend/src/features/budget-structure/components/BudgetLineRow.vue` — table row rendering line fields; `dblclick` emits `edit` event; inline empty row in create mode (operator+ only via `useRoleGate`) (REQ-BL-2, REQ-BL-3)
- [x] 5.4 Create `frontend/src/features/budget-structure/components/BudgetLineModal.vue` — full edit modal pre-populated with line values; submits `PUT` on save; read-only users cannot open (REQ-BL-3)
- [x] 5.5 Create `frontend/src/features/budget-structure/views/BudgetLinesView.vue` — loads lines for `periodId`; renders `BudgetLineRow` list; appends inline create row (operator+ only); handles `dblclick → BudgetLineModal`; confirmation dialog for delete (REQ-BL-1 through REQ-BL-4)
- [x] 5.6 Modify `frontend/src/router/index.ts` — add `BudgetLines` route at `/budgets/:budgetId/cycles/:cycleId/periods/:periodId/lines` (REQ-BL-1)
- [x] 5.7 Add `budgetStructure.budgetLines.*` and `budgetStructure.common.*` i18n keys to `en.json` and `es.json` (REQ-I18N-1)

---

## PR6 — Polish + Empty States + Tests (~300 lines)

Satisfies: REQ-CYC-1 empty state, REQ-CAT-1 empty state, REQ-BL-1 empty state, REQ-NAV-1, NAV-2 through NAV-4, BUDSEL-1, BUDSEL-2 (test coverage), REQ-I18N-1 (selection keys)

- [x] 6.1 Create `frontend/src/features/budget-structure/components/EmptyState.vue` — reusable guided empty-state with `title`, `description`, `actionLabel`, `action` props; used by Cycle, Category, and BudgetLine views (REQ-CYC-1, REQ-CAT-1, REQ-BL-1)
- [x] 6.2 Wire `EmptyState` into `CycleListView.vue` — show when `cycles.length === 0`; action = "New Cycle" (REQ-CYC-1)
- [x] 6.3 Wire `EmptyState` into `CategoryTreeView.vue` — show when `groups.length === 0`; action = "New Group" (REQ-CAT-1)
- [x] 6.4 Wire `EmptyState` into `BudgetLinesView.vue` — show when `lines.length === 0`; action = "New Line" (REQ-BL-1)
- [x] 6.5 Add `budgetStructure.selection.*` i18n keys to `en.json` and `es.json` (verified already added in PR5 task 5.7) (REQ-I18N-1)
- [x] 6.6 Write Vitest unit tests for `useRoleGate.ts` — scenarios: admin/operator/read-only membership returns correct computed values (ADR-BSUI-05)
- [x] 6.7 Write Vitest unit tests for `store.ts` — scenarios: `loadCycles` populates state; `createCycle` appends to list; `deleteCycle` removes from list; same pattern for periods/groups/categories/lines (all REQ-CYC/PER/CAT/BL store actions)
- [x] 6.8 Write Vitest unit tests for `DateString` utils in `types.ts` — `toDateString` and `formatDate` output (ADR-BSUI-04)
- [x] 6.9 Write `@testing-library/vue` component tests for `CycleListView.vue` — scenarios: cycles listed, empty state shown, non-admin sees no create button (REQ-CYC-1, REQ-CYC-2)
- [x] 6.10 Write `@testing-library/vue` component tests for `CategoryTreeView.vue` — scenarios: tree renders groups + categories, drag-and-drop emit fires reorder action, non-admin cannot drag (REQ-CAT-1, REQ-CAT-3, REQ-CAT-5)
- [x] 6.11 Write `@testing-library/vue` component tests for `BudgetLinesView.vue` — scenarios: lines listed, operator sees inline row, read-only user does not, dblclick opens modal (REQ-BL-1, REQ-BL-2, REQ-BL-3)
- [x] 6.12 Write `@testing-library/vue` component tests for `AppLayout.vue` — scenarios: initials derived correctly, badge shown when unreadCount > 0, page actions render and clear on route change (LAYOUT-1, NAV-2, NAV-3, NAV-4)

---

## E2E Tests — Playwright (PR6 addition)

- [x] E2E-1 Write Playwright E2E test: full cycle CRUD flow (create cycle → edit → set active → delete)
- [x] E2E-2 Write Playwright E2E test: period management (open cycle detail → create period → change status → delete)
- [x] E2E-3 Write Playwright E2E test: category structure (create group → add categories → drag reorder → delete)
- [x] E2E-4 Write Playwright E2E test: budget lines (navigate to period lines → create line → edit via dblclick → delete)
- [x] E2E-5 Write Playwright E2E test: role gating (login as read-only user → verify no write buttons visible)
