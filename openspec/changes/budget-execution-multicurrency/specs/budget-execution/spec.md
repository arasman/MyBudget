# Delta for budget-execution

**Change name**: budget-execution-multicurrency
**Type**: Delta on existing capability
**Date**: 2026-07-15
**Capability**: budget-execution (modified)

---

## ADDED Requirements

### Requirement: REQ-MC-1 — Currency Symbol in Matrix Cells

Each matrix cell displaying a monetary amount MUST show the currency symbol of the currently selected display currency. When display currency is `default`, the symbol MUST be derived from `Cycle.DefaultCurrencyId → Currency.Symbol`. When display currency is `alternate`, the symbol MUST be derived from `Cycle.AlternateCurrencyId → Currency.Symbol`. A cell MUST NOT display an empty string in place of the symbol when a valid display currency is active.

#### Scenario: Default currency selected — GTQ symbol shown

- GIVEN a cycle with DefaultCurrency.Symbol = "Q" and displayCurrency = "default"
- WHEN a matrix cell renders a monetary amount
- THEN the cell displays "Q" as the currency symbol next to the amount

#### Scenario: Alternate currency selected — USD symbol shown

- GIVEN a cycle with AlternateCurrency.Symbol = "$" and displayCurrency = "alternate"
- WHEN a matrix cell renders a monetary amount
- THEN the cell displays "$" as the currency symbol next to the amount

---

### Requirement: REQ-MC-2 — Editable Exchange Rate Input in MatrixControls

When displayCurrency is `alternate`, MatrixControls MUST render a numeric exchange rate input pre-populated with `Cycle.ExchangeRate`. The input MUST be editable when at least one period in the current view has `isClosed = false`. The input MUST be read-only when all visible periods have `isClosed = true`. Submitting the input MUST re-fetch the cycle via `loadCycleDetail()` and then call `PUT /api/budgets/{budgetId}/cycles/{cycleId}` with the full cycle payload and the updated `exchangeRate`. When displayCurrency is `default`, the exchange rate input MUST NOT be rendered.

#### Scenario: Alternate currency selected with open period — input is editable

- GIVEN displayCurrency = "alternate" AND at least one period has isClosed = false
- WHEN MatrixControls renders
- THEN the exchange rate input is visible and accepts user input

#### Scenario: Alternate currency selected with all periods closed — input is read-only

- GIVEN displayCurrency = "alternate" AND all visible periods have isClosed = true
- WHEN MatrixControls renders
- THEN the exchange rate input is visible but read-only

#### Scenario: Default currency selected — exchange rate input absent

- GIVEN displayCurrency = "default"
- WHEN MatrixControls renders
- THEN no exchange rate input is present in the DOM

#### Scenario: Saving a new rate calls PUT cycle and matrix values update

- GIVEN displayCurrency = "alternate", an open period, and the user enters rate = 8.0
- WHEN the user saves the exchange rate input
- THEN `loadCycleDetail()` is called first, then `PUT /cycles/{cycleId}` is called with the full cycle payload and exchangeRate = 8.0
- AND all matrix cell values recalculate using the new rate

---

### Requirement: REQ-MC-3 — Display-Only Currency Conversion for All Matrix Values

All monetary values in the matrix — per-cell budgeted, executed, and difference amounts; lineType subtotals; and the total row — MUST reflect the selected display currency. Conversion is display-only; no stored records are mutated. The conversion MUST use `useCurrencyDisplay.convert(amount)` without modification to its formula. When a value is already in the selected display currency, it MUST be shown as-is. When a value is in the opposite currency, it MUST be converted via: `amount_in_alternate = amount_in_default / ExchangeRate`; `amount_in_default = amount_in_alternate × ExchangeRate`. Footer subtotals and the total row MUST follow the same conversion rules as individual cells.

#### Scenario: Record in default currency — shown as-is when default selected

- GIVEN a budget line value stored in the default currency (GTQ) and displayCurrency = "default"
- WHEN the matrix renders that cell
- THEN the amount is displayed without conversion

#### Scenario: Record in alternate currency — converted using cycle rate when default selected

- GIVEN an execution record with Amount = 75 in alternate currency (USD), ExchangeRate = 7.5, and displayCurrency = "default"
- WHEN the matrix renders that cell
- THEN the displayed value is 75 × 7.5 = 562.50 (GTQ)

#### Scenario: Record in default currency — converted using cycle rate when alternate selected

- GIVEN a budget line amount = 750 in default currency (GTQ), ExchangeRate = 7.5, and displayCurrency = "alternate"
- WHEN the matrix renders that cell
- THEN the displayed value is 750 / 7.5 = 100.00 (USD)

#### Scenario: Record in alternate currency — shown as-is when alternate selected

- GIVEN an execution record with Amount = 100 in alternate currency (USD) and displayCurrency = "alternate"
- WHEN the matrix renders that cell
- THEN the amount is displayed as 100.00 without additional conversion

#### Scenario: Footer subtotals reflect conversion

- GIVEN displayCurrency = "alternate" and ExchangeRate = 7.5, and an Expense subtotal of 1500 GTQ
- WHEN the summary footer renders
- THEN the Expense subtotal row shows 1500 / 7.5 = 200.00 (USD)

---

### Requirement: REQ-MC-4 — MatrixTotalRow Derives Values from LineType Subtotals

