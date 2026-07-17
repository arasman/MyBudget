# Spec: Budget Structure UI E2E Test Debt

**Change name**: `budget-structure-ui-e2e-debt`
**Status**: Draft
**Date**: 2026-07-17

---

## 1. Capability Index

| ID | Capability | Domain | Phase |
|----|------------|--------|-------|
| CAP-TOAST-AUDIT | All CRUD operations for all 5 entity types fire a success toast | Frontend views (CycleListView, CycleDetailView, CategoryTreeView, BudgetLinesView) | 1 |
| CAP-TOAST-E2E | Existing CRUD E2E tests assert toast appearance and resolved text | E2E — all 4 spec files | 2 |
| CAP-TOGGLE-E2E | Toggle show-deleted ON/OFF controls item visibility in the list | E2E — all 4 spec files | 3 |
| CAP-RESTORE-E2E | Restore action returns a soft-deleted item to the active list and fires success toast | E2E — all 4 spec files | 3 |
| CAP-RESTORE-PERIOD-CASCADE | Period restore discloses cascade count and requires explicit user confirmation | E2E — budget-structure-periods.spec.ts | 3 |
| CAP-SEED-HELPERS | `helpers.ts` exposes seed-deleted-entity helpers for E2E setup | E2E helpers | 3 |

---

## 2. Phase 1 — Toast Audit and Fix

### 2.1 Audit Findings (confirmed from source)

The audit of view files reveals which toast calls are present and which are missing.

| Entity | Operation | Toast Present? | i18n Key (en resolved text) |
|--------|-----------|---------------|------------------------------|
| Cycle | create | YES | `budgetStructure.cycles.createSuccess` → "Cycle created successfully" |
| Cycle | edit/rename | **NO** | `budgetStructure.cycles.createSuccess` (none exists for edit — needs new key or reuse) |
| Cycle | set-active | **NO** | (no key exists — needs new key or assumed in-scope fix) |
| Cycle | delete | YES | `budgetStructure.cycles.deleteSuccess` → "Cycle deleted successfully" |
| Cycle | restore | YES | `budgetStructure.cycles.restoreSuccess` → "Cycle restored successfully" |
| Period | create | YES | `budgetStructure.periods.createSuccess` → "Period created successfully" |
| Period | edit/rename | **NO** | (no key exists for edit) |
| Period | patch-status | **NO** | (no key exists for status change) |
| Period | delete | YES | `budgetStructure.periods.deleteSuccess` → "Period deleted successfully" |
| Period | restore | YES | `budgetStructure.periods.restoreSuccess` → "Period restored successfully" |
| CategoryGroup | create | YES | `budgetStructure.categoryGroups.createSuccess` → "Category group created successfully" |
| CategoryGroup | edit/rename | **NO** | (no key exists for edit) |
| CategoryGroup | delete | YES | `budgetStructure.categoryGroups.deleteSuccess` → "Category group deleted successfully" |
| CategoryGroup | restore | YES | `budgetStructure.categoryGroups.restoreSuccess` → "Category group restored successfully" |
| Category | create | YES | `budgetStructure.categories.createSuccess` → "Category created successfully" |
| Category | edit/rename | **NO** | (no key exists for edit) |
| Category | delete | YES | `budgetStructure.categories.deleteSuccess` → "Category deleted successfully" |
| Category | restore | YES | `budgetStructure.categories.restoreSuccess` → "Category restored successfully" |
| BudgetLine | create | YES | `budgetStructure.budgetLines.createSuccess` → "Budget line created successfully" |
| BudgetLine | edit/inline-edit | **NO** | (no key exists for edit) |
| BudgetLine | delete | YES | `budgetStructure.budgetLines.deleteSuccess` → "Budget line deleted successfully" |
| BudgetLine | restore | YES | `budgetStructure.budgetLines.restoreSuccess` → "Budget line restored successfully" |

### 2.2 Requirements

