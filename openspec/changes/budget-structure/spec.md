# Budget Structure Specification

## Purpose

Defines the structural backbone of a budget: Cycles (yearly planning periods), Periods (monthly or
arbitrary sub-divisions), CategoryGroups, Categories, BudgetLines, and BudgetLineRevisions. This is
a new capability — no existing spec exists for this domain.

---

## Shared Constraints

### REQ-SC-01: Authentication

All endpoints MUST require a valid JWT access token. Unauthenticated requests MUST return HTTP 401.

### REQ-SC-02: RBAC

Write endpoints (POST/PUT/PATCH/DELETE) MUST require the caller to hold `budget:admin` on the
target budget. Read endpoints MUST require `budget:read`. Insufficient role MUST return HTTP 403.

### REQ-SC-03: Budget Existence

All routes carry `{id}` as the budget segment. The system MUST return HTTP 404 when the budget does
not exist or the caller has no membership in it (existence-before-auth fallback).

### REQ-SC-04: Soft Delete

All entities MUST support soft delete via `DeletedAt` timestamp. Soft-deleted records MUST be
excluded from all read responses by default. The system MUST allow restoring a soft-deleted record.
Soft delete on a parent MUST cascade logically to all children (sets `DeletedAt` on children).

### REQ-SC-05: Hard Delete Order

Hard deletes (permanent removal) MUST follow child-first order: BudgetLineRevisions → BudgetLines
→ Periods → Cycles; Categories → CategoryGroups.

---

## 1. Cycles

### REQ-CYC-01: Create Cycle

The system MUST allow a `budget:admin` user to create a Cycle for a budget, providing Name,
StartDate, and EndDate. Cycle date ranges MUST NOT overlap with any existing (non-deleted) Cycle in
the same budget.

#### Scenario: Happy path — create cycle `@integration`
- GIVEN a budget exists and the caller has `budget:admin`
- WHEN POST `/api/budgets/{id}/cycles` with valid Name, StartDate, EndDate
- THEN HTTP 201 is returned with the new Cycle id and fields

#### Scenario: Date overlap rejected `@integration`
- GIVEN a Cycle already covers 2025-01-01 to 2025-12-31
- WHEN POST with StartDate=2025-06-01, EndDate=2026-06-30
- THEN HTTP 422 with error code `CYCLE_DATE_OVERLAP`

#### Scenario: StartDate after EndDate rejected `@unit`
- GIVEN valid budget context
- WHEN POST with StartDate=2025-12-31, EndDate=2025-01-01
- THEN HTTP 422 with validation error on StartDate

### REQ-CYC-02: Update Cycle

The system MUST allow updating a Cycle's Name, StartDate, and EndDate. Date-overlap validation MUST
exclude the Cycle being updated. The system MUST reject the update if the new date range would
exclude any existing Period's dates.

#### Scenario: Happy path — rename and shift dates `@integration`
- GIVEN a Cycle with no date conflicts after the change
- WHEN PUT `/api/budgets/{id}/cycles/{cycleId}` with new Name and adjusted dates
- THEN HTTP 200 with updated fields

#### Scenario: Shrinking range that orphans a Period `@integration`
- GIVEN a Cycle 2025-01-01..2025-12-31 contains a Period in December
- WHEN PUT with EndDate=2025-11-30
- THEN HTTP 422 with error code `CYCLE_PERIOD_OUT_OF_RANGE`

### REQ-CYC-03: Delete Cycle

The system MUST soft-delete the Cycle and cascade-soft-delete all its Periods and their BudgetLines
and BudgetLineRevisions. Hard delete MUST follow the child-first order defined in REQ-SC-05.

#### Scenario: Soft delete cascades `@integration`
- GIVEN a Cycle with 2 Periods each having BudgetLines
- WHEN DELETE `/api/budgets/{id}/cycles/{cycleId}`
- THEN HTTP 204; Cycle, Periods, BudgetLines, and Revisions all have `DeletedAt` set

#### Scenario: Non-existent Cycle `@unit`
- GIVEN no Cycle with the given id in the budget
- WHEN DELETE `/api/budgets/{id}/cycles/{unknownId}`
- THEN HTTP 404

### REQ-CYC-04: Set Active Cycle

Only ONE Cycle per Budget MAY be active at a time. The system MUST perform an atomic swap:
deactivate the currently active Cycle (if any) and activate the specified Cycle in a single
transaction.

#### Scenario: Atomic swap from one active to another `@integration`
- GIVEN CycleA is active, CycleB exists and is inactive
- WHEN PUT `/api/budgets/{id}/active-cycle` with body `{ "cycleId": "<CycleB>" }`
- THEN HTTP 200; CycleA.IsActive=false, CycleB.IsActive=true in one transaction

