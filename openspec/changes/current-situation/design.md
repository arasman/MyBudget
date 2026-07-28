# Design: Current Situation (Periodic Financial Snapshot)

## Technical Approach

Three normalized entities (BankAccount, CutRecord, CutBankAccount) following existing VSA 4-file slice pattern. EF Core for all writes; Dapper for GetCutRecord (complex join + budget execution summary) and ListCutDates/ListBankAccounts reads. Frontend: new feature folder with Pinia store, date-navigable cut form, and separate bank-account CRUD.

## Architecture Decisions

| # | Decision | Choice | Rejected | Rationale |
|---|----------|--------|----------|-----------|
| 1 | Upsert strategy | EF Core load-or-create in handler | Raw `ON CONFLICT` SQL | Matches codebase convention (no raw SQL for writes); EF tracks audit log automatically |
| 2 | GetCutRecord draft logic | Single Dapper query with LEFT JOIN to last cut | Two separate queries (header + accounts) | One round-trip; Dapper already used for complex reads in ListPeriodExecutionTotals |
| 3 | BalanceInPrimary computation | Computed at write time (UpsertCutRecord handler) | Computed at read time | Avoids recalculation on every GET; snapshot is point-in-time by definition |
| 4 | Budget execution summary | Inline in GetCutRecord Dapper query via CTE | Separate endpoint | Data always shown together; avoids extra HTTP call; matches ListPeriodExecutionTotals pattern |
| 5 | BankAccount feature folder | Separate `Features/BankAccounts/` | Nested under CurrentSituation | BankAccounts are a standalone catalog; may be reused by other features |
| 6 | Frontend stores | Two stores: `useBankAccountStore`, `useCutRecordStore` | Single combined store | Separation of concerns; bank accounts managed independently |
| 7 | Delete CutRecord | Hard delete via EF Core (CASCADE handles CutBankAccounts) | Soft delete | Cut records have no downstream dependents; proposal specifies hard delete |
| 8 | Active period lookup | Dapper CTE joining Periods+Cycles with date range check | Reuse existing handler | No existing reusable query; CTE keeps it in single round-trip with GetCutRecord |

## Data Flow

