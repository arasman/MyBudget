# Design: Cut Record Totals Persistence

## Technical Approach

Compute-at-write / read-verbatim, mirroring the existing `CutBankAccount.BalanceInPrimary` precedent. `UpsertCutRecordHandler` (EF write slice) computes all 16 totals from the same in-memory `BalanceInPrimary` values it is about to persist, plus one shared execution-summary Dapper query, and stores them on the `CutRecord` header. `GetCutRecordHandler` (Dapper read slice) selects those columns for an existing record (CS-2) and runs the live queries only for drafts. Implements CS-1, CS-2, CS-6, CS-9.

## Architecture Decisions

| # | Decision | Choice | Alternatives rejected | Rationale |
|---|---|---|---|---|
| 1 | Totals carrier | `CutTotals` positional record (16 `decimal`s) in `SharedKernel/Entities/`, passed to `Create`/`Update`; 16 plain scalar props on `CutRecord` | 20-parameter factory methods; EF owned type | Honors the proposal ("Create/Update accept all 16") without unreadable signatures. Owned type would complicate column naming/migration for zero gain |
| 2 | Where aggregation lives | Pure static `CutTotalsCalculator.Compute(rows, execSummary, exchangeRate) → CutTotals` | Duplicate SQL aggregation per path | The bank-account "aggregation" is already C# LINQ (`GetCutRecordHandler` step 4), not SQL — only the arithmetic needs sharing. Pure = unit-testable with no DB |
| 3 | Execution summary | Extract the existing CTE **verbatim** into `BudgetExecutionSummaryQuery.ExecuteAsync(IDbConnection, budgetId, cutDate)` | Copy/paste the SQL into Upsert | Removes the drift risk flagged in the proposal; zero behavioural delta |
| 4 | Upsert ordering | Resolve accounts, compute `BalanceInPrimary`, and fail `ACCOUNT_NOT_FOUND` **before any** `SaveChanges`; then totals → header → rows, wrapped in `BeginTransactionAsync` | Re-read persisted rows and recompute totals after insert | Totals derive by construction from the exact persisted values. Also closes an existing partial-write hole (today a bad account id aborts *after* the header was already saved) |
| 5 | Rounding | `Math.Round(v, 2, MidpointRounding.AwayFromZero)` on all 16 inside the calculator | Let `numeric(18,2)` round on store | Postgres rounds half-away-from-zero on store; explicit rounding keeps `persisted == computed` so the CS-1 integration assertion is exact |
| 6 | Migration backfill | One migration: add 16 columns `nullable: true` → `migrationBuilder.Sql(UPDATE …)` → `AlterColumn` to `nullable: false` | `defaultValue: 0m` (leaves an unmodelled DB default and silently ships zeros); separate data-migration tool | No data-migration precedent exists in `Migrations/` — **this establishes the pattern**. Final schema matches the model snapshot exactly |

## Data Flow

    UPSERT (write)
      cmd ─→ active-period check ─→ cycle ─→ accounts + BalanceInPrimary (in-memory)
                                                    │
              BudgetExecutionSummaryQuery ──────────┤
                                                    ▼
                                        CutTotalsCalculator ──→ CutTotals(16)
                                                    │
                          [tx] CutRecord.Create/Update(…, totals) + CutBankAccount rows

    GET (read)
      existing ─→ SELECT header + 16 columns ─→ CutTotalsDto + BudgetExecutionSummaryDto (no CTE, no LINQ sum)
      draft    ─→ draft rows + BudgetExecutionSummaryQuery ─→ CutTotalsCalculator (live, unchanged)

## File Changes

| File | Action | Description |
|---|---|---|
| `SharedKernel/Entities/CutTotals.cs` | Create | Positional record, 16 decimals; `CutTotals.Zero` |
| `SharedKernel/Entities/CutRecord.cs` | Modify | 16 `decimal` props (CS-6 names); `Create(…, CutTotals totals, string? projectionsJson = null)`, `Update(exchangeRate, CutTotals totals, projectionsJson)` |
| `SharedKernel/Persistence/Configurations/CutRecordConfiguration.cs` | Modify | `HasPrecision(18, 2).IsRequired()` × 16 (matches `CutBankAccountConfiguration.BalanceInPrimary`) |
| `Features/CurrentSituation/Shared/CutTotalsCalculator.cs` | Create | Pure static; the only implementation of the 16-value arithmetic |
| `Features/CurrentSituation/Shared/BudgetExecutionSummaryQuery.cs` | Create | Holds the CTE moved out of `GetCutRecordHandler`; returns `(TotalBudgeted, TotalRegistered, Remaining)` or zeros |
| `Features/CurrentSituation/UpsertCutRecord/UpsertCutRecordHandler.cs` | Modify | Reorder per Decision 4; compute + persist 16 totals in one transaction |
| `Features/CurrentSituation/GetCutRecord/GetCutRecordHandler.cs` | Modify | Existing path: 16 columns in the header SELECT, no CTE, no LINQ sums. Draft path: calls the two shared components |
| `Migrations/{ts}_AddCutRecordPersistedTotals.cs` | Create | Hand-edited per Decision 6 |

