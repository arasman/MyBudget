# Tasks: Current Situation (Periodic Financial Snapshot)

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 1 200 – 1 800 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 → Backend foundation + BankAccount slices + CutRecord slices; PR 2 → Frontend |
| Delivery strategy | ask-on-risk |
| Chain strategy | feature-branch-chain |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: feature-branch-chain
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Focused test command | Runtime harness | Rollback boundary |
|------|------|-----------|----------------------|-----------------|-------------------|
| 1 | Backend: entities, EF config, migration, AppDbContext | PR 1 | `dotnet ef migrations list` | N/A — migration-only, no HTTP surface yet | Drop migration + remove 3 entity files + remove DbSets |
| 2 | Backend: BankAccount slices + unit + integration tests | PR 1 (cont.) | `dotnet test --filter Category=BankAccounts` | `POST /api/budgets/{id}/bank-accounts` via integration harness | Remove Features/BankAccounts/ |
| 3 | Backend: CutRecord slices + unit + integration tests | PR 1 (cont.) | `dotnet test --filter Category=CutRecord` | `PUT /api/budgets/{id}/cut-records/{date}` via integration harness | Remove Features/CurrentSituation/ |
| 4 | Frontend: BankAccount CRUD feature | PR 2 | `vitest run src/features/bank-accounts` | Navigate to budget config, create/edit/delete an account | Remove frontend/src/features/bank-accounts/ |
| 5 | Frontend: CurrentSituation view + i18n + E2E | PR 2 (cont.) | `vitest run src/features/current-situation && npx playwright test e2e/current-situation` | Navigate to /budgets/:id/current-situation, enter a cut | Remove frontend/src/features/current-situation/ + revert router/i18n |

---

## Phase 1: Backend Foundation

- [x] 1.1 Create `src/MyBudget.Domain/Entities/BankAccount.cs` — entity with `Create`, `Update`, `SoftDelete` factory methods; fields: Id, BudgetId, CurrencyId, Alias, IsPositive, DisplayOrder, DeletedAt, CreatedAt, UpdatedAt
- [x] 1.2 Create `src/MyBudget.Domain/Entities/CutRecord.cs` — entity with `Create` and `Update` factory methods; fields: Id, BudgetId, CutDate, ExchangeRate, ProjectionsJson (nullable placeholder), CreatedAt, UpdatedAt
- [x] 1.3 Create `src/MyBudget.Domain/Entities/CutBankAccount.cs` — snapshot entity with `Create` factory; fields: Id, CutRecordId, BankAccountId, Alias, CurrencyId, IsPositive, DisplayOrder, Balance, BalanceInPrimary
- [x] 1.4 Create `src/MyBudget.Infrastructure/Persistence/Configurations/BankAccountConfiguration.cs` — EF config: FK RESTRICT to Budget + Currency; global query filter `DeletedAt == null`; unique index on (BudgetId, Alias) is NOT applied (alias can repeat)
- [x] 1.5 Create `src/MyBudget.Infrastructure/Persistence/Configurations/CutRecordConfiguration.cs` — EF config: FK RESTRICT to Budget; UNIQUE index on (BudgetId, CutDate)
- [x] 1.6 Create `src/MyBudget.Infrastructure/Persistence/Configurations/CutBankAccountConfiguration.cs` — EF config: FK CASCADE to CutRecord; FK RESTRICT to BankAccount; UNIQUE index on (CutRecordId, BankAccountId)
- [x] 1.7 Modify `src/MyBudget.Infrastructure/Persistence/AppDbContext.cs` — add `DbSet<BankAccount> BankAccounts`, `DbSet<CutRecord> CutRecords`, `DbSet<CutBankAccount> CutBankAccounts`
- [x] 1.8 Add EF Core migration `AddCurrentSituationTables` — creates BankAccounts, CutRecords, CutBankAccounts tables with all FKs and UNIQUE indexes in a single migration

---

## Phase 2: BankAccount API Slices

