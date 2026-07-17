# Delta Spec: budget-execution-ui-e2e-debt

**Change**: `budget-execution-ui-e2e-debt`
**Date**: 2026-07-17
**Artifact store**: hybrid

---

## Capability Index

| Capability | Type | Delta |
|---|---|---|
| `budget-execution` | Modified | REQ-EXEC-TOAST-1 extended (add create/update); new REQ-EXEC-UI-CRUD-1, REQ-EXEC-UI-DELETE-1, REQ-EXEC-UI-TOAST-1 |
| `ephemeral-toast` | Modified | REQ-TOAST-I18N-1 extended (add `createSuccess` + `updateSuccess` for `budgetExecution.record`) |
| `e2e-helpers` | New | REQ-E2E-AUTH-1 |

---

## MODIFIED Requirements

### Requirement: REQ-EXEC-TOAST-1 — Success Toasts on ExecutionRecord Operations

On successful create, update, delete, or restore of an ExecutionRecord, the UI MUST push a success
toast via `useToastStore` using the appropriate i18n key. No toast MUST be shown on failed operations.
(Previously: only delete and restore fired toasts; create and update had no toast call.)

#### Scenario: Delete success toast

- GIVEN the two-step delete confirmation is confirmed
- WHEN `DELETE .../executions/{id}` returns 204
- THEN a success toast is shown with the `budgetExecution.record.deleteSuccess` message

#### Scenario: Restore success toast

- GIVEN a soft-deleted ExecutionRecord in a view with includeDeleted=true
- WHEN `POST .../executions/{id}/restore` returns 200
- THEN a success toast is shown with the `budgetExecution.record.restoreSuccess` message

#### Scenario: Create success toast

- GIVEN the ExecutionRecordForm is open in create mode
- WHEN the form is submitted and the API returns 201
- THEN a success toast is shown with the `budgetExecution.record.createSuccess` message

#### Scenario: Update success toast

- GIVEN the ExecutionRecordForm is open in edit mode with an existing record
- WHEN the form is submitted and the API returns 200
- THEN a success toast is shown with the `budgetExecution.record.updateSuccess` message

---

### Requirement: REQ-TOAST-I18N-1 — Toast i18n Keys

The following keys MUST be present in both `en.json` and `es.json` under their entity namespaces.
(Previously: `budgetExecution.record` namespace had only `deleteSuccess` and `restoreSuccess`.)

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
| `budgetExecution.record` | `createSuccess` | Record created |
| `budgetExecution.record` | `updateSuccess` | Record updated |

#### Scenario: All keys present in both locales

- GIVEN the application builds with locale files loaded
- WHEN any toast message references the keys above
- THEN no i18n missing-key warning is emitted in either EN or ES locale

---

## ADDED Requirements

### Requirement: REQ-E2E-AUTH-1 — Shared E2E Auth Helper

A shared helper module MUST exist at `e2e/helpers/auth.ts` exporting `loginWithToken(page, token, budgetId)`. The function MUST set `accessToken` and `activeBudgetId` in `localStorage` and navigate to a stable post-login route. The `budget-matrix/helpers.ts` module MUST re-export `loginWithToken` from the shared helper (no duplication of implementation).

#### Scenario: Shared helper sets required localStorage keys

- GIVEN a Playwright `page` object and valid credentials
- WHEN `loginWithToken(page, token, budgetId)` is called
- THEN `localStorage.accessToken` is set
- AND `localStorage.activeBudgetId` is set to the given budgetId

#### Scenario: budget-matrix helper re-exports from shared path

- GIVEN `budget-matrix/helpers.ts` is imported
- WHEN a test calls `loginWithToken` from it
- THEN the call delegates to the shared `e2e/helpers/auth.ts` implementation without duplication

---

### Requirement: REQ-EXEC-UI-CRUD-1 — ExecutionRecord UI CRUD Flows

The E2E suite MUST include `{ page, request }` tests covering create, update, and OperationDate
default via the `ExecutionListModal` UI.

#### SCENARIO-CRUD-1.1: Create — record appears in list

- GIVEN an open period with a seeded BudgetLine and the user is authenticated as owner
- WHEN the user navigates to `/budgets/{id}/cycles/{cycleId}/matrix`, opens `[data-testid="execution-list-modal"]` via MatrixCell click, fills `[data-testid="amount-input"]` and `[data-testid="entry-type-select"]`, and clicks `[data-testid="execution-form-submit"]`
- THEN `expectToast(page, i18n.budgetExecution.record.createSuccess)` passes
- AND the new record is visible in the modal list

#### SCENARIO-CRUD-1.2: Create — OperationDate defaults to today

- GIVEN the ExecutionRecordForm is open in create mode
- WHEN the form renders
- THEN `[data-testid="operation-date-input"]` (or equivalent date field) value equals today's date in ISO format

#### SCENARIO-CRUD-1.3: Update — record reflects change

- GIVEN a seeded ExecutionRecord visible in the modal
- WHEN the user clicks the edit action for that row, modifies `[data-testid="amount-input"]`, and clicks `[data-testid="execution-form-submit"]`
- THEN `expectToast(page, i18n.budgetExecution.record.updateSuccess)` passes
- AND the row in the modal list shows the updated amount

#### SCENARIO-CRUD-1.4: Update — form pre-fills existing values

- GIVEN a seeded ExecutionRecord with a known amount and entry type
- WHEN the user clicks the edit action for that row
- THEN `[data-testid="amount-input"]` is pre-filled with the record's amount
- AND `[data-testid="entry-type-select"]` shows the record's entry type