#### REQ-TOAST-1: Cycle edit/rename fires success toast
Every successful call to `updateCycle` in `CycleListView` MUST push a `success` toast with resolved text matching the i18n key `budgetStructure.cycles.updateSuccess` (new key to be added to `en.json`).

**Acceptance criteria:**
- `en.json` gains key `budgetStructure.cycles.updateSuccess` with English text `"Cycle updated successfully"`.
- `CycleListView` calls `toastStore.push(...)` after `updateCycle` resolves.
- No toast is pushed when `updateCycle` throws.

#### REQ-TOAST-2: Cycle set-active fires success toast
Every successful call to `setActiveCycle` in `CycleListView` MUST push a `success` toast.

**Acceptance criteria:**
- `en.json` gains key `budgetStructure.cycles.setActiveSuccess` with English text `"Cycle set as active"`.
- `CycleListView` calls `toastStore.push(...)` after `setActiveCycle` resolves.

#### REQ-TOAST-3: Period edit/rename fires success toast
Every successful call to `updatePeriod` in `CycleDetailView` MUST push a `success` toast.

**Acceptance criteria:**
- `en.json` gains key `budgetStructure.periods.updateSuccess` with English text `"Period updated successfully"`.
- `CycleDetailView` calls `toastStore.push(...)` after `updatePeriod` resolves.

#### REQ-TOAST-4: Period patch-status fires success toast
Every successful call to `patchPeriodStatus` in `CycleDetailView` MUST push a `success` toast.

**Acceptance criteria:**
- `en.json` gains key `budgetStructure.periods.statusSuccess` with English text `"Period status updated"`.
- `CycleDetailView` calls `toastStore.push(...)` after `patchPeriodStatus` resolves.

#### REQ-TOAST-5: CategoryGroup edit/rename fires success toast
Every successful call to `updateGroup` in `CategoryTreeView` MUST push a `success` toast.

**Acceptance criteria:**
- `en.json` gains key `budgetStructure.categoryGroups.updateSuccess` with English text `"Category group updated successfully"`.
- `CategoryTreeView` calls `toastStore.push(...)` after `updateGroup` resolves.

#### REQ-TOAST-6: Category edit/rename fires success toast
Every successful call to `updateCategory` in `CategoryTreeView` MUST push a `success` toast.

**Acceptance criteria:**
- `en.json` gains key `budgetStructure.categories.updateSuccess` with English text `"Category updated successfully"`.
- `CategoryTreeView` calls `toastStore.push(...)` after `updateCategory` resolves.

#### REQ-TOAST-7: BudgetLine edit/inline-edit fires success toast
Every successful call to `updateLine` in `BudgetLinesView` MUST push a `success` toast (both modal-edit and inline-edit paths).

**Acceptance criteria:**
- `en.json` gains key `budgetStructure.budgetLines.updateSuccess` with English text `"Budget line updated successfully"`.
- `BudgetLinesView` calls `toastStore.push(...)` after `updateLine` resolves.

---

## 3. Phase 2 — Retrofit Toast Assertions into Existing CRUD Tests

### 3.1 Requirements

#### REQ-E2E-TOAST-1: Cycle CRUD test asserts toasts
The existing test `'create cycle → edit → set active → delete'` in `budget-structure-cycles.spec.ts` MUST assert:
- After create: a `role=alert` element is visible containing text `"Cycle created successfully"`.
- After edit/rename: a `role=alert` element is visible containing text `"Cycle updated successfully"`.
- After set-active (when button is enabled): a `role=alert` element is visible containing text `"Cycle set as active"`.
- After delete: a `role=alert` element is visible containing text `"Cycle deleted successfully"`.

#### REQ-E2E-TOAST-2: Period CRUD test asserts toasts
The existing test `'create period → change status → delete'` in `budget-structure-periods.spec.ts` MUST assert:
- After create: a `role=alert` element is visible containing `"Period created successfully"`.
- After status change: a `role=alert` element is visible containing `"Period status updated"`.
- After delete: a `role=alert` element is visible containing `"Period deleted successfully"`.

