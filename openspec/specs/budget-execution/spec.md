# Spec: budget-execution

**Change name**: `budget-execution`
**Type**: New capability + delta on `budget-structure`
**Depends on**: `budget-structure-patch` (archived 2026-07-11)
**Date**: 2026-07-13

---

## Capability Index

| Capability | Type | Requirements |
|---|---|---|
| `budget-execution` | New | REQ-EXEC-1 … REQ-EXEC-CASCADE-2 |
| `budget-structure` | Modified (delta) | RST-6 activated (IncludeExecutionRecords no longer no-op) |

---

## Requirements Table

| ID | Capability | Statement |
|---|---|---|
| REQ-EXEC-1 | `budget-execution` | An `ExecutionRecord` entity MUST exist with: `Id` (Guid PK), `BudgetLineId` (Guid FK, NOT NULL), `PeriodId` (Guid, NOT NULL, denormalized), `EntryType` (int, NOT NULL), `Amount` (decimal(18,2), NOT NULL, positive), `CurrencyId` (Guid FK, NOT NULL), `ExchangeRate` (decimal(18,6), nullable), `ExchangeRateTo` (decimal(18,6), nullable), `AccountId` (Guid?, nullable, no FK), `PaymentMethodId` (Guid?, nullable, no FK), `Note` (varchar(500), nullable), `OperationDate` (DateOnly, nullable), `CreatedAt`, `UpdatedAt`, `DeletedAt` (soft-delete). |
| REQ-EXEC-2 | `budget-execution` | `EntryType` MUST be an enum with exactly three values: `Expense = 1`, `CreditNote = 2`, `DebitNote = 3`. No other values are valid. |
| REQ-EXEC-3 | `budget-execution` | `Amount` MUST be greater than zero. Zero and negative values MUST be rejected with error code `AMOUNT_MUST_BE_POSITIVE` (400). |
| REQ-EXEC-4 | `budget-execution` | `Note` MUST be provided (non-null, non-empty) for ALL `EntryType` values: `Expense`, `CreditNote`, and `DebitNote`. Absence of `Note` MUST be rejected with error code `NOTE_REQUIRED` (400) regardless of entry type. |
| REQ-EXEC-5 | `budget-execution` | When `CurrencyId` equals the Cycle's `DefaultCurrencyId`, `ExchangeRate` and `ExchangeRateTo` MUST both be null. Providing either when currencies match MUST be rejected with error code `EXCHANGE_RATE_NOT_ALLOWED` (400). |
| REQ-EXEC-6 | `budget-execution` | When `CurrencyId` differs from the Cycle's `DefaultCurrencyId`, `ExchangeRate` and `ExchangeRateTo` MUST both be provided. Providing one without the other MUST be rejected with error code `EXCHANGE_RATE_PAIR_INCOMPLETE` (400). |
| REQ-EXEC-7 | `budget-execution` | The Period's date range MUST be covered by the BudgetLine's date range: `Period.StartDate >= BudgetLine.StartDate AND (BudgetLine.EndDate IS NULL OR Period.StartDate <= BudgetLine.EndDate)`. A mismatch MUST be rejected with error code `BUDGET_LINE_NOT_IN_PERIOD` (422). |
| REQ-EXEC-CLOSED-1 | `budget-execution` | ALL write operations (Create, Update, Delete, Restore) MUST check `Period.IsClosed` before proceeding. If the period is closed, the operation MUST be rejected with error code `PERIOD_CLOSED` (409). |
| REQ-EXEC-CREATE-1 | `budget-execution` | `POST /budgets/{budgetId}/periods/{periodId}/budget-lines/{lineId}/executions` MUST create an ExecutionRecord linked to the specified BudgetLine and return `201 Created` with the new record's `Id`. Requires role `budget:operator`. |
| REQ-EXEC-CREATE-2 | `budget-execution` | The handler MUST verify the BudgetLine exists and belongs to the specified Period and Budget. A missing or mismatched BudgetLine MUST return `404 Not Found`. |
| REQ-EXEC-UPDATE-1 | `budget-execution` | `PUT /budgets/{budgetId}/periods/{periodId}/budget-lines/{lineId}/executions/{executionId}` MUST update a non-deleted ExecutionRecord. Returns `200 OK`. Requires role `budget:operator`. |
| REQ-EXEC-UPDATE-2 | `budget-execution` | An update MUST apply REQ-EXEC-3, REQ-EXEC-4, REQ-EXEC-5, REQ-EXEC-6 on the incoming values. |
| REQ-EXEC-DELETE-1 | `budget-execution` | `DELETE /budgets/{budgetId}/periods/{periodId}/budget-lines/{lineId}/executions/{executionId}` MUST soft-delete an ExecutionRecord (set `DeletedAt = now`). Returns `204 No Content`. Requires role `budget:operator`. |
| REQ-EXEC-DELETE-2 | `budget-execution` | Deleting an already-soft-deleted ExecutionRecord MUST return `404 Not Found`. |
| REQ-EXEC-RESTORE-1 | `budget-execution` | `POST /budgets/{budgetId}/periods/{periodId}/budget-lines/{lineId}/executions/{executionId}/restore` MUST restore a soft-deleted ExecutionRecord (set `DeletedAt = null`). Returns `200 OK`. Requires role `budget:operator`. |
| REQ-EXEC-RESTORE-2 | `budget-execution` | Restoring a non-deleted (already active) ExecutionRecord MUST return `404 Not Found`. |
| REQ-EXEC-LIST-1 | `budget-execution` | `GET /budgets/{budgetId}/periods/{periodId}/budget-lines/{lineId}/executions` MUST return all non-deleted ExecutionRecords for the specified BudgetLine, ordered by `CreatedAt` ASC. Requires role `budget:read`. |
| REQ-EXEC-LIST-2 | `budget-execution` | Each item in the list response MUST include: `id`, `entryType`, `amount`, `currencyId`, `exchangeRate`, `exchangeRateTo`, `accountId`, `paymentMethodId`, `note`, `operationDate`, `createdAt`, `updatedAt`. |
| REQ-EXEC-TOTALS-1 | `budget-execution` | `GET /budgets/{budgetId}/periods/{periodId}/execution-totals` MUST return two aggregation shapes in a single response: per-BudgetLine and per-CategoryGroup/Category. Requires role `budget:read`. |
| REQ-EXEC-TOTALS-2 | `budget-execution` | The per-BudgetLine shape MUST contain: `budgetLineId`, `totalExpenses` (sum of Amount where EntryType=Expense), `totalCreditNotes` (sum where EntryType=CreditNote), `totalDebitNotes` (sum where EntryType=DebitNote), `netAmount` (Expenses + DebitNotes − CreditNotes). Only non-deleted records count. |
| REQ-EXEC-TOTALS-3 | `budget-execution` | The per-CategoryGroup/Category shape MUST contain: `categoryGroupId`, `categoryGroupName`, `categoryId`, `categoryName`, `netAmount`. It is grouped by the BudgetLine's CategoryGroupId and CategoryId. |
| REQ-EXEC-TOTALS-4 | `budget-execution` | Totals MUST be computed in the Cycle's `DefaultCurrencyId`. When an ExecutionRecord has a different `CurrencyId`, the amount MUST be converted using `Amount / ExchangeRate` before summing. |
| REQ-EXEC-CASCADE-1 | `budget-execution` | Soft-deleting a BudgetLine MUST cascade to soft-delete all its non-deleted child ExecutionRecords in the same DB operation (same `SaveChangesAsync`). |
| REQ-EXEC-CASCADE-2 | `budget-execution` | Restoring a BudgetLine with `includeExecutionRecords=true` MUST restore all soft-deleted child ExecutionRecords. With `includeExecutionRecords=false` (default), child ExecutionRecords remain soft-deleted. The same flag and behavior apply when BudgetLines are restored via Cycle, CategoryGroup, or Category cascade. |
| REQ-EXEC-DATE-RANGE-1 | `budget-execution` | `OperationDate` MUST fall within `MAX(Period.StartDate, BudgetLine.StartDate) .. MIN(Period.EndDate, BudgetLine.EndDate ?? Period.EndDate)`. Dates outside the range MUST be rejected with error code `OPERATION_DATE_OUT_OF_RANGE` (422). |
| REQ-EXEC-DECIMAL-VAL-1 | `budget-execution` | `ExecutionRecordForm.vue` MUST enforce client-side validation on decimal precision: `amount` MUST have at most 2 decimal places, `exchangeRate` MUST have at most 6 decimal places. Violations MUST block form submission and show inline messages using `budgetExecution.form.validation.amountDecimals` and `budgetExecution.form.validation.exchangeRateDecimals`. |
| REQ-EXEC-DATE-VAL-1 | `budget-execution` | `ExecutionRecordForm.vue` MUST validate that the selected `operationDate` falls within the parent period's date range. The period's StartDate and EndDate MUST be passed to the form as props or retrieved from context. Violations MUST show using key `budgetExecution.form.validation.operationDateOutOfRange`. API errors with code `OPERATION_DATE_OUT_OF_RANGE` MUST also produce an error toast using key `budgetExecution.form.errors.operationDateOutOfRange`. |
| REQ-EXEC-TOAST-MIGRATE-1 | `budget-execution` | `ExecutionRecordForm.vue` MUST remove its inline `submitError` alert banner. API errors MUST be surfaced exclusively via `toastStore.push({ type: 'error', title: t(key) })`. |
| REQ-EXEC-FORM-1 | `budget-execution` | `ExecutionRecordForm.vue` MUST expose an `OperationDate` date picker field. The field MUST default to today's date when the form is opened for creation. The field MUST be editable. The field MUST be nullable (clearing it sends null to the backend). |
| REQ-EXEC-FORM-2 | `budget-execution` | `ExecutionRecordForm.vue` MUST expose `CurrencyId` (currency dropdown) and `ExchangeRate` (numeric input) fields. These fields MUST map to the existing entity properties. Both fields MUST save and reload correctly via the create/update commands and list query. |
| REQ-EXEC-CURRENCY-READ-1 | `budget-execution` | The `ListBudgetLines` query response MUST include `currencyId` (Guid) per line so the frontend can pre-populate the currency field in the edit form without a separate lookup. |
| REQ-MC-1 | `budget-execution` | Each matrix cell displaying a monetary amount MUST show the currency symbol of the currently selected display currency. When display currency is `default`, the symbol MUST be derived from `Cycle.DefaultCurrencyId → Currency.Symbol`. When display currency is `alternate`, the symbol MUST be derived from `Cycle.AlternateCurrencyId → Currency.Symbol`. A cell MUST NOT display an empty string in place of the symbol when a valid display currency is active. |
| REQ-MC-2 | `budget-execution` | When displayCurrency is `alternate`, MatrixControls MUST render a numeric exchange rate input pre-populated with `Cycle.ExchangeRate`. The input MUST be editable when at least one period in the current view has `isClosed = false`. The input MUST be read-only when all visible periods have `isClosed = true`. Submitting the input MUST re-fetch the cycle via `loadCycleDetail()` and then call `PUT /api/budgets/{budgetId}/cycles/{cycleId}` with the full cycle payload and the updated `exchangeRate`. When displayCurrency is `default`, the exchange rate input MUST NOT be rendered. |
| REQ-MC-3 | `budget-execution` | All monetary values in the matrix — per-cell budgeted, executed, and difference amounts; lineType subtotals; and the total row — MUST reflect the selected display currency. Conversion is display-only; no stored records are mutated. The conversion MUST use `useCurrencyDisplay.convert(amount)` without modification to its formula. When a value is already in the selected display currency, it MUST be shown as-is. When a value is in the opposite currency, it MUST be converted via: `amount_in_alternate = amount_in_default / ExchangeRate`; `amount_in_default = amount_in_alternate × ExchangeRate`. Footer subtotals and the total row MUST follow the same conversion rules as individual cells. |
| REQ-MC-4 | `budget-execution` | The Total row in the matrix footer MUST derive its budgeted and executed values by summing the three lineType subtotals: Expense, PreventiveSavings, and LongTermSavings. The Total row MUST NOT aggregate raw budget lines or execution records directly. Each subtotal value MUST be provided by a store getter `subtotalByLineType(lineType, periodId)` that returns `{ budgeted, executed }` per lineType per period. The three lineType subtotals MUST be the single source of truth consumed by MatrixTotalRow; no separate aggregation path is permitted. |
| REQ-S001 | `budget-execution` | The project MUST be verified for the SQLitePCLRaw GHSA-v5pm-xwqc-g5wc vulnerability by running `dotnet list package --vulnerable` against `MyBudget.Features.csproj` and `MyBudget.Features.Tests.csproj`. An explicit `PackageReference` pin for `SQLitePCLRaw.lib.e_sqlite3` MUST be added to each affected `.csproj` only if the transitive version resolved by `Microsoft.EntityFrameworkCore.Sqlite` is still vulnerable. If the resolved version is non-vulnerable, no pin is required. |
| REQ-S002 | `budget-execution` | The keys `budgetExecution.form.noteRequired` and `budgetExecution.form.validation.noteRequired` MUST be removed from `en.json` and `es.json`. The two test files that reference `form.validation.noteRequired` — `ExecutionRecordForm.spec.ts` and `ExecutionListModal.spec.ts` — MUST be updated to use `budgetExecution.form.validation.noteRequiredAlways` or remove the obsolete assertion. After removal, no orphan i18n key warnings MAY appear for these keys in either locale file. |
| REQ-MATRIX-FOOTER-1 | `budget-execution` | The budget matrix summary footer MUST display subtotals in the following fixed order: Expenses, PreventiveSavings, LongTermSavings. Each subtotal row MUST be labeled "SubTotal". A Total row MUST appear below the three SubTotal rows. The Total row MUST derive its values by summing the three SubTotal values produced by `subtotalByLineType(lineType, periodId)` store getters — it MUST NOT aggregate raw budget lines or execution records independently. |

