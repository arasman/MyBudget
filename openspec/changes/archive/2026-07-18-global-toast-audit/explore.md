# Exploration: global-toast-audit

## Current State

Toast system built around `useToastStore()` at `src/stores/toast.store.ts`. Single entry point: `push({ type, title, message?, autoDismiss? })`. All existing toast calls are in component `<script setup>` blocks — never in store actions or composables. Call shape throughout: `push({ type: 'success', title: t('i18n.key') })` — no `message`, no custom `autoDismiss`.

**Anomaly**: `ChangePasswordModal.vue` uses `notificationStore` instead of `toastStore` — inconsistent with the rest of the app.
**Anomaly**: `InviteUserModal.vue` uses an inline `successMessage` ref + auto-close — no toast at all.

---

## Slice-by-Slice Inventory Table

| Slice | Entity | Operation | Toast? | Location |
|-------|--------|-----------|--------|----------|
| budget-structure | budget | create | **MISSING** | `BudgetSelectionView.vue:220` (key exists, never called) |
| budget-structure | budget | rename | **MISSING** | `BudgetSelectionView.vue:237` (no key) |
| budget-structure | budget | delete | YES | `BudgetSelectionView.vue:276` |
| budget-structure | budget | restore | YES | `BudgetSelectionView.vue:289` |
| budget-structure | cycle | create | YES | `CycleListView.vue:330` |
| budget-structure | cycle | update | YES | `CycleListView.vue:276,327` |
| budget-structure | cycle | delete | YES | `CycleListView.vue:304` |
| budget-structure | cycle | restore | YES | `CycleListView.vue:309` |
| budget-structure | cycle | set-active | YES | `CycleListView.vue:314` |
| budget-structure | period | create | YES | `CycleDetailView.vue:437` |
| budget-structure | period | update | YES | `CycleDetailView.vue:334,430` |
| budget-structure | period | delete | YES | `CycleDetailView.vue:374` |
| budget-structure | period | restore | YES | `CycleDetailView.vue:405` |
| budget-structure | period | change-status | YES | `CycleDetailView.vue:415` |
| budget-structure | categoryGroup | create | YES | `CategoryTreeView.vue:473` |
| budget-structure | categoryGroup | update | YES | `CategoryTreeView.vue:336,475` |
| budget-structure | categoryGroup | delete | YES | `CategoryTreeView.vue:397` |
| budget-structure | categoryGroup | restore | YES | `CategoryTreeView.vue:402` |
| budget-structure | category | create | YES | `CategoryTreeView.vue:484` |
| budget-structure | category | update | YES | `CategoryTreeView.vue:352,486` |
| budget-structure | category | delete | YES | `CategoryTreeView.vue:460` |
| budget-structure | category | restore | YES | `CategoryTreeView.vue:465` |
| budget-structure | budgetLine | create | YES | `BudgetLinesView.vue:391,440` |
| budget-structure | budgetLine | update | YES | `BudgetLinesView.vue:388,406` |
| budget-structure | budgetLine | delete | YES | `BudgetLinesView.vue:377` |
| budget-structure | budgetLine | restore | YES | `BudgetLinesView.vue:382` |
| budget-execution | executionRecord | create | YES | `ExecutionRecordForm.vue:282` |
| budget-execution | executionRecord | update | YES | `ExecutionRecordForm.vue:279` |
| budget-execution | executionRecord | delete | YES | `ExecutionRecordRow.vue:205` |
| budget-execution | executionRecord | restore | YES | `ExecutionRecordRow.vue:222` |
| budget-execution (matrix) | group | create | **MISSING** | `BudgetMatrixView.vue:411` |
| budget-execution (matrix) | group | update-name | **MISSING** | `MatrixGroupRow.vue:184` |
| budget-execution (matrix) | group | delete | **MISSING** | `MatrixGroupRow.vue:191` |
| budget-execution (matrix) | group | restore | **MISSING** | `MatrixGroupRow.vue:202` |
| budget-execution (matrix) | category | create | **MISSING** | `BudgetMatrixView.vue:437` |
| budget-execution (matrix) | category | update-name | **MISSING** | `MatrixCategoryRow.vue:178` |
| budget-execution (matrix) | category | delete | **MISSING** | `MatrixCategoryRow.vue:185` |
| budget-execution (matrix) | category | restore | **MISSING** | `MatrixCategoryRow.vue:196` |
| budget-execution (matrix) | line | create | **MISSING** | `BudgetMatrixView.vue:465` |
| budget-execution (matrix) | line | delete | **MISSING** | `MatrixLineRow.vue:172` |
| budget-execution (matrix) | line | restore | **MISSING** | `MatrixLineRow.vue:183` |
| auth | password | change | **INCONSISTENT** | `ChangePasswordModal.vue:54` (notificationStore ≠ toastStore) |
| budget-members | invitation | send | MISSING (low-pri) | `InviteUserModal.vue:61` (inline successMessage) |