#### REQ-E2E-TOAST-3: Category CRUD test asserts toasts
The existing test `'create group → add categories → delete category → delete group'` in `budget-structure-categories.spec.ts` MUST assert:
- After create group: `role=alert` contains `"Category group created successfully"`.
- After create first category: `role=alert` contains `"Category created successfully"`.
- After delete category: `role=alert` contains `"Category deleted successfully"`.
- After delete group: `role=alert` contains `"Category group deleted successfully"`.

#### REQ-E2E-TOAST-4: BudgetLine CRUD test asserts toasts
The existing test `'create line → edit via dblclick → delete'` in `budget-structure-lines.spec.ts` MUST assert:
- After create: `role=alert` contains `"Budget line created successfully"`.
- After inline edit: `role=alert` contains `"Budget line updated successfully"`.
- After delete: `role=alert` contains `"Budget line deleted successfully"`.

### 3.2 Toast selector contract
- Toast elements MUST be located via `page.getByRole('alert')`.
- Text MUST be asserted as the i18n-resolved English string, NOT the i18n key.
- Assertions MUST use `toBeVisible({ timeout: 5_000 })` to tolerate async rendering.
- Each assertion refers to the most recently pushed toast; if prior toasts are still visible, filter by text content.

---

## 4. Phase 3 — Soft-Delete/Restore E2E Describe Blocks

### 4.1 Seed Helpers Requirements

#### REQ-SEED-1: helpers.ts gains seed-deleted-entity functions
`helpers.ts` MUST export the following async functions, each accepting `(page, budgetId, token, ...)` and returning the created entity's `id`:

| Function | Creates | Then soft-deletes via |
|----------|---------|----------------------|
| `seedDeletedCycle(page, budgetId, token)` | Cycle via `POST /api/budgets/:id/cycles` | `DELETE /api/budgets/:id/cycles/:cycleId` |
| `seedDeletedPeriod(page, budgetId, cycleId, token)` | Period via `POST .../periods` | `DELETE .../periods/:periodId` |
| `seedDeletedCategoryGroup(page, budgetId, token)` | CategoryGroup via `POST .../category-groups` | `DELETE .../category-groups/:groupId` |
| `seedDeletedCategory(page, budgetId, groupId, token)` | Category via `POST .../categories` | `DELETE .../categories/:categoryId` |
| `seedDeletedBudgetLine(page, budgetId, periodId, token)` | BudgetLine via `POST .../lines` | `DELETE .../lines/:lineId` |

Each function MUST:
- Assert the create response is `201`.
- Assert the delete response is `204`.
- Return the entity `id` string.

### 4.2 Toggle E2E Requirements

#### REQ-TOGGLE-1: Show-deleted toggle ON reveals soft-deleted items in the list
For each entity domain (Cycles, Periods, CategoryGroups+Categories, BudgetLines):
- A soft-deleted entity seeded via API MUST NOT appear in the list while the toggle is OFF.
- After toggling the show-deleted toggle ON, the soft-deleted entity MUST appear in the list.

#### REQ-TOGGLE-2: Show-deleted toggle OFF hides soft-deleted items
After toggling ON (item visible), toggling the show-deleted toggle OFF MUST cause the soft-deleted item to disappear from the list.

#### REQ-TOGGLE-3: Toggle selector is role/data-testid based
The toggle element MUST be located via `page.getByRole('switch', { name: /show deleted/i })` or `page.getByTestId('show-deleted-toggle')`. No CSS class selectors are permitted.

#### REQ-TOGGLE-4: Active (non-deleted) items remain visible regardless of toggle state
When the toggle is ON or OFF, items without `deletedAt` MUST remain visible.

### 4.3 Restore E2E Requirements

#### REQ-RESTORE-1: Restore button on a deleted item fires restore action and item reappears active
For Cycle, CategoryGroup, Category, and BudgetLine (non-period entities):
- A soft-deleted entity is visible in the list (toggle ON).
- Clicking the restore button triggers the restore API call.
- The entity MUST appear in the active list (present with toggle OFF).
- A success toast MUST be visible with the entity-appropriate resolved text.

