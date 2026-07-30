# Spec: Current Situation (Periodic Financial Snapshot)

## Purpose

Defines the behavioral requirements for two new capabilities:
- **bank-accounts** (BA-*): budget-scoped bank account catalog with soft-delete
- **current-situation** (CS-*): periodic cut record lifecycle, balance snapshots, and budget execution summary at cut date

---

## Capability: bank-accounts

### BA-1: Create Bank Account

The system MUST allow a `budget:admin` user to create a bank account for a budget. Alias MUST be non-empty and at most 100 characters. CurrencyId MUST reference an existing currency. DisplayOrder MUST be a non-negative integer. A budget MAY have multiple accounts with the same currency.

#### Scenario: Successful creation

- GIVEN an authenticated `budget:admin` user
- WHEN POST `/api/budgets/{id}/bank-accounts` with valid alias, currencyId, isPositive, displayOrder
- THEN 201 Created is returned with the new account id

#### Scenario: Alias exceeds length

- GIVEN a `budget:admin` user
- WHEN POST with alias longer than 100 characters
- THEN 422 Unprocessable Entity is returned

#### Scenario: Non-admin role rejected

- GIVEN an authenticated `budget:operator` or `budget:read` user
- WHEN POST `/api/budgets/{id}/bank-accounts`
- THEN 403 Forbidden is returned

---

### BA-2: List Bank Accounts

The system MUST return all bank accounts for a budget ordered by DisplayOrder. Soft-deleted accounts (DeletedAt IS NOT NULL) MUST be excluded from the response.

#### Scenario: Active accounts returned in order

- GIVEN a budget with 3 active accounts at DisplayOrder 1, 2, 3
- WHEN GET `/api/budgets/{id}/bank-accounts`
- THEN 200 OK is returned with accounts sorted by DisplayOrder ascending

#### Scenario: Soft-deleted accounts excluded

- GIVEN a budget with 1 active and 1 soft-deleted account
- WHEN GET `/api/budgets/{id}/bank-accounts`
- THEN only the active account is returned

#### Scenario: Read access sufficient

- GIVEN a `budget:read` user
- WHEN GET `/api/budgets/{id}/bank-accounts`
- THEN 200 OK is returned

---

### BA-3: Update Bank Account

The system MUST allow a `budget:admin` user to update alias, isPositive, and displayOrder of an existing non-deleted bank account. CurrencyId MUST NOT be changed after creation.

#### Scenario: Successful update

- GIVEN an existing active bank account
- WHEN PUT `/api/budgets/{id}/bank-accounts/{accountId}` with new alias
- THEN 200 OK is returned and alias is persisted

#### Scenario: Account not found

- GIVEN a non-existent or soft-deleted accountId
- WHEN PUT `/api/budgets/{id}/bank-accounts/{accountId}`
- THEN 404 Not Found is returned

---

### BA-4: Soft-Delete Bank Account

The system MUST allow a `budget:admin` user to soft-delete a bank account at any time, even if it is referenced in existing cut records. Soft-deletion MUST set DeletedAt to the current timestamp. The alias in existing CutBankAccount snapshot rows MUST be preserved as-is (no cascade update).

#### Scenario: Successful soft-delete

- GIVEN an active bank account referenced in a past cut
- WHEN DELETE `/api/budgets/{id}/bank-accounts/{accountId}`
- THEN 204 No Content is returned and DeletedAt is set

#### Scenario: Historical snapshots unaffected

- GIVEN a soft-deleted account with CutBankAccount rows
- WHEN GET on an existing cut record
- THEN historical balance rows still appear with the original alias

---

## Capability: current-situation

### CS-1: Upsert Cut Record

The system MUST allow a `budget:operator` user to create or replace a cut record for a given date. Date format in the URL path MUST be `YYYY-MM-DD`. The upsert MUST fully replace all CutBankAccount rows for that date (delete then re-insert). An active period (StartDate ≤ CutDate ≤ EndDate, cycle is active, period is not closed) MUST exist; otherwise the request MUST be rejected with 422. ExchangeRate MUST be a positive decimal.

#### Scenario: Successful upsert (create)

