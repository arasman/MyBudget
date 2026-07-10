# Design: Budget Structure

## Technical Approach

Add 6 entities (Cycle, Period, CategoryGroup, Category, BudgetLine, BudgetLineRevision) + LineType enum to the SharedKernel, with EF Core configurations using soft-delete query filters and cascade rules. Implement 23 VSA slices (19 write + 4 read) under `Features/BudgetStructure/`, following the existing 4-file pattern (Command/Query + Validator + Handler + Endpoint). Write handlers use EF Core via `AppDbContext`; read handlers use Dapper via `ConnectionFactory`. All routes nest under `/api/budgets/{id}/...` so `BudgetAuthorizationHandler` resolves budget context automatically. Deliver via 4 chained PRs to stay within the 400-line review budget.

## Architecture Decisions

### ADR-BS-01: Soft Delete with Global Query Filters

| Option | Tradeoff | Decision |
|--------|----------|----------|
| Physical delete | Simple, but loses audit trail | Rejected |
| `DeletedAt` column + EF global query filter | Automatic filtering, recoverable, slight index overhead | **Chosen** |
| Separate archive table | Complex migrations, dual writes | Rejected |

All 5 structural entities (Cycle, Period, CategoryGroup, Category, BudgetLine) get `DateTimeOffset? DeletedAt`. EF configs add `.HasQueryFilter(e => e.DeletedAt == null)`. Hard-delete slices (DeleteCycle, DeletePeriod, etc.) set `DeletedAt = DateTimeOffset.UtcNow`. BudgetLineRevision has no soft delete (immutable append-only).

### ADR-BS-02: Cascade Strategy

| Parent -> Child | Rule | Rationale |
|-----------------|------|-----------|
| Budget -> Cycle | Restrict (EF) | Budget deletion is not in scope; prevent accidental orphans |
| Cycle -> Period | Cascade (DB + soft) | Deleting a cycle means its periods are meaningless |
| Period -> BudgetLine | Cascade (DB + soft) | Lines belong to a period |
| Budget -> CategoryGroup | Restrict (EF) | Same as Budget -> Cycle |
| CategoryGroup -> Category | Cascade (DB + soft) | Categories are children of groups |
| BudgetLine -> BudgetLineRevision | Cascade (DB hard) | Revisions are owned; no soft delete on revisions |

Soft-delete handlers: when deleting Cycle, handler also sets `DeletedAt` on child Periods and their BudgetLines in the same transaction. Same pattern for CategoryGroup -> Categories.

### ADR-BS-03: SetActiveCycle Atomic Swap

**Choice**: Handler uses an explicit `BeginTransactionAsync` + two sequential `SaveChangesAsync` calls — first deactivate the current cycle, then activate the target. Both saves run inside the same DB transaction, preserving atomicity.

**Why not single SaveChangesAsync**: PostgreSQL's partial unique index `IX_Cycles_BudgetId_IsActive WHERE IsActive=true` fires a constraint violation if EF Core sends the activate UPDATE before the deactivate UPDATE within the same batch. Splitting into two saves with the deactivation first avoids the transient constraint violation while keeping the operation atomic.

**Rejected**: DB trigger (hidden logic), two separate API calls (race condition risk), single `SaveChangesAsync` (causes constraint violation with the partial unique index).

### ADR-BS-04: Reorder via Ordered ID List

**Choice**: `ReorderCategoryGroups` / `ReorderCategories` accept `List<Guid> OrderedIds`. Handler loads all entities for the parent, validates the list contains exactly the same IDs (no additions/removals), then assigns `DisplayOrder = index + 1` from the list order.

**Rejected**: Gap-based ordering (e.g., 10/20/30) adds complexity with no benefit at this scale. Drag-and-drop "move item from position X to Y" requires client-side state the API shouldn't depend on.

### ADR-BS-05: IsClosed Period Guard

**Choice**: A shared private method `EnsurePeriodOpen` in each BudgetLine handler loads `Period` by ID, checks `IsClosed`, returns `Result.Failure("PERIOD_CLOSED")` if true. Endpoint maps this to HTTP 409 Conflict.

**Rejected**: MediatR pipeline behavior (too broad; only 3 slices need it). EF interceptor (wrong layer).

### ADR-BS-06: BudgetLineRevision Auto-Create

**Choice**: `UpdateBudgetLine` handler always creates a new `BudgetLineRevision` row with the updated amount/currency/note. Existing revisions are never modified. The `RevisedAt` timestamp is set to `DateTimeOffset.UtcNow`. `CreateBudgetLine` handler also creates the initial revision.

**Rejected**: Updating the latest revision in place (loses history). Separate "AddRevision" endpoint (unnecessary API surface).

### ADR-BS-07: Read Slices with Dapper

**Choice**: Follow existing `GetCurrentUserHandler` pattern. `ConnectionFactory.CreateConnection()` + raw SQL. For `ListBudgetLines`, join to latest revision using `DISTINCT ON` (PostgreSQL):

