# Audit Log Specification

## Purpose

Define behavioral requirements for entity mutation tracking (`AuditLog`), auth/security event recording (`SecurityAuditLog`), and configurable retention cleanup (`audit-retention`) introduced by the `audit-log` change.

---

## Requirements

### Requirement: Entity Mutation Recording

The system MUST record an `AuditLog` entry for every Created, Updated, Deleted, or Restored operation on any whitelisted entity (Budget, Cycle, Period, CategoryGroup, Category, BudgetLine, BudgetLineRevision).

Each entry MUST capture: `Id` (Guid PK), `EntityName` (varchar 100), `EntityId` (Guid), `Action` (varchar 20), `UserId` (Guid?), `Timestamp` (timestamptz, UTC), `BeforeJson` (text?), `AfterJson` (text?), `BudgetId` (Guid? — denormalized).

`BeforeJson` MUST be null for Created actions. `AfterJson` MUST be null for Deleted actions.

Non-whitelisted entities MUST NOT produce `AuditLog` entries.

#### Scenario: Created entity produces AuditLog entry

- GIVEN a whitelisted entity (e.g., Category) is saved for the first time
- WHEN `SaveChangesAsync` completes successfully
- THEN an `AuditLog` record exists with `Action = Created`, `AfterJson` populated, and `BeforeJson = null`
- AND `EntityName`, `EntityId`, `BudgetId`, `UserId`, and `Timestamp` are correctly set

#### Scenario: Updated entity produces AuditLog entry

- GIVEN a whitelisted entity is modified and saved
- WHEN `SaveChangesAsync` completes
- THEN an `AuditLog` record exists with `Action = Updated`, both `BeforeJson` and `AfterJson` populated

#### Scenario: Deleted entity produces AuditLog entry

- GIVEN a whitelisted entity is deleted
- WHEN `SaveChangesAsync` completes
- THEN an `AuditLog` record exists with `Action = Deleted`, `BeforeJson` populated, and `AfterJson = null`

#### Scenario: Non-whitelisted entity is saved

- GIVEN an entity type not in the whitelist (e.g., ApplicationUser) is modified
- WHEN `SaveChangesAsync` completes
- THEN no `AuditLog` entry is created for that entity

#### Scenario: Unauthenticated context

- GIVEN no authenticated user is present (e.g., background job, migration seed)
- WHEN a whitelisted entity is saved
- THEN an `AuditLog` entry is created with `UserId = null`

---

### Requirement: Security Event Recording

The system MUST record a `SecurityAuditLog` entry for each of the following auth events: `FailedLogin`, `SuccessfulLogin`, `AccountRegistered`, `InvitationAccepted`, `TokenRefreshed`, `TokenRevoked`, `AccountLocked`.

`PasswordChanged` is a reserved event name for future use by the `password-management` change (email recovery, account settings, forced-change policy). It MUST NOT be written in this change.

Each entry MUST capture: `Id` (Guid PK), `Event` (varchar 50), `UserId` (Guid?), `Email` (varchar?), `IpAddress` (varchar?), `UserAgent` (varchar?), `Timestamp` (timestamptz, UTC), `Details` (jsonb?).

`SecurityAuditLog` entries MUST be written explicitly within the respective auth handler; they MUST NOT be intercepted via `SaveChangesAsync`.

#### Scenario: Successful login produces SecurityAuditLog entry

- GIVEN a user submits valid credentials
- WHEN the LoginUser handler completes
- THEN a `SecurityAuditLog` record exists with `Event = SuccessfulLogin`, `UserId`, `Email`, `IpAddress`, and `Timestamp` populated

#### Scenario: Failed login produces SecurityAuditLog entry

- GIVEN a user submits invalid credentials
- WHEN the LoginUser handler rejects the attempt
- THEN a `SecurityAuditLog` record exists with `Event = FailedLogin` and `UserId = null` when the user is not found

#### Scenario: Token refresh produces SecurityAuditLog entry

- GIVEN a valid refresh token is submitted
- WHEN the RefreshToken handler completes successfully
- THEN a `SecurityAuditLog` record exists with `Event = TokenRefreshed` and the correct `UserId`

#### Scenario: Logout produces SecurityAuditLog entry

