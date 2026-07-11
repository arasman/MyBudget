# Design: Budget Structure Patch

## Technical Approach

Extend the existing VSA budget-structure feature with: (1) a new `Currency` catalog entity with EF Core `HasData` seeding, (2) currency FK columns on `Cycle` and `BudgetLineRevision`, (3) `DisplayOrder` on `BudgetLine` with migration backfill, and (4) four restore slices mirroring the existing delete cascade pattern. All writes use EF Core; all reads use Dapper. Endpoints auto-register via the existing `MapAllSliceEndpoints` reflection scanner.

## Architecture Decisions

| # | Decision | Choice | Rejected Alternative | Rationale |
|---|----------|--------|---------------------|-----------|
| 1 | Currency entity base class | Standalone class (no `BaseEntity` inheritance) | Inherit `BaseEntity` | `BaseEntity` carries `CreatedAt`/`UpdatedAt`/domain events. Currency is an immutable catalog -- no timestamps or events needed. Simpler, no soft-delete column. |
| 2 | Seed strategy | `HasData()` in `CurrencyConfiguration` | Raw SQL in migration `Up()` | `HasData` is idempotent across re-scaffolds, keeps seed tied to entity config, and EF generates deterministic GUIDs. |
| 3 | Seed GUIDs | Hard-coded well-known `Guid` constants in a static `CurrencySeeds` class | `Guid.NewGuid()` at migration time | Deterministic IDs enable FK references in tests and future migrations without lookups. |
| 4 | ExchangeRate precision | `decimal(18,6)` via explicit `HasPrecision(18, 6)` in `CycleConfiguration` | Rely on global `(18,2)` | Exchange rates need 6 decimal places. Override global precision only on this property. |
| 5 | `Restore()` placement | Instance method on each entity (mirrors `SoftDelete()`) | Domain service | Follows existing pattern: `SoftDelete()` is an instance method. `Restore()` is the inverse. |
| 6 | Parent-deleted guard | Handler-level check with `IgnoreQueryFilters` before restoring | Database trigger / CHECK constraint | Consistent with existing handler-level validation (e.g., `PERIOD_CLOSED` guard). Keeps logic in application layer. |
| 7 | `includeExecutionRecords` location | Query-string `bool` parameter on restore endpoints | Command body property | It is a behavioral flag, not domain data. Query string keeps the POST body empty (restore has no payload). |
| 8 | BudgetLine `CurrencyId` on entity vs. revision only | On `BudgetLineRevision` only (not on `BudgetLine`) | Add `CurrencyId` to `BudgetLine` too | Spec BLR-1 replaces `Currency` on revision. Proposal mentions `BudgetLine.CurrencyId` but spec does not require it -- currency lives on the revision per the append-only design. The `Create`/`Update` handlers resolve `CurrencyId` and pass it to `BudgetLineRevision.Create()`. |
| 9 | Migration order | Single migration: Currency table + seed -> Cycle columns -> BudgetLineRevision delete+alter -> BudgetLine DisplayOrder backfill | Multiple migrations | All schema changes are interdependent (Cycle FK needs Currency table). One migration reduces coordination risk. |

## Data Flow

### Restore Cascade (RestoreCycle example)

```
POST /api/budgets/{id}/cycles/{cycleId}/restore
  |
  v
RestoreCycleEndpoint -> RestoreCycleCommand -> RestoreCycleHandler
  |
  |  1. IgnoreQueryFilters -> load Cycle where DeletedAt != null
  |  2. Cycle.Restore()
  |  3. IgnoreQueryFilters -> load soft-deleted Periods for CycleId
  |  4. Each Period.Restore()
  |  5. IgnoreQueryFilters -> load soft-deleted BudgetLines for restored PeriodIds
  |  6. Each BudgetLine.Restore()
  |  7. SaveChangesAsync
  v
204 No Content
```

### CurrencyId Default Resolution (CreateBudgetLine)