#### Scenario: No previous active cycle `@integration`
- GIVEN no active Cycle exists
- WHEN PUT `/api/budgets/{id}/active-cycle` with a valid cycleId
- THEN HTTP 200; target Cycle.IsActive=true

#### Scenario: Non-existent target cycle `@unit`
- GIVEN the cycleId does not belong to the budget
- WHEN PUT `/api/budgets/{id}/active-cycle`
- THEN HTTP 404

---

## 2. Periods

### REQ-PER-01: Create Period

The system MUST allow creating a Period under a Cycle. Period dates MUST fall within the parent
Cycle date range. No two Periods within the same Cycle MAY have overlapping date ranges.
PeriodNumber MUST be a positive integer unique within the Cycle.

#### Scenario: Happy path `@integration`
- GIVEN a Cycle 2025-01-01..2025-12-31 with no periods
- WHEN POST `/api/budgets/{id}/cycles/{cycleId}/periods` with Name="January", PeriodNumber=1, StartDate=2025-01-01, EndDate=2025-01-31
- THEN HTTP 201 with the new Period id

#### Scenario: Dates outside Cycle range `@unit`
- GIVEN Cycle ends 2025-12-31
- WHEN POST with StartDate=2025-12-01, EndDate=2026-01-31
- THEN HTTP 422 with error code `PERIOD_OUT_OF_CYCLE_RANGE`

#### Scenario: Period date overlap within Cycle `@integration`
- GIVEN a Period already covers 2025-01-01..2025-01-31
- WHEN POST with StartDate=2025-01-15, EndDate=2025-02-15
- THEN HTTP 422 with error code `PERIOD_DATE_OVERLAP`

### REQ-PER-02: Update Period

The system MUST allow updating a Period's Name, PeriodNumber, StartDate, and EndDate, subject to
the same range and overlap rules as Create.

#### Scenario: Happy path `@integration`
- GIVEN a Period that can expand without conflicts
- WHEN PUT `.../periods/{periodId}` with updated dates still within Cycle range
- THEN HTTP 200 with updated fields

### REQ-PER-03: Set Period Status

The system MUST expose a dedicated PATCH endpoint to open or close a Period. Setting IsClosed=true
on a Period MUST prevent any BudgetLine mutations on that Period.

#### Scenario: Close a period `@integration`
- GIVEN an open Period with BudgetLines
- WHEN PATCH `.../periods/{periodId}/status` with `{ "isClosed": true }`
- THEN HTTP 200; Period.IsClosed=true

#### Scenario: Reopen a closed period `@integration`
- GIVEN IsClosed=true
- WHEN PATCH `.../periods/{periodId}/status` with `{ "isClosed": false }`
- THEN HTTP 200; Period.IsClosed=false; BudgetLine mutations allowed again

### REQ-PER-04: Delete Period

The system MUST soft-delete the Period and cascade-soft-delete all its BudgetLines and their
Revisions.

#### Scenario: Soft delete cascades `@integration`
- GIVEN a Period with BudgetLines and Revisions
- WHEN DELETE `.../periods/{periodId}`
- THEN HTTP 204; Period, BudgetLines, Revisions all have `DeletedAt` set

---

## 3. CategoryGroups

### REQ-CG-01: Create CategoryGroup

The system MUST allow creating a CategoryGroup with a Name and DisplayOrder. CategoryGroup.Name
MUST be unique (case-insensitive) per Budget among non-deleted groups.

#### Scenario: Happy path `@integration`
- GIVEN no CategoryGroup named "Housing" in the budget
- WHEN POST `/api/budgets/{id}/category-groups` with Name="Housing", DisplayOrder=1
- THEN HTTP 201 with new group id

#### Scenario: Duplicate name rejected `@integration`
- GIVEN a CategoryGroup named "Housing" already exists
- WHEN POST with Name="Housing"
- THEN HTTP 422 with error code `CATEGORY_GROUP_NAME_DUPLICATE`

### REQ-CG-02: Update CategoryGroup

The system MUST allow updating Name and DisplayOrder. Uniqueness rule applies excluding self.

#### Scenario: Happy path `@integration`
- GIVEN a CategoryGroup "Housing"
- WHEN PUT `.../category-groups/{groupId}` with Name="Home & Utilities"
- THEN HTTP 200 with updated name

### REQ-CG-03: Delete CategoryGroup

Soft-deletes the CategoryGroup and cascade-soft-deletes all its Categories. BudgetLines that
reference a deleted CategoryGroup MUST be included in responses with a `groupDeleted: true` flag.

#### Scenario: Soft delete and cascade `@integration`
- GIVEN a CategoryGroup with 3 Categories
- WHEN DELETE `.../category-groups/{groupId}`
- THEN HTTP 204; group and all Categories have `DeletedAt` set

