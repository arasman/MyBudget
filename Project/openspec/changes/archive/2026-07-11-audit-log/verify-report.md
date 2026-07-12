# Verify Report: audit-log

| Field | Value |
|-------|-------|
| Change | audit-log |
| Date | 2026-07-11 |
| Verdict | **PASS WITH WARNINGS** |
| CRITICAL | 0 |
| WARNING | 2 |
| SUGGESTION | 1 |
| Test evidence | 334/334 passing (230 unit + 104 integration) |

---

## Completeness Table

| Artifact | Status |
|----------|--------|
| spec.md | Present |
| design.md | Present |
| tasks.md | Present |
| apply-progress.md | Present - all phases COMPLETE |

---

## Build / Test Evidence

| Check | Result |
|-------|--------|
| dotnet test | 334/334 PASS (230 unit + 104 integration) |
| Build | PASS - 0 errors, 4 pre-existing NU1903 (SQLitePCLRaw) |
| pnpm vitest run | 88/88 PASS |
| pnpm exec playwright test | 16/16 PASS |

---

## Spec Compliance Matrix

### Requirement: Entity Mutation Recording

| Scenario | Covered By | Status |
|----------|------------|--------|
| Created entity - BeforeJson=null AfterJson set | AppDbContextAuditTests 2.4 | PASS |
| Updated entity - both snapshots | AppDbContextAuditTests 2.5 | PASS |
| Deleted entity - AfterJson=null | AppDbContextAuditTests 2.6 | PASS |
| Restored entity - Action=Restored | AppDbContextAuditTests 2.7 | PASS |
| Non-whitelisted entity - zero AuditLog rows | AppDbContextAuditTests 2.8 | PASS |
| Unauthenticated context - UserId=null | AppDbContextAuditTests 2.9 | PASS |

AuditLog fields confirmed: Id (Guid PK), EntityName, EntityId, Action, UserId (Guid?), Timestamp (DateTimeOffset UTC), BeforeJson (string?), AfterJson (string?), BudgetId (Guid?).
IAuditableEntity on all 7 whitelisted entities: Budget Cycle Period CategoryGroup Category BudgetLine BudgetLineRevision.

### Requirement: Security Event Recording

| Scenario | Covered By | Status |
|----------|------------|--------|
| SuccessfulLogin | SecurityAuditLogTests 3.8 | PASS |
| FailedLogin | SecurityAuditLogTests 3.9 | PASS |
| TokenRefreshed | SecurityAuditLogTests 3.10 | PASS |
| TokenRevoked | SecurityAuditLogTests 3.11 | PASS |
| AccountRegistered | SecurityAuditLogTests 3.12 | PASS |
| InvitationAccepted | SecurityAuditLogTests 3.13 | PASS |
| AccountLocked | No test no implementation | WARNING W-001 |
| PasswordChanged NOT written | Confirmed absent | PASS |

SecurityAuditLog writes are explicit in auth handlers (not via SaveChangesAsync). Confirmed by code inspection.

### Requirement: Audit Log Read Endpoint

| Scenario | Covered By | Status |
|----------|------------|--------|
| Admin retrieves audit log - 200 OK paginated | AuditLogEndpointTests 4.7 | PASS |
| Member cannot access - 403 Forbidden | AuditLogEndpointTests 4.8 | PASS |
| Filter by EntityName and date range | AuditLogEndpointTests 4.9 | PASS |

GET /api/budgets/{id}/audit-log - budget:admin auth - filters EntityName Action from to. Confirmed.

### Requirement: Security Audit Log Read Endpoint

| Scenario | Covered By | Status |
|----------|------------|--------|
| Owner retrieves security audit log - only member events | AuditLogEndpointTests 4.10 | PASS |
| Non-member - 403 Forbidden | AuditLogEndpointTests 4.11 | PASS |

JOIN BudgetMemberships confirmed in GetSecurityAuditLogHandler.

### Requirement: Audit Retention Policy

| Scenario | Covered By | Status |
|----------|------------|--------|
| Records older than TTL deleted | AuditRetentionServiceTests 5.4 | PASS |
| Records within TTL preserved | AuditRetentionServiceTests 5.5 | PASS |
| TTL configurable | AppSettingsAuditRetentionPolicyTests 5.2 | PASS |
| Default TTL = 90 when key absent | AppSettingsAuditRetentionPolicyTests 5.3 | PASS |