#### REQ-RESTORE-2: Period restore — confirm path triggers restore
For Period:
- A soft-deleted period is visible in the list (toggle ON).
- Clicking the restore button opens a confirmation dialog disclosing the cascade (or zero) count.
- Clicking Confirm in the dialog triggers `restorePeriod`.
- The period MUST appear in the active list (visible with toggle OFF).
- A success toast MUST be visible with text `"Period restored successfully"`.

#### REQ-RESTORE-3: Period restore — cancel path does NOT restore
- Same setup as REQ-RESTORE-2 up to clicking the restore button.
- Clicking Cancel in the confirmation dialog MUST NOT trigger `restorePeriod`.
- The period MUST remain absent from the active list (toggle OFF).
- No success toast for period restore appears.

#### REQ-RESTORE-4: Success toast text after restore matches i18n resolution
| Entity | Expected toast text |
|--------|-------------------|
| Cycle | "Cycle restored successfully" |
| Period | "Period restored successfully" |
| CategoryGroup | "Category group restored successfully" |
| Category | "Category restored successfully" |
| BudgetLine | "Budget line restored successfully" |

### 4.4 Scope Constraints
- Visual distinction of deleted items in the list is tested only by presence/absence — no CSS class assertions.
- Session persistence of toggle state across navigation is OUT OF SCOPE.
- Multi-budget context tests are OUT OF SCOPE.
- Error-path toast suppression is OUT OF SCOPE.

---

## 5. Acceptance Scenarios

### Phase 1

#### SCENARIO-TOAST-1.1: Cycle updated toast fires
```
Given a cycle exists in the list
When the user edits the cycle name and clicks Save
Then a success toast appears with text "Cycle updated successfully"
And the toast auto-dismisses after ~3 seconds
```

#### SCENARIO-TOAST-1.2: Cycle set-active toast fires
```
Given an inactive cycle exists in the list
When the user clicks "Set as Active" and the action completes
Then a success toast appears with text "Cycle set as active"
```

#### SCENARIO-TOAST-1.3: Period edit toast fires
```
Given a period exists in the cycle detail view
When the user edits the period name and clicks Save
Then a success toast appears with text "Period updated successfully"
```

#### SCENARIO-TOAST-1.4: Period status change toast fires
```
Given an open period exists in the cycle detail view
When the user changes status to Closed and clicks Save
Then a success toast appears with text "Period status updated"
```

#### SCENARIO-TOAST-1.5: CategoryGroup edit toast fires
```
Given a category group exists on the categories page
When the user edits the group name and clicks Save
Then a success toast appears with text "Category group updated successfully"
```

#### SCENARIO-TOAST-1.6: Category edit toast fires
```
Given a category exists in a group
When the user edits the category name and clicks Save
Then a success toast appears with text "Category updated successfully"
```

#### SCENARIO-TOAST-1.7: BudgetLine edit toast fires
```
Given a budget line exists in the lines view
When the user edits the line (via modal or inline dblclick) and saves
Then a success toast appears with text "Budget line updated successfully"
```

---

### Phase 2

#### SCENARIO-E2E-TOAST-2.1: Cycle CRUD test includes toast assertions
```
Given the cycles CRUD test runs against a fresh user
When create cycle succeeds
Then page.getByRole('alert') is visible with text "Cycle created successfully"

When edit cycle succeeds
Then page.getByRole('alert') is visible with text "Cycle updated successfully"

When set-active cycle succeeds (button enabled)
Then page.getByRole('alert') is visible with text "Cycle set as active"

When delete cycle is confirmed
Then page.getByRole('alert') is visible with text "Cycle deleted successfully"
```