## Interfaces / Contracts

```csharp
public sealed record CutTotals(
    decimal TotalPositive,   decimal TotalPositiveAlt,
    decimal TotalNegative,   decimal TotalNegativeAlt,
    decimal TotalDeudaEnCurso, decimal TotalDeudaEnCursoAlt,
    decimal TotalBudgeted,   decimal TotalBudgetedAlt,
    decimal TotalRegistered, decimal TotalRegisteredAlt,
    decimal Remaining,       decimal RemainingAlt,
    decimal TotalAvailable,  decimal TotalAvailableAlt,
    decimal TotalNet,        decimal TotalNetAlt);

static CutTotals Compute(
    IEnumerable<(bool IsPositive, decimal BalanceInPrimary)> rows,
    BudgetExecutionSummary summary,
    decimal exchangeRate);   // er <= 0 → 1m guard, preserved from today
```

**Response DTOs are unchanged — verified.** `GetCutRecordResponse`, `CutTotalsDto` (6 fields) and `BudgetExecutionSummaryDto` (3 fields) keep their exact shapes; `frontend/src/features/current-situation/types/cutRecord.ts` needs no edit and `CutTotalsPanel.vue` keeps its client-side `totalAvailable`/`totalNet` `computed()`s (which stay numerically identical). The 7 persisted values with no DTO slot (`*BudgetedAlt`, `*RegisteredAlt`, `RemainingAlt`, `TotalAvailable(Alt)`, `TotalNet(Alt)`) are written for the deferred `dashboard` item only. For an existing record, `ExecutionSummary` **must** be filled from the persisted `TotalBudgeted/TotalRegistered/Remaining` columns — otherwise frozen totals would sit next to a live summary, the exact inconsistency CS-2 removes.

## Testing Strategy

| Layer | What | Approach |
|---|---|---|
| Unit | `CutTotalsCalculator`: CS-6 table (500/200/300 → 500/200/500), zero exchange-rate guard, rounding half-away-from-zero, empty rows | xUnit + Shouldly, `MyBudget.Features.Tests/Features/CurrentSituation/` |
| Integration | `persisted == freshly computed` on save; edit balances/execution records after save → totals unchanged (CS-6 snapshot); re-save overwrites all 16; client-submitted totals ignored; existing GET returns stored values; draft GET still live; migration backfill on pre-seeded rows | Extend `CutRecordIntegrationTests` (Testcontainers) |
| Frontend | None required — DTO shape verified unchanged | Re-run `useCutRecordStore.spec.ts` as regression only |
| E2E | Save a cut, mutate an execution record, reload the cut → displayed totals unchanged | New `e2e/current-situation/cut-totals-snapshot.spec.ts` |

## Threat Matrix

N/A — no routing, shell, subprocess, VCS/PR automation, executable-file classification, or process-integration boundary.

## Migration / Rollout

Single migration, three phases. Scaffold, then hand-edit `AddColumn` to `nullable: true`, insert the backfill `Sql`, append 16 `AlterColumn(nullable: false)`. `Down` drops the 16 columns.

```sql
UPDATE "CutRecords" cr SET "TotalPositive" = ROUND(a.pos, 2), … 
FROM (SELECT "CutRecordId",
             COALESCE(SUM("BalanceInPrimary") FILTER (WHERE "IsPositive"), 0) AS pos,
             COALESCE(SUM("BalanceInPrimary") FILTER (WHERE NOT "IsPositive"), 0) AS neg
      FROM "CutBankAccounts" GROUP BY "CutRecordId") a
LEFT JOIN LATERAL (/* existing execution CTE, correlated on cr.BudgetId, cr.CutDate */) ex ON true
WHERE cr."Id" = a."CutRecordId";
-- then: UPDATE "CutRecords" SET <col> = 0 WHERE <col> IS NULL;  (cuts with no CutBankAccount rows)
```

Accepted duplication: the backfill re-expresses `CutTotalsCalculator` in SQL once. It is dead code after it runs and is covered by an integration test; it is deliberately not extracted further.

Caveats to carry into tasks: (1) the backfill uses **current** execution data, which is precisely "what `GetCutRecord` would have returned pre-change" per CS-9 — not the data as of each cut's original save; (2) the CTE filters `p."IsClosed" = false`, so old cuts whose period has since closed backfill the execution trio to `0` and `TotalDeudaEnCurso = TotalNegative` — spec-conformant (identical to today's GET) but visibly different from a fresh re-save.

## Open Questions

- [ ] None blocking. Optional: add ES/EN i18n copy for snapshot semantics ("as of last save") — proposal success criterion, but a copy-only change that can slip to its own slice.
