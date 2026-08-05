-- =============================================================================
-- seed_dashboard_demo.sql
--
-- Idempotent demo-data seed for the MyBudget dashboard (Tendencia Historica,
-- Comportamiento de Lineas Presupuestarias, Comportamiento Promedio).
--
-- What it does:
--   1) Deletes any pre-existing demo data owned by the fixed demo user
--      (seed-demo@mybudget.local) — same scoped cleanup as
--      cleanup_dashboard_demo.sql — so this script can be re-run freely.
--   2) Recreates: 1 owner, 1 budget, 2 cycles (24 periods), 3 category
--      groups / 6 categories / 18 budget lines (+ revisions), 3 bank
--      accounts, ~3000 execution records, 240 cut records (10 per period,
--      distinct dates) with 720 cut-bank-account snapshots — CutRecord
--      totals are aggregated for real from the seeded ExecutionRecords /
--      BudgetLineRevisions / CutBankAccounts, following the exact formulas
--      in CutTotalsCalculator.cs.
--
-- Never touches any other user/budget. Wrapped in a single transaction:
-- either the whole reseed lands, or nothing changes.
--
-- Target: PostgreSQL 16. Requires the pgcrypto extension (created below if
-- missing) to generate a real, loggable-in bcrypt hash for the demo user.
-- gen_random_uuid() itself is built-in since PG13 and needs no extension.
--
-- Demo login credentials (real bcrypt hash, workFactor 12, same as
-- RegisterUserHandler.cs — this account can log in through the app):
--   Email:    seed-demo@mybudget.local
--   Password: DemoPass123!
--
-- Fixed reference data used (already present via EF HasData, CurrencySeeds.cs):
--   GTQ '11111111-1111-1111-1111-111111111111' (default currency)
--   USD '22222222-2222-2222-2222-222222222222' (alternate currency)
--
-- Assumptions taken beyond what the handoff already decided (see report):
--   - Budget name: 'Dashboard Demo Budget'; owner FirstName/LastName: Demo/Owner.
--   - Fixed exchange rate 7.75 (GTQ per USD) used everywhere a rate is needed
--     (Cycle.ExchangeRate, CutRecord.ExchangeRate, USD BankAccount conversion).
--   - All BudgetLines: LineType = Expense (0); StartDate = '2025-01-01' (TEXT),
--     EndDate = NULL (open), matching the single open BudgetLineRevision.
--   - Periods in Cycle 2025 (inactive) are IsClosed = true; periods in Cycle
--     2026 (active) are IsClosed = false.
--   - ExecutionRecords/BudgetLineRevisions all use GTQ (no currency mismatch
--     introduced on purpose, so the currency-mismatch guard added in PR6
--     stays quiet unless the user wants to exercise it separately).
--   - Category/category-group/bank-account names are illustrative English
--     demo labels (no naming convention was specified).
--   - Determinism: every "random-looking" figure (execution amounts, entry
--     type mix, cut-bank balances, cut dates) is derived from plain integer
--     arithmetic over stable indices (period/line/cut/account position), not
--     from random()/now(), so re-running this script after a cleanup always
--     reproduces the same aggregated totals (only the generated UUIDs differ).
-- =============================================================================

BEGIN;

CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- -----------------------------------------------------------------------------
-- 0. Scoped cleanup (identical scope/logic to cleanup_dashboard_demo.sql)
-- -----------------------------------------------------------------------------

CREATE TEMP TABLE _cleanup_scope ON COMMIT DROP AS
SELECT b."Id" AS budget_id, u."Id" AS owner_id
FROM "Budgets" b
JOIN "Users" u ON u."Id" = b."OwnerId"
WHERE u."Email" = 'seed-demo@mybudget.local';

DELETE FROM "CutBankAccounts" cba
USING "CutRecords" cr
WHERE cba."CutRecordId" = cr."Id"
  AND cr."BudgetId" IN (SELECT budget_id FROM _cleanup_scope);

DELETE FROM "CutRecords"
WHERE "BudgetId" IN (SELECT budget_id FROM _cleanup_scope);

DELETE FROM "ExecutionRecords"
WHERE "BudgetId" IN (SELECT budget_id FROM _cleanup_scope);

DELETE FROM "BudgetLineRevisions"
WHERE "BudgetId" IN (SELECT budget_id FROM _cleanup_scope);

DELETE FROM "BudgetLines"
WHERE "BudgetId" IN (SELECT budget_id FROM _cleanup_scope);

DELETE FROM "BankAccounts"
WHERE "BudgetId" IN (SELECT budget_id FROM _cleanup_scope);

