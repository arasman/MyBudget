# Tasks: Bank Account Restore

## Metadata

- Change: `bank-account-restore`
- Branch: `feat/bank-account-restore`
- Stack: .NET 10 VSA (4-file slice), EF Core writes, Dapper reads, Vue 3 + Pinia
- TDD: off
- Tests required: unit, integration, frontend, E2E
- Estimated diff: ~120–160 lines

---

## Dependency Graph

```
T-01 (Entity)
  ├── T-02 (Restore slice)       → T-11 (Integration: restore)
  ├── T-03 (List + DTO)          → T-06 → T-07 → T-08 → T-14, T-15
  │                              → T-12 (Integration: list)
  ├── T-04 (Create validator)    → T-10, T-13
  └── T-05 (Update validator)    → T-10, T-13
T-09 (Unit: Restore()) depends on T-01
T-15 (E2E) depends on T-08 + T-11 + T-12 + T-13
```

Parallelism: T-02, T-03, T-04, T-05 can proceed in parallel after T-01 lands.
Frontend group (T-06 → T-07 → T-08) is sequential and gated on T-03.

---

## Group 1 — Entity

### T-01 — Add BankAccount.Restore() domain method

- [x] **Action**: modify
- **File**: `SharedKernel/Entities/BankAccount.cs`
- **Work**: Add public `Restore()` method — set `DeletedAt = null; UpdatedAt = DateTimeOffset.UtcNow`. Must not touch alias, currencyId, isPositive, or displayOrder.
- **Satisfies**: BA-6
- **Depends on**: none

---

## Group 2 — Backend Slices

> All four tasks in this group can start in parallel once T-01 is merged.

### T-02 — Create RestoreBankAccount slice (4 files)

- [x] **Action**: create
- **Files**:
  - `BankAccounts/RestoreBankAccount/RestoreBankAccountCommand.cs` — `record(Guid BudgetId, Guid AccountId) : IRequest<Result<Guid>>`
  - `BankAccounts/RestoreBankAccount/RestoreBankAccountValidator.cs` — NotEmpty on BudgetId + AccountId
  - `BankAccounts/RestoreBankAccount/RestoreBankAccountHandler.cs` — `IgnoreQueryFilters()`, find by Id+BudgetId+`DeletedAt != null`, call `account.Restore()`, `SaveChangesAsync`, return 204 or 404
  - `BankAccounts/RestoreBankAccount/RestoreBankAccountEndpoint.cs` — `POST bank-accounts/{accountId}/restore`, `budget:admin` policy, maps 204/404
- **Satisfies**: BA-5 (all scenarios)
- **Depends on**: T-01

### T-03 — ListBankAccounts: add includeDeleted param + DeletedAt to DTO

- [x] **Action**: modify
- **Files**:
  - `BankAccounts/ListBankAccounts/ListBankAccountsQuery.cs` — add `bool IncludeDeleted`; add `DateTimeOffset? DeletedAt` to `BankAccountDto`
  - `BankAccounts/ListBankAccounts/ListBankAccountsHandler.cs` — branch SQL on `IncludeDeleted` (ternary pattern from ListCategoryGroups); Dapper read side
  - `BankAccounts/ListBankAccounts/ListBankAccountsEndpoint.cs` — add `bool? includeDeleted` query param; pass to query
- **Satisfies**: BA-2 (amended, all scenarios)
- **Depends on**: T-01

### T-04 — CreateBankAccount validator: alias uniqueness including soft-deleted

- [x] **Action**: modify
- **File**: `BankAccounts/CreateBankAccount/CreateBankAccountValidator.cs`
- **Work**: Inject `AppDbContext`; add async `MustAsync` rule — `IgnoreQueryFilters()`, `AnyAsync` by `BudgetId + Alias.Trim()`; `WithErrorCode("ALIAS_DUPLICATE")`
- **Satisfies**: BA-1 (amended, all scenarios)
- **Depends on**: T-01

### T-05 — UpdateBankAccount validator: alias uniqueness including soft-deleted, excluding self

- [x] **Action**: modify
- **File**: `BankAccounts/UpdateBankAccount/UpdateBankAccountValidator.cs`
- **Work**: Same pattern as T-04 but additionally exclude current account by `AccountId` from the uniqueness check
- **Satisfies**: BA-3 (amended, all scenarios)
- **Depends on**: T-01

---

## Group 3 — Frontend

> T-06 → T-07 → T-08 are strictly sequential. Gate on T-03 for DTO contract.

### T-06 — bankAccountApi.ts: restoreBankAccount() + includeDeleted param

- [x] **Action**: modify
- **File**: `frontend/bank-accounts/api/bankAccountApi.ts`
- **Work**:
  - Add `opts?: { includeDeleted?: boolean }` to `listBankAccounts()`; append `?includeDeleted=true` when flag is set
  - Add `restoreBankAccount(budgetId: string, accountId: string): Promise<void>` — POST `.../restore`
- **Satisfies**: FE-BA-2 (API layer)
- **Depends on**: T-03

### T-07 — useBankAccountStore.ts: showDeletedAccounts ref + restoreAccount action