---

## Validation Error Codes

| Code | Trigger | HTTP |
|---|---|---|
| `AMOUNT_MUST_BE_POSITIVE` | Amount ≤ 0 on Create or Update | 400 |
| `NOTE_REQUIRED` | Note absent for any EntryType | 400 |
| `EXCHANGE_RATE_NOT_ALLOWED` | ExchangeRate or ExchangeRateTo provided when CurrencyId = DefaultCurrencyId | 400 |
| `EXCHANGE_RATE_PAIR_INCOMPLETE` | Exactly one of ExchangeRate / ExchangeRateTo provided when CurrencyId ≠ DefaultCurrencyId | 400 |
| `BUDGET_LINE_NOT_IN_PERIOD` | BudgetLine date range does not cover the Period | 422 |
| `OPERATION_DATE_OUT_OF_RANGE` | OperationDate outside Period.StartDate..Period.EndDate (inclusive) | 422 |
| `PERIOD_CLOSED` | Period.IsClosed = true on any write | 409 |
| `PARENT_IS_DELETED` | BudgetLine is soft-deleted on Create | 409 |

---

## RBAC

| Operation | Required Role |
|---|---|
| Create, Update, Delete, Restore | `budget:operator` |
| List, ListPeriodExecutionTotals | `budget:read` |

