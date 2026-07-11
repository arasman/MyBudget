# Design: Audit Log

## Technical Approach

Override `SaveChangesAsync` in `AppDbContext` to intercept whitelisted entity mutations and produce `AuditLog` entries with before/after JSON snapshots. Security events are written explicitly in each auth handler via a thin `ISecurityAuditWriter` abstraction. Both tables are read via Dapper paginated queries behind `budget:admin`-authorized endpoints. A daily `IHostedService` handles TTL-based retention cleanup.

## Architecture Decisions

| # | Decision | Choice | Rejected | Rationale |
|---|----------|--------|----------|-----------|
| 1 | AuditLog write mechanism | `SaveChangesAsync` override in `AppDbContext` | EF interceptor, MediatR behavior | Override is simpler, co-located with DbContext, avoids interceptor registration ceremony. ADR-006 allows non-IMediator injection into AppDbContext. |
| 2 | SecurityAuditLog write abstraction | `ISecurityAuditWriter` interface + `SecurityAuditWriter` impl (wraps `AppDbContext.SecurityAuditLogs.Add` + `SaveChangesAsync`) | Direct DbContext injection in each handler | Thin interface reduces per-handler boilerplate (IP/UserAgent extraction centralized), testable via mock, single responsibility. |
| 3 | ICurrentUserService scope | Scoped service backed by `IHttpContextAccessor` → `ClaimTypes.NameIdentifier` | Ambient static, MediatR behavior | Scoped aligns with request lifetime. Returns `null` when no HTTP context (background jobs). |
| 4 | BudgetId resolution for denormalized column | `IBudgetOwned` marker interface with `Guid? GetBudgetId()` on whitelisted entities; fallback Dapper lookup for deep entities (BudgetLine, BudgetLineRevision) | Navigation-property traversal, always-Dapper | Navigation properties may not be loaded. Marker interface works for Budget/Cycle/CategoryGroup (direct). For Period→Cycle→Budget and deeper, use a single Dapper query at audit time since these are rare relative to the overall write. |
| 5 | Snapshot serialization | `System.Text.Json` serialize `OriginalValues`/`CurrentValues` dictionaries from ChangeTracker | Newtonsoft, manual property copy | STJ is already used project-wide. ChangeTracker values avoid loading navigation properties. |
| 6 | Soft-delete/restore detection | Inspect `DeletedAt` property in ChangeTracker: Modified + null→value = Deleted, value→null = Restored | Entity method flags, domain events | ChangeTracker inspection is reliable, no entity changes required. All soft-deletable entities already have `DeletedAt`. |
| 7 | SecurityAuditLog budget-scoped read | JOIN on `BudgetMemberships` to filter by users who are members of the requested budget | Denormalize BudgetId on SecurityAuditLog | Security events are user-scoped (login, register); denormalizing would require knowing budget at auth time which is not available. Join is correct. |

## Data Flow

