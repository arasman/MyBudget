# Apply Progress: Bank Account Restore

## Status: COMPLETE — all 15 tasks done

## Task Checklist

- [x] T-01 — BankAccount.Restore() domain method
- [x] T-02 — RestoreBankAccount slice (4 files)
- [x] T-03 — ListBankAccounts: includeDeleted + DeletedAt in DTO
- [x] T-04 — CreateBankAccount validator: alias uniqueness including soft-deleted
- [x] T-05 — UpdateBankAccount validator: alias uniqueness excluding self
- [x] T-06 — Frontend API client: restoreBankAccount() + includeDeleted param
- [x] T-07 — Frontend store: showDeletedAccounts ref + restoreAccount action
- [x] T-08 — Frontend view: toggle, deleted styling, restore button, toasts
- [x] T-09 — Unit tests: BankAccount.Restore()
- [x] T-10 — Unit tests: alias validator rules (Create + Update)
- [x] T-11 — Integration tests: RestoreBankAccount endpoint
- [x] T-12 — Integration tests: ListBankAccounts includeDeleted
- [x] T-13 — Integration tests: alias uniqueness
- [x] T-14 — Frontend tests: BankAccountListView + store
- [x] T-15 — E2E test: full restore flow

## Files Changed

| File | Action |
|------|--------|
| `Project/src/MyBudget.Features/SharedKernel/Entities/BankAccount.cs` | Modified — added Restore() method |
| `Project/src/MyBudget.Features/Features/BankAccounts/RestoreBankAccount/RestoreBankAccountCommand.cs` | Created |
| `Project/src/MyBudget.Features/Features/BankAccounts/RestoreBankAccount/RestoreBankAccountValidator.cs` | Created |
| `Project/src/MyBudget.Features/Features/BankAccounts/RestoreBankAccount/RestoreBankAccountHandler.cs` | Created |
| `Project/src/MyBudget.Features/Features/BankAccounts/RestoreBankAccount/RestoreBankAccountEndpoint.cs` | Created |
| `Project/src/MyBudget.Features/Features/BankAccounts/ListBankAccounts/ListBankAccountsQuery.cs` | Modified — IncludeDeleted param + DeletedAt in DTO |
| `Project/src/MyBudget.Features/Features/BankAccounts/ListBankAccounts/ListBankAccountsHandler.cs` | Modified — Dapper SQL branch + BankAccountRow private record |
| `Project/src/MyBudget.Features/Features/BankAccounts/ListBankAccounts/ListBankAccountsEndpoint.cs` | Modified — includeDeleted query param |
| `Project/src/MyBudget.Features/Features/BankAccounts/CreateBankAccount/CreateBankAccountValidator.cs` | Modified — injected AppDbContext, async MustAsync alias uniqueness |
| `Project/src/MyBudget.Features/Features/BankAccounts/UpdateBankAccount/UpdateBankAccountValidator.cs` | Modified — injected AppDbContext, async MustAsync alias uniqueness + self-exclude |
| `Project/frontend/src/features/bank-accounts/api/bankAccountApi.ts` | Modified — restoreBankAccount(), includeDeleted param |
| `Project/frontend/src/features/bank-accounts/store/useBankAccountStore.ts` | Modified — showDeletedAccounts, restoreAccount, threaded includeDeleted |
| `Project/frontend/src/features/bank-accounts/views/BankAccountListView.vue` | Modified — toggle, opacity+badge, RotateCcw, Pencil/Trash2, toasts |
| `Project/frontend/src/i18n/locales/en.json` | Modified — showDeleted, deleted, restore, success toast keys |
| `Project/frontend/src/i18n/locales/es.json` | Modified — Spanish equivalents |
| `Project/tests/MyBudget.Features.Tests/SharedKernel/Entities/BankAccountEntityTests.cs` | Created |
| `Project/tests/MyBudget.Features.Tests/Features/BankAccounts/CreateBankAccount/CreateBankAccountValidatorTests.cs` | Modified — updated to async ValidateAsync, added alias uniqueness tests |
| `Project/tests/MyBudget.Features.Tests/Features/BankAccounts/UpdateBankAccount/UpdateBankAccountValidatorTests.cs` | Modified — updated to async ValidateAsync, added alias uniqueness + self-exclusion tests |
| `Project/tests/MyBudget.Integration.Tests/Features/BankAccounts/RestoreBankAccountTests.cs` | Created |
| `Project/tests/MyBudget.Integration.Tests/Features/BankAccounts/ListBankAccountsIncludeDeletedTests.cs` | Created |
| `Project/tests/MyBudget.Integration.Tests/Features/BankAccounts/AliasUniquenessTests.cs` | Created |
| `Project/tests/MyBudget.Integration.Tests/Features/CurrentSituation/CurrentSituationTestBase.cs` | Modified — BankAccountListItem got DateTimeOffset? DeletedAt |
| `Project/frontend/src/features/bank-accounts/__tests__/useBankAccountStore.spec.ts` | Modified — restoreAccount + showDeletedAccounts tests |
| `Project/frontend/src/features/bank-accounts/views/__tests__/BankAccountListView.spec.ts` | Created |
| `Project/frontend/e2e/bank-accounts/bank-account-restore.spec.ts` | Created |

## Test Results

- Unit tests (dotnet): 24/24 passed
- Frontend tests (vitest): 22/22 passed
- Integration tests: not run (require live DB — run via sdd-verify)
- E2E tests: not run (require full stack — run via sdd-verify)

## Deviations from Design

None. All patterns mirror existing codebase conventions exactly:
- RestoreBankAccount follows RestoreCycle 4-file pattern
- ListBankAccounts SQL branching follows ListCategoryGroups ternary pattern
- Dapper intermediate record (BankAccountRow) handles DateTime→DateTimeOffset conversion
- Frontend toast/icon pattern matches CategoryTreeView

## Notes

- Validators with MustAsync rules require `ValidateAsync()` — all existing sync validator tests updated
- Endpoint auto-discovered by MapAllSliceEndpoints() reflection — no explicit registration needed