---

### Requirement: REQ-EXEC-UI-DELETE-1 — ExecutionRecord UI Delete/Restore Flows

The E2E suite MUST include `{ page, request }` tests covering the two-step confirm-delete UX,
cancel behavior, restore, and restore in a closed period.

#### SCENARIO-DELETE-2.1: Two-step delete — confirm flow

- GIVEN an active ExecutionRecord row visible in the modal
- WHEN the user clicks `[data-testid="delete-record-btn"]`
- THEN the row renders `[data-testid="delete-record-confirm-btn"]` and `[data-testid="delete-record-cancel-btn"]`
- AND no API call is made at this point

#### SCENARIO-DELETE-2.2: Two-step delete — cancel resets state

- GIVEN `[data-testid="delete-record-confirm-btn"]` is visible
- WHEN the user clicks `[data-testid="delete-record-cancel-btn"]`
- THEN `[data-testid="delete-record-btn"]` is restored to its original state
- AND `[data-testid="delete-record-confirm-btn"]` is no longer visible
- AND no API call is made

#### SCENARIO-DELETE-2.3: Two-step delete — confirm deletes with toast

- GIVEN `[data-testid="delete-record-confirm-btn"]` is visible
- WHEN the user clicks it
- THEN `expectToast(page, i18n.budgetExecution.record.deleteSuccess)` passes
- AND the record row is no longer visible in the default list (not in include-deleted view)

#### SCENARIO-DELETE-2.4: Restore deleted record

- GIVEN a soft-deleted ExecutionRecord
- WHEN the user toggles `[data-testid="modal-include-deleted-toggle"]` ON, making deleted records visible
- AND clicks the Restore button on the deleted record row
- THEN `expectToast(page, i18n.budgetExecution.record.restoreSuccess)` passes
- AND the record reappears in the default (non-deleted) list when the toggle is turned OFF

#### SCENARIO-DELETE-2.5: Restore in closed period — restore button renders

- GIVEN a soft-deleted ExecutionRecord within a closed period
- WHEN the user opens the modal on that closed period (the `[data-testid="closed-period-banner"]` is visible)
- AND toggles `[data-testid="modal-include-deleted-toggle"]` ON
- THEN the Restore button is present on the deleted record row (the `v-else-if="record.deletedAt && canWrite"` branch renders)

---

### Requirement: REQ-EXEC-UI-TOAST-1 — Explicit Toast Assertions for All Four Operations

The E2E suite MUST include a dedicated `execution-ui-toast.spec.ts` asserting all four success
toasts using the `expectToast()` pattern from `budget-structure/helpers.ts`.

#### SCENARIO-TOAST-3.1: createSuccess toast fires on create

- GIVEN a create flow is completed via the UI
- WHEN the API returns 201
- THEN `expectToast(page, i18n.budgetExecution.record.createSuccess)` passes within the configured timeout

#### SCENARIO-TOAST-3.2: updateSuccess toast fires on update

- GIVEN an edit flow is completed via the UI
- WHEN the API returns 200
- THEN `expectToast(page, i18n.budgetExecution.record.updateSuccess)` passes within the configured timeout

#### SCENARIO-TOAST-3.3: deleteSuccess toast fires on delete

- GIVEN the two-step delete is confirmed via the UI
- WHEN the API returns 204
- THEN `expectToast(page, i18n.budgetExecution.record.deleteSuccess)` passes within the configured timeout

#### SCENARIO-TOAST-3.4: restoreSuccess toast fires on restore

- GIVEN a restore is triggered via the UI
- WHEN the API returns 200
- THEN `expectToast(page, i18n.budgetExecution.record.restoreSuccess)` passes within the configured timeout

---

## Selectors Reference

| Selector | Usage |
|---|---|
| `[data-testid="execution-list-modal"]` | Assert modal is open |
| `[data-testid="delete-record-btn"]` | First click in two-step delete |
| `[data-testid="delete-record-confirm-btn"]` | Second click — confirms delete |
| `[data-testid="delete-record-cancel-btn"]` | Cancels delete, reverts state |
| `[data-testid="modal-include-deleted-toggle"]` | Shows soft-deleted records in modal |
| `[data-testid="closed-period-banner"]` | Asserts modal is in closed-period state |
| `[data-testid="amount-input"]` | Amount field in ExecutionRecordForm |
| `[data-testid="entry-type-select"]` | Entry type selector |
| `[data-testid="execution-form-submit"]` | Submit button |

## Toast Assertion Pattern

Use `expectToast(page, text)` as established in `budget-structure/helpers.ts`:

```ts
// getByRole('alert').filter({ hasText: text }).first() — 8s timeout
await expectToast(page, 'Record created successfully')
```

---

## Assumptions Made

1. `[data-testid="modal-include-deleted-toggle"]` exists in `ExecutionListModal.vue` (confirmed from exploration).
2. The Restore button in a closed period relies on the `v-else-if="record.deletedAt && canWrite"` branch in `ExecutionRecordRow.vue` — seeding as owner guarantees `canWrite = true`.
3. `expectToast()` will be extracted into `e2e/helpers/toast.ts` or co-located in `e2e/budget-execution/helpers.ts` following the `budget-structure/helpers.ts` pattern.
4. MatrixCell click (single click, not dblclick) is the correct trigger — to be confirmed against current `MatrixCell.vue` event handler during implementation.
5. The closed-period restore SCENARIO-DELETE-2.5 only asserts button visibility; API rejection (409) is already covered by the existing `period-closed-guard.spec.ts`.