```sql
SELECT bl."Id", bl."Name", bl."LineType", bl."IsRecurring",
       bl."CategoryGroupId", bl."CategoryId",
       r."BudgetedAmount", r."Currency", r."RevisedAt", r."Note"
FROM "BudgetLines" bl
LEFT JOIN LATERAL (
    SELECT * FROM "BudgetLineRevisions" r2
    WHERE r2."BudgetLineId" = bl."Id"
    ORDER BY r2."RevisedAt" DESC
    LIMIT 1
) r ON true
WHERE bl."PeriodId" = @PeriodId AND bl."DeletedAt" IS NULL
ORDER BY bl."Name"
```

**Rejected**: EF Core projections (N+1 risk on revisions, harder to optimize). Window functions with `ROW_NUMBER()` (LATERAL JOIN is more readable for single-latest pattern).

### ADR-BS-08: Resource Isolation

**Choice**: Every write handler loads the target entity and verifies it belongs to the budget from the route (`entity.BudgetId == budgetId` or via parent chain). This prevents cross-budget manipulation. Read handlers filter by `BudgetId` in the SQL WHERE clause.

For nested entities (Period, Category, BudgetLine), the handler joins up the ownership chain:
- Period: load Period, load its Cycle, check `Cycle.BudgetId == routeBudgetId`
- Category: load Category, load its CategoryGroup, check `CategoryGroup.BudgetId == routeBudgetId`
- BudgetLine: load BudgetLine, load its Period -> Cycle, check `Cycle.BudgetId == routeBudgetId`

### ADR-BS-09: i18n Deferred

**Choice**: Use hardcoded error code strings (e.g., `"PERIOD_CLOSED"`, `"CYCLE_NOT_FOUND"`) matching the auth feature pattern. No `.resx` files for now.

**Rationale**: The auth feature shipped without `.resx` files. Adding them only for budget-structure creates inconsistency. `.resx` will be added in a future cross-cutting change when the frontend localization layer is mature enough to consume server-side translated messages.

## Entity Signatures

```csharp
public sealed class Cycle : BaseEntity
{
    public Guid BudgetId { get; private set; }
    public string Name { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    // Nav: Budget? Budget, ICollection<Period> Periods
}

public sealed class Period : BaseEntity
{
    public Guid CycleId { get; private set; }
    public string Name { get; private set; }
    public int PeriodNumber { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public bool IsClosed { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    // Nav: Cycle? Cycle, ICollection<BudgetLine> BudgetLines
}

public sealed class CategoryGroup : BaseEntity
{
    public Guid BudgetId { get; private set; }
    public string Name { get; private set; }
    public int DisplayOrder { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    // Nav: Budget? Budget, ICollection<Category> Categories
}

public sealed class Category : BaseEntity
{
    public Guid CategoryGroupId { get; private set; }
    public string Name { get; private set; }
    public int DisplayOrder { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    // Nav: CategoryGroup? CategoryGroup
}

public sealed class BudgetLine : BaseEntity
{
    public Guid PeriodId { get; private set; }
    public Guid CategoryGroupId { get; private set; }       // Required
    public Guid? CategoryId { get; private set; }            // Optional
    public string Name { get; private set; }
    public LineType LineType { get; private set; }
    public bool IsRecurring { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    // Nav: Period? Period, CategoryGroup? CategoryGroup, Category? Category,
    //      ICollection<BudgetLineRevision> Revisions
}

public sealed class BudgetLineRevision : BaseEntity
{
    public Guid BudgetLineId { get; private set; }
    public decimal BudgetedAmount { get; private set; }
    public string Currency { get; private set; }             // "GTQ" | "USD"
    public DateTimeOffset RevisedAt { get; private set; }
    public string? Note { get; private set; }
    // Nav: BudgetLine? BudgetLine
}

public enum LineType { Expense = 0, LongTermSavings = 1, PreventiveSavings = 2 }
```

## EF Configuration Summary

| Entity | Table | Key Indexes | Query Filter | Cascade From Parent |
|--------|-------|-------------|--------------|---------------------|
| Cycle | "Cycles" | IX_Cycles_BudgetId, IX_Cycles_BudgetId_IsActive (filtered: IsActive=true, unique) | `DeletedAt == null` | Restrict from Budget |
| Period | "Periods" | IX_Periods_CycleId | `DeletedAt == null` | Cascade from Cycle |
| CategoryGroup | "CategoryGroups" | IX_CategoryGroups_BudgetId, IX_CategoryGroups_BudgetId_Name (unique) | `DeletedAt == null` | Restrict from Budget |
| Category | "Categories" | IX_Categories_CategoryGroupId, IX_Categories_CategoryGroupId_Name (unique) | `DeletedAt == null` | Cascade from CategoryGroup |
| BudgetLine | "BudgetLines" | IX_BudgetLines_PeriodId, IX_BudgetLines_CategoryGroupId | `DeletedAt == null` | Cascade from Period |
| BudgetLineRevision | "BudgetLineRevisions" | IX_BudgetLineRevisions_BudgetLineId_RevisedAt (desc) | None | Cascade from BudgetLine |