- [x] 2.1 Create `Features/BankAccounts/CreateBankAccount/CreateBankAccountCommand.cs` — record with BudgetId, Alias, CurrencyId, IsPositive, DisplayOrder
- [x] 2.2 Create `Features/BankAccounts/CreateBankAccount/CreateBankAccountValidator.cs` — Alias non-empty max 100 chars, CurrencyId not empty, DisplayOrder >= 0
- [x] 2.3 Create `Features/BankAccounts/CreateBankAccount/CreateBankAccountHandler.cs` — EF: verify BudgetId exists, verify CurrencyId exists, call `BankAccount.Create()`, add to context, SaveChanges; returns new Id
- [x] 2.4 Create `Features/BankAccounts/CreateBankAccount/CreateBankAccountEndpoint.cs` — `POST /api/budgets/{id}/bank-accounts`; `budget:admin` required; maps 201 Created
- [x] 2.5 Create `Features/BankAccounts/ListBankAccounts/ListBankAccountsQuery.cs` — record with BudgetId
- [x] 2.6 Create `Features/BankAccounts/ListBankAccounts/ListBankAccountsHandler.cs` — Dapper: `SELECT ... FROM BankAccounts WHERE BudgetId=@BudgetId AND DeletedAt IS NULL ORDER BY DisplayOrder`; returns list DTO
- [x] 2.7 Create `Features/BankAccounts/ListBankAccounts/ListBankAccountsEndpoint.cs` — `GET /api/budgets/{id}/bank-accounts`; `budget:read` sufficient
- [x] 2.8 Create `Features/BankAccounts/UpdateBankAccount/UpdateBankAccountCommand.cs` — record with BudgetId, AccountId, Alias, IsPositive, DisplayOrder (no CurrencyId)
- [x] 2.9 Create `Features/BankAccounts/UpdateBankAccount/UpdateBankAccountValidator.cs` — Alias non-empty max 100 chars, DisplayOrder >= 0
- [x] 2.10 Create `Features/BankAccounts/UpdateBankAccount/UpdateBankAccountHandler.cs` — EF: load active account (DeletedAt null), return 404 if missing, call `Update()`, SaveChanges
- [x] 2.11 Create `Features/BankAccounts/UpdateBankAccount/UpdateBankAccountEndpoint.cs` — `PUT /api/budgets/{id}/bank-accounts/{accountId}`; `budget:admin` required
- [x] 2.12 Create `Features/BankAccounts/DeleteBankAccount/DeleteBankAccountCommand.cs` — record with BudgetId, AccountId
- [x] 2.13 Create `Features/BankAccounts/DeleteBankAccount/DeleteBankAccountValidator.cs` — AccountId not empty
- [x] 2.14 Create `Features/BankAccounts/DeleteBankAccount/DeleteBankAccountHandler.cs` — EF: load account (any DeletedAt state), return 404 if missing, call `SoftDelete()` (sets DeletedAt = UtcNow), SaveChanges
- [x] 2.15 Create `Features/BankAccounts/DeleteBankAccount/DeleteBankAccountEndpoint.cs` — `DELETE /api/budgets/{id}/bank-accounts/{accountId}`; `budget:admin` required; returns 204

---

## Phase 3: CutRecord API Slices

