# Exploration: budget-execution-ui-patch

## Current State

`budget-execution-ui` is archived. The matrix renders, CRUD works for all structural entities and ExecutionRecords. Arrow-button reorder is wired for groups/categories/lines. Eight gaps remain.

---

## Affected Areas

### 1. BudgetLineModal — category selector

`Project/frontend/src/features/budget-structure/components/BudgetLineModal.vue`

The category dropdown IS rendered (lines 67–79). `filteredCategories` filters by `form.categoryGroupId` but does NOT exclude deleted categories. The i18n key used for the label (`budgetStructure.categories.edit`, line 68) is an action key, not a label key — cosmetic bug.

More significant: the **inline add-line row** in `BudgetMatrixView.vue` (lines 136–157) only captures `name` via a bare `<input>`. No lineType selector, no currency, no category selector beyond the implicit `categoryId` from the `+` button context. The ROADMAP note "category selector not exposed" most likely refers to this inline create path, not the full `BudgetLineModal`.

---

### 2. BudgetLine currency bug (confirmed)

`Project/frontend/src/features/budget-structure/components/BudgetLineModal.vue` lines 102–106, 170, 181–191
`Project/frontend/src/features/budget-structure/types.ts` — `CreateBudgetLinePayload.currency?: string`, `UpdateBudgetLinePayload.currency?: string`

Backend: `CreateBudgetLineRequest.CurrencyId: Guid?` and `UpdateBudgetLineRequest.CurrencyId: Guid?`. Field names differ (`currency` vs `CurrencyId`) and types differ (string code vs Guid). JSON deserialization → `CurrencyId = null` → handler falls back to `line.Period.Cycle.DefaultCurrencyId`. Currency selection always ignored.

`BudgetLineResponse` exposes `currencyCode` and `currencySymbol` but NOT `currencyId`. Backend `ListBudgetLines` read model must be extended to return `currencyId` for pre-population in edit mode.

---

### 3. Drag-and-drop reorder

`Project/frontend/package.json` line 24: `"vue-draggable-plus": "^0.6.1"` — installed, not used in any matrix component. Arrow buttons functional. Backend reorder endpoints exist. DnD should be additive (drag handles alongside arrows).

---

### 4. Summary footer

`Project/frontend/src/features/budget-execution/views/BudgetMatrixView.vue` lines 222–237: current order is Expenses(1) → LongTermSavings(2) → PreventiveSavings(3).

Required: Expenses(1) → PreventiveSavings(3) → LongTermSavings(2), rename "Total X" → "SubTotal X", add Total row (sum of 3 SubTotals).

i18n keys: `Project/frontend/src/i18n/locales/en.json` and `es.json`, `budgetMatrix.summary.*`.

---

### 5. STATUS_BREAKPOINT crash (partially fixed)

`Project/frontend/src/features/budget-execution/components/MatrixCell.vue` line 29: `window.getSelection()?.removeAllRanges()` — **ALREADY FIXED** here.

Still missing:
- `MatrixGroupRow.vue` line 66: `@dblclick="startEdit"` — `startEdit()` does not clear selection
- `MatrixCategoryRow.vue` line 66: `@dblclick="startEdit"` — same
- `MatrixLineRow.vue` line 37: `@dblclick="!line.deletedAt && openEditModal()"` — `openEditModal()` does not clear selection

---

### 6. Render optimization

`Project/frontend/src/features/budget-execution/store.ts`: execution CRUD already uses `_invalidateAndRefresh(lineId, periodId)` — incremental. Structural mutations (group/category/line edit/delete/restore) all call `invalidateAllPeriods()`. Group/category name-only edits are safe to skip period reload; all others are legitimately full.

Verdict: optimization scope is narrow — only name-only edits of groups/categories can be made incremental. Other full reloads are correct.

---

### 7. ExecutionRecord — operationDate + form exposure

Entity: `Project/src/MyBudget.Features/SharedKernel/Entities/ExecutionRecord.cs` — `CurrencyId`, `ExchangeRate`, `ExchangeRateTo` exist. `OperationDate` is **absent** (confirmed codebase-wide).

Form: `Project/frontend/src/features/budget-execution/components/ExecutionRecordForm.vue` — only `entryType`, `amount`, `note`. `currencyId` hardcoded to cycle default at line 149. `exchangeRate` always null at line 156.

Migration needed: `AddOperationDateToExecutionRecord` — nullable `DateOnly` column, safe additive change.

---

### 8. Multi-currency matrix display

`PeriodTotalsDto.netTotal` aggregates raw amounts server-side without per-record exchange rate. Closed-period per-record conversion requires backend query change. **Deferred to Phase 3.**

---

## Recommended Phasing

| Phase | Scope | Risk |
|-------|-------|------|
| Phase 1 — Frontend patch | DnD, footer, STATUS_BREAKPOINT (3 remaining handlers), currency bug (backend read model + frontend payload), category in inline form | Medium |
| Phase 2 — OperationDate | Entity, migration, handlers, form | Medium |
| Phase 3 — Multi-currency totals | Backend totals query, matrix display | High — defer |

---

## Risks

- Currency bug fix ripples to `BudgetLinesView` callers — all callers of `CreateBudgetLinePayload`/`UpdateBudgetLinePayload` must be audited
- `BudgetLineResponse` missing `currencyId` field — backend `ListBudgetLines` read model must be extended
- DnD in mixed-row flat tbody is structurally complex; arrow buttons are functional — DnD must be additive only
- `OperationDate` migration is safe (nullable column) but all handler unit/integration tests must be updated
- Multi-currency totals (closed-period per-record exchange rate) requires backend `PeriodTotals` query change — highest risk, recommended defer
- i18n key renaming (summary SubTotal labels) requires coordinated update in both `en.json` and `es.json`

---

## Ready for Proposal

Yes — Phase 1 and Phase 2 are ready for `sdd-propose`.
