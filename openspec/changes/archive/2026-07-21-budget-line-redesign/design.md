# Design: Budget Line Redesign

## Technical Approach

Promote BudgetLine from Period-scoped to Budget-scoped entity with date-range validity (`StartDate`/`EndDate`) and a gapless append-only revision system (`ValidFrom`/`ValidTo`). The change touches entities, EF configurations, 12 backend slices, the frontend API/store/components layer, and all BudgetLine-related tests. Dev-only data wipe migration; no backward compatibility.

Strategy: entity-first, then EF migration (wipe), then backend slices (structure + execution), then frontend, then tests. Chained PRs to stay within 400-line review budget.

## Architecture Decisions

| # | Decision | Choice | Rejected | Rationale |
|---|----------|--------|----------|-----------|
| 1 | Revision validity model | `ValidFrom`/`ValidTo` DateOnly pair per revision; gapless invariant enforced by `SplitRevision()` domain method | Append-only with `RevisedAt` timestamp (current) | Timestamp model cannot express "this amount is valid for March-June only" -- date ranges are required for period-amount resolution |
| 2 | Uniqueness constraint | `UNIQUE(BudgetId, Name)` -- no filtered index, includes soft-deleted | `UNIQUE(BudgetId, Name) WHERE DeletedAt IS NULL` (filtered) | SQLite does not support partial/filtered unique indexes; simpler enforcement; prevents name reuse even after soft-delete |
| 3 | Concurrency guard | `ValidFrom >= today` check in `SplitRevision()` -- no distributed lock | Pessimistic lock on BudgetLine row | Dev scale; single-user budget; retroactive edit prevention is the real invariant, not concurrent writes |
| 4 | Period-amount resolution | Single Dapper query: `ValidFrom <= periodStart AND (ValidTo IS NULL OR ValidTo >= periodStart)` | Load all revisions client-side and filter | DB-side resolution is O(1) per line; avoids shipping revision history to frontend |
| 5 | Active-for-period rule | `BudgetLine.StartDate <= Period.StartDate AND (EndDate IS NULL OR EndDate >= Period.StartDate)` | `EndDate >= Period.EndDate` (full coverage) | Partial coverage is valid -- a line starting mid-period should still appear; `StartDate` comparison is the anchor |
| 6 | UpdateBudgetLine split semantics | Amount change requires explicit `validFrom` from frontend; handler delegates to `SplitRevision()` | Backend infers `validFrom = today` | Explicit date gives user control over when the new amount takes effect; frontend passes it from a date picker |
| 7 | Migration strategy | Wipe: delete all migrations, `dotnet ef database drop`, fresh `InitialCreate` | Additive migration preserving data | Dev-only; no production data to preserve; clean schema is simpler |
| 8 | Frontend matrix loading | `loadLines(budgetId)` returns ALL lines; client-side active-for-period filter per column | Server-side filter with `?periodStartDate=` param | Budget typically has <100 lines; single fetch avoids N+1 per period column in matrix |

## Data Flow