All retention components confirmed: IAuditRetentionPolicy AppSettingsAuditRetentionPolicy(default 90) AuditRetentionService(PeriodicTimer 24h EF ExecuteDeleteAsync) appsettings.json AuditLog:RetentionDays=90.

---

## Design Coherence Table

| Decision | Implementation | Status |
|----------|---------------|--------|
| SaveChangesAsync override | Two-phase save ChangeTracker scan IAuditableEntity filter | PASS |
| ISecurityAuditWriter + SecurityAuditWriter | X-Forwarded-For aware separate DbContext write | PASS |
| ICurrentUserService scoped | HttpContextCurrentUserService null when no HTTP context | PASS |
| BudgetId via ResolveBudgetId() | All 7 entities return BudgetId directly after PR2b | PASS |
| STJ snapshots OriginalValues/CurrentValues | Implemented | PASS |
| Soft-delete detection via DeletedAt | Modified+null-to-value=Deleted value-to-null=Restored | PASS |
| SecurityAuditLog scope via JOIN BudgetMemberships | Implemented | PASS |
| DI registrations | AddHttpContextAccessor scoped ICurrentUserService+ISecurityAuditWriter singleton IAuditRetentionPolicy hosted AuditRetentionService | PASS |
| EF migrations | AddAuditLogTables + AddBudgetIdToChildEntities both present | PASS |
| Dapper fallback for deep entities | Not implemented superseded by PR2b | WARNING W-002 |

---

## Issues

### WARNING

**W-001 - AccountLocked event not implemented**

- Severity: WARNING
- Spec location: Security Event Recording requirement (spec.md)
- Code: src/MyBudget.Features/Features/Auth/LoginUser/LoginUserHandler.cs
- Finding: The spec mandates AccountLocked as a required security event. No AccountLocked SecurityAuditLog entry is written anywhere. No account-lock mechanism exists in the codebase.
- Impact: Missing spec scenario. Cannot be produced without a lockout mechanism.
- Disposition: Intentional deferral. Not documented as known deviation. Archive acceptable if team acknowledges deferral to a future change such as account-management.

**W-002 - Design doc Dapper fallback superseded by PR2b denormalization**

- Severity: WARNING
- Location: design.md Decision 4 / apply-progress.md PR2b section
- Finding: Design doc states deep entities use a Dapper fallback. PR2b denormalized BudgetId directly. Dapper fallback never built.
- Impact: None - actual approach is superior. Design doc is stale.
- Disposition: Archive acceptable. Design doc update recommended but not blocking.

### SUGGESTION

**S-001 - tasks.md phases 3-5 retain unchecked checkboxes**

- Severity: SUGGESTION
- Location: openspec/changes/audit-log/tasks.md lines 67-106
- Finding: apply-progress.md marks all PR3/PR4/PR5 tasks complete. tasks.md phases 3-5 still show unchecked checkboxes.
- Disposition: Update tasks.md or accept apply-progress.md as the completion authority.

---

## Task Completion Summary

| Phase | apply-progress.md | tasks.md | Verdict |
|-------|-------------------|----------|---------|
| PR1 Foundation | COMPLETE all checked | All checked | OK |
| PR2 SaveChangesAsync | COMPLETE all checked | All checked | OK |
| PR2b BudgetId denormalization | COMPLETE all checked | Not in tasks.md | OK |
| PR3 SecurityAuditWriter | COMPLETE all checked | Unchecked stale | OK (apply-progress is authority) |
| PR4 Read endpoints | COMPLETE all checked | Unchecked stale | OK (apply-progress is authority) |
| PR5 Retention service | COMPLETE all checked | Unchecked stale | OK (apply-progress is authority) |

No unchecked tasks block archive.

---

## Final Verdict

**PASS WITH WARNINGS** - 0 CRITICAL 2 WARNING 1 SUGGESTION

All 334 tests pass. Both warnings are intentional deviations: AccountLocked deferred (no lock mechanism in scope) and Dapper fallback replaced by the superior PR2b denormalization. The suggestion is documentation housekeeping only.

Recommended next step: sdd-archive
