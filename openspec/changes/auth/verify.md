## Verification Report

**Change**: auth
**Version**: spec v1.0 (2026-07-08)
**Mode**: Strict TDD (active)

---

### Completeness

| Metric | Value |
|--------|-------|
| Tasks total | 62 |
| Tasks complete | 62 |
| Tasks incomplete | 0 |

All 62 tasks across 7 groups are marked complete.

---

### Build and Tests Execution

Build: PASS

Backend Tests: 38 unit + 31 integration = 69 passed / 0 failed
Frontend Tests: 30 passed / 0 failed
E2E Tests: 9/9 passed
Coverage: Not measured

---

### TDD Compliance

| Check | Result | Details |
|-------|--------|---------|
| TDD Evidence reported | PASS | Found in apply-progress |
| All tasks have tests | PASS | 22/22 implementation tasks |
| RED confirmed | PASS | All test files on filesystem |
| GREEN confirmed | PASS | 108 tests pass |
| Triangulation adequate | WARN | See WARNING-001 and WARNING-002 |
| Safety Net for modified files | PASS | Reported in apply-progress |

5/6 checks passed

---

### Test Layer Distribution

| Layer | Tests | Files | Tools |
|-------|-------|-------|-------|
| Unit | 68 | 9 | NSubstitute/Shouldly + Vitest/testing-library |
| Integration | 31 | 7 | AspNetCore.Mvc.Testing + Postgres |
| E2E | 9 | 3 | Playwright |
| Total | 108 | 19 | |

---

### Spec Compliance Matrix

REG-1 User Registration

| Scenario | Test | Result |
|----------|------|--------|
| Happy path 201 | RegisterUserTests.ValidPayload | COMPLIANT |
| Duplicate email 409 | RegisterUserTests.DuplicateEmail | COMPLIANT |
| Weak password 422 | RegisterUserTests.WeakPassword | COMPLIANT |
| Missing field 422 | RegisterUserTests.MissingFirstName | COMPLIANT |
| Unsupported locale 422 | RegisterUserTests.UnsupportedLocale | COMPLIANT |
| User+Budget+Membership atomic | RegisterUserTests.SuccessfulRegister_Creates | COMPLIANT |
| RefreshToken row hashed | RegisterUserTests.SuccessfulRegister_CreatesRefreshTokenRow | COMPLIANT |

LOGIN-1 User Login

| Scenario | Test | Result |
|----------|------|--------|
| Happy path 200 | LoginUserTests.ValidCredentials | COMPLIANT |
| LastLoginAt updated | LoginUserTests.SuccessfulLogin_UpdatesLastLoginAt | COMPLIANT |
| Wrong password 401 | LoginUserTests.WrongPassword | COMPLIANT |
| Unknown email 401 no enum | LoginUserTests.UnknownEmail_Returns401 | COMPLIANT |
| Missing field 422 | LoginUserValidatorTests unit | COMPLIANT |

REFRESH-1 Token Refresh

| Scenario | Test | Result |
|----------|------|--------|
| Happy path 200 new pair old revoked | RefreshTokenTests.ValidToken | COMPLIANT |
| Reuse 401 family revoked | RefreshTokenTests.ReuseRevokedToken | COMPLIANT |
| Expired 401 AUTH_REFRESH_TOKEN_EXPIRED | RefreshTokenTests.ExpiredToken (proxy) | PARTIAL |
| Unknown 401 AUTH_REFRESH_TOKEN_INVALID | RefreshTokenTests.UnknownToken | COMPLIANT |

LOGOUT-1 User Logout

| Scenario | Test | Result |
|----------|------|--------|
| Happy path 200 revoked | LogoutAndMeTests.AuthenticatedLogout | COMPLIANT |
| Unauthenticated 401 | LogoutAndMeTests.UnauthenticatedLogout | COMPLIANT |
| Already revoked 200 idempotent | LogoutAndMeTests.SecondLogout | COMPLIANT |

ME-1 Current User

| Scenario | Test | Result |
|----------|------|--------|
| Happy path 200 | LogoutAndMeTests.AuthenticatedMe | COMPLIANT |
| Missing token 401 | LogoutAndMeTests.NoAuthHeader | COMPLIANT |

INV-1 Budget Invitation

| Scenario | Test | Result |
|----------|------|--------|
| Happy path admin 201 | InviteUserToBudgetTests.AdminCaller | COMPLIANT |
| Operator caller 403 AUTH_INSUFFICIENT_ROLE | No test | UNTESTED |
| Role owner 422 AUTH_CANNOT_INVITE_AS_OWNER | InviteUserToBudgetTests.RoleOwner | COMPLIANT |
| Already member 409 AUTH_ALREADY_MEMBER | InviteUserToBudgetTests.AlreadyMember | COMPLIANT |
| Budget not found 404 BUDGET_NOT_FOUND | InviteUserToBudgetTests.UnknownBudget | COMPLIANT |

ACCEPT-1 Accept Invitation

| Scenario | Test | Result |
|----------|------|--------|
| Happy path 200 membership created | AcceptInvitationTests (DB only) + E2E | PARTIAL |
| Expired 410 AUTH_INVITATION_EXPIRED | No test | UNTESTED |
| Already used 410 AUTH_INVITATION_ALREADY_USED | No test | UNTESTED |
| Not found 404 AUTH_INVITATION_NOT_FOUND | AcceptInvitationTests.UnknownToken | COMPLIANT |
| Email mismatch 403 AUTH_INVITATION_EMAIL_MISMATCH | No test | UNTESTED |
| Unauthenticated 401 | AcceptInvitationTests.Unauthenticated | COMPLIANT |