```
CREATE flow:
  Frontend ──POST /api/budgets/{id}/lines──> CreateBudgetLineEndpoint
    ──> CreateBudgetLineHandler
      ├─ validate uniqueness (BudgetId, Name)
      ├─ BudgetLine.Create(budgetId, ..., startDate, endDate)
      ├─ BudgetLineRevision.Create(budgetId, lineId, startDate, endDate, amount, currencyId)
      └─ SaveChanges ──> Result<Guid>

SPLIT REVISION flow:
  Frontend ──PUT /api/budgets/{id}/lines/{lid}──> UpdateBudgetLineEndpoint
    ──> UpdateBudgetLineHandler
      ├─ load line + revisions
      ├─ update metadata (name, type, category)
      ├─ if amount changed: line.SplitRevision(validFrom, validTo, amount, currencyId)
      │    ├─ find enclosing revision
      │    ├─ truncate enclosing.ValidTo = validFrom - 1 day
      │    ├─ add new revision [validFrom, validTo]
      │    └─ if tail: add tail revision [validTo + 1 day, originalValidTo]
      └─ SaveChanges

PERIOD RESOLUTION flow:
  ListPeriodExecutionTotals
    ├─ JOIN BudgetLines ON date-range intersection with Period
    ├─ LATERAL JOIN BudgetLineRevisions ON ValidFrom <= Period.StartDate
    └─ return budgeted + executed totals per line
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `SharedKernel/Entities/BudgetLine.cs` | Modify | Remove `PeriodId`, `IsRecurring`, `Period` nav; add `StartDate` (DateOnly), `EndDate` (DateOnly?); add `SplitRevision()` domain method; update `Create()`, `Update()` signatures |
| `SharedKernel/Entities/BudgetLineRevision.cs` | Modify | Remove `RevisedAt`; add `ValidFrom` (DateOnly), `ValidTo` (DateOnly?); add `SetValidTo()` method; update `Create()` signature |
| `SharedKernel/Entities/Period.cs` | Modify | Remove `ICollection<BudgetLine> BudgetLines` navigation |
| `Persistence/Configurations/BudgetLineConfiguration.cs` | Modify | Remove PeriodId FK/index/cascade; add StartDate/EndDate props; add `UX_BudgetLines_BudgetId_Name` unique index |
| `Persistence/Configurations/BudgetLineRevisionConfiguration.cs` | Modify | Remove `RevisedAt` index; add `ValidFrom`/`ValidTo` props; add `IX_BudgetLineRevisions_BudgetLineId_ValidFrom` |
| `Features/BudgetStructure/CreateBudgetLine/*` | Modify | 4 files: remove PeriodId/IsRecurring; add StartDate/EndDate; initial revision uses `[StartDate, EndDate]`; uniqueness = `(BudgetId, Name)` |
| `Features/BudgetStructure/UpdateBudgetLine/*` | Modify | 4 files: remove PeriodId/IsRecurring; add optional `ValidFrom`/`ValidTo` for revision split; delegate to `SplitRevision()` when amount changes |
| `Features/BudgetStructure/DeleteBudgetLine/*` | Modify | 3 files: remove PeriodId from command/route |
| `Features/BudgetStructure/RestoreBudgetLine/*` | Modify | 3 files: remove PeriodId from command/route |
| `Features/BudgetStructure/ListBudgetLines/*` | Modify | 3 files: query by BudgetId; remove PeriodId; response adds `startDate`/`endDate`, removes `isRecurring`/`revisedAt` |
| `Features/BudgetStructure/ReorderBudgetLines/*` | Modify | 4 files: scope by BudgetId instead of PeriodId |
| `Features/BudgetStructure/DeletePeriod/DeletePeriodHandler.cs` | Modify | Remove BudgetLine cascade soft-delete |
| `Features/BudgetStructure/DeleteCycle/DeleteCycleHandler.cs` | Modify | Remove BudgetLine cascade via period IDs |
| `Features/BudgetStructure/RestoreCycle/RestoreCycleHandler.cs` | Modify | Remove BudgetLine restore via period IDs |
| `Features/BudgetExecution/CreateExecutionRecord/CreateExecutionRecordHandler.cs` | Modify | Replace `line.PeriodId == cmd.PeriodId` guard with date-range intersection check: `line.StartDate <= period.StartDate AND (line.EndDate IS NULL OR line.EndDate >= period.StartDate)` |
| `Features/BudgetExecution/ListPeriodExecutionTotals/ListPeriodExecutionTotalsHandler.cs` | Modify | Replace `bl."PeriodId" = @PeriodId` with date-range JOIN; add LATERAL JOIN for revision amount resolution by period start date |
| `Features/BudgetExecution/ListExecutionRecords/ListExecutionRecordsHandler.cs` | Modify | Remove Period JOIN; verify BudgetLine via `bl."BudgetId"` directly |
| `frontend/src/features/budget-structure/types.ts` | Modify | `BudgetLineResponse`: remove `isRecurring`/`revisedAt`; add `startDate`/`endDate`. Payloads: same field changes |
| `frontend/src/features/budget-structure/api/budgetLines.api.ts` | Modify | Base URL → `/api/budgets/${budgetId}/lines`; remove `periodId` from all function signatures |
| `frontend/src/features/budget-structure/store.ts` | Modify | Remove `periodId` from all action signatures; `loadLines(budgetId)` |
| `frontend/src/features/budget-structure/views/BudgetLinesView.vue` | Modify | Remove `periodId` route param; add date range fields; remove `isRecurring` |
| `frontend/src/features/budget-structure/components/BudgetLineModal.vue` | Modify | Remove `isRecurring` checkbox; add `startDate`/`endDate` date inputs |
| `frontend/src/features/budget-structure/components/BudgetLineRow.vue` | Modify | Remove `isRecurring` column; add date display |
| `frontend/src/features/budget-execution/views/BudgetMatrixView.vue` | Modify | Remove `periodId` from `createLine`; add client-side active-for-period filter for cell enable/disable |
| `frontend/src/features/budget-execution/store.ts` | Modify | Remove `periodId` from `loadLines` call |
| All EF migrations | Delete | Wipe all; regenerate fresh `InitialCreate` |

## Interfaces / Contracts

```csharp
// BudgetLine.SplitRevision — domain method
public void SplitRevision(
    DateOnly newValidFrom,
    DateOnly? newValidTo,
    decimal amount,
    Guid currencyId,
    string? note = null)
{
    if (newValidFrom < DateOnly.FromDateTime(DateTime.UtcNow))
        throw new InvalidOperationException("Cannot create retroactive revisions.");

    var enclosing = Revisions.FirstOrDefault(r =>
        r.ValidFrom <= newValidFrom &&
        (r.ValidTo is null || r.ValidTo >= newValidFrom))
        ?? throw new InvalidOperationException("No enclosing revision found.");

    var originalValidTo = enclosing.ValidTo;

    // Truncate enclosing
    enclosing.SetValidTo(newValidFrom.AddDays(-1));

    // New revision
    Revisions.Add(BudgetLineRevision.Create(
        BudgetId, Id, amount, currencyId, newValidFrom, newValidTo, note));

    // Tail segment if needed
    if (newValidTo.HasValue && (originalValidTo is null || originalValidTo > newValidTo))
    {
        Revisions.Add(BudgetLineRevision.Create(
            BudgetId, Id, enclosing.BudgetedAmount, enclosing.CurrencyId,
            newValidTo.Value.AddDays(1), originalValidTo));
    }
}

// BudgetLineRevision.SetValidTo — mutable only for split operations
internal void SetValidTo(DateOnly validTo) => ValidTo = validTo;

// BudgetLineRevision.Create — new signature
public static BudgetLineRevision Create(
    Guid budgetId, Guid budgetLineId, decimal amount, Guid currencyId,
    DateOnly validFrom, DateOnly? validTo, string? note = null);
```

```typescript
// Frontend types (post-redesign)
interface BudgetLineResponse {
  id: string; name: string; lineType: LineType;
  categoryGroupId: string; categoryId?: string;
  startDate: DateString; endDate: DateString | null;
  budgetedAmount?: number; currencyId?: string;
  currencyCode?: string; currencySymbol?: string;
  note?: string; deletedAt?: string | null;
}

interface CreateBudgetLinePayload {
  name: string; lineType: LineType;
  categoryGroupId?: string; categoryId?: string;
  startDate: DateString; endDate?: DateString;
  budgetedAmount?: number; currencyId?: string; note?: string;
}

interface UpdateBudgetLinePayload {
  name: string; lineType: LineType;
  categoryGroupId?: string; categoryId?: string;
  budgetedAmount?: number; currencyId?: string; note?: string;
  validFrom?: DateString; validTo?: DateString; // for revision split
}
```

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| Unit | `SplitRevision()` domain method: single split, tail generation, null ValidTo, retroactive rejection, no enclosing revision error | xUnit + Shouldly; in-memory entity construction; highest priority |
| Unit | `BudgetLine.Create()` / `BudgetLineRevision.Create()` new signatures | xUnit + Shouldly; verify initial revision covers `[StartDate, EndDate]` |
| Unit | All 6 BudgetStructure handler tests: rewrite for new signatures (no PeriodId) | xUnit + SQLite in-memory + Shouldly |
| Unit | `CreateExecutionRecordHandler`: date-range intersection guard | xUnit + SQLite in-memory |
| Integration | `ListBudgetLines`: BudgetId-scoped query, revision resolution | WebApplicationFactory + Shouldly |
| Integration | `ListPeriodExecutionTotals`: date-range JOIN, multi-period fixtures | WebApplicationFactory; fixtures with lines spanning 1, 2, N periods |
| Integration | `CreateBudgetLine`: uniqueness `(BudgetId, Name)` including soft-deleted | WebApplicationFactory |
| Integration | Cascade: Period delete does NOT cascade to BudgetLines | WebApplicationFactory |
| Frontend | BudgetLineModal: date inputs render, validation (`startDate < endDate`) | Vitest + @testing-library/vue |
| Frontend | Matrix cell enable/disable based on active-for-period rule | Vitest; mock store with lines that partially cover periods |

### Tests that break (structural -- must rewrite):
- All 15+ test files listed in exploration; every test using `PeriodId` in BudgetLine commands, URLs, or seed data.

## Migration / Rollout

**Dev wipe migration**: delete all files in `Migrations/` folder, run `dotnet ef database drop`, then `dotnet ef migrations add InitialCreate`, `dotnet ef database update`. No backward migration needed. No feature flags.

## Implementation Order (Chained PRs)

| PR | Scope | Est. Lines | Dependencies |
|----|-------|-----------|--------------|
| PR1 | Entity changes (`BudgetLine`, `BudgetLineRevision`, `Period`), EF configs, migration wipe, `SplitRevision()` domain method + unit tests | ~300 | None |
| PR2 | Backend slices: 6 BudgetStructure + 3 BudgetExecution + 3 cascade handlers | ~350 | PR1 |
| PR3 | Frontend: types, API layer, store, components (BudgetLinesView, BudgetLineModal, BudgetLineRow, BudgetMatrixView) | ~300 | PR2 |
| PR4 | Integration tests + remaining unit test rewrites | ~350 | PR2 (can parallel with PR3) |

All PRs target `feat/budget-line-redesign` branch. PR1 targets `feat/budget-line-redesign` from `main`; subsequent PRs chain from previous.

## Open Questions

- None. All design decisions settled in proposal.
