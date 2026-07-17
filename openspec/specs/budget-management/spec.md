# Budget Management Specification

## Purpose

Defines Create, Rename, Soft-Delete, and Restore operations on the Budget entity.
This is a new capability — no prior spec exists for budget-level CRUD.

---

## Shared Constraints

- SC-BM-01: All endpoints require a valid JWT (`RequireAuthorization()`).
- SC-BM-02: `DELETE` and `POST .../restore` are restricted to `BudgetRole.Owner` (value 40).
- SC-BM-03: `PUT /api/budgets/:id` requires `budget:admin` policy.
- SC-BM-04: `POST /api/budgets` MUST NOT use a `budget:*` policy (no budgetId in route).
- SC-BM-05: Budget `Name` MUST be 1–200 characters, trimmed, non-empty.
- SC-BM-06: `IsDeleted` and `DeletedAt` on `Budget` are set/cleared only by the soft-delete and restore operations. No other slice mutates these fields.
- SC-BM-07: Cache key `budget-membership:{userId}:{budgetId}` MUST be evicted for all current members on soft-delete and restore.

---

## Requirements

### Requirement: BM-01 — Create Budget

The system MUST allow an authenticated user to create a new Budget. The caller becomes the Owner and a `BudgetMembership` record with `BudgetRole.Owner` MUST be created atomically in the same transaction. The new Budget MUST have `IsDeleted = false`.

#### Scenario: Happy path

- GIVEN an authenticated user
- WHEN `POST /api/budgets` is called with `{ name: "Household" }`
- THEN HTTP 201 is returned with `{ id, name }`
- AND a `Budget` row exists with `Name = "Household"`, `OwnerId = callerId`, `IsDeleted = false`
- AND a `BudgetMembership` row exists with `Role = Owner` linking caller to the new budget

#### Scenario: Name too long

- GIVEN an authenticated user
- WHEN `POST /api/budgets` is called with `name` exceeding 200 characters
- THEN HTTP 422 is returned with validation error on `name`, code `FIELD_INVALID`

#### Scenario: Name empty or whitespace-only

- GIVEN an authenticated user
- WHEN `POST /api/budgets` is called with `name = "   "`
- THEN HTTP 422 is returned with validation error on `name`, code `FIELD_REQUIRED`

#### Scenario: Unauthenticated

- GIVEN no valid Bearer token
- WHEN `POST /api/budgets` is called
- THEN HTTP 401 is returned

---

### Requirement: BM-02 — Rename Budget

The system MUST allow a member with `budget:admin` role to change the Budget's `Name`. The rename MUST evict `budget-membership:{userId}:{budgetId}` cache entries for all members so dropdowns reflect the new name promptly.

#### Scenario: Happy path

- GIVEN an authenticated user with `admin` role in budget `{id}`
- WHEN `PUT /api/budgets/{id}` is called with `{ name: "Personal" }`
- THEN HTTP 200 is returned with `{ id, name: "Personal" }`
- AND `Budget.Name` is updated in the database

#### Scenario: Insufficient role

- GIVEN an authenticated user with `operator` role in budget `{id}`
- WHEN `PUT /api/budgets/{id}` is called
- THEN HTTP 403 is returned with error code `AUTH_INSUFFICIENT_ROLE`

#### Scenario: Budget not found or caller has no membership

- GIVEN no budget with id `{id}` or caller has no membership
- WHEN `PUT /api/budgets/{id}` is called
- THEN HTTP 404 is returned

#### Scenario: Name validation

- GIVEN an authenticated admin
- WHEN `PUT /api/budgets/{id}` is called with an empty name
- THEN HTTP 422 is returned with validation error on `name`

---

### Requirement: BM-03 — Soft-Delete Budget

The system MUST allow the Budget Owner to soft-delete a Budget by setting `IsDeleted = true` and `DeletedAt = DateTimeOffset.UtcNow`. After soft-delete, all requests to budget-scoped endpoints MUST return 404. Cache entries for all members MUST be evicted.

No cascade soft-delete is applied to child entities (cycles, categories, etc.).

#### Scenario: Happy path