---

## Scenarios

### REQ-EXEC-3 — Amount Positive

#### Scenario: Amount zero rejected

- GIVEN a CreateExecution request with Amount = 0
- WHEN the validator runs
- THEN 400 Bad Request, error code AMOUNT_MUST_BE_POSITIVE

#### Scenario: Amount negative rejected

- GIVEN a CreateExecution request with Amount = -50.00
- WHEN the validator runs
- THEN 400 Bad Request, error code AMOUNT_MUST_BE_POSITIVE

#### Scenario: Amount positive accepted

- GIVEN a CreateExecution request with Amount = 100.00 and EntryType = Expense
- WHEN the handler processes the request
- THEN 201 Created

---

### REQ-EXEC-4 — Note Requirement

#### Scenario: Note absent for Expense rejected

- GIVEN a CreateExecution or UpdateExecution request with EntryType = Expense and Note = null or ""
- WHEN the validator runs
- THEN HTTP 400 with error code `NOTE_REQUIRED`

#### Scenario: Note absent for CreditNote rejected

- GIVEN a CreateExecution request with EntryType = CreditNote and Note = null
- WHEN the validator runs
- THEN HTTP 400 with error code `NOTE_REQUIRED`

#### Scenario: Note absent for DebitNote rejected

- GIVEN a CreateExecution request with EntryType = DebitNote and Note = ""
- WHEN the validator runs
- THEN HTTP 400 with error code `NOTE_REQUIRED`

