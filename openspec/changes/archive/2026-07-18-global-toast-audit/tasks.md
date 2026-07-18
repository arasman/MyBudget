# Tasks: Global Toast Audit

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 120–160 (additions) + ~20 (deletions in ChangePasswordModal) |
| 400-line budget risk | Low |
| Chained PRs recommended | No |
| Suggested split | Single PR |
| Delivery strategy | ask-on-risk |
| Chain strategy | size-exception (not needed — well under budget) |

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: pending
400-line budget risk: Low

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | All phases (i18n + call sites + migration + tests) | PR 1 | Single PR; ~140 changed lines; well under 400-line budget |

---

## Phase 1: i18n Keys (REQ-I18N-KEYS — prerequisite for all toast call sites)

- [x] 1.1 Add `budgetStructure.selection.renameSuccess` to `frontend/src/i18n/locales/en.json` inside the `budgetStructure.selection` block (after existing `createSuccess`).
- [x] 1.2 Add 7 keys to `budgetMatrix.rows` block in `frontend/src/i18n/locales/en.json`: `createGroupSuccess`, `updateGroupSuccess`, `deleteSuccess`, `restoreSuccess`, `createCategorySuccess`, `updateCategorySuccess`, `createLineSuccess`.
- [x] 1.3 Mirror task 1.1 in `frontend/src/i18n/locales/es.json` (`renameSuccess: "Presupuesto renombrado correctamente"`).
- [x] 1.4 Mirror task 1.2 in `frontend/src/i18n/locales/es.json` (7 keys with Spanish translations per design).

---

## Phase 2: BudgetSelectionView (REQ-TOAST-BUDGET-CREATE, REQ-TOAST-BUDGET-RENAME)

- [x] 2.1 Wire orphaned key: in `frontend/src/features/budget-structure/views/BudgetSelectionView.vue`, add `toastStore.push({ type: 'success', title: t('budgetStructure.selection.createSuccess') })` inside `onBudgetCreated`, after `selectBudget(budget.id, budget.name)`.
- [x] 2.2 Add rename toast: in the same file, add `toastStore.push({ type: 'success', title: t('budgetStructure.selection.renameSuccess') })` inside `saveInlineEdit`, in the `try` block after `inlineEditingBudgetId.value = null`.

---

## Phase 3: BudgetMatrixView Inline Adds (REQ-TOAST-MATRIX-GROUP-CREATE, REQ-TOAST-MATRIX-CAT-CREATE, REQ-TOAST-MATRIX-LINE-CREATE)

- [x] 3.1 In `frontend/src/features/budget-execution/views/BudgetMatrixView.vue`, add `import { useToastStore } from '@/stores/toast.store'` and `const toast = useToastStore()`.
- [x] 3.2 In `confirmAddGroup`, add `toast.push({ type: 'success', title: t('budgetMatrix.rows.createGroupSuccess') })` after `await matrixStore.invalidateAllPeriods()`, before `addingGroup.value = false`.
- [x] 3.3 In `confirmAddCategory`, add `toast.push({ type: 'success', title: t('budgetMatrix.rows.createCategorySuccess') })` after `await matrixStore.invalidateAllPeriods()`, before `addingCategoryForGroup.value = null`.
- [x] 3.4 In `confirmAddLine`, add `toast.push({ type: 'success', title: t('budgetMatrix.rows.createLineSuccess') })` after `await matrixStore.invalidateAllPeriods()`, before `addingLineForCategory.value = null`.

---

## Phase 4: Matrix Row Components (parallelizable — no inter-dependency)

