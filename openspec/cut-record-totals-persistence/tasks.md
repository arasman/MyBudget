# Tasks: Cut Record Totals Persistence

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 550 – 800 (excludes generated `*.Designer.cs` / `AppDbContextModelSnapshot.cs`) |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 → Backend foundation + handlers (build-coupled, can't split further); PR 2 → Backend tests (unit + integration); PR 3 → E2E + frontend regression check |
| Delivery strategy | ask-on-risk |
| Chain strategy | feature-branch-chain |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: feature-branch-chain
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Focused test command | Runtime harness | Rollback boundary |
|------|------|-----------|----------------------|-----------------|-------------------|
| 1 | Entity + EF config + migration + shared calculator/query + Upsert/Get handlers (must ship together — `CutRecord.Create/Update` signature change breaks handlers otherwise) | PR 1 | `dotnet build MyBudget.slnx` | N/A — schema/handler-only, verified via PR 2's integration tests | Down-migration drops 16 columns; revert entity/handler diff |
| 2 | Unit test `CutTotalsCalculator`; extend `CutRecordIntegrationTests` (snapshot, re-save, ignored-totals, existing/draft GET, backfill) | PR 2 | `dotnet test --filter Category=CurrentSituation` | `PUT`/`GET /api/budgets/{id}/cut-records/{date}` via Testcontainers harness | Remove new test methods; no production code touched |
| 3 | New `cut-totals-snapshot.spec.ts` E2E; re-run `useCutRecordStore.spec.ts` regression | PR 3 | `npx playwright test e2e/current-situation/cut-totals-snapshot.spec.ts` | Navigate to `/budgets/:id/current-situation`, save cut, edit execution record, reload | Remove the new spec file |

---

## Phase 1: Foundation — Entity, EF Config, Migration

- [x] 1.1 Create `SharedKernel/Entities/CutTotals.cs` — positional record, 16 `decimal`s (CS-6 names) + `CutTotals.Zero`. Satisfies: CS-6
- [x] 1.2 Modify `SharedKernel/Entities/CutRecord.cs` — add 16 `decimal` props (CS-6 names); `Create(…, CutTotals totals, string? projectionsJson = null)`; `Update(exchangeRate, CutTotals totals, projectionsJson)`. Satisfies: CS-1, CS-6
- [x] 1.3 Modify `SharedKernel/Persistence/Configurations/CutRecordConfiguration.cs` — `HasPrecision(18, 2).IsRequired()` × 16 new columns. Satisfies: CS-6 (rounding precision scenario)
- [x] 1.4 Scaffold + hand-edit `Migrations/{ts}_AddCutRecordPersistedTotals.cs` — phase A: `AddColumn` nullable × 16; phase B: `migrationBuilder.Sql` backfill (bank-account aggregation UPDATE + LEFT JOIN LATERAL execution CTE, per design.md Migration/Rollout SQL) + zero-fill NULLs; phase C: `AlterColumn` non-nullable × 16; `Down` drops all 16. Satisfies: CS-9

---

## Phase 2: Shared Query/Calculator Components

- [x] 2.1 Create `Features/CurrentSituation/Shared/CutTotalsCalculator.cs` — pure static `Compute(rows, summary, exchangeRate) → CutTotals`; `exchangeRate <= 0` guard → divisor `1m`; `Math.Round(v, 2, MidpointRounding.AwayFromZero)` on all 16. Satisfies: CS-6
- [x] 2.2 Create `Features/CurrentSituation/Shared/BudgetExecutionSummaryQuery.cs` — extract the existing CTE verbatim from `GetCutRecordHandler` into `ExecuteAsync(IDbConnection, budgetId, cutDate)` returning `(TotalBudgeted, TotalRegistered, Remaining)` or zeros when no active period. Satisfies: CS-2 (execution summary scenarios)

---

## Phase 3: Handler Wiring

- [x] 3.1 Modify `Features/CurrentSituation/UpsertCutRecord/UpsertCutRecordHandler.cs` — reorder: resolve accounts + compute `BalanceInPrimary` + fail `ACCOUNT_NOT_FOUND` before any `SaveChanges`; call `BudgetExecutionSummaryQuery` + `CutTotalsCalculator`; wrap totals + header + `CutBankAccount` rows in one `BeginTransactionAsync`; ignore any total fields in the request body. Satisfies: CS-1 (all scenarios)
- [x] 3.2 Modify `Features/CurrentSituation/GetCutRecord/GetCutRecordHandler.cs` — existing-record path: SELECT header + 16 persisted columns, remove LINQ sums and the execution CTE call, populate `CutTotalsDto`/`BudgetExecutionSummaryDto` from stored columns; draft path: call `BudgetExecutionSummaryQuery` + `CutTotalsCalculator` (unchanged live behavior). Satisfies: CS-2 (all scenarios)

---

## Phase 4: Backend Tests

- [ ] 4.1 Unit test `MyBudget.Features.Tests/Features/CurrentSituation/CutTotalsCalculatorTests.cs` — CS-6 table case (500/200/300 → 500/200/500); zero-exchange-rate guard; rounding half-away-from-zero on >2-decimal inputs; empty rows → `CutTotals.Zero`. Satisfies: CS-6
- [ ] 4.2 Delete `MyBudget.Features.Tests/Features/CurrentSituation/CutTotalsComputationTests.cs` — superseded by 4.1; its inline re-implementation duplicated arithmetic now centralized in `CutTotalsCalculator`
- [ ] 4.3 Extend `MyBudget.Integration.Tests/Features/CurrentSituation/CutRecordIntegrationTests.cs` — persisted totals equal freshly computed totals at save time. Satisfies: CS-6 "Totals computed correctly at save time"
- [ ] 4.4 Extend `CutRecordIntegrationTests.cs` — editing bank account balances or execution records after save does not change the saved cut's persisted totals. Satisfies: CS-6 "Snapshot unaffected by later data changes"
- [ ] 4.5 Extend `CutRecordIntegrationTests.cs` — re-save overwrites all 16 persisted totals. Satisfies: CS-1 "Re-save overwrites all 16 totals"
- [ ] 4.6 Extend `CutRecordIntegrationTests.cs` — PUT body with client-submitted total fields is ignored; server-computed values persisted instead. Satisfies: CS-1 "Client-submitted totals ignored"
- [ ] 4.7 Extend `CutRecordIntegrationTests.cs` — GET for an existing cut returns the 16 stored columns verbatim without re-running the aggregation/CTE. Satisfies: CS-2 "Existing cut returns persisted totals verbatim"
- [ ] 4.8 Extend `CutRecordIntegrationTests.cs` — draft GET (no persisted cut) still computes all 8 total concepts live. Satisfies: CS-2 "Draft computes all 8 total concepts live"
- [ ] 4.9 Extend `CutRecordIntegrationTests.cs`/`CurrentSituationTestBase.cs` — pre-seed rows with no persisted totals, run the migration, assert backfilled values equal pre-change `GetCutRecord` output; assert all 16 columns non-null post-migration. Satisfies: CS-9 (both scenarios)

---

## Phase 5: Frontend Regression + E2E

- [ ] 5.1 Re-run `frontend/src/features/current-situation/__tests__/useCutRecordStore.spec.ts` as a regression check — no code change; confirms DTO shape unchanged
- [ ] 5.2 Create `frontend/e2e/current-situation/cut-totals-snapshot.spec.ts` — save a cut, mutate an execution record in the active period, reload the cut, assert displayed totals unchanged. Satisfies: CS-6 "Snapshot unaffected by later data changes" (E2E level)

---

## Phase 6: Documentation (optional, may slip to its own slice)

- [ ] 6.1 Add ES/EN i18n copy in `frontend/src/i18n/locales/{en,es}.json` documenting snapshot semantics ("totals reflect state as of last save"). Satisfies: proposal success criterion "Snapshot semantics documented … reflected in ES + EN UI copy"
