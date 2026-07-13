# Exploration: budget-execution

## Executive Summary

The codebase provides clear patterns for every aspect of budget-execution — IsClosed guard, Currency FK with ExchangeRate pair rule, RBAC, soft-delete, IAuditableEntity audit, and forward-compat `IncludeExecutionRecords` hooks are all already in place. Recommended approach: new `BudgetExecution` domain folder with 5–6 slices, `ExecutionRecord` extending `BaseEntity + IAuditableEntity` with nullable `AccountId`/`PaymentMethodId` Guids (no FK constraint yet), delivered in 2 chained PRs.

---

## Current State

All prerequisite infrastructure is in place. `budget-structure-patch` is archived and merged into `main` as of 2026-07-11.

**Entities available:**
- `BudgetLine` — has `BudgetId`, `PeriodId`, `DeletedAt`, `Restore()`. FK-connected to `Period` and `BudgetLineRevision`.
- `BudgetLineRevision` — append-only, has `BudgetLineId`, `BudgetedAmount`, `CurrencyId` (FK → Currency).
- `Period` — has `IsClosed` bool (`SetClosed()` method exists). Already guards `CreateBudgetLine` with `PERIOD_CLOSED` → HTTP 409.
- `Cycle` — has `DefaultCurrencyId`, `AlternateCurrencyId`, `ExchangeRate` (decimal 18,6). ExchangeRate semantics: X DefaultCurrency = 1 AlternateCurrency.
- `Currency` — immutable catalog with GTQ, USD, EUR seeds. Deterministic GUIDs in `CurrencySeeds`.
- `BudgetRole` enum: ReadOnly=10, Operator=20, Admin=30, Owner=40.
- Policies: `budget:read` (≥10), `budget:operator` (≥20), `budget:admin` (≥30).

**Auth pattern:** `BudgetAuthorizationHandler` reads `{id}` from route values. All budget-scoped routes follow `/api/budgets/{id}/...`.

**Forward-compat:** All 4 restore handlers (`RestoreCycle`, `RestoreCategoryGroup`, `RestoreCategory`, `RestoreBudgetLine`) already accept `IncludeExecutionRecords: bool` as a no-op. Budget-execution must activate this parameter.

**Audit:** `AppDbContext.SaveChangesAsync` intercepts all `IAuditableEntity` mutations. `ExecutionRecord` should implement `IAuditableEntity` for automatic audit trail — no extra wiring needed.

---

## Affected Areas

- `Project/src/MyBudget.Features/SharedKernel/Entities/ExecutionRecord.cs` — new entity
- `Project/src/MyBudget.Features/SharedKernel/Persistence/AppDbContext.cs` — add `DbSet<ExecutionRecord>`
- `Project/src/MyBudget.Features/SharedKernel/Persistence/Configurations/ExecutionRecordConfiguration.cs` — EF config + indexes
- `Project/src/MyBudget.Features/Migrations/YYYYMMDD_AddExecutionRecords.cs` — new migration
- `Project/src/MyBudget.Features/Features/BudgetExecution/CreateExecution/` — 4 files
- `Project/src/MyBudget.Features/Features/BudgetExecution/UpdateExecution/` — 4 files
- `Project/src/MyBudget.Features/Features/BudgetExecution/DeleteExecution/` — 4 files
- `Project/src/MyBudget.Features/Features/BudgetExecution/ListExecutions/` — 3 files (query slice, no validator)
- `Project/src/MyBudget.Features/Features/BudgetExecution/ListPeriodExecutionTotals/` — 3 files (aggregate read)
- `Project/src/MyBudget.Features/Features/BudgetExecution/RestoreExecution/` — 4 files
- `Project/src/MyBudget.Features/Features/BudgetStructure/RestoreBudgetLine/` — activate `IncludeExecutionRecords`
- `Project/src/MyBudget.Features/Features/BudgetStructure/RestoreCycle/` — activate cascade
- `Project/src/MyBudget.Features/Features/BudgetStructure/RestoreCategory/` — activate cascade
- `Project/src/MyBudget.Features/Features/BudgetStructure/RestoreCategoryGroup/` — activate cascade
- `Project/tests/MyBudget.Features.Tests/Features/BudgetExecution/` — unit tests
- `Project/tests/MyBudget.Integration.Tests/Features/BudgetExecution/` — integration tests

---

## AccountId / PaymentMethodId Decision

`Account` and `PaymentMethod` entities do not exist yet — they belong to `current-situation` (feature 10, unplanned). The design image confirms execution entries reference a payment method.

| Approach | Pros | Cons | Effort |
|---|---|---|---|
| **A: Nullable Guid columns, no FK constraint** | API contract stable from day 1; no migration needed when current-situation lands | No referential integrity now | Low |
| **B: Exclude entirely** | Simplest entity | Requires migration + API change later | Low now, cost later |
| **C: Nullable columns + deferred FK migration note** | Same as A with explicit future plan documented | None vs A | Low |

