# Exploration: current-situation

## Summary

The codebase has `Cycle.DefaultCurrencyId / AlternateCurrencyId / ExchangeRate` but no bank account catalog and no cut record entity. Implementing `current-situation` requires 3 new entities (`CutRecord`, `BankAccount`, `CutBankAccount`), 7 new API slices, a new frontend feature folder, and 1 migration.

**Recommended approach**: Approach A — BankAccount catalog + CutBankAccount snapshot.

---

## Current State

**Stack**: .NET 10 Vertical Slice Architecture + Vue 3 + PostgreSQL. 4-file slice pattern (Command/Validator/Handler/Endpoint). Mediator (not MediatR), EF Core for writes, Dapper for reads.

### Domain Entities (`SharedKernel/Entities/`)

| Entity | Key fields |
|---|---|
| `Budget` | Name, OwnerId, soft-delete |
| `Cycle` | BudgetId, DefaultCurrencyId, AlternateCurrencyId, ExchangeRate (18,6), StartDate, EndDate, IsActive |
| `Period` | CycleId, BudgetId, StartDate, EndDate, IsClosed |
| `BudgetLine` | BudgetId, CategoryGroupId, LineType, StartDate, EndDate, revision chain |
| `BudgetLineRevision` | BudgetLineId, BudgetedAmount, CurrencyId, ValidFrom, ValidTo |
| `ExecutionRecord` | BudgetId, PeriodId, BudgetLineId, Amount, CurrencyId, ExchangeRate, ExchangeRateTo, AccountId (nullable Guid — no FK), OperationDate |
| `Currency` | Seeded catalog: GTQ, USD, EUR |

### Currency Model

- Primary = `Cycle.DefaultCurrencyId`
- Alternate = `Cycle.AlternateCurrencyId`
- `Cycle.ExchangeRate` is cycle-wide; `ExecutionRecord.ExchangeRate` is per-record
- No bank account catalog exists; `ExecutionRecord.AccountId` is a bare nullable Guid with no FK

### Budget Execution Totals

Computed at query time in `ListPeriodExecutionTotalsHandler` (Dapper UNION ALL). Sums Expenses/CreditNotes/DebitNotes per BudgetLine and per CategoryGroup. Includes BudgetedAmount from the effective revision.

### Migrations

4 migrations through `AddBudgetLineDescription` (2026-07-23). No cut-record or bank-account tables.

### Frontend

`budget-structure` and `budget-execution` feature folders. Pinia composition-API stores. Route: `/budgets/:budgetId/cycles/:cycleId/matrix`. `BudgetTabs` component is the tab bar for budget-scoped views.

### Testing

`WebApplicationFactory<Program>` + real PostgreSQL. `BudgetExecutionTestBase > BudgetStructureTestBase > IntegrationTestBase` hierarchy. Each test class registers a user and operates within a freshly-cleaned DB.

---

## Affected Areas

| Path | Why |
|---|---|
| `Project/src/MyBudget.Features/SharedKernel/Entities/` | Add `CutRecord`, `BankAccount`, `CutBankAccount` entities |
| `Project/src/MyBudget.Features/SharedKernel/Persistence/AppDbContext.cs` | Add 3 new DbSets |
| `Project/src/MyBudget.Features/SharedKernel/Persistence/Configurations/` | 3 new EF config files |
| `Project/src/MyBudget.Features/Migrations/` | 1 new migration |
| `Project/src/MyBudget.Features/Features/CurrentSituation/` | New feature folder: UpsertCutRecord, GetCutRecord, ListCutDates |
| `Project/src/MyBudget.Features/Features/BankAccounts/` | New feature folder: Create/List/Update/Delete BankAccount |
| `Project/frontend/src/features/current-situation/` | New feature folder: store, api, types, views, components |
| `Project/frontend/src/router/index.ts` | Add `/budgets/:budgetId/current-situation` route |
| `Project/frontend/src/features/budget-structure/components/BudgetTabs.vue` | Add Current Situation tab |
| `Project/frontend/src/i18n/locales/en.json` and `es.json` | New i18n key namespace |
| `Project/tests/MyBudget.Integration.Tests/Features/CurrentSituation/` | New integration test class |
| `Project/tests/MyBudget.Features.Tests/Features/CurrentSituation/` | New unit test files |

