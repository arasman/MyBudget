# Delta for budget-execution

## MODIFIED Requirements

### Requirement: REQ-EXEC-4 — Note Requirement

`Note` MUST be provided (non-null, non-empty) for ALL `EntryType` values: `Expense`, `CreditNote`,
and `DebitNote`. The backend validator MUST require `Note` unconditionally on both Create and Update.
Absence of `Note` MUST be rejected with error code `NOTE_REQUIRED` (400) regardless of entry type.

(Previously: Note required only for CreditNote and DebitNote; Expense allowed null Note — misaligned with frontend)

#### Scenario: Note absent for Expense rejected `@unit`
- GIVEN a CreateExecution or UpdateExecution request with EntryType = Expense and Note = null or ""
- WHEN the validator runs
- THEN HTTP 400 with error code `NOTE_REQUIRED`

#### Scenario: Note absent for CreditNote rejected `@unit`
- GIVEN a CreateExecution request with EntryType = CreditNote and Note = null
- WHEN the validator runs
- THEN HTTP 400 with error code `NOTE_REQUIRED`

#### Scenario: Note absent for DebitNote rejected `@unit`
- GIVEN a CreateExecution request with EntryType = DebitNote and Note = ""
- WHEN the validator runs
- THEN HTTP 400 with error code `NOTE_REQUIRED`

#### Scenario: Note present for any entry type accepted `@unit`
- GIVEN any EntryType and Note = "valid text"
- WHEN the validator runs
- THEN no validation error for Note

---

## ADDED Requirements

### Requirement: REQ-EXEC-DATE-RANGE-1 — OperationDate Within Period Range

When `OperationDate` is provided, the backend MUST validate that it falls within the parent
Period's `StartDate` and `EndDate` (inclusive). Dates outside the range MUST be rejected.

#### Scenario: OperationDate within period accepted `@integration`
- GIVEN Period StartDate=2025-01-01, EndDate=2025-01-31
- WHEN CreateExecution with OperationDate=2025-01-15
- THEN HTTP 201 Created

#### Scenario: OperationDate before period start rejected `@integration`
- GIVEN Period StartDate=2025-01-01
- WHEN CreateExecution with OperationDate=2024-12-31
- THEN HTTP 422 with error code `OPERATION_DATE_OUT_OF_RANGE`

#### Scenario: OperationDate after period end rejected `@integration`
- GIVEN Period EndDate=2025-01-31
- WHEN CreateExecution with OperationDate=2025-02-01
- THEN HTTP 422 with error code `OPERATION_DATE_OUT_OF_RANGE`

#### Scenario: OperationDate null — no range check `@unit`
- GIVEN OperationDate = null
- WHEN the validator runs
- THEN no date-range error (null is permitted)

---

### Requirement: REQ-EXEC-DECIMAL-VAL-1 — Decimal Precision Validation (Frontend)

`ExecutionRecordForm.vue` MUST enforce client-side validation on decimal precision:
- `amount` MUST have at most 2 decimal places.
- `exchangeRate` MUST have at most 6 decimal places.

Violations MUST block form submission and show inline messages using:
- `budgetExecution.form.validation.amountDecimals`
- `budgetExecution.form.validation.exchangeRateDecimals`

#### Scenario: Amount with 3 decimal places blocked `@unit`
- GIVEN amount = 10.123 in ExecutionRecordForm
- WHEN the user attempts to submit
- THEN submission is blocked and `budgetExecution.form.validation.amountDecimals` is shown

#### Scenario: ExchangeRate with 7 decimal places blocked `@unit`
- GIVEN exchangeRate = 7.1234567 in ExecutionRecordForm
- WHEN the user attempts to submit
- THEN submission is blocked and `budgetExecution.form.validation.exchangeRateDecimals` is shown

#### Scenario: Valid decimal precision accepted `@unit`
- GIVEN amount = 10.12 and exchangeRate = 7.123456
- WHEN the user submits
- THEN no decimal-precision error is raised

---

### Requirement: REQ-EXEC-DATE-VAL-1 — OperationDate Out-of-Range (Frontend)

`ExecutionRecordForm.vue` MUST validate that the selected `operationDate` falls within the
parent period's date range. The period's StartDate and EndDate MUST be passed to the form
as props or retrieved from context. Violations MUST show using key
`budgetExecution.form.validation.operationDateOutOfRange`.

API errors with code `OPERATION_DATE_OUT_OF_RANGE` MUST also produce an error toast using key
`budgetExecution.form.errors.operationDateOutOfRange`.

#### Scenario: OperationDate outside period range blocked client-side `@unit`
- GIVEN period bounds Jan 1–31 2025 and the user selects Feb 1 2025
- WHEN the user attempts to submit
- THEN submission is blocked and `budgetExecution.form.validation.operationDateOutOfRange` is shown

#### Scenario: OperationDate out-of-range API error produces toast `@unit`
- GIVEN operationDate passes client-side validation but the API returns `OPERATION_DATE_OUT_OF_RANGE`
- WHEN the error is handled
- THEN `toastStore.push({ type: 'error', title: t('budgetExecution.form.errors.operationDateOutOfRange') })` is called

---

### Requirement: REQ-EXEC-TOAST-MIGRATE-1 — ExecutionRecordForm Error Surfacing via Toast

`ExecutionRecordForm.vue` MUST remove its inline `submitError` alert banner. API errors MUST be
surfaced exclusively via `toastStore.push({ type: 'error', title: t(key) })`.

#### Scenario: API error shows toast, not inline banner `@unit`
- GIVEN the ExecutionRecordForm submission returns an API error
- WHEN the error is handled
- THEN an error toast is pushed and no inline `submitError` div is rendered

#### Scenario: i18n keys for execution errors present in both locales `@unit`
- GIVEN `en.json` and `es.json` are loaded
- WHEN `budgetExecution.form.errors.operationDateOutOfRange` is looked up
- THEN a non-empty translated string is returned in each locale
