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

## PR2 — SaveChangesAsync Override + Unit Tests

### Status: COMPLETE

### Tasks

- [x] 2.1 Modify `SharedKernel/Persistence/AppDbContext.cs` — override `SaveChangesAsync`: scan `ChangeTracker.Entries<BaseEntity>()`, filter `IAuditableEntity`, detect action via `State` and `DeletedAt` transitions
- [x] 2.2 Add BudgetId resolution logic in override — direct `ResolveBudgetId()` for Budget/Cycle/CategoryGroup; null returned for Period, Category, BudgetLine, BudgetLineRevision (Dapper fallback deferred to PR4 read path, not needed at write time for unit tests)
- [x] 2.3 Add snapshot logic — `System.Text.Json` serialize `OriginalValues`/`CurrentValues` dictionaries; `BeforeJson=null` for Created; `AfterJson=null` for Deleted
- [x] 2.4 Unit test — `SaveChangesAsync` with whitelisted entity in `Added` state → `AuditLog` with `Action=Created`, `BeforeJson=null`, `AfterJson` populated
- [x] 2.5 Unit test — Modified entity (no DeletedAt change) → `AuditLog` with `Action=Updated`, both snapshots populated
- [x] 2.6 Unit test — Modified entity `DeletedAt` null→value → `Action=Deleted`, `AfterJson=null`
- [x] 2.7 Unit test — Modified entity `DeletedAt` value→null → `Action=Restored`
- [x] 2.8 Unit test — non-whitelisted entity save → zero `AuditLog` rows
- [x] 2.9 Unit test — no authenticated user → `AuditLog.UserId = null`

### Implementation notes

- `ICurrentUserService` added as optional constructor parameter (`= null`) — backward-compatible with `DbTestHelpers`, `AppDbContextFactory`, and existing tests
- Two-phase `SaveChangesAsync`: business save first (so EF-generated PKs are set for Added entries), then audit rows saved in a second `base.SaveChangesAsync` call
- Hard-delete (`EntityState.Deleted`) treated as `Action=Deleted` with `BeforeJson` populated, `AfterJson=null`
- Soft-delete detected via `DeletedAt` property inspection in ChangeTracker; entities without `DeletedAt` (e.g. `BudgetLineRevision`) fall through to `Updated`
- Snapshot serialization uses `OriginalValues.Properties` / `CurrentValues.Properties` (scalar properties only — no navigation)

### Build & Test Results

- Build: PASS — 0 errors, 4 warnings (pre-existing SQLitePCLRaw vulnerability warning)
- Unit tests: PASS — 224/224 (+6 new audit tests)
- Integration tests: PASS — 90/90
- Total: 314/314

### Files changed

- `src/MyBudget.Features/SharedKernel/Persistence/AppDbContext.cs` — modified
- `tests/MyBudget.Features.Tests/SharedKernel/Persistence/AppDbContextAuditTests.cs` — created

## PR2b — Schema denormalization: BudgetId onto Period, Category, BudgetLine, BudgetLineRevision

### Status: COMPLETE

### Tasks

- [x] 2b.1 Add `BudgetId` property to Period, Category, BudgetLine, BudgetLineRevision entities
- [x] 2b.2 Update `Create()` factory methods on all four entities to accept and set `budgetId`
- [x] 2b.3 Update `ResolveBudgetId()` on all four entities to return `BudgetId` directly (was `null`)
- [x] 2b.4 Update EF configurations — add `.Property(BudgetId).IsRequired()` + FK to Budgets table + BudgetId index
- [x] 2b.5 Update CreatePeriodHandler to pass `cycle.BudgetId` to `Period.Create()`
- [x] 2b.6 Update CreateCategoryHandler to pass `group.BudgetId` to `Category.Create()`
- [x] 2b.7 Update CreateBudgetLineHandler to pass `budgetId` to `BudgetLine.Create()` and `BudgetLineRevision.Create()`
- [x] 2b.8 Update UpdateBudgetLineHandler to pass `line.Period.Cycle.BudgetId` to `BudgetLineRevision.Create()`
- [x] 2b.9 Update all test files that call entity factories (20+ call sites across 12 test files)
- [x] 2b.10 Add EF migration `AddBudgetIdToChildEntities`
- [x] 2b.11 Build (0 errors) and test (314/314 pass)

### Implementation notes