DELETE FROM "Categories"
WHERE "BudgetId" IN (SELECT budget_id FROM _cleanup_scope);

DELETE FROM "CategoryGroups"
WHERE "BudgetId" IN (SELECT budget_id FROM _cleanup_scope);

DELETE FROM "Periods"
WHERE "BudgetId" IN (SELECT budget_id FROM _cleanup_scope);

DELETE FROM "Cycles"
WHERE "BudgetId" IN (SELECT budget_id FROM _cleanup_scope);

DELETE FROM "BudgetMemberships"
WHERE "BudgetId" IN (SELECT budget_id FROM _cleanup_scope);

DELETE FROM "Budgets"
WHERE "Id" IN (SELECT budget_id FROM _cleanup_scope);

DELETE FROM "Users"
WHERE "Email" = 'seed-demo@mybudget.local';

DROP TABLE _cleanup_scope;

-- -----------------------------------------------------------------------------
-- 1. Owner (Users) + Budget + BudgetMembership (Owner role = 40)
-- -----------------------------------------------------------------------------

CREATE TEMP TABLE _owner ON COMMIT DROP AS
SELECT gen_random_uuid() AS id, 'seed-demo@mybudget.local'::varchar(320) AS email;

INSERT INTO "Users" (
  "Id", "Email", "PasswordHash", "FirstName", "LastName", "PreferredLocale",
  "LastLoginAt", "FailedLoginAttempts", "LockoutUntil", "PasswordChangedAt",
  "ForcePasswordChange", "CreatedAt", "UpdatedAt"
)
SELECT
  id, email,
  crypt('DemoPass123!', gen_salt('bf', 12)), -- real bcrypt hash, workFactor 12 — matches RegisterUserHandler.cs, so this account can log in
  'Demo', 'Owner', 'en',
  NULL, 0, NULL, NULL, false, now(), NULL
FROM _owner;

CREATE TEMP TABLE _budget ON COMMIT DROP AS
SELECT gen_random_uuid() AS id, (SELECT id FROM _owner) AS owner_id;

INSERT INTO "Budgets" ("Id", "Name", "OwnerId", "IsDeleted", "DeletedAt", "CreatedAt", "UpdatedAt")
SELECT id, 'Dashboard Demo Budget', owner_id, false, NULL, now(), NULL
FROM _budget;

INSERT INTO "BudgetMemberships" ("Id", "BudgetId", "UserId", "Role", "JoinedAt", "CreatedAt", "UpdatedAt")
SELECT gen_random_uuid(), (SELECT id FROM _budget), (SELECT id FROM _owner), 40, now(), now(), NULL;

-- -----------------------------------------------------------------------------
-- 2. Cycles (2025 inactive, 2026 active) + Periods (12 each, 24 total)
-- -----------------------------------------------------------------------------

CREATE TEMP TABLE _cycle ON COMMIT DROP AS
SELECT * FROM (
  VALUES
    (gen_random_uuid(), 'Cycle 2025', 2025, DATE '2025-01-01', DATE '2025-12-31', false, 0),
    (gen_random_uuid(), 'Cycle 2026', 2026, DATE '2026-01-01', DATE '2026-12-31', true,  1)
) AS t(id, name, cycle_year, start_date, end_date, is_active, cycle_offset);

INSERT INTO "Cycles" (
  "Id", "BudgetId", "Name", "StartDate", "EndDate", "IsActive",
  "DefaultCurrencyId", "AlternateCurrencyId", "ExchangeRate",
  "DeletedAt", "CreatedAt", "UpdatedAt"
)
SELECT
  id, (SELECT id FROM _budget), name, start_date, end_date, is_active,
  '11111111-1111-1111-1111-111111111111'::uuid,
  '22222222-2222-2222-2222-222222222222'::uuid,
  7.75,
  NULL, now(), NULL
FROM _cycle;

CREATE TEMP TABLE _period ON COMMIT DROP AS
SELECT
  gen_random_uuid() AS id,
  c.id AS cycle_id,
  c.is_active AS cycle_is_active,
  gs.period_number AS period_number,
  (c.cycle_offset * 12 + (gs.period_number - 1)) AS period_index,
  make_date(c.cycle_year, gs.period_number, 1) AS start_date,
  (make_date(c.cycle_year, gs.period_number, 1) + INTERVAL '1 month')::date - 1 AS end_date
FROM _cycle c
CROSS JOIN generate_series(1, 12) AS gs(period_number);

