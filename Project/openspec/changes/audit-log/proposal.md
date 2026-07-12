# Proposal: Audit Log

## Intent

The application has no visibility into entity mutations or security events. Budget owners cannot see who changed what, and there is no record of failed logins, token refreshes, or password changes. This change introduces two audit tables with read endpoints and a configurable retention policy, establishing the observability foundation before budget execution features arrive.

## Scope

### In Scope
- `AuditLog` entity + EF config: intercept `SaveChangesAsync` for budget-domain entities (Budget, Cycle, Period, CategoryGroup, Category, BudgetLine, BudgetLineRevision)
- `SecurityAuditLog` entity + EF config: explicit writes in auth handlers (LoginUser, LogoutUser, RefreshToken, AcceptInvitation, RegisterUser)
- `GET /budgets/{budgetId}/audit-log` — paginated, Owner/Admin only
- `GET /budgets/{budgetId}/security-audit-log` — paginated, Owner/Admin only
- `IAuditRetentionPolicy` abstraction + `AppSettingsAuditRetentionPolicy` (reads `AuditLog:RetentionDays`, default 90)
- Retention cleanup background job (hosted service or similar)
- EF migration for both tables

### Out of Scope
- User deletion / anonymization of audit records
- Per-user or DB-driven retention policies (future swap via `IAuditRetentionPolicy`)
- Audit log export (CSV, PDF)
- Real-time audit streaming / webhooks
- UI for audit log viewing

## Capabilities

### New Capabilities
- `audit-log`: Domain entity mutation tracking via SaveChangesAsync interception, read endpoint with budget-scoped authorization
- `security-audit-log`: Auth/security event recording with explicit writes, read endpoint with budget-scoped authorization
- `audit-retention`: Configurable TTL-based cleanup with abstracted policy interface

### Modified Capabilities
- None

## Approach

1. **Two-table design**: `AuditLog` (entity mutations with before/after JSON) and `SecurityAuditLog` (auth events with IP, user-agent, details). Different schemas, different write paths, different query patterns.
2. **SaveChangesAsync override** in `AppDbContext` for `AuditLog` — whitelisted entity types only, captures Created/Updated/Deleted/Restored actions with before/after snapshots.
3. **Explicit writes** in auth handlers for `SecurityAuditLog` — each handler writes its own event (FailedLogin, SuccessfulLogin, PasswordChanged, TokenRefreshed, TokenRevoked, AccountLocked).
4. **Authorization**: Both read endpoints require `BudgetRole >= Admin` (covers Admin=30 and Owner=40).
5. **Retention**: `IAuditRetentionPolicy` returns TTL; `AppSettingsAuditRetentionPolicy` reads `AuditLog:RetentionDays` from appsettings. Background cleanup deletes records older than TTL.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `SharedKernel/Persistence/AppDbContext.cs` | Modified | Override SaveChangesAsync, add AuditLog + SecurityAuditLog DbSets |
| `SharedKernel/Entities/` | New | AuditLog, SecurityAuditLog entities |
| `SharedKernel/Persistence/Configurations/` | New | EF configs for both audit tables |
| `Features/Auth/*/Handler.cs` | Modified | Add SecurityAuditLog writes to Login, Logout, RefreshToken, AcceptInvitation, Register handlers |
| `Features/AuditLog/` | New | Query handlers + endpoints for both read surfaces |
| `SharedKernel/Services/` | New | IAuditRetentionPolicy, AppSettingsAuditRetentionPolicy, cleanup hosted service |
| `Migrations/` | New | Migration for AuditLog + SecurityAuditLog tables |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| SaveChangesAsync overhead on hot write paths | Low | Whitelist-only interception; before/after JSON serialized only for tracked entities |
| Large audit tables over time | Med | 90-day TTL default with background cleanup; index on Timestamp + BudgetId |
| Auth handler changes break existing tests | Low | Each handler gets minimal SecurityAuditLog write; existing behavior unchanged |
| SecurityAuditLog endpoint scoped to budget but some events are user-global | Med | Login/register events use UserId; budget-scoped endpoint filters by user's budget memberships |

## Rollback Plan

1. Revert the EF migration (`dotnet ef database update <previous-migration>`)
2. Remove SaveChangesAsync override — AppDbContext returns to passthrough
3. Remove SecurityAuditLog writes from auth handlers
4. Remove audit read endpoints and retention service
5. All changes are additive; no existing schema or behavior is modified destructively

## Dependencies

- Existing `BudgetRole` enum and budget authorization infrastructure (already in place)
- `IHttpContextAccessor` for capturing UserId, IP, and UserAgent in security events

## Success Criteria

- [ ] All budget-domain entity mutations produce AuditLog records with correct before/after JSON
- [ ] Auth events (login success/failure, token refresh, logout, registration, invitation acceptance) produce SecurityAuditLog records
- [ ] Both read endpoints return paginated results, restricted to Admin/Owner roles
- [ ] Unauthorized users receive 403 on audit endpoints
- [ ] Retention cleanup deletes records older than configured TTL
- [ ] Retention TTL is configurable via `AuditLog:RetentionDays` in appsettings
- [ ] Existing tests remain green; no regressions in auth or budget-structure flows
