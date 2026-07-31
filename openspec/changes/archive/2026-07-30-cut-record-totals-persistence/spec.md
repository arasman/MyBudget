# Delta for current-situation

## MODIFIED Requirements

### Requirement: CS-1 Upsert Cut Record

The system MUST allow a `budget:operator` user to create or replace a cut record for a given date. Date format in the URL path MUST be `YYYY-MM-DD`. The upsert MUST fully replace all CutBankAccount rows for that date (delete then re-insert). An active period (StartDate ≤ CutDate ≤ EndDate, cycle is active, period is not closed) MUST exist; otherwise the request MUST be rejected with 422. ExchangeRate MUST be a positive decimal.

The upsert MUST also compute all 16 total columns (8 concepts × primary/alternate — see CS-6) server-side from the submitted balances, the active period's execution data, and ExchangeRate, and persist them on the CutRecord. The request body MUST NOT be read for total fields; any total values present in the request MUST be ignored. Each upsert (create or replace) MUST fully overwrite all 16 previously persisted totals with freshly computed values (full-replace, not merge).
(Previously: upsert only wrote CutBankAccount balances; no totals were persisted on CutRecord.)

#### Scenario: Successful upsert (create)

- GIVEN no cut record exists for the date, an active period covers the date
- WHEN PUT `/api/budgets/{id}/cut-records/2026-07-28` with exchangeRate and balances
- THEN 200 OK is returned and the record is persisted

#### Scenario: Successful upsert (replace)

- GIVEN an existing cut record for the date
- WHEN PUT with new balances
- THEN all previous CutBankAccount rows are replaced with the new values

#### Scenario: No active period for cut date

- GIVEN no active period covers the requested date
- WHEN PUT `/api/budgets/{id}/cut-records/{date}`
- THEN 422 Unprocessable Entity is returned

#### Scenario: Non-operator role rejected

- GIVEN a `budget:read` user
- WHEN PUT `/api/budgets/{id}/cut-records/{date}`
- THEN 403 Forbidden is returned

#### Scenario: Duplicate date enforced at DB level

- GIVEN two concurrent upserts for the same BudgetId and CutDate
- WHEN both reach the database
- THEN the UNIQUE INDEX on (BudgetId, CutDate) ensures only one record exists

#### Scenario: All 16 totals computed and persisted on save

- GIVEN valid balances, an active period, and ExchangeRate
- WHEN PUT `/api/budgets/{id}/cut-records/{date}`
- THEN all 16 total columns are computed server-side and persisted on the CutRecord row

#### Scenario: Client-submitted totals ignored

- GIVEN a PUT request body that includes total fields (e.g. `totalPositive`) alongside balances
- WHEN the upsert is processed
- THEN the submitted total values are ignored and the server-computed values are persisted instead

#### Scenario: Re-save overwrites all 16 totals

- GIVEN an existing cut record with previously persisted totals
- WHEN PUT is called again with different balances or under different execution data
- THEN all 16 persisted totals are recomputed and overwrite the previous values entirely

---

### Requirement: CS-2 Get Cut Record

The system MUST return the cut record for a given date. If a persisted cut record exists for the date, the response MUST return all 16 persisted total columns (see CS-6) read directly from storage; the bank-account aggregation and the execution-summary query MUST NOT be re-executed for an existing record. If the record does not exist, the system MUST return a draft response pre-populated with currently-active bank accounts (DeletedAt IS NULL) and balance 0, with all 8 total concepts computed live from current data, exactly as before this change. If a previous cut exists, the draft MUST copy balances for accounts present in both; newly-added accounts (not in previous cut) MUST use balance 0; accounts soft-deleted since the last cut MUST be excluded. If no cut has ever existed for the budget, the draft MUST include all active accounts with balance 0.

The draft response MUST include the live budget execution summary for the active period at cut date: TotalBudgeted, TotalRegistered, Remaining. If no active period covers the cut date, these fields MUST be returned as zero (no error).
(Previously: totals and execution summary were always recomputed at read time, for both existing and draft cuts.)

#### Scenario: Existing cut returns persisted totals verbatim

- GIVEN a cut record exists for the date
- WHEN GET `/api/budgets/{id}/cut-records/2026-07-28`
- THEN 200 OK is returned with the 16 persisted totals read directly from storage, without recomputing bank-account aggregation or the execution-summary query

#### Scenario: Draft from previous cut

- GIVEN no cut exists for 2026-07-28, a cut exists for 2026-07-25
- WHEN GET `/api/budgets/{id}/cut-records/2026-07-28`
- THEN a draft is returned with balances cloned from 2026-07-25 for matching accounts

#### Scenario: Draft computes all 8 total concepts live

