# Spec: budget-structure-patch

**Change name**: `budget-structure-patch`
**Type**: Patch — schema extension + new endpoints
**Depends on**: `budget-structure` (merged, main)
**Blocks**: `budget-execution`
**Date**: 2026-07-11

---

## Capability Index

| Capability | Type | Requirements |
|---|---|---|
| `currency-reference` | New | CUR-1, CUR-2, CUR-3, CUR-4 |
| `cycle-currency` | Modified (delta on `budget-structure`) | CYC-1, CYC-2, CYC-3, CYC-4, CYC-5, CYC-6, CYC-7, CYC-8, CYC-9 |
| `budget-line-currency` | Modified (delta on `budget-structure`) | BLR-1, BLR-2, BLR-3, BLR-4, BLR-5 |
| `budget-line-display-order` | Modified (delta on `budget-structure`) | BLD-1, BLD-2, BLD-3 |
| `budget-restore` | New | RST-1, RST-2, RST-3, RST-4, RST-5, RST-6, RST-7 |

---

## Requirements Table

| ID | Capability | Statement |
|---|---|---|
| CUR-1 | `currency-reference` | A `Currency` entity with `Id` (Guid PK), `Code` (varchar 3, unique), `Name` (varchar 100), `Symbol` (varchar 10) MUST exist as a database table. |
| CUR-2 | `currency-reference` | The migration MUST seed exactly three rows: GTQ / Quetzal / Q, USD / US Dollar / $, EUR / Euro / €. |
| CUR-3 | `currency-reference` | `Currency` has no soft-delete column. It is a global catalog — rows are never deleted via the application. |
| CUR-4 | `currency-reference` | A read-only endpoint `GET /budgets/{budgetId}/currencies` MUST exist. It returns all currencies regardless of budget. The `budgetId` path segment is present for routing consistency only. No write endpoints exist for Currency. |
| CYC-1 | `cycle-currency` | `Cycle` MUST have `DefaultCurrencyId` (Guid, NOT NULL, FK → Currency). |
| CYC-2 | `cycle-currency` | `Cycle` MUST have `AlternateCurrencyId` (Guid?, nullable, FK → Currency). |
| CYC-3 | `cycle-currency` | `Cycle` MUST have `ExchangeRate` (decimal(18,6), nullable). |
| CYC-4 | `cycle-currency` | `AlternateCurrencyId` and `ExchangeRate` MUST be provided together or both omitted. Providing one without the other is a validation error. |
| CYC-5 | `cycle-currency` | ExchangeRate semantics: X DefaultCurrency = 1 AlternateCurrency (e.g., 7.5 GTQ = 1 USD means ExchangeRate = 7.5). |
| CYC-6 | `cycle-currency` | `CreateCycle` command MUST accept `DefaultCurrencyId` (required), `AlternateCurrencyId` (optional), `ExchangeRate` (optional). |
| CYC-7 | `cycle-currency` | `UpdateCycle` command MUST accept `DefaultCurrencyId` (required), `AlternateCurrencyId` (optional), `ExchangeRate` (optional). The CYC-4 pair rule applies on both create and update. |
| CYC-8 | `cycle-currency` | `GetCycleDetail` response MUST include `defaultCurrency` (Code, Symbol) and optionally `alternateCurrency` (Code, Symbol) and `exchangeRate`. |
| CYC-9 | `cycle-currency` | `ListCycles` response items MUST include `defaultCurrency` (Code, Symbol). |
| BLR-1 | `budget-line-currency` | `BudgetLineRevision.Currency` varchar(3) column MUST be replaced by `CurrencyId` (Guid, NOT NULL, FK → Currency). |
| BLR-2 | `budget-line-currency` | The migration MUST DELETE all existing `BudgetLineRevision` rows before altering the column (test data, approved). |
| BLR-3 | `budget-line-currency` | `CreateBudgetLine` MUST accept an optional `CurrencyId`. When omitted, the handler MUST resolve `Cycle.DefaultCurrencyId` via the Period → Cycle chain and use it. |
| BLR-4 | `budget-line-currency` | `UpdateBudgetLine` MUST accept an optional `CurrencyId`. When omitted, `CurrencyId` on the new revision defaults to `Cycle.DefaultCurrencyId` (same resolution path as BLR-3). |
| BLR-5 | `budget-line-currency` | `ListBudgetLines` response items MUST include `currency` containing `code` and `symbol` from the latest revision's Currency. |
| BLD-1 | `budget-line-display-order` | `BudgetLine` MUST have `DisplayOrder` (int, NOT NULL). |
| BLD-2 | `budget-line-display-order` | The migration MUST backfill existing `BudgetLine` rows with a sequential `DisplayOrder` starting at 1, ordered by `CreatedAt` ASC within each `(PeriodId, CategoryGroupId, CategoryId)` partition. |
| BLD-3 | `budget-line-display-order` | A `ReorderBudgetLines` endpoint MUST exist following the same pattern as `ReorderCategories` and `ReorderCategoryGroups`. |
| RST-1 | `budget-restore` | Each soft-deletable entity (Cycle, Period, CategoryGroup, Category, BudgetLine) MUST expose a `Restore()` method that sets `DeletedAt = null`. |
| RST-2 | `budget-restore` | `POST /budgets/{budgetId}/cycles/{cycleId}/restore` MUST restore Cycle, then all its soft-deleted Periods, then all soft-deleted BudgetLines of those Periods. |
| RST-3 | `budget-restore` | `POST /budgets/{budgetId}/category-groups/{groupId}/restore` MUST restore CategoryGroup, then all its soft-deleted Categories, then all soft-deleted BudgetLines whose `CategoryGroupId` matches. |
| RST-4 | `budget-restore` | `POST /budgets/{budgetId}/categories/{categoryId}/restore` MUST restore Category, then all soft-deleted BudgetLines whose `CategoryId` matches. |
| RST-5 | `budget-restore` | `POST /budgets/{budgetId}/periods/{periodId}/budget-lines/{lineId}/restore` MUST restore only the specified BudgetLine. |
| RST-6 | `budget-restore` | Every restore endpoint MUST accept `includeExecutionRecords` (bool, default false) as a query or body parameter. Handlers MUST ignore this flag (no-op). The parameter exists for forward-compatibility with `budget-execution`. |
| RST-7 | `budget-restore` | A restore request MUST be rejected with `409 Conflict` if the immediate parent entity is currently soft-deleted. Cascade ancestors are not checked (only direct parent). |