All `Name` properties: `HasMaxLength(200)`. LineType stored as `int`. Currency: `HasMaxLength(3)`.

## Slice File Layout

```
Features/BudgetStructure/
  CreateCycle/         (Command, Validator, Handler, Endpoint)
  UpdateCycle/
  DeleteCycle/
  SetActiveCycle/
  CreatePeriod/
  UpdatePeriod/
  SetPeriodStatus/
  DeletePeriod/
  CreateCategoryGroup/
  UpdateCategoryGroup/
  DeleteCategoryGroup/
  ReorderCategoryGroups/
  CreateCategory/
  UpdateCategory/
  DeleteCategory/
  ReorderCategories/
  CreateBudgetLine/
  UpdateBudgetLine/
  DeleteBudgetLine/
  ListCycles/          (Query, Handler, Endpoint — no validator)
  GetCycleDetail/
  ListCategoryGroups/
  ListBudgetLines/
```

## Handler Pseudocode

### SetActiveCycle

```
1. Load targetCycle = db.Cycles.First(c => c.Id == cycleId && c.BudgetId == budgetId)
2. If null -> 404
3. Load currentActive = db.Cycles.FirstOrDefault(c => c.BudgetId == budgetId && c.IsActive)
4. If currentActive != null -> currentActive.Deactivate()
5. targetCycle.Activate()
6. await db.SaveChangesAsync()   // single transaction
7. Return 204
```

### ReorderCategoryGroups

```
1. Load allGroups = db.CategoryGroups.Where(g => g.BudgetId == budgetId).ToList()
2. If request.OrderedIds.Count != allGroups.Count -> 400 "ORDER_LIST_MISMATCH"
3. If request.OrderedIds has duplicates or IDs not in allGroups -> 400
4. For i in 0..OrderedIds.Count-1:
     group = allGroups.First(g => g.Id == OrderedIds[i])
     group.SetDisplayOrder(i + 1)
5. await db.SaveChangesAsync()
6. Return 204
```

### ReorderCategories (same pattern, scoped to CategoryGroupId)

## Data Flow

```
Client ──POST──> /api/budgets/{id}/periods/{periodId}/lines
                    │
              BudgetAuthorizationHandler (budget:admin policy)
                    │
              ValidationBehaviour (FluentValidation)
                    │
              CreateBudgetLineHandler
                    ├── Load Period -> Cycle -> verify BudgetId
                    ├── Check Period.IsClosed -> 409 if true
                    ├── Create BudgetLine entity
                    ├── Create initial BudgetLineRevision
                    └── db.SaveChangesAsync()
                    │
              ──201──> { id, name, lineType, ... }
```

## PR Delivery Plan

| PR | Scope | Estimated Lines | Target |
|----|-------|----------------|--------|
| PR1 | 6 entities + LineType enum + 6 EF configs + AppDbContext DbSets + migration | ~350 | `feat/budget-structure` |
| PR2 | 11 write slices: Cycle (Create/Update/Delete/SetActive) + Period (Create/Update/SetStatus/Delete) + CategoryGroup (Create/Update/Delete) | ~380 | PR1 branch |
| PR3 | 8 write slices: ReorderCategoryGroups + Category (Create/Update/Delete) + ReorderCategories + BudgetLine (Create/Update/Delete) | ~350 | PR2 branch |
| PR4 | 4 read slices (ListCycles, GetCycleDetail, ListCategoryGroups, ListBudgetLines) + all tests | ~400 | PR3 branch |

Each PR is independently deployable and testable. PR1 adds tables with no runtime behavior. PR2-PR3 add write endpoints. PR4 adds reads and full test suite.

## Testing Strategy

| Layer | What | Approach |
|-------|------|----------|
| Unit | All 19 validators (FluentValidation) | Isolated `AbstractValidator` tests, no DB. ~38 tests (happy + error per validator). |
| Unit | Handler logic: SetActiveCycle swap, Reorder validation, IsClosed guard, revision auto-create | Mock `AppDbContext` with in-memory or use EF InMemory provider. ~15 tests. |
| Integration | All 23 endpoints: happy path + auth (401/403) + validation (400) + business rules (409 for IsClosed, 404 for not found, 409 for overlap) | `IntegrationTestBase` + `IntegrationTestFactory` (Testcontainers Postgres). ~60 tests. |
| Integration | Resource isolation: verify cross-budget access returns 404/403 | Included in endpoint tests above. |

Strict TDD: write test first, see it fail, implement, see it pass.

## Migration

Single migration `AddBudgetStructureTables`. Must run `dotnet tool update --global dotnet-ef` first to resolve version mismatch (tools 9.x vs runtime 10.x). Migration adds 6 tables, indexes, and foreign keys. No data migration needed (all tables are new). Rollback: `dotnet ef migrations remove` or revert the migration file.

## Open Questions

- None. All design decisions are resolved based on the proposal and exploration artifacts.
