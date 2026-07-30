# Spec: Bank Account Restore (delta)

## Purpose

Delta spec for the `bank-account-restore` change. Extends the `bank-accounts` capability (see `openspec/specs/bank-accounts/spec.md`) with restore, inclusive listing, alias uniqueness enforcement, and toast feedback.

---

## Capability: bank-accounts (delta additions)

### BA-5: Restore Bank Account

A soft-deleted bank account MUST be restorable by a `budget:admin` user via POST `.../restore`. Restore sets `DeletedAt = null` and `UpdatedAt = UtcNow`. Attempting to restore a non-existent or already-active account MUST return 404. The route is unauthenticated only at the wrong role; `budget:operator` and `budget:read` MUST be rejected with 403.

#### Scenario: Successful restore

- GIVEN a soft-deleted bank account (DeletedAt IS NOT NULL)
- WHEN POST `/api/budgets/{budgetId}/bank-accounts/{accountId}/restore` as `budget:admin`
- THEN 204 No Content is returned, DeletedAt is null, UpdatedAt is refreshed to UtcNow

#### Scenario: Restore non-existent account

- GIVEN an accountId that does not exist in the budget
- WHEN POST `.../restore` as `budget:admin`
- THEN 404 Not Found is returned

#### Scenario: Restore already-active account

- GIVEN a bank account with DeletedAt IS NULL (active)
- WHEN POST `.../restore` as `budget:admin`
- THEN 404 Not Found is returned

#### Scenario: Non-admin role rejected

- GIVEN an authenticated `budget:operator` or `budget:read` user
- WHEN POST `.../restore`
- THEN 403 Forbidden is returned

---

### BA-2 (amended): List Bank Accounts — includeDeleted support

The existing BA-2 behavior (active accounts only, ordered by DisplayOrder) MUST remain unchanged when `includeDeleted` is absent or false. When `includeDeleted=true` is supplied, soft-deleted accounts MUST be included. Every account in the response MUST include a `deletedAt` field (null for active accounts).

#### Scenario: Default listing excludes deleted accounts

- GIVEN a budget with 1 active and 1 soft-deleted account
- WHEN GET `/api/budgets/{budgetId}/bank-accounts` (no query param)
- THEN only the active account is returned; `deletedAt` is null on all returned items

#### Scenario: includeDeleted=true returns all accounts

- GIVEN a budget with 1 active and 1 soft-deleted account
- WHEN GET `/api/budgets/{budgetId}/bank-accounts?includeDeleted=true`
- THEN both accounts are returned; the soft-deleted account has `deletedAt` populated

#### Scenario: deletedAt field always present

- GIVEN any GET listing request
- WHEN response is received
- THEN every account object contains a `deletedAt` field (string ISO-8601 or null)

---

### BA-1 (amended): Create Bank Account — alias uniqueness including soft-deleted

The alias MUST be unique within the budget among ALL accounts, including soft-deleted ones. A request that would duplicate an alias belonging to an active or soft-deleted account MUST be rejected with 422. This prevents a restore from ever colliding on alias.

#### Scenario: Duplicate alias of active account blocked

- GIVEN a budget with an active account aliased "Savings"
- WHEN POST `.../bank-accounts` with alias "Savings"
- THEN 422 Unprocessable Entity is returned

#### Scenario: Duplicate alias of soft-deleted account blocked

- GIVEN a budget with a soft-deleted account aliased "OldChecking"
- WHEN POST `.../bank-accounts` with alias "OldChecking"
- THEN 422 Unprocessable Entity is returned (alias is still reserved)

#### Scenario: Unique alias accepted

- GIVEN no account in the budget (active or deleted) has alias "NewAccount"
- WHEN POST `.../bank-accounts` with alias "NewAccount"
- THEN 201 Created is returned

---

### BA-3 (amended): Update Bank Account — alias uniqueness including soft-deleted, excluding self

On update, the alias MUST be unique within the budget among ALL accounts (including soft-deleted), excluding the account being updated. A request that would create an alias collision MUST be rejected with 422.

#### Scenario: Update alias to same value (no-op uniqueness) accepted

- GIVEN an active account "Checking" being updated
- WHEN PUT `.../bank-accounts/{accountId}` with alias "Checking" (unchanged)
- THEN 200 OK is returned

#### Scenario: Update alias to alias of another active account blocked

- GIVEN account A is "Checking" and account B is "Savings"
- WHEN PUT `.../bank-accounts/{accountIdA}` with alias "Savings"
- THEN 422 Unprocessable Entity is returned

#### Scenario: Update alias to alias of a soft-deleted account blocked

- GIVEN account A is "Checking" and a soft-deleted account has alias "Archived"
- WHEN PUT `.../bank-accounts/{accountIdA}` with alias "Archived"
- THEN 422 Unprocessable Entity is returned

---

### BA-6: BankAccount Domain Method — Restore()

The `BankAccount` entity MUST expose a `Restore()` method. Calling it MUST set `DeletedAt = null` and `UpdatedAt = DateTime.UtcNow`. It MUST NOT modify any other field (alias, currencyId, isPositive, displayOrder).

