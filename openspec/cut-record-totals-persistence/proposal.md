# Proposal: Cut Record Totals Persistence

## Intent

A cut record ("corte") is meant to be a financial snapshot at a point in time, but today its totals are **recomputed on every read**: `GetCutRecordHandler` re-aggregates `CutBankAccount` balances and runs a live CTE over `ExecutionRecords`/`BudgetLineRevisions`/`Periods`/`Cycles`. Consequences: a saved cut silently changes value when unrelated execution data changes later; listing many cuts costs one multi-query read per date; and no cheap historical/aggregate query exists. Persisting totals at save time makes the snapshot real and unblocks the `dashboard` roadmap item (totals over time without per-row recompute).

## Scope

### In Scope

- **16 new persisted decimal columns on `CutRecord`** — the 8 concepts named in ROADMAP 10b, each in primary + alternate currency:

  | Concept (ROADMAP) | New entity property (+ Alt) | Today |
  |---|---|---|
  | Total Assets | `TotalPositive` | `CutTotalsDto.totalPositive` (already computed, has Alt) |
  | Total Liabilities | `TotalNegative` | `CutTotalsDto.totalNegative` (already computed, has Alt) |
  | Total Debt | `TotalDeudaEnCurso` | `CutTotalsDto.totalDeudaEnCurso` (already computed, has Alt) |
  | Total Budgeted | `TotalBudgeted` | `BudgetExecutionSummaryDto.totalBudgeted` (primary only today; Alt computed client-side via `/exchangeRate`) |
  | Total Registered | `TotalRegistered` | `BudgetExecutionSummaryDto.totalRegistered` (same as above) |
  | Budget Commitment | `Remaining` | `BudgetExecutionSummaryDto.remaining` (same as above) |
  | Total Available | `TotalAvailable` | frontend-only `computed()`, currently `= totalPositive`, **zero new information today** |
  | Total Net | `TotalNet` | frontend-only `computed()`, currently `= totalPositive - totalDeudaEnCurso`, **zero new information today** |

- EF configuration (`decimal(18,2)`, matching `CutBankAccount.BalanceInPrimary`) + additive migration with SQL backfill for existing rows
- `CutRecord.Create`/`Update` accept all 16 totals; **`UpsertCutRecordHandler` computes them server-side** and persists them
- Extract the existing bank-account aggregation *and* the execution-summary CTE from `GetCutRecordHandler` into shared, reusable queries so Upsert and the draft path use identical logic
- Alt-currency values for the 3 execution-derived concepts (Budgeted/Registered/Commitment) use the same `/exchangeRate` division already used for the existing 6 — no new conversion logic
- `TotalAvailable`/`TotalNet` computed server-side from the other 6 at save time and persisted as their own columns (denormalized on purpose — see Decision 4)
- `GetCutRecord` **existing-record** path reads all 16 stored columns directly (no bank-account aggregation, no execution CTE); **draft** path (no persisted cut yet) keeps computing everything live

### Out of Scope