- [x] 3.1 Create `Features/CurrentSituation/UpsertCutRecord/UpsertCutRecordCommand.cs` — record with BudgetId, CutDate (DateOnly), ExchangeRate, ProjectionsJson?, Accounts (list of BankAccountId + Balance)
- [x] 3.2 Create `Features/CurrentSituation/UpsertCutRecord/UpsertCutRecordValidator.cs` — CutDate valid, ExchangeRate > 0, each BankAccountId not empty, each Balance >= 0
- [x] 3.3 Create `Features/CurrentSituation/UpsertCutRecord/UpsertCutRecordHandler.cs` — EF: (a) check active period covers CutDate via Dapper CTE, 422 if none; (b) load-or-create CutRecord; (c) delete existing CutBankAccounts for this record; (d) for each input account: load BankAccount, compute BalanceInPrimary (primary→same, alternate→Balance×ExchangeRate); (e) insert CutBankAccount rows; (f) SaveChanges
- [x] 3.4 Create `Features/CurrentSituation/UpsertCutRecord/UpsertCutRecordEndpoint.cs` — `PUT /api/budgets/{id}/cut-records/{date}`; `budget:operator` required; returns 200
- [x] 3.5 Create `Features/CurrentSituation/GetCutRecord/GetCutRecordQuery.cs` — record with BudgetId, CutDate
- [x] 3.6 Create `Features/CurrentSituation/GetCutRecord/GetCutRecordHandler.cs` — Dapper: CTE `active_period` (Period+Cycle date range + active check), CTE `execution_summary` (SUM budgeted/registered), CTE `last_cut` (latest CutRecord before requested date), LEFT JOIN active BankAccounts against last_cut balances; compute Totals (TotalPositive, TotalNegative, TotalDeudaEnCurso + alt versions); returns `GetCutRecordResponse` with IsDraft flag
- [x] 3.7 Create `Features/CurrentSituation/GetCutRecord/GetCutRecordEndpoint.cs` — `GET /api/budgets/{id}/cut-records/{date}`; `budget:read` sufficient; returns 200
- [x] 3.8 Create `Features/CurrentSituation/ListCutDates/ListCutDatesQuery.cs` — record with BudgetId
- [x] 3.9 Create `Features/CurrentSituation/ListCutDates/ListCutDatesHandler.cs` — Dapper: `SELECT CutDate FROM CutRecords WHERE BudgetId=@BudgetId ORDER BY CutDate ASC`; returns `IReadOnlyList<DateOnly>`
- [x] 3.10 Create `Features/CurrentSituation/ListCutDates/ListCutDatesEndpoint.cs` — `GET /api/budgets/{id}/cut-records/dates`; `budget:read` sufficient
- [x] 3.11 Create `Features/CurrentSituation/DeleteCutRecord/DeleteCutRecordCommand.cs` — record with BudgetId, CutDate
- [x] 3.12 Create `Features/CurrentSituation/DeleteCutRecord/DeleteCutRecordValidator.cs` — CutDate valid format
- [x] 3.13 Create `Features/CurrentSituation/DeleteCutRecord/DeleteCutRecordHandler.cs` — EF: load CutRecord by (BudgetId, CutDate), return 404 if missing, `context.Remove(cutRecord)` (CASCADE removes CutBankAccounts), SaveChanges
- [x] 3.14 Create `Features/CurrentSituation/DeleteCutRecord/DeleteCutRecordEndpoint.cs` — `DELETE /api/budgets/{id}/cut-records/{date}`; `budget:operator` required; returns 204

---

## Phase 4: Backend Tests

