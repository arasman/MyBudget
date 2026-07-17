# Tasks: budget-execution-ui-e2e-debt

**Change**: `budget-execution-ui-e2e-debt`
**Date**: 2026-07-17
**Branch**: `feat/budget-execution-ui-e2e-debt`
**Delivery strategy**: ask-on-risk

---

## Review Workload Forecast

| Task(s) | Est. Changed Lines | Risk |
|---------|-------------------|------|
| 1.1 — i18n keys (2 files) | ~4 lines | Low |
| 1.2 — ExecutionRecordForm toast patch | ~8 lines | Low |
| 1.3 — e2e/helpers/auth.ts (new file) | ~30 lines | Low |
| 1.4 — e2e/helpers/toast.ts (new file) | ~10 lines | Low |
| 1.5 — budget-matrix/helpers.ts refactor | ~10 lines | Low |
| 1.6 — budget-structure/helpers.ts refactor | ~5 lines | Low |
| 2.1 — execution-ui-crud.spec.ts (new file) | ~110 lines | Med |
| 2.2 — execution-ui-delete-restore.spec.ts (new file) | ~130 lines | Med |
| 2.3 — execution-ui-toast.spec.ts (new file) | ~80 lines | Med |
| **Total** | **~387 lines** | **Under 400-line budget** |

Single PR is safe — total delta stays just under the 400-line review threshold. No chained PRs required.

---

## Dependency Graph

```
1.1 ──────────────────────────────────────────────────────────────┐
1.2 (depends on 1.1 for i18n keys to exist before asserting them)  │
                                                                    ├──> 2.1
1.3 ──────────────────────────────────────────────────────────────┤    2.2
1.4 ──────────────────────────────────────────────────────────────┤    2.3
1.5 (depends on 1.3)                                               │
1.6 (depends on 1.4)                                              ─┘
```

Phase 1 tasks must all be complete before Phase 2 spec files are written. Within Phase 1, tasks 1.3, 1.4, and 1.1 are independent and can be done in parallel. Task 1.5 depends on 1.3; 1.6 depends on 1.4; 1.2 depends on 1.1.

---

## Phase 1 — Production + Test Infrastructure

### Task 1.1 — Add i18n keys for create/update toasts

**Satisfies**: REQ-TOAST-I18N-1, REQ-EXEC-TOAST-1 (create/update scenarios)
**Parallel with**: 1.3, 1.4
**Sequential before**: 1.2

Files:
- `Project/frontend/src/i18n/locales/en.json` — add inside `budgetExecution.record` after `restoreSuccess` (line ~342):
  ```json
  "createSuccess": "Entry created successfully",
  "updateSuccess": "Entry updated successfully"
  ```
- `Project/frontend/src/i18n/locales/es.json` — same location:
  ```json
  "createSuccess": "Entrada creada exitosamente",
  "updateSuccess": "Entrada actualizada exitosamente"
  ```

Verification: no i18n missing-key console warning when the app runs in either locale.

---

### Task 1.2 — Patch ExecutionRecordForm.vue — add toastStore calls

**Satisfies**: REQ-EXEC-TOAST-1 (create success, update success)
**Depends on**: 1.1 (i18n keys must exist)

File: `Project/frontend/src/features/budget-execution/components/ExecutionRecordForm.vue`

Changes:
1. Import `useToastStore` from the toast store composable (mirror pattern from `ExecutionRecordRow.vue`).
2. Instantiate `const toastStore = useToastStore()` in `<script setup>`.
3. After `await matrixStore.createExecution(...)` succeeds (before `emit('saved')`):
   ```ts
   toastStore.push({ type: 'success', title: t('budgetExecution.record.createSuccess') })
   ```
4. After `await matrixStore.updateExecution(...)` succeeds (before or after `emit('saved')`):
   ```ts
   toastStore.push({ type: 'success', title: t('budgetExecution.record.updateSuccess') })
   ```

