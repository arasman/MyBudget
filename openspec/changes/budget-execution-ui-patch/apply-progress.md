# Apply Progress: budget-execution-ui-patch

**Status**: COMPLETE (Phases 1–6 done; Phase 7 = manual verification by user)
**Date**: 2026-07-15
**Build**: Backend clean, Frontend clean

---

## Completed Tasks

### Phase 1: Backend Foundation
- [x] 1.1 `ExecutionRecord.cs` — `DateOnly? OperationDate` added; `Create()`/`Update()` updated
- [x] 1.2 Migration `20260715223718_AddOperationDateToExecutionRecord` generated
- [x] 1.3 `ListBudgetLinesQuery.cs` — `Guid? CurrencyId` added to `BudgetLineResponse`
- [x] 1.4 `ListBudgetLinesHandler.cs` — SQL + projection map updated

### Phase 2: Backend Command Layer
- [x] 2.1 `CreateExecutionRecord` slice — `OperationDate` threaded through Command → Handler → Endpoint
- [x] 2.2 `UpdateExecutionRecord` slice — `OperationDate` threaded through Command → Handler → Endpoint

### Phase 3: Frontend Type Contracts
- [x] 3.1 `budget-structure/types.ts` — `currency?` → `currencyId?`; `BudgetLineResponse.currencyId` added
- [x] 3.2 `budget-execution/types.ts` — `operationDate: string | null` added to DTO + requests

### Phase 4: Frontend Component Patches
- [x] 4.1 `BudgetLineModal.vue` — currencyId from cycle Guids
- [x] 4.2 `ExecutionRecordForm.vue` — operationDate / currencyId / exchangeRate fields
- [x] 4.3 `MatrixGroupRow.vue` — `getSelection().removeAllRanges()` on dblclick + drag handle span
- [x] 4.4 `MatrixCategoryRow.vue` — `getSelection().removeAllRanges()` on dblclick
- [x] 4.5 `MatrixLineRow.vue` — `getSelection().removeAllRanges()` on dblclick

### Phase 5: Matrix View — Footer, DnD, Inline Category
- [x] 5.1 `MatrixTotalRow.vue` created (total footer row)
- [x] 5.2 `BudgetMatrixView.vue` — footer reordered; SubTotal labels; `<MatrixTotalRow>` appended
- [x] 5.3 `en.json` — `expensesSubTotal`, `preventiveSavingsSubTotal`, `longTermSavingsSubTotal`, `total`, form keys added
- [x] 5.4 `es.json` — same in Spanish
- [x] 5.5 `BudgetMatrixView.vue` — inline add-line category `<select>` filtered by group
- [x] 5.6 `BudgetMatrixView.vue` — group-level DnD via `VueDraggable tag="tbody"`; `onGroupsDragEnd` calls `reorderGroups`

### Phase 6: Incremental Name-Edit Rendering
- [x] 6.1 No-op confirmed: store already does in-place updates on name edits (no matrix reload)

---

## Phase 7: Manual Verification (pending user)

- [ ] 7.1 Budget line create with currency → DB stores Guid
- [ ] 7.2 Budget line edit → currencyId pre-populates
- [ ] 7.3 Drag groups → persisted order after reload
- [ ] 7.4 Footer: Expenses/PreventiveSavings/LongTermSavings SubTotal rows + Total row
- [ ] 7.5 Double-click edit → no text-selection highlight
- [ ] 7.6 Execution record create/edit → operationDate defaults today, saves, reads back
- [ ] 7.7 Inline add-line → category dropdown filtered by group
- [ ] 7.8 Rename group/category → no full matrix reload

---

## Key Files Changed

**Backend**
- `Project/src/MyBudget.Features/SharedKernel/Entities/ExecutionRecord.cs`
- `Project/src/MyBudget.Features/Migrations/20260715223718_AddOperationDateToExecutionRecord.cs`
- `Project/src/MyBudget.Features/Features/BudgetStructure/ListBudgetLines/ListBudgetLinesQuery.cs`
- `Project/src/MyBudget.Features/Features/BudgetStructure/ListBudgetLines/ListBudgetLinesHandler.cs`
- `Project/src/MyBudget.Features/Features/BudgetExecution/CreateExecutionRecord/{Command,Handler,Endpoint}.cs`
- `Project/src/MyBudget.Features/Features/BudgetExecution/UpdateExecutionRecord/{Command,Handler,Endpoint}.cs`
- `Project/src/MyBudget.Features/Features/BudgetExecution/ListExecutionRecords/{Query,Handler}.cs`

**Frontend**
- `Project/frontend/src/features/budget-structure/types.ts`
- `Project/frontend/src/features/budget-execution/types.ts`
- `Project/frontend/src/features/budget-structure/components/BudgetLineModal.vue`
- `Project/frontend/src/features/budget-structure/components/BudgetLineRow.vue`
- `Project/frontend/src/features/budget-structure/views/BudgetLinesView.vue`
- `Project/frontend/src/features/budget-execution/components/ExecutionRecordForm.vue`
- `Project/frontend/src/features/budget-execution/components/MatrixGroupRow.vue`
- `Project/frontend/src/features/budget-execution/components/MatrixCategoryRow.vue`
- `Project/frontend/src/features/budget-execution/components/MatrixLineRow.vue`
- `Project/frontend/src/features/budget-execution/components/MatrixTotalRow.vue` (NEW)
- `Project/frontend/src/features/budget-execution/views/BudgetMatrixView.vue`
- `Project/frontend/src/i18n/locales/en.json`
- `Project/frontend/src/i18n/locales/es.json`
- `Project/frontend/src/features/budget-execution/__tests__/ExecutionListModal.spec.ts`
- `Project/frontend/src/features/budget-execution/__tests__/ExecutionRecordForm.spec.ts`