#### SCENARIO-E2E-TOAST-2.2: Period CRUD test includes toast assertions
```
Given the periods CRUD test runs with an API-seeded cycle
When create period succeeds
Then role=alert visible with "Period created successfully"

When status changed to Closed
Then role=alert visible with "Period status updated"

When delete period confirmed
Then role=alert visible with "Period deleted successfully"
```

#### SCENARIO-E2E-TOAST-2.3: Category CRUD test includes toast assertions
```
Given the categories CRUD test runs
When create group succeeds
Then role=alert visible with "Category group created successfully"

When create category "Food" succeeds
Then role=alert visible with "Category created successfully"

When delete category confirmed
Then role=alert visible with "Category deleted successfully"

When delete group confirmed
Then role=alert visible with "Category group deleted successfully"
```

#### SCENARIO-E2E-TOAST-2.4: BudgetLine CRUD test includes toast assertions
```
Given the lines CRUD test runs with API-seeded cycle, period, group
When create line succeeds
Then role=alert visible with "Budget line created successfully"

When inline edit (dblclick → save) succeeds
Then role=alert visible with "Budget line updated successfully"

When delete line confirmed
Then role=alert visible with "Budget line deleted successfully"
```

---

### Phase 3 — Toggle

#### SCENARIO-TOGGLE-3.1: Deleted cycle hidden by default; shown when toggle ON
```
Given a user is logged in with a fresh budget
And seedDeletedCycle() has created and soft-deleted a cycle via API
When the user navigates to /budgets/:id/cycles
Then the deleted cycle is NOT visible in the list

When the user activates the show-deleted toggle
Then the deleted cycle IS visible in the list
```

#### SCENARIO-TOGGLE-3.2: Deleted cycle hidden again when toggle OFF
```
Given the show-deleted toggle is ON and a deleted cycle is visible
When the user deactivates the show-deleted toggle
Then the deleted cycle is NOT visible in the list
And active cycles remain visible
```

#### SCENARIO-TOGGLE-3.3: Deleted period hidden by default; shown when toggle ON
```
Given a cycle exists with a soft-deleted period seeded via API
When the user navigates to /budgets/:id/cycles/:cycleId
Then the deleted period is NOT visible

When the user activates the show-deleted toggle
Then the deleted period IS visible
```

#### SCENARIO-TOGGLE-3.4: Deleted period hidden when toggle OFF
```
Given the show-deleted toggle is ON and a deleted period is visible
When the user deactivates the toggle
Then the deleted period is NOT visible
```

#### SCENARIO-TOGGLE-3.5: Deleted category group hidden by default; shown when toggle ON
```
Given a soft-deleted category group is seeded via API
When the user navigates to /budgets/:id/categories
Then the deleted group is NOT visible

When the user activates the show-deleted toggle
Then the deleted group IS visible
```

#### SCENARIO-TOGGLE-3.6: Deleted category hidden by default; shown when toggle ON
```
Given a soft-deleted category is seeded inside a group via API
When the user navigates to /budgets/:id/categories
Then the deleted category is NOT visible (even if its group is visible)

When the user activates the show-deleted toggle
Then the deleted category IS visible under its group
```

#### SCENARIO-TOGGLE-3.7: Deleted budget line hidden by default; shown when toggle ON
```
Given a soft-deleted budget line is seeded via API within a period
When the user navigates to .../lines
Then the deleted line is NOT visible

When the user activates the show-deleted toggle
Then the deleted line IS visible
```

---

### Phase 3 — Restore

#### SCENARIO-RESTORE-3.1: Restore cycle reappears in active list with success toast
```
Given a soft-deleted cycle exists (seeded via API)
And the show-deleted toggle is ON (deleted cycle visible)
When the user clicks the Restore button for the cycle
Then the restore API is called
And a success toast appears with text "Cycle restored successfully"
And after toggling show-deleted OFF, the restored cycle IS visible in the active list
```

