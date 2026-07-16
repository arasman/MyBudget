# Exploration: budget-execution-multicurrency

**Date**: 2026-07-15
**Engram topic**: `sdd/budget-execution-multicurrency/explore`

---

## Scope

Phase-3 of budget execution. Four items:

| ID | Item |
|---|---|
| A | Full multi-currency matrix display (currency toggle affects all cells + footer) |
| W-001 | Refactor MatrixTotalRow to sum from 3 MatrixSummaryRow subtotals |
| S-001 | Upgrade SQLitePCLRaw.lib.e_sqlite3 2.1.11 in test projects |
| S-002 | Prune 2 stale i18n keys from en.json / es.json |

---

## Key Findings

### A — Multi-currency display

**Conversion chain is already correct end-to-end:**
- `ListPeriodExecutionTotalsHandler` converts records at the DB level: `amount / ExchangeRate` when `CurrencyId != DefaultCurrencyId` → totals arrive at frontend already in default currency
- `useCurrencyDisplay.convert(amount)` divides by cycle exchange rate to go default → alternate

**Gaps:**
- `MatrixControls.vue`: exchange rate displayed as static `<span>` — no editable input
- `MatrixCell.vue`: `currencySymbol = ''` hardcoded (deferred from PR5)

**Exchange rate save approach:**
- Reuse existing `structureStore.updateCycle()` (PUT full cycle) — no new backend slice needed
- `structureStore.currentCycle` already has all required cycle fields
- Guard: re-fetch cycle detail before saving to avoid stale-snapshot overwrite

**Period open/closed check:**
- `matrixStore.allPeriods[n].isClosed` available
- "At least 1 open period" = `visiblePeriods.some(p => !p.isClosed)`
- Input editable when true; read-only when all closed

### W-001 — MatrixTotalRow refactor

**Current:**
- `totalBudgeted()` sums all `structureStore.budgetLines.budgetedAmount` directly
- `totalExecuted()` sums all `categoryTotals.netTotal`
- No communication between `MatrixTotalRow` and the 3 `MatrixSummaryRow` siblings

**Approach: store-level computed subtotals**
- Add getters to store: `subtotalByLineType(lineType, periodId)` for budgeted and executed
- `MatrixTotalRow` sums the 3 lineType getters — no prop-drilling

### S-001 — SQLitePCLRaw

- ROADMAP scope says 4 projects — repo has **2** test projects (`MyBudget.Features.Tests`, `MyBudget.Integration.Tests`)
- Both reference `Microsoft.EntityFrameworkCore.Sqlite Version="10.*"` (transitive)
- No direct `SQLitePCLRaw` pin in any `.csproj`
- **Action**: run `dotnet list package --vulnerable` before writing tasks; pin only if transitive version is still 2.1.11

### S-002 — Stale i18n keys

Two dead keys in both `en.json` and `es.json`:
- `budgetExecution.form.noteRequired` — zero usages in production code
- `budgetExecution.form.validation.noteRequired` — used only in 2 test stubs, not in any `.vue`/`.ts`

Production code uses only `budgetExecution.form.validation.noteRequiredAlways`.

Affected test files:
- `Project/frontend/src/features/budget-execution/__tests__/ExecutionRecordForm.spec.ts`
- `Project/frontend/src/features/budget-execution/__tests__/ExecutionListModal.spec.ts`

---

## Affected Files

### Frontend
- `MatrixControls.vue` — add editable exchange rate input + save logic
- `MatrixCell.vue` — wire currency symbol (replace `''` with real symbol)
- `MatrixTotalRow.vue` — W-001 refactor: consume store subtotals
- `store.ts` — add `subtotalByLineType` computed getters
- `en.json` / `es.json` — remove 2 dead keys
- 2 test stubs — update fixture after key removal

### Backend
- None (all conversion logic already correct)

### .NET projects (S-001 — conditional)
- `MyBudget.Features.Tests.csproj` — pin only if vulnerable transitive found
- `MyBudget.Integration.Tests.csproj` — pin only if vulnerable transitive found

---

## Approaches Evaluated

| Area | Option | Decision |
|---|---|---|
| Exchange rate save | A1: Reuse PUT /cycles | **Selected** — no backend changes |
| Exchange rate save | A2: New PATCH /exchange-rate | Rejected — adds backend scope |
| W-001 | B1: Store computed subtotals | **Selected** — single source, thin components |
| W-001 | B2: Emit from SummaryRow → parent | Rejected — prop-drilling, fragile |
| S-001 | C1: Verify transitive, pin if needed | **Selected** — correct scope |
| S-001 | C2: Unconditional pin | Rejected — may conflict with EF Core resolution |
| S-002 | D2: Remove both keys + update 2 test stubs | **Selected** — complete cleanup |

---

## Risks

| Risk | Severity | Mitigation |
|---|---|---|
| Exchange rate direction (`amount / rate` not `amount * rate`) | HIGH | Preserve existing `convert()` formula exactly; add unit test |
| Stale `currentCycle` snapshot on exchange rate save | MEDIUM | Re-fetch cycle detail before PUT |
| S-001 scope: 2 projects not 4 | LOW | Verify with `dotnet list package --vulnerable` |
| W-001 double-counting across 3 lineType subtotals | MEDIUM | Unit test that union of 3 subtotals = total |

---

## Ready for Proposal

Yes. All scope items understood. S-001 verification task should be the first task in apply phase.