- [x] 4.1 Unit test `CreateBankAccountValidator` — Alias empty → invalid; Alias 101 chars → invalid; DisplayOrder -1 → invalid; valid payload → passes (spec BA-1)
- [x] 4.2 Unit test `UpdateBankAccountValidator` — same field rules; CurrencyId field must NOT exist on command (spec BA-3)
- [x] 4.3 Unit test `UpsertCutRecordValidator` — ExchangeRate = 0 → invalid; ExchangeRate negative → invalid; valid → passes (spec CS-1)
- [x] 4.4 Unit test `BalanceInPrimary computation` — primary currency: BalanceInPrimary = Balance; alternate currency: BalanceInPrimary = Balance × ExchangeRate (spec CS-5)
- [x] 4.5 Unit test `CutTotals computation` — TotalPositive, TotalNegative, TotalDeudaEnCurso = Remaining + TotalNegative; alt-currency variants (spec CS-6)
- [x] 4.6 Integration test `CreateBankAccount` — 201 with valid payload (BA-1); 422 on alias > 100 chars (BA-1); 403 for operator role (BA-1)
- [x] 4.7 Integration test `ListBankAccounts` — returns only active accounts ordered by DisplayOrder (BA-2); soft-deleted excluded (BA-2); read role returns 200 (BA-2)
- [x] 4.8 Integration test `UpdateBankAccount` — 200 persists alias change (BA-3); 404 for deleted account (BA-3)
- [x] 4.9 Integration test `DeleteBankAccount` — 204 sets DeletedAt (BA-4); existing CutBankAccount rows unaffected (BA-4)
- [x] 4.10 Integration test `UpsertCutRecord (create)` — 200 with valid payload + active period (CS-1); 422 when no active period (CS-1); 403 for read role (CS-1)
- [x] 4.11 Integration test `UpsertCutRecord (replace)` — re-PUT replaces all CutBankAccount rows (CS-1)
- [x] 4.12 Integration test `GetCutRecord (existing)` — returns persisted balances + execution summary (CS-2); IsDraft = false
- [x] 4.13 Integration test `GetCutRecord (draft — first ever)` — IsDraft = true; all active accounts with Balance = 0 (CS-2)
- [x] 4.14 Integration test `GetCutRecord (draft — cloned)` — account from previous cut cloned; new account gets 0; soft-deleted account excluded (CS-2)
- [x] 4.15 Integration test `GetCutRecord (no active period)` — execution summary TotalBudgeted/TotalRegistered/Remaining all 0 (CS-2)
- [x] 4.16 Integration test `ListCutDates` — returns ascending dates; empty list when no cuts (CS-3)
- [x] 4.17 Integration test `DeleteCutRecord` — 204 removes record + CutBankAccount rows (CS-4); 404 on non-existent date (CS-4); 403 for read role (CS-4)

---

## Phase 5: Frontend — BankAccount Feature

- [x] 5.1 Create `frontend/src/features/bank-accounts/api/bankAccountApi.ts` — typed functions: `createBankAccount`, `listBankAccounts`, `updateBankAccount`, `deleteBankAccount` calling `/api/budgets/{budgetId}/bank-accounts`
- [x] 5.2 Create `frontend/src/features/bank-accounts/types/bankAccount.ts` — TS interfaces: `BankAccount`, `CreateBankAccountDto`, `UpdateBankAccountDto`
- [x] 5.3 Create `frontend/src/features/bank-accounts/store/useBankAccountStore.ts` — Pinia store: state `accounts[]`, `loading`, `error`; actions `fetchAccounts`, `createAccount`, `updateAccount`, `deleteAccount`
- [x] 5.4 Create `frontend/src/features/bank-accounts/views/BankAccountListView.vue` — presents account list, create/edit modal, delete confirm; accessible from budget config (spec CS-8)
- [x] 5.5 Create `frontend/src/features/bank-accounts/components/BankAccountForm.vue` — form for alias, currencyId (dropdown), isPositive (toggle), displayOrder; validates client-side
- [x] 5.6 Add `bankAccount.*` i18n keys to `frontend/src/i18n/locales/en.json` and `frontend/src/i18n/locales/es.json` (spec CS-8)
- [x] 5.7 Modify `frontend/src/router/index.ts` — add route `/budgets/:budgetId/bank-accounts` pointing to `BankAccountListView`
- [x] 5.8 Unit test `useBankAccountStore` — fetchAccounts populates state; createAccount appends; deleteAccount removes (Vitest + mock api)
- [x] 5.9 Unit test `BankAccountForm.vue` — renders fields; shows error on empty alias; emits `submit` with payload (@testing-library/vue)

---

## Phase 6: Frontend — CurrentSituation Feature

