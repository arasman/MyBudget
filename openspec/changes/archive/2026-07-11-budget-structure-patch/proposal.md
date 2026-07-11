# Proposal: Budget Structure Patch

## Intent

The budget-structure backend is missing several capabilities that block the upcoming `budget-execution` feature and leave data integrity gaps: (1) soft-deleted entities cannot be restored, (2) BudgetLine lacks currency, exchange-rate, and display-order columns, (3) Cycle has no default currency, and (4) the Currency model is a hardcoded string array instead of a reference table. This patch closes those gaps with schema changes, new restore endpoints, and a data migration for existing BudgetLines.

## Scope

### In Scope
- **Currency reference table**: new `Currency` entity (Id, Code, Name, Symbol) with seed data (GTQ/Quetzal/Q, USD/US Dollar/$, EUR/Euro/€); replace hardcoded `AllowedCurrencies` arrays
- **Cycle currency fields**: `DefaultCurrencyId` (FK → Currency, NOT NULL), `AlternateCurrencyId` (FK → Currency, nullable), `ExchangeRate` (decimal(18,6), nullable)
- **ExchangeRate validation**: `AlternateCurrencyId` and `ExchangeRate` must both be provided or both be null; semantics: X DefaultCurrency = 1 AlternateCurrency (e.g., 7.5 GTQ = 1 USD)
- **BudgetLineRevisions.Currency migration**: change `Currency varchar(3)` → `CurrencyId Guid FK → Currency`; migration deletes existing BudgetLineRevision rows first (test data only, approved)
- **BudgetLine new columns**: `CurrencyId` (FK → Currency, optional — defaults to Cycle's DefaultCurrencyId at creation), `DisplayOrder` (int)
- **DisplayOrder migration**: backfill existing BudgetLines with sequential `DisplayOrder` based on `CreatedAt` ascending within each `(PeriodId, CategoryGroupId, CategoryId)` group
- **Restore endpoints** (cascading, with `includeExecutionRecords: bool` parameter):
  - `RestoreCycle`: Cycle -> Periods -> BudgetLines -> (ExecutionRecords: no-op)
  - `RestoreCategoryGroup`: Group -> Categories -> BudgetLines -> (ExecutionRecords: no-op)
  - `RestoreCategory`: Category -> BudgetLines -> (ExecutionRecords: no-op)
  - `RestoreBudgetLine`: BudgetLine -> (ExecutionRecords: no-op)
- **Entity `Restore()` methods**: add to Cycle, Period, CategoryGroup, Category, BudgetLine
- **Forward-compatibility contract**: `includeExecutionRecords` parameter exists in API contract now; handlers are no-op until `budget-execution` is implemented

### Out of Scope
- Audit logging (separate `audit-log` SDD change)
- ExecutionRecord entity/table (deferred to `budget-execution`)
- Period-level restore endpoint (Periods restore as part of Cycle cascade)
- Currency management UI / CRUD (no-auth read-only list only)
- Frontend changes (separate SDD cycle)

## Capabilities

### New Capabilities
- `currency-reference`: Currency reference table with seed data, replacing hardcoded string arrays
- `budget-restore`: Cascading restore endpoints for Cycle, CategoryGroup, Category, BudgetLine

### Modified Capabilities
- `budget-structure`: BudgetLine gains CurrencyId, AlternateCurrencyId, ExchangeRate, DisplayOrder; Cycle gains DefaultCurrencyId; Create/Update commands and validators updated

## Approach

- **Database-first**: EF Core migration adds Currency table with seed, new columns on Cycle and BudgetLine, backfills DisplayOrder via SQL
- **Entity changes**: add `Restore()` method (sets `DeletedAt = null`) to all soft-deletable entities; extend `BudgetLine.Create()` and `BudgetLine.Update()` with new fields; extend `Cycle.Create()` and `Cycle.Update()` with currency fields
- **Restore handlers**: mirror existing delete cascade pattern (e.g., RestoreCycle loads soft-deleted children with `IgnoreQueryFilters()`, calls `Restore()` on each)
- **CurrencyId default resolution**: CreateBudgetLineHandler resolves `Cycle.DefaultCurrencyId` via Period → Cycle chain when `CurrencyId` is not provided in the request
- **Validation on Cycle**: FluentValidation rule ensures `AlternateCurrencyId` and `ExchangeRate` are both present or both null (applied to CreateCycle + UpdateCycle)
- **BudgetLineRevision migration**: DELETE existing rows in migration before altering Currency column to CurrencyId FK (test data — approved)

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `SharedKernel/Entities/Currency.cs` | New | Currency reference entity (Id, Code, Name, Symbol) |
| `SharedKernel/Entities/BudgetLine.cs` | Modified | Add CurrencyId, DisplayOrder, Restore() |
| `SharedKernel/Entities/BudgetLineRevision.cs` | Modified | Replace Currency string → CurrencyId FK |
| `SharedKernel/Entities/Cycle.cs` | Modified | Add DefaultCurrencyId, AlternateCurrencyId, ExchangeRate, Restore() |
| `SharedKernel/Entities/Period.cs` | Modified | Add Restore() |
| `SharedKernel/Entities/CategoryGroup.cs` | Modified | Add Restore() |
| `SharedKernel/Entities/Category.cs` | Modified | Add Restore() |
| `Features/BudgetStructure/Restore*/` | New | 4 restore feature folders (command, handler, endpoint, validator) |
| `Features/BudgetStructure/CreateBudgetLine/` | Modified | CurrencyId default resolution, new fields |
| `Features/BudgetStructure/UpdateBudgetLine/` | Modified | New fields, exchange-rate pair validation |
| `Persistence/Configurations/` | Modified | Currency config, BudgetLine/Cycle FK configs |
| `Migrations/` | New | Migration for Currency table, new columns, DisplayOrder backfill |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Migration backfill assigns wrong DisplayOrder | Low | Order by CreatedAt ascending is deterministic; verify in test |
| Restore of entity whose parent is still deleted | Medium | Validate parent is not soft-deleted before restore; return error |
| ExchangeRate precision loss | Low | Use `decimal(18,6)` column type |
| Forward-compat `includeExecutionRecords` becomes stale | Low | Document explicitly; `budget-execution` SDD will reference this contract |

## Rollback Plan

Revert the migration (EF Core `dotnet ef database update <previous-migration>`). All changes are additive columns and new endpoints; no destructive schema changes.

## Dependencies

- None (all changes are backend-only, building on existing budget-structure tables)

## Success Criteria

- [ ] Currency reference table seeded with GTQ, USD, EUR (with Symbol field)
- [ ] Cycle.DefaultCurrencyId required on create/update
- [ ] Cycle.AlternateCurrencyId + ExchangeRate validated as a pair (both or neither) on Cycle
- [ ] BudgetLineRevisions.CurrencyId FK replaces Currency varchar(3)
- [ ] BudgetLine.CurrencyId defaults to Cycle.DefaultCurrencyId when omitted
- [ ] BudgetLine.CurrencyId defaults to Cycle.DefaultCurrencyId when omitted
- [ ] Existing BudgetLines backfilled with sequential DisplayOrder
- [ ] RestoreCycle cascades to Periods and BudgetLines
- [ ] RestoreCategoryGroup cascades to Categories and BudgetLines
- [ ] RestoreCategory cascades to BudgetLines
- [ ] RestoreBudgetLine restores single BudgetLine
- [ ] `includeExecutionRecords` parameter present on all restore endpoints (no-op)
- [ ] Restore rejects if parent entity is soft-deleted
- [ ] All new/modified handlers have unit tests
