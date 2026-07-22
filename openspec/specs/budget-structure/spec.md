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
| `soft-delete-restore` | Added (patch) | REQ-RST-PERIOD-1, REQ-LIST-CYC-DELETED-1 |

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

### REQ-AUTHZ-1: Budget Authorization with Soft-Delete Check

The system MUST resolve the current user's role for a given budget by querying `BudgetMembership`
at request time. This MUST NOT rely on any role stored in the JWT.

**Role hierarchy (descending privilege):** `owner` > `admin` > `operator` > `read-only`

**Soft-delete check:** Before membership lookup, the authorization handler MUST check whether
the target Budget has `IsDeleted = true`. If the Budget is soft-deleted, the handler MUST treat it
identically to a non-existent budget: set `httpContext.Items["budget-not-found"] = true` and return
HTTP 404. The membership cache entry (`budget-membership:{userId}:{budgetId}`) MUST NOT be populated
for a soft-deleted budget.

**Caching:** The authorization handler SHOULD use a short-TTL in-memory cache (keyed by `userId + budgetId`, TTL ≤ 60 seconds) to avoid N+1 DB queries per request. Cache MUST be invalidated when membership changes or the budget is soft-deleted/restored.

#### Scenario: Authorized request

- GIVEN user has `admin` role in budget `{id}` and budget is not soft-deleted
- WHEN a protected endpoint requiring minimum `admin` role is called
- THEN the request is allowed through

#### Scenario: Insufficient role

- GIVEN user has `operator` role in budget `{id}` and budget is not soft-deleted
- WHEN a protected endpoint requiring `admin` or higher is called
- THEN `403 Forbidden` is returned with error code `AUTH_INSUFFICIENT_ROLE`

#### Scenario: No membership

- GIVEN user has no `BudgetMembership` record for budget `{id}` and budget is not soft-deleted
- WHEN any protected budget endpoint is called
- THEN `403 Forbidden` is returned with error code `AUTH_NOT_A_MEMBER`

#### Scenario: JWT has no roles

- GIVEN a valid JWT with no role claims
- WHEN a protected budget endpoint is called
- THEN role resolution is performed exclusively via DB/cache lookup — the JWT role field is never consulted

#### Scenario: Soft-deleted budget returns 404