#### SCENARIO-RESTORE-3.2: Period restore — confirm path completes restore
```
Given a soft-deleted period exists in the cycle detail view (toggle ON)
When the user clicks the Restore button for the period
Then a confirmation dialog appears
When the user clicks Confirm
Then the restore API is called
And a success toast appears with text "Period restored successfully"
And after toggling show-deleted OFF, the restored period IS visible
```

#### SCENARIO-RESTORE-3.3: Period restore — cancel path aborts restore
```
Given a soft-deleted period exists in the cycle detail view (toggle ON)
When the user clicks the Restore button for the period
And the confirmation dialog appears
When the user clicks Cancel
Then the restore API is NOT called
And the period remains absent from the active list (toggle OFF)
And no period-restore success toast appears
```

#### SCENARIO-RESTORE-3.4: Restore category group reappears with success toast
```
Given a soft-deleted category group exists (seeded via API, toggle ON)
When the user clicks the Restore button for the group
Then the restore API is called
And a success toast appears with text "Category group restored successfully"
And the group IS visible in the active list (toggle OFF)
```

#### SCENARIO-RESTORE-3.5: Restore category reappears with success toast
```
Given a soft-deleted category exists in a visible group (seeded via API, toggle ON)
When the user clicks the Restore button for the category
Then the restore API is called
And a success toast appears with text "Category restored successfully"
And the category IS visible under its group (toggle OFF)
```

#### SCENARIO-RESTORE-3.6: Restore budget line reappears with success toast
```
Given a soft-deleted budget line exists in the lines view (seeded via API, toggle ON)
When the user clicks the Restore button for the line
Then the restore API is called
And a success toast appears with text "Budget line restored successfully"
And the line IS visible in the active list (toggle OFF)
```

---

## 6. New i18n Keys Required

The following keys MUST be added to `src/i18n/locales/en.json` (and any other locale files) before Phase 1 toast calls can be tested:

| i18n key | English text |
|---------|-------------|
| `budgetStructure.cycles.updateSuccess` | `"Cycle updated successfully"` |
| `budgetStructure.cycles.setActiveSuccess` | `"Cycle set as active"` |
| `budgetStructure.periods.updateSuccess` | `"Period updated successfully"` |
| `budgetStructure.periods.statusSuccess` | `"Period status updated"` |
| `budgetStructure.categoryGroups.updateSuccess` | `"Category group updated successfully"` |
| `budgetStructure.categories.updateSuccess` | `"Category updated successfully"` |
| `budgetStructure.budgetLines.updateSuccess` | `"Budget line updated successfully"` |

---

## 7. Files to Modify

| File | Change |
|------|--------|
| `src/i18n/locales/en.json` | Add 7 new i18n keys (see §6) |
| `src/features/budget-structure/views/CycleListView.vue` | Add `updateSuccess` and `setActiveSuccess` toast calls |
| `src/features/budget-structure/views/CycleDetailView.vue` | Add `updateSuccess` and `statusSuccess` toast calls |
| `src/features/budget-structure/views/CategoryTreeView.vue` | Add `updateSuccess` toast calls for group and category |
| `src/features/budget-structure/views/BudgetLinesView.vue` | Add `updateSuccess` toast call for line edit paths |
| `e2e/budget-structure/helpers.ts` | Add 5 `seedDeleted*` functions |
| `e2e/budget-structure/budget-structure-cycles.spec.ts` | Retrofit toast assertions + add soft-delete/restore describe block |
| `e2e/budget-structure/budget-structure-periods.spec.ts` | Retrofit toast assertions + add soft-delete/restore describe block |
| `e2e/budget-structure/budget-structure-categories.spec.ts` | Retrofit toast assertions + add soft-delete/restore describe block |
| `e2e/budget-structure/budget-structure-lines.spec.ts` | Retrofit toast assertions + add soft-delete/restore describe block |

---

## 8. Out of Scope

- CSS class assertions for visual distinction of deleted items
- Session-scoped toggle persistence across navigation
- Multi-budget context tests
- Error-path toast suppression
- Budget-execution E2E debt (separate change)
- Locale files other than `en.json` (separate i18n change)