---

## Capability Specifications

---

### CUR — Currency Reference

#### CUR-1 through CUR-4: Currency Entity and Endpoint

**CUR-1 to CUR-3: Schema**

The `Currencies` table has no soft-delete column. It is seeded once and never modified by the application.

Seed data:

| Code | Name | Symbol |
|---|---|---|
| GTQ | Quetzal | Q |
| USD | US Dollar | $ |
| EUR | Euro | € |

**Scenarios**

```
GIVEN the migration has run
WHEN SELECT * FROM Currencies
THEN exactly 3 rows returned with codes GTQ, USD, EUR

GIVEN a request to insert a new currency directly into the table
WHEN no application endpoint exists for Currency write
THEN no endpoint is available (404 on any non-GET path under currencies)
```

**CUR-4: GET /budgets/{budgetId}/currencies**

Returns the full currency list regardless of budgetId. The response is an array of `{ id, code, name, symbol }`.

```
GIVEN budgetId = any valid budget Guid
WHEN GET /budgets/{budgetId}/currencies
THEN 200 OK with array of 3 items (or more if seeds are extended)
THEN each item contains id, code, name, symbol

GIVEN budgetId that does not exist
WHEN GET /budgets/{budgetId}/currencies
THEN 200 OK — endpoint does not validate budget existence (catalog is global)
```

---

### CYC — Cycle Currency Fields

#### CYC-1 through CYC-5: Schema Rules

`DefaultCurrencyId` is NOT NULL — every Cycle must be assigned a default currency at creation.

`AlternateCurrencyId` + `ExchangeRate` are a nullable pair: both present or both absent.

**Scenarios**

```
GIVEN CreateCycle request with DefaultCurrencyId only (no alternate)
WHEN handler processes the request
THEN Cycle.DefaultCurrencyId set, AlternateCurrencyId = null, ExchangeRate = null
THEN 201 Created

GIVEN CreateCycle request with AlternateCurrencyId and ExchangeRate both provided
WHEN handler processes the request
THEN Cycle.AlternateCurrencyId set, Cycle.ExchangeRate set
THEN 201 Created

GIVEN CreateCycle request with AlternateCurrencyId provided but ExchangeRate absent
WHEN validator runs
THEN validation error returned
THEN 400 Bad Request, error code CYC_PAIR_INCOMPLETE

GIVEN CreateCycle request with ExchangeRate provided but AlternateCurrencyId absent
WHEN validator runs
THEN validation error returned
THEN 400 Bad Request, error code CYC_PAIR_INCOMPLETE

GIVEN UpdateCycle request that changes DefaultCurrencyId to a valid Currency Guid
WHEN handler processes the request
THEN Cycle.DefaultCurrencyId updated

GIVEN UpdateCycle request that clears AlternateCurrencyId (set to null) but leaves ExchangeRate set
WHEN validator runs
THEN validation error, error code CYC_PAIR_INCOMPLETE
```

#### CYC-8 to CYC-9: Response Projection

```
GIVEN GetCycleDetail for a Cycle with no alternate currency
WHEN response serialised
THEN defaultCurrency: { code, symbol } present
THEN alternateCurrency: null, exchangeRate: null

GIVEN GetCycleDetail for a Cycle with alternate currency
WHEN response serialised
THEN alternateCurrency: { code, symbol } present
THEN exchangeRate: <value> present

GIVEN ListCycles
WHEN response serialised
THEN each item contains defaultCurrency: { code, symbol }
```

---

### BLR — BudgetLine Revision Currency

#### BLR-1 and BLR-2: Schema Migration

The `BudgetLineRevisions.Currency` varchar(3) column is replaced by `CurrencyId` Guid FK to `Currencies`.

The migration must:
1. DELETE FROM BudgetLineRevisions (all rows — test data only, approved by user)
2. DROP the `Currency` varchar column
3. ADD `CurrencyId` Guid NOT NULL FK → Currencies

**Scenarios**

```
GIVEN the migration has run
WHEN DESCRIBE BudgetLineRevisions
THEN no column named "Currency" exists
THEN column "CurrencyId" exists as Guid NOT NULL with FK to Currencies
```

#### BLR-3 to BLR-5: Create / Update / List

**Scenarios**

```
GIVEN CreateBudgetLine request with CurrencyId explicitly provided
WHEN handler creates initial BudgetLineRevision
THEN revision.CurrencyId = provided CurrencyId

GIVEN CreateBudgetLine request with CurrencyId absent
WHEN handler resolves BudgetLine → Period → Cycle
THEN revision.CurrencyId = Cycle.DefaultCurrencyId

GIVEN UpdateBudgetLine request with CurrencyId explicitly provided
WHEN handler creates new BudgetLineRevision
THEN revision.CurrencyId = provided CurrencyId

GIVEN UpdateBudgetLine request with CurrencyId absent
WHEN handler resolves Period → Cycle
THEN new revision.CurrencyId = Cycle.DefaultCurrencyId

GIVEN ListBudgetLines for a Period
WHEN response serialised
THEN each item contains currency: { code, symbol } from the latest revision's Currency
```

---

### BLD — BudgetLine DisplayOrder

#### BLD-1 and BLD-2: Schema and Backfill

`BudgetLines.DisplayOrder` is int NOT NULL. The migration backfills existing rows.

Backfill rule: within each `(PeriodId, CategoryGroupId, CategoryId)` partition, assign sequential integers starting at 1 ordered by `CreatedAt` ASC.

**Scenarios**

```
GIVEN existing BudgetLines before migration
WHEN migration runs
THEN every BudgetLine has DisplayOrder >= 1
THEN within a given (PeriodId, CategoryGroupId, CategoryId) group, DisplayOrder values are consecutive starting at 1

GIVEN two BudgetLines in the same group with CreatedAt: T1 < T2
WHEN migration backfill runs
THEN line with T1 has DisplayOrder = 1
THEN line with T2 has DisplayOrder = 2
```

#### BLD-3: ReorderBudgetLines Endpoint

Follows the exact pattern of `ReorderCategories` / `ReorderCategoryGroups`. Accepts an ordered array of BudgetLine IDs and updates `DisplayOrder` on each.

Route: `PUT /budgets/{budgetId}/periods/{periodId}/budget-lines/order`

**Scenarios**

```
GIVEN 3 BudgetLines in a Period with DisplayOrder 1, 2, 3
WHEN PUT .../budget-lines/order with IDs in reversed order [id3, id2, id1]
THEN BudgetLines have DisplayOrder 1, 2, 3 assigned to id3, id2, id1 respectively

GIVEN ReorderBudgetLines with an ID that does not belong to the specified Period
WHEN handler validates
THEN 422 Unprocessable Entity, error code REORDER_ID_NOT_IN_SCOPE

GIVEN ReorderBudgetLines with a duplicate ID in the list
WHEN handler validates
THEN 422 Unprocessable Entity, error code REORDER_DUPLICATE_ID
```

---

### RST — Budget Restore

#### RST-1: Entity Restore() Method

Each soft-deletable entity (Cycle, Period, CategoryGroup, Category, BudgetLine) gains a `Restore()` method. The method sets `DeletedAt = null` and `UpdatedAt = DateTimeOffset.UtcNow`.

**Scenarios**

```
GIVEN a soft-deleted Cycle (DeletedAt is set)
WHEN Cycle.Restore() called
THEN Cycle.DeletedAt = null
THEN Cycle.UpdatedAt refreshed
```

#### RST-2: RestoreCycle

Route: `POST /budgets/{budgetId}/cycles/{cycleId}/restore`

Cascade order: Cycle → all soft-deleted Periods that belong to this Cycle → all soft-deleted BudgetLines that belong to those Periods.

ExecutionRecords: no-op (not in scope).

**Scenarios**

```
GIVEN a soft-deleted Cycle with 2 soft-deleted Periods, each with 2 soft-deleted BudgetLines
WHEN POST .../cycles/{cycleId}/restore
THEN Cycle.DeletedAt = null
THEN both Periods.DeletedAt = null
THEN all 4 BudgetLines.DeletedAt = null
THEN 200 OK (or 204 No Content, consistent with existing Delete responses)

GIVEN a soft-deleted Cycle with 1 Period (NOT deleted) containing 2 soft-deleted BudgetLines
WHEN POST .../cycles/{cycleId}/restore
THEN Cycle.DeletedAt = null
THEN already-active Period unchanged
THEN soft-deleted BudgetLines under the active Period are NOT restored (only children restored through cascade from restored Periods)

GIVEN a soft-deleted Cycle whose Budget is not accessible to the caller
WHEN POST .../cycles/{cycleId}/restore
THEN 403 Forbidden

GIVEN an already-active Cycle (DeletedAt = null)
WHEN POST .../cycles/{cycleId}/restore
THEN 404 Not Found (soft-deleted entities only visible via IgnoreQueryFilters in restore handlers)
```

Note on scenario 2: the cascade only walks Periods that were restored during this operation — it does not re-restore BudgetLines under Periods that were already active. This matches the "delete cascades down, restore mirrors delete" principle.

#### RST-3: RestoreCategoryGroup

Route: `POST /budgets/{budgetId}/category-groups/{groupId}/restore`

Cascade: CategoryGroup → its soft-deleted Categories → soft-deleted BudgetLines with `CategoryGroupId` = this group.

**Scenarios**

```
GIVEN a soft-deleted CategoryGroup with 2 soft-deleted Categories,
      each Category having 2 soft-deleted BudgetLines referencing that CategoryGroup
WHEN POST .../category-groups/{groupId}/restore
THEN CategoryGroup.DeletedAt = null
THEN both Categories.DeletedAt = null
THEN all 4 BudgetLines.DeletedAt = null

GIVEN a soft-deleted CategoryGroup whose parent Budget is soft-deleted
WHEN POST .../category-groups/{groupId}/restore
THEN 409 Conflict, error code PARENT_IS_DELETED
```

#### RST-4: RestoreCategory

Route: `POST /budgets/{budgetId}/categories/{categoryId}/restore`

Cascade: Category → soft-deleted BudgetLines with `CategoryId` = this category.

**Scenarios**

```
GIVEN a soft-deleted Category with 3 soft-deleted BudgetLines
WHEN POST .../categories/{categoryId}/restore
THEN Category.DeletedAt = null
THEN 3 BudgetLines.DeletedAt = null

GIVEN a soft-deleted Category whose parent CategoryGroup is soft-deleted
WHEN POST .../categories/{categoryId}/restore
THEN 409 Conflict, error code PARENT_IS_DELETED
```

#### RST-5: RestoreBudgetLine

Route: `POST /budgets/{budgetId}/periods/{periodId}/budget-lines/{lineId}/restore`

Restores only the specified BudgetLine. No children to cascade to.

**Scenarios**

```
GIVEN a soft-deleted BudgetLine in an active Period
WHEN POST .../budget-lines/{lineId}/restore
THEN BudgetLine.DeletedAt = null
THEN 200 OK

GIVEN a soft-deleted BudgetLine whose parent Period is soft-deleted
WHEN POST .../budget-lines/{lineId}/restore
THEN 409 Conflict, error code PARENT_IS_DELETED
```

#### RST-6: includeExecutionRecords Forward-Compat Parameter

All four restore endpoints MUST accept `includeExecutionRecords` (bool, default false). The parameter is present in the API contract today so that `budget-execution` SDD can add the implementation without a breaking API change.

**Scenarios**

```
GIVEN any restore endpoint called with ?includeExecutionRecords=true
WHEN handler processes the request
THEN restore completes as if the parameter were false (no-op)
THEN no error returned

GIVEN any restore endpoint called without includeExecutionRecords
WHEN handler processes the request
THEN restore completes normally (defaults to false)
```

#### RST-7: Parent-Deleted Guard

The guard checks only the **direct** parent entity (one level up). It does not walk the full ancestor chain.

| Restore target | Direct parent checked |
|---|---|
| Cycle | Budget (not soft-deletable — guard not applicable) |
| CategoryGroup | Budget (not soft-deletable — guard not applicable) |
| Category | CategoryGroup |
| BudgetLine (via RestoreBudgetLine) | Period |
| BudgetLines cascaded via RestoreCycle | — (guard already passed at Cycle level) |
| BudgetLines cascaded via RestoreCategoryGroup | — (guard already passed at Group level) |
| BudgetLines cascaded via RestoreCategory | — (guard already passed at Category level) |

Note: Budget entity is not soft-deletable in the current schema, so RST-7 applies only to Category and RestoreBudgetLine.

---

## Validation Rules

| Code | Trigger | Condition | HTTP |
|---|---|---|---|
| `CYC_PAIR_INCOMPLETE` | CreateCycle / UpdateCycle | AlternateCurrencyId XOR ExchangeRate is provided | 400 |
| `CYC_DEFAULT_CURRENCY_REQUIRED` | CreateCycle / UpdateCycle | DefaultCurrencyId is absent or null | 400 |
| `PARENT_IS_DELETED` | Any restore endpoint | Direct parent entity has DeletedAt set | 409 |
| `REORDER_ID_NOT_IN_SCOPE` | ReorderBudgetLines | Any ID in payload does not belong to the specified Period | 422 |
| `REORDER_DUPLICATE_ID` | ReorderBudgetLines | Payload contains duplicate IDs | 422 |

---

## Error Response Shape

Follows existing project error envelope (same as DeleteCycle, ReorderCategories, etc.):

```json
{
  "type": "https://mybudget.app/errors/<error-code>",
  "title": "<human-readable title>",
  "status": <http-status>,
  "detail": "<detail message>",
  "traceId": "<trace-id>"
}
```

---

## Out of Scope (explicit exclusions)

- Audit logging — separate `audit-log` SDD change
- ExecutionRecord entity, table, restore logic — deferred to `budget-execution`
- Period-level restore endpoint — Periods restore only through Cycle cascade
- Currency CRUD (create/update/delete currency records) — catalog is immutable via API
- Frontend changes — separate SDD cycle

---

## Assumptions Made

1. `Budget` entity has no `DeletedAt` column — RST-7 guard is not applicable at Budget level.
2. `BudgetLines` cascaded during `RestoreCategoryGroup` are matched by `CategoryGroupId` only (not `CategoryId`), covering lines that belonged to the group regardless of specific category.
3. A BudgetLine soft-deleted independently (not as part of a cascade) is still restored by a cascade restore of its parent, as long as the parent is being restored in this operation.
4. `ReorderBudgetLines` scopes IDs by `PeriodId` (consistent with how `ReorderCategories` scopes by `CategoryGroupId`).
5. `GET /budgets/{budgetId}/currencies` returns 200 even when budgetId does not correspond to an existing budget — the list is global. No authorization check on budgetId beyond the authenticated user's session.