- GIVEN budget `{id}` has `IsDeleted = true`
- WHEN any protected budget endpoint is called (regardless of the caller's membership or role)
- THEN HTTP 404 is returned
- AND no membership cache entry is written for `budget-membership:{userId}:{budgetId}`

#### Scenario: Restored budget is accessible again

- GIVEN budget `{id}` was soft-deleted and has now been restored (`IsDeleted = false`)
- AND any stale `budget-membership:{userId}:{budgetId}` cache entries have been evicted
- WHEN a protected budget endpoint is called by a member
- THEN the request is handled normally (200 or 403 by role, not 404)

**Error responses:**
| Condition | HTTP | Error Code |
|---|---|---|
| Role below required threshold | 403 | `AUTH_INSUFFICIENT_ROLE` |
| No membership for budget | 403 | `AUTH_NOT_A_MEMBER` |
| Budget is soft-deleted | 404 | (same path as budget-not-found) |

## Validation: Budget and Entity Uniqueness

### Requirement: REQ-BUDGET-UNIQUE-1: Budget Name Uniqueness per User

The system MUST reject creating or renaming a Budget when the same name already exists for the
same user, including soft-deleted budgets. The check MUST include soft-deleted budgets
(no global `HasQueryFilter` on Budget; handler query MUST NOT add one).

#### Scenario: Create duplicate budget name rejected `@integration`
- GIVEN a budget named "Family Budget" (active) for user U1
- WHEN POST `/api/budgets` with Name="Family Budget" for U1
- THEN HTTP 422 with error code `BUDGET_NAME_DUPLICATE`

#### Scenario: Create rejected when same name in soft-deleted budget `@integration`
- GIVEN a soft-deleted budget named "Family Budget" for user U1
- WHEN POST `/api/budgets` with Name="Family Budget" for U1
- THEN HTTP 422 with error code `BUDGET_NAME_DUPLICATE`

#### Scenario: Rename duplicate budget name rejected `@integration`
- GIVEN budgets "A" and "B" (active) for user U1
- WHEN PATCH/PUT rename on "B" to Name="A"
- THEN HTTP 422 with error code `BUDGET_NAME_DUPLICATE`

#### Scenario: Rename allowed when name is unique `@integration`
- GIVEN only one budget named "A" for user U1
- WHEN rename to "A Updated"
- THEN HTTP 200 with updated name

---

### Requirement: REQ-CYC-NAME-1: Cycle Name Uniqueness per Budget

The system MUST reject creating or updating a Cycle when the same name already exists in the same
budget, including soft-deleted cycles.

#### Scenario: Create duplicate cycle name rejected `@integration`
- GIVEN a Cycle named "2025" (active or soft-deleted) in the budget
- WHEN POST `/api/budgets/{id}/cycles` with Name="2025"
- THEN HTTP 422 with error code `CYCLE_NAME_DUPLICATE`

#### Scenario: Update allowed — self-rename `@integration`
- GIVEN a Cycle "2025" being updated (self)
- WHEN PUT with Name="2025" on the same cycleId
- THEN HTTP 200 (self-exclusion applies)

---

### Requirement: REQ-PER-NAME-1: Period Name Uniqueness per Cycle

The system MUST reject creating or updating a Period when the same name already exists in the same
cycle, including soft-deleted periods.

#### Scenario: Create duplicate period name rejected `@integration`
- GIVEN a Period named "January" (active or soft-deleted) in cycle C1
- WHEN POST `.../cycles/{C1}/periods` with Name="January"
- THEN HTTP 422 with error code `PERIOD_NAME_DUPLICATE`

#### Scenario: Update allowed — self-rename `@integration`
- GIVEN a Period "January" being updated (self)
- WHEN PUT with Name="January" on the same periodId
- THEN HTTP 200 (self-exclusion applies)

---

### Requirement: REQ-BL-NAME-1: BudgetLine Name Uniqueness per Budget

The system MUST reject creating or updating a BudgetLine when the same name already exists within
the same Budget (scoped by `BudgetId` only), including soft-deleted lines. The uniqueness check MUST
be enforced via a DB-level `UNIQUE(BudgetId, Name)` index with no filter clause (soft-deleted rows
are included).

#### Scenario: Create duplicate name rejected across budget `@integration`
- GIVEN a BudgetLine named "Rent" (active or soft-deleted) in Budget B1
- WHEN POST `/api/budgets/{budgetId}/lines` with Name="Rent"
- THEN HTTP 422 with error code `BUDGET_LINE_NAME_DUPLICATE`

#### Scenario: Self-rename allowed `@integration`
- GIVEN a BudgetLine "Rent" being updated (same lineId)
- WHEN PUT with Name="Rent"
- THEN HTTP 200 (self-exclusion applies)

#### Scenario: Same name in different budget allowed `@integration`
- GIVEN a BudgetLine named "Rent" in Budget B1
- WHEN POST `/api/budgets/{B2}/lines` with Name="Rent"
- THEN HTTP 201 (different budget scope)

---

### Requirement: REQ-BL-AMOUNT-1: BudgetLine Amount Greater Than Zero

The system MUST reject a BudgetLine BudgetedAmount of zero or below. The FluentValidation rule
MUST use `GreaterThan(0)`, not `GreaterThanOrEqualTo(0)`.

#### Scenario: Amount zero rejected `@unit`
- GIVEN a CreateBudgetLine or UpdateBudgetLine command with BudgetedAmount = 0
- WHEN the validator runs
- THEN HTTP 422 with validation error on BudgetedAmount

#### Scenario: Positive amount accepted `@unit`
- GIVEN BudgetedAmount = 0.01
- WHEN the validator runs
- THEN no validation error on BudgetedAmount

---

### Requirement: REQ-BL-NOTE-MAX-1: BudgetLineRevision Note Max Length

`BudgetLineRevisionConfiguration` MUST configure `Note` with `HasMaxLength(200)`.

#### Scenario: Note within max length stored `@unit`
- GIVEN a BudgetLineRevision Note of exactly 200 characters
- WHEN SaveChangesAsync runs
- THEN no DB truncation or constraint error

#### Scenario: Note exceeding max length rejected at DB level `@unit`
- GIVEN a BudgetLineRevision Note of 201 characters
- WHEN SaveChangesAsync runs
- THEN a DB constraint violation is raised

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

### REQ-CYC-03: Delete Cycle (BudgetLine cascade removed)

The system MUST soft-delete the Cycle and cascade-soft-delete all its Periods.
BudgetLines MUST NOT be cascade-deleted when Cycle is deleted.

#### Scenario: Soft delete Cycle — BudgetLines unaffected `@integration`
- GIVEN a Cycle with Periods; Budget-level BudgetLines exist
- WHEN DELETE cycle
- THEN Cycle and Periods deleted; BudgetLines remain active

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

The system MUST soft-delete the Period. BudgetLines MUST NOT be cascade-deleted when Period is deleted.

#### Scenario: Soft delete period `@integration`
- GIVEN a Period with BudgetLines
- WHEN DELETE `.../periods/{periodId}`
- THEN HTTP 204; Period has `DeletedAt` set; BudgetLines remain active

---

## 3. CategoryGroups

### REQ-CG-01: Create CategoryGroup

The system MUST allow creating a CategoryGroup with a Name and DisplayOrder. CategoryGroup.Name
MUST be unique (case-insensitive) per Budget among ALL groups, including soft-deleted ones.
The uniqueness check MUST use `IgnoreQueryFilters()` so that soft-deleted records are included.

#### Scenario: Happy path `@integration`
- GIVEN no CategoryGroup named "Housing" in the budget (active or deleted)
- WHEN POST `/api/budgets/{id}/category-groups` with Name="Housing", DisplayOrder=1
- THEN HTTP 201 with new group id

#### Scenario: Duplicate name rejected — active group `@integration`
- GIVEN an active CategoryGroup named "Housing"
- WHEN POST with Name="Housing"
- THEN HTTP 422 with error code `CATEGORY_GROUP_NAME_DUPLICATE`

#### Scenario: Duplicate name rejected — soft-deleted group `@integration`
- GIVEN a soft-deleted CategoryGroup named "Housing" in the same budget
- WHEN POST with Name="Housing"
- THEN HTTP 422 with error code `CATEGORY_GROUP_NAME_DUPLICATE`

### REQ-CG-02: Update CategoryGroup

The system MUST allow updating Name and DisplayOrder. Uniqueness rule applies excluding self,
and MUST include soft-deleted records via `IgnoreQueryFilters()`.

#### Scenario: Happy path `@integration`
- GIVEN a CategoryGroup "Housing" and no other group named "Home & Utilities" (active or deleted)
- WHEN PUT `.../category-groups/{groupId}` with Name="Home & Utilities"
- THEN HTTP 200 with updated name

#### Scenario: Duplicate name rejected — soft-deleted sibling `@integration`
- GIVEN a soft-deleted CategoryGroup named "Home & Utilities" in the same budget
- WHEN PUT with Name="Home & Utilities" on a different group
- THEN HTTP 422 with error code `CATEGORY_GROUP_NAME_DUPLICATE`

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
(case-insensitive) within the same CategoryGroup among ALL categories, including soft-deleted ones.
The uniqueness check MUST use `IgnoreQueryFilters()`.

#### Scenario: Happy path `@integration`
- GIVEN a CategoryGroup "Housing" with no "Rent" category (active or deleted)
- WHEN POST `.../category-groups/{groupId}/categories` with Name="Rent", DisplayOrder=1
- THEN HTTP 201 with new category id

#### Scenario: Duplicate name within group rejected — soft-deleted `@integration`
- GIVEN a soft-deleted Category "Rent" in the same group
- WHEN POST with Name="Rent"
- THEN HTTP 422 with error code `CATEGORY_NAME_DUPLICATE`

### REQ-CAT-02: Update Category

The system MUST allow updating Name and DisplayOrder. Uniqueness rule applies within the same
group, excluding self, and MUST include soft-deleted records via `IgnoreQueryFilters()`.

#### Scenario: Happy path `@integration`
- GIVEN a Category "Rent" and no soft-deleted or active sibling named "Rent & Mortgage"
- WHEN PUT `.../categories/{categoryId}` with Name="Rent & Mortgage"
- THEN HTTP 200 with updated name

#### Scenario: Duplicate name rejected — soft-deleted sibling `@integration`
- GIVEN a soft-deleted Category "Rent & Mortgage" in the same group
- WHEN PUT with Name="Rent & Mortgage" on a different category
- THEN HTTP 422 with error code `CATEGORY_NAME_DUPLICATE`

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

The system MUST reject revision splits with `ValidFrom` in a closed period. Metadata-only edits
(name, categoryGroupId, categoryId) are not blocked by IsClosed. Route no longer includes `periodId`.

#### Scenario: Revision split with ValidFrom in closed period blocked `@integration`
- GIVEN a Period with `IsClosed=true` covering a date range
- WHEN PUT `.../lines/{lineId}` with `ValidFrom` falling within that closed period
- THEN HTTP 409 with error code `PERIOD_CLOSED`

#### Scenario: Metadata-only update on budget with closed periods allowed `@integration`
- GIVEN all Periods in a Budget are closed
- WHEN PUT `.../lines/{lineId}` updating only Name
- THEN HTTP 200

### REQ-BL-02: Create BudgetLine

The command MUST provide `StartDate`, `EndDate` (optional), `InitialAmount` (>0), `CurrencyId`.
Handler creates BudgetLine + initial BudgetLineRevision covering [StartDate, EndDate].
Route: `POST /api/budgets/{budgetId}/lines`.

#### Scenario: Happy path with finite date range `@integration`
- GIVEN StartDate=2025-01-01, EndDate=2025-12-31, InitialAmount=1500, CurrencyId=GTQ
- WHEN POST `/api/budgets/{budgetId}/lines`
- THEN HTTP 201; BudgetLineRevision with ValidFrom=2025-01-01, ValidTo=2025-12-31

#### Scenario: Happy path with perpetual end date `@integration`
- GIVEN StartDate=2025-01-01, EndDate=null
- WHEN POST `/api/budgets/{budgetId}/lines`
- THEN HTTP 201; BudgetLineRevision.ValidTo = null

#### Scenario: EndDate before StartDate rejected `@unit`
- GIVEN StartDate=2025-06-01, EndDate=2025-05-31
- WHEN validator runs
- THEN HTTP 422 with validation error on EndDate

#### Scenario: InitialAmount zero rejected `@unit`
- GIVEN InitialAmount = 0
- WHEN validator runs
- THEN HTTP 422 with validation error on InitialAmount

### REQ-BL-03: Update BudgetLine

Metadata updates (Name, CategoryGroupId, CategoryId) do not affect revisions. Amount revision
requires `ValidFrom`, `NewAmount`, `CurrencyId` — calls `BudgetLine.SplitRevision()`.
`IsRecurring` removed.

#### Scenario: Metadata-only update `@integration`
- GIVEN a BudgetLine with existing revisions
- WHEN PUT with only Name changed
- THEN HTTP 200; revision count unchanged

#### Scenario: Amount revision split `@integration`
- GIVEN revision [2025-01-01, null, 1500 GTQ], ValidFrom=2025-06-01, NewAmount=2000
- WHEN PUT `.../lines/{lineId}`
- THEN original revision trimmed to ValidTo=2025-05-31; new revision [2025-06-01, null, 2000] inserted

#### Scenario: ValidFrom before today rejected `@unit`
- GIVEN ValidFrom = yesterday
- WHEN validator runs
- THEN HTTP 422 (no retroactive splits)

#### Scenario: ValidFrom outside BudgetLine date range rejected `@unit`
- GIVEN BudgetLine.EndDate=2025-12-31, ValidFrom=2026-01-01
- WHEN validator runs
- THEN HTTP 422 on ValidFrom

### REQ-BL-04: Delete BudgetLine

Route: `DELETE /api/budgets/{budgetId}/lines/{lineId}` (no periodId). IsClosed guard removed from delete.

#### Scenario: Soft delete `@integration`
- GIVEN an active BudgetLine
- WHEN DELETE `/api/budgets/{budgetId}/lines/{lineId}`
- THEN HTTP 204; BudgetLine.DeletedAt set

### REQ-BL-05: Currency and DisplayOrder (Reorder scope)

Reorder scope: `(BudgetId, CategoryGroupId, CategoryId)`. Route: `/api/budgets/{budgetId}/lines/order`.

#### Scenario: Reorder at budget scope `@integration`
- GIVEN 3 BudgetLines under same CategoryGroup in a Budget
- WHEN PUT `/api/budgets/{budgetId}/lines/order` with IDs in new order
- THEN HTTP 200; DisplayOrder values reassigned

### REQ-BL-ENTITY-1: BudgetLine Date Range Fields

`BudgetLine` MUST have `StartDate` (DateOnly, required) and `EndDate` (DateOnly?, nullable).
`PeriodId` and `IsRecurring` MUST NOT exist.

#### Scenario: BudgetLine created with date range `@unit`
- GIVEN BudgetLine.Create(..., startDate=2025-01-01, endDate=null)
- WHEN factory runs
- THEN BudgetLine.StartDate=2025-01-01, EndDate=null

### REQ-BL-REVISION-1: BudgetLineRevision ValidFrom/ValidTo

`BudgetLineRevision` MUST have `ValidFrom` (DateOnly, required) and `ValidTo` (DateOnly?, nullable).
`RevisedAt` MUST NOT exist.

#### Scenario: BudgetLineRevision created with ValidFrom/ValidTo `@unit`
- GIVEN BudgetLineRevision.Create(..., validFrom=2025-01-01, validTo=null, amount=1500)
- WHEN factory runs
- THEN ValidFrom=2025-01-01, ValidTo=null

### REQ-BL-SPLIT-1: Gapless Revision via SplitRevision

`BudgetLine.SplitRevision(newValidFrom, newValidTo, amount, currencyId)`: (1) trims enclosing
revision ValidTo = newValidFrom-1, (2) inserts new revision, (3) if newValidTo is not null and
enclosing had no ValidTo or ValidTo > newValidTo: inserts tail revision.

#### Scenario: Split creates head, new, and tail `@unit`
- GIVEN revision [2025-01-01, null, 1500, GTQ]
- WHEN SplitRevision(2025-06-01, 2025-08-31, 2000, GTQ)
- THEN revisions: [2025-01-01, 2025-05-31, 1500], [2025-06-01, 2025-08-31, 2000], [2025-09-01, null, 1500]

#### Scenario: Open-ended split — no tail `@unit`
- GIVEN revision [2025-01-01, null, 1500, GTQ]
- WHEN SplitRevision(2025-06-01, null, 2000, GTQ)
- THEN revisions: [2025-01-01, 2025-05-31, 1500], [2025-06-01, null, 2000]

#### Scenario: No enclosing revision — error `@unit`
- GIVEN revision [2025-01-01, 2025-06-30, 1500]
- WHEN SplitRevision(newValidFrom=2025-08-01, ...)
- THEN domain exception

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

### REQ-RST-02: RestoreCycle (BudgetLine cascade removed)

Route: `POST /budgets/{budgetId}/cycles/{cycleId}/restore`

Restores Cycle and all its soft-deleted Periods. MUST NOT cascade-restore BudgetLines.

#### Scenario: Restore Cycle and Periods only `@integration`
- GIVEN soft-deleted Cycle with 2 soft-deleted Periods
- WHEN POST `.../cycles/{cycleId}/restore`
- THEN HTTP 200; Cycle and all Periods have DeletedAt = null

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

Route: `POST /budgets/{budgetId}/lines/{lineId}/restore` (no periodId).

Restores only the specified BudgetLine.

#### Scenario: Restore without periodId `@integration`
- GIVEN soft-deleted BudgetLine
- WHEN POST `/api/budgets/{budgetId}/lines/{lineId}/restore`
- THEN HTTP 200; DeletedAt = null

### REQ-RST-06: includeExecutionRecords Parameter

All four restore endpoints MUST accept `includeExecutionRecords` (bool, default false) as a query parameter. When `includeExecutionRecords=true`, soft-deleted ExecutionRecords (managed by `budget-execution`) MUST be restored along with their parent entities.

#### Scenario: Parameter accepted and children restored `@integration`
- GIVEN any restore endpoint with a soft-deleted child ExecutionRecord
- WHEN called with ?includeExecutionRecords=true
- THEN soft-deleted child ExecutionRecords are restored in the same operation

#### Scenario: Parameter false or omitted preserves previous behavior `@integration`
- GIVEN any restore endpoint
- WHEN called with ?includeExecutionRecords=false or parameter omitted
- THEN ExecutionRecords remain soft-deleted (default behavior)

### REQ-RST-PERIOD-1 — RestorePeriod Endpoint

Route: `POST /api/budgets/{budgetId}/cycles/{cycleId}/periods/{periodId}/restore`

Restores a soft-deleted Period and cascade-restores all its soft-deleted BudgetLines. The endpoint
MUST require role `budget:admin`. It MUST return HTTP 404 when the Period does not exist under the
given Cycle or is not currently soft-deleted. It MUST return HTTP 409 Conflict with error code
`PARENT_IS_DELETED` when the parent Cycle is soft-deleted.

#### Scenario: Happy path — Period and BudgetLines restored `@integration`
- GIVEN a soft-deleted Period with 3 soft-deleted BudgetLines
- WHEN POST `.../periods/{periodId}/restore`
- THEN HTTP 200; Period.DeletedAt = null; all 3 BudgetLines.DeletedAt = null

#### Scenario: Period not soft-deleted — 404 `@unit`
- GIVEN a non-deleted (active) Period
- WHEN POST `.../periods/{periodId}/restore`
- THEN HTTP 404

#### Scenario: Period does not exist — 404 `@unit`
- GIVEN a periodId that does not belong to the given Cycle or Budget
- WHEN POST `.../periods/{periodId}/restore`
- THEN HTTP 404

#### Scenario: Parent Cycle is soft-deleted — 409 `@integration`
- GIVEN the Period's parent Cycle has DeletedAt set
- WHEN POST `.../periods/{periodId}/restore`
- THEN HTTP 409 Conflict, error code `PARENT_IS_DELETED`

#### Scenario: Unauthenticated — 401 `@integration`
- GIVEN no JWT in the request
- WHEN POST `.../periods/{periodId}/restore`
- THEN HTTP 401

#### Scenario: Insufficient role — 403 `@integration`
- GIVEN caller has `budget:operator` (below `budget:admin`)
- WHEN POST `.../periods/{periodId}/restore`
- THEN HTTP 403

### REQ-LIST-CYC-DELETED-1 — ListCycles includeDeleted Flag

The `GET /api/budgets/{budgetId}/cycles` endpoint MUST accept an optional `includeDeleted` boolean
query parameter (default `false`). When `includeDeleted=true`, the response MUST include soft-deleted
Cycles in addition to active ones. Each Cycle item in the response MUST include a `deletedAt` field
(ISO 8601 string or null). When `includeDeleted=false` or omitted, behavior MUST match the existing
REQ-READ-01 (non-deleted only).

#### Scenario: Default — no deleted cycles returned `@integration`
- GIVEN a budget with 1 active Cycle and 1 soft-deleted Cycle
- WHEN GET `/api/budgets/{id}/cycles` (no query param)
- THEN HTTP 200 with 1 item; soft-deleted Cycle is absent

#### Scenario: includeDeleted=true — all cycles returned `@integration`
- GIVEN a budget with 1 active Cycle and 1 soft-deleted Cycle
- WHEN GET `/api/budgets/{id}/cycles?includeDeleted=true`
- THEN HTTP 200 with 2 items; soft-deleted Cycle present with `deletedAt` set

#### Scenario: deletedAt field present on active cycles `@integration`
- GIVEN a budget with 1 active Cycle
- WHEN GET `/api/budgets/{id}/cycles?includeDeleted=true`
- THEN the active Cycle item has `deletedAt: null`

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

Scoped by `budgetId` only. Response includes `startDate`, `endDate`, `budgetedAmount`.
Excludes `isRecurring`, `revisedAt`. Route: `GET /api/budgets/{budgetId}/lines`.

#### Scenario: Returns budget-scoped lines `@integration`
- GIVEN a Budget with 3 active BudgetLines
- WHEN GET `/api/budgets/{budgetId}/lines`
- THEN HTTP 200; all 3 lines with startDate, endDate, budgetedAmount; no isRecurring