- [x] **Action**: modify
- **File**: `frontend/bank-accounts/store/useBankAccountStore.ts`
- **Work**:
  - Add `showDeletedAccounts = ref(false)`
  - Thread `includeDeleted: showDeletedAccounts.value` into `fetchAccounts()` call
  - Add `restoreAccount(budgetId, accountId)` action — calls `restoreBankAccount()`, then re-fetches list
- **Satisfies**: FE-BA-1, FE-BA-2 (store layer)
- **Depends on**: T-06

### T-08 — BankAccountListView.vue: toggle, deleted styling, restore button, toasts

- [x] **Action**: modify
- **File**: `frontend/bank-accounts/views/BankAccountListView.vue`
- **Work**:
  - "Show deleted" checkbox toggle bound to `store.showDeletedAccounts`; triggers re-fetch on change
  - Deleted rows: `opacity-60` (or equivalent) + visible "deleted" badge
  - RotateCcw restore button — visible only on deleted rows; calls `store.restoreAccount()`
  - Replace text edit/delete buttons with Pencil/Trash2 icon buttons on active rows
  - Success toasts via `useToastStore` after create, edit, delete, and restore
  - Restore button absent on active rows; edit/delete buttons absent on deleted rows
- **Satisfies**: FE-BA-1, FE-BA-2, FE-BA-3, FE-BA-4
- **Depends on**: T-07

---

## Group 4 — Tests

> Test tasks within the same layer can be written in parallel once their implementation layer is complete.

### T-09 — Unit: BankAccount.Restore()

- [x] **Scope**: unit
- **Work**: Test that `Restore()` sets `DeletedAt = null` and `UpdatedAt ≈ UtcNow`; test idempotency on already-active account (no throw, UpdatedAt refreshed)
- **Satisfies**: spec unit row "BankAccount.Restore() — field mutations and idempotency"
- **Depends on**: T-01

### T-10 — Unit: alias validator rules (Create + Update)

- [x] **Scope**: unit
- **Work**: Mock `AppDbContext` with in-memory data; verify Create validator rejects alias matching soft-deleted account (ALIAS_DUPLICATE); verify Update validator rejects alias of soft-deleted account but accepts own alias
- **Satisfies**: spec unit rows for CreateBankAccountValidator and UpdateBankAccountValidator
- **Depends on**: T-04, T-05

### T-11 — Integration: restore endpoint (204 / 404 × 2)

- [x] **Scope**: integration
- **Work**:
  - Seed soft-deleted account → POST restore → assert 204, `DeletedAt IS NULL`, `UpdatedAt` refreshed
  - POST restore for non-existent accountId → 404
  - POST restore for active account → 404
- **Satisfies**: spec integration rows for POST `.../restore`
- **Depends on**: T-02

### T-12 — Integration: list includeDeleted

- [x] **Scope**: integration
- **Work**:
  - Seed 1 active + 1 deleted account
  - GET without param → only active returned, `deletedAt` null
  - GET `?includeDeleted=true` → both returned, deleted account has `deletedAt` populated (ISO-8601)
- **Satisfies**: spec integration rows for BA-2 amended
- **Depends on**: T-03

### T-13 — Integration: alias uniqueness (422 on Create + Update)

- [x] **Scope**: integration
- **Work**:
  - Seed account with alias "Savings" (active); POST create with alias "Savings" → 422
  - Seed soft-deleted account with alias "OldChecking"; POST create with alias "OldChecking" → 422
  - Seed 2 accounts A and B; PUT A with alias of B → 422; PUT A with own alias → 200
  - PUT A with alias of soft-deleted account → 422
- **Satisfies**: spec integration rows for BA-1 + BA-3 amended
- **Depends on**: T-04, T-05

### T-14 — Frontend: toggle + restore button rendering (Vitest/Vue Test Utils)

- [x] **Scope**: frontend
- **Work**:
  - Mount with deleted accounts in store; assert no deleted rows visible when toggle off
  - Toggle on; assert deleted rows visible with opacity class + badge
  - Assert RotateCcw button present on deleted rows, absent on active rows
  - Assert Pencil + Trash2 buttons present on active rows
  - Assert success toast fires after mocked create/edit/delete/restore
- **Satisfies**: spec frontend rows
- **Depends on**: T-08

### T-15 — E2E: full restore flow + alias-of-soft-deleted rejected

- [x] **Scope**: E2E
- **Work**:
  - Create account → delete → toggle "Show deleted" on → verify row visible with styling → click restore → verify row disappears from deleted view → toggle off → verify account active in list
  - Create account with alias "X" → delete → attempt create new account with alias "X" → verify 422 / error state
- **Satisfies**: spec E2E rows
- **Depends on**: T-08, T-11, T-12, T-13

---

## Sequential Critical Path

```
T-01 → T-02 → T-11
     → T-03 → T-06 → T-07 → T-08 → T-14
                                   → T-15
     → T-04 → T-13
     → T-05 → T-13
T-09 (can run with T-02 after T-01)
T-10 (after T-04 + T-05)
T-12 (after T-03)
```