#### Scenario: Note present for any entry type accepted

- GIVEN any EntryType and Note = "valid text"
- WHEN the validator runs
- THEN no validation error for Note

---

### REQ-EXEC-5 / REQ-EXEC-6 — ExchangeRate Pair Rule

#### Scenario: Same currency, exchange rate provided — rejected

- GIVEN CurrencyId = Cycle.DefaultCurrencyId AND ExchangeRate = 7.5
- WHEN the validator runs
- THEN 400 Bad Request, error code EXCHANGE_RATE_NOT_ALLOWED

#### Scenario: Different currency, both exchange rates provided — accepted

- GIVEN CurrencyId ≠ Cycle.DefaultCurrencyId AND ExchangeRate = 7.5 AND ExchangeRateTo = 0.133
- WHEN the validator runs
- THEN no validation error for exchange rate

#### Scenario: Different currency, one exchange rate missing — rejected

- GIVEN CurrencyId ≠ Cycle.DefaultCurrencyId AND ExchangeRate = 7.5 AND ExchangeRateTo = null
- WHEN the validator runs
- THEN 400 Bad Request, error code EXCHANGE_RATE_PAIR_INCOMPLETE

---

### REQ-EXEC-CLOSED-1 — Period Closed Guard

#### Scenario: Create on closed period rejected

- GIVEN Period.IsClosed = true
- WHEN POST .../executions with valid payload
- THEN 409 Conflict, error code PERIOD_CLOSED

#### Scenario: Update on closed period rejected

- GIVEN Period.IsClosed = true AND an existing ExecutionRecord
- WHEN PUT .../executions/{id} with valid payload
- THEN 409 Conflict, error code PERIOD_CLOSED

#### Scenario: Delete on closed period rejected

- GIVEN Period.IsClosed = true AND an existing ExecutionRecord
- WHEN DELETE .../executions/{id}
- THEN 409 Conflict, error code PERIOD_CLOSED

#### Scenario: Restore on closed period rejected

- GIVEN Period.IsClosed = true AND a soft-deleted ExecutionRecord
- WHEN POST .../executions/{id}/restore
- THEN 409 Conflict, error code PERIOD_CLOSED

---

### REQ-EXEC-CREATE-1 / REQ-EXEC-CREATE-2 — CreateExecution

#### Scenario: Happy path — Expense

- GIVEN an open Period, an active BudgetLine, EntryType=Expense, Amount=250.00, same CurrencyId as DefaultCurrencyId
- WHEN POST .../executions
- THEN 201 Created with new ExecutionRecord Id
- AND ExecutionRecord.PeriodId = BudgetLine.PeriodId

#### Scenario: BudgetLine not found

- GIVEN a lineId that does not exist under the given periodId
- WHEN POST .../executions
- THEN 404 Not Found

#### Scenario: BudgetLine soft-deleted

- GIVEN a soft-deleted BudgetLine
- WHEN POST .../executions
- THEN 409 Conflict, error code PARENT_IS_DELETED

#### Scenario: PeriodId mismatch

