# Apply Progress: current-situation (all phases 1–7)

## Status: COMPLETE

---

## PR1 — Backend (feat/cs-backend): COMPLETE

### Branch: feat/cs-backend

### Phase 1 — Backend Foundation (1.1–1.8): DONE
- Entities: BankAccount, CutRecord, CutBankAccount
- EF Configurations: BankAccountConfiguration, CutRecordConfiguration, CutBankAccountConfiguration
- AppDbContext: 3 new DbSets added
- Migration: AddCurrentSituationTables

### Phase 2 — BankAccount API Slices (2.1–2.15): DONE
- CreateBankAccount, ListBankAccounts, UpdateBankAccount, DeleteBankAccount (4-file VSA each)

### Phase 3 — CutRecord API Slices (3.1–3.14): DONE
- UpsertCutRecord, GetCutRecord, ListCutDates, DeleteCutRecord (4/3-file VSA each)

### Phase 4 — Backend Tests (4.1–4.17): DONE
- Unit tests: 25 new tests (CreateBankAccountValidator, UpdateBankAccountValidator, UpsertCutRecordValidator, BalanceInPrimary, CutTotals)
- Integration tests: 24 new tests across BankAccountIntegrationTests + CutRecordIntegrationTests
- All 222 integration tests pass (3 pre-existing skips)
- All 462 unit tests pass

### Key Implementation Notes (PR1)
- Route parameter MUST be `{id}` (not `{budgetId}`) — BudgetAuthorizationHandler reads `RouteValues["id"]`
- UpsertCutRecord: two-phase save (CutRecord first, CutBankAccounts second) to get the FK ID
- GetCutRecord: two Dapper queries (header + execution summary + accounts)
- Draft SQL: LEFT JOIN active BankAccounts against last_cut CTE for clone logic
- IntegrationTestFactory.CleanDatabaseAsync updated to include CutBankAccounts, CutRecords, BankAccounts

---

## PR2 — Frontend (feat/cs-frontend): COMPLETE

### Branch: feat/cs-frontend

### Phase 5 — Frontend BankAccount Feature (5.1–5.9): DONE

**Files created:**
- `Project/frontend/src/features/bank-accounts/types/bankAccount.ts` — BankAccount, CreateBankAccountDto, UpdateBankAccountDto
- `Project/frontend/src/features/bank-accounts/api/bankAccountApi.ts` — listBankAccounts, createBankAccount, updateBankAccount, deleteBankAccount
- `Project/frontend/src/features/bank-accounts/store/useBankAccountStore.ts` — Pinia composition store
- `Project/frontend/src/features/bank-accounts/views/BankAccountListView.vue` — list + CRUD modal UI
- `Project/frontend/src/features/bank-accounts/components/BankAccountForm.vue` — create/edit form with validation
- `Project/frontend/src/features/bank-accounts/__tests__/useBankAccountStore.spec.ts` — 5 unit tests (all passing)
- `Project/frontend/src/features/bank-accounts/__tests__/BankAccountForm.spec.ts` — 5 unit tests (all passing)

**Files modified:**
- `Project/frontend/src/router/index.ts` — added `BankAccounts` route at `/budgets/:budgetId/bank-accounts`
- `Project/frontend/src/i18n/locales/en.json` — added `bankAccount.*` namespace
- `Project/frontend/src/i18n/locales/es.json` — added `bankAccount.*` namespace

### Phase 6 — Frontend CurrentSituation Feature (6.1–6.15): DONE