```
CreateBudgetLineCommand (CurrencyId = null)
  |
  v
Handler: _db.Periods.Include(p => p.Cycle) -> period.Cycle.DefaultCurrencyId
  |
  v
BudgetLineRevision.Create(lineId, amount, currencyId)
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `SharedKernel/Entities/Currency.cs` | Create | `Id`, `Code`, `Name`, `Symbol`. No `BaseEntity`. Private ctor + static seeds. |
| `SharedKernel/Entities/CurrencySeeds.cs` | Create | Static class with well-known `Guid` constants for GTQ, USD, EUR. |
| `SharedKernel/Entities/Cycle.cs` | Modify | Add `DefaultCurrencyId`, `AlternateCurrencyId`, `ExchangeRate`, navigation props, extend `Create()` and `Update()`, add `Restore()`. |
| `SharedKernel/Entities/Period.cs` | Modify | Add `Restore()` method. |
| `SharedKernel/Entities/CategoryGroup.cs` | Modify | Add `Restore()` method. |
| `SharedKernel/Entities/Category.cs` | Modify | Add `Restore()` method. |
| `SharedKernel/Entities/BudgetLine.cs` | Modify | Add `DisplayOrder` property, `SetDisplayOrder()`, `Restore()`. Extend `Create()` with `displayOrder` param. |
| `SharedKernel/Entities/BudgetLineRevision.cs` | Modify | Replace `Currency string` with `CurrencyId Guid` + navigation. Update `Create()` signature. |
| `SharedKernel/Persistence/AppDbContext.cs` | Modify | Add `DbSet<Currency> Currencies`. |
| `Persistence/Configurations/CurrencyConfiguration.cs` | Create | Table config, unique index on `Code`, `HasData()` seed. |
| `Persistence/Configurations/CycleConfiguration.cs` | Modify | Two FK relationships to `Currency`, precision override for `ExchangeRate`. |
| `Persistence/Configurations/BudgetLineConfiguration.cs` | Modify | Add `DisplayOrder` property mapping. |
| `Persistence/Configurations/BudgetLineRevisionConfiguration.cs` | Modify | Replace `Currency` varchar config with `CurrencyId` FK. |
| `Migrations/YYYYMMDD_BudgetStructurePatch.cs` | Create | Currency table + seed, Cycle columns, BudgetLineRevision delete + alter, BudgetLine DisplayOrder + backfill. |
| `Features/BudgetStructure/ListCurrencies/` | Create | 3 files: Query, Handler (Dapper), Endpoint. Simple `SELECT * FROM "Currencies"`. |
| `Features/BudgetStructure/RestoreCycle/` | Create | 4 files: Command, Handler, Endpoint, Validator. |
| `Features/BudgetStructure/RestoreCategoryGroup/` | Create | 4 files: Command, Handler, Endpoint, Validator. |
| `Features/BudgetStructure/RestoreCategory/` | Create | 4 files: Command, Handler, Endpoint, Validator. |
| `Features/BudgetStructure/RestoreBudgetLine/` | Create | 4 files: Command, Handler, Endpoint, Validator. |
| `Features/BudgetStructure/ReorderBudgetLines/` | Create | 4 files: Command, Handler, Endpoint, Validator. Mirror `ReorderCategories` exactly. |
| `Features/BudgetStructure/CreateCycle/` | Modify | All 4 files: add currency fields to command, request, handler, validator. |
| `Features/BudgetStructure/UpdateCycle/` | Modify | All 4 files: add currency fields to command, request, handler, validator. |
| `Features/BudgetStructure/GetCycleDetail/` | Modify | Query + Handler: JOIN `Currencies`, extend response with `defaultCurrency`, `alternateCurrency`, `exchangeRate`. |
| `Features/BudgetStructure/ListCycles/` | Modify | Query + Handler: JOIN `Currencies`, extend response with `defaultCurrency`. |
| `Features/BudgetStructure/CreateBudgetLine/` | Modify | Command: replace `Currency string` with `CurrencyId Guid?`. Handler: resolve default from `Cycle.DefaultCurrencyId`. |
| `Features/BudgetStructure/UpdateBudgetLine/` | Modify | Command: replace `Currency string` with `CurrencyId Guid?`. Handler: resolve default. |
| `Features/BudgetStructure/ListBudgetLines/` | Modify | Handler: JOIN `Currencies` via revision. Response: replace `Currency string?` with `CurrencyCode`/`CurrencySymbol` or nested object. |

## Interfaces / Contracts

```csharp
// Currency entity (no BaseEntity)
public sealed class Currency
{
    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;   // varchar(3)
    public string Name { get; private set; } = string.Empty;   // varchar(100)
    public string Symbol { get; private set; } = string.Empty;  // varchar(10)
    private Currency() { }
}

