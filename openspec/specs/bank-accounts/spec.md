# Spec: Bank Accounts

## Purpose

Defines the behavioral requirements for the bank-accounts capability: budget-scoped bank account catalog with soft-delete.

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

## Non-Goals

- No account balance reconciliation or audit trail beyond creation/update timestamps
- No linked accounts across budgets
- No account hierarchy or nesting