INSERT INTO "Periods" (
  "Id", "BudgetId", "CycleId", "Name", "PeriodNumber", "StartDate", "EndDate",
  "IsClosed", "DeletedAt", "CreatedAt", "UpdatedAt"
)
SELECT
  id, (SELECT id FROM _budget), cycle_id,
  'Period ' || period_number || ' - ' || to_char(start_date, 'YYYY'),
  period_number, start_date, end_date,
  NOT cycle_is_active, -- periods of the inactive (2025) cycle are closed; active (2026) cycle stays open
  NULL, now(), NULL
FROM _period;

-- -----------------------------------------------------------------------------
-- 3. CategoryGroups (3) + Categories (2 per group = 6) + BankAccounts (3)
-- -----------------------------------------------------------------------------

CREATE TEMP TABLE _category_group ON COMMIT DROP AS
SELECT * FROM (
  VALUES
    (gen_random_uuid(), 1, 'Housing & Utilities'),
    (gen_random_uuid(), 2, 'Food & Daily Living'),
    (gen_random_uuid(), 3, 'Lifestyle & Savings')
) AS t(id, grp_num, name);

INSERT INTO "CategoryGroups" ("Id", "BudgetId", "Name", "DisplayOrder", "DeletedAt", "CreatedAt", "UpdatedAt")
SELECT id, (SELECT id FROM _budget), name, grp_num, NULL, now(), NULL
FROM _category_group;

CREATE TEMP TABLE _category ON COMMIT DROP AS
SELECT
  gen_random_uuid() AS id,
  cg.id AS category_group_id,
  cg.grp_num,
  cn.cat_num,
  CASE cg.grp_num
    WHEN 1 THEN CASE cn.cat_num WHEN 1 THEN 'Rent' WHEN 2 THEN 'Utilities' END
    WHEN 2 THEN CASE cn.cat_num WHEN 1 THEN 'Groceries' WHEN 2 THEN 'Dining Out' END
    WHEN 3 THEN CASE cn.cat_num WHEN 1 THEN 'Entertainment' WHEN 2 THEN 'Savings' END
  END AS name,
  cn.cat_num AS display_order
FROM _category_group cg
CROSS JOIN generate_series(1, 2) AS cn(cat_num);

INSERT INTO "Categories" ("Id", "BudgetId", "CategoryGroupId", "Name", "DisplayOrder", "DeletedAt", "CreatedAt", "UpdatedAt")
SELECT id, (SELECT id FROM _budget), category_group_id, name, display_order, NULL, now(), NULL
FROM _category;

CREATE TEMP TABLE _bank_account ON COMMIT DROP AS
SELECT * FROM (
  VALUES
    (gen_random_uuid(), 'Main Checking',     true,  1, '11111111-1111-1111-1111-111111111111'::uuid, 0),
    (gen_random_uuid(), 'Savings Account',   true,  2, '11111111-1111-1111-1111-111111111111'::uuid, 1),
    (gen_random_uuid(), 'Credit Card (USD)', false, 3, '22222222-2222-2222-2222-222222222222'::uuid, 2)
) AS t(id, alias, is_positive, display_order, currency_id, acct_index);

INSERT INTO "BankAccounts" ("Id", "BudgetId", "CurrencyId", "Alias", "IsPositive", "DisplayOrder", "DeletedAt", "CreatedAt", "UpdatedAt")
SELECT id, (SELECT id FROM _budget), currency_id, alias, is_positive, display_order, NULL, now(), NULL
FROM _bank_account;

-- -----------------------------------------------------------------------------
-- 4. BudgetLines (3 per category = 18) + BudgetLineRevisions (1 per line, open)
-- -----------------------------------------------------------------------------

CREATE TEMP TABLE _budget_line ON COMMIT DROP AS
SELECT
  gen_random_uuid() AS id,
  c.id AS category_id,
  c.category_group_id,
  c.grp_num,
  c.cat_num,
  ln.line_num,
  ((c.grp_num - 1) * 6 + (c.cat_num - 1) * 3 + (ln.line_num - 1)) AS line_index,
  c.name || ' - Line ' || ln.line_num AS name,
  (300 + ((c.grp_num - 1) * 6 + (c.cat_num - 1) * 3 + (ln.line_num - 1)) * 129)::numeric(18,2) AS budgeted_amount
FROM _category c
CROSS JOIN generate_series(1, 3) AS ln(line_num);

INSERT INTO "BudgetLines" (
  "Id", "BudgetId", "CategoryGroupId", "CategoryId", "Name", "Description",
  "LineType", "StartDate", "EndDate", "DisplayOrder", "DeletedAt", "CreatedAt", "UpdatedAt"
)
SELECT
  id, (SELECT id FROM _budget), category_group_id, category_id, name, NULL,
  0, -- LineType = Expense for all demo lines (see assumptions above)
  '2025-01-01', NULL, line_num, NULL, now(), NULL
