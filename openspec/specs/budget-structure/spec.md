# Budget Structure Specification

## Purpose

Defines the structural backbone of a budget: Cycles (yearly planning periods), Periods (monthly or
arbitrary sub-divisions), CategoryGroups, Categories, BudgetLines, and BudgetLineRevisions.
Extends with Currency reference table, cycle currency fields, budget line currency tracking,
display ordering, and restore cascade endpoints.

---

## Capability Index

| Capability | Type | Requirements |
|---|---|---|
| `cycles` | Core | REQ-CYC-01 through REQ-CYC-04 |
| `periods` | Core | REQ-PER-01 through REQ-PER-04 |
| `category-groups` | Core | REQ-CG-01 through REQ-CG-04 |
| `categories` | Core | REQ-CAT-01 through REQ-CAT-04 |
| `budget-lines` | Core | REQ-BL-01 through REQ-BL-05 |
| `currency-reference` | Added (patch) | REQ-CUR-01, REQ-CUR-02 |
| `cycle-currency` | Modified (patch) | REQ-CYC-CUR-01, REQ-CYC-CUR-02 |
| `budget-restore` | Added (patch) | REQ-RST-01 through REQ-RST-06 |

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
- THEN HTTP 400 (JSON deserialization rejects unknown enum name before FluentValidation; unit validator tests cover enum rejection at domain layer)

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

The system MUST soft-delete the BudgetLine when the Period is open. BudgetLineRevisions are
immutable (no `DeletedAt` per ADR-BS-01) and become inaccessible through the ListBudgetLines JOIN
when the BudgetLine is soft-deleted — this satisfies the business intent without mutating revision
history. Hard delete is sequential: BudgetLineRevisions first, then BudgetLine.

#### Scenario: Soft delete `@integration`
- GIVEN open Period, BudgetLine with 2 Revisions
- WHEN DELETE `.../lines/{lineId}`
- THEN HTTP 204; BudgetLine.DeletedAt set; Revisions remain in DB (immutable) but are inaccessible

### REQ-BL-05: Currency and DisplayOrder

**Currency**: Each `BudgetLineRevision` MUST store a `CurrencyId` (FK to Currency table) instead of a Currency string. When creating or updating a BudgetLine without an explicit `CurrencyId`, the handler MUST default to the parent Cycle's `DefaultCurrencyId`.

**DisplayOrder**: Each `BudgetLine` MUST have a `DisplayOrder` (int, NOT NULL) for explicit ordering. The `ReorderBudgetLines` endpoint accepts an ordered array of BudgetLine IDs and updates `DisplayOrder` on each.

#### Scenario: CurrencyId defaults to Cycle default `@integration`
- GIVEN open Period → Cycle with DefaultCurrencyId = GTQ
- WHEN POST `.../lines` without explicit CurrencyId
- THEN BudgetLineRevision.CurrencyId = GTQ

#### Scenario: DisplayOrder backfill `@integration`
- GIVEN existing BudgetLines before migration
- WHEN migration runs
- THEN every BudgetLine has DisplayOrder >= 1, ordered by CreatedAt within (PeriodId, CategoryGroupId, CategoryId) partitions

#### Scenario: Reorder BudgetLines `@integration`
- GIVEN 3 BudgetLines in a Period with DisplayOrder 1, 2, 3
- WHEN PUT `.../periods/{periodId}/budget-lines/order` with IDs in reversed order
- THEN BudgetLines have DisplayOrder reassigned to match the new order

---

## 6. Currency Reference

### REQ-CUR-01: Currency Entity

A `Currency` entity with `Id` (Guid PK), `Code` (varchar 3, unique), `Name` (varchar 100), `Symbol` (varchar 10) MUST exist as a database table. The table is seeded with exactly three rows and has no soft-delete column.

#### Seed data

| Code | Name | Symbol |
|---|---|---|
| GTQ | Quetzal | Q |
| USD | US Dollar | $ |
| EUR | Euro | € |

#### Scenario: Currency catalog unchanged by API `@unit`
- GIVEN the Currencies table seeded
- WHEN a request to create or update a currency directly via the application
- THEN no write endpoint exists (404 on non-GET paths under currencies)

### REQ-CUR-02: List Currencies

A read-only endpoint `GET /budgets/{budgetId}/currencies` MUST exist. It returns all currencies regardless of budgetId. The `budgetId` path segment is present for routing consistency only.

#### Scenario: Happy path `@integration`
- GIVEN budgetId = any valid budget Guid (or nonexistent)
- WHEN GET `/budgets/{budgetId}/currencies`
- THEN HTTP 200 with array of all currencies (currently 3: GTQ, USD, EUR)

---

## 7. Cycle Currency Fields

### REQ-CYC-CUR-01: Currency Fields on Cycle

`Cycle` MUST have:
- `DefaultCurrencyId` (Guid, NOT NULL, FK → Currency)
- `AlternateCurrencyId` (Guid?, nullable, FK → Currency)
- `ExchangeRate` (decimal(18,6), nullable)

`AlternateCurrencyId` and `ExchangeRate` MUST be provided together or both omitted.

#### Scenario: Create with default currency only `@integration`
- GIVEN valid budget and Cycle data
- WHEN POST `/api/budgets/{id}/cycles` with DefaultCurrencyId, no alternate
- THEN HTTP 201; Cycle.DefaultCurrencyId set, AlternateCurrencyId = null, ExchangeRate = null