- GIVEN no persisted cut exists for the date
- WHEN GET `/api/budgets/{id}/cut-records/{date}`
- THEN all 8 total concepts (primary + alternate) are computed live from current bank-account and execution data, same as before this change

#### Scenario: Newly-added account gets zero balance in draft

- GIVEN account A existed in last cut, account B was created after last cut
- WHEN GET returns a draft
- THEN account A has its previous balance, account B has balance 0

#### Scenario: Soft-deleted account excluded from draft

- GIVEN account C existed in last cut but was soft-deleted before today
- WHEN GET returns a draft
- THEN account C does not appear in the draft

#### Scenario: First cut ever — empty draft

- GIVEN no prior cut exists for the budget
- WHEN GET `/api/budgets/{id}/cut-records/{date}`
- THEN a draft is returned with all active accounts and balance 0

#### Scenario: No active period — execution summary zeroed

- GIVEN the cut date falls outside all active periods
- WHEN GET `/api/budgets/{id}/cut-records/{date}` (draft)
- THEN TotalBudgeted, TotalRegistered, Remaining are all 0

---

### Requirement: CS-6 Cut Totals

The system MUST compute and persist 16 total columns on CutRecord (8 concepts × primary/alternate) at upsert time (see CS-1), not at query time. All 16 columns MUST use `decimal(18,2)` precision, consistent with `CutBankAccount.BalanceInPrimary` (CS-5).

| Concept | Primary column | Alt column | Source |
|---|---|---|---|
| Total Assets | TotalPositive | TotalPositiveAlt | SUM(BalanceInPrimary) WHERE IsPositive=true |
| Total Liabilities | TotalNegative | TotalNegativeAlt | SUM(BalanceInPrimary) WHERE IsPositive=false |
| Total Debt | TotalDeudaEnCurso | TotalDeudaEnCursoAlt | Remaining + TotalNegative |
| Total Budgeted | TotalBudgeted | TotalBudgetedAlt | Execution summary, active period |
| Total Registered | TotalRegistered | TotalRegisteredAlt | Execution summary, active period |
| Budget Commitment | Remaining | RemainingAlt | Execution summary, active period |
| Total Available | TotalAvailable | TotalAvailableAlt | = TotalPositive (denormalized) |
| Total Net | TotalNet | TotalNetAlt | = TotalPositive − TotalDeudaEnCurso (denormalized) |

Once a cut is saved, these 16 values are frozen (snapshot semantics): subsequent changes to bank account balances or execution records MUST NOT alter a saved cut's persisted totals; only an explicit re-save (upsert) of that cut refreshes them.
(Previously: only 3 fields — TotalCuentasQueSuman, TotalCuentasQueRestan, TotalDeudaEnCurso — computed live at GET time from CutBankAccount rows; field names are corrected here to match the persisted/DTO names used elsewhere in this spec and codebase.)

#### Scenario: Totals computed correctly at save time

- GIVEN accounts A (IsPositive=true, BalanceInPrimary=500) and B (IsPositive=false, BalanceInPrimary=200), Remaining=300
- WHEN the cut is saved (upsert)
- THEN TotalPositive=500, TotalNegative=200, TotalDeudaEnCurso=500 are persisted

#### Scenario: Snapshot unaffected by later data changes

- GIVEN a cut saved on 2026-07-28 with persisted totals
- WHEN a bank account balance or an execution record affecting that period is edited after 2026-07-28's save
- THEN the persisted totals on the 2026-07-28 cut remain unchanged until that cut is explicitly re-saved

#### Scenario: Rounding precision

- GIVEN computed values with more than 2 decimal digits
- WHEN totals are persisted
- THEN each of the 16 columns is stored as `decimal(18,2)`, matching `BalanceInPrimary`'s precision

---

## ADDED Requirements

### Requirement: CS-9 Migration Backfill for Persisted Totals

The system MUST provide a one-time migration that backfills all 16 total columns for every existing CutRecord row created before this change. Backfilled values MUST be computed using the same logic GetCutRecordHandler used prior to this change (bank-account aggregation for the 6 balance-derived totals, the execution-summary query for TotalBudgeted/TotalRegistered/Remaining, and the existing TotalAvailable/TotalNet formulas). After migration, all 16 columns MUST be non-nullable; the read path MUST NOT retain a live-fallback branch for missing totals.

#### Scenario: Existing rows backfilled correctly

- GIVEN CutRecord rows saved before this change, with no persisted totals
- WHEN the migration runs
- THEN each row's 16 total columns equal the values GetCutRecord would have computed for that row prior to this change

#### Scenario: Columns non-nullable post-migration

- GIVEN the migration has completed
- WHEN any CutRecord row is queried
- THEN all 16 total columns have non-null decimal values