- `BudgetId` placed as first property after `BaseEntity` fields, before parent FK, consistent with `CategoryGroup` pattern
- Handlers already loaded the necessary parent entities (Cycle/Group) for authorization checks — `BudgetId` extracted from those, no extra DB queries needed
- `UpdateBudgetLineHandler` uses `line.Period!.Cycle!.BudgetId` (already loaded via Include chain) for the new revision
- Migration adds columns with `defaultValue: Guid.Empty` (EF default for non-nullable) — safe for empty tables in test/dev; existing production data would need a backfill SQL script

### Build & Test Results

- Build: PASS — 0 errors, 4 warnings (pre-existing SQLitePCLRaw vulnerability warning)
- Unit tests: PASS — 224/224
- Integration tests: PASS — 90/90
- Total: 314/314

### Files changed

**Entities (src/MyBudget.Features/SharedKernel/Entities/)**
- `Period.cs` — added `BudgetId`, updated `Create()`, updated `ResolveBudgetId()`
- `Category.cs` — added `BudgetId`, updated `Create()`, updated `ResolveBudgetId()`
- `BudgetLine.cs` — added `BudgetId`, updated `Create()`, updated `ResolveBudgetId()`
- `BudgetLineRevision.cs` — added `BudgetId`, updated `Create()`, updated `ResolveBudgetId()`

**EF Configurations (src/MyBudget.Features/SharedKernel/Persistence/Configurations/)**
- `PeriodConfiguration.cs` — BudgetId property + FK + index
- `CategoryConfiguration.cs` — BudgetId property + FK + index
- `BudgetLineConfiguration.cs` — BudgetId property + FK + index
- `BudgetLineRevisionConfiguration.cs` — BudgetId property + FK + index

**Handlers (src/MyBudget.Features/Features/BudgetStructure/)**
- `CreatePeriod/CreatePeriodHandler.cs`
- `CreateCategory/CreateCategoryHandler.cs`
- `CreateBudgetLine/CreateBudgetLineHandler.cs`
- `UpdateBudgetLine/UpdateBudgetLineHandler.cs`

**Migration**
- `src/MyBudget.Features/Migrations/20260711233322_AddBudgetIdToChildEntities.cs`
- `src/MyBudget.Features/Migrations/20260711233322_AddBudgetIdToChildEntities.Designer.cs`

**Tests (12 test files updated)**
- `SharedKernel/Entities/PeriodEntityTests.cs`
- `SharedKernel/Entities/CategoryEntityTests.cs`
- `SharedKernel/Entities/BudgetLineEntityTests.cs`
- `SharedKernel/Entities/BudgetLineRevisionEntityTests.cs`
- `Features/BudgetStructure/CreateBudgetLine/CreateBudgetLineHandlerTests.cs`
- `Features/BudgetStructure/UpdateBudgetLine/UpdateBudgetLineHandlerTests.cs`
- `Features/BudgetStructure/DeleteBudgetLine/DeleteBudgetLineHandlerTests.cs`
- `Features/BudgetStructure/RestoreBudgetLine/RestoreBudgetLineHandlerTests.cs`
- `Features/BudgetStructure/ReorderBudgetLines/ReorderBudgetLinesHandlerTests.cs`
- `Features/BudgetStructure/ReorderCategories/ReorderCategoriesHandlerTests.cs`
- `Features/BudgetStructure/RestoreCategory/RestoreCategoryHandlerTests.cs`
- `Features/BudgetStructure/RestoreCategoryGroup/RestoreCategoryGroupHandlerTests.cs`
- `Features/BudgetStructure/RestoreCycle/RestoreCycleHandlerTests.cs`

### Commit

`feat(audit-log): denormalize BudgetId onto Period, Category, BudgetLine, BudgetLineRevision`

## PR3 — SecurityAuditWriter + Auth Handler Events + Integration Tests

### Status: COMPLETE

### Tasks