- GIVEN an authenticated user with `Owner` role in budget `{id}`
- WHEN `DELETE /api/budgets/{id}` is called
- THEN HTTP 204 is returned
- AND `Budget.IsDeleted = true`, `Budget.DeletedAt` is set to approximately now

#### Scenario: Insufficient role — non-owner

- GIVEN an authenticated user with `admin` role (not Owner) in budget `{id}`
- WHEN `DELETE /api/budgets/{id}` is called
- THEN HTTP 403 is returned with error code `AUTH_INSUFFICIENT_ROLE`

#### Scenario: Already deleted

- GIVEN a Budget already has `IsDeleted = true`
- WHEN `DELETE /api/budgets/{id}` is called by the Owner
- THEN HTTP 404 is returned (soft-deleted budget is invisible to the authz handler)

#### Scenario: Budget not found

- GIVEN no budget `{id}` exists
- WHEN `DELETE /api/budgets/{id}` is called
- THEN HTTP 404 is returned

---

### Requirement: BM-04 — Restore Budget

The system MUST allow the Budget Owner to restore a soft-deleted Budget by setting `IsDeleted = false` and `DeletedAt = null`. Cache entries for all members MUST be evicted.

#### Scenario: Happy path

- GIVEN an authenticated user with `Owner` role and budget `{id}` has `IsDeleted = true`
- WHEN `POST /api/budgets/{id}/restore` is called
- THEN HTTP 200 is returned
- AND `Budget.IsDeleted = false`, `Budget.DeletedAt = null`

#### Scenario: Budget is not deleted

- GIVEN budget `{id}` has `IsDeleted = false`
- WHEN `POST /api/budgets/{id}/restore` is called
- THEN HTTP 404 is returned (only soft-deleted budgets are visible to restore handler via IgnoreQueryFilters)

#### Scenario: Insufficient role

- GIVEN an authenticated user with `admin` role (not Owner)
- WHEN `POST /api/budgets/{id}/restore` is called
- THEN HTTP 403 is returned with error code `AUTH_INSUFFICIENT_ROLE`

---

### Requirement: BM-FE-01 — BudgetSelectionView Controls

`BudgetSelectionView` MUST render a "New Budget" button that opens `CreateBudgetModal`. After successful creation, `authStore.fetchMe()` MUST be called to refresh the membership list, and the UI MUST navigate to the new budget's default route (`/budgets/{newId}/cycles`).

The view MUST also render a "Show deleted" toggle. When enabled, soft-deleted memberships (where `isDeleted: true`) MUST be visible with a restore action button. When disabled, only active memberships are shown.

`BudgetSelectionView` MUST NOT auto-redirect when `memberships.length === 1` if that one membership has `isDeleted: true`.

#### Scenario: Create budget and navigate

- GIVEN user opens BudgetSelectionView
- WHEN they click "New Budget", enter a valid name, and submit
- THEN `POST /api/budgets` is called
- AND on success, memberships are refreshed and the app navigates to `/budgets/{newId}/cycles`

#### Scenario: Show deleted toggle reveals deleted budgets

- GIVEN user has one active and one deleted budget membership
- WHEN the "Show deleted" toggle is enabled
- THEN both memberships appear; the deleted one shows a "Restore" button
- AND clicking "Restore" calls `POST /api/budgets/{id}/restore`

#### Scenario: Auto-redirect skipped for sole deleted budget

- GIVEN user has only one membership and it has `isDeleted: true`
- WHEN BudgetSelectionView renders
- THEN no automatic redirect occurs; the view stays visible with the deleted budget shown

---

### Requirement: BM-FE-02 — CreateBudgetModal

A `CreateBudgetModal` component MUST exist with:
- A name text input (max 200 chars, required)
- A submit button (disabled while request is in-flight)
- Inline validation error when name is empty or exceeds 200 characters
- An inline error message when the API returns 4xx

#### Scenario: Submit with valid name

- GIVEN the modal is open with `name = "Travel Fund"`
- WHEN the user submits
- THEN the submit button is disabled during the request
- AND on success the modal closes and a success notification is shown

#### Scenario: Submit with empty name shows inline error

