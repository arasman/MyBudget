# ephemeral-toast Specification

## Purpose

Defines the ephemeral toast overlay system — a transient, auto-dismissing feedback layer separate
from the persistent notification bell. Used to confirm create/delete/restore actions across all
entities.

---

## Requirements

### Requirement: REQ-TOAST-1 — Toast Store

The system MUST provide a `useToastStore` Pinia store (or equivalent module-level reactive
singleton) that manages a list of active toast messages. Each toast MUST have: `id` (auto-generated),
`message` (string), `type` (`success` | `error` | `info`), and `autoDismiss` (ms, default 3000).

#### Scenario: Push and auto-dismiss

- GIVEN the toast store is initialized
- WHEN `push({ message: "Done", type: "success" })` is called
- THEN a toast entry appears in the list
- AND it is removed automatically after 3000 ms

#### Scenario: Manual close removes toast

- GIVEN a toast is visible
- WHEN the user clicks its × button
- THEN the toast is removed immediately without waiting for the timer

#### Scenario: Multiple toasts stack

- GIVEN two pushes with no dismissal between them
- WHEN the component renders
- THEN both toasts are visible simultaneously (stacked)

---

### Requirement: REQ-TOAST-2 — AppToast Component

`AppToast.vue` MUST be mounted once in `AppLayout.vue`. It MUST render all active toasts as a
stack using DaisyUI `toast` + `alert` classes. Each toast MUST include a visible × close button.
The component MUST be positioned at a z-index above modals so toasts remain visible when a modal
is open.

#### Scenario: Renders at correct z-index

- GIVEN a modal is open in the application
- WHEN a toast is pushed
- THEN the toast overlay appears above the modal without being clipped

#### Scenario: Close button on each toast

- GIVEN two stacked toasts are visible
- WHEN the user clicks × on the first toast
- THEN only that toast is removed; the second toast remains

---

### Requirement: REQ-TOAST-3 — Bell Exclusion

Toasts pushed via `useToastStore` MUST NOT be written to `useNotificationStore`. The notification
bell dropdown MUST only contain persistent notifications. Auto-dismiss toasts MUST NOT accumulate
in the bell inbox.

#### Scenario: Toast does not appear in bell

- GIVEN a delete success toast is pushed via useToastStore
- WHEN the user opens the notification bell dropdown
- THEN no entry corresponding to that toast appears in the bell list

#### Scenario: Existing bell notifications unaffected

- GIVEN a persistent notification exists in the bell
- WHEN a toast is pushed and auto-dismissed
- THEN the bell notification count is unchanged

---

### Requirement: REQ-TOAST-BUDGET-CREATE

`BudgetSelectionView.onBudgetCreated` MUST fire `toastStore.push({ type: 'success', title: t('budgetStructure.selection.createSuccess') })` after `router.push()` completes. The orphaned i18n key MUST be wired to an actual call site.

#### Scenario: Budget create toast appears post-navigation

- GIVEN the user submits the create-budget form
- WHEN `onBudgetCreated` executes and `router.push()` resolves
- THEN a success toast with key `budgetStructure.selection.createSuccess` is visible on the destination view

#### Scenario: No toast on create failure

- GIVEN the create-budget API call rejects
- WHEN the error handler runs
- THEN no success toast is pushed

---

### Requirement: REQ-TOAST-BUDGET-RENAME

`BudgetSelectionView` rename handler MUST fire `toastStore.push({ type: 'success', title: t('budgetStructure.selection.renameSuccess') })` on successful rename.

#### Scenario: Budget rename toast appears

- GIVEN a budget rename operation succeeds
- WHEN the rename handler resolves
- THEN a success toast with key `budgetStructure.selection.renameSuccess` is visible

#### Scenario: No toast on rename failure

- GIVEN the rename API call rejects
- WHEN the error handler runs
- THEN no success toast is pushed

---

### Requirement: REQ-TOAST-MATRIX-GROUP-CREATE

`BudgetMatrixView.confirmAddGroup` MUST fire `toastStore.push({ type: 'success', title: t('budgetMatrix.rows.createGroupSuccess') })` on success.

#### Scenario: Matrix group create toast appears

- GIVEN the user confirms add-group in the matrix view
- WHEN the operation succeeds
- THEN a success toast with key `budgetMatrix.rows.createGroupSuccess` is visible

