# Proposal: Budget Line Customizations

## Intent

BudgetLine date ranges are immutable after creation and revisions are invisible beyond the current effective amount. Users cannot adjust a line's active period, view revision history, or delete obsolete revisions. This change exposes date-range editing, revision CRUD, and restore guards so budget administrators can manage line lifecycles without recreating lines.

## Scope

### In Scope
- PR1 — Frontend customizations view: child route `lines/:lineId/customizations`, revision list/create/delete UI, strip Amount Revision section from `BudgetLineModal` edit mode
- PR2 — Backend range guards + revision CRUD: `PATCH .../date-range` endpoint, `GET/POST .../revisions`, `DELETE .../revisions/:revisionId`, `ListBudgetLineRevisions` handler, concurrency token (`xmin`) on `BudgetLine`, audit log entries
- PR3 — Restore validation: `RestoreExecutionRecord` gains BudgetLine date-range intersection check; returns `EXECUTION_OUT_OF_DATE_RANGE`

### Out of Scope
- Bulk revision operations
- Revision undo/revert (beyond delete)
- Frontend date-range editing UI (deferred)

## Approach

Separate PATCH /date-range endpoint (Approach B). Each concern gets its own command.

| Concern | Entry Point |
|---|---|
| Date-range change | `PATCH /api/budgets/{budgetId}/lines/{lineId}/date-range` |
| Revision list | `GET /api/budgets/{budgetId}/lines/{lineId}/revisions` |
| Revision create | `POST /api/budgets/{budgetId}/lines/{lineId}/revisions` (delegates to `SplitRevision`) |
| Revision delete | `DELETE /api/budgets/{budgetId}/lines/{lineId}/revisions/{revisionId}` |

Authorization: All new endpoints require `budget:admin`.
Concurrency: PostgreSQL `xmin` as concurrency token on `BudgetLine`.
Audit: Date-range changes and revision deletes write to existing `AuditLog` table.

## Error Codes

| Code | Trigger |
|---|---|
| `RANGE_WOULD_ORPHAN_REVISION` | Date-range shrink leaves a revision outside |
| `RANGE_WOULD_ORPHAN_EXECUTION` | Date-range shrink has active executions outside |
| `REVISION_HAS_ACTIVE_EXECUTIONS` | Revision delete blocked by execution records |
| `CANNOT_DELETE_ORIGINAL_REVISION` | Delete blocked — revision is the original (no predecessor) |
| `EXECUTION_OUT_OF_DATE_RANGE` | Restore execution blocked by BudgetLine date range |

## PR Chain

PR1 → PR2a → PR2b → PR3 → feat/budget-line-customizations → main

## Rollback Plan

Each PR independently revertable. Revert order: PR3, PR2, PR1. No migrations alter existing columns — only `xmin` shadow property added (no schema migration needed).
