# Proposal: Current Situation (Periodic Financial Snapshot)

## Intent

Users need a daily financial snapshot ("cut record") per budget showing budget execution status and bank account balances at a point in time. No mechanism exists today to record or track financial position across time. This feature introduces the cut record workflow, a bank account catalog, and balance snapshots.

## Scope

### In Scope
- `BankAccount` entity: budget-scoped catalog with alias, currency, polarity, soft-delete, display order
- `CutRecord` entity: one per budget per calendar day, owns exchange rate and projections placeholder
- `CutBankAccount` entity: balance snapshot per account per cut, with BalanceInPrimary
- API slices: CRUD for BankAccount (4 slices), Upsert/Get/ListDates/Delete for CutRecord (4 slices)
- Budget execution summary: query-time computation of TotalBudgeted, TotalRegistered, Remaining for the active period at cut date
- Clone-from-previous: new cut pre-populates from currently-active (non-deleted) bank accounts with balance 0; newly-added accounts included
- Server-side guard: API blocks cut creation if no active period covers the cut date
- Frontend: `current-situation` feature with date navigation, cut form, bank account sections, execution summary
- Delete cut: hard delete with confirmation modal (user types cut date to confirm)
- i18n: ES + EN keys for all new UI
- Integration + unit tests per strict TDD

### Out of Scope
- Layer 2 projections (receivables/payables) — `ProjectionsJson` column reserved but unused
- Linking `ExecutionRecord.AccountId` to `BankAccounts` table (remains opaque Guid)
- Historical trend charts or cross-cut comparison views
- Multi-currency exchange rate history or rate provider integration
- Mobile-specific layout

## Capabilities

### New Capabilities
- `current-situation`: Cut record lifecycle (create, read, list dates, delete), budget execution summary at cut date, bank account balance snapshots
- `bank-accounts`: Budget-scoped account catalog CRUD with soft-delete

### Modified Capabilities
- `budget-structure`: Period lookup query reused for active-period-at-date validation (no spec change, implementation extension only)

## Approach

Follow Approach A from exploration: three normalized tables (`CutRecord`, `BankAccount`, `CutBankAccount`). BankAccount is the catalog; CutBankAccount is the per-cut snapshot.

- **Backend**: VSA 4-file slice pattern. EF Core for writes, Dapper for reads. Upsert via `ON CONFLICT (BudgetId, CutDate) DO UPDATE`. Active-period lookup = `Period WHERE StartDate <= cutDate AND EndDate >= cutDate AND Cycle is active`. No-period case returns server error (400) blocking creation.
- **Exchange rate**: `CutRecord.ExchangeRate` = 1 unit alternate currency in primary currency units (same direction as `Cycle.ExchangeRate`). `BalanceInPrimary = Balance * ExchangeRate` when account currency is alternate; `BalanceInPrimary = Balance` when account currency is primary.
- **Clone logic**: On new cut, copy all currently-active BankAccounts (where `DeletedAt IS NULL`) as CutBankAccount rows with Balance = 0. Newly-added accounts included; soft-deleted accounts excluded.
- **Delete**: Hard delete CutRecord cascades to CutBankAccounts. Frontend shows warning modal requiring typed date confirmation.
- **Bank account soft-delete**: Always allowed. Hidden from new cuts but alias preserved in existing CutBankAccount snapshots.
- **Frontend**: New feature folder, Pinia store, tabbed view with BudgetTabs integration. Route: `/budgets/:budgetId/current-situation`.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `SharedKernel/Entities/` | New | CutRecord, CutBankAccount, BankAccount entities |
| `SharedKernel/Persistence/` | Modified | AppDbContext DbSets + EF configurations |
| `Migrations/` | New | Migration for 3 new tables + unique index |
| `Features/CurrentSituation/` | New | 4 cut record slices + budget execution summary query |
| `Features/BankAccounts/` | New | 4 bank account CRUD slices |
| `frontend/src/features/current-situation/` | New | Store, API, views, components (~8 components) |
| `frontend/src/router/` | Modified | New route registration |
| `frontend/src/i18n/locales/` | Modified | EN + ES keys |
| `tests/` | New | Integration + unit tests for all slices |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Exceeds 400-line PR budget | High | Chained PRs: PR1 backend (entities + migration + slices), PR2 frontend |
| No active period at cut date edge case | Medium | Server returns 400; UI disables save; clear error message |
| Exchange rate confusion (CutRecord vs Cycle) | Low | Distinct field names; UI labels clarify point-in-time vs cycle-default |
| Orphan CutBankAccount rows after BankAccount soft-delete | Low | By design: snapshots preserve historical alias; no cleanup needed |

## Rollback Plan

1. Revert migration with `dotnet ef migrations remove` or apply a down migration
2. Remove feature folders (backend + frontend) — no existing code is modified beyond AppDbContext DbSets and router
3. Remove i18n keys from locale files

## Dependencies

- Active Cycle + Period data must exist for a budget before cuts can be created
- Currency seed data (GTQ, USD, EUR) must be present

## Success Criteria

- [ ] User can create a bank account catalog for a budget (CRUD)
- [ ] User can create a cut record for a date, see budget execution summary for the active period
- [ ] User can enter bank account balances; BalanceInPrimary computed correctly per exchange rate direction
- [ ] New cut clones only currently-active accounts with zero balances
- [ ] API rejects cut creation when no period covers the cut date
- [ ] User can hard-delete a cut record via typed-date confirmation modal
- [ ] Soft-deleted bank accounts are excluded from new cuts but visible in historical snapshots
- [ ] Navigation between cut dates works (prev/next)
- [ ] All UI strings available in ES and EN
- [ ] Integration tests pass for all 8 API slices