- GIVEN route periodId = P1 AND BudgetLine.PeriodId = P2 (P1 ≠ P2)
- WHEN handler validates
- THEN 400 Bad Request, error code PERIOD_MISMATCH

#### Scenario: RBAC — operator required

- GIVEN an authenticated user without role budget:operator
- WHEN POST .../executions
- THEN 403 Forbidden

---

### REQ-EXEC-UPDATE-1 — UpdateExecution

#### Scenario: Happy path update

- GIVEN an existing, non-deleted ExecutionRecord in an open Period
- WHEN PUT .../executions/{id} with EntryType=CreditNote, Amount=50.00, Note="refund"
- THEN 200 OK with updated record

#### Scenario: Update non-existent record

- GIVEN an executionId that does not exist or is soft-deleted
- WHEN PUT .../executions/{id}
- THEN 404 Not Found

---

### REQ-EXEC-DELETE-1 / REQ-EXEC-DELETE-2 — DeleteExecution

#### Scenario: Happy path soft-delete

- GIVEN an existing, non-deleted ExecutionRecord in an open Period
- WHEN DELETE .../executions/{id}
- THEN 204 No Content AND ExecutionRecord.DeletedAt is set

#### Scenario: Delete already-deleted record

- GIVEN a soft-deleted ExecutionRecord
- WHEN DELETE .../executions/{id}
- THEN 404 Not Found

---

### REQ-EXEC-RESTORE-1 / REQ-EXEC-RESTORE-2 — RestoreExecution

#### Scenario: Happy path restore

- GIVEN a soft-deleted ExecutionRecord in an open Period
- WHEN POST .../executions/{id}/restore
- THEN 200 OK AND ExecutionRecord.DeletedAt = null

#### Scenario: Restore already-active record

- GIVEN a non-deleted ExecutionRecord
- WHEN POST .../executions/{id}/restore
- THEN 404 Not Found

---

### REQ-EXEC-LIST-1 / REQ-EXEC-LIST-2 — ListExecutions

#### Scenario: Returns non-deleted records ordered by CreatedAt ASC

- GIVEN 3 ExecutionRecords for a BudgetLine (1 soft-deleted, 2 active)
- WHEN GET .../executions
- THEN 200 OK with 2 items ordered by CreatedAt ASC
- AND each item contains id, entryType, amount, currencyId, exchangeRate, exchangeRateTo, accountId, paymentMethodId, note, createdAt, updatedAt

#### Scenario: Empty list

- GIVEN a BudgetLine with no ExecutionRecords
- WHEN GET .../executions
- THEN 200 OK with empty array

#### Scenario: RBAC — read role required

- GIVEN an authenticated user without role budget:read
- WHEN GET .../executions
- THEN 403 Forbidden

---

### REQ-EXEC-TOTALS-1 through REQ-EXEC-TOTALS-4 — ListPeriodExecutionTotals

#### Scenario: Dual shape returned

- GIVEN a Period with 2 BudgetLines, each with multiple ExecutionRecords of mixed EntryTypes
- WHEN GET .../execution-totals
- THEN 200 OK with response containing `byBudgetLine` array AND `byCategory` array

#### Scenario: Per-BudgetLine netAmount calculation

- GIVEN BudgetLine with: Expense=100, Expense=50, CreditNote=30, DebitNote=20
- WHEN GET .../execution-totals
- THEN for that line: totalExpenses=150, totalCreditNotes=30, totalDebitNotes=20, netAmount=140

#### Scenario: Currency conversion in totals

- GIVEN an ExecutionRecord with CurrencyId ≠ DefaultCurrencyId, Amount=75, ExchangeRate=7.5
- WHEN totals are computed
- THEN the record contributes 75/7.5 = 10 to the netAmount in DefaultCurrency

#### Scenario: Soft-deleted records excluded from totals

- GIVEN a BudgetLine with 1 active Expense=100 and 1 soft-deleted Expense=200
- WHEN GET .../execution-totals
- THEN totalExpenses = 100 (deleted record excluded)

#### Scenario: Per-Category aggregation

- GIVEN two BudgetLines sharing the same CategoryGroupId and CategoryId, each with netAmount of 50
- WHEN GET .../execution-totals
- THEN byCategory contains one entry for that group/category with netAmount=100

---

### REQ-EXEC-CASCADE-1 / REQ-EXEC-CASCADE-2 — Cascade Soft-Delete / Restore

#### Scenario: BudgetLine delete cascades to ExecutionRecords

- GIVEN a BudgetLine with 3 active ExecutionRecords
- WHEN DELETE .../budget-lines/{lineId}
- THEN BudgetLine.DeletedAt is set
- AND all 3 ExecutionRecords.DeletedAt is set in the same operation

#### Scenario: BudgetLine restore without includeExecutionRecords

- GIVEN a soft-deleted BudgetLine with 3 soft-deleted ExecutionRecords
- WHEN POST .../budget-lines/{lineId}/restore (includeExecutionRecords=false or omitted)
- THEN BudgetLine.DeletedAt = null
- AND all 3 ExecutionRecords remain soft-deleted

