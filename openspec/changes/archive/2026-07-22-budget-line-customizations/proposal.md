# Proposal: Budget Line Customizations

## Intent

BudgetLine date ranges are immutable after creation and revisions are invisible beyond the current effective amount. Users cannot adjust a line's active period, view revision history, or delete obsolete revisions. This change exposes date-range editing, revision CRUD, and restore guards so budget administrators can manage line lifecycles without recreating lines.

## Scope

### In Scope

- **PR1 — Frontend customizations view**: child route `lines/:lineId/customizations`, revision list/create/delete UI, strip Amount Revision section from `BudgetLineModal` edit mode
- **PR2 — Backend range guards + revision CRUD**: `PATCH .../date-range` endpoint, `GET/POST .../revisions`, `DELETE .../revisions/:revisionId`, `ListBudgetLineRevisions` handler, concurrency token (`xmin`) on `BudgetLine`, audit log entries for date-range changes and revision deletes
- **PR3 — Restore validation**: `RestoreExecutionRecord` gains BudgetLine date-range intersection check; returns `EXECUTION_OUT_OF_DATE_RANGE`

### Out of Scope

- Bulk revision operations
- Revision undo/revert (beyond delete)
- Frontend date-range editing UI (deferred to a later PR after PR2 backend lands)
- Changes to `SplitRevision` domain logic

## Capabilities

### New Capabilities

- `budget-line-revisions`: revision list, create (via `SplitRevision`), and delete with gapless repair

### Modified Capabilities

- `budget-structure`: `BudgetLine.UpdateDateRange()` domain method, `DeleteRevision()` domain method, concurrency token, audit events
- `budget-execution`: `RestoreExecutionRecord` gains date-range intersection guard

## Approach

**Separate PATCH /date-range endpoint** (Approach B from exploration). Each concern gets its own command: date-range mutation is isolated from metadata updates and revision splits.

| Concern | Entry Point |
|---|---|
| Date-range change | `PATCH /api/budgets/{budgetId}/lines/{lineId}/date-range` |
| Revision list | `GET /api/budgets/{budgetId}/lines/{lineId}/revisions` |
| Revision create | `POST /api/budgets/{budgetId}/lines/{lineId}/revisions` (delegates to `SplitRevision`) |
| Revision delete | `DELETE /api/budgets/{budgetId}/lines/{lineId}/revisions/{revisionId}` |

**Authorization**: All new endpoints require `budget:admin`.

**Concurrency**: PostgreSQL `xmin` as concurrency token on `BudgetLine` aggregate root. Handler catches `DbUpdateConcurrencyException` and returns 409 Conflict.

**Audit**: Date-range changes and revision deletes write to existing `AuditLog` table (`EntityId`, `Action`, `UserId`, `Timestamp`).

**Error codes** (domain-level):

| Code | Trigger |
|---|---|
| `RANGE_WOULD_ORPHAN_REVISION` | Date-range shrink leaves a revision outside |
| `RANGE_WOULD_ORPHAN_EXECUTION` | Date-range shrink has active executions outside |
| `REVISION_HAS_ACTIVE_EXECUTIONS` | Revision delete blocked by execution records |
| `CANNOT_DELETE_ORIGINAL_REVISION` | Delete blocked — revision is the original (no predecessor) |
| `EXECUTION_OUT_OF_DATE_RANGE` | Restore execution blocked by BudgetLine date range |

## Affected Areas

| Area | Impact | Description |
|---|---|---|
| `SharedKernel/Entities/BudgetLine.cs` | Modified | `UpdateDateRange()`, `DeleteRevision()`, `xmin` concurrency |
| `Features/BudgetStructure/` (4 new slices) | New | ListRevisions, CreateRevision, DeleteRevision, UpdateDateRange |
| `Features/BudgetExecution/RestoreExecutionRecord/` | Modified | Date-range intersection guard |
| `frontend/src/features/budget-structure/views/` | New | `BudgetLineCustomizationsView.vue` |
| `frontend/src/features/budget-structure/api/budgetLines.api.ts` | Modified | Revision endpoints |
| `frontend/src/features/budget-structure/store.ts` | Modified | Revision state + actions |
| `frontend/src/features/budget-structure/components/BudgetLineModal.vue` | Modified | Strip Amount Revision section |
| `frontend/src/router/index.ts` | Modified | Child route |
| New resx keys (EN + ES) | New | Error messages for 5 error codes |

## Risks

| Risk | Likelihood | Mitigation |
|---|---|---|
| Gapless repair edge cases on delete (last revision, first in chain) | Med | Unit tests before integration; block delete of original revision |
| EF tracking — nav collection removal vs explicit `DbSet.Remove` | Med | Use explicit `DbSet.Remove` for revision deletion |
| PR2 size approaching 400-line budget | Med | Monitor during tasks phase; split `UpdateDateRange` into micro-PR if needed |
| PR1 strip ordering — removing Amount Revision before customizations route | Low | Wire customizations route first, then strip modal section |

## Rollback Plan

Each PR is independently revertable. PR3 depends on PR2 (needs date-range fields), PR1 is independent. Revert order: PR3, PR2, PR1. No migrations alter existing columns — only `xmin` shadow property added (no schema migration needed for PostgreSQL `xmin`).

## Dependencies

- Existing `AuditLog` infrastructure (table + write helper)
- `SplitRevision` domain method (already implemented and tested)
- PostgreSQL `xmin` system column (no migration required)

## Success Criteria

- [ ] Revision CRUD endpoints pass integration tests with `budget:admin` gating
- [ ] Date-range PATCH rejects orphaning revisions and executions with correct error codes
- [ ] Revision delete performs gapless repair and is audit-logged
- [ ] `RestoreExecutionRecord` rejects restores outside BudgetLine date range
- [ ] Concurrency conflict returns 409 on stale `xmin`
- [ ] Frontend customizations view lists, creates, and deletes revisions
- [ ] All error codes have EN + ES translations
