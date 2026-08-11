# Explore: audit-log

**Date**: 2026-07-11
**Status**: done — ready for proposal
**Engram**: topic `sdd/audit-log/explore`

---

## Current State

**DbContext** (`src/MyBudget.Features/SharedKernel/Persistence/AppDbContext.cs`):
- Pure EF Core DbContext — ADR-006: no IMediator injection
- 12 DbSets covering all domain entities
- No interceptors registered — `AddInterceptors()` absent from entire codebase
- 36 `SaveChangesAsync` call sites across all write handlers, all flowing through one DbContext

**BaseEntity** (`src/MyBudget.Features/SharedKernel/Entities/BaseEntity.cs`):
- Fields: `Id`, `CreatedAt`, `UpdatedAt`
- No `IsDeleted`, no `IAuditableEntity` interface
- `UpdatedAt` set manually inside entity methods

**Soft-delete pattern**:
- Cycle, Period, CategoryGroup, Category, BudgetLine all have `DeletedAt (DateTimeOffset?)`
- Each has `SoftDelete()` and `Restore()` methods
- No global query filter — handlers call `IgnoreQueryFilters()` explicitly for restore ops
- BudgetLineRevision is append-only (no soft-delete)

**UserId access — critical gap**:
- UserId extracted from `ClaimsPrincipal` only at the endpoint layer
- Commands carry `BudgetId` but NOT `UserId`
- No `ICurrentUserService` or `IHttpContextAccessor` registered anywhere
- `AddHttpContextAccessor()` absent from `ServiceCollectionExtensions` and `Program.cs`

**Test infrastructure**:
- `IntegrationTestFactory` uses WebApplicationFactory with real Postgres
- `ConfigureServices()` override available for adding services
- SQLite already referenced in `MyBudget.Features.csproj` — suitable for DbContext unit tests

---

## Affected Areas

| Path | Why affected |
|---|---|
| `src/MyBudget.Features/SharedKernel/Persistence/AppDbContext.cs` | Override `SaveChangesAsync`; add `AuditLog` DbSet |
| `src/MyBudget.Features/Extensions/ServiceCollectionExtensions.cs` | Register `ICurrentUserService`, `AddHttpContextAccessor()` |
| `src/MyBudget.Api/Program.cs` | `AddHttpContextAccessor()` if not moved to Features |
| `src/MyBudget.Features/SharedKernel/Entities/AuditLog.cs` | New entity |
| `src/MyBudget.Features/SharedKernel/Persistence/Configurations/AuditLogConfiguration.cs` | EF config + indexes |
| `src/MyBudget.Features/Migrations/` | New migration |
| `src/MyBudget.Features/Features/Budgets/GetAuditLog/` | New read slice |
| `tests/MyBudget.Integration.Tests/Infrastructure/IntegrationTestFactory.cs` | Add AuditLogs cleanup |

Zero changes to any existing handler or command record.

---

## Approach Comparison

| Approach | Pros | Cons | Effort |
|---|---|---|---|
| **1. Override `SaveChangesAsync` in AppDbContext** | Single file change; ChangeTracker gives before/after state; zero handler changes; ICurrentUserService is standard scoped; SQLite available for unit tests | AppDbContext gains new constructor dependency (ADR-006 safe — not IMediator) | Low |
| **2. EF Core `SaveChangesInterceptor`** | Clean separation; does not modify AppDbContext class | More complex DI (scoped interceptor needs careful wiring); no additional benefit at this codebase size | Low-Medium |
| **3. Mediator pipeline AuditBehaviour** | Fits existing pipeline | Requires adding UserId to all 36+ commands (retrofit); no ChangeTracker access; doesn't cover cascade changes | High |

---

## Recommendation

**Option 1 — `SaveChangesAsync` override** in AppDbContext.

Reasons:
- All 36 write call sites converge on `AppDbContext.SaveChangesAsync` — natural single intercept point
- `ChangeTracker.Entries<BaseEntity>()` gives full before (`OriginalValues`) and after (`CurrentValues`) state
- Cascade soft-deletes (e.g. DeleteCycle cascades to Periods and BudgetLines) appear as separate Modified entries — each gets its own audit row (desirable)
- Detecting soft-delete vs. update vs. restore is deterministic via `DeletedAt` transition:
  - `null → value` = Deleted
  - `value → null` = Restored
  - State.Added = Created
  - State.Modified (no DeletedAt change) = Updated
- `ICurrentUserService` backed by `IHttpContextAccessor` is standard ASP.NET Core; no new packages; Scoped lifetime aligns with AppDbContext
- ADR-006 says "never inject IMediator" — ICurrentUserService is not IMediator; rule not violated

---

## Proposed AuditLog Entity Schema

```
Id           Guid PK
EntityName   varchar(100)   -- "Cycle", "BudgetLine", etc.
EntityId     Guid           -- PK of audited entity
Action       varchar(20)    -- Created | Updated | Deleted | Restored
UserId       Guid?          -- null for background/system operations
Timestamp    timestamptz
BeforeJson   text?          -- null for Created
AfterJson    text?          -- null for hard deletes
BudgetId     Guid?          -- denormalized for query filtering
```

Indexes: `(BudgetId, Timestamp DESC)`, `(EntityName, EntityId)`, `(UserId)`.

---

## ICurrentUserService Contract

```csharp
public interface ICurrentUserService { Guid? UserId { get; } }
// Scoped implementation backed by IHttpContextAccessor → ClaimTypes.NameIdentifier
```

---

## Risks

| Risk | Mitigation |
|---|---|
| Sensitive field exposure in auth entities (User, RefreshToken, Invitation) pass through ChangeTracker | Apply entity or property exclusion list in SaveChangesAsync override; OR scope auditing to budget-domain entities only |
| Double-save in CreateBudgetLine produces 2 audit rows | Correct and desirable — both BudgetLine.Created and BudgetLineRevision.Created are meaningful events |
| Null UserId for background operations (test cleanup, etc.) | AuditLog.UserId is `Guid?`; override must not throw when null |
| Performance at scale | Audit rows in same transaction adds latency; acceptable at MVP; defer async fan-out to post-MVP |

---

## Decisions — Resolved

1. **Entity scope**: Whitelist budget-domain only — `Budget`, `Cycle`, `Period`, `CategoryGroup`, `Category`, `BudgetLine`, `BudgetLineRevision`. Auth entities excluded from EF override.
2. **Security events**: Separate `SecurityAuditLog` table. Explicit writes in auth handlers. Events: FailedLogin, SuccessfulLogin, PasswordChanged, TokenRefreshed, TokenRevoked, AccountLocked. Fields include IpAddress, UserAgent, Details (JSON) for future attack detection and policy enforcement.
3. **Snapshot format**: Full JSON snapshot (simpler; diff can be derived at query time).
4. **Query endpoint**: TBD in spec — `GET /budgets/{budgetId}/audit-log` with filters by EntityName, Action, date range; pagination.