---

## New Entities Required

### `CutRecord`

One per calendar day per budget.

```
Id            Guid PK
BudgetId      Guid FK -> Budgets (RESTRICT)
CutDate       DateOnly
ExchangeRate  decimal(18,6)  -- point-in-time rate at cut date
ProjectionsJson text?        -- Layer 2 placeholder (nullable JSON blob)
CreatedAt, UpdatedAt
UNIQUE INDEX (BudgetId, CutDate)
```

### `BankAccount`

Account catalog, budget-scoped.

```
Id           Guid PK
BudgetId     Guid FK -> Budgets (RESTRICT)
Alias        string(100)
CurrencyId   Guid FK -> Currencies (RESTRICT)
IsPositive   bool  -- true=asset, false=liability
DisplayOrder int
DeletedAt    DateTimeOffset?  -- soft-delete
CreatedAt, UpdatedAt
```

### `CutBankAccount`

Balance snapshot per cut per account.

```
Id               Guid PK
CutRecordId      Guid FK -> CutRecords (CASCADE)
BankAccountId    Guid FK -> BankAccounts (RESTRICT)
Balance          decimal(18,2)  -- in account currency
BalanceInPrimary decimal(18,2)  -- Balance * CutRecord.ExchangeRate (when foreign currency)
CreatedAt, UpdatedAt
UNIQUE INDEX (CutRecordId, BankAccountId)
```

---

## New API Slices

### CurrentSituation (`Features/CurrentSituation/`)

| # | Slice | Method | Path | Auth |
|---|---|---|---|---|
| 1 | `UpsertCutRecord` | PUT | `/api/budgets/{id}/cut-records/{date}` | `budget:operator` |
| 2 | `GetCutRecord` | GET | `/api/budgets/{id}/cut-records/{date}` | `budget:read` |
| 3 | `ListCutDates` | GET | `/api/budgets/{id}/cut-records/dates` | `budget:read` |

### BankAccounts (`Features/BankAccounts/`)

| # | Slice | Method | Path | Auth |
|---|---|---|---|---|
| 4 | `CreateBankAccount` | POST | `/api/budgets/{id}/bank-accounts` | `budget:admin` |
| 5 | `ListBankAccounts` | GET | `/api/budgets/{id}/bank-accounts` | `budget:read` |
| 6 | `UpdateBankAccount` | PUT | `/api/budgets/{id}/bank-accounts/{accountId}` | `budget:admin` |
| 7 | `DeleteBankAccount` | DELETE | `/api/budgets/{id}/bank-accounts/{accountId}` | `budget:admin` |

Budget Execution Summary is computed at query time in `GetCutRecord` using existing tables — no new slice needed.

---

## Approaches Evaluated

| Approach | Pros | Cons | Effort |
|---|---|---|---|
| **A — BankAccount catalog + CutBankAccount snapshot** | Clean separation; clone from previous = copy account refs; normalized; Layer 2 ready | 3 new tables; join complexity | Medium |
| **B — JSONB blob on CutRecord** | Single table; simple migration | Not queryable per account; no catalog reuse; Layer 2 hard | Low |
| **C — CutBankAccount only (no catalog)** | Simpler than A | Aliases drift across cuts; no independent account management | Low-Medium |

**Recommended**: Approach A.

---

## Risks

| Risk | Impact | Mitigation |
|---|---|---|
| Two exchange rate concepts: `Cycle.ExchangeRate` (cycle-wide) vs. `CutRecord.ExchangeRate` (point-in-time) | Medium | Spec must be explicit; UI must show cut-level rate separately |
| No active period at cut date (date outside any period) | Medium | Handler returns zeros for Section 1 gracefully |
| `ExecutionRecord.AccountId` — opaque Guid, not FK to new BankAccounts | Low | Field stays opaque by design; no migration conflict |
| Layer 2 JSON column contract — hard to evolve | Medium | Spec declares expected shape now; validated at Layer 2 implementation |
| 400-line budget exceeded — 3 entities + migration + 7 slices + frontend | High | Chained PRs mandatory: PR1 = backend, PR2 = frontend |
