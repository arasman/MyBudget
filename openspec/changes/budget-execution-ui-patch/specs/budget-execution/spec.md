# Delta for budget-execution

## MODIFIED Requirements

### Requirement: REQ-EXEC-1 — ExecutionRecord Entity

An `ExecutionRecord` entity MUST exist with: `Id` (Guid PK), `BudgetLineId` (Guid FK, NOT NULL), `PeriodId` (Guid, NOT NULL, denormalized), `EntryType` (int, NOT NULL), `Amount` (decimal(18,2), NOT NULL, positive), `CurrencyId` (Guid FK, NOT NULL), `ExchangeRate` (decimal(18,6), nullable), `ExchangeRateTo` (decimal(18,6), nullable), `AccountId` (Guid?, nullable, no FK), `PaymentMethodId` (Guid?, nullable, no FK), `Note` (varchar(500), nullable), `OperationDate` (DateOnly, nullable), `CreatedAt`, `UpdatedAt`, `DeletedAt` (soft-delete).
(Previously: entity did not include `OperationDate`)

#### Scenario: ExecutionRecord created with OperationDate provided

- GIVEN a CreateExecution request with OperationDate = a valid past date
- WHEN the handler processes the request
- THEN 201 Created and ExecutionRecord.OperationDate equals the provided date

#### Scenario: ExecutionRecord created without OperationDate

- GIVEN a CreateExecution request with OperationDate = null
- WHEN the handler processes the request
- THEN 201 Created and ExecutionRecord.OperationDate is null

#### Scenario: OperationDate updated independently

- GIVEN an existing ExecutionRecord in an open Period
- WHEN PUT .../executions/{id} with a new OperationDate value
- THEN 200 OK and ExecutionRecord.OperationDate reflects the new date

---

### Requirement: REQ-EXEC-LIST-2 — ListExecutions Response Fields

Each item in the list response MUST include: `id`, `entryType`, `amount`, `currencyId`, `exchangeRate`, `exchangeRateTo`, `accountId`, `paymentMethodId`, `note`, `operationDate`, `createdAt`, `updatedAt`.
(Previously: response did not include `operationDate`)

#### Scenario: List response includes operationDate

- GIVEN 2 active ExecutionRecords, one with OperationDate set and one null
- WHEN GET .../executions
- THEN each item includes an `operationDate` field (date string or null respectively)

---

## ADDED Requirements

### Requirement: REQ-EXEC-FORM-1 — ExecutionRecord Form — OperationDate Field

`ExecutionRecordForm.vue` MUST expose an `OperationDate` date picker field. The field MUST default to today's date when the form is opened for creation. The field MUST be editable. The field MUST be nullable (clearing it sends null to the backend).

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

### Requirement: REQ-EXEC-FORM-2 — ExecutionRecord Form — Currency and Exchange Rate Fields

`ExecutionRecordForm.vue` MUST expose `CurrencyId` (currency dropdown) and `ExchangeRate` (numeric input) fields. These fields MUST map to the existing entity properties. Both fields MUST save and reload correctly via the create/update commands and list query.

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

### Requirement: REQ-EXEC-CURRENCY-READ-1 — BudgetLine Read Model Includes CurrencyId

The `ListBudgetLines` query response MUST include `currencyId` (Guid) per line so the frontend can pre-populate the currency field in the edit form without a separate lookup.

#### Scenario: BudgetLine list response includes currencyId

- GIVEN a BudgetLine with a specific CurrencyId
- WHEN GET /budgets/{budgetId}/periods/{periodId}/lines
- THEN each line item in the response includes a `currencyId` field containing the line's currency Guid

#### Scenario: currencyId available for pre-population

- GIVEN the inline edit form reads currencyId from the ListBudgetLines response
- WHEN the user opens the edit form for an existing line
- THEN the currency dropdown is pre-selected with the line's currency