FROM _budget_line;

INSERT INTO "BudgetLineRevisions" (
  "Id", "BudgetId", "BudgetLineId", "BudgetedAmount", "CurrencyId", "ValidFrom", "ValidTo", "Note",
  "CreatedAt", "UpdatedAt"
)
SELECT
  gen_random_uuid(), (SELECT id FROM _budget), id, budgeted_amount,
  '11111111-1111-1111-1111-111111111111'::uuid, -- GTQ, fixed
  '2025-01-01', NULL, NULL,
  now(), NULL
FROM _budget_line;

-- -----------------------------------------------------------------------------
-- 5. ExecutionRecords: 5-10 per (Period x BudgetLine), ~3000 rows total
-- -----------------------------------------------------------------------------

CREATE TEMP TABLE _period_line ON COMMIT DROP AS
SELECT
  p.id AS period_id,
  p.period_index,
  p.start_date,
  p.end_date,
  bl.id AS budget_line_id,
  bl.line_index,
  bl.budgeted_amount,
  (5 + ((p.period_index * 18 + bl.line_index) % 6)) AS exec_count -- deterministic, 5..10
FROM _period p
CROSS JOIN _budget_line bl;

CREATE TEMP TABLE _execution_record ON COMMIT DROP AS
SELECT
  gen_random_uuid() AS id,
  pl.period_id,
  pl.budget_line_id,
  pl.start_date + ((pl.period_index + pl.line_index + gs.occurrence) % (pl.end_date - pl.start_date + 1)) AS operation_date,
  CASE
    WHEN ((pl.period_index + pl.line_index + gs.occurrence) % 11) = 0 THEN 3 -- DebitNote
    WHEN ((pl.period_index + pl.line_index + gs.occurrence) % 7) = 0 THEN 2  -- CreditNote
    ELSE 1                                                                   -- Expense
  END AS entry_type,
  ROUND(
    (pl.budgeted_amount / pl.exec_count)
    * (0.7 + (((pl.period_index * 13 + pl.line_index * 7 + gs.occurrence * 3) % 61) / 100.0)),
    2
  ) AS amount
FROM _period_line pl
CROSS JOIN LATERAL generate_series(1, pl.exec_count) AS gs(occurrence);

INSERT INTO "ExecutionRecords" (
  "Id", "BudgetId", "PeriodId", "BudgetLineId", "EntryType", "Amount", "Note",
  "CurrencyId", "ExchangeRate", "ExchangeRateTo", "AccountId", "PaymentMethodId",
  "OperationDate", "DeletedAt", "CreatedAt", "UpdatedAt"
)
SELECT
  id, (SELECT id FROM _budget), period_id, budget_line_id, entry_type, amount, NULL,
  '11111111-1111-1111-1111-111111111111'::uuid, -- GTQ, same as BudgetLineRevision (no currency mismatch)
  NULL, NULL, NULL, NULL,
  operation_date, NULL, now(), NULL
FROM _execution_record;

-- -----------------------------------------------------------------------------
-- 6. Per-period aggregates, computed for real from the tables just inserted
--    (TotalBudgeted: sum of all open BudgetLineRevisions; TotalRegistered:
--    sum of ExecutionRecords.Amount for that period).
-- -----------------------------------------------------------------------------

CREATE TEMP TABLE _period_totals ON COMMIT DROP AS
SELECT
  p.id AS period_id,
  p.period_index,
  p.start_date,
  p.end_date,
  (SELECT SUM("BudgetedAmount") FROM "BudgetLineRevisions" WHERE "BudgetId" = (SELECT id FROM _budget)) AS total_budgeted,
  COALESCE((SELECT SUM("Amount") FROM "ExecutionRecords" WHERE "PeriodId" = p.id), 0) AS total_registered
FROM _period p;

-- -----------------------------------------------------------------------------
-- 7. CutRecords: 10 per period (240 total), distinct CutDate within the
--    period's month. CutBankAccounts: 1 snapshot per (CutRecord, BankAccount)
--    (720 total), balances vary deterministically per cut/account.
-- -----------------------------------------------------------------------------

CREATE TEMP TABLE _cut_record ON COMMIT DROP AS
SELECT
  gen_random_uuid() AS id,
  pt.period_id,
  pt.period_index,
  cn.cut_num,
  pt.start_date + ((cn.cut_num - 1) * (pt.end_date - pt.start_date) / 9) AS cut_date, -- 10 distinct dates spread across the month
  pt.total_budgeted,
  pt.total_registered,
  (pt.total_budgeted - pt.total_registered) AS remaining