// Restore method (same on all soft-deletable entities)
public void Restore()
{
    DeletedAt = null;
    UpdatedAt = DateTimeOffset.UtcNow;
}

// RestoreCycleCommand
public sealed record RestoreCycleCommand(
    Guid BudgetId, Guid CycleId, bool IncludeExecutionRecords
) : IRequest<Result<Guid>>;

// CurrencyDto (used in cycle and budget-line responses)
public sealed record CurrencyDto(string Code, string Symbol);
```

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| Unit | `Restore()` sets `DeletedAt=null`, `UpdatedAt` refreshed | Entity unit tests |
| Unit | Cycle/BudgetLine `Create()` with new params | Entity factory tests |
| Unit | CYC_PAIR_INCOMPLETE validator (AlternateCurrencyId XOR ExchangeRate) | FluentValidation `TestValidate` |
| Unit | RestoreCycle cascade logic, parent-deleted guard | Handler tests with in-memory EF |
| Unit | ReorderBudgetLines handler (scope, duplicates) | Handler tests with in-memory EF |
| Unit | CurrencyId default resolution in CreateBudgetLine | Handler tests with in-memory EF |

## Migration / Rollout

Single EF Core migration with the following ordered steps in `Up()`:

1. `CREATE TABLE "Currencies"` with columns `Id`, `Code` (varchar 3), `Name` (varchar 100), `Symbol` (varchar 10). Unique index on `Code`.
2. `INSERT INTO "Currencies"` -- 3 seed rows (GTQ, USD, EUR) with deterministic GUIDs.
3. `ALTER TABLE "Cycles"` -- add `DefaultCurrencyId` (uuid NOT NULL, DEFAULT = GTQ seed GUID), `AlternateCurrencyId` (uuid NULL), `ExchangeRate` (numeric(18,6) NULL). FK constraints.
4. `DELETE FROM "BudgetLineRevisions"` -- purge test data (approved).
5. `ALTER TABLE "BudgetLineRevisions"` -- drop `Currency` varchar column, add `CurrencyId` uuid NOT NULL FK.
6. `ALTER TABLE "BudgetLines"` -- add `DisplayOrder` int NOT NULL DEFAULT 0.
7. Backfill `DisplayOrder`: `UPDATE "BudgetLines" SET "DisplayOrder" = sub.rn FROM (SELECT "Id", ROW_NUMBER() OVER (PARTITION BY "PeriodId", "CategoryGroupId", "CategoryId" ORDER BY "CreatedAt") AS rn FROM "BudgetLines") sub WHERE "BudgetLines"."Id" = sub."Id"`.

Rollback: `Down()` reverses all steps. `dotnet ef database update <previous-migration>`.

## PR Split Recommendation

Estimated total: ~900-1100 changed lines. Recommended 3 chained PRs:

| PR | Scope | Est. Lines | Branch |
|----|-------|-----------|--------|
| PR1 | Currency entity + config + seed + migration (Currency table + Cycle columns only) + CreateCycle/UpdateCycle/GetCycleDetail/ListCycles updates + GET currencies endpoint | ~350 | `feat/budget-patch-currency` |
| PR2 | BudgetLineRevision CurrencyId migration + BudgetLine DisplayOrder + backfill + ReorderBudgetLines slice + CreateBudgetLine/UpdateBudgetLine/ListBudgetLines updates | ~350 | `feat/budget-patch-budgetline` |
| PR3 | Restore() methods on all entities + 4 restore slices (RestoreCycle, RestoreCategoryGroup, RestoreCategory, RestoreBudgetLine) with parent-deleted guard | ~300 | `feat/budget-patch-restore` |

PR1 targets `main`. PR2 targets PR1 branch. PR3 targets PR2 branch. Each PR is independently deployable and testable.

## Open Questions

- None. All decisions are resolved based on existing codebase patterns and spec requirements.
