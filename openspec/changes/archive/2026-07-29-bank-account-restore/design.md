# Design: Bank Account Restore

## Technical Approach

Follow the established 4-file VSA slice pattern (RestoreCycle reference) and the Dapper-based includeDeleted SQL branching pattern (ListCategoryGroups reference). No new patterns introduced -- every decision mirrors an existing codebase convention.

## Architecture Decisions

| Decision | Choice | Alternative | Rationale |
|----------|--------|-------------|-----------|
| Restore HTTP method | `POST .../restore` | `PATCH` with body | Matches RestoreCycle/RestoreCategory pattern; restore is a command, not a partial update |
| Handler query filter | `IgnoreQueryFilters` + `DeletedAt != null` guard | Separate unfiltered DbSet | Proven pattern from RestoreCycleHandler; single query, explicit guard |
| Alias uniqueness scope | Validate against all accounts incl. soft-deleted (`IgnoreQueryFilters`) | Active-only check | Prevents alias collision on restore; soft-deleted accounts retain namespace |
| List SQL branching | Ternary `IncludeDeleted` SQL like ListCategoryGroups | EF Core query with conditional `.Where` | Read side uses Dapper; mirrors exact ListCategoryGroups pattern |
| Frontend showDeleted | Checkbox toggle with `ref(false)` in store | URL query param | Matches CategoryTreeView/budget-structure store pattern; session-scoped |
| Toast integration | Import `useToastStore` directly in view | Centralized event bus | All other views (CategoryTreeView) use direct `useToastStore` import |
| No cascade restore | Restore account only, no child entities | Cascade CutBankAccount records | CutBankAccount FK is Restrict; cut records are immutable snapshots (out of scope) |

## Data Flow

```
Frontend toggle ON
    |
    v
store.fetchAccounts(budgetId, includeDeleted=true)
    |
    v
GET /api/budgets/{id}/bank-accounts?includeDeleted=true
    |
    v
ListBankAccountsHandler (Dapper, SQL branch)
    |
    v
Returns BankAccountDto[] with DeletedAt field

User clicks Restore
    |
    v
store.restoreAccount(budgetId, accountId)
    |
    v
POST /api/budgets/{id}/bank-accounts/{accountId}/restore
    |
    v
RestoreBankAccountHandler (EF Core, IgnoreQueryFilters)
    -> Find account (must exist, must be deleted)
    -> account.Restore()
    -> SaveChangesAsync
    -> 204
    |
    v
store re-fetches list -> toast success
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `SharedKernel/Entities/BankAccount.cs` | Modify | Add `Restore()` method: `DeletedAt = null; UpdatedAt = DateTimeOffset.UtcNow` |
| `BankAccounts/RestoreBankAccount/RestoreBankAccountCommand.cs` | Create | `record(Guid BudgetId, Guid AccountId) : IRequest<Result<Guid>>` |
| `BankAccounts/RestoreBankAccount/RestoreBankAccountValidator.cs` | Create | NotEmpty on BudgetId + AccountId |
| `BankAccounts/RestoreBankAccount/RestoreBankAccountHandler.cs` | Create | IgnoreQueryFilters, find by Id+BudgetId+DeletedAt!=null, call Restore(), save |
| `BankAccounts/RestoreBankAccount/RestoreBankAccountEndpoint.cs` | Create | POST `bank-accounts/{accountId}/restore`, budget:admin, 204/404 |
| `BankAccounts/ListBankAccounts/ListBankAccountsQuery.cs` | Modify | Add `bool IncludeDeleted` param; add `DeletedAt` to `BankAccountDto` |
| `BankAccounts/ListBankAccounts/ListBankAccountsHandler.cs` | Modify | Branch SQL on IncludeDeleted (ternary pattern from ListCategoryGroups) |
| `BankAccounts/ListBankAccounts/ListBankAccountsEndpoint.cs` | Modify | Add `bool? includeDeleted` parameter, pass to query |
| `BankAccounts/CreateBankAccount/CreateBankAccountValidator.cs` | Modify | Inject `AppDbContext`, add async `MustAsync` rule: alias unique within budget (IgnoreQueryFilters) |
| `BankAccounts/UpdateBankAccount/UpdateBankAccountValidator.cs` | Modify | Inject `AppDbContext`, add async `MustAsync` rule: alias unique excluding self (IgnoreQueryFilters) |
| `frontend/bank-accounts/api/bankAccountApi.ts` | Modify | Add `restoreBankAccount()`, add `includeDeleted` param to `listBankAccounts()` |
| `frontend/bank-accounts/store/useBankAccountStore.ts` | Modify | Add `showDeletedAccounts` ref, `restoreAccount` action, pass includeDeleted to fetch |
| `frontend/bank-accounts/views/BankAccountListView.vue` | Modify | Toggle checkbox, deleted-row opacity+badge, RotateCcw restore button, hide edit/delete for deleted rows, toast for CRUD+restore |

## Interfaces / Contracts

### Backend

```csharp
// RestoreBankAccountCommand
public sealed record RestoreBankAccountCommand(Guid BudgetId, Guid AccountId)
    : IRequest<Result<Guid>>;

