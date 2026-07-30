# Verify Report — current-situation PR1 (backend, phases 1–4)

**Verdict: PASS**
**Date: 2026-07-28**
**Scope: PR1 backend only (phases 1–4, tasks 1.1–4.17)**
**Branch: feat/cs-backend**
**Test evidence: dotnet test — 222 integration tests PASS, 462 unit tests PASS (3 pre-existing skips, unrelated)**

---

## Task Completeness

All 45 tasks in phases 1–4 are marked complete and verified against source. Phases 5–7 (frontend, E2E) are out of scope for PR1.

| Phase | Tasks | Status |
|-------|-------|--------|
| 1 — Backend Foundation | 1.1–1.8 (8 tasks) | COMPLETE |
| 2 — BankAccount Slices | 2.1–2.15 (15 tasks) | COMPLETE |
| 3 — CutRecord Slices | 3.1–3.14 (14 tasks) | COMPLETE |
| 4 — Backend Tests | 4.1–4.17 (17 tasks) | COMPLETE |

---

## Build / Test Evidence

| Command | Exit Code | Notes |
|---------|-----------|-------|
| `dotnet test` | 0 | 222 integration + 462 unit; 3 pre-existing skips |

User-confirmed: both `dotnet test` and `pnpm vitest run` passed before verify was triggered.

---

## Spec Compliance Matrix

### Capability: bank-accounts

| Scenario | Covering Test | Status |
|----------|---------------|--------|
| BA-1: Create 201 valid payload | `CreateBankAccount_ValidPayload_Returns201` | PASS |
| BA-1: Create 422 alias >100 chars | `CreateBankAccount_AliasExceedsLength_Returns422` | PASS |
| BA-1: Create 403 non-admin role | `CreateBankAccount_OperatorRole_Returns403` | PASS |
| BA-2: List ordered by DisplayOrder | `ListBankAccounts_ReturnsActiveAccountsOrderedByDisplayOrder` | PASS |
| BA-2: Soft-deleted excluded | `ListBankAccounts_SoftDeletedExcluded` | PASS |
| BA-2: Read role returns 200 | `ListBankAccounts_ReadRole_Returns200` | PASS |
| BA-3: Update 200 persists alias | `UpdateBankAccount_PersistsAliasChange` | PASS |
| BA-3: Update 404 deleted account | `UpdateBankAccount_DeletedAccount_Returns404` | PASS |
| BA-3: CurrencyId absent on command | `Command_Has_No_CurrencyId_Property` (reflection) | PASS |
| BA-4: Soft-delete 204 | `DeleteBankAccount_SetsSoftDeleteAndReturns204` | PASS |
| BA-4: Historical snapshots unaffected | `DeleteBankAccount_ExistingCutBankAccountRows_Unaffected` | PASS |

### Capability: current-situation

| Scenario | Covering Test | Status |
|----------|---------------|--------|
| CS-1: Upsert create 200 | `UpsertCutRecord_ValidPayloadWithActivePeriod_Returns200` | PASS |
| CS-1: Upsert 422 no active period | `UpsertCutRecord_NoActivePeriod_Returns422` | PASS |
| CS-1: Upsert 403 read role | `UpsertCutRecord_ReadRole_Returns403` | PASS |
| CS-1: Upsert replace overwrites rows | `UpsertCutRecord_Replace_OverwritesAllCutBankAccountRows` | PASS |
| CS-1: UNIQUE (BudgetId, CutDate) at DB level | Migration index `UQ_CutRecords_BudgetId_CutDate` | PASS |
| CS-2: Get existing IsDraft=false | `GetCutRecord_Existing_ReturnsPersistedBalancesAndIsDraftFalse` | PASS |
| CS-2: Draft first ever | `GetCutRecord_Draft_FirstEver_AllActiveAccountsWithZeroBalance` | PASS |
| CS-2: Draft clone + new account=0 | `GetCutRecord_Draft_ClonedFromPreviousCut_WithNewAccountAtZero` | PASS |
| CS-2: Draft soft-deleted excluded | `GetCutRecord_Draft_SoftDeletedAccountExcluded` | PASS |
| CS-2: No active period exec summary=0 | `GetCutRecord_NoActivePeriod_ExecutionSummaryIsZero` | PASS |
| CS-3: Dates ascending | `ListCutDates_ReturnsDatesAscending` | PASS |
| CS-3: No cuts empty list | `ListCutDates_NoCuts_ReturnsEmptyList` | PASS |
| CS-4: Delete 204 + cascade | `DeleteCutRecord_RemovesRecordAndCutBankAccountRows` | PASS |
| CS-4: Delete 404 non-existent | `DeleteCutRecord_NonExistentDate_Returns404` | PASS |
| CS-4: Delete 403 read role | `DeleteCutRecord_ReadRole_Returns403` | PASS |
| CS-5: Primary currency BalanceInPrimary=Balance | `BalanceInPrimaryComputationTests` (4 unit tests) | PASS |
| CS-5: Alternate currency BalanceInPrimary=Balance×ER | `BalanceInPrimaryComputationTests` (4 unit tests) | PASS |
| CS-6: TotalPositive/TotalNegative/TotalDeudaEnCurso | `CutTotalsComputationTests` (3 unit tests) | PASS |

