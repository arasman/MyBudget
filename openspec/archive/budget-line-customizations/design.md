# Design: Budget Line Customizations

## Technical Approach

Approach B from proposal: each concern gets its own VSA slice with isolated command/handler. Domain logic lives in `BudgetLine` aggregate (`UpdateDateRange`, `DeleteRevision`). Concurrency via PostgreSQL `xmin` shadow property. Audit piggybacks on existing `AppDbContext.SaveChangesAsync` interceptor -- domain mutations to `BudgetLine` and physical deletes of `BudgetLineRevision` are already captured by the `IAuditableEntity` + `ChangeTracker` pattern.

Three PRs: PR1 (frontend customizations view), PR2 (backend slices + domain methods + concurrency), PR3 (restore guard).

## Architecture Decisions

| Decision | Choice | Rejected | Rationale |
|---|---|---|---|
| Revision delete strategy | Physical delete via `_db.BudgetLineRevisions.Remove()` | Soft-delete | Revisions are append-only by design; soft-delete adds query filter complexity. Physical delete with gapless repair keeps the chain clean. |
| Audit for physical delete | Explicit `AuditLog.Create()` in handler BEFORE `Remove()` | Rely on `SaveChangesAsync` interceptor | The interceptor captures `EntityState.Deleted` but `DbSet.Remove` on a nav-collection entity may not always be tracked as Deleted before `SaveChanges`. Explicit write is safer and gives control over `Action` string. |
| Concurrency token | `xmin` shadow property in EF config | `RowVersion` byte[] column | `xmin` is a PostgreSQL system column -- no migration needed, no schema change. SQLite tests use a `Version` shadow property fallback via conditional config. |
| Date-range update scope | Standalone PATCH endpoint | Extend PUT `/lines/:lineId` | PUT already mixes metadata + optional revision split. Adding date-range guards there violates SRP and makes error handling ambiguous. |
| Frontend revision state | Separate `revisions` ref in store, loaded on-demand per line | Nested in `BudgetLineResponse` | Revisions are only needed in the customizations view -- loading them with every line list call wastes bandwidth. |

## Data Flow

### DeleteRevision Gapless Repair

```
Handler loads BudgetLine + Revisions (tracked)
  |
  v
line.DeleteRevision(revisionId)
  |
  +--> Find target revision in Revisions collection
  +--> If no predecessor (earliest ValidFrom) --> throw CANNOT_DELETE_ORIGINAL_REVISION
  +--> If execution records exist for target --> throw REVISION_HAS_ACTIVE_EXECUTIONS
  +--> Find predecessor (max ValidFrom < target.ValidFrom)
  +--> predecessor.SetValidTo(target.ValidTo)   // extend to cover gap
  +--> Revisions.Remove(target)                 // nav collection removal
  |
Handler: explicit _db.BudgetLineRevisions.Remove(target) // EF tracking gotcha
Handler: write AuditLog.Create("BudgetLineRevision", target.Id, "BudgetLineRevisionDeleted", ...)
Handler: _db.SaveChangesAsync()
```

### UpdateDateRange Guards