#### Scenario: BudgetLine restore with includeExecutionRecords=true

- GIVEN a soft-deleted BudgetLine with 3 soft-deleted ExecutionRecords
- WHEN POST .../budget-lines/{lineId}/restore?includeExecutionRecords=true
- THEN BudgetLine.DeletedAt = null
- AND all 3 ExecutionRecords.DeletedAt = null

#### Scenario: Cycle restore with includeExecutionRecords=true cascades to ExecutionRecords

- GIVEN a soft-deleted Cycle → Period → BudgetLine → 2 soft-deleted ExecutionRecords
- WHEN POST .../cycles/{cycleId}/restore?includeExecutionRecords=true
- THEN Cycle, Period, BudgetLine, and both ExecutionRecords are all restored

---

### REQ-EXEC-DATE-RANGE-1 — OperationDate Within Period Range

#### Scenario: OperationDate within period accepted

- GIVEN Period StartDate=2025-01-01, EndDate=2025-01-31
- WHEN CreateExecution with OperationDate=2025-01-15
- THEN HTTP 201 Created

#### Scenario: OperationDate before period start rejected

- GIVEN Period StartDate=2025-01-01
- WHEN CreateExecution with OperationDate=2024-12-31
- THEN HTTP 422 with error code `OPERATION_DATE_OUT_OF_RANGE`

#### Scenario: OperationDate after period end rejected

- GIVEN Period EndDate=2025-01-31
- WHEN CreateExecution with OperationDate=2025-02-01
- THEN HTTP 422 with error code `OPERATION_DATE_OUT_OF_RANGE`

#### Scenario: OperationDate null — no range check

- GIVEN OperationDate = null
- WHEN the validator runs
- THEN no date-range error (null is permitted)

---

### REQ-EXEC-DECIMAL-VAL-1 — Decimal Precision Validation (Frontend)

#### Scenario: Amount with 3 decimal places blocked

- GIVEN amount = 10.123 in ExecutionRecordForm
- WHEN the user attempts to submit
- THEN submission is blocked and `budgetExecution.form.validation.amountDecimals` is shown

#### Scenario: ExchangeRate with 7 decimal places blocked

- GIVEN exchangeRate = 7.1234567 in ExecutionRecordForm
- WHEN the user attempts to submit
- THEN submission is blocked and `budgetExecution.form.validation.exchangeRateDecimals` is shown

#### Scenario: Valid decimal precision accepted

- GIVEN amount = 10.12 and exchangeRate = 7.123456
- WHEN the user submits
- THEN no decimal-precision error is raised

---

### REQ-EXEC-DATE-VAL-1 — OperationDate Out-of-Range (Frontend)

#### Scenario: OperationDate outside period range blocked client-side

- GIVEN period bounds Jan 1–31 2025 and the user selects Feb 1 2025
- WHEN the user attempts to submit
- THEN submission is blocked and `budgetExecution.form.validation.operationDateOutOfRange` is shown

#### Scenario: OperationDate out-of-range API error produces toast

- GIVEN operationDate passes client-side validation but the API returns `OPERATION_DATE_OUT_OF_RANGE`
- WHEN the error is handled
- THEN `toastStore.push({ type: 'error', title: t('budgetExecution.form.errors.operationDateOutOfRange') })` is called

---

### REQ-EXEC-TOAST-MIGRATE-1 — ExecutionRecordForm Error Surfacing via Toast

#### Scenario: API error shows toast, not inline banner

- GIVEN the ExecutionRecordForm submission returns an API error
- WHEN the error is handled
- THEN an error toast is pushed and no inline `submitError` div is rendered

#### Scenario: i18n keys for execution errors present in both locales

- GIVEN `en.json` and `es.json` are loaded
- WHEN `budgetExecution.form.errors.operationDateOutOfRange` is looked up
- THEN a non-empty translated string is returned in each locale

---

### REQ-EXEC-FORM-1 — ExecutionRecord Form — OperationDate Field

#### Scenario: Form defaults OperationDate to today on create

- GIVEN the user opens ExecutionRecordForm for a new record
- WHEN the form renders
- THEN the OperationDate input is pre-filled with today's date

#### Scenario: User clears OperationDate

- GIVEN the OperationDate input is populated
- WHEN the user clears the field and submits
- THEN the request payload includes operationDate = null

#### Scenario: Form pre-populates OperationDate on edit

- GIVEN an existing ExecutionRecord with OperationDate = 2026-05-15
- WHEN the edit form opens
- THEN the OperationDate input shows 2026-05-15

---

### REQ-EXEC-FORM-2 — ExecutionRecord Form — Currency and Exchange Rate Fields

#### Scenario: Operator selects a non-default currency

- GIVEN the user opens ExecutionRecordForm and selects a currency different from the cycle default
- WHEN they submit with a valid ExchangeRate
- THEN the record is created with the selected CurrencyId and ExchangeRate