#### Scenario: Pair rule enforced `@unit`
- GIVEN CreateCycle or UpdateCycle command with AlternateCurrencyId but no ExchangeRate
- WHEN validator runs
- THEN validation error, code `CYC_PAIR_INCOMPLETE`, HTTP 400

### REQ-CYC-CUR-02: Cycle Read Responses

`GetCycleDetail` response MUST include `defaultCurrency` (Code, Symbol), and optionally `alternateCurrency` (Code, Symbol) and `exchangeRate`.

`ListCycles` response items MUST include `defaultCurrency` (Code, Symbol), and optionally `alternateCurrencyId` (Guid?), `exchangeRate` (decimal?), and `alternateCurrency` (object with Code and Symbol, nullable).

#### Scenario: Detail with alternate currency `@integration`
- GIVEN Cycle with DefaultCurrencyId=GTQ, AlternateCurrencyId=USD, ExchangeRate=7.5
- WHEN GET `/api/budgets/{id}/cycles/{cycleId}`
- THEN HTTP 200; defaultCurrency: { code: "GTQ", symbol: "Q" }, alternateCurrency: { code: "USD", symbol: "$" }, exchangeRate: 7.5

#### Scenario: List includes alternate currency when present `@integration`
- GIVEN a Cycle with DefaultCurrencyId=GTQ, AlternateCurrencyId=USD, ExchangeRate=7.5
- WHEN GET `/api/budgets/{id}/cycles`
- THEN each matching item includes alternateCurrencyId, exchangeRate, and alternateCurrency: { code: "USD", symbol: "$" }

#### Scenario: List item has null alternate fields when not set `@integration`
- GIVEN a Cycle with only DefaultCurrencyId set and no AlternateCurrencyId
- WHEN GET `/api/budgets/{id}/cycles`
- THEN the item has alternateCurrencyId=null, exchangeRate=null, alternateCurrency=null

---

## 8. Restore Endpoints

### REQ-RST-01: Restore() Method

Each soft-deletable entity (Cycle, Period, CategoryGroup, Category, BudgetLine) MUST expose a `Restore()` method that sets `DeletedAt = null` and refreshes `UpdatedAt = DateTimeOffset.UtcNow`.

#### Scenario: Restore entity `@unit`
- GIVEN a soft-deleted Cycle
- WHEN Cycle.Restore() called
- THEN Cycle.DeletedAt = null, UpdatedAt refreshed

### REQ-RST-02: RestoreCycle Cascade

Route: `POST /budgets/{budgetId}/cycles/{cycleId}/restore`

Restores Cycle, then all its soft-deleted Periods, then all soft-deleted BudgetLines of those Periods. The `includeExecutionRecords` query parameter is accepted but ignored (no-op, forward-compat for `budget-execution`).

#### Scenario: Full cascade restore `@integration`
- GIVEN soft-deleted Cycle with 2 soft-deleted Periods, each with 2 soft-deleted BudgetLines
- WHEN POST `.../cycles/{cycleId}/restore`
- THEN HTTP 200; Cycle, all Periods, all BudgetLines have DeletedAt = null

#### Scenario: Non-deleted Cycle rejected `@unit`
- GIVEN an already-active (non-deleted) Cycle
- WHEN POST `.../cycles/{cycleId}/restore`
- THEN HTTP 404 (soft-deleted entities only visible via IgnoreQueryFilters in restore handlers)

### REQ-RST-03: RestoreCategoryGroup Cascade

Route: `POST /budgets/{budgetId}/category-groups/{groupId}/restore`

Restores CategoryGroup, then all its soft-deleted Categories, then all soft-deleted BudgetLines whose `CategoryGroupId` matches.

#### Scenario: Happy path `@integration`
- GIVEN soft-deleted CategoryGroup with 2 soft-deleted Categories, each with 2 soft-deleted BudgetLines
- WHEN POST `.../category-groups/{groupId}/restore`
- THEN HTTP 200; all entities have DeletedAt = null

### REQ-RST-04: RestoreCategory

Route: `POST /budgets/{budgetId}/categories/{categoryId}/restore`

Restores Category and all soft-deleted BudgetLines whose `CategoryId` matches.

#### Scenario: Parent-deleted guard `@integration`
- GIVEN soft-deleted Category whose parent CategoryGroup is soft-deleted
- WHEN POST `.../categories/{categoryId}/restore`
- THEN HTTP 409 Conflict, error code `PARENT_IS_DELETED`

### REQ-RST-05: RestoreBudgetLine

Route: `POST /budgets/{budgetId}/periods/{periodId}/budget-lines/{lineId}/restore`

Restores only the specified BudgetLine.

#### Scenario: Parent-deleted guard `@integration`
- GIVEN soft-deleted BudgetLine whose parent Period is soft-deleted
- WHEN POST `.../budget-lines/{lineId}/restore`
- THEN HTTP 409 Conflict, error code `PARENT_IS_DELETED`

### REQ-RST-06: includeExecutionRecords Parameter

All four restore endpoints MUST accept `includeExecutionRecords` (bool, default false) as a query parameter. The parameter is present in the API contract today for forward-compatibility with `budget-execution`. Handlers MUST ignore this flag (no-op).

#### Scenario: Parameter accepted and ignored `@unit`
- GIVEN any restore endpoint
- WHEN called with ?includeExecutionRecords=true
- THEN restore completes as if parameter were false (no-op)

---

## 9. Read Endpoints

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