---

### Requirement: REQ-TOAST-MATRIX-GROUP-UPDATE

`MatrixGroupRow.saveEdit` MUST fire `toastStore.push({ type: 'success', title: t('budgetMatrix.rows.updateGroupSuccess') })` on success.

#### Scenario: Matrix group rename toast appears

- GIVEN the user saves an inline group name edit
- WHEN `saveEdit` resolves successfully
- THEN a success toast with key `budgetMatrix.rows.updateGroupSuccess` is visible

---

### Requirement: REQ-TOAST-MATRIX-GROUP-DELETE

`MatrixGroupRow.doDelete` MUST fire `toastStore.push({ type: 'success', title: t('budgetMatrix.rows.deleteSuccess') })` on success.

#### Scenario: Matrix group delete toast appears

- GIVEN the user deletes a group row
- WHEN `doDelete` resolves successfully
- THEN a success toast with key `budgetMatrix.rows.deleteSuccess` is visible

---

### Requirement: REQ-TOAST-MATRIX-GROUP-RESTORE

`MatrixGroupRow.doRestore` MUST fire `toastStore.push({ type: 'success', title: t('budgetMatrix.rows.restoreSuccess') })` on success.

#### Scenario: Matrix group restore toast appears

- GIVEN the user restores a deleted group row
- WHEN `doRestore` resolves successfully
- THEN a success toast with key `budgetMatrix.rows.restoreSuccess` is visible

---

### Requirement: REQ-TOAST-MATRIX-CAT-CREATE

`BudgetMatrixView.confirmAddCategory` MUST fire `toastStore.push({ type: 'success', title: t('budgetMatrix.rows.createCategorySuccess') })` on success.

#### Scenario: Matrix category create toast appears

- GIVEN the user confirms add-category in the matrix view
- WHEN the operation succeeds
- THEN a success toast with key `budgetMatrix.rows.createCategorySuccess` is visible

---

### Requirement: REQ-TOAST-MATRIX-CAT-UPDATE

`MatrixCategoryRow.saveEdit` MUST fire `toastStore.push({ type: 'success', title: t('budgetMatrix.rows.updateCategorySuccess') })` on success.

#### Scenario: Matrix category rename toast appears

- GIVEN the user saves an inline category name edit
- WHEN `saveEdit` resolves successfully
- THEN a success toast with key `budgetMatrix.rows.updateCategorySuccess` is visible

---

### Requirement: REQ-TOAST-MATRIX-CAT-DELETE

`MatrixCategoryRow.doDelete` MUST fire `toastStore.push({ type: 'success', title: t('budgetMatrix.rows.deleteSuccess') })` on success (shared key with group delete).

#### Scenario: Matrix category delete toast appears

- GIVEN the user deletes a category row
- WHEN `doDelete` resolves successfully
- THEN a success toast with key `budgetMatrix.rows.deleteSuccess` is visible

---

### Requirement: REQ-TOAST-MATRIX-CAT-RESTORE

`MatrixCategoryRow.doRestore` MUST fire `toastStore.push({ type: 'success', title: t('budgetMatrix.rows.restoreSuccess') })` on success (shared key with group restore).

#### Scenario: Matrix category restore toast appears

- GIVEN the user restores a deleted category row
- WHEN `doRestore` resolves successfully
- THEN a success toast with key `budgetMatrix.rows.restoreSuccess` is visible

---

### Requirement: REQ-TOAST-MATRIX-LINE-CREATE

`BudgetMatrixView.confirmAddLine` MUST fire `toastStore.push({ type: 'success', title: t('budgetMatrix.rows.createLineSuccess') })` on success.

#### Scenario: Matrix line create toast appears

- GIVEN the user confirms add-line in the matrix view
- WHEN the operation succeeds
- THEN a success toast with key `budgetMatrix.rows.createLineSuccess` is visible

---

### Requirement: REQ-TOAST-MATRIX-LINE-DELETE

`MatrixLineRow.doDelete` MUST fire `toastStore.push({ type: 'success', title: t('budgetMatrix.rows.deleteSuccess') })` on success (shared key).

#### Scenario: Matrix line delete toast appears

- GIVEN the user deletes a line row
- WHEN `doDelete` resolves successfully
- THEN a success toast with key `budgetMatrix.rows.deleteSuccess` is visible

