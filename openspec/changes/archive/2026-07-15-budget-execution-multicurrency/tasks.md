# Tasks: Budget Execution Multi-Currency

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 235–270 |
| 400-line budget risk | Low |
| Chained PRs recommended | No |
| Suggested split | Single PR |
| Delivery strategy | ask-on-risk |
| Chain strategy | N/A |

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: size-exception
400-line budget risk: Low

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | All phases as single PR | PR 1 | ~250 lines; well within budget |

---

## Phase 1: Store Foundation

Satisfies: REQ-MC-4, REQ-MATRIX-FOOTER-1 (W-001), REQ-MC-3 (convert() chain)

- [x] 1.1 In `src/features/budgetExecution/store.ts`: add getter `subtotalByLineType(periodId: string, lineType: 'Expense' | 'LongTermSavings' | 'PreventiveSavings'): { budgeted: number; executed: number }` — sums `budgetLines` (budgeted × convert() factor) and `periodTotals[periodId].categoryTotals` (filtered by lineType) for the given period.
- [x] 1.2 In `store.ts`: add action `syncExchangeRate(): void` — reads `structureStore.currentCycle.exchangeRate` and writes it to `matrixStore.exchangeRate`.
- [x] 1.3 In `__tests__/store.spec.ts` (or equivalent): add unit tests for `subtotalByLineType` — scenario: budgeted sum correct for Expense lineType; scenario: executed sum correct per periodId; scenario: unknown lineType returns { budgeted: 0, executed: 0 }.

---

## Phase 2: MatrixTotalRow Refactor (W-001)

Satisfies: REQ-MC-4, REQ-MATRIX-FOOTER-1

- [x] 2.1 In `src/features/budgetExecution/components/MatrixTotalRow.vue`: replace direct `budgetLines` / `categoryTotals` aggregation with three calls to `matrixStore.subtotalByLineType(periodId, lineType)` for Expense, PreventiveSavings, LongTermSavings; sum results for displayed budgeted and executed totals.
- [x] 2.2 In `MatrixTotalRow.vue`: add currency symbol next to displayed amounts — same resolution pattern as MatrixCell (see Phase 3).
- [x] 2.3 In `__tests__/MatrixTotalRow.spec.ts`: add unit test — scenario: total row budgeted = Expense.budgeted + PreventiveSavings.budgeted + LongTermSavings.budgeted; scenario: changing subtotal mock value updates total row reactively.

---

## Phase 3: MatrixCell Currency Symbol

Satisfies: REQ-MC-1

- [x] 3.1 In `src/features/budgetExecution/components/MatrixCell.vue`: replace `currencySymbol = ''` hardcode with a computed property that reads `structureStore.currentCycle.defaultCurrency.symbol` when `matrixStore.displayCurrency === 'default'` and `structureStore.currentCycle.alternateCurrency.symbol` when `matrixStore.displayCurrency === 'alternate'`.
- [x] 3.2 In `__tests__/MatrixCell.spec.ts`: add unit tests — scenario: symbol = "Q" when displayCurrency = "default" and DefaultCurrency.Symbol = "Q"; scenario: symbol = "$" when displayCurrency = "alternate" and AlternateCurrency.Symbol = "$"; scenario: toggling displayCurrency updates symbol reactively.

---

## Phase 4: MatrixSummaryRow Currency Symbol

Satisfies: REQ-MC-3 (footer subtotals), REQ-MATRIX-FOOTER-1

- [x] 4.1 In `src/features/budgetExecution/components/MatrixSummaryRow.vue`: pass resolved currency symbol (same source as MatrixCell) into `formatAmount()` calls for budgeted, executed, and difference amounts.
- [x] 4.2 In `__tests__/MatrixSummaryRow.spec.ts`: add unit test — scenario: Expense subtotal row displays "$" symbol when displayCurrency = "alternate".

---

## Phase 5: MatrixControls Exchange Rate Input

Satisfies: REQ-MC-2

- [x] 5.1 In `src/features/budgetExecution/components/MatrixControls.vue`: add `localExchangeRate` ref initialized from `matrixStore.exchangeRate`; conditionally render `<input type="number">` bound to `localExchangeRate` only when `matrixStore.displayCurrency === 'alternate'`; remove or hide the static `<span>` for the rate.
- [x] 5.2 In `MatrixControls.vue`: bind `:readonly="visiblePeriods.every(p => p.isClosed)"` on the exchange rate input.
- [x] 5.3 In `MatrixControls.vue`: on `blur` and `keydown.enter`: call `structureStore.loadCycleDetail()` → `structureStore.updateCycle({ ...currentCycle, exchangeRate: localExchangeRate.value })` → `structureStore.loadCycleDetail()` → `matrixStore.syncExchangeRate()`.
- [x] 5.4 In `__tests__/MatrixControls.spec.ts`: add unit tests — scenario: input absent when displayCurrency = "default"; scenario: input present and editable when displayCurrency = "alternate" and at least one period open; scenario: input present but readonly when all periods closed; scenario: blur triggers loadCycleDetail → updateCycle → loadCycleDetail → syncExchangeRate in order.

---

## Phase 6: i18n Cleanup (S-002)

Satisfies: REQ-S002

- [x] 6.1 In `src/i18n/locales/en.json`: remove key `budgetExecution.form.noteRequired` and key `budgetExecution.form.validation.noteRequired`.
- [x] 6.2 In `src/i18n/locales/es.json`: remove the same two keys.
- [x] 6.3 In `src/features/budgetExecution/__tests__/ExecutionRecordForm.spec.ts`: remove `noteRequired` from the i18n fixture object (line ~21).
- [x] 6.4 In `src/features/budgetExecution/__tests__/ExecutionListModal.spec.ts`: remove `noteRequired` from the i18n fixture object (line ~95).
- [x] 6.5 Confirm: run `npm run build` or locale linter to verify zero orphan i18n key warnings for these keys.

---

## Phase 7: S-001 SQLitePCLRaw Verification (conditional)

Satisfies: REQ-S001

- [x] 7.1 Run `dotnet list package --vulnerable --include-transitive` in `MyBudget.Features/` and `MyBudget.Features.Tests/`.
- [x] 7.2 If `SQLitePCLRaw.lib.e_sqlite3` appears as vulnerable: add explicit `<PackageReference Include="SQLitePCLRaw.lib.e_sqlite3" Version="{latest-non-vulnerable}" />` to `MyBudget.Features/MyBudget.Features.csproj` and `MyBudget.Features.Tests/MyBudget.Features.Tests.csproj`.
- [x] 7.3 If output is clean: document as no-op in PR description; no file changes.