- [x] 4.1 **MatrixGroupRow** (`frontend/src/features/budget-execution/components/MatrixGroupRow.vue`): add `useToastStore` import + `const toast`. Wire `saveEdit` → `updateGroupSuccess`, `doDelete` → `deleteSuccess`, `doRestore` → `restoreSuccess`. All calls inside `try`, after the awaited store action. (REQ-TOAST-MATRIX-GROUP-UPDATE, REQ-TOAST-MATRIX-GROUP-DELETE, REQ-TOAST-MATRIX-GROUP-RESTORE)
- [x] 4.2 **MatrixCategoryRow** (`frontend/src/features/budget-execution/components/MatrixCategoryRow.vue`): add `useToastStore` import + `const toast`. Wire `saveEdit` → `updateCategorySuccess`, `doDelete` → `deleteSuccess`, `doRestore` → `restoreSuccess`. (REQ-TOAST-MATRIX-CAT-UPDATE, REQ-TOAST-MATRIX-CAT-DELETE, REQ-TOAST-MATRIX-CAT-RESTORE)
- [x] 4.3 **MatrixLineRow** (`frontend/src/features/budget-execution/components/MatrixLineRow.vue`): add `useToastStore` import + `const toast`. Wire `doDelete` → `deleteSuccess`, `doRestore` → `restoreSuccess`. (REQ-TOAST-MATRIX-LINE-DELETE, REQ-TOAST-MATRIX-LINE-RESTORE)

---

## Phase 5: ChangePasswordModal Migration (REQ-TOAST-NOTIFICATION-MIGRATION)

- [x] 5.1 In `frontend/src/components/auth/ChangePasswordModal.vue`: replace `import { useNotificationStore } from '@/stores/notification.store'` with `import { useToastStore } from '@/stores/toast.store'`.
- [x] 5.2 In the same file: replace `const notificationStore = useNotificationStore()` with `const toast = useToastStore()`.
- [x] 5.3 In the same file: replace `notificationStore.push({ type: 'success', title: t('auth.password.changeSuccess'), message: '' })` with `toast.push({ type: 'success', title: t('auth.password.changeSuccess') })`.

---

## Phase 6: Tests (unit + integration + frontend component — no E2E)

- [x] 6.1 **Integration — i18n keys**: add test in `frontend/src/i18n/` (or nearest `__tests__` dir) asserting all 8 new keys exist in both `en.json` and `es.json` (covers REQ-I18N-KEYS scenario: "All new keys present in both locales").
- [x] 6.2 **Component — BudgetSelectionView**: in the existing component test file, add two cases: (a) `onBudgetCreated` calls `toast.push` with `createSuccess` key; (b) `saveInlineEdit` success calls `toast.push` with `renameSuccess` key. Mock `useToastStore`. (REQ-TOAST-BUDGET-CREATE, REQ-TOAST-BUDGET-RENAME)
- [x] 6.3 **Component — BudgetMatrixView**: add three cases: `confirmAddGroup`, `confirmAddCategory`, `confirmAddLine` each call `toast.push` with the correct key on success. Mock `useToastStore` + `matrixStore`. (REQ-TOAST-MATRIX-GROUP-CREATE, REQ-TOAST-MATRIX-CAT-CREATE, REQ-TOAST-MATRIX-LINE-CREATE)
- [x] 6.4 **Component — MatrixGroupRow**: add three cases: `saveEdit`, `doDelete`, `doRestore` each call `toast.push` with the correct key. Assert toast is NOT called when the store action throws. (REQ-TOAST-MATRIX-GROUP-UPDATE, DELETE, RESTORE)
- [x] 6.5 **Component — MatrixCategoryRow**: same pattern as 6.4 for `saveEdit`, `doDelete`, `doRestore`. (REQ-TOAST-MATRIX-CAT-UPDATE, DELETE, RESTORE)
- [x] 6.6 **Component — MatrixLineRow**: add two cases for `doDelete` and `doRestore`. (REQ-TOAST-MATRIX-LINE-DELETE, RESTORE)
- [x] 6.7 **Component — ChangePasswordModal**: assert `toast.push` is called with `{ type: 'success', title: t('auth.password.changeSuccess') }` on successful submit, AND that `useNotificationStore` is not imported/called. (REQ-TOAST-NOTIFICATION-MIGRATION)