#### Scenario: Restore method clears deletion marker

- GIVEN a BankAccount instance with DeletedAt set to a past timestamp
- WHEN `Restore()` is called
- THEN DeletedAt is null and UpdatedAt equals the current UTC time

#### Scenario: Restore method is idempotent on active account

- GIVEN a BankAccount instance with DeletedAt = null
- WHEN `Restore()` is called
- THEN DeletedAt remains null and UpdatedAt is updated (method does not throw)

---

## Frontend Behavior

### FE-BA-1: Show Deleted Toggle

A toggle labeled (or aria-labeled) "Show deleted" MUST appear in BankAccountListView. When off (default), deleted accounts are hidden. When on, deleted accounts are fetched and displayed.

#### Scenario: Toggle off — deleted rows hidden

- GIVEN BankAccountListView is rendered with the toggle off
- WHEN the view loads
- THEN no deleted accounts appear; only active accounts are visible

#### Scenario: Toggle on — deleted rows visible with styling

- GIVEN the toggle is switched on
- WHEN the account list re-renders
- THEN soft-deleted accounts appear with reduced opacity AND a visible "deleted" badge
- AND the RotateCcw (restore) button is visible on each deleted row

#### Scenario: Toggle off again — deleted rows hidden

- GIVEN the toggle was on and deleted rows were visible
- WHEN the toggle is switched off
- THEN deleted accounts disappear from the list without page reload

---

### FE-BA-2: Restore Button

The RotateCcw restore button MUST appear only on deleted rows. Clicking it MUST call POST `.../restore`, then refresh the list, and display a success toast.

#### Scenario: Restore button absent on active rows

- GIVEN an active account row
- WHEN the row is rendered
- THEN no restore button is visible

#### Scenario: Restore button present on deleted rows

- GIVEN a deleted account row (toggle on)
- WHEN the row is rendered
- THEN the RotateCcw restore button is visible

#### Scenario: Restore success

- GIVEN the user clicks RotateCcw on a deleted row
- WHEN POST `.../restore` responds with 204
- THEN the account disappears from the deleted-rows view (now active)
- AND a success toast is shown

---

### FE-BA-3: Icon Buttons

Edit and delete action buttons on each active account row MUST use icon components (Pencil for edit, Trash2 for delete) instead of text buttons.

#### Scenario: Icon buttons render on active rows

- GIVEN an active account row
- WHEN the row is rendered
- THEN a Pencil icon button and a Trash2 icon button are present (no plain text buttons)

---

### FE-BA-4: Toast Notifications

BankAccountListView MUST display success toasts after each mutating operation. Toast keys and behavior follow the existing `useToastStore` pattern.

#### Scenario: Create success toast

- GIVEN the user submits the create form with valid data
- WHEN the API responds with 201
- THEN a success toast fires

#### Scenario: Edit success toast

- GIVEN the user submits the edit form with valid data
- WHEN the API responds with 200
- THEN a success toast fires

#### Scenario: Delete success toast

- GIVEN the user confirms deletion
- WHEN the API responds with 204
- THEN a success toast fires

#### Scenario: Restore success toast

- GIVEN the user clicks the restore button on a deleted row
- WHEN the API responds with 204
- THEN a success toast fires

#### Scenario: Error path — no success toast on failure

- GIVEN any mutating operation
- WHEN the API responds with 4xx or 5xx
- THEN no success toast fires (error handling is out of scope for this spec but must not emit success)

---

## Test Coverage Requirements

All four test layers are required.

| Layer | Scope |
|-------|-------|
| Unit | `BankAccount.Restore()` — field mutations and idempotency |
| Unit | `RestoreBankAccountValidator` — not-found and already-active guard |
| Unit | `CreateBankAccountValidator` — alias uniqueness including soft-deleted |
| Unit | `UpdateBankAccountValidator` — alias uniqueness including soft-deleted, excluding self |
| Integration | POST `.../restore` happy path (204) |
| Integration | POST `.../restore` not found (404) |
| Integration | POST `.../restore` already active (404) |
| Integration | GET `.../bank-accounts` default excludes deleted |
| Integration | GET `.../bank-accounts?includeDeleted=true` includes deleted with `deletedAt` populated |
| Integration | POST `.../bank-accounts` rejects alias duplicate of soft-deleted account (422) |
| Integration | PUT `.../bank-accounts/{id}` rejects alias duplicate of soft-deleted account (422) |
| Frontend | Toggle on/off visibility behavior (Vitest/Vue Test Utils) |
| Frontend | Restore button absent on active rows, present on deleted rows |
| Frontend | Toast fires after create, edit, delete, and restore |
| E2E | Full restore flow: delete → toggle on → restore → toggle off → account active again |
| E2E | Create rejected when alias matches soft-deleted account |

---

## Non-Goals (reaffirmed from proposal)

- DisplayOrder reordering on restore
- Cut record interaction with restored accounts
- Batch restore or undo-delete confirmation modal
- Account balance reconciliation
