## Exploration: budget-line-customizations

### Current State

**BudgetLine** has `StartDate`/`EndDate` set at creation; `Update()` only accepts name/category/lineType — no date-range mutation path exists. `SplitRevision()` is the only revision mutation; it is gapless and fully tested.

**BudgetLineRevision** is append-only with no soft-delete and no physical-delete method. Two internal mutators (`SetValidTo`, `SetAmount`) are used only by `SplitRevision`. No dedicated revision endpoints exist — `ListBudgetLines` surfaces only today's effective revision via LATERAL JOIN.

**Existing backend gaps:**
- No `ListBudgetLineRevisions`, `CreateBudgetLineRevision`, `DeleteBudgetLineRevision` handlers/endpoints
- No `UpdateBudgetLineDateRange` handler (date-range change path doesn't exist)
- `RestoreExecutionRecord` only guards `IsClosed`; no date-range intersection check

**Existing frontend gaps:**
- No `BudgetLineRevisionResponse` type; `BudgetLineResponse` has no revisions array
- No revision endpoints in `budgetLines.api.ts`
- No revision state/actions in store
- Router has `budgets/:budgetId/lines` but no child routes
- `BudgetLineModal` edit mode contains Amount Revision section that PR1 must strip

### Affected Areas

**PR1 — Customizations view (frontend):**
- `frontend/src/router/index.ts` — add `lines/:lineId/customizations` child route
- `frontend/src/features/budget-structure/types.ts` — add `BudgetLineRevisionResponse`; extend `BudgetLineResponse` with optional `revisions?`
- `frontend/src/features/budget-structure/api/budgetLines.api.ts` — add `listRevisions`, `createRevision`, `deleteRevision`
- `frontend/src/features/budget-structure/store.ts` — add revision state + actions
- New: `frontend/src/features/budget-structure/views/BudgetLineCustomizationsView.vue`
- `frontend/src/features/budget-structure/components/BudgetLineModal.vue` — strip Amount Revision section from edit mode
- `frontend/src/features/budget-structure/views/BudgetLinesView.vue` — add nav link per row to customizations

**PR2 — Backend range guards + revision CRUD:**
- `SharedKernel/Entities/BudgetLine.cs` — add `UpdateDateRange()` + `DeleteRevision()` domain methods
- New slices: `ListBudgetLineRevisions`, `CreateBudgetLineRevision`, `DeleteBudgetLineRevision`, `UpdateBudgetLineDateRange`
- New endpoints: `GET/POST /api/budgets/:budgetId/lines/:lineId/revisions`, `DELETE .../revisions/:revisionId`, `PATCH .../date-range`

**PR3 — Restore validation:**
- `Features/BudgetExecution/RestoreExecutionRecord/RestoreExecutionRecordHandler.cs` — add BudgetLine load + date-range intersection check (via Period dates, not OperationDate); return `EXECUTION_OUT_OF_DATE_RANGE`

### Recommended Approach

**Approach B — Separate PATCH /date-range endpoint.**
Isolated guard logic, clean command per concern. `SplitRevision` domain method is the entry point for `CreateBudgetLineRevision`. New `BudgetLine.DeleteRevision(revisionId)` domain method handles gapless repair on deletion.

### Risks
1. Gapless repair on delete edge cases (last revision, first in chain)
2. EF tracking for revision removal — explicit `DbSet.Remove` needed
3. PR1 strip ordering: wire customizations route BEFORE stripping modal section
4. PR3 Period load requirement: Period's StartDate/EndDate — not OperationDate
5. PR2 size risk: ~400-line budget concern for 4 new slices