- GIVEN the modal is open
- WHEN the user submits with an empty name field
- THEN no API call is made
- AND an inline validation error is shown

---

### Requirement: BM-FE-03 — Navigation Guard for Deleted Budget

The Vue Router MUST add a navigation guard that resolves the budget membership for any route matching `/budgets/:budgetId/*`. If the matching membership has `isDeleted: true`, the guard MUST redirect to `/`.

#### Scenario: Redirect when budget is deleted

- GIVEN user navigates to `/budgets/{id}/cycles` and `{id}` has `isDeleted: true` in `authStore.user.memberships`
- WHEN the router guard runs
- THEN the user is redirected to `/`
- AND the cycles view is NOT rendered

#### Scenario: Guard passes for active budget

- GIVEN user navigates to `/budgets/{id}/cycles` and `{id}` has `isDeleted: false`
- WHEN the router guard runs
- THEN navigation proceeds normally

---

### Requirement: BM-FE-04 — AppLayout activeBudgetName Restore on Reload

When `AppLayout` mounts and `layoutStore.activeBudgetName` is `null` but the current route contains a `budgetId` param, the layout MUST look up the budget name from `authStore.user.memberships` and set it in `layoutStore`.

#### Scenario: Name restored after page reload

- GIVEN user reloads the page while on `/budgets/{id}/cycles`
- WHEN AppLayout mounts with `layoutStore.activeBudgetName = null`
- THEN the layout reads `budgetId` from `useRoute().params`
- AND finds the matching membership in `authStore.user.memberships`
- AND calls `layoutStore.setActiveBudget(id, name)` so the header shows the correct name

#### Scenario: No matching membership

- GIVEN user reloads on `/budgets/{id}/cycles` but has no membership for `{id}`
- WHEN AppLayout mounts
- THEN no name is set; the layout shows the default app name without crashing

---

### Requirement: BM-FE-05 — i18n Keys

The following i18n keys MUST exist in both `en.json` and `es.json`:

| Key | Purpose |
|---|---|
| `budgetStructure.selection.newBudget` | "New Budget" button label |
| `budgetStructure.selection.createBudgetTitle` | CreateBudgetModal heading |
| `budgetStructure.selection.budgetNameLabel` | Name input label |
| `budgetStructure.selection.budgetNamePlaceholder` | Name input placeholder |
| `budgetStructure.selection.createSuccess` | Success notification after creation |
| `budgetStructure.selection.showDeleted` | "Show deleted" toggle label |
| `budgetStructure.selection.restore` | Restore button label |
| `budgetStructure.selection.restoreSuccess` | Success notification after restore |
| `budgetStructure.selection.confirmDelete` | Confirmation prompt text for soft-delete |
| `budgetStructure.selection.deleteSuccess` | Success notification after soft-delete |

#### Scenario: All keys resolve in both locales

- GIVEN locale is set to "es"
- WHEN any BudgetSelectionView or CreateBudgetModal label renders
- THEN all labels display Spanish translations without missing-key warnings in the console

---

---

### Requirement: BM-MIGRATION-01 — Budget Soft-Delete Columns

The `Budget` table MUST gain two new columns via an EF Core migration (`AddBudgetSoftDelete`):

| Column | Type | Nullable | Default |
|---|---|---|---|
| `IsDeleted` | `bool` | NOT NULL | `false` |
| `DeletedAt` | `DateTimeOffset?` | NULL | — |

The migration MUST set `IsDeleted = false` for all existing rows. The `Budget` entity MUST expose
`SoftDelete()` and `Restore()` domain methods that set/clear these fields.

#### Scenario: Existing budgets unaffected after migration

- GIVEN existing `Budget` rows before migration
- WHEN the `AddBudgetSoftDelete` migration runs
- THEN all existing rows have `IsDeleted = false` and `DeletedAt = null`
- AND all budget-scoped endpoints continue to function without change

---

## Error Code Registry

| Code | HTTP | Meaning |
|---|---|---|
| `BUDGET_NAME_REQUIRED` | 422 | Budget name is empty or whitespace |
| `BUDGET_NAME_TOO_LONG` | 422 | Budget name exceeds 200 characters |
