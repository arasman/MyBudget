# Tasks: Audit Log

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~1400 total (5 PRs × ~280 avg) |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR1 → PR2 → PR3 → PR4 → PR5 (stacked-to-main) |
| Delivery strategy | ask-on-risk |
| Chain strategy | stacked-to-main |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Schema, entities, EF configs, migration, DI | PR1 | Base for all PRs; no behavior change |
| 2 | SaveChangesAsync override + unit tests | PR2 | Depends on PR1; behavior added |
| 3 | ISecurityAuditWriter + auth handlers + integration tests | PR3 | Depends on PR1; parallel to PR2 conceptually, sequential in stacked chain |
| 4 | Read endpoints (GetAuditLog, GetSecurityAuditLog) + integration tests | PR4 | Depends on PR1 and PR2 |
| 5 | AuditRetentionService + retention tests | PR5 | Depends on PR1; independent of PR2/3/4 |

---

## Phase 1: Foundation — Entities, Schema, DI (PR1)

- [x] 1.1 Create `SharedKernel/Entities/IAuditableEntity.cs` — marker interface with `Guid? ResolveBudgetId()`
- [x] 1.2 Create `SharedKernel/Entities/AuditLog.cs` — standalone entity (Guid PK, EntityName, EntityId, Action, UserId, Timestamp UTC, BeforeJson, AfterJson, BudgetId)
- [x] 1.3 Create `SharedKernel/Entities/SecurityAuditLog.cs` — standalone entity (Guid PK, Event, UserId, Email, IpAddress, UserAgent, Timestamp UTC, Details jsonb)
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

---

## Phase 2: SaveChangesAsync Override + Unit Tests (PR2)

- [x] 2.1 Modify `SharedKernel/Persistence/AppDbContext.cs` — override `SaveChangesAsync`: scan `ChangeTracker.Entries<BaseEntity>()`, filter `IAuditableEntity`, detect action via `State` and `DeletedAt` transitions
- [x] 2.2 Add BudgetId resolution logic in override — direct `ResolveBudgetId()` for Budget/Cycle/CategoryGroup; Dapper fallback for Period, Category, BudgetLine, BudgetLineRevision
- [x] 2.3 Add snapshot logic — `System.Text.Json` serialize `OriginalValues`/`CurrentValues` dictionaries; `BeforeJson=null` for Created; `AfterJson=null` for Deleted
- [x] 2.4 Write unit test — `SaveChangesAsync` with whitelisted entity in `Added` state produces `AuditLog` with `Action=Created`, `BeforeJson=null`, `AfterJson` populated (spec: Created entity scenario)
- [x] 2.5 Write unit test — Modified entity (no DeletedAt change) produces `AuditLog` with `Action=Updated`, both snapshots populated (spec: Updated entity scenario)
- [x] 2.6 Write unit test — Modified entity `DeletedAt` null→value produces `Action=Deleted`, `AfterJson=null` (spec: Deleted entity scenario)
- [x] 2.7 Write unit test — Modified entity `DeletedAt` value→null produces `Action=Restored`
- [x] 2.8 Write unit test — non-whitelisted entity save produces zero `AuditLog` rows (spec: Non-whitelisted entity scenario)
- [x] 2.9 Write unit test — no authenticated user → `AuditLog.UserId = null` (spec: Unauthenticated context scenario)

---

## Phase 3: SecurityAuditWriter + Auth Handlers + Integration Tests (PR3)

- [ ] 3.1 Create `SharedKernel/Services/SecurityAuditWriter.cs` — extracts `IpAddress` and `UserAgent` from `IHttpContextAccessor`, builds `SecurityAuditLog`, saves via `AppDbContext`
- [ ] 3.2 Modify `Features/Auth/LoginUser/LoginUserHandler.cs` — inject `ISecurityAuditWriter`; write `SuccessfulLogin` on success, `FailedLogin` on wrong password or user not found
- [ ] 3.3 Modify `Features/Auth/RegisterUser/RegisterUserHandler.cs` — inject `ISecurityAuditWriter`; write `AccountRegistered` after user created
- [ ] 3.4 Modify `Features/Auth/AcceptInvitation/AcceptInvitationHandler.cs` — inject `ISecurityAuditWriter`; write `InvitationAccepted`
- [ ] 3.5 Modify `Features/Auth/RefreshToken/RefreshTokenHandler.cs` — inject `ISecurityAuditWriter`; write `TokenRefreshed`
- [ ] 3.6 Modify `Features/Auth/LogoutUser/LogoutUserHandler.cs` — inject `ISecurityAuditWriter`; write `TokenRevoked`
- [ ] 3.7 Write unit test — `SecurityAuditWriter` extracts `IpAddress` and `UserAgent` from mock `IHttpContextAccessor` correctly
- [ ] 3.8 Write integration test — `POST /auth/login` with valid credentials → `SecurityAuditLog` row with `Event=SuccessfulLogin`, `UserId` and `Email` populated (spec: Successful login scenario)
- [ ] 3.9 Write integration test — `POST /auth/login` with invalid credentials → `SecurityAuditLog` row with `Event=FailedLogin` (spec: Failed login scenario)
- [ ] 3.10 Write integration test — `POST /auth/refresh` → `SecurityAuditLog` row with `Event=TokenRefreshed` (spec: Token refresh scenario)
- [ ] 3.11 Write integration test — `POST /auth/logout` → `SecurityAuditLog` row with `Event=TokenRevoked` (spec: Logout scenario)
- [ ] 3.12 Write integration test — `POST /auth/register` → `SecurityAuditLog` row with `Event=AccountRegistered` (spec: Registration scenario)
- [ ] 3.13 Write integration test — `POST /auth/accept-invitation` → `SecurityAuditLog` row with `Event=InvitationAccepted` (spec: Invitation acceptance scenario)

