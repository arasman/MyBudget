# Dashboard demo-data seed scripts

Idempotent Postgres seed for exercising all 3 dashboard widgets (Lifetime
Trend, Average Behavior, BudgetLine Behavior) with realistic, internally
consistent data. Originally written to unblock manual testing of the
`dashboard` change (Average Behavior needs ≥2 periods with CutRecords to
stop showing "insufficient history"). Also reusable as-is to initialize a
cloud environment with demo data — pure SQL, no app/API step required.

## Files

- `seed_dashboard_demo.sql` — cleans up any prior run, then recreates
  everything. Safe to re-run anytime (wrapped in one transaction: either
  the whole reseed lands or nothing changes).
- `cleanup_dashboard_demo.sql` — same scoped cleanup only, standalone.

## How to run

Target database: `mybudget` (dev), user `mybudget` — **not**
`mybudget_test`/`mybudget_e2e`, those are for automated tests only.

**pgAdmin**: connect to `localhost:5432`, open the `mybudget` database,
Query Tool → File → Open → `seed_dashboard_demo.sql` → F5.

**CLI**:
```
docker exec -i -e PGPASSWORD=mybudget project-postgres-1 psql -U mybudget -d mybudget < seed_dashboard_demo.sql
```

Requires EF migrations already applied to the target database
(`dotnet ef database update --project src/MyBudget.Features --startup-project src/MyBudget.Api`,
idempotent — no-ops if already up to date).

## Demo login

```
Email:    seed-demo@mybudget.local
Password: DemoPass123!
```

Real bcrypt hash (workFactor 12, generated via `pgcrypto`'s `crypt()`/`gen_salt('bf', 12)`),
matching `RegisterUserHandler.cs` — this account logs in through the app normally.

## Scope and safety

Cleanup is scoped entirely to the `seed-demo@mybudget.local` owner (matched
by email, cascading through their one budget). It never touches any other
user, budget, or data — safe to run against a database that already has
real/other test data.

## What gets generated

1 owner, 1 budget ("Dashboard Demo Budget"), 2 cycles (2025 inactive, 2026
active — only one cycle may be active at a time), 24 periods (12/cycle), 3
category groups, 6 categories, 3 bank accounts, 18 budget lines (+ 1 open
revision each), ~3,240 execution records, 240 cut records (10 per period,
distinct dates within the month), 720 cut-bank-account snapshots.

## Design notes

- **Currencies**: GTQ (`11111111-…`) default, USD (`22222222-…`) alternate,
  fixed exchange rate 7.75 GTQ/USD — reused for `Cycle.ExchangeRate`,
  `CutRecord.ExchangeRate`, and the one USD bank account's conversion.
  ExecutionRecords/BudgetLineRevisions are all GTQ on purpose (no currency
  mismatch introduced — the PR6 mismatch guard stays quiet unless exercised
  separately).
- **CutRecord totals are real aggregates**, not arbitrary numbers — computed
  per period from the actual seeded ExecutionRecords/BudgetLineRevisions/
  CutBankAccounts, following the exact formulas in `CutTotalsCalculator.cs`:
  `Remaining = TotalBudgeted - TotalRegistered`,
  `TotalDeudaEnCurso = Remaining + TotalNegative`,
  `TotalAvailable = TotalPositive`,
  `TotalNet = TotalPositive - TotalDeudaEnCurso`,
  `*Alt = ROUND(Primary / ExchangeRate, 2)` (division, not multiplication).
- **Determinism**: every "random-looking" value (execution amounts, entry
  type mix, cut-bank balances, cut dates) is derived from plain integer
  arithmetic over stable indices, not `random()`/`now()` — re-running after
  cleanup always reproduces the same aggregated totals (only the generated
  UUIDs differ between runs).
- `BudgetLine.StartDate/EndDate` and `BudgetLineRevision.ValidFrom/ValidTo`
  are `TEXT` (ISO strings), not native `date` — unlike `Cycle`/`Period`/
  `CutRecord` date columns. Don't mix the two when editing this script.
- `gen_random_uuid()` is native since PG13. `pgcrypto` is required only for
  `crypt()`/`gen_salt()` (the login hash) — standard contrib module, bundled
  in the `postgres:16-alpine` image, no extra install needed.
