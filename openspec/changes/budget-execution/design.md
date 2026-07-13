# Design: Budget Execution

## Technical Approach

New `Features/BudgetExecution/` folder with 6 vertical slices following established patterns. `ExecutionRecord` entity extends `BaseEntity + IAuditableEntity` with `EntryType` enum. Write slices (Create, Update, Delete, Restore) use EF Core with IsClosed guard. Read slices (List, ListPeriodExecutionTotals) use Dapper. BudgetLine soft-delete cascades to ExecutionRecords; restore handlers gain `IncludeExecutionRecords` activation. Delivery: 2 chained PRs on `feat/budget-execution`.

## Architecture Decisions

| # | Decision | Choice | Rejected | Rationale |
|---|----------|--------|----------|-----------|
| 1 | EntryType storage | `int` enum conversion (Expense=1, CreditNote=2, DebitNote=3) | String | Matches `LineType` pattern in `BudgetLineConfiguration`. Int is compact, indexed, migration-safe. |
| 2 | ExchangeRate pair rule | Validator-level `Must()` check (CurrencyId != Cycle.DefaultCurrencyId requires ExchangeRate) | Handler-level check | Mirrors `CYC_PAIR_INCOMPLETE` pattern. Fail-fast before handler. Handler loads Cycle to resolve DefaultCurrencyId. |
| 3 | PeriodId/BudgetId denormalization | Store both on `ExecutionRecord`; validate route PeriodId == BudgetLine.PeriodId in handler | Derive at query time via JOINs | Fast RBAC without joins; handler validates consistency. Matches `BudgetLine.BudgetId` pattern. |
| 4 | AccountId/PaymentMethodId FK | No FK constraint, nullable Guid columns | Add FK to placeholder entities | Deferred to `current-situation`. No entity exists yet. Forward-compat only. |
| 5 | Cascade soft-delete | Handler-level: `DeleteBudgetLine` loads ExecutionRecords and calls `SoftDelete()` on each | DB trigger / EF cascade | Consistent with existing handler-level cascade pattern (RestoreCycle cascading Periods+Lines). Explicit, testable. |
| 6 | Totals aggregation | Single Dapper query with `UNION ALL` returning line-level + category-level rows, differentiated by a `GroupLevel` discriminator | Two separate queries | One roundtrip. Handler splits rows by discriminator into two response lists. |
| 7 | Amount semantics | Always positive in DB; totals formula: `SUM(Expense+DebitNote) - SUM(CreditNote)` | Signed amounts | Positive-only prevents user confusion, EntryType drives semantics. |

## Data Flow

### Create ExecutionRecord
```
POST /api/budgets/{id}/periods/{periodId}/lines/{lineId}/executions
  |
  CreateExecutionRecordEndpoint -> Command -> Handler
  |  1. Load BudgetLine.Include(Period.Cycle) -> verify BudgetId + PeriodId match
  |  2. IsClosed guard -> PERIOD_CLOSED 409
  |  3. If CurrencyId != Cycle.DefaultCurrencyId -> require ExchangeRate (validated)
  |  4. ExecutionRecord.Create(...) with BudgetId + PeriodId denormalized
  |  5. SaveChangesAsync
  v
  201 Created { id }
```

### ListPeriodExecutionTotals (dual aggregation)
```
GET /api/budgets/{id}/periods/{periodId}/execution-totals
  |
  Dapper query with UNION ALL:
  |  Part 1: GROUP BY BudgetLineId -> per-line totals
  |  Part 2: GROUP BY CategoryGroupId, CategoryId -> per-category totals
  |  Discriminator column 'GroupLevel' = 'Line' | 'Category'
  v
  200 { lineTotals: [...], categoryTotals: [...] }
```

### BudgetLine soft-delete cascade
```
DeleteBudgetLineHandler (modified):
  1. Existing: line.SoftDelete()
  2. NEW: load ExecutionRecords for lineId -> each.SoftDelete()
  3. SaveChangesAsync
```

### Restore with IncludeExecutionRecords=true
```
RestoreBudgetLineHandler (modified):
  1. Existing: line.Restore()
  2. NEW: if cmd.IncludeExecutionRecords -> load soft-deleted ExecutionRecords -> each.Restore()
  3. SaveChangesAsync
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `SharedKernel/Entities/ExecutionRecord.cs` | Create | Entity with EntryType enum, Create/Update/SoftDelete/Restore methods |
| `SharedKernel/Entities/EntryType.cs` | Create | Enum: Expense=1, CreditNote=2, DebitNote=3 |
| `SharedKernel/Persistence/AppDbContext.cs` | Modify | Add `DbSet<ExecutionRecord>` |
| `SharedKernel/Persistence/Configurations/ExecutionRecordConfiguration.cs` | Create | Table config, indexes, no FK on AccountId/PaymentMethodId |
| `Migrations/YYYYMMDD_AddExecutionRecords.cs` | Create | Table + composite indexes |
| `Features/BudgetExecution/CreateExecutionRecord/` | Create | 4 files: Command, Validator, Handler, Endpoint |
| `Features/BudgetExecution/UpdateExecutionRecord/` | Create | 4 files: Command, Validator, Handler, Endpoint |
| `Features/BudgetExecution/DeleteExecutionRecord/` | Create | 4 files: Command, Validator, Handler, Endpoint |
| `Features/BudgetExecution/ListExecutionRecords/` | Create | 3 files: Query, Handler (Dapper), Endpoint |
| `Features/BudgetExecution/ListPeriodExecutionTotals/` | Create | 3 files: Query, Handler (Dapper), Endpoint |
| `Features/BudgetExecution/RestoreExecutionRecord/` | Create | 4 files: Command, Validator, Handler, Endpoint |
| `Features/BudgetStructure/DeleteBudgetLine/DeleteBudgetLineHandler.cs` | Modify | Add cascade soft-delete of child ExecutionRecords |
| `Features/BudgetStructure/RestoreBudgetLine/RestoreBudgetLineHandler.cs` | Modify | Activate IncludeExecutionRecords: restore child records |
| `Features/BudgetStructure/RestoreCategory/RestoreCategoryHandler.cs` | Modify | Activate IncludeExecutionRecords cascade through BudgetLines |
| `Features/BudgetStructure/RestoreCategoryGroup/RestoreCategoryGroupHandler.cs` | Modify | Activate IncludeExecutionRecords cascade |
| `Features/BudgetStructure/RestoreCycle/RestoreCycleHandler.cs` | Modify | Activate IncludeExecutionRecords cascade |

## Interfaces / Contracts

```csharp
// EntryType enum
public enum EntryType { Expense = 1, CreditNote = 2, DebitNote = 3 }