- GIVEN no cut record exists for the date, an active period covers the date
- WHEN PUT `/api/budgets/{id}/cut-records/2026-07-28` with exchangeRate and balances
- THEN 200 OK is returned and the record is persisted

#### Scenario: Successful upsert (replace)

- GIVEN an existing cut record for the date
- WHEN PUT with new balances
- THEN all previous CutBankAccount rows are replaced with the new values

#### Scenario: No active period for cut date

- GIVEN no active period covers the requested date
- WHEN PUT `/api/budgets/{id}/cut-records/{date}`
- THEN 422 Unprocessable Entity is returned

#### Scenario: Non-operator role rejected

- GIVEN a `budget:read` user
- WHEN PUT `/api/budgets/{id}/cut-records/{date}`
- THEN 403 Forbidden is returned

#### Scenario: Duplicate date enforced at DB level

- GIVEN two concurrent upserts for the same BudgetId and CutDate
- WHEN both reach the database
- THEN the UNIQUE INDEX on (BudgetId, CutDate) ensures only one record exists

---

### CS-2: Get Cut Record

The system MUST return the cut record for a given date. If the record does not exist, the system MUST return a draft response pre-populated with currently-active bank accounts (DeletedAt IS NULL) and balance 0. If a previous cut exists, the draft MUST copy balances for accounts present in both; newly-added accounts (not in previous cut) MUST use balance 0; accounts soft-deleted since the last cut MUST be excluded. If no cut has ever existed for the budget, the draft MUST include all active accounts with balance 0.

The response MUST include the budget execution summary for the active period at cut date: TotalBudgeted, TotalRegistered, Remaining. If no active period covers the cut date, these fields MUST be returned as zero (no error).

#### Scenario: Existing cut returned

- GIVEN a cut record exists for the date
- WHEN GET `/api/budgets/{id}/cut-records/2026-07-28`
- THEN 200 OK is returned with persisted balances and execution summary

#### Scenario: Draft from previous cut

- GIVEN no cut exists for 2026-07-28, a cut exists for 2026-07-25
- WHEN GET `/api/budgets/{id}/cut-records/2026-07-28`
- THEN a draft is returned with balances cloned from 2026-07-25 for matching accounts

#### Scenario: Newly-added account gets zero balance in draft

- GIVEN account A existed in last cut, account B was created after last cut
- WHEN GET returns a draft
- THEN account A has its previous balance, account B has balance 0

#### Scenario: Soft-deleted account excluded from draft

- GIVEN account C existed in last cut but was soft-deleted before today
- WHEN GET returns a draft
- THEN account C does not appear in the draft

#### Scenario: First cut ever — empty draft

- GIVEN no prior cut exists for the budget
- WHEN GET `/api/budgets/{id}/cut-records/{date}`
- THEN a draft is returned with all active accounts and balance 0

#### Scenario: No active period — execution summary zeroed

- GIVEN the cut date falls outside all active periods
- WHEN GET `/api/budgets/{id}/cut-records/{date}`
- THEN TotalBudgeted, TotalRegistered, Remaining are all 0

---

### CS-3: List Cut Dates

The system MUST return the list of calendar dates for which a cut record exists for a given budget, ordered ascending. Only `budget:read` or higher access is required.

#### Scenario: Dates returned ascending

- GIVEN 3 cut records on different dates
- WHEN GET `/api/budgets/{id}/cut-records/dates`
- THEN a list of 3 dates in YYYY-MM-DD format is returned in ascending order

#### Scenario: No cuts exist

- GIVEN no cut records for the budget
- WHEN GET `/api/budgets/{id}/cut-records/dates`
- THEN 200 OK with an empty list

---

### CS-4: Delete Cut Record

The system MUST allow a `budget:operator` user to hard-delete a cut record and all associated CutBankAccount rows. The delete MUST be physical (no soft-delete). The frontend MUST present a confirmation modal requiring the user to type the cut date before the API call is made (UI constraint only — the API itself has no typed-confirmation requirement).

#### Scenario: Successful delete

- GIVEN an existing cut record for 2026-07-28
- WHEN DELETE `/api/budgets/{id}/cut-records/2026-07-28`
- THEN 204 No Content is returned and the record plus all CutBankAccount rows are removed

