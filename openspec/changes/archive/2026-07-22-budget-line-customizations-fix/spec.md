# Delta Spec: budget-line-customizations

**Change**: `budget-line-customizations`
**Date**: 2026-07-21
**Domains**: `budget-line-revisions` (new), `budget-structure` (delta), `budget-execution` (delta)

## Capability Index

| Capability | Type | Requirements |
|---|---|---|
| `budget-line-revisions` | New | REQ-BLR-01 … REQ-BLR-05 |
| `budget-structure` | Modified | REQ-BL-DATERANGE-1, REQ-BL-CONCURRENCY-1, REQ-BL-AUDIT-1 |
| `budget-execution` | Modified | REQ-EXEC-RESTORE-DATERANGE-1 |

## Key Requirements

- REQ-BLR-01: GET .../revisions — list all revisions ordered ValidFrom ASC; requires budget:admin
- REQ-BLR-02: POST .../revisions — create via SplitRevision; validFrom must be today or future and within line date range; requires budget:admin
- REQ-BLR-03: DELETE .../revisions/{id} — gapless repair; block original revision; block if active executions in range; requires budget:admin
- REQ-BLR-04: i18n EN+ES for all 5 error codes
- REQ-BLR-05: Frontend BudgetLineCustomizationsView at lines/:lineId/customizations; BudgetLineModal edit mode strips Amount Revision section
- REQ-BL-DATERANGE-1: PATCH .../date-range — UpdateDateRange domain method; guards: RANGE_WOULD_ORPHAN_REVISION, RANGE_WOULD_ORPHAN_EXECUTION; requires budget:admin; writes audit log
- REQ-BL-CONCURRENCY-1: BudgetLine uses xmin concurrency token; DbUpdateConcurrencyException → 409
- REQ-BL-AUDIT-1: AuditLog entries for BudgetLineDateRangeUpdated and BudgetLineRevisionDeleted
- REQ-EXEC-RESTORE-DATERANGE-1: RestoreExecutionRecord loads BudgetLine; checks Period.StartDate/EndDate against BudgetLine range; rejects with EXECUTION_OUT_OF_DATE_RANGE (422); OperationDate is NOT used

## HTTP Status / Error Code Map

| Code | Domain | HTTP | Trigger |
|---|---|---|---|
| `RANGE_WOULD_ORPHAN_REVISION` | budget-structure | 422 | Date-range shrink orphans a revision |
| `RANGE_WOULD_ORPHAN_EXECUTION` | budget-structure | 409 | Date-range shrink has active executions outside |
| `REVISION_HAS_ACTIVE_EXECUTIONS` | budget-line-revisions | 409 | Revision delete blocked by active executions |
| `CANNOT_DELETE_ORIGINAL_REVISION` | budget-line-revisions | 422 | Delete of original revision |
| `EXECUTION_OUT_OF_DATE_RANGE` | budget-execution | 422 | Restore blocked — period outside BudgetLine range |

## RBAC

| Operation | Required Role |
|---|---|
| GET .../revisions | budget:admin |
| POST .../revisions | budget:admin |
| DELETE .../revisions/{id} | budget:admin |
| PATCH .../date-range | budget:admin |
| POST .../executions/{id}/restore | budget:operator (unchanged) |
