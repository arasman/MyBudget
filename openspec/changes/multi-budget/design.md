# Design: Multi-Budget Management

## Technical Approach

Four vertical slices (Create, Rename, Delete, Restore) following existing VSA 4-file pattern. Soft-delete via `IsDeleted`/`DeletedAt` on `Budget` entity with domain methods. `BudgetAuthorizationHandler` modified inline (Option A) to return 404 for deleted budgets. Frontend extends `BudgetSelectionView` with create/delete/restore controls; navigation guard added to router `beforeEach`.

## Architecture Decisions

### Decision: Soft-delete check location

| Option | Tradeoff | Decision |
|--------|----------|----------|
| A: Inline in `BudgetAuthorizationHandler` Dapper query | Minimal change, single query, reuses existing 404 path | **Chosen** |
| B: Separate middleware/filter | Extra hop, new file, split concern | Rejected |

**Rationale**: The handler already queries `BudgetMemberships` and distinguishes "budget not found" from "not a member". Adding `JOIN "Budgets" b ON b."Id" = bm."BudgetId" WHERE b."IsDeleted" = false` to the membership query makes a deleted budget return `null` role, triggering the existing `budget-not-found` 404 path with zero new code paths.

### Decision: Cache eviction strategy on Delete/Restore

| Option | Tradeoff | Decision |
|--------|----------|----------|
| Query all member userIds, evict per-key | N+1 cache removes but bounded by membership count (small) | **Chosen** |
| Wildcard/prefix eviction | `IMemoryCache` has no prefix scan; requires Redis or custom wrapper | Rejected |
| Skip eviction, rely on TTL | 5-min stale window where deleted budget still accessible | Rejected |

**Rationale**: Budget membership count is small (typically <10). A single Dapper query fetches all `UserId` values, then `_cache.Remove()` per key. Same pattern applies to Rename (cached membership may include budget name in future) and Delete/Restore.

### Decision: CreateBudget authorization

| Option | Tradeoff | Decision |
|--------|----------|----------|
| `RequireAuthorization()` only (no budget policy) | Any authenticated user can create; no `budgetId` in route | **Chosen** |
| Custom policy `budget:create` | Over-engineered; route has no `:id` param for handler to extract | Rejected |

**Rationale**: `BudgetAuthorizationHandler` requires `RouteValues["id"]` which does not exist on `POST /api/budgets`. Standard JWT auth is sufficient.

### Decision: Owner-only enforcement for Delete/Restore

| Option | Tradeoff | Decision |
|--------|----------|----------|
| New `budget:owner` policy + existing handler | Reuses `BudgetRequirement(MinimumRole)` pattern, `BudgetRole.Owner = 40` | **Chosen** |
| Check role inside handler manually | Duplicates auth logic, bypasses policy pipeline | Rejected |

**Rationale**: Add `budget:owner` policy mapped to `BudgetRequirement(BudgetRole.Owner)` alongside existing `budget:admin`, `budget:operator`, `budget:readonly` policies.

### Decision: Frontend deleted-budget visibility

| Option | Tradeoff | Decision |
|--------|----------|----------|
| `/api/auth/me` returns all memberships with `isDeleted` flag | Single existing call, no new endpoint, frontend filters | **Chosen** |
| New endpoint `GET /api/budgets?includeDeleted=true` | Extra slice, extra API call, redundant with `/me` | Rejected |

## Data Flow

