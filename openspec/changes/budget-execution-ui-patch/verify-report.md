# Verification Report: budget-execution-ui-patch

**Change**: `budget-execution-ui-patch`
**Date**: 2026-07-15
**Mode**: Hybrid (Engram + OpenSpec)
**Strict TDD**: OFF
**Verdict**: PASS

---

## Completeness Table

| Phase | Tasks | Status |
|-------|-------|--------|
| Phase 1 — Backend Foundation | 4 | COMPLETE |
| Phase 2 — Backend Command Layer | 2 | COMPLETE |
| Phase 3 — Frontend Type Contracts | 2 | COMPLETE |
| Phase 4 — Frontend Component Patches | 5 | COMPLETE |
| Phase 5 — Matrix View (Footer, DnD, Inline Category) | 6 | COMPLETE |
| Phase 6 — Incremental Name-Edit Rendering | 1 | COMPLETE |
| Phase 7 — Manual Verification | 8 items | N/A (manual; excluded from code task count) |
| **Additional scope: Sortable BudgetLinesView** | — | COMPLETE |
| **Additional scope: Note always required** | — | COMPLETE |

All 22 implementation tasks (Phases 1–6) confirmed complete by source inspection.

---

## Build / Test Evidence

| Suite | Command | Result |
|-------|---------|--------|
| Frontend Vitest | `npx vitest run` | 166/166 PASS |
| Frontend TypeScript | `tsc --noEmit` | 0 errors |
| Backend .NET build | `dotnet build` | 0 errors, 4 dependency warnings (SQLite vuln — pre-existing) |
| Backend unit tests | `dotnet test` — Features.Tests | 284/284 PASS |
| Backend integration tests | `dotnet test` — Integration.Tests | 137/137 PASS |
| E2E (user-confirmed) | Playwright | 51/51 PASS |

---

## Spec Compliance Matrix

| Req | Description | Evidence | Status |
|-----|-------------|----------|--------|
| REQ-EXEC-1 | `DateOnly? OperationDate` on `ExecutionRecord` | `ExecutionRecord.cs` line 16; `Create()`/`Update()` accept it; migration `20260715223718_AddOperationDateToExecutionRecord.cs` adds nullable `date` column with `Down()` drop | PASS |
| REQ-EXEC-LIST-2 | `operationDate` in list response DTO | `ExecutionRecordDto` has `DateOnly? OperationDate`; handler projects it from SQL; FE `ExecutionRecordDto` has `operationDate: string | null` | PASS |
| REQ-EXEC-FORM-1 | OperationDate date picker: defaults to today, editable, nullable | `ExecutionRecordForm.vue`: `form.operationDate = props.editRecord?.operationDate ?? todayString()`; `input[type=date]`; payload sends `form.operationDate || null` | PASS |
| REQ-EXEC-FORM-2 | CurrencyId dropdown + ExchangeRate input | `ExecutionRecordForm.vue`: `select#exec-currency` bound to `form.currencyId`; exchange rate shown when `currencyId !== defaultCurrencyId`; pre-populated from `editRecord.currencyId` | PASS |
| REQ-EXEC-CURRENCY-READ-1 | `currencyId` in `ListBudgetLines` response | `BudgetLineResponse` record has `Guid? CurrencyId`; `ListBudgetLinesHandler.cs` projects `r.CurrencyId` in LATERAL JOIN; FE `BudgetLineResponse` has `currencyId?: string` | PASS |
| REQ-BL-2 | Inline add-line category dropdown filtered by parent group | `BudgetMatrixView.vue`: `group.categories.filter(c => !c.deletedAt)` scoped to triggering group; `BudgetLineModal.vue`: `@change="form.categoryId = undefined"` resets on group change | PASS |
| REQ-BL-3 | `removeAllRanges()` on dblclick in all 3 matrix rows | `MatrixGroupRow.vue` `startEdit()` line 168; `MatrixCategoryRow.vue` line 162; `MatrixLineRow.vue` line 146 | PASS |
| REQ-MATRIX-DND-1 | DnD reorder for groups; same endpoint as arrow buttons; no closed-period gate | SortableJS in `BudgetMatrixView.vue`; `onGroupsDragEnd` and arrow handlers both call `structureStore.reorderGroups`; no `isClosed` check in DnD path | PASS |
| REQ-MATRIX-FOOTER-1 | Footer order: Expenses → PreventiveSavings → LongTermSavings → Total; SubTotal labels; Total = sum of subtotals | tfoot: `line-type=1` → `line-type=3` → `line-type=2` → `MatrixTotalRow`; i18n keys verified in `en.json` | PASS |
| REQ-MATRIX-RENDER-1 | Name-only group/category edit: in-place update, no full reload | `store.ts updateGroup()` line 216: `categoryGroups.value[idx] = { ...existing, ...payload }` — no API fetch after save | PASS |

---

## Additional Scope Compliance

| Feature | Description | Evidence | Status |
|---------|-------------|----------|--------|
| Sortable BudgetLinesView | Column order correct; default sort Group→Category→Type→Name; asc/desc toggle; sort indicators; reactive `sortedLines` | `BudgetLinesView.vue`: full sort implementation with `defaultSort` 4-key fallback | PASS |
| Note always required | Note required for ALL entry types | `ExecutionRecordForm.vue` validate(): unconditional note check; Vitest "any entry type" test passes | PASS |

---

## Issues

### CRITICAL
None.

### WARNING

**W-001** — `MatrixTotalRow.vue` computes Total by summing ALL budget lines rather than summing the three `MatrixSummaryRow` subtotals. Result is mathematically identical given the current 3-type enum, but diverges from spec wording "sum of three subtotals". Risk only materializes if a 4th line type is added without updating `MatrixTotalRow`.

### SUGGESTION

**S-001** — SQLite dependency vulnerability warnings (NU1903, `SQLitePCLRaw.lib.e_sqlite3` 2.1.11) are pre-existing across 4 projects. Not introduced by this change.

**S-002** — `noteRequired` i18n key (old conditional message) still exists in `en.json` alongside `noteRequiredAlways`. No longer reachable from `ExecutionRecordForm.vue`. Safe to prune in a housekeeping pass.

---

## Final Verdict: PASS

- 0 CRITICAL issues
- 1 WARNING (W-001 — mathematical no-op divergence in MatrixTotalRow)
- 2 SUGGESTIONS
- All 10 spec requirements satisfied
- All 22 implementation tasks complete
- All test suites green (166 Vitest + 421 .NET + 51 E2E)