// ExecutionRecord entity
public sealed class ExecutionRecord : BaseEntity, IAuditableEntity
{
    public Guid      BudgetId         { get; private set; }
    public Guid      PeriodId         { get; private set; }
    public Guid      BudgetLineId     { get; private set; }
    public EntryType EntryType        { get; private set; }
    public decimal   Amount           { get; private set; }  // always positive
    public string?   Note             { get; private set; }  // required for CreditNote/DebitNote
    public Guid      CurrencyId       { get; private set; }
    public decimal?  ExchangeRate     { get; private set; }  // required when CurrencyId != DefaultCurrencyId
    public Guid?     AccountId        { get; private set; }  // no FK, forward-compat
    public Guid?     PaymentMethodId  { get; private set; }  // no FK, forward-compat
    public DateTimeOffset? DeletedAt  { get; private set; }
    public Guid? ResolveBudgetId() => BudgetId;
}

// EF Configuration key indexes
// IX_ExecutionRecords_BudgetLineId_DeletedAt (filtered: DeletedAt IS NULL)
// IX_ExecutionRecords_BudgetLineId_DeletedAt_EntryType (for aggregation)
// IX_ExecutionRecords_PeriodId_DeletedAt (for totals query)
// IX_ExecutionRecords_BudgetId (for RBAC)
// HasQueryFilter(e => e.DeletedAt == null)

// CreateExecutionRecordCommand
public sealed record CreateExecutionRecordCommand(
    Guid BudgetId, Guid PeriodId, Guid BudgetLineId,
    EntryType EntryType, decimal Amount, string? Note,
    Guid CurrencyId, decimal? ExchangeRate,
    Guid? AccountId, Guid? PaymentMethodId
) : IRequest<Result<Guid>>;

// ListPeriodExecutionTotals response
public sealed record PeriodExecutionTotalsResponse(
    IReadOnlyList<LineTotalDto> LineTotals,
    IReadOnlyList<CategoryTotalDto> CategoryTotals);

public sealed record LineTotalDto(
    Guid BudgetLineId, string BudgetLineName,
    decimal TotalExpenses, decimal TotalCreditNotes, decimal TotalDebitNotes, decimal NetTotal);

public sealed record CategoryTotalDto(
    Guid CategoryGroupId, string CategoryGroupName,
    Guid? CategoryId, string? CategoryName,
    decimal TotalExpenses, decimal TotalCreditNotes, decimal TotalDebitNotes, decimal NetTotal);
```

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| Unit | Validators: Amount>0, Note required for CreditNote/DebitNote, EntryType defined, ExchangeRate pair rule | FluentValidation `TestValidate` |
| Unit | Create/Update/Delete handlers: IsClosed guard, PeriodId mismatch, entity creation | In-memory SQLite EF |
| Unit | Restore handler: restores soft-deleted record, rejects non-deleted | In-memory SQLite EF |
| Unit | Delete cascade: BudgetLine delete also deletes child ExecutionRecords | In-memory SQLite EF |
| Unit | Restore cascade: IncludeExecutionRecords=true restores children | In-memory SQLite EF |
| Integration | Create -> List roundtrip, RBAC policy enforcement | WebApplicationFactory + in-memory DB |
| Integration | ListPeriodExecutionTotals: seed Expenses + CreditNotes, verify dual aggregation | WebApplicationFactory + in-memory DB |
| Integration | IsClosed guard returns 409 | WebApplicationFactory |

## Migration / Rollout

Single EF Core migration `AddExecutionRecords`:

1. `CREATE TABLE "ExecutionRecords"` with all columns. `EntryType` as int, `Amount` as decimal(18,2), `Note` as varchar(500), `ExchangeRate` as decimal(18,6), `AccountId`/`PaymentMethodId` as nullable uuid with NO FK constraint.
2. Composite indexes: `(BudgetLineId, DeletedAt)`, `(BudgetLineId, DeletedAt, EntryType)`, `(PeriodId, DeletedAt)`, `(BudgetId)`.
3. FK on `BudgetLineId` -> `BudgetLines.Id` with `Restrict` delete behavior.
4. FK on `CurrencyId` -> `Currencies.Id` with `Restrict` delete behavior.
5. FK on `PeriodId` -> `Periods.Id` with `Restrict` delete behavior (denormalized, validates consistency).

Rollback: `dotnet ef database update <previous-migration>`. Net-new table, no data migration.

## Open Questions

- None. All decisions resolved from confirmed decisions and existing codebase patterns.
