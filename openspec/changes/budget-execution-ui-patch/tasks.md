# Tasks: budget-execution-ui-patch

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 350–480 |
| 400-line budget risk | Medium |
| Chained PRs recommended | No |
| Suggested split | Single PR — all changes are tightly scoped deltas on existing slices |
| Delivery strategy | ask-on-risk |
| Chain strategy | pending |

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: pending
400-line budget risk: Medium

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | All 8 deferred items shipped together | PR 1 | Changes are interdependent or too small to justify splitting |

---

## Phase 1: Backend Foundation

- [x] 1.1 Add `DateOnly? OperationDate` property to `ExecutionRecord.cs`; update `Create()` and `Update()` factory signatures to accept it. _(REQ-EXEC-1)_
- [x] 1.2 Generate EF Core migration `AddOperationDateToExecutionRecord` adding the nullable `OperationDate` column; verify `Down()` drops it cleanly. _(REQ-EXEC-1)_
- [x] 1.3 Add `Guid? CurrencyId` field to `BudgetLineResponse` record in `ListBudgetLinesQuery.cs`. _(REQ-EXEC-CURRENCY-READ-1)_
- [x] 1.4 Map `entity.CurrencyId` → `BudgetLineResponse.CurrencyId` in `ListBudgetLinesHandler.cs`. _(REQ-EXEC-CURRENCY-READ-1)_

## Phase 2: Backend Command Layer

- [x] 2.1 Add `DateOnly? OperationDate` parameter to `CreateExecutionRecordCommand.cs`; pass it to `ExecutionRecord.Create()` in `CreateExecutionRecordHandler.cs`. _(REQ-EXEC-1)_
- [x] 2.2 Add `DateOnly? OperationDate` parameter to `UpdateExecutionRecordCommand.cs`; pass it to `ExecutionRecord.Update()` in `UpdateExecutionRecordHandler.cs`. _(REQ-EXEC-1)_

## Phase 3: Frontend Type Contracts

- [x] 3.1 In `budget-structure/types.ts`: rename `currency?` → `currencyId?` in `CreateBudgetLinePayload` and `UpdateBudgetLinePayload`; add `currencyId?: string` to `BudgetLineResponse`. _(REQ-EXEC-CURRENCY-READ-1)_
- [x] 3.2 In `budget-execution/types.ts`: add `operationDate: string | null` to `ExecutionRecordDto`; add `operationDate?: string | null` to `CreateExecutionRequest` and `UpdateExecutionRequest`. _(REQ-EXEC-LIST-2)_

## Phase 4: Frontend Component Patches

- [x] 4.1 `BudgetLineModal.vue`: change `form.currency` → `form.currencyId`; populate dropdown from cycle currencies by Guid; send `currencyId` (Guid) in payload. _(REQ-EXEC-CURRENCY-READ-1)_
- [x] 4.2 `ExecutionRecordForm.vue`: add `operationDate` date input (default = today's date), `currencyId` select (from cycle currencies), and `exchangeRate` number input. _(REQ-EXEC-FORM-1, REQ-EXEC-FORM-2)_
- [x] 4.3 `MatrixGroupRow.vue`: add `window.getSelection()?.removeAllRanges()` inside `startEdit()` dblclick handler. _(REQ-BL-3)_
- [x] 4.4 `MatrixCategoryRow.vue`: add `window.getSelection()?.removeAllRanges()` inside `startEdit()` dblclick handler. _(REQ-BL-3)_
- [x] 4.5 `MatrixLineRow.vue`: add `window.getSelection()?.removeAllRanges()` inside `openEditModal()` dblclick handler. _(REQ-BL-3)_

## Phase 5: Matrix View — Footer, DnD, Inline Category

- [x] 5.1 Create `MatrixTotalRow.vue`: sums budgeted and executed values across all 3 lineType `MatrixSummaryRow` subtotals; accepts those three subtotals as props. _(REQ-MATRIX-FOOTER-1)_
- [x] 5.2 `BudgetMatrixView.vue` — footer: reorder footer rows to Expenses → PreventiveSavings → LongTermSavings; change labels to "SubTotal" via i18n keys; append `<MatrixTotalRow>` after the three subtotal rows. _(REQ-MATRIX-FOOTER-1)_
- [x] 5.3 Update `frontend/src/i18n/locales/en.json`: add/rename summary keys to `subTotal.*` and add a `total` key. _(REQ-MATRIX-FOOTER-1)_
- [x] 5.4 Update `frontend/src/i18n/locales/es.json`: same i18n additions in Spanish. _(REQ-MATRIX-FOOTER-1)_
- [x] 5.5 `BudgetMatrixView.vue` — inline category dropdown: add a `<select>` filtered by the parent group's categories in the inline add-line row. _(REQ-BL-2)_
- [x] 5.6 `BudgetMatrixView.vue` — DnD: wrap group rows with `vue-draggable-plus`; on `@end`, extract ordered IDs and dispatch `reorderGroups` store action. _(REQ-MATRIX-DND-1)_

## Phase 6: Incremental Name-Edit Rendering

- [x] 6.1 In `BudgetMatrixView.vue` (or its store): after a successful name-only edit of a group or category, update the local reactive data in place instead of triggering a full matrix reload. _(REQ-MATRIX-RENDER-1 — confirmed no-op: store already does in-place updates)_

## Phase 7: Manual Verification Checklist

- [ ] 7.1 Verify: create a budget line with currency — confirm DB stores a Guid, not a code string.
- [ ] 7.2 Verify: edit a budget line — `currencyId` pre-populates correctly.
- [ ] 7.3 Verify: drag groups/categories/lines — reload page and confirm persisted order.
- [ ] 7.4 Verify: footer shows Expenses/PreventiveSavings/LongTermSavings as SubTotal rows with a Total row below.
- [ ] 7.5 Verify: double-click to edit does not trigger text-selection highlight in any row type.
- [ ] 7.6 Verify: create/edit an execution record — `operationDate` defaults to today, saves correctly, reads back.
- [ ] 7.7 Verify: inline add-line category dropdown only shows categories belonging to the selected group.
- [ ] 7.8 Verify: renaming a group or category name does not trigger a full matrix reload.
