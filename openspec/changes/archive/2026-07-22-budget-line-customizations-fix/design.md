# Design: Budget Line Customizations

## Technical Approach

Approach B: each concern gets its own VSA slice. Domain logic in `BudgetLine` aggregate (`UpdateDateRange`, `DeleteRevision`). Concurrency via PostgreSQL `xmin` shadow property. Audit piggybacks on existing `AppDbContext.SaveChangesAsync` interceptor — explicit `AuditLog.Create()` in handler for physical revision delete.

## Architecture Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Revision delete | Physical delete via `_db.BudgetLineRevisions.Remove()` | Revisions are append-only; soft-delete adds query filter complexity |
| Audit for physical delete | Explicit `AuditLog.Create()` in handler BEFORE `Remove()` | Interceptor may not capture `EntityState.Deleted` for nav-collection entities reliably |
| Concurrency token | `xmin` shadow property | PostgreSQL system column — no migration needed; SQLite tests use conditional `Version` shadow property fallback |
| Date-range update scope | Standalone PATCH endpoint | PUT `/lines/:lineId` already mixes concerns; adding guards there violates SRP |
| Frontend revision state | Separate `revisions` ref in store, loaded on-demand | Revisions only needed in customizations view — avoid loading in every line list call |

## Key Contracts

```csharp
// New domain methods on BudgetLine
public void UpdateDateRange(DateOnly startDate, DateOnly? endDate);
public BudgetLineRevision DeleteRevision(Guid revisionId);

// New slice commands
record ListBudgetLineRevisionsQuery(Guid BudgetId, Guid LineId) : IRequest<Result<List<RevisionDto>>>;
record CreateBudgetLineRevisionCommand(Guid BudgetId, Guid LineId, DateOnly ValidFrom, DateOnly? ValidTo, decimal Amount, Guid? CurrencyId) : IRequest<Result<Guid>>;
record DeleteBudgetLineRevisionCommand(Guid BudgetId, Guid LineId, Guid RevisionId) : IRequest<Result<Guid>>;
record UpdateBudgetLineDateRangeCommand(Guid BudgetId, Guid LineId, DateOnly StartDate, DateOnly? EndDate) : IRequest<Result<Guid>>;
```

```typescript
interface BudgetLineRevisionResponse {
  id: string
  budgetedAmount: number
  currencyId: string
  currencyCode?: string
  currencySymbol?: string
  validFrom: DateString
  validTo: DateString | null
  note?: string
}
```

## File Changes

| File | Action |
|---|---|
| `SharedKernel/Entities/BudgetLine.cs` | Modify — add `UpdateDateRange()`, `DeleteRevision()` |
| `SharedKernel/Persistence/Configurations/BudgetLineConfiguration.cs` | Modify — add `UseXminAsConcurrencyToken()` |
| `Features/BudgetStructure/ListBudgetLineRevisions/` (4 files) | Create |
| `Features/BudgetStructure/CreateBudgetLineRevision/` (4 files) | Create |
| `Features/BudgetStructure/DeleteBudgetLineRevision/` (4 files) | Create |
| `Features/BudgetStructure/UpdateBudgetLineDateRange/` (4 files) | Create |
| `Features/BudgetExecution/RestoreExecutionRecord/RestoreExecutionRecordHandler.cs` | Modify — add date-range guard |
| `frontend/src/features/budget-structure/types.ts` | Modify |
| `frontend/src/features/budget-structure/api/budgetLines.api.ts` | Modify |
| `frontend/src/features/budget-structure/store.ts` | Modify |
| `frontend/src/features/budget-structure/views/BudgetLineCustomizationsView.vue` | Create |
| `frontend/src/features/budget-structure/views/BudgetLinesView.vue` | Modify — add nav link |
| `frontend/src/features/budget-structure/components/BudgetLineModal.vue` | Modify — strip Amount Revision section |
| `frontend/src/router/index.ts` | Modify — add child route |

## Migration / Rollout

No EF migration required. `xmin` is a PostgreSQL system column. PR order: PR1 (frontend, can mock API) can be developed in parallel with PR2 (backend). PR3 depends on PR2.