- [x] 3.1 Create `SharedKernel/Services/SecurityAuditWriter.cs` — extracts IpAddress (X-Forwarded-For → RemoteIpAddress fallback) and UserAgent from IHttpContextAccessor; builds SecurityAuditLog; saves via AppDbContext.SaveChangesAsync (separate from business save)
- [x] 3.2 Modify `Features/Auth/LoginUser/LoginUserHandler.cs` — inject ISecurityAuditWriter; write SuccessfulLogin on success, FailedLogin on invalid credentials
- [x] 3.3 Modify `Features/Auth/RegisterUser/RegisterUserHandler.cs` — inject ISecurityAuditWriter; write AccountRegistered after user created
- [x] 3.4 Modify `Features/Auth/AcceptInvitation/AcceptInvitationHandler.cs` — inject ISecurityAuditWriter; write InvitationAccepted
- [x] 3.5 Modify `Features/Auth/RefreshToken/RefreshTokenHandler.cs` — inject ISecurityAuditWriter; write TokenRefreshed
- [x] 3.6 Modify `Features/Auth/LogoutUser/LogoutUserHandler.cs` — inject ISecurityAuditWriter; write TokenRevoked
- [x] 3.7 Unit tests — SecurityAuditWriter extracts IpAddress and UserAgent from mock IHttpContextAccessor (4 tests)
- [x] 3.8 Integration test — POST /auth/login valid creds → SecurityAuditLog row Event=SuccessfulLogin, UserId + Email populated
- [x] 3.9 Integration test — POST /auth/login invalid creds → SecurityAuditLog row Event=FailedLogin
- [x] 3.10 Integration test — POST /auth/refresh → SecurityAuditLog row Event=TokenRefreshed
- [x] 3.11 Integration test — POST /auth/logout → SecurityAuditLog row Event=TokenRevoked
- [x] 3.12 Integration test — POST /auth/register → SecurityAuditLog row Event=AccountRegistered
- [x] 3.13 Integration test — POST /auth/invitations/accept → SecurityAuditLog row Event=InvitationAccepted

### Implementation notes

- SecurityAuditWriter is `public sealed` (not `internal`) so unit tests can instantiate it directly
- X-Forwarded-For parsed as comma-separated; first value taken as client IP
- Null HttpContext (background jobs) handled gracefully — IpAddress=null, UserAgent=null, no throw
- DI registration updated in ServiceCollectionExtensions: `NullSecurityAuditWriter` → `SecurityAuditWriter`
- IntegrationTestFactory.CleanDatabaseAsync updated to clear AuditLogs + SecurityAuditLogs tables
- FailedLogin for unknown email: UserId=null (user not found, no row to resolve)
- FailedLogin for wrong password: UserId=row.Id (user exists, password mismatch)
- InvitationAccepted test seeds invitation directly with known raw token (BCrypt, workFactor 4 for speed)
- ClearAuditLogsAsync helper in SecurityAuditLogTests resets audit tables between test phases

### Build & Test Results

- Build: PASS — 0 errors, pre-existing SQLitePCLRaw vulnerability warning only
- Unit tests: PASS — 228/228 (+4 new SecurityAuditWriter tests)
- Integration tests: PASS — 97/97 (+7 new SecurityAuditLog integration tests)
- Total: 325/325

### Files changed

**New files**
- `src/MyBudget.Features/SharedKernel/Services/SecurityAuditWriter.cs`
- `tests/MyBudget.Features.Tests/SharedKernel/Services/SecurityAuditWriterTests.cs`
- `tests/MyBudget.Integration.Tests/Features/Auth/SecurityAuditLogTests.cs`

**Modified files**
- `src/MyBudget.Features/Extensions/ServiceCollectionExtensions.cs` — NullSecurityAuditWriter → SecurityAuditWriter
- `src/MyBudget.Features/Features/Auth/LoginUser/LoginUserHandler.cs` — inject ISecurityAuditWriter; SuccessfulLogin + FailedLogin
- `src/MyBudget.Features/Features/Auth/RegisterUser/RegisterUserHandler.cs` — inject ISecurityAuditWriter; AccountRegistered
- `src/MyBudget.Features/Features/Auth/AcceptInvitation/AcceptInvitationHandler.cs` — inject ISecurityAuditWriter; InvitationAccepted
- `src/MyBudget.Features/Features/Auth/RefreshToken/RefreshTokenHandler.cs` — inject ISecurityAuditWriter; TokenRefreshed
- `src/MyBudget.Features/Features/Auth/LogoutUser/LogoutUserHandler.cs` — inject ISecurityAuditWriter; TokenRevoked
- `tests/MyBudget.Integration.Tests/Infrastructure/IntegrationTestFactory.cs` — CleanDatabaseAsync clears AuditLogs + SecurityAuditLogs

## Remaining PRs

- [ ] PR4 — Read endpoints (GetAuditLog, GetSecurityAuditLog) + integration tests
- [ ] PR5 — AuditRetentionService (full implementation) + tests