```
UpsertCutRecord (write):
  Client ──PUT──> Endpoint ──> Validator ──> Handler
    Handler: load/create CutRecord (EF) ──> delete old CutBankAccounts
           ──> compute BalanceInPrimary per account ──> insert CutBankAccounts
           ──> SaveChangesAsync (audit log auto-captured)

GetCutRecord (read — existing record):
  Client ──GET──> Endpoint ──> Handler (Dapper)
    CTE: active_period ──> budget_execution_summary ──> cut_data + bank_accounts
    Returns: CutRecordResponse { IsDraft=false, Header, Accounts[], ExecutionSummary, Totals }

GetCutRecord (read — no record / draft):
  Same endpoint ──> Handler (Dapper)
    CTE: last_cut ──> active_accounts LEFT JOIN last_cut_balances
    Returns: CutRecordResponse { IsDraft=true, Accounts with Balance=0 or cloned, ExecutionSummary }
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `SharedKernel/Entities/BankAccount.cs` | Create | Entity with Create/Update/SoftDelete methods |
| `SharedKernel/Entities/CutRecord.cs` | Create | Entity with Create/Update methods, ProjectionsJson placeholder |
| `SharedKernel/Entities/CutBankAccount.cs` | Create | Snapshot entity with Create factory |
| `SharedKernel/Persistence/Configurations/BankAccountConfiguration.cs` | Create | EF config: FK RESTRICT to Budget+Currency, soft-delete query filter, unique index |
| `SharedKernel/Persistence/Configurations/CutRecordConfiguration.cs` | Create | EF config: FK RESTRICT to Budget, UNIQUE(BudgetId,CutDate) |
| `SharedKernel/Persistence/Configurations/CutBankAccountConfiguration.cs` | Create | EF config: FK CASCADE to CutRecord, RESTRICT to BankAccount, UNIQUE(CutRecordId,BankAccountId) |
| `SharedKernel/Persistence/AppDbContext.cs` | Modify | Add 3 DbSets |
| `Migrations/` | Create | Single migration for 3 tables + indexes |
| `Features/BankAccounts/CreateBankAccount/` | Create | 4 files: Command, Validator, Handler (EF), Endpoint |
| `Features/BankAccounts/ListBankAccounts/` | Create | 3 files: Query, Handler (Dapper), Endpoint |
| `Features/BankAccounts/UpdateBankAccount/` | Create | 4 files: Command, Validator, Handler (EF), Endpoint |
| `Features/BankAccounts/DeleteBankAccount/` | Create | 4 files: Command, Validator, Handler (EF soft-delete), Endpoint |
| `Features/CurrentSituation/UpsertCutRecord/` | Create | 4 files: Command, Validator, Handler (EF), Endpoint |
| `Features/CurrentSituation/GetCutRecord/` | Create | 3 files: Query, Handler (Dapper), Endpoint |
| `Features/CurrentSituation/ListCutDates/` | Create | 3 files: Query, Handler (Dapper), Endpoint |
| `Features/CurrentSituation/DeleteCutRecord/` | Create | 4 files: Command, Validator, Handler (EF), Endpoint |
| `frontend/src/features/bank-accounts/` | Create | api, store, types, views (BankAccountListView) |
| `frontend/src/features/current-situation/` | Create | api, store, types, views, components (6 components) |
| `frontend/src/router/index.ts` | Modify | Add 2 routes: current-situation, bank-accounts |
| `frontend/src/features/budget-structure/components/BudgetTabs.vue` | Modify | Add Current Situation tab |
| `frontend/src/i18n/locales/en.json` | Modify | Add bankAccount.* and currentSituation.* keys |
| `frontend/src/i18n/locales/es.json` | Modify | Add bankAccount.* and currentSituation.* keys |

## Interfaces / Contracts

```csharp
// GetCutRecord response (Dapper DTO)
record GetCutRecordResponse(
    bool IsDraft,
    Guid? CutRecordId,
    DateOnly CutDate,
    decimal ExchangeRate,
    string? ProjectionsJson,
    BudgetExecutionSummaryDto ExecutionSummary,
    IReadOnlyList<CutBankAccountDto> Accounts,
    CutTotalsDto Totals);

record BudgetExecutionSummaryDto(
    decimal TotalBudgeted,
    decimal TotalRegistered,
    decimal Remaining);

record CutBankAccountDto(
    Guid BankAccountId,
    string Alias,
    Guid CurrencyId,
    bool IsPositive,
    int DisplayOrder,
    decimal Balance,
    decimal BalanceInPrimary);

record CutTotalsDto(
    decimal TotalPositive,         // SUM(BalanceInPrimary) where IsPositive
    decimal TotalNegative,         // SUM(BalanceInPrimary) where !IsPositive
    decimal TotalDeudaEnCurso,     // Remaining + TotalNegative
    decimal TotalPositiveAlt,      // TotalPositive / ExchangeRate
    decimal TotalNegativeAlt,
    decimal TotalDeudaEnCursoAlt);

// UpsertCutRecord command
record UpsertCutRecordCommand(
    Guid BudgetId,
    DateOnly CutDate,
    decimal ExchangeRate,
    string? ProjectionsJson,
    IReadOnlyList<UpsertCutBankAccountItem> Accounts);

record UpsertCutBankAccountItem(
    Guid BankAccountId,
    decimal Balance);
```

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| Unit | Validators (all 8 slices), BalanceInPrimary computation logic | xUnit + NSubstitute; SQLite in-memory for entity tests |
| Integration | All 8 API slices end-to-end, draft/clone logic, active-period guard, soft-delete exclusion, cascade delete | WebApplicationFactory + real PostgreSQL; new `CurrentSituationTestBase` extending `BudgetStructureTestBase` (needs budget+cycle+period+currency setup) |
| Frontend unit | Stores (state mutations, API call wiring), component rendering | Vitest + @testing-library/vue |

## Threat Matrix

N/A -- no routing, shell, subprocess, VCS/PR automation, executable-file classification, or process-integration boundary.

## Migration / Rollout

Single EF Core migration creating 3 tables (BankAccounts, CutRecords, CutBankAccounts) with unique indexes. No data migration needed (greenfield tables). Rollback: `dotnet ef migrations remove` or down migration drops 3 tables.