**Decision: A.** Store `AccountId Guid?` and `PaymentMethodId Guid?` in `ExecutionRecord` entity and migration. No `HasForeignKey()` in EF config. When `current-situation` ships, a migration adds the FK constraints. The API accepts/returns these as nullable Guids today.

---

## Proposed ExecutionRecord Entity Shape

```csharp
public sealed class ExecutionRecord : BaseEntity, IAuditableEntity
{
    public Guid        BudgetId        { get; private set; }  // denormalized; enables RBAC + audit without JOIN
    public Guid        BudgetLineId    { get; private set; }  // FK → BudgetLines
    public Guid        PeriodId        { get; private set; }  // denormalized; enables period-level aggregate queries
    public DateOnly    Date            { get; private set; }
    public decimal     Amount          { get; private set; }  // precision (18,6)
    public Guid        CurrencyId      { get; private set; }  // FK → Currencies
    public decimal?    ExchangeRate    { get; private set; }  // X DefaultCurrency = 1 CurrencyId (same Cycle semantics)
    public Guid?       AccountId       { get; private set; }  // future FK — no constraint yet
    public Guid?       PaymentMethodId { get; private set; }  // future FK — no constraint yet
    public string?     Note            { get; private set; }
    public DateTimeOffset? DeletedAt   { get; private set; }

    // Navigation
    public BudgetLine? BudgetLine { get; private set; }
    public Currency?   Currency   { get; private set; }

    public Guid? ResolveBudgetId() => BudgetId;  // IAuditableEntity — no JOIN needed
}
```

---

## Slices

| Slice | Auth | EF/Dapper | Route |
|---|---|---|---|
| `CreateExecution` | `budget:operator` | EF (write) | `POST /api/budgets/{id}/periods/{periodId}/lines/{lineId}/executions` |
| `UpdateExecution` | `budget:operator` | EF (write) | `PUT /api/budgets/{id}/periods/{periodId}/lines/{lineId}/executions/{executionId}` |
| `DeleteExecution` | `budget:operator` | EF (soft-delete) | `DELETE /api/budgets/{id}/periods/{periodId}/lines/{lineId}/executions/{executionId}` |
| `ListExecutions` | `budget:read` | Dapper (read) | `GET /api/budgets/{id}/periods/{periodId}/lines/{lineId}/executions` |
| `ListPeriodExecutionTotals` | `budget:read` | Dapper (aggregate) | `GET /api/budgets/{id}/periods/{periodId}/execution-totals` |
| `RestoreExecution` | `budget:operator` | EF (restore) | `POST /api/budgets/{id}/periods/{periodId}/lines/{lineId}/executions/{executionId}/restore` |

---

## Key Guards

### IsClosed Guard
`CreateExecution` handler must check `Period.IsClosed`. Pattern from `CreateBudgetLine`:
- Load `BudgetLine`, verify it belongs to the budget via Period → Cycle chain
- If `Period.IsClosed` → return `PERIOD_CLOSED` → HTTP 409

### ExchangeRate Pair Rule
When `CurrencyId != Cycle.DefaultCurrencyId`, `ExchangeRate` must be provided. When same currency, `ExchangeRate` must be null. FluentValidation cross-property rule — reference `CycleValidator` for pattern.

### PeriodId Denormalization Validation
`CreateExecution` must validate that the `periodId` route param matches `BudgetLine.PeriodId` to prevent denormalized inconsistency.

---

## Folder Structure Decision

| Option | Recommendation |
|---|---|
| New `Features/BudgetExecution/` folder | ✅ Recommended — clean domain separation |
| Nest under `Features/BudgetStructure/` | ✗ Violates VSA feature cohesion |

---

## Delivery Plan (2 Chained PRs)

- **PR1**: Entity + EF config + migration + 3 write slices (Create, Update, Delete) + unit tests
- **PR2**: 2 read slices (ListExecutions, ListPeriodExecutionTotals) + RestoreExecution + restore-handler activation + integration tests

---

## Risks

| Risk | Severity | Mitigation |
|---|---|---|
| `IncludeExecutionRecords` activation touches 4 existing restore handlers | Medium | Extend existing unit tests for each handler |
| `PeriodId` denormalization: route param vs `BudgetLine.PeriodId` mismatch | Medium | Validate in `CreateExecution` handler; return 400 if mismatch |
| ExchangeRate pair rule (cross-property FluentValidation) | Low | Reference `CycleValidator` pattern |
| `Amount` decimal precision — may need `(18,6)` override in EF config | Low | Check global precision config; add column override if needed |
| `AccountId`/`PaymentMethodId` no FK constraint — referential integrity deferred | Low | Document in spec as accepted deviation; FK migration in `current-situation` |

---

## Ready for Proposal

Yes — all decisions resolved. No blocking unknowns remain.