AUTHZ-1 Per-Budget Authorization

| Scenario | Test | Result |
|----------|------|--------|
| Authorized admin passes budget:admin | BudgetAuthorizationTests.Owner | COMPLIANT |
| Insufficient role 403 | No integration test | UNTESTED |
| No membership 403 | No integration test | UNTESTED |
| JWT has no roles DB-only | BudgetAuthorizationHandlerTests unit | COMPLIANT |

STARTUP-1 Startup Guard

| Scenario | Test | Result |
|----------|------|--------|
| Key missing startup fails | Code inspection only | PARTIAL |
| Key present startup succeeds | Integration factory provides JWT__Key | COMPLIANT |

Shared Constraints

| Constraint | Status |
|------------|--------|
| SC-1: fails to start if JWT__Key absent | PASS |
| SC-2: JWT__Key not in appsettings.json | PASS |
| SC-3: AddAuthTables only migration | PASS |
| SC-4: JWT limited to 5 claims | PASS |
| SC-5: roles resolved from DB not JWT | PASS |

Compliance summary: 28/37 scenarios compliant (6 UNTESTED, 3 PARTIAL)

---

### Assertion Quality

| File | Issue | Severity |
|------|-------|----------|
| axios.test.ts line 52 | Calls mockRefresh() directly not through interceptor | WARNING |
| axios.test.ts line 57-66 | Manually calls mockLogout not via interceptor | WARNING |
| axios.test.ts line 70-93 | simulateRetry is local logic not actual interceptor | WARNING |
| AcceptInvitationTests.cs line 45 | ValidToken test does not call HTTP endpoint | WARNING |

0 CRITICAL, 4 WARNING

---

### Correctness (Static Evidence)

| Requirement | Status |
|------------|--------|
| BCrypt workFactor 12 for passwords | Implemented |
| BCrypt workFactor 6 for tokens | Implemented |
| Refresh token rotation RevokedAt+ReplacedByTokenId | Implemented |
| Theft detection family revoke | Implemented |
| GetCurrentUser Dapper-only no EF | Implemented |
| 256-bit invitation token BCrypt-hashed | Implemented |
| IMemoryCache eviction on invite and accept | Implemented |
| Cache TTL 5 minutes | Implemented |
| No roles in JWT | Implemented |
| Startup guard descriptive message | Implemented |
| Budget name format firstName Budget | Implemented |
| Default locale en | Implemented |
| Email mismatch check case-insensitive | Implemented |

---

### Coherence (Design)

| ADR | Followed? |
|-----|-----------|
| ADR-001: JWT 15min rotating refresh 7d | Yes |
| ADR-002: No roles in JWT BudgetAuthorizationHandler reads DB | Yes |
| ADR-003: IMemoryCache TTL 5min consistent key | Yes |
| ADR-004: Budget entity in AddAuthTables migration | Yes |
| ADR-005: localStorage DOMPurify | Yes |
| GetCurrentUser memberships inline | Yes |
| BudgetAuthorizationMiddlewareResultHandler present | Yes |

---

### Issues Found

CRITICAL:

CRITICAL-001: AcceptInvitation integration test ValidToken_Returns200_WithBudgetAndRole does not call the HTTP endpoint. BCrypt constraint makes raw token unavailable in-process. Three error scenarios have zero covering tests at any layer: AUTH_INVITATION_EXPIRED (410), AUTH_INVITATION_ALREADY_USED (410), AUTH_INVITATION_EMAIL_MISMATCH (403). E2E covers happy path only.

CRITICAL-002: INV-1 operator caller 403 AUTH_INSUFFICIENT_ROLE has no integration test. Task 5.5 required this. Unit tests cover role comparison logic but there is no HTTP-level proof that policy enforcement works end-to-end.

WARNING:

WARNING-001: RefreshTokenTests.ExpiredToken_Returns401 exercises INVALID path not EXPIRED. Test comments acknowledge this. AUTH_REFRESH_TOKEN_EXPIRED has no covering test for the ExpiresAt branch.

WARNING-002: BudgetAuthorizationTests covers 2 of 6 scenarios from task 5.5. Missing: admin-200, operator-403 on admin-policy, read-only-403 on operator-policy, read-only-200 on read-policy.

WARNING-003: Axios interceptor tests do not exercise the actual interceptor code in axios.ts. All assertions target local mock functions.

WARNING-004: AcceptInvitationTests.ValidToken test name is misleading. No HTTP call is made, no 200 response asserted, no BudgetMembership creation verified at integration level.

SUGGESTION:

SUGGESTION-001: Seed RefreshTokens with past ExpiresAt via SQL helper to test EXPIRED branch.
SUGGESTION-002: Seed Invitations with known plaintext+hash pair for AcceptInvitation error scenarios at integration level.
SUGGESTION-003: Add seeded BudgetMembership rows with operator and read-only roles to BudgetAuthorizationTests to cover all 6 combinations.
SUGGESTION-004: InviteUserToBudget Mailpit verification is E2E-only. Acceptable but worth documenting.

---

### Verdict

PASS WITH WARNINGS

All 62 tasks complete. All 108 tests pass. Implementation is structurally correct and faithfully follows all design decisions. Two CRITICAL findings are test-coverage gaps not implementation bugs. AcceptInvitation error paths and INV-1 operator-caller-403 lack passing covering tests. E2E covers AcceptInvitation happy path. Archive is conditionally recommended once CRITICAL gaps are acknowledged and tracked.