- Extending `ListCutDates` to return totals (dashboard change, deferred)
- Any recompute/refresh job, background sync, or dashboard UI
- Client-submitted totals (explicitly rejected — see Approach)
- Changing the `TotalAvailable`/`TotalNet` *formulas* themselves (still `= Assets` and `= Assets - Debt` respectively for this change; a future change may compose `TotalAvailable` from more elements — that's exactly why it gets its own column now)

## Capabilities

### New Capabilities

- None

### Modified Capabilities

- `current-situation`: all 8 cut-record total concepts (primary + alternate) become persisted snapshot values written at upsert time, read back verbatim for existing cuts. `GetCutRecord`'s execution-summary CTE and bank-account aggregation are no longer executed for existing (non-draft) cuts.

## Approach

1. **Server-computed, never client-submitted.** ROADMAP 10b says "`CutRecordForm` sends computed totals in the upsert payload" — this proposal **overrides that**. Financial totals are computed by `UpsertCutRecordHandler` from data the server already trusts, following the `CutBankAccount.BalanceInPrimary` precedent (compute at write, persist, read back). The upsert payload shape is unchanged: the frontend keeps sending exchange rate + account balances only.
2. **Snapshot-at-save-time semantics (accepted tradeoff, not a bug).** `Remaining`, `TotalBudgeted`, `TotalRegistered`, and everything derived from them (`TotalDeudaEnCurso`, `TotalNet`) derive from execution records in the active period. Once a cut is saved, later execution edits do **not** change it. Contract: *a cut's totals reflect the state as of its last save/edit.* Re-saving the cut refreshes all 16. This must be documented in the spec and surfaced in UI copy.
3. **Full-replace on edit**, consistent with the handler's existing delete-and-reinsert of `CutBankAccount` rows: every save recomputes and overwrites all 16 totals.
4. **All 8 concepts persisted, including the 2 pure derivations (`TotalAvailable`, `TotalNet`).** Even though today `TotalAvailable = TotalPositive` and `TotalNet = TotalPositive - TotalDeudaEnCurso` exactly, the user's explicit call: a cut record represents 8 named financial concepts, and `TotalAvailable` in particular is expected to be composed of more elements in a future change. Persisting it as its own column now means that future formula change only touches `UpsertCutRecordHandler`, not every historical row or every dashboard query — the stored value stays correct for cuts saved under the old formula. Same denormalization precedent as `CutBankAccount.BalanceInPrimary`.
5. **No more execution CTE on the existing-record read path.** Because `TotalBudgeted`/`TotalRegistered`/`Remaining` are now persisted, `GetCutRecord` for an existing cut no longer needs to run the `ExecutionRecords`/`BudgetLineRevisions`/`Periods`/`Cycles` CTE at all — it's a straight column read. This removes the internal-consistency risk flagged in the original exploration (frozen `TotalDeudaEnCurso` next to live `Remaining`) because *both* are now frozen together, consistently.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `SharedKernel/Entities/CutRecord.cs` | Modified | 16 total properties (8 concepts × primary/alt); `Create`/`Update` signatures |
| `Persistence/Configurations/CutRecordConfiguration.cs` | Modified | `HasPrecision(18, 2)` per column |
| `Migrations/` | New | Additive columns + backfill (pattern: `AddBudgetLineDescription`) |
| `CurrentSituation/UpsertCutRecord/UpsertCutRecordHandler.cs` | Modified | Compute + persist all 16 totals |
| `CurrentSituation/GetCutRecord/GetCutRecordHandler.cs` | Modified | Existing-record path reads all 16 stored columns, no aggregation/CTE; draft path unchanged (still live) |
| `CurrentSituation/` (shared) | New | Extracted bank-account totals query + execution-summary query, reused by Upsert and the draft read path |
| `frontend/src/features/current-situation/` | Unchanged | Response shape identical (same DTO field names); only UI copy on snapshot semantics |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| All totals frozen at save time, including execution-derived ones | High (by design) | Documented snapshot contract; re-save refreshes; UI copy states "as of last save" |
| Existing cut rows have no totals after migration | High | Migration backfills via the same aggregation + CTE SQL; verify row counts post-migration |
| Get/Upsert aggregation logic drifts apart | Medium | Single extracted query per source (bank accounts, execution summary), reused by both paths; integration test asserts persisted == freshly computed on save |
| `TotalAvailable`/`TotalNet` formula hardcoded twice (entity + old frontend `computed()`) during transition | Low | Frontend switches to reading the persisted fields directly once backend ships; no parallel formula maintenance after cutover |
| Rounding differences vs. previous read-time values | Low | Same `decimal(18,2)` precision as `BalanceInPrimary`; no new arithmetic beyond existing `/exchangeRate` division |

## Rollback Plan

Single revert: down migration drops the 16 columns; handler changes revert to read-time computation (bank-account aggregation + execution CTE). No data loss risk — every persisted total is recomputable from `CutBankAccount` rows and `ExecutionRecords`. Frontend is untouched at the DTO level, so no coordinated rollback needed.

## Dependencies

- `current-situation` (archived ✅) — entities, handlers, and the aggregation logic being reused
- Unblocks: ROADMAP #11 `dashboard` (historical/aggregate totals queries over all 8 concepts without per-row recompute)

## Decisions (confirmed by user 2026-07-30)

1. **Column scope — 16 (8 concepts × primary/alternate), matching ROADMAP 10b literally.** Supersedes an earlier draft of this proposal that incorrectly scoped only the 6 `CutTotalsDto` fields — that draft was written and shown to the user before confirmation; the user caught the gap against the ROADMAP text. All 8 named concepts (Assets, Liabilities, Budgeted, Registered, Budget Commitment, Available, Debt, Net) get persisted columns.
2. **`TotalAvailable`/`TotalNet` persisted too, despite being pure derivations today.** Explicit user reasoning: `TotalAvailable` is expected to be composed of more elements in a future change, so it needs its own column now rather than being recomputed forever from a fixed formula. Consistent with treating the cut record as representing exactly 8 first-class financial concepts.
3. **Column naming — DTO-aligned, extended to all 3 source conventions.** The 6 pre-existing concepts keep `CutTotalsDto` names (`TotalPositive`, `TotalPositiveAlt`, etc.). The execution trio keeps `BudgetExecutionSummaryDto` names (`TotalBudgeted`, `TotalRegistered`, `Remaining`, + `Alt`). `TotalAvailable`/`TotalNet` keep the existing frontend `computed()` names (there is no other backend precedent for them). Not ROADMAP's `TotalAssetsInPrimary`/`InAlternate` suggestion.
4. **Migration — non-nullable + SQL backfill.** Columns are `NOT NULL`; a one-time backfill migration computes and populates values for existing cut rows using the same aggregation + CTE logic being extracted. No nullable/live-fallback branch in the read path.

## Success Criteria

- [ ] `CutRecord` persists all 16 totals; migration applies and backfills existing rows
- [ ] `UpsertCutRecordHandler` computes all 16 totals server-side; upsert request contract unchanged (no client-supplied totals)
- [ ] `GetCutRecord` for an existing cut returns all 16 stored values without re-running the bank-account aggregation or the execution-summary CTE
- [ ] `GetCutRecord` draft path (no persisted cut) still returns live-computed totals for all 8 concepts
- [ ] Re-saving a cut recomputes and overwrites all 16 totals
- [ ] Integration test: persisted totals equal freshly computed totals at save time; editing bank account balances or execution records afterwards does **not** change the saved cut's persisted totals
- [ ] Snapshot semantics documented in spec and reflected in ES + EN UI copy