---

### Requirement: REQ-TOAST-MATRIX-LINE-RESTORE

`MatrixLineRow.doRestore` MUST fire `toastStore.push({ type: 'success', title: t('budgetMatrix.rows.restoreSuccess') })` on success (shared key).

#### Scenario: Matrix line restore toast appears

- GIVEN the user restores a deleted line row
- WHEN `doRestore` resolves successfully
- THEN a success toast with key `budgetMatrix.rows.restoreSuccess` is visible

---

### Requirement: REQ-TOAST-NOTIFICATION-MIGRATION

`ChangePasswordModal.vue` MUST use `useToastStore` instead of `useNotificationStore` for its success feedback. The `notificationStore` import and usage MUST be removed from that component entirely.

#### Scenario: Password change uses toastStore

- GIVEN the user submits a valid password change
- WHEN the operation succeeds
- THEN a success toast appears via `useToastStore.push()`
- AND `useNotificationStore` is NOT called by this component

#### Scenario: Bell count unaffected by password change success

- GIVEN a persistent notification exists in the bell
- WHEN a password change success fires
- THEN the bell notification count is unchanged

---

### Requirement: REQ-TOAST-I18N-1 — Toast i18n Keys

The following keys MUST be present in both `en.json` and `es.json` under their entity namespaces.

| Namespace | Key | Purpose |
|---|---|---|
| `budgetStructure.cycles` | `createSuccess` | Cycle created |
| `budgetStructure.cycles` | `deleteSuccess` | Cycle deleted |
| `budgetStructure.cycles` | `restoreSuccess` | Cycle restored |
| `budgetStructure.cycles` | `showDeleted` | Toggle label |
| `budgetStructure.periods` | `createSuccess` | Period created |
| `budgetStructure.periods` | `deleteSuccess` | Period deleted |
| `budgetStructure.periods` | `restoreSuccess` | Period restored |
| `budgetStructure.periods` | `showDeleted` | Toggle label |
| `budgetStructure.categoryGroups` | `createSuccess` | Group created |
| `budgetStructure.categoryGroups` | `deleteSuccess` | Group deleted |
| `budgetStructure.categoryGroups` | `restoreSuccess` | Group restored |
| `budgetStructure.categoryGroups` | `showDeleted` | Toggle label |
| `budgetStructure.categories` | `createSuccess` | Category created |
| `budgetStructure.categories` | `deleteSuccess` | Category deleted |
| `budgetStructure.categories` | `restoreSuccess` | Category restored |
| `budgetStructure.categories` | `showDeleted` | Toggle label |
| `budgetStructure.budgetLines` | `createSuccess` | Line created |
| `budgetStructure.budgetLines` | `deleteSuccess` | Line deleted |
| `budgetStructure.budgetLines` | `restoreSuccess` | Line restored |
| `budgetStructure.budgetLines` | `showDeleted` | Toggle label |
| `budgetExecution.record` | `deleteSuccess` | Record deleted |
| `budgetExecution.record` | `restoreSuccess` | Record restored |
| `budgetStructure.selection` | `createSuccess` | Budget created (wired) |
| `budgetStructure.selection` | `renameSuccess` | Budget renamed (new) |
| `budgetMatrix.rows` | `createGroupSuccess` | Matrix group created (new) |
| `budgetMatrix.rows` | `updateGroupSuccess` | Matrix group renamed (new) |
| `budgetMatrix.rows` | `deleteSuccess` | Matrix row deleted — shared (new) |
| `budgetMatrix.rows` | `restoreSuccess` | Matrix row restored — shared (new) |
| `budgetMatrix.rows` | `createCategorySuccess` | Matrix category created (new) |
| `budgetMatrix.rows` | `updateCategorySuccess` | Matrix category renamed (new) |
| `budgetMatrix.rows` | `createLineSuccess` | Matrix line created (new) |

#### Scenario: All keys present in both locales

- GIVEN the application builds with locale files loaded
- WHEN any toast message references the keys above
- THEN no i18n missing-key warning is emitted in either EN or ES locale

#### Scenario: Orphaned key wired up

- GIVEN `budgetStructure.selection.createSuccess` already exists in both locale files
- WHEN a budget create operation succeeds
- THEN the toast displays the correct translated title with no missing-key fallback