// Updated BankAccountDto (ListBankAccounts)
public sealed record BankAccountDto(
    Guid              Id,
    Guid              CurrencyId,
    string            Alias,
    bool              IsPositive,
    int               DisplayOrder,
    DateTimeOffset?   DeletedAt);

// Updated ListBankAccountsQuery
public sealed record ListBankAccountsQuery(Guid BudgetId, bool IncludeDeleted)
    : IRequest<Result<IReadOnlyList<BankAccountDto>>>;
```

### Alias uniqueness validator rule (both Create and Update)

```csharp
// In CreateBankAccountValidator constructor (inject AppDbContext db):
RuleFor(x => x.Alias)
    .MustAsync(async (cmd, alias, ct) =>
    {
        return !await db.BankAccounts
            .IgnoreQueryFilters()
            .AnyAsync(a => a.BudgetId == cmd.BudgetId
                        && a.Alias == alias.Trim(), ct);
    })
    .WithErrorCode("ALIAS_DUPLICATE");

// UpdateBankAccountValidator: same but exclude self by AccountId
```

### Frontend API

```typescript
// bankAccountApi.ts additions
export async function listBankAccounts(
  budgetId: string,
  opts?: { includeDeleted?: boolean },
): Promise<BankAccount[]>

export async function restoreBankAccount(
  budgetId: string,
  accountId: string,
): Promise<void>
```

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| Unit | `BankAccount.Restore()` sets DeletedAt=null, UpdatedAt=now | Instantiate via Create, SoftDelete, Restore, assert fields |
| Unit | Alias uniqueness validator rejects duplicates | Mock AppDbContext with in-memory data, test Create and Update validators |
| Integration | Restore happy path (204) | Seed soft-deleted account, POST restore, verify 204 + account active |
| Integration | Restore 404 (not found / not deleted) | POST restore for non-existent or active account, verify 404 |
| Integration | ListBankAccounts includeDeleted=true | Seed active + deleted accounts, verify both returned with DeletedAt |
| Integration | Alias uniqueness on Create (422) | Seed account, create with same alias, verify 422 |
| Integration | Alias uniqueness on Update (422 + self-exclude) | Seed 2 accounts, update one to other's alias (422), update to own alias (200) |
| Frontend | BankAccountListView toggle + restore button rendering | Mount with deleted accounts in store, verify toggle, opacity class, restore button |
| E2E | Full restore flow | Create account, delete, toggle showDeleted, verify deleted row, restore, verify active |

## Threat Matrix

N/A -- no routing, shell, subprocess, VCS/PR automation, executable-file classification, or process-integration boundary.

## Migration / Rollout

No migration required. All changes are additive code-only. BankAccount table already has DeletedAt column. Alias uniqueness is enforced at validation layer only (no DB constraint change).

## Open Questions

None -- all patterns established by existing restore slices.
