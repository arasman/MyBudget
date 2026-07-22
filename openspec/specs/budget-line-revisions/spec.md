# Spec: budget-line-revisions

**Domain**: `budget-line-revisions`
**Added**: budget-line-customizations change (2026-07-22)

---

## REQ-BLR-01 — List BudgetLine Revisions

`GET /api/budgets/{budgetId}/lines/{lineId}/revisions` returns all revisions for the BudgetLine ordered by `ValidFrom` ASC. Requires `budget:admin`.

---

## REQ-BLR-02 — Create BudgetLine Revision

`POST /api/budgets/{budgetId}/lines/{lineId}/revisions` adds a revision via `BudgetLine.SplitRevision()`. Requires `budget:admin`. Request: `validFrom` (DateOnly, today or future, within BudgetLine range), `newAmount` (decimal > 0), `currencyId` (Guid), `note` (optional, varchar 200). Returns 409 on stale `xmin`.

---

## REQ-BLR-03 — Delete BudgetLine Revision

`DELETE /api/budgets/{budgetId}/lines/{lineId}/revisions/{revisionId}` calls `BudgetLine.DeleteRevision()` with gapless repair. Requires `budget:admin`.

Deletion rules:
- Preceding revision: its `ValidTo` extends to cover deleted revision's range (or null if deleted was last).
- No preceding revision (original): reject `CANNOT_DELETE_ORIGINAL_REVISION` (422).
- Active execution in revision's `[ValidFrom, ValidTo]`: reject `REVISION_HAS_ACTIVE_EXECUTIONS` (409).
- Soft-deleted executions: do NOT block deletion.

Returns 409 on stale `xmin`. Writes `AuditLog` entry with `Action="BudgetLineRevisionDeleted"`.

---

## REQ-BLR-04 — i18n Error Keys

| Error Code | i18n Key |
|---|---|
| `CANNOT_DELETE_ORIGINAL_REVISION` | `budgetLineRevisions.errors.cannotDeleteOriginalRevision` |
| `REVISION_HAS_ACTIVE_EXECUTIONS` | `budgetLineRevisions.errors.revisionHasActiveExecutions` |
| `RANGE_WOULD_ORPHAN_REVISION` | `budgetLineRevisions.errors.rangeWouldOrphanRevision` |
| `RANGE_WOULD_ORPHAN_EXECUTION` | `budgetLineRevisions.errors.rangeWouldOrphanExecution` |
| `EXECUTION_OUT_OF_DATE_RANGE` | `budgetLineRevisions.errors.executionOutOfDateRange` |

---

## REQ-BLR-05 — Frontend Customizations View

`BudgetLineCustomizationsView` at child route `lines/:lineId/customizations`. Displays revision table (ValidFrom, ValidTo, Amount, Currency, Note) with inline create and delete actions. `BudgetLineModal` edit mode must NOT render Amount Revision section (validFrom, validTo, newAmount).

---

## Error Code HTTP Mapping

| Code | HTTP |
|---|---|
| `CANNOT_DELETE_ORIGINAL_REVISION` | 422 |
| `RANGE_WOULD_ORPHAN_REVISION` | 422 |
| `REVISION_HAS_ACTIVE_EXECUTIONS` | 409 |
| `RANGE_WOULD_ORPHAN_EXECUTION` | 409 |
| `EXECUTION_OUT_OF_DATE_RANGE` | 422 |

---

## RBAC

| Operation | Role |
|---|---|
| GET revisions | `budget:admin` |
| POST revisions | `budget:admin` |
| DELETE revision | `budget:admin` |
| PATCH date-range | `budget:admin` |
