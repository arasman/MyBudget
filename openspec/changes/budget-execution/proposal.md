# Proposal: Budget Execution

## Intent

Budget structure (cycles, periods, lines) exists but users cannot record actual spending against budget lines. Without execution tracking, budgets are static plans with no way to compare budgeted vs actual amounts. This change adds the `ExecutionRecord` entity and 6 vertical slices to enable full budget execution lifecycle with entry-type semantics (Expense, CreditNote, DebitNote).

## Scope

### In Scope
- `ExecutionRecord` entity with `EntryType` enum (Expense=1, CreditNote=2, DebitNote=3)
- 6 slices: Create, Update, Delete, List, ListPeriodExecutionTotals, Restore
- `IsClosed` guard on ALL write operations (Create, Update, Delete, Restore) returning `PERIOD_CLOSED` (409)
- Amount always positive; sign determined by EntryType
- `Note` field: varchar(500), nullable for Expense, required for CreditNote/DebitNote
- `ListPeriodExecutionTotals` returning both per-BudgetLine and per-CategoryGroup/Category aggregations
- Cascade soft-delete: BudgetLine delete cascades to child ExecutionRecords
- Activate `IncludeExecutionRecords` parameter in all 4 existing restore handlers
- ExchangeRate pair rule (required when CurrencyId != DefaultCurrencyId, null otherwise)
- `AccountId`/`PaymentMethodId` as nullable Guids without FK constraints (forward-compat)

### Out of Scope
- Account / PaymentMethod entities (deferred to `current-situation`)
- Frontend UI for execution entry
- Budget vs actual charts/visualizations
- Hard delete of execution records
- Pagination on ListExecutions (simple list per budget line, not expected to be large)

## Capabilities

### New Capabilities
- `budget-execution`: CRUD + restore + aggregate totals for execution records against budget lines

### Modified Capabilities
- `budget-structure`: Activate `IncludeExecutionRecords` cascade in restore handlers; cascade soft-delete from BudgetLine to ExecutionRecords

## Approach

Vertical Slice Architecture: new `Features/BudgetExecution/` folder with 6 independent slices. Write slices use EF Core; read slices use Dapper. Entity extends `BaseEntity + IAuditableEntity`. Follows established patterns from `BudgetStructure` (same RBAC, IsClosed guard, soft-delete, ExchangeRate pair rule). Delivery in 2 chained PRs to stay within 400-line review budget.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `SharedKernel/Entities/ExecutionRecord.cs` | New | Entity with EntryType enum |
| `SharedKernel/Persistence/AppDbContext.cs` | Modified | Add DbSet |
| `SharedKernel/Persistence/Configurations/` | New | EF config + indexes |
| `Features/BudgetExecution/` (6 slices) | New | Create, Update, Delete, List, Totals, Restore |
| `Features/BudgetStructure/` (4 restore handlers) | Modified | Activate IncludeExecutionRecords |
| `Migrations/` | New | AddExecutionRecords migration |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| IncludeExecutionRecords activation touches 4 existing handlers | Med | Extend existing unit tests for each |
| PeriodId denormalization mismatch (route vs BudgetLine) | Med | Validate in handler; return 400 |
| EntryType enum migration if values change | Low | Use explicit int values (1,2,3) |
| Aggregate query performance on large datasets | Low | Index on (BudgetLineId, DeletedAt, EntryType) |

## Rollback Plan

Revert the 2 PRs in reverse order (PR2 then PR1). Run `dotnet ef migrations remove` to drop the migration. No data migration needed since this is a net-new table.

## Dependencies

- `budget-structure-patch` merged (confirmed: archived 2026-07-11)
- All 4 restore handlers with `IncludeExecutionRecords` stub (confirmed in codebase)

## Success Criteria

- [ ] All 6 slices pass unit tests (handler + validator)
- [ ] Integration tests cover RBAC, IsClosed guard, EntryType validation, cascade delete/restore
- [ ] ListPeriodExecutionTotals returns both line-level and category-level aggregations
- [ ] Negative amounts rejected; EntryType determines sign in totals calculation
- [ ] Note required for CreditNote/DebitNote, optional for Expense
- [ ] Existing restore handler tests still pass with IncludeExecutionRecords activated

## Delivery Plan

| PR | Content | Est. Lines |
|----|---------|-----------|
| PR1 | Entity + EF config + migration + Create + Update + Delete + unit tests | ~350 |
| PR2 | List + ListPeriodExecutionTotals + Restore + restore activation + integration tests | ~350 |
