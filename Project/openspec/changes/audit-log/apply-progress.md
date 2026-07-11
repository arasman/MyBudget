# Apply Progress: audit-log

## PR1 — Foundation: Entities, EF Configs, Migration, Interfaces, DI

### Status: COMPLETE

### Tasks

- [x] 1.1 Create `SharedKernel/Entities/IAuditableEntity.cs` — marker interface with `Guid? ResolveBudgetId()`
- [x] 1.2 Create `SharedKernel/Entities/AuditLog.cs` — standalone entity (Guid PK, EntityName, EntityId, Action, UserId?, Timestamp UTC, BeforeJson?, AfterJson?, BudgetId?)
- [x] 1.3 Create `SharedKernel/Entities/SecurityAuditLog.cs` — standalone entity (Guid PK, Event, UserId?, Email?, IpAddress?, UserAgent?, Timestamp UTC, Details text?)
- [x] 1.4 Create `SharedKernel/Persistence/Configurations/AuditLogConfiguration.cs` — indexes on (BudgetId, Timestamp DESC), (EntityName, EntityId), (UserId)
- [x] 1.5 Create `SharedKernel/Persistence/Configurations/SecurityAuditLogConfiguration.cs` — indexes on (UserId), (Event), (Timestamp DESC)
- [x] 1.6 Modify `SharedKernel/Persistence/AppDbContext.cs` — add `DbSet<AuditLog>` and `DbSet<SecurityAuditLog>`
- [x] 1.7 Implement `IAuditableEntity` on Budget (returns `Id`), Cycle (returns `BudgetId`), CategoryGroup (returns `BudgetId`)
- [x] 1.8 Implement `IAuditableEntity` on Period, Category, BudgetLine, BudgetLineRevision — returns `null` (Dapper fallback at audit time)
- [x] 1.9 Create `SharedKernel/Services/ICurrentUserService.cs` — `Guid? UserId { get; }`
- [x] 1.10 Create `SharedKernel/Services/HttpContextCurrentUserService.cs` — scoped, reads `ClaimTypes.NameIdentifier` from `IHttpContextAccessor`; returns null when no context
- [x] 1.11 Create `SharedKernel/Services/ISecurityAuditWriter.cs` — `Task WriteAsync(string eventName, Guid? userId, string? email, object? details, CancellationToken ct)`
- [x] 1.12 Create `SharedKernel/Services/IAuditRetentionPolicy.cs` — `int GetRetentionDays()`
- [x] 1.13 Create `SharedKernel/Services/AppSettingsAuditRetentionPolicy.cs` — reads `AuditLog:RetentionDays` from `IConfiguration`, default 90
- [x] 1.14 Modify `Extensions/ServiceCollectionExtensions.cs` — register `AddHttpContextAccessor`, scoped `ICurrentUserService`, scoped `ISecurityAuditWriter`, singleton `IAuditRetentionPolicy`, hosted `AuditRetentionService`
- [x] 1.15 Add EF migration for both tables (single migration file)

### Additional files created (stubs for compilation)

- `SharedKernel/Services/AuditRetentionService.cs` — stub IHostedService (empty Start/StopAsync); full implementation in PR5
- `SharedKernel/Services/NullSecurityAuditWriter.cs` — no-op ISecurityAuditWriter; replaced by SecurityAuditWriter in PR3

### Build & Test Results

- Build: PASS — 0 errors, 8 warnings (pre-existing SQLitePCLRaw vulnerability warning)
- Unit tests: PASS — 218/218
- Integration tests: PASS — 90/90
- Total: 308/308

### Migration

- File: `src/MyBudget.Features/Migrations/20260711225020_AddAuditLogTables.cs`
- Creates: `AuditLogs` table, `SecurityAuditLogs` table, all required indexes

## Remaining PRs

- [ ] PR2 — SaveChangesAsync override + unit tests
- [ ] PR3 — ISecurityAuditWriter impl + auth handler modifications + integration tests
- [ ] PR4 — Read endpoints (GetAuditLog, GetSecurityAuditLog) + integration tests
- [ ] PR5 — AuditRetentionService (full implementation) + tests