- [x] 6.1 Create `frontend/src/features/current-situation/api/cutRecordApi.ts` — typed functions: `getCutRecord`, `upsertCutRecord`, `listCutDates`, `deleteCutRecord`
- [x] 6.2 Create `frontend/src/features/current-situation/types/cutRecord.ts` — TS interfaces: `CutRecordResponse`, `CutBankAccountDto`, `BudgetExecutionSummaryDto`, `CutTotalsDto`, `UpsertCutRecordDto`
- [x] 6.3 Create `frontend/src/features/current-situation/store/useCutRecordStore.ts` — Pinia store: state `currentRecord`, `cutDates[]`, `currentDateIndex`, `loading`; actions `fetchCutDates`, `fetchCutRecord(date)`, `upsertCutRecord`, `deleteCutRecord`; computed `hasPrevious`, `hasNext`, `previousDate`, `nextDate`
- [x] 6.4 Create `frontend/src/features/current-situation/views/CurrentSituationView.vue` — top-level view: date navigator header, cut form, execution summary panel, totals panel; loads most recent cut on mount (spec CS-7)
- [x] 6.5 Create `frontend/src/features/current-situation/components/CutDateNavigator.vue` — prev/next buttons, current date display; disables prev when at first date, next when at last (spec CS-7)
- [x] 6.6 Create `frontend/src/features/current-situation/components/CutRecordForm.vue` — exchange rate input, account balance rows (alias, currency badge, isPositive indicator, balance input), save button; marks IsDraft badge when applicable
- [x] 6.7 Create `frontend/src/features/current-situation/components/ExecutionSummaryPanel.vue` — displays TotalBudgeted, TotalRegistered, Remaining from `BudgetExecutionSummaryDto`
- [x] 6.8 Create `frontend/src/features/current-situation/components/CutTotalsPanel.vue` — displays TotalPositive, TotalNegative, TotalDeudaEnCurso (primary + alt)
- [x] 6.9 Create `frontend/src/features/current-situation/components/DeleteCutModal.vue` — confirmation modal: text input requiring user to type the cut date; delete button enabled only when typed value matches (spec CS-4)
- [x] 6.10 Add `currentSituation.*` i18n keys to `frontend/src/i18n/locales/en.json` and `frontend/src/i18n/locales/es.json` (spec CS-7)
- [x] 6.11 Modify `frontend/src/router/index.ts` — add route `/budgets/:budgetId/current-situation` pointing to `CurrentSituationView`
- [x] 6.12 Modify `frontend/src/features/budget-structure/components/BudgetTabs.vue` — add "Current Situation" tab with correct route-link (spec CS-7)
- [x] 6.13 Unit test `useCutRecordStore` — fetchCutDates populates dates; date navigation increments/decrements index correctly; hasPrevious/hasNext computed correctly (Vitest)
- [x] 6.14 Unit test `CutDateNavigator.vue` — prev disabled at first date; next disabled at last date; emits `navigate` with correct date (Vitest + @testing-library/vue)
- [x] 6.15 Unit test `DeleteCutModal.vue` — delete button disabled until typed date matches; emits `confirm` on submit (Vitest + @testing-library/vue)

---

## Phase 7: E2E Tests

- [x] 7.1 E2E `bank-account-crud.spec.ts` — create account "Caja GTQ", verify appears in list; edit alias, verify updated; delete, verify removed from list and absent from new cut draft (spec CS-8)
- [x] 7.2 E2E `cut-record-create.spec.ts` — navigate to `/budgets/:id/current-situation`; enter balances + exchange rate; save; verify record persisted; reload and verify data (spec CS-1, CS-7)
- [x] 7.3 E2E `cut-record-navigation.spec.ts` — create 3 cuts on different dates; navigate prev/next; verify correct dates display in sequence (spec CS-7)
- [x] 7.4 E2E `cut-record-delete.spec.ts` — open delete modal; assert delete disabled without correct date typed; type date; confirm; verify cut removed from date list (spec CS-4)
- [x] 7.5 E2E `cut-draft-clone.spec.ts` — create cut for date A; add new account B after cut A; open cut for date B (later); verify A's balance cloned, B's balance = 0, soft-deleted account absent (spec CS-2)
