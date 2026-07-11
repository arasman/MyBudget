# Verify Report: budget-structure-patch

**Change**: budget-structure-patch
**Date**: 2026-07-11
**Mode**: Standard (TDD OFF)
**Branch**: feat/budget-structure-patch
**Verdict**: PASS WITH WARNINGS

---

## Build / Test Evidence

| Suite | Command | Result |
|---|---|---|
| Backend unit tests | dotnet test | Passed: 218, Failed: 0, Skipped: 0 |
| Build | dotnet build (implicit) | SUCCESS |
| Notes | Dev-server DLL lock MSB3027 warnings — environment noise, not test failures |

---

## Task Completion

All 66 tasks across 3 PR slices are marked complete in tasks.md and confirmed via apply-progress.

| PR Slice | Tasks | Status |
|---|---|---|
| PR1 Currency + Cycle | PR1.1-PR1.24 | All complete |
| PR2 BudgetLine + DisplayOrder | PR2.1-PR2.18 | All complete |
| PR3 Restore endpoints | PR3.1-PR3.24 | All complete |

---

## Spec Compliance Matrix

### CUR - Currency Reference

| ID | Status | Notes |
|---|---|---|
| CUR-1 | PASS | Currency entity: Id, Code, Name, Symbol; private ctor; no BaseEntity |
| CUR-2 | PASS | HasData seeds GTQ/USD/EUR; CurrencySeeds GUIDs; migration inserts rows |
| CUR-3 | PASS | No DeletedAt on Currency |
| CUR-4 | PASS | GET /api/budgets/{id}/currencies; no budget existence check; full catalog |

### CYC - Cycle Currency Fields

| ID | Status | Notes |
|---|---|---|
| CYC-1 | PASS | DefaultCurrencyId Guid NOT NULL; migration default GTQ seed |
| CYC-2 | PASS | AlternateCurrencyId Guid? nullable FK |
| CYC-3 | PASS | ExchangeRate decimal? numeric(18,6) |
| CYC-4 | PASS | CYC_PAIR_INCOMPLETE on XOR in both validators; tests PR1.21, PR1.22 |
| CYC-5 | N/A | Semantic documentation only; no runtime enforcement required |
| CYC-6 | PASS | CreateCycleCommand + Handler; test PR1.23 |
| CYC-7 | PASS | UpdateCycleCommand + Handler; test PR1.24 |
| CYC-8 | PASS | GetCycleDetailHandler LEFT JOINs both currencies; projects CurrencyDto |
| CYC-9 | PASS | ListCyclesHandler JOINs DefaultCurrency; projects code+symbol |

### BLR - BudgetLine Revision Currency

| ID | Status | Notes |
|---|---|---|
| BLR-1 | PASS | BudgetLineRevision.CurrencyId Guid FK to Currencies |
| BLR-2 | PASS | Migration DELETE FROM BudgetLineRevisions before column change |
| BLR-3 | PASS | CreateBudgetLineHandler resolves Period->Cycle.DefaultCurrencyId when absent; test PR2.17 |
| BLR-4 | PASS | UpdateBudgetLineHandler same resolution; test coverage |
| BLR-5 | WARNING | WARN-001: flat currencyCode/currencySymbol vs spec nested currency object |

### BLD - BudgetLine DisplayOrder

| ID | Status | Notes |
|---|---|---|
| BLD-1 | PASS | BudgetLine.DisplayOrder int NOT NULL |
| BLD-2 | PASS | ROW_NUMBER() OVER PARTITION BY PeriodId,CategoryGroupId,CategoryId ORDER BY CreatedAt |
| BLD-3 | PASS | PUT .../budget-lines/order; REORDER_DUPLICATE_ID + REORDER_ID_NOT_IN_SCOPE; test PR2.18 |

### RST - Budget Restore

| ID | Status | Notes |
|---|---|---|
| RST-1 | PASS | Restore() on 5 entities: Cycle, BudgetLine, Period, CategoryGroup, Category; tests PR3.20 |
| RST-2 | PASS | RestoreCycleHandler: Cycle->restored Periods->BudgetLines of those Periods; test PR3.21 |
| RST-3 | PASS | RestoreCategoryGroupHandler: Group->Categories->BudgetLines by CategoryGroupId; test PR3.22 |
| RST-4 | WARNING | WARN-002: nested route deviates from flat spec route |
| RST-5 | WARNING | WARN-003: route uses lines/ vs spec budget-lines/ segment |
| RST-6 | PASS | All 4 endpoints accept bool includeExecutionRecords; handlers ignore it (no-op) |
| RST-7 | PASS | RestoreCategory 409 on CategoryGroup.DeletedAt; RestoreBudgetLine 409 on Period.DeletedAt |

---

## Validation Rules

| Code | Implemented | Tests |
|---|---|---|
| CYC_PAIR_INCOMPLETE | Yes - CreateCycleValidator + UpdateCycleValidator | PR1.21, PR1.22 |
| CYC_DEFAULT_CURRENCY_REQUIRED | Implicit via NotEmpty on DefaultCurrencyId | PR1.21, PR1.22 |
| PARENT_IS_DELETED | Yes - RestoreCategory + RestoreBudgetLine return 409 | PR3.23, PR3.24 |
| REORDER_ID_NOT_IN_SCOPE | Yes - handler returns 422 | PR2.18 |
| REORDER_DUPLICATE_ID | Yes - validator | PR2.18 |

---

## Issues

### WARNINGS

**WARN-001** | BLR-5 | ListBudgetLines/ListBudgetLinesQuery.cs
Spec specifies currency:{code,symbol} as a nested response object.
Implementation returns flat currencyCode / currencySymbol on BudgetLineResponse.
Functionally equivalent; JSON shape differs. Frontend already consumes flat shape. Intentional, no breakage.

**WARN-002** | RST-4 | RestoreCategory/RestoreCategoryEndpoint.cs:12
Spec route: POST /budgets/{budgetId}/categories/{categoryId}/restore
Implemented: POST /api/budgets/{id}/category-groups/{groupId}/categories/{categoryId}/restore
Nested route consistent with existing DELETE pattern for categories. Documented in apply-progress.

**WARN-003** | RST-5 | RestoreBudgetLine/RestoreBudgetLineEndpoint.cs:12
Spec route segment: budget-lines/{lineId}/restore
Implemented: lines/{lineId}/restore
Consistent with existing DELETE .../lines/{lineId} project pattern. Intentional and documented.

### SUGGESTIONS

**SUGG-001**: Update spec.md BLR-5 to reflect flat currencyCode/currencySymbol as canonical API shape.
**SUGG-002**: Update spec.md RST-4 and RST-5 route examples to match implemented nested routes.
**SUGG-003**: SQLitePCLRaw.lib.e_sqlite3 2.1.11 has known high-severity CVE GHSA-2m69-gcr7-jv3q. Non-blocking.

---

## Final Verdict

**PASS WITH WARNINGS**

- CRITICAL: 0
- WARNING: 3 (intentional documented deviations, no functional regression)
- SUGGESTION: 3
- Tasks: 66/66 complete
- Tests: 218 passed, 0 failed
- Archive readiness: READY