# Delta for budget-structure

## ADDED Requirements

### Requirement: REQ-RST-PERIOD-1 — RestorePeriod Endpoint

`POST /budgets/{budgetId}/cycles/{cycleId}/periods/{periodId}/restore` MUST restore a
soft-deleted Period and cascade-restore all its soft-deleted BudgetLines. The endpoint MUST
require role `budget:admin`. It MUST return HTTP 404 when the Period does not exist under the
given Cycle or is not currently soft-deleted.

#### Scenario: Happy path — Period and BudgetLines restored

- GIVEN a soft-deleted Period with 3 soft-deleted BudgetLines
- WHEN POST `.../periods/{periodId}/restore`
- THEN HTTP 200; Period.DeletedAt = null; all 3 BudgetLines.DeletedAt = null

#### Scenario: Period not soft-deleted — 404

- GIVEN a non-deleted (active) Period
- WHEN POST `.../periods/{periodId}/restore`
- THEN HTTP 404

#### Scenario: Period does not exist — 404

- GIVEN a periodId that does not belong to the given Cycle or Budget
- WHEN POST `.../periods/{periodId}/restore`
- THEN HTTP 404

#### Scenario: Parent Cycle is soft-deleted — 409

- GIVEN the Period's parent Cycle has DeletedAt set
- WHEN POST `.../periods/{periodId}/restore`
- THEN HTTP 409 Conflict, error code `PARENT_IS_DELETED`

#### Scenario: Unauthenticated — 401

- GIVEN no JWT in the request
- WHEN POST `.../periods/{periodId}/restore`
- THEN HTTP 401

#### Scenario: Insufficient role — 403

- GIVEN caller has `budget:operator` (below `budget:admin`)
- WHEN POST `.../periods/{periodId}/restore`
- THEN HTTP 403

---

### Requirement: REQ-LIST-CYC-DELETED-1 — ListCycles includeDeleted Flag

`GET /budgets/{budgetId}/cycles` MUST accept an optional `includeDeleted` boolean query parameter
(default `false`). When `includeDeleted=true`, the response MUST include soft-deleted Cycles in
addition to active ones. Each Cycle item in the response MUST include a `deletedAt` field
(ISO 8601 string or null). When `includeDeleted=false` or omitted, behavior MUST match the
existing REQ-READ-01 (non-deleted only).

#### Scenario: Default — no deleted cycles returned

- GIVEN a budget with 1 active Cycle and 1 soft-deleted Cycle
- WHEN GET `/api/budgets/{id}/cycles` (no query param)
- THEN HTTP 200 with 1 item; soft-deleted Cycle is absent

#### Scenario: includeDeleted=true — all cycles returned

- GIVEN a budget with 1 active Cycle and 1 soft-deleted Cycle
- WHEN GET `/api/budgets/{id}/cycles?includeDeleted=true`
- THEN HTTP 200 with 2 items; soft-deleted Cycle present with `deletedAt` set

#### Scenario: deletedAt field present on active cycles

- GIVEN a budget with 1 active Cycle
- WHEN GET `/api/budgets/{id}/cycles?includeDeleted=true`
- THEN the active Cycle item has `deletedAt: null`