#### Scenario: Currency pre-populates on edit

- GIVEN an existing ExecutionRecord with a specific CurrencyId
- WHEN the edit form opens
- THEN the currency dropdown shows the record's currency

#### Scenario: Form respects existing exchange rate pair validation

- GIVEN the user selects a non-default currency but leaves ExchangeRate empty
- WHEN they submit
- THEN submission is blocked (existing REQ-EXEC-6 validation applies)

---

### REQ-EXEC-CURRENCY-READ-1 — BudgetLine Read Model Includes CurrencyId

#### Scenario: BudgetLine list response includes currencyId

- GIVEN a BudgetLine with a specific CurrencyId
- WHEN GET /budgets/{budgetId}/periods/{periodId}/lines
- THEN each line item in the response includes a `currencyId` field containing the line's currency Guid

#### Scenario: currencyId available for pre-population

- GIVEN the inline edit form reads currencyId from the ListBudgetLines response
- WHEN the user opens the edit form for an existing line
- THEN the currency dropdown is pre-selected with the line's currency

---

### REQ-MC-1 — Currency Symbol in Matrix Cells

#### Scenario: Default currency selected — GTQ symbol shown

- GIVEN a cycle with DefaultCurrency.Symbol = "Q" and displayCurrency = "default"
- WHEN a matrix cell renders a monetary amount
- THEN the cell displays "Q" as the currency symbol next to the amount

#### Scenario: Alternate currency selected — USD symbol shown

- GIVEN a cycle with AlternateCurrency.Symbol = "$" and displayCurrency = "alternate"
- WHEN a matrix cell renders a monetary amount
- THEN the cell displays "$" as the currency symbol next to the amount

---

### REQ-MC-2 — Editable Exchange Rate Input in MatrixControls

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

### REQ-MC-3 — Display-Only Currency Conversion for All Matrix Values

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

### REQ-MC-4 — MatrixTotalRow Derives Values from LineType Subtotals

#### Scenario: Total row equals sum of three lineType subtotals

- GIVEN Expense subtotal = { budgeted: 1000, executed: 800 }, PreventiveSavings subtotal = { budgeted: 200, executed: 150 }, LongTermSavings subtotal = { budgeted: 300, executed: 250 }
- WHEN MatrixTotalRow renders
- THEN budgeted total = 1500 AND executed total = 1200

#### Scenario: Changing exchange rate updates total via subtotal chain

- GIVEN displayCurrency = "alternate", ExchangeRate = 7.5, and total budgeted in default currency = 1500 GTQ
- WHEN the exchange rate is changed to 8.0
- THEN the total budgeted is recalculated as 1500 / 8.0 = 187.50

---

### REQ-EXEC-CONFIRM-1 — ExecutionRecord Two-Step Delete Confirmation

The UI MUST require a two-step confirmation before soft-deleting an ExecutionRecord. On the first
click of the Delete button, the button MUST change to a confirmation state (e.g., "Confirm?" with
a cancel option). On the second click, the delete call MUST proceed. This MUST follow the
MatrixLineRow two-step pattern. A cancel action (clicking away or pressing Escape) MUST reset the
button to its initial state without making any API call.

#### Scenario: First click — enters confirmation state

- GIVEN an active ExecutionRecord row and an open Period
- WHEN the user clicks the Delete button once
- THEN the button renders in its "confirm" state (changed label or highlight)
- AND no API call is made

#### Scenario: Second click — delete proceeds

- GIVEN the Delete button is in confirmation state
- WHEN the user clicks it again
- THEN `DELETE .../executions/{id}` is called
- AND a success toast is shown with the `budgetExecution.record.deleteSuccess` message
- AND the row is removed or marked as deleted

#### Scenario: Cancel resets confirmation state

- GIVEN the Delete button is in confirmation state
- WHEN the user clicks outside the button or presses Escape
- THEN the button reverts to its initial Delete state
- AND no API call is made

#### Scenario: Confirmation state is row-local

- GIVEN two ExecutionRecord rows are visible
- WHEN the user enters the confirmation state on Row A
- THEN Row B's delete button remains in its normal initial state

---

### REQ-EXEC-TOAST-1 — Success Toasts on ExecutionRecord Delete and Restore

On successful delete or restore of an ExecutionRecord, the UI MUST push a success toast via
`useToastStore` using the appropriate i18n key. No toast MUST be shown on failed operations.

#### Scenario: Delete success toast

- GIVEN the two-step delete confirmation is confirmed
- WHEN `DELETE .../executions/{id}` returns 204
- THEN a success toast is shown with the `budgetExecution.record.deleteSuccess` message

#### Scenario: Restore success toast

- GIVEN a soft-deleted ExecutionRecord in a view with includeDeleted=true
- WHEN `POST .../executions/{id}/restore` returns 200
- THEN a success toast is shown with the `budgetExecution.record.restoreSuccess` message
- WHEN the user saves a new ExchangeRate = 10.0
- THEN subtotalByLineType values recalculate, and the Total row displays 150.00 (USD) instead of 200.00