**Summary: 13 MISSING + 1 INCONSISTENT out of 43 total operations.**

---

## i18n Key Inventory

### Keys that EXIST and ARE used

All keys under:
- `budgetStructure.cycles.*Success`
- `budgetStructure.periods.*Success`
- `budgetStructure.categoryGroups.*Success`
- `budgetStructure.categories.*Success`
- `budgetStructure.budgetLines.*Success`
- `budgetStructure.selection.deleteSuccess`, `restoreSuccess`
- `budgetExecution.record.*Success`

Present in both `en.json` and `es.json`.

### Orphaned key (EXISTS but NEVER called)

- `budgetStructure.selection.createSuccess` — "Budget created successfully" — exists in en.json:209 + es.json. Never wired up: `onBudgetCreated` navigates away without firing a toast.

### Missing keys (need to be added to both locales)

- `budgetStructure.selection.renameSuccess`
- `budgetMatrix.rows.createGroupSuccess`
- `budgetMatrix.rows.updateGroupSuccess`
- `budgetMatrix.rows.deleteSuccess` (shared: group/category/line delete)
- `budgetMatrix.rows.restoreSuccess` (shared: group/category/line restore)
- `budgetMatrix.rows.createCategorySuccess`
- `budgetMatrix.rows.updateCategorySuccess`
- `budgetMatrix.rows.createLineSuccess`

---

## Toast Call Pattern

```ts
// Existing pattern (from BudgetSelectionView.vue)
const toast = useToastStore()
// ...
toast.push({ type: 'success', title: t('budgetStructure.selection.deleteSuccess') })
```

All calls: `push({ type: 'success', title: t('<key>') })` — no `message`, no custom `autoDismiss`.

---

## Approaches

| Approach | Scope | Effort | Recommendation |
|----------|-------|--------|----------------|
| A — Fix all MISSING + migrate notificationStore | 13 missing ops + 1 inconsistency | ~15 call sites, ~8 new i18n keys × 2 locales | **Recommended** |
| B — Full coverage including InviteUserModal | Adds invite send toast | Adds 1 call site, 1 key × 2 locales | Optional / low priority |

**Recommendation: Approach A.** Matrix inline ops (11 gaps) are the highest-priority — 11 silent mutations on a complex UI. Budget create (orphaned key) and rename (missing key + no toast) are quick wins. notificationStore migration aligns `ChangePasswordModal` with the established pattern.

---

## Risk Signals

- `MatrixGroupRow`, `MatrixCategoryRow`, `MatrixLineRow` have zero store dependencies beyond `structureStore` — adding `toastStore` is consistent with the pattern but increases coupling slightly.
- `BudgetMatrixView.vue` inline add functions need `useToastStore()` injected and called ONLY on success (not in `finally`).
- `budgetStructure.selection.createSuccess` is an orphaned key — should be wired to `onBudgetCreated` (post-navigation toast, valid because toast store survives router push).
- Both `en.json` and `es.json` must stay in sync — forgetting one locale causes runtime i18n warnings.
- `notificationStore` → `toastStore` migration is a 2-line change; risk is low.