### REQ-CG-04: Reorder CategoryGroups

The system MUST accept an ordered list of CategoryGroup ids and assign sequential DisplayOrder
values starting at 1. The list MUST contain all non-deleted groups for the budget.

#### Scenario: Happy path `@integration`
- GIVEN 3 groups with DisplayOrder 1, 2, 3
- WHEN PUT `/api/budgets/{id}/category-groups/order` with ids in new order [3, 1, 2]
- THEN HTTP 200; groups have DisplayOrder 1, 2, 3 respectively in the new order

#### Scenario: Incomplete list rejected `@unit`
- GIVEN 3 groups exist
- WHEN PUT with only 2 ids
- THEN HTTP 422 with error code `REORDER_LIST_INCOMPLETE`

---

## 4. Categories

### REQ-CAT-01: Create Category

The system MUST allow creating a Category under a CategoryGroup. Category.Name MUST be unique
(case-insensitive) within the same CategoryGroup among non-deleted categories.

#### Scenario: Happy path `@integration`
- GIVEN a CategoryGroup "Housing" with no "Rent" category
- WHEN POST `.../category-groups/{groupId}/categories` with Name="Rent", DisplayOrder=1
- THEN HTTP 201 with new category id

#### Scenario: Duplicate name within group rejected `@unit`
- GIVEN a Category "Rent" exists in the group
- WHEN POST with Name="Rent"
- THEN HTTP 422 with error code `CATEGORY_NAME_DUPLICATE`

### REQ-CAT-02: Update Category

The system MUST allow updating Name and DisplayOrder. Uniqueness rule applies within the same
group, excluding self.

#### Scenario: Happy path `@integration`
- GIVEN a Category "Rent"
- WHEN PUT `.../categories/{categoryId}` with Name="Rent & Mortgage"
- THEN HTTP 200 with updated name

### REQ-CAT-03: Delete Category

Soft-deletes the Category. BudgetLines referencing this CategoryId retain the reference; the read
layer MUST surface a `categoryDeleted: true` flag.

#### Scenario: Soft delete `@integration`
- GIVEN a Category with BudgetLines referencing it
- WHEN DELETE `.../categories/{categoryId}`
- THEN HTTP 204; Category has `DeletedAt` set; BudgetLines rows unchanged

### REQ-CAT-04: Reorder Categories

The system MUST accept an ordered list of Category ids within a CategoryGroup and assign sequential
DisplayOrder values starting at 1. The list MUST contain all non-deleted categories in the group.

#### Scenario: Happy path `@integration`
- GIVEN 2 categories in a group with DisplayOrder 1, 2
- WHEN PUT `.../categories/order` with ids reversed
- THEN HTTP 200; DisplayOrder values swapped to reflect new order

#### Scenario: Incomplete list rejected `@unit`
- GIVEN 2 categories exist
- WHEN PUT with only 1 id
- THEN HTTP 422 with error code `REORDER_LIST_INCOMPLETE`

---

## 5. BudgetLines

### REQ-BL-01: IsClosed Guard

The system MUST reject CreateBudgetLine, UpdateBudgetLine, and DeleteBudgetLine when the target
Period has `IsClosed = true`. Response MUST be HTTP 409 with error code `PERIOD_CLOSED`.

#### Scenario: Create blocked on closed period `@integration`
- GIVEN Period.IsClosed=true
- WHEN POST `/api/budgets/{id}/periods/{periodId}/lines` with valid payload
- THEN HTTP 409 with error code `PERIOD_CLOSED`

#### Scenario: Update blocked on closed period `@integration`
- GIVEN Period.IsClosed=true and a BudgetLine exists
- WHEN PUT `.../lines/{lineId}` with updated fields
- THEN HTTP 409 with error code `PERIOD_CLOSED`

#### Scenario: Delete blocked on closed period `@integration`
- GIVEN Period.IsClosed=true and a BudgetLine exists
- WHEN DELETE `.../lines/{lineId}`
- THEN HTTP 409 with error code `PERIOD_CLOSED`

### REQ-BL-02: Create BudgetLine

The system MUST allow creating a BudgetLine under an open Period. CategoryGroupId MUST be provided.
CategoryId is optional. LineType MUST be one of `Expense`, `LongTermSavings`, `PreventiveSavings`.
The initial BudgetedAmount and Currency MUST also be provided and stored as the first
BudgetLineRevision.

#### Scenario: Happy path with category `@integration`
- GIVEN open Period, valid CategoryGroupId and CategoryId, LineType=Expense
- WHEN POST `.../lines` with Name="Rent", Amount=1500, Currency="GTQ"
- THEN HTTP 201; BudgetLine row created; BudgetLineRevision row created with Amount=1500