- GIVEN an authenticated user initiates logout
- WHEN the LogoutUser handler completes
- THEN a `SecurityAuditLog` record exists with `Event = TokenRevoked`

#### Scenario: Registration produces SecurityAuditLog entry

- GIVEN a new user completes registration
- WHEN the RegisterUser handler completes
- THEN a `SecurityAuditLog` record exists with `Event = AccountRegistered` and `UserId` populated

#### Scenario: Invitation acceptance produces SecurityAuditLog entry

- GIVEN a user accepts a budget invitation via token link
- WHEN the AcceptInvitation handler completes
- THEN a `SecurityAuditLog` record exists with `Event = InvitationAccepted` and `UserId` populated

---

### Requirement: Audit Log Read Endpoint

The system MUST expose `GET /budgets/{budgetId}/audit-log` returning paginated `AuditLog` entries scoped to the given budget.

Only users with `BudgetRole >= Admin` (Admin=30, Owner=40) MAY access this endpoint.

The endpoint MUST support filters: `EntityName`, `Action`, and date range (`from` / `to`).

Unauthorized callers MUST receive `403 Forbidden`.

#### Scenario: Admin retrieves audit log

- GIVEN a user with `BudgetRole = Admin` for budget X
- WHEN `GET /budgets/X/audit-log` is called
- THEN a paginated list of `AuditLog` entries for budget X is returned with `200 OK`

#### Scenario: Member cannot access audit log

- GIVEN a user with `BudgetRole = Member` (< Admin)
- WHEN `GET /budgets/X/audit-log` is called
- THEN the response is `403 Forbidden`

#### Scenario: Filter by EntityName and date range

- GIVEN an Admin user
- WHEN `GET /budgets/X/audit-log?entityName=Category&from=2025-01-01&to=2025-06-30` is called
- THEN only `AuditLog` entries matching `EntityName = Category` within the date range are returned

---

### Requirement: Security Audit Log Read Endpoint

The system MUST expose `GET /budgets/{budgetId}/security-audit-log` returning paginated `SecurityAuditLog` entries filtered to users who are members of that budget.

Only users with `BudgetRole >= Admin` MAY access this endpoint.

Unauthorized callers MUST receive `403 Forbidden`.

#### Scenario: Owner retrieves security audit log

- GIVEN a user with `BudgetRole = Owner` for budget X
- WHEN `GET /budgets/X/security-audit-log` is called
- THEN a paginated list of security events for users who are members of budget X is returned with `200 OK`

#### Scenario: Non-member cannot access security audit log

- GIVEN a user who is not a member of budget X
- WHEN `GET /budgets/X/security-audit-log` is called
- THEN the response is `403 Forbidden`

#### Scenario: Security events not in budget membership are excluded

- GIVEN a user who is a member of budget X but not budget Y
- WHEN `GET /budgets/X/security-audit-log` is called
- THEN events from users who are not members of budget X are not included in the response

---

### Requirement: Audit Retention Policy

The system MUST delete `AuditLog` and `SecurityAuditLog` records older than the configured TTL.

The TTL MUST be read from `AuditLog:RetentionDays` in application settings, defaulting to 90 days.

An `IAuditRetentionPolicy` abstraction MUST be the sole source of the TTL; the background cleanup service MUST depend only on this interface.

#### Scenario: Records older than TTL are deleted

- GIVEN `AuditLog:RetentionDays = 90` and records with `Timestamp` older than 90 days exist
- WHEN the retention cleanup job runs
- THEN all records with `Timestamp < (now - 90 days)` are deleted from both audit tables

#### Scenario: Records within TTL are preserved

- GIVEN records with `Timestamp` within the last 90 days
- WHEN the retention cleanup job runs
- THEN those records are not deleted

#### Scenario: TTL is configurable

- GIVEN `AuditLog:RetentionDays = 30` in appsettings
- WHEN the retention cleanup job runs
- THEN records older than 30 days are deleted

#### Scenario: Default TTL applies when setting is absent

- GIVEN `AuditLog:RetentionDays` is not present in appsettings
- WHEN the retention cleanup job runs
- THEN records older than 90 days are deleted (default TTL)
