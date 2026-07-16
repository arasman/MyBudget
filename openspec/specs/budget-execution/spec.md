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
| REQ-EXEC-4 | `budget-execution` | `Note` MUST be provided (non-null, non-empty) when `EntryType` is `CreditNote` or `DebitNote`. It MUST be rejected with error code `NOTE_REQUIRED_FOR_ENTRY_TYPE` (400) when absent. For `EntryType = Expense`, `Note` is optional. |
| REQ-EXEC-5 | `budget-execution` | When `CurrencyId` equals the Cycle's `DefaultCurrencyId`, `ExchangeRate` and `ExchangeRateTo` MUST both be null. Providing either when currencies match MUST be rejected with error code `EXCHANGE_RATE_NOT_ALLOWED` (400). |
| REQ-EXEC-6 | `budget-execution` | When `CurrencyId` differs from the Cycle's `DefaultCurrencyId`, `ExchangeRate` and `ExchangeRateTo` MUST both be provided. Providing one without the other MUST be rejected with error code `EXCHANGE_RATE_PAIR_INCOMPLETE` (400). |
| REQ-EXEC-7 | `budget-execution` | `PeriodId` on the ExecutionRecord MUST equal the `BudgetLine.PeriodId`. A mismatch between the route `periodId` and `BudgetLine.PeriodId` MUST be rejected with error code `PERIOD_MISMATCH` (400). |
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
| REQ-EXEC-FORM-1 | `budget-execution` | `ExecutionRecordForm.vue` MUST expose an `OperationDate` date picker field. The field MUST default to today's date when the form is opened for creation. The field MUST be editable. The field MUST be nullable (clearing it sends null to the backend). |
| REQ-EXEC-FORM-2 | `budget-execution` | `ExecutionRecordForm.vue` MUST expose `CurrencyId` (currency dropdown) and `ExchangeRate` (numeric input) fields. These fields MUST map to the existing entity properties. Both fields MUST save and reload correctly via the create/update commands and list query. |
| REQ-EXEC-CURRENCY-READ-1 | `budget-execution` | The `ListBudgetLines` query response MUST include `currencyId` (Guid) per line so the frontend can pre-populate the currency field in the edit form without a separate lookup. |

---

## Validation Error Codes

| Code | Trigger | HTTP |
|---|---|---|
| `AMOUNT_MUST_BE_POSITIVE` | Amount ≤ 0 on Create or Update | 400 |
| `NOTE_REQUIRED_FOR_ENTRY_TYPE` | Note absent for CreditNote or DebitNote | 400 |
| `EXCHANGE_RATE_NOT_ALLOWED` | ExchangeRate or ExchangeRateTo provided when CurrencyId = DefaultCurrencyId | 400 |
| `EXCHANGE_RATE_PAIR_INCOMPLETE` | Exactly one of ExchangeRate / ExchangeRateTo provided when CurrencyId ≠ DefaultCurrencyId | 400 |
| `PERIOD_MISMATCH` | Route periodId ≠ BudgetLine.PeriodId | 400 |
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

#### Scenario: Note absent for CreditNote rejected

- GIVEN a CreateExecution request with EntryType = CreditNote and Note = null
- WHEN the validator runs
- THEN 400 Bad Request, error code NOTE_REQUIRED_FOR_ENTRY_TYPE

#### Scenario: Note absent for DebitNote rejected

- GIVEN a CreateExecution request with EntryType = DebitNote and Note = ""
- WHEN the validator runs
- THEN 400 Bad Request, error code NOTE_REQUIRED_FOR_ENTRY_TYPE

#### Scenario: Note absent for Expense accepted

- GIVEN a CreateExecution request with EntryType = Expense and Note = null
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