FROM _period_totals pt
CROSS JOIN generate_series(1, 10) AS cn(cut_num);

CREATE TEMP TABLE _cut_bank_balance ON COMMIT DROP AS
SELECT
  cr.id AS cut_record_id,
  ba.id AS bank_account_id,
  ba.alias,
  ba.currency_id,
  ba.is_positive,
  ba.display_order,
  bal.balance,
  CASE
    WHEN ba.currency_id = '11111111-1111-1111-1111-111111111111'::uuid THEN bal.balance
    ELSE ROUND(bal.balance * 7.75, 2)
  END AS balance_in_primary
FROM _cut_record cr
CROSS JOIN _bank_account ba
CROSS JOIN LATERAL (
  SELECT
    CASE
      WHEN ba.is_positive THEN (3000 + ((cr.period_index * 10 + cr.cut_num) * 7 + ba.acct_index * 13) % 2000)::numeric(18,2)
      ELSE (500 + ((cr.period_index * 10 + cr.cut_num) * 5 + ba.acct_index * 11) % 1500)::numeric(18,2)
    END AS balance
) AS bal;

CREATE TEMP TABLE _cut_totals ON COMMIT DROP AS
SELECT
  cr.id,
  cr.cut_date,
  cr.total_budgeted,
  cr.total_registered,
  cr.remaining,
  COALESCE(pos.total_positive, 0) AS total_positive,
  COALESCE(neg.total_negative, 0) AS total_negative
FROM _cut_record cr
LEFT JOIN (
  SELECT cut_record_id, SUM(balance_in_primary) AS total_positive
  FROM _cut_bank_balance
  WHERE is_positive
  GROUP BY cut_record_id
) pos ON pos.cut_record_id = cr.id
LEFT JOIN (
  SELECT cut_record_id, SUM(balance_in_primary) AS total_negative
  FROM _cut_bank_balance
  WHERE NOT is_positive
  GROUP BY cut_record_id
) neg ON neg.cut_record_id = cr.id;

-- Formulas below mirror CutTotalsCalculator.cs exactly:
--   Remaining         = TotalBudgeted - TotalRegistered            (already in _cut_totals.remaining)
--   TotalDeudaEnCurso = Remaining + TotalNegative
--   TotalAvailable    = TotalPositive
--   TotalNet          = TotalPositive - TotalDeudaEnCurso
--   *Alt              = ROUND(Primary / NULLIF(ExchangeRate, 0), 2)   (division, not multiplication)
INSERT INTO "CutRecords" (
  "Id", "BudgetId", "CutDate", "ExchangeRate", "ProjectionsJson",
  "TotalPositive", "TotalPositiveAlt",
  "TotalNegative", "TotalNegativeAlt",
  "TotalDeudaEnCurso", "TotalDeudaEnCursoAlt",
  "TotalBudgeted", "TotalBudgetedAlt",
  "TotalRegistered", "TotalRegisteredAlt",
  "Remaining", "RemainingAlt",
  "TotalAvailable", "TotalAvailableAlt",
  "TotalNet", "TotalNetAlt",
  "CreatedAt", "UpdatedAt"
)
SELECT
  id, (SELECT id FROM _budget), cut_date, 7.75, NULL,
  total_positive, ROUND(total_positive / NULLIF(7.75, 0), 2),
  total_negative, ROUND(total_negative / NULLIF(7.75, 0), 2),
  (remaining + total_negative), ROUND((remaining + total_negative) / NULLIF(7.75, 0), 2),
  total_budgeted, ROUND(total_budgeted / NULLIF(7.75, 0), 2),
  total_registered, ROUND(total_registered / NULLIF(7.75, 0), 2),
  remaining, ROUND(remaining / NULLIF(7.75, 0), 2),
  total_positive, ROUND(total_positive / NULLIF(7.75, 0), 2), -- TotalAvailable = TotalPositive
  (total_positive - (remaining + total_negative)), ROUND((total_positive - (remaining + total_negative)) / NULLIF(7.75, 0), 2), -- TotalNet
  now(), NULL
FROM _cut_totals;

INSERT INTO "CutBankAccounts" (
  "Id", "CutRecordId", "BankAccountId", "Alias", "CurrencyId", "IsPositive", "DisplayOrder",
  "Balance", "BalanceInPrimary", "CreatedAt", "UpdatedAt"
)
SELECT
  gen_random_uuid(), cut_record_id, bank_account_id, alias, currency_id, is_positive, display_order,
  balance, balance_in_primary, now(), NULL
FROM _cut_bank_balance;

COMMIT;