#### Scenario: Delete non-existent cut

- GIVEN no cut record for the date
- WHEN DELETE `/api/budgets/{id}/cut-records/{date}`
- THEN 404 Not Found is returned

#### Scenario: Non-operator role rejected

- GIVEN a `budget:read` user
- WHEN DELETE `/api/budgets/{id}/cut-records/{date}`
- THEN 403 Forbidden is returned

---

### CS-5: Balance and Exchange Rate Computation

The system MUST compute BalanceInPrimary at write time (upsert) using the cut's ExchangeRate. When a bank account's currency matches the budget's primary currency, BalanceInPrimary MUST equal Balance. When a bank account's currency matches the budget's alternate currency, BalanceInPrimary MUST equal Balance × CutRecord.ExchangeRate. The ExchangeRate MUST NOT be applied to ExecutionRecord data.

#### Scenario: Primary currency account

- GIVEN an account with CurrencyId = budget primary currency
- WHEN a balance of 1000 is saved for any ExchangeRate
- THEN BalanceInPrimary = 1000

#### Scenario: Alternate currency account

- GIVEN an account with CurrencyId = budget alternate currency
- WHEN a balance of 100 is saved with ExchangeRate = 7.8
- THEN BalanceInPrimary = 780

---

### CS-6: Cut Totals

The system MUST compute the following totals at query time (GET CutRecord) from the persisted CutBankAccount rows:

| Field | Definition |
|---|---|
| TotalCuentasQueSuman | SUM(BalanceInPrimary) WHERE IsPositive = true |
| TotalCuentasQueRestan | SUM(BalanceInPrimary) WHERE IsPositive = false |
| TotalDeudaEnCurso | Remaining + TotalCuentasQueRestan |

#### Scenario: Totals computed correctly

- GIVEN accounts A (IsPositive=true, BalanceInPrimary=500) and B (IsPositive=false, BalanceInPrimary=200), Remaining=300
- WHEN GET cut record
- THEN TotalCuentasQueSuman=500, TotalCuentasQueRestan=200, TotalDeudaEnCurso=500

---

### CS-7: Frontend — Current Situation View

The system MUST expose a route `/budgets/:budgetId/current-situation` as a tab in BudgetTabs. The view MUST support navigation to the previous and next cut date using the dates from ListCutDates. The view MUST display the cut form (exchange rate, bank account balances), budget execution summary, and computed totals. All UI strings MUST be available in ES and EN via vue-i18n.

#### Scenario: Tab renders on navigation

- GIVEN a budget with at least one cut
- WHEN the user navigates to `/budgets/:budgetId/current-situation`
- THEN the most recent cut is displayed

#### Scenario: Previous/next navigation

- GIVEN cuts on 2026-07-20, 2026-07-25, 2026-07-28
- WHEN the user views 2026-07-25 and clicks "next"
- THEN 2026-07-28 is displayed

#### Scenario: Delete confirmation modal

- GIVEN the user wants to delete a cut
- WHEN the user opens the delete modal and types the correct cut date
- THEN the delete button is enabled and the API call proceeds on confirm

---

### CS-8: Bank Account Management (Frontend)

The system MUST provide a standalone bank account management section accessible from budget configuration (not exclusively from within the cut form). The section MUST support create, update, and soft-delete operations. All UI strings MUST be available in ES and EN.

#### Scenario: Account created from config section

- GIVEN the user is on the budget configuration page
- WHEN the user creates a bank account with alias "Caja GTQ"
- THEN the account appears in the list and is available for new cut records

#### Scenario: Soft-delete from config section

- GIVEN an existing account
- WHEN the user deletes it from the config section
- THEN the account no longer appears in the list or in new cut drafts

---

## Non-Goals (Explicit Exclusions)

- ProjectionsJson column: reserved as nullable placeholder. No schema defined. No read or write behavior specified.
- No accounts receivable or payable installment tracking (Layer 2).
- No TotalProyectadoCurso, TotalProyectado, TotalAdeudadoProyectado fields.
- ExecutionRecord.AccountId remains an opaque nullable Guid with no FK to BankAccounts.
- No historical trend charts or cross-cut comparisons.