The Total row in the matrix footer MUST derive its budgeted and executed values by summing the three lineType subtotals: Expense, PreventiveSavings, and LongTermSavings. The Total row MUST NOT aggregate raw budget lines or execution records directly. Each subtotal value MUST be provided by a store getter `subtotalByLineType(lineType, periodId)` that returns `{ budgeted, executed }` per lineType per period. The three lineType subtotals MUST be the single source of truth consumed by MatrixTotalRow; no separate aggregation path is permitted.

#### Scenario: Total row equals sum of three lineType subtotals

- GIVEN Expense subtotal = { budgeted: 1000, executed: 800 }, PreventiveSavings subtotal = { budgeted: 200, executed: 150 }, LongTermSavings subtotal = { budgeted: 300, executed: 250 }
- WHEN MatrixTotalRow renders
- THEN budgeted total = 1500 AND executed total = 1200

#### Scenario: Changing exchange rate updates total via subtotal chain

- GIVEN displayCurrency = "alternate", ExchangeRate = 7.5, and total budgeted in default currency = 1500 GTQ
- WHEN the user saves a new ExchangeRate = 10.0
- THEN subtotalByLineType values recalculate, and the Total row displays 150.00 (USD) instead of 200.00

---

## ADDED Requirements — Maintenance Items

### Requirement: REQ-S001 — SQLitePCLRaw Vulnerability Verification

The project MUST be verified for the SQLitePCLRaw GHSA-v5pm-xwqc-g5wc vulnerability by running `dotnet list package --vulnerable` against `MyBudget.Features.csproj` and `MyBudget.Features.Tests.csproj`. An explicit `PackageReference` pin for `SQLitePCLRaw.lib.e_sqlite3` MUST be added to each affected `.csproj` only if the transitive version resolved by `Microsoft.EntityFrameworkCore.Sqlite` is still vulnerable. If the resolved version is non-vulnerable, no pin is required.

#### Scenario: Transitive version is non-vulnerable — no pin added

- GIVEN `dotnet list package --vulnerable` returns no findings for SQLitePCLRaw
- WHEN the verification step completes
- THEN no explicit SQLitePCLRaw pin is added to any csproj

#### Scenario: Transitive version is vulnerable — pin added to both csproj files

- GIVEN `dotnet list package --vulnerable` reports SQLitePCLRaw as vulnerable
- WHEN the fix is applied
- THEN an explicit `PackageReference` with the latest non-vulnerable version is added to `MyBudget.Features.csproj` and `MyBudget.Features.Tests.csproj`

---

### Requirement: REQ-S002 — Stale i18n Key Removal

The keys `budgetExecution.form.noteRequired` and `budgetExecution.form.validation.noteRequired` MUST be removed from `en.json` and `es.json`. The two test files that reference `form.validation.noteRequired` — `ExecutionRecordForm.spec.ts` and `ExecutionListModal.spec.ts` — MUST be updated to use `budgetExecution.form.validation.noteRequiredAlways` or remove the obsolete assertion. After removal, no orphan i18n key warnings MAY appear for these keys in either locale file.

#### Scenario: Stale key removed — no production reference broken

- GIVEN `budgetExecution.form.noteRequired` is deleted from en.json and es.json
- WHEN the application is built
- THEN no production Vue component references a missing i18n key

#### Scenario: Test stubs updated after key removal

- GIVEN `budgetExecution.form.validation.noteRequired` is removed from both locale files
- WHEN `ExecutionRecordForm.spec.ts` and `ExecutionListModal.spec.ts` run
- THEN all tests pass using the updated key or removed assertion

---

## MODIFIED Requirements

### Requirement: REQ-MATRIX-FOOTER-1 — Matrix Summary Footer Order, Labels, and Total Source

The budget matrix summary footer MUST display subtotals in the following fixed order: Expenses, PreventiveSavings, LongTermSavings. Each subtotal row MUST be labeled "SubTotal". A Total row MUST appear below the three SubTotal rows. The Total row MUST derive its values by summing the three SubTotal values produced by `subtotalByLineType(lineType, periodId)` store getters — it MUST NOT aggregate raw budget lines or execution records independently.

(Previously: Total row was free to aggregate from any source; no store getter was required.)

#### Scenario: Footer renders in correct order

- GIVEN a matrix with execution data across all three budget types
- WHEN the summary footer renders
- THEN rows appear in order: Expenses SubTotal → PreventiveSavings SubTotal → LongTermSavings SubTotal → Total

#### Scenario: Total row equals sum of three subtotals (store-getter source)

- GIVEN Expenses SubTotal = 1000, PreventiveSavings SubTotal = 200, LongTermSavings SubTotal = 300 (each from subtotalByLineType)
- WHEN the footer renders
- THEN the Total row displays 1500

#### Scenario: Footer labels use "SubTotal" text

- GIVEN the matrix summary footer is rendered
- WHEN the user views any of the three category rows
- THEN each row label reads "SubTotal"

---

## UNCHANGED

All other requirements from `openspec/specs/budget-execution/spec.md` (REQ-EXEC-1 through REQ-EXEC-CURRENCY-READ-1) are unchanged by this delta.
All requirements from `openspec/specs/budget-structure-ui/spec.md` not listed above are unchanged by this delta.