---

## Phase 4: Read Endpoints + Integration Tests (PR4)

- [ ] 4.1 Create `Features/AuditLog/GetAuditLog/GetAuditLogQuery.cs` — record with `BudgetId`, `Page`, `PageSize`, `EntityName?`, `Action?`, `From?`, `To?`
- [ ] 4.2 Create `Features/AuditLog/GetAuditLog/GetAuditLogHandler.cs` — Dapper paginated query on `AuditLogs` filtered by `BudgetId` and optional filters
- [ ] 4.3 Create `Features/AuditLog/GetAuditLog/GetAuditLogEndpoint.cs` — `GET /budgets/{budgetId}/audit-log`, `budget:admin` authorization
- [ ] 4.4 Create `Features/AuditLog/GetSecurityAuditLog/GetSecurityAuditLogQuery.cs` — record with `BudgetId`, `Page`, `PageSize`
- [ ] 4.5 Create `Features/AuditLog/GetSecurityAuditLog/GetSecurityAuditLogHandler.cs` — Dapper paginated query JOIN `BudgetMemberships` to scope security events to budget members
- [ ] 4.6 Create `Features/AuditLog/GetSecurityAuditLog/GetSecurityAuditLogEndpoint.cs` — `GET /budgets/{budgetId}/security-audit-log`, `budget:admin` authorization
- [ ] 4.7 Write integration test — Admin calls `GET /budgets/{id}/audit-log` → `200 OK` with paginated entries (spec: Admin retrieves audit log)
- [ ] 4.8 Write integration test — Member calls `GET /budgets/{id}/audit-log` → `403 Forbidden` (spec: Member cannot access audit log)
- [ ] 4.9 Write integration test — Filter by `entityName=Category&from=...&to=...` returns only matching rows (spec: Filter by EntityName and date range)
- [ ] 4.10 Write integration test — Owner calls `GET /budgets/{id}/security-audit-log` → `200 OK`, only events from budget members included (spec: Owner retrieves security audit log / security events not in budget membership are excluded)
- [ ] 4.11 Write integration test — Non-member calls `GET /budgets/{id}/security-audit-log` → `403 Forbidden` (spec: Non-member cannot access security audit log)

---

## Phase 5: Retention Service + Tests (PR5)

- [ ] 5.1 Create `SharedKernel/Services/AuditRetentionService.cs` — `IHostedService` with daily `PeriodicTimer`; deletes `AuditLogs` and `SecurityAuditLogs` where `Timestamp < now - TTL`
- [ ] 5.2 Write unit test — `AppSettingsAuditRetentionPolicy` reads `AuditLog:RetentionDays` from mock `IConfiguration`; returns configured value (spec: TTL is configurable)
- [ ] 5.3 Write unit test — `AppSettingsAuditRetentionPolicy` returns 90 when key is absent (spec: Default TTL applies when setting is absent)
- [ ] 5.4 Write integration test — seed `AuditLog` rows with `Timestamp < now - 90d`, trigger service, verify rows deleted (spec: Records older than TTL are deleted)
- [ ] 5.5 Write integration test — seed `AuditLog` rows with `Timestamp` within TTL window, trigger service, verify rows preserved (spec: Records within TTL are preserved)
- [ ] 5.6 Add `AuditLog:RetentionDays` entry to `appsettings.json` / `appsettings.Development.json`

---

## Test Groups Summary

| Group | Status | Location |
|-------|--------|----------|
| Backend unit tests | Included — Phases 2, 3, 5 | `Tests/Unit/` |
| Backend integration tests | Included — Phases 3, 4, 5 | `Tests/Integration/` |
| Frontend unit/component tests | N/A — backend-only change | — |
| E2E tests | N/A — no UI interaction; covered by integration tests | — |
