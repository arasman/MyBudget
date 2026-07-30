# Proposal: Bank Account Restore

## Intent

Soft-deleted bank accounts are permanently invisible -- no way to recover them without direct DB access. Every other soft-deletable entity (Cycle, Category, CategoryGroup, Period, ExecutionRecord) already supports restore. This closes the gap, adds alias uniqueness validation to prevent conflicts, and brings toast notifications to the bank-accounts feature.

## Scope

### In Scope

- **RestoreBankAccount backend slice** -- Command, Validator, Handler, Endpoint (POST `/api/budgets/{id}/bank-accounts/{accountId}/restore`, `budget:admin`, 204/404)
- **BankAccount.Restore() domain method** -- add to entity (pattern: `DeletedAt = null; UpdatedAt = UtcNow`)
- **ListBankAccounts includeDeleted support** -- add `IncludeDeleted` query param, `DeletedAt` to DTO, branch SQL
- **Frontend restore UX** -- showDeleted toggle, deleted-row opacity + badge, RotateCcw restore button (CategoryTreeView pattern)
- **Alias uniqueness validation** -- CreateBankAccount + UpdateBankAccount validators must reject duplicate aliases within the same budget, including soft-deleted accounts (uses `IgnoreQueryFilters`)
- **Toast notifications** -- add success toasts for create, edit, delete, and restore operations in BankAccountListView (currently zero toast usage; pattern from BudgetSelectionView)

### Out of Scope

- DisplayOrder reordering UI on restore (user reorders manually)
- Cut record interaction with restored accounts (cut records are immutable snapshots)
- Batch restore or undo-delete confirmation modal
- Account balance reconciliation

## Capabilities

### New Capabilities

- None (restore is part of the existing `bank-accounts` capability)

### Modified Capabilities

- `bank-accounts`: adds restore endpoint (BA-5), includeDeleted listing, alias uniqueness rule, and toast feedback for all CRUD+restore operations

## Approach

1. **Entity**: add `Restore()` to `BankAccount.cs` (identical to Cycle/Category pattern)
2. **Backend restore slice**: new `RestoreBankAccount/` folder with Command(`BudgetId, AccountId`), Validator, Handler (IgnoreQueryFilters + null-check + Restore()), Endpoint (POST, 204)
3. **List enhancement**: add `IncludeDeleted` param to query/handler/endpoint, add `DeletedAt` to DTO
4. **Alias uniqueness**: inject `AppDbContext` into CreateBankAccountValidator and UpdateBankAccountValidator; add async rule using `IgnoreQueryFilters()` to check alias uniqueness within the budget
5. **Frontend**: API client (restoreBankAccount + includeDeleted param), store (showDeletedAccounts toggle + restoreAccount action), view (toggle, icon buttons, deleted styling, restore button)
6. **Toasts**: import `useToastStore` in BankAccountListView, add success toasts after create/edit/delete/restore

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `SharedKernel/Entities/BankAccount.cs` | Modified | Add `Restore()` method |
| `BankAccounts/RestoreBankAccount/` | New | 4 files: Command, Validator, Handler, Endpoint |
| `BankAccounts/ListBankAccounts/` | Modified | IncludeDeleted param + DeletedAt in DTO |
| `BankAccounts/CreateBankAccount/Validator` | Modified | Alias uniqueness rule (incl. soft-deleted) |
| `BankAccounts/UpdateBankAccount/Validator` | Modified | Alias uniqueness rule (excl. self, incl. soft-deleted) |
| `bank-accounts/api/bankAccountApi.ts` | Modified | restoreBankAccount + includeDeleted param |
| `bank-accounts/store/useBankAccountStore.ts` | Modified | showDeleted toggle + restoreAccount action |
| `bank-accounts/views/BankAccountListView.vue` | Modified | Toggle, icons, deleted styling, restore, toasts |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Alias uniqueness breaks existing data with duplicates | Low | Validation only on create/update; existing data untouched |
| Toast i18n keys missing | Low | Add keys alongside implementation; follow existing pattern |

## Rollback Plan

Single PR revert. No migrations involved -- only code changes. Alias uniqueness validator is additive (no data changes). Toast additions are purely presentational.

## Dependencies

- None. All patterns established by existing restore slices and CategoryTreeView.

## Success Criteria

- [ ] POST `/api/budgets/{id}/bank-accounts/{accountId}/restore` returns 204 for a soft-deleted account
- [ ] GET with `?includeDeleted=true` returns soft-deleted accounts with `deletedAt` populated
- [ ] Creating/updating a bank account with a duplicate alias (including soft-deleted) returns 422
- [ ] Frontend shows toggle, deleted styling, and restore button matching CategoryTreeView pattern
- [ ] Success toasts appear for create, edit, delete, and restore operations
