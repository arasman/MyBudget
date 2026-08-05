-- =============================================================================
-- cleanup_dashboard_demo.sql
--
-- Removes ALL demo data owned by the fixed dashboard-demo user
-- (seed-demo@mybudget.local), and nothing else. Safe to run standalone,
-- independently of seed_dashboard_demo.sql, and safe to re-run (no-op if the
-- demo owner does not exist).
--
-- Scope: every DELETE below is scoped either directly to the demo owner's
-- Budget(s) (via BudgetId) or, for CutBankAccounts (which has no BudgetId
-- column), via a join back to CutRecords -> Budgets. No other user/budget is
-- ever touched.
--
-- Target: PostgreSQL 16.
-- =============================================================================

BEGIN;

-- Resolve the demo owner's Budget(s) once, so every DELETE below shares the
-- exact same scope. Using a temp table (rather than repeating the
-- Users/Budgets subquery per statement) keeps the scope consistent even if
-- this script is ever extended with more than one demo Budget.
CREATE TEMP TABLE _cleanup_scope ON COMMIT DROP AS
SELECT b."Id" AS budget_id, u."Id" AS owner_id
FROM "Budgets" b
JOIN "Users" u ON u."Id" = b."OwnerId"
WHERE u."Email" = 'seed-demo@mybudget.local';

-- Children first, respecting FKs (deepest dependents deleted before parents).

-- CutBankAccounts has no BudgetId column; scope via CutRecords.
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

-- Currencies are shared EF HasData seed rows (GTQ/USD/EUR) — never deleted here.

DELETE FROM "Users"
WHERE "Email" = 'seed-demo@mybudget.local';

COMMIT;