```
  ── AuditLog (entity mutations) ──

  Handler ──→ entity.Modify() ──→ AppDbContext.SaveChangesAsync()
                                       │
                                  ChangeTracker scan
                                  (whitelisted types only)
                                       │
                                  Snapshot before/after
                                  Resolve BudgetId
                                  Resolve UserId (ICurrentUserService)
                                       │
                                  Add AuditLog entries
                                  base.SaveChangesAsync()

  ── SecurityAuditLog (auth events) ──

  Auth Handler ──→ ISecurityAuditWriter.WriteAsync(event, userId, email, details)
                           │
                    Extracts IP + UserAgent from IHttpContextAccessor
                    Builds SecurityAuditLog entity
                    _db.SecurityAuditLogs.Add(entry)
                    _db.SaveChangesAsync()

  ── Read ──

  GET /budgets/{id}/audit-log ──→ Dapper query with pagination + filters
  GET /budgets/{id}/security-audit-log ──→ Dapper query JOIN BudgetMemberships

  ── Retention ──

  AuditRetentionService (IHostedService) ──→ daily timer
      ──→ IAuditRetentionPolicy.GetRetentionDays()
      ──→ DELETE FROM AuditLogs WHERE Timestamp < cutoff
      ──→ DELETE FROM SecurityAuditLogs WHERE Timestamp < cutoff
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `SharedKernel/Entities/AuditLog.cs` | Create | AuditLog entity (no BaseEntity — standalone with Guid PK) |
| `SharedKernel/Entities/SecurityAuditLog.cs` | Create | SecurityAuditLog entity (standalone) |
| `SharedKernel/Entities/IAuditableEntity.cs` | Create | Marker interface with `Guid? ResolveBudgetId()` for whitelisted entities |
| `SharedKernel/Persistence/Configurations/AuditLogConfiguration.cs` | Create | EF config: indexes on (BudgetId, Timestamp DESC), (EntityName, EntityId), (UserId) |
| `SharedKernel/Persistence/Configurations/SecurityAuditLogConfiguration.cs` | Create | EF config: indexes on (UserId), (Event), (Timestamp DESC) |
| `SharedKernel/Persistence/AppDbContext.cs` | Modify | Add DbSets, override SaveChangesAsync with ChangeTracker scan |
| `SharedKernel/Services/ICurrentUserService.cs` | Create | Interface: `Guid? UserId { get; }` |
| `SharedKernel/Services/HttpContextCurrentUserService.cs` | Create | Scoped impl backed by IHttpContextAccessor |
| `SharedKernel/Services/ISecurityAuditWriter.cs` | Create | Interface: `Task WriteAsync(string event, Guid? userId, string? email, object? details, CancellationToken)` |
| `SharedKernel/Services/SecurityAuditWriter.cs` | Create | Impl: extracts IP/UserAgent from IHttpContextAccessor, writes to DbContext |
| `SharedKernel/Services/IAuditRetentionPolicy.cs` | Create | Interface: `int GetRetentionDays()` |
| `SharedKernel/Services/AppSettingsAuditRetentionPolicy.cs` | Create | Reads `AuditLog:RetentionDays` from IConfiguration, default 90 |
| `SharedKernel/Services/AuditRetentionService.cs` | Create | IHostedService with daily timer, batch-deletes expired records |
| `Features/AuditLog/GetAuditLog/` | Create | Query + Handler + Endpoint (Dapper, paginated, budget:admin) |
| `Features/AuditLog/GetSecurityAuditLog/` | Create | Query + Handler + Endpoint (Dapper, paginated, budget:admin, JOIN BudgetMemberships) |
| `Features/Auth/LoginUser/LoginUserHandler.cs` | Modify | Inject ISecurityAuditWriter, write SuccessfulLogin/FailedLogin |
| `Features/Auth/RegisterUser/RegisterUserHandler.cs` | Modify | Inject ISecurityAuditWriter, write AccountRegistered |
| `Features/Auth/AcceptInvitation/AcceptInvitationHandler.cs` | Modify | Inject ISecurityAuditWriter, write InvitationAccepted |
| `Features/Auth/RefreshToken/RefreshTokenHandler.cs` | Modify | Inject ISecurityAuditWriter, write TokenRefreshed |
| `Features/Auth/LogoutUser/LogoutUserHandler.cs` | Modify | Inject ISecurityAuditWriter, write TokenRevoked |
| `Extensions/ServiceCollectionExtensions.cs` | Modify | Register ICurrentUserService, ISecurityAuditWriter, IAuditRetentionPolicy, AuditRetentionService, AddHttpContextAccessor |
| `Migrations/` | Create | Single migration for both tables |
| Whitelisted entities (Budget, Cycle, etc.) | Modify | Implement `IAuditableEntity` marker with `ResolveBudgetId()` |

## Interfaces / Contracts

```csharp
// Marker for whitelisted entities
public interface IAuditableEntity
{
    Guid? ResolveBudgetId(); // Budget returns Id, Cycle/CategoryGroup return BudgetId, others return null (resolved via Dapper)
}

public interface ICurrentUserService
{
    Guid? UserId { get; }
}

public interface ISecurityAuditWriter
{
    Task WriteAsync(string eventName, Guid? userId, string? email,
                    object? details = null, CancellationToken ct = default);
}

public interface IAuditRetentionPolicy
{
    int GetRetentionDays();
}
```

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| Unit | SaveChangesAsync override produces correct AuditLog entries per action type | In-memory DbContext; verify entries after save for Created/Updated/Deleted/Restored |
| Unit | Non-whitelisted entity produces no AuditLog | In-memory DbContext; save User, verify zero AuditLog rows |
| Unit | Soft-delete detection (DeletedAt transitions) | In-memory DbContext; exercise SoftDelete/Restore, verify Action values |
| Unit | SecurityAuditWriter extracts IP/UserAgent correctly | Mock IHttpContextAccessor, verify entity fields |
| Unit | AppSettingsAuditRetentionPolicy reads config, defaults to 90 | Mock IConfiguration |
| Integration | Auth handlers write SecurityAuditLog entries | WebApplicationFactory; login/register/refresh/logout, query DB for entries |
| Integration | GET audit-log returns paginated, filtered results | WebApplicationFactory; seed audit data, verify filters and pagination |
| Integration | GET security-audit-log filters by budget membership | WebApplicationFactory; two users in different budgets, verify isolation |
| Integration | Unauthorized user gets 403 on audit endpoints | WebApplicationFactory; Member role, verify 403 |
| Integration | Retention service deletes expired records | Seed old records, trigger cleanup, verify deletion |

## Migration / Rollout

Single EF migration creates both tables with indexes. Fully additive — no existing schema changes. Rollback: `dotnet ef database update <previous-migration>`.

### PR Slice Plan

| PR | Scope | Est. Lines |
|----|-------|-----------|
| 1 | Entities (AuditLog, SecurityAuditLog), IAuditableEntity marker on whitelisted entities, EF configs, migration, ICurrentUserService + impl, DI registration | ~300 |
| 2 | SaveChangesAsync override + AuditLog unit tests | ~250 |
| 3 | ISecurityAuditWriter + impl, auth handler modifications + integration tests | ~350 |
| 4 | Read endpoints (both) + integration tests | ~300 |
| 5 | Retention service + tests | ~200 |

Each PR is independently deployable and testable. PR 1 is schema-only (no behavior change). PRs 2-5 can proceed sequentially after PR 1.

## Open Questions

- None — all decisions confirmed in the user context block.
