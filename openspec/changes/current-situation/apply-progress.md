# Apply Progress: current-situation PR1 (backend only, phases 1–4)

## Status: COMPLETE

## Branch: feat/cs-backend

## Tasks Completed

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

## Key Implementation Notes

- Route parameter MUST be `{id}` (not `{budgetId}`) — BudgetAuthorizationHandler reads `RouteValues["id"]`
- UpsertCutRecord: two-phase save (CutRecord first, CutBankAccounts second) to get the FK ID
- GetCutRecord: two Dapper queries (header + execution summary + accounts)
- Draft SQL: LEFT JOIN active BankAccounts against last_cut CTE for clone logic
- IntegrationTestFactory.CleanDatabaseAsync updated to include CutBankAccounts, CutRecords, BankAccounts