```
Handler loads BudgetLine + Revisions (tracked)
  |
  v
line.UpdateDateRange(newStart, newEnd)
  |
  +--> Check: any revision.ValidFrom < newStart? --> RANGE_WOULD_ORPHAN_REVISION
  +--> Check: any revision with ValidTo > newEnd (when newEnd not null)? --> RANGE_WOULD_ORPHAN_REVISION
  +--> StartDate = newStart; EndDate = newEnd
  |
Handler: check ExecutionRecords outside new range --> RANGE_WOULD_ORPHAN_EXECUTION
Handler: _db.SaveChangesAsync() (interceptor writes "Updated" audit automatically)
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `SharedKernel/Entities/BudgetLine.cs` | Modify | Add `UpdateDateRange()`, `DeleteRevision()` |
| `SharedKernel/Persistence/Configurations/BudgetLineConfiguration.cs` | Modify | Add `xmin` concurrency token (`builder.UseXminAsConcurrencyToken()`) |
| `Features/BudgetStructure/ListBudgetLineRevisions/` (4 files) | Create | Query + Handler (Dapper) + Endpoint. No validator needed (route params only). |
| `Features/BudgetStructure/CreateBudgetLineRevision/` (4 files) | Create | Command + Handler + Validator + Endpoint. Delegates to `SplitRevision`. |
| `Features/BudgetStructure/DeleteBudgetLineRevision/` (4 files) | Create | Command + Handler + Validator + Endpoint. Delegates to `DeleteRevision`. |
| `Features/BudgetStructure/UpdateBudgetLineDateRange/` (4 files) | Create | Command + Handler + Validator + Endpoint. Delegates to `UpdateDateRange`. |
| `Features/BudgetExecution/RestoreExecutionRecord/RestoreExecutionRecordHandler.cs` | Modify | Add BudgetLine load + date-range intersection guard after `IsClosed` check. |
| `frontend/src/features/budget-structure/types.ts` | Modify | Add `BudgetLineRevisionResponse` interface |
| `frontend/src/features/budget-structure/api/budgetLines.api.ts` | Modify | Add `listRevisions`, `createRevision`, `deleteRevision` functions |
| `frontend/src/features/budget-structure/store.ts` | Modify | Add `revisions` ref, `fetchRevisions`, `createRevision`, `deleteRevision` actions |
| `frontend/src/features/budget-structure/views/BudgetLineCustomizationsView.vue` | Create | Revision list with create/delete, nav back to lines |
| `frontend/src/features/budget-structure/views/BudgetLinesView.vue` | Modify | Add nav link per row to customizations route |
| `frontend/src/features/budget-structure/components/BudgetLineModal.vue` | Modify | Strip Amount Revision section from edit mode |
| `frontend/src/router/index.ts` | Modify | Add `lines/:lineId/customizations` child route |

## Interfaces / Contracts

```csharp
// New domain methods on BudgetLine
public void UpdateDateRange(DateOnly startDate, DateOnly? endDate);
public BudgetLineRevision DeleteRevision(Guid revisionId); // returns removed entity for handler

// New slice commands
record ListBudgetLineRevisionsQuery(Guid BudgetId, Guid LineId) : IRequest<Result<List<RevisionDto>>>;
record CreateBudgetLineRevisionCommand(Guid BudgetId, Guid LineId,
    DateOnly ValidFrom, DateOnly? ValidTo, decimal Amount, Guid? CurrencyId) : IRequest<Result<Guid>>;
record DeleteBudgetLineRevisionCommand(Guid BudgetId, Guid LineId, Guid RevisionId) : IRequest<Result<Guid>>;
record UpdateBudgetLineDateRangeCommand(Guid BudgetId, Guid LineId,
    DateOnly StartDate, DateOnly? EndDate) : IRequest<Result<Guid>>;
```

```typescript
// Frontend
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

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| Unit | `DeleteRevision` gapless repair (predecessor extends, original blocked, execution guard) | `BudgetLineEntityTests.cs` -- create line with revisions, call `DeleteRevision`, assert chain integrity |
| Unit | `UpdateDateRange` guards (orphan revision, orphan execution) | `BudgetLineEntityTests.cs` -- set up revisions outside proposed range, assert domain error |
| Unit | Validators for all 4 new slices | xUnit + FluentValidation `TestValidate` |
| Integration | Full CRUD endpoints (list/create/delete revisions, PATCH date-range) | `WebApplicationFactory` + SQLite, assert HTTP status + response body |
| Integration | Concurrency conflict returns 409 | Two concurrent updates to same `BudgetLine`, second gets `DbUpdateConcurrencyException` |
| Integration | `RestoreExecutionRecord` date-range guard | Create line with narrow range, execution outside, attempt restore, assert `EXECUTION_OUT_OF_DATE_RANGE` |
| Frontend | `BudgetLineCustomizationsView` renders revision list, handles create/delete | Vitest + @testing-library/vue with mocked API |
| Frontend | `BudgetLineModal` edit mode no longer shows Amount Revision section | Vitest snapshot/query assertion |

## Migration / Rollout

No EF migration required. `xmin` is a PostgreSQL system column accessed via `UseXminAsConcurrencyToken()` in EF config. SQLite test provider ignores `xmin`; tests that need concurrency use a conditional `Version` shadow property or skip the concurrency assertion.

PR order: PR2 (backend) first, then PR1 (frontend consumes new endpoints), then PR3 (restore guard). PR1 can start in parallel with PR2 since it can mock the API calls.

## Open Questions

- [x] Audit write path: existing `SaveChangesAsync` interceptor handles `Updated` action on `BudgetLine` automatically. Physical revision delete needs explicit `AuditLog.Create()` in the handler. Resolved -- design above covers both.
- [ ] SQLite concurrency: `UseXminAsConcurrencyToken()` is PostgreSQL-specific. Integration tests on SQLite may need a conditional branch or accept that concurrency is not tested there. Verify during apply.