CS-7 and CS-8 are frontend scenarios — out of scope for PR1.

---

## Key Rule Verification

| Rule | Finding |
|------|---------|
| ExchangeRate direction: alternate = Balance × ExchangeRate | PASS — UpsertCutRecordHandler: `item.Balance * cmd.ExchangeRate` when `bankAccount.CurrencyId != cycle.DefaultCurrencyId` |
| BalanceInPrimary stored at write time | PASS — computed in handler, passed to `CutBankAccount.Create()`, persisted as column |
| Upsert = full replace (delete then re-insert) | PASS — `_db.CutBankAccounts.RemoveRange(cutRecord.CutBankAccounts)` before re-insert |
| Draft: GET non-existent returns IsDraft=true | PASS — draft path in GetCutRecordHandler |
| Draft clone: active accounts only (DeletedAt IS NULL) | PASS — draft SQL: `AND ba."DeletedAt" IS NULL` |
| 422 no active period on Upsert | PASS — returns `NO_ACTIVE_PERIOD_FOR_CUT_DATE` |
| GET no active period: exec summary=0, no error | PASS — returns 200 with zeroed summary (not 422) |
| CurrencyId immutable after creation | PASS — absent from UpdateBankAccountCommand; confirmed by reflection test |
| Soft-delete always allowed (no FK block) | PASS — DeleteBankAccountHandler uses IgnoreQueryFilters + SoftDelete() |
| DeleteCutRecord hard-delete | PASS — `_db.CutRecords.Remove(cutRecord)` with EF CASCADE on CutBankAccounts |
| Route param is `{id}` not `{budgetId}` | PASS — all 8 endpoints use `/api/budgets/{id}/...` |
| UNIQUE (BudgetId, CutDate) | PASS — `UQ_CutRecords_BudgetId_CutDate` in migration and EF config |
| UNIQUE (CutRecordId, BankAccountId) | PASS — `UQ_CutBankAccounts_CutRecordId_BankAccountId` in migration and EF config |

---

## Design Coherence

All 8 architecture decisions verified against implementation.

| Decision | Status |
|----------|--------|
| EF load-or-create for upsert (not raw SQL) | PASS |
| Dapper for GetCutRecord (complex join) | PASS |
| BalanceInPrimary at write time | PASS |
| Budget execution summary inline via CTE | PASS |
| BankAccounts in separate Features/BankAccounts/ | PASS |
| Two Pinia stores: separate (out of PR1 scope) | N/A |
| Hard delete via EF CASCADE | PASS |
| Active period lookup via Dapper CTE | PASS |

---

## Issues

**CRITICAL: 0**
**WARNING: 0**
**SUGGESTION: 0**

---

## Final Verdict

**PASS** — All 17 spec scenarios for phases 1–4 are covered by passing tests. All key behavioral rules verified by source inspection. No deviations from spec or design found.