```
CreateBudget:
  Client ──POST /api/budgets──→ Endpoint ──→ Handler (EF: Budget.Create + BudgetMembership.Create)
    └── Response: { budgetId } ──→ Client calls fetchMe() ──→ navigate to new budget

Delete/Restore:
  Client ──DELETE|POST /api/budgets/:id──→ BudgetAuthorizationHandler (budget:owner)
    ──→ Handler (EF: budget.SoftDelete()/Restore())
    ──→ Dapper: SELECT UserId FROM BudgetMemberships WHERE BudgetId = @id
    ──→ foreach userId: cache.Remove("budget-membership:{userId}:{budgetId}")
    ──→ 204 No Content

BudgetAuthorizationHandler (modified):
  Extract userId, budgetId ──→ cache lookup
    miss? ──→ Dapper: SELECT bm.Role FROM BudgetMemberships bm
                       JOIN Budgets b ON b.Id = bm.BudgetId
                       WHERE bm.UserId=@u AND bm.BudgetId=@b AND b.IsDeleted = false
    null? ──→ check budget exists (including deleted) ──→ set budget-not-found ──→ 404
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `SharedKernel/Entities/Budget.cs` | Modify | Add `IsDeleted`, `DeletedAt`, `Rename()`, `SoftDelete()`, `Restore()` methods |
| `SharedKernel/Persistence/Configurations/BudgetConfiguration.cs` | Modify | Map `IsDeleted` (default false), `DeletedAt` columns |
| `SharedKernel/Auth/BudgetMembershipDto.cs` | Modify | Add `bool IsDeleted` property |
| `SharedKernel/Auth/Authorization/BudgetAuthorizationHandler.cs` | Modify | Join Budgets table, filter `IsDeleted = false` in membership query |
| `Features/Auth/GetCurrentUser/GetCurrentUserHandler.cs` | Modify | Join `b.IsDeleted` into MembershipRow, pass to DTO |
| `Features/Budgets/CreateBudget/` (4 files) | Create | Command, Validator, Handler, Endpoint |
| `Features/Budgets/RenameBudget/` (4 files) | Create | Command, Validator, Handler, Endpoint |
| `Features/Budgets/DeleteBudget/` (4 files) | Create | Command, Handler, Endpoint (+ validator stub) |
| `Features/Budgets/RestoreBudget/` (4 files) | Create | Command, Handler, Endpoint (+ validator stub) |
| EF Migration | Create | `AddBudgetSoftDelete` — `IsDeleted bool NOT NULL DEFAULT false`, `DeletedAt timestamptz NULL` |
| `Program.cs` or auth policy registration | Modify | Add `budget:owner` policy |
| `frontend/src/features/budget-structure/api/budgets.api.ts` | Create | `createBudget`, `renameBudget`, `deleteBudget`, `restoreBudget` |
| `frontend/src/features/budget-structure/components/CreateBudgetModal.vue` | Create | Modal: name input (max 200), submit/cancel |
| `frontend/src/features/budget-structure/views/BudgetSelectionView.vue` | Modify | "New Budget" button, "show deleted" toggle, restore button (Owner only) |
| `frontend/src/stores/auth.store.ts` | Modify | Add `isDeleted` to `BudgetMembershipDto` interface |
| `frontend/src/layouts/AppLayout.vue` | Modify | On mount, if `activeBudgetName` null and route has `budgetId`, restore from `authStore.user.memberships` |
| `frontend/src/router/index.ts` | Modify | `beforeEach`: check if `budgetId` membership has `isDeleted: true` -> redirect `/` |
| `frontend/src/i18n/locales/en.json` | Modify | ~10 new keys under `budgetStructure.selection` |
| `frontend/src/i18n/locales/es.json` | Modify | ~10 new keys (Spanish) |

## Interfaces / Contracts

```csharp
// Budget.cs — new domain methods
public void Rename(string newName) { Name = newName.Trim(); UpdatedAt = DateTimeOffset.UtcNow; }
public void SoftDelete() { IsDeleted = true; DeletedAt = DateTimeOffset.UtcNow; UpdatedAt = DateTimeOffset.UtcNow; }
public void Restore() { IsDeleted = false; DeletedAt = null; UpdatedAt = DateTimeOffset.UtcNow; }

// BudgetMembershipDto.cs
public sealed record BudgetMembershipDto(Guid BudgetId, string BudgetName, string Role, bool IsDeleted);

// CreateBudgetCommand
public sealed record CreateBudgetCommand(string Name, Guid UserId) : IRequest<Result<CreateBudgetResponse>>;
public sealed record CreateBudgetResponse(Guid BudgetId);

// RenameBudgetCommand
public sealed record RenameBudgetCommand(Guid BudgetId, string NewName, Guid UserId) : IRequest<Result<Unit>>;

// DeleteBudgetCommand
public sealed record DeleteBudgetCommand(Guid BudgetId, Guid UserId) : IRequest<Result<Unit>>;

// RestoreBudgetCommand
public sealed record RestoreBudgetCommand(Guid BudgetId, Guid UserId) : IRequest<Result<Unit>>;
```

```typescript
// budgets.api.ts
export async function createBudget(name: string): Promise<{ budgetId: string }>
export async function renameBudget(budgetId: string, newName: string): Promise<void>
export async function deleteBudget(budgetId: string): Promise<void>
export async function restoreBudget(budgetId: string): Promise<void>
```

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| Unit | Validator rules (name length, empty) | In-memory, per-slice validator tests |
| Unit | Handler logic (create sets Owner membership, delete sets flag, restore clears flag) | SQLite in-memory `AppDbContext` |
| Unit | `Budget.Rename/SoftDelete/Restore` domain methods | Direct entity method tests |
| Integration | Full HTTP round-trip per endpoint | `WebApplicationFactory`, SQLite in-memory |
| Frontend | `CreateBudgetModal` form validation + submit | `@testing-library/vue` + vitest mock |
| Frontend | `BudgetSelectionView` deleted toggle + restore button visibility | Component test with seeded store |

## Migration / Rollout

EF migration `AddBudgetSoftDelete`:
- `ALTER TABLE "Budgets" ADD COLUMN "IsDeleted" boolean NOT NULL DEFAULT false`
- `ALTER TABLE "Budgets" ADD COLUMN "DeletedAt" timestamp with time zone NULL`
- Rollback: drop both columns. No data loss since all existing budgets have `IsDeleted = false`.

## Open Questions

- None. All design questions from the proposal have been resolved above.