#### Scenario: Happy path without category `@integration`
- GIVEN open Period and valid CategoryGroupId, no CategoryId
- WHEN POST `.../lines` with Name="Miscellaneous", LineType=Expense, Amount=500, Currency="USD"
- THEN HTTP 201; BudgetLine.CategoryId=null; Revision created

#### Scenario: Invalid LineType rejected `@unit`
- GIVEN open Period
- WHEN POST with LineType="Income"
- THEN HTTP 422 with validation error on LineType

#### Scenario: Invalid currency rejected `@unit`
- GIVEN open Period
- WHEN POST with Currency="EUR"
- THEN HTTP 422 with validation error on Currency

### REQ-BL-03: Update BudgetLine

The system MUST allow updating a BudgetLine's Name, LineType, IsRecurring, CategoryGroupId,
CategoryId, BudgetedAmount, and Currency. The update MUST auto-create a new BudgetLineRevision with
the new Amount, Currency, and a timestamp. Existing revisions MUST NOT be mutated.

#### Scenario: Happy path — amount change creates revision `@integration`
- GIVEN open Period, BudgetLine with 1 existing Revision
- WHEN PUT `.../lines/{lineId}` with new Amount=2000, Currency="GTQ"
- THEN HTTP 200; BudgetLine updated; a second BudgetLineRevision row exists; first Revision unchanged

#### Scenario: Revision immutability `@unit`
- GIVEN a BudgetLine with existing Revision rows
- WHEN the handler processes an UpdateBudgetLine command
- THEN existing Revision rows remain byte-for-byte unchanged; only a new row is inserted

### REQ-BL-04: Delete BudgetLine

The system MUST soft-delete the BudgetLine (and cascade-soft-delete its Revisions) when the Period
is open. A soft-deleted BudgetLine MAY be restored. Hard delete removes Revisions first, then
the BudgetLine row.

#### Scenario: Soft delete `@integration`
- GIVEN open Period, BudgetLine with 2 Revisions
- WHEN DELETE `.../lines/{lineId}`
- THEN HTTP 204; BudgetLine.DeletedAt set; both Revisions have DeletedAt set

#### Scenario: Restore soft-deleted line `@integration`
- GIVEN a soft-deleted BudgetLine
- WHEN PATCH `.../lines/{lineId}/restore` (or equivalent restore verb)
- THEN HTTP 200; BudgetLine.DeletedAt=null; Revisions restored

---

## 6. Read Endpoints

### REQ-READ-01: List Cycles

The system MUST return all non-deleted Cycles for a budget, ordered by StartDate ascending, each
including the active-cycle flag and period count.

#### Scenario: Happy path `@integration`
- GIVEN a budget with 2 Cycles, one active
- WHEN GET `/api/budgets/{id}/cycles`
- THEN HTTP 200; both Cycles returned; active one has `isActive: true`

### REQ-READ-02: Get Cycle Detail

The system MUST return a single Cycle with its full Period list (non-deleted, ordered by
PeriodNumber) including each Period's `isClosed` status.

#### Scenario: Happy path `@integration`
- GIVEN Cycle with 3 Periods
- WHEN GET `/api/budgets/{id}/cycles/{cycleId}`
- THEN HTTP 200; body includes `periods` array with 3 entries

#### Scenario: Non-existent Cycle `@unit`
- GIVEN no Cycle with that id
- WHEN GET `.../cycles/{unknownId}`
- THEN HTTP 404

### REQ-READ-03: List CategoryGroups

The system MUST return all non-deleted CategoryGroups for a budget, ordered by DisplayOrder
ascending, with nested non-deleted Categories ordered by their DisplayOrder.

#### Scenario: Happy path `@integration`
- GIVEN 2 groups each with 2 categories
- WHEN GET `/api/budgets/{id}/category-groups`
- THEN HTTP 200; 2 groups; each has `categories` array with 2 entries; ordered by DisplayOrder

### REQ-READ-04: List BudgetLines

The system MUST return all non-deleted BudgetLines for a Period, each showing the latest
BudgetLineRevision's Amount and Currency, ordered by CategoryGroup DisplayOrder then Category
DisplayOrder then BudgetLine Name.

#### Scenario: Happy path with latest revision `@integration`
- GIVEN a BudgetLine with 2 Revisions (latest Amount=2000)
- WHEN GET `/api/budgets/{id}/periods/{periodId}/lines`
- THEN HTTP 200; line shows Amount=2000 (latest revision only)

#### Scenario: Budget:read caller can list `@integration`
- GIVEN caller has ReadOnly membership
- WHEN GET `.../lines`
- THEN HTTP 200

#### Scenario: Budget:admin required to create `@integration`
- GIVEN caller has ReadOnly membership
- WHEN POST `.../lines`
- THEN HTTP 403