Design decision: toast fires in the component performing the action, not in the modal callback (Decision #7 from design.md).

---

### Task 1.3 — Create e2e/helpers/auth.ts

**Satisfies**: REQ-E2E-AUTH-1
**Parallel with**: 1.1, 1.4
**Sequential before**: 1.5

File: `Project/frontend/e2e/helpers/auth.ts` (new file — directory does not exist yet, must create)

Contract (from design.md):
```typescript
export interface LoginTokens {
  accessToken: string
  refreshToken?: string
  activeBudgetId?: string
}

export async function loginWithToken(page: Page, tokens: LoginTokens): Promise<void>
```

Implementation: `page.goto('/')`, then `page.evaluate` to set `accessToken`, `refreshToken` (defaults to empty string), and `activeBudgetId` in `localStorage`. Matches what `budget-matrix/helpers.ts` does today for `accessToken` + `activeBudgetId`, and what `budget-structure/helpers.ts` does for `refreshToken`.

---

### Task 1.4 — Create e2e/helpers/toast.ts

**Satisfies**: REQ-EXEC-UI-TOAST-1 (shared helper used by all spec files)
**Parallel with**: 1.1, 1.3
**Sequential before**: 1.6

File: `Project/frontend/e2e/helpers/toast.ts` (new file)

Extract verbatim from `budget-structure/helpers.ts` lines 107-111:
```typescript
export async function expectToast(page: Page, text: string): Promise<void> {
  await expect(
    page.getByRole('alert').filter({ hasText: text }).first(),
  ).toBeVisible({ timeout: 8_000 })
}
```

---

### Task 1.5 — Update budget-matrix/helpers.ts to re-export from shared auth helper

**Satisfies**: REQ-E2E-AUTH-1 (no duplication clause)
**Depends on**: 1.3

File: `Project/frontend/e2e/budget-matrix/helpers.ts`

Change: replace the inline `loginWithToken` implementation (lines 155-172) with a re-export that delegates to the shared helper. The existing callers in `budget-matrix/*.spec.ts` pass positional args `(page, accessToken, budgetId)` — the adapter must bridge the positional signature to the object signature of the shared helper, OR the re-export exposes a wrapper with the same positional signature to avoid touching all call sites.

Preferred approach: keep the positional wrapper in `budget-matrix/helpers.ts` so zero call sites change:
```typescript
import { loginWithToken as _loginWithToken } from '../helpers/auth'

export async function loginWithToken(page: Page, accessToken: string, budgetId: string): Promise<void> {
  return _loginWithToken(page, { accessToken, activeBudgetId: budgetId })
}
```

Risk: `refreshToken` was never set by the budget-matrix helper — the shared helper defaults it to empty string, which normalizes the gap silently.

---

### Task 1.6 — Update budget-structure/helpers.ts to re-export expectToast

**Satisfies**: DRY convention (single source of truth for toast assertion)
**Depends on**: 1.4

File: `Project/frontend/e2e/budget-structure/helpers.ts`

Change: replace the inline `expectToast` implementation (lines 107-111) with a re-export:
```typescript
export { expectToast } from '../helpers/toast'
```

All existing `budget-structure/*.spec.ts` callers import from `./helpers` — the re-export keeps them unbroken.

---

## Phase 2 — New UI E2E Spec Files

All Phase 2 tasks depend on all Phase 1 tasks being complete. Tasks 2.1, 2.2, and 2.3 are independent of each other and can be written in parallel.

### Task 2.1 — execution-ui-crud.spec.ts

**Satisfies**: REQ-EXEC-UI-CRUD-1 (SCENARIO-CRUD-1.1 through 1.4)
**Parallel with**: 2.2, 2.3
**Depends on**: all Phase 1 tasks

File: `Project/frontend/e2e/budget-execution/execution-ui-crud.spec.ts` (new file)

Scenarios to cover:

| ID | Scenario | Key assertion |
|----|----------|--------------|
| CRUD-1.1 | Create — record appears in list | `expectToast(page, 'Entry created successfully')` + row visible |
| CRUD-1.2 | Create — OperationDate defaults to today | `[data-testid="operation-date-input"]` value === today ISO |
| CRUD-1.3 | Update — record reflects change | `expectToast(page, 'Entry updated successfully')` + updated amount in row |
| CRUD-1.4 | Update — form pre-fills existing values | Amount and entry-type inputs match seeded record values |

Setup pattern:
- `{ page, request }` fixture
- `seedBudgetMatrixFixture(request)` for data (creates user + budget + cycle + periods + groups + categories + lines + token)
- `loginWithToken(page, { accessToken, activeBudgetId: budgetId })` from `e2e/helpers/auth.ts`
- Navigate to `/budgets/{budgetId}/cycles/{cycleId}/matrix`
- dblclick `[data-testid="matrix-cell-ejecutado"]` (or single click — confirm against MatrixCell.vue; proposal notes this needs confirming at implementation time)
- Wait for `[data-testid="execution-list-modal"]` to be visible

Navigation note (from design.md): dblclick on `matrix-cell-ejecutado` is the confirmed trigger per `budget-matrix/execution-crud.spec.ts` pattern.

---

### Task 2.2 — execution-ui-delete-restore.spec.ts

**Satisfies**: REQ-EXEC-UI-DELETE-1 (SCENARIO-DELETE-2.1 through 2.5)
**Parallel with**: 2.1, 2.3
**Depends on**: all Phase 1 tasks

File: `Project/frontend/e2e/budget-execution/execution-ui-delete-restore.spec.ts` (new file)

Scenarios to cover:

| ID | Scenario | Key assertion |
|----|----------|--------------|
| DELETE-2.1 | Two-step delete — enter confirm state | `delete-record-confirm-btn` + `delete-record-cancel-btn` visible; no API call |
| DELETE-2.2 | Two-step delete — cancel resets | `delete-record-btn` restored; `delete-record-confirm-btn` gone; no API call |
| DELETE-2.3 | Two-step delete — confirm deletes | `expectToast(page, 'Entry deleted successfully')` + row gone from default list |
| DELETE-2.4 | Restore deleted record | Toggle `modal-include-deleted-toggle` ON → Restore btn click → `expectToast(page, 'Entry restored successfully')` → toggle OFF → row visible |
| DELETE-2.5 | Restore in closed period | Seed closed period → open modal → toggle ON → Restore btn present (render only, no API assert) |

Setup note for DELETE-2.5: use `closePeriodApi` from `budget-matrix/helpers.ts` (or equivalent `closePeriod` from existing `budget-execution/helpers.ts`). The test asserts button visibility only; API rejection (409 guard) is already covered by `period-closed-guard.spec.ts`.

---

### Task 2.3 — execution-ui-toast.spec.ts

**Satisfies**: REQ-EXEC-UI-TOAST-1 (SCENARIO-TOAST-3.1 through 3.4)
**Parallel with**: 2.1, 2.2
**Depends on**: all Phase 1 tasks

File: `Project/frontend/e2e/budget-execution/execution-ui-toast.spec.ts` (new file)

Scenarios to cover:

| ID | Scenario | Toast text |
|----|----------|-----------|
| TOAST-3.1 | createSuccess fires on create | `'Entry created successfully'` |
| TOAST-3.2 | updateSuccess fires on update | `'Entry updated successfully'` |
| TOAST-3.3 | deleteSuccess fires on delete | `'Entry deleted successfully'` |
| TOAST-3.4 | restoreSuccess fires on restore | `'Entry restored successfully'` |

Note: toast text values are the English locale strings added in task 1.1. Specs assert the rendered text, not the i18n key.

Design consideration: TOAST-3.1 and TOAST-3.2 can share setup with execution-ui-crud.spec.ts scenarios. TOAST-3.3 and TOAST-3.4 can share setup with execution-ui-delete-restore.spec.ts. The dedicated toast spec file is separate (per REQ-EXEC-UI-TOAST-1) but may seed data the same way.

---

## Execution Order Summary

```
Step 1 (parallel): Task 1.1 + Task 1.3 + Task 1.4
Step 2 (sequential): Task 1.2 (after 1.1) + Task 1.5 (after 1.3) + Task 1.6 (after 1.4)
Step 3 (parallel): Task 2.1 + Task 2.2 + Task 2.3
```

Minimum critical path: 1.1 → 1.2 → 2.1/2.2/2.3 (3 sequential hops; helpers are not on the critical path for spec writing, only for import resolution).

---

## Success Criteria (from proposal.md)

- [x] `ExecutionRecordForm` fires `createSuccess` toast on create and `updateSuccess` on update
- [x] `budgetExecution.record.createSuccess` and `updateSuccess` present in `en.json` and `es.json`
- [x] `e2e/helpers/auth.ts` created; `budget-matrix/helpers.ts` updated to use shared helper
- [x] `execution-ui-crud.spec.ts` passes: CRUD-1.1 to 1.4
- [x] `execution-ui-delete-restore.spec.ts` passes: DELETE-2.1 to 2.5
- [x] `execution-ui-toast.spec.ts` passes: TOAST-3.1 to 3.4
- [x] All 11 existing API-only E2E tests still pass unmodified