**Files created:**
- `Project/frontend/src/features/current-situation/types/cutRecord.ts` — CutRecordResponse, CutBankAccountDto, BudgetExecutionSummaryDto, CutTotalsDto, UpsertCutRecordDto
- `Project/frontend/src/features/current-situation/api/cutRecordApi.ts` — getCutRecord, upsertCutRecord, listCutDates, deleteCutRecord
- `Project/frontend/src/features/current-situation/store/useCutRecordStore.ts` — Pinia store with computed hasPrevious/hasNext/previousDate/nextDate
- `Project/frontend/src/features/current-situation/views/CurrentSituationView.vue` — main view composing all components; loads most-recent cut on mount
- `Project/frontend/src/features/current-situation/components/CutDateNavigator.vue` — prev/next navigation with disabled states
- `Project/frontend/src/features/current-situation/components/CutRecordForm.vue` — exchange rate + account balance rows split by isPositive, draft badge
- `Project/frontend/src/features/current-situation/components/ExecutionSummaryPanel.vue` — read-only TotalBudgeted/TotalRegistered/Remaining
- `Project/frontend/src/features/current-situation/components/CutTotalsPanel.vue` — TotalPositive/TotalNegative/TotalDeudaEnCurso with alt currency
- `Project/frontend/src/features/current-situation/components/DeleteCutModal.vue` — date-confirmation modal; delete enabled only when typed date matches exactly
- `Project/frontend/src/features/current-situation/__tests__/useCutRecordStore.spec.ts` — 8 unit tests (all passing)
- `Project/frontend/src/features/current-situation/__tests__/CutDateNavigator.spec.ts` — 6 unit tests (all passing)
- `Project/frontend/src/features/current-situation/__tests__/DeleteCutModal.spec.ts` — 6 unit tests (all passing)

**Files modified:**
- `Project/frontend/src/router/index.ts` — added `CurrentSituation` route at `/budgets/:budgetId/current-situation`
- `Project/frontend/src/features/budget-structure/components/BudgetTabs.vue` — added "Current Situation" tab with isActive support
- `Project/frontend/src/i18n/locales/en.json` — added `currentSituation.*` namespace
- `Project/frontend/src/i18n/locales/es.json` — added `currentSituation.*` namespace

### Phase 7 — E2E Tests (7.1–7.5): DONE

**Files created:**
- `Project/frontend/e2e/bank-accounts/helpers.ts` — seedBudgetCtx, createBankAccount, upsertCutRecord helpers
- `Project/frontend/e2e/bank-accounts/bank-account-crud.spec.ts` — create, list, update, soft-delete; draft exclusion (CS-8)
- `Project/frontend/e2e/current-situation/helpers.ts` — re-exports from bank-accounts helpers
- `Project/frontend/e2e/current-situation/cut-record-create.spec.ts` — draft pre-population, save+reload, 422 no-period (CS-1, CS-2)
- `Project/frontend/e2e/current-situation/cut-record-navigation.spec.ts` — 3 cuts ascending dates, navigate sequence (CS-7)
- `Project/frontend/e2e/current-situation/cut-record-delete.spec.ts` — 204 delete, 404 non-existent, fresh draft after delete (CS-4)
- `Project/frontend/e2e/current-situation/cut-draft-clone.spec.ts` — clone from previous cut, new account gets 0, soft-deleted excluded (CS-2)

### Work Unit Evidence (PR2)

| Evidence | Value |
|---|---|
| Focused unit test command | `npx vitest run src/features/bank-accounts/__tests__/ src/features/current-situation/__tests__/` |
| Unit test result | 30 tests passed, 5 test files, 0 failures |
| Full suite regression | `npx vitest run` — 428 tests passed, 56 test files, 0 failures |
| Runtime harness | E2E specs in `e2e/bank-accounts/` and `e2e/current-situation/` (require running API with ASPNETCORE_ENVIRONMENT=E2E) |
| Rollback boundary | Remove `frontend/src/features/bank-accounts/`, `frontend/src/features/current-situation/`, `frontend/e2e/bank-accounts/`, `frontend/e2e/current-situation/`; revert router, BudgetTabs, en.json, es.json |

### Key Implementation Notes (PR2)
- `@vue/test-utils` not installed; used `@testing-library/vue` throughout (matching project convention)
- `toBeInTheDocument()` not available (no jest-dom setup in vitest config); used `.not.toBeNull()` pattern
- `<dialog>` elements need `open` attribute for jsdom accessibility in tests (DeleteCutModal)
- `listCurrencies` imported from `budget-structure/api/currencies.api` for both BankAccountListView and CurrentSituationView
- 422 detection in upsertCutRecord store uses `e.response?.status === 422` pattern (axios error shape)
- E2E helpers use active-period date range `2026-01-01..2026-12-31` to cover all 2026 cut dates