---

### REQ-S001 — SQLitePCLRaw Vulnerability Verification

#### Scenario: Transitive version is non-vulnerable — no pin added

- GIVEN `dotnet list package --vulnerable` returns no findings for SQLitePCLRaw
- WHEN the verification step completes
- THEN no explicit SQLitePCLRaw pin is added to any csproj

#### Scenario: Transitive version is vulnerable — pin added to affected csproj files

- GIVEN `dotnet list package --vulnerable` reports SQLitePCLRaw as vulnerable
- WHEN the fix is applied
- THEN an explicit `PackageReference` with the latest non-vulnerable version is added to all affected `.csproj` files

---

### REQ-S002 — Stale i18n Key Removal

#### Scenario: Stale key removed — no production reference broken

- GIVEN `budgetExecution.form.noteRequired` is deleted from en.json and es.json
- WHEN the application is built
- THEN no production Vue component references a missing i18n key

#### Scenario: Test stubs updated after key removal

- GIVEN `budgetExecution.form.validation.noteRequired` is removed from both locale files
- WHEN `ExecutionRecordForm.spec.ts` and `ExecutionListModal.spec.ts` run
- THEN all tests pass using the updated key or removed assertion

---

### REQ-MATRIX-FOOTER-1 — Matrix Summary Footer Order, Labels, and Total Source

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

### REQ-EXEC-7 — BudgetLine Period Validation (Updated)

#### Scenario: BudgetLine covers the period — accepted `@integration`
- GIVEN BudgetLine.StartDate=2025-01-01, EndDate=null; Period.StartDate=2025-03-01
- WHEN POST `.../executions`
- THEN HTTP 201

#### Scenario: BudgetLine does not cover the period — rejected `@integration`
- GIVEN BudgetLine.StartDate=2025-06-01; Period.StartDate=2025-03-01
- WHEN POST `.../executions`
- THEN HTTP 422, code `BUDGET_LINE_NOT_IN_PERIOD`

#### Scenario: Perpetual BudgetLine covers any period `@integration`
- GIVEN BudgetLine.StartDate=2020-01-01, EndDate=null; Period.StartDate=2030-01-01
- WHEN POST `.../executions`
- THEN HTTP 201

---

### REQ-EXEC-DATE-RANGE-1 — OperationDate Combined Range Check (Updated)

#### Scenario: OperationDate within intersection accepted `@integration`
- GIVEN Period=Jan 2025, BudgetLine.StartDate=2025-01-15, OperationDate=2025-01-20
- WHEN CreateExecution
- THEN HTTP 201

#### Scenario: OperationDate before BudgetLine StartDate rejected `@integration`
- GIVEN Period.StartDate=2025-01-01, BudgetLine.StartDate=2025-01-15, OperationDate=2025-01-10
- WHEN CreateExecution
- THEN HTTP 422, code `OPERATION_DATE_OUT_OF_RANGE`

#### Scenario: OperationDate after BudgetLine EndDate rejected `@integration`
- GIVEN Period.EndDate=2025-01-31, BudgetLine.EndDate=2025-01-20, OperationDate=2025-01-25
- WHEN CreateExecution
- THEN HTTP 422, code `OPERATION_DATE_OUT_OF_RANGE`

#### Scenario: OperationDate null — no range check `@unit`
- GIVEN OperationDate = null
- WHEN validator runs
- THEN no date-range error

---

## Modified Capability: budget-structure

### RST-6 — IncludeExecutionRecords Activation

All four restore endpoints MUST accept `includeExecutionRecords` (bool, default false) as a query parameter. When `includeExecutionRecords=true`, soft-deleted ExecutionRecords MUST be restored along with their parent entities.

#### Scenario: Parameter accepted and children restored

- GIVEN any restore endpoint with a soft-deleted child ExecutionRecord
- WHEN called with ?includeExecutionRecords=true
- THEN soft-deleted child ExecutionRecords are restored (no longer a no-op)

#### Scenario: Parameter false or omitted preserves previous behavior

- GIVEN any restore endpoint
- WHEN called with ?includeExecutionRecords=false or parameter omitted
- THEN ExecutionRecords remain soft-deleted (default behavior)

---

## Assumptions Made

1. `ExchangeRateTo` is the inverse rate (1 DefaultCurrency = ExchangeRateTo AlternateCurrency). The conversion formula for totals is `Amount / ExchangeRate` to obtain the DefaultCurrency equivalent.
2. Period's `IsClosed` field is checked via the BudgetLine→Period navigation, resolved from the route `periodId`.
3. `ListExecutions` has no pagination — it returns a flat list. This is consistent with the proposal scope exclusion.
4. RBAC roles (`budget:operator`, `budget:read`) follow the same enforcement mechanism as existing BudgetStructure slices.
5. `includeExecutionRecords` flag propagates through all cascade levels (Cycle→Period→BudgetLine→ExecutionRecords).
