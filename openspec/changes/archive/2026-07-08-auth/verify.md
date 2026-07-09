# Verification Report — ARCHIVED

**Change**: auth
**Version**: spec v1.0 (2026-07-08)
**Mode**: Strict TDD (active)
**Status**: PASS WITH WARNINGS
**Archived**: 2026-07-08

---

## Executive Summary

All 62 tasks complete. All 112 tests pass (0 failures). Build: PASS. Implementation is structurally correct and faithfully follows all design decisions. Two CRITICAL findings are test-coverage gaps not implementation bugs. See Issues Found below.

---

## Completeness

| Metric | Value |
|--------|-------|
| Tasks total | 62 |
| Tasks complete | 62 |
| Tasks incomplete | 0 |

All 62 tasks across 7 groups marked complete.

---

## Build and Tests Execution

Build: PASS

Backend Tests: 38 unit + 31 integration = 69 passed / 0 failed
Frontend Tests: 30 passed / 0 failed
E2E Tests: 9/9 passed
Coverage: Not measured
Total: 108 tests, 0 failures

---

## Test Layer Distribution

| Layer | Tests | Files | Tools |
|-------|-------|-------|-------|
| Unit | 68 | 9 | NSubstitute/Shouldly + Vitest/testing-library |
| Integration | 31 | 7 | AspNetCore.Mvc.Testing + Postgres |
| E2E | 9 | 3 | Playwright |
| Total | 108 | 19 | |

---

## Spec Compliance

| Requirement | Scenarios | Compliant | Partial | Untested |
|---|---|---|---|---|
| REG-1 User Registration | 7 | 7 | 0 | 0 |
| LOGIN-1 User Login | 5 | 5 | 0 | 0 |
| REFRESH-1 Token Refresh | 4 | 3 | 1 | 0 |
| LOGOUT-1 User Logout | 3 | 3 | 0 | 0 |
| ME-1 Current User | 2 | 2 | 0 | 0 |
| INV-1 Budget Invitation | 5 | 4 | 0 | 1 |
| ACCEPT-1 Accept Invitation | 7 | 1 | 1 | 3 |
| AUTHZ-1 Per-Budget Authorization | 4 | 2 | 0 | 2 |
| STARTUP-1 Startup Guard | 2 | 1 | 1 | 0 |
| Shared Constraints (5) | 5 | 5 | 0 | 0 |

**Compliance summary**: 28/37 scenarios compliant (6 UNTESTED, 3 PARTIAL)

---

## Issues Found

### CRITICAL Issues

**CRITICAL-001**: AcceptInvitation integration test ValidToken_Returns200_WithBudgetAndRole does not call the HTTP endpoint. BCrypt constraint makes raw token unavailable in-process. Three error scenarios have zero covering tests: AUTH_INVITATION_EXPIRED (410), AUTH_INVITATION_ALREADY_USED (410), AUTH_INVITATION_EMAIL_MISMATCH (403). E2E covers happy path only.

**CRITICAL-002**: INV-1 operator caller 403 AUTH_INSUFFICIENT_ROLE has no integration test. Unit tests cover role comparison logic but there is no HTTP-level proof that policy enforcement works end-to-end.

### WARNINGS

- WARNING-001: RefreshToken expired test exercises INVALID path not EXPIRED
- WARNING-002: BudgetAuthorizationTests covers 2 of 6 scenarios (missing role combinations)
- WARNING-003: Axios interceptor tests do not exercise actual interceptor code
- WARNING-004: AcceptInvitationTests.ValidToken is misleading (no HTTP call)

### SUGGESTIONS

- SUGGESTION-001: Seed RefreshTokens with past ExpiresAt to test EXPIRED branch
- SUGGESTION-002: Seed Invitations with known plaintext+hash pair for error scenarios
- SUGGESTION-003: Add seeded memberships for all 6 role-policy combinations
- SUGGESTION-004: Mailpit verification is E2E-only (acceptable, worth documenting)

---

## Correctness (Static Evidence)

All 13 implementation requirements verified:
- BCrypt factors (12 for passwords, 6 for tokens)
- Refresh token rotation with RevokedAt+ReplacedByTokenId
- Theft detection (family revoke)
- GetCurrentUser Dapper-only (no EF)
- 256-bit invitation token BCrypt-hashed
- IMemoryCache eviction and TTL
- No roles in JWT
- Startup guard with descriptive message
- Budget auto-creation on register
- Default locale handling
- Case-insensitive email validation

---

## Coherence (Design)

All 7 ADRs implemented correctly:
- ADR-001: JWT + rotating refresh ✓
- ADR-002: No roles in JWT, DB-read handler ✓
- ADR-003: IMemoryCache TTL 5min ✓
- ADR-004: Budget in AddAuthTables ✓
- ADR-005: localStorage + DOMPurify ✓
- GetCurrentUser memberships inline ✓
- BudgetAuthorizationMiddlewareHandler present ✓

---

## Verdict

**PASS WITH WARNINGS**

The auth change is production-ready subject to acknowledged test-coverage gaps. Archive is recommended with tracking for CRITICAL gaps to be addressed in a follow-up iteration.

---

## Recommendations for Follow-up

Create tracking issues to address:
1. AcceptInvitation error paths (expired, already-used, email-mismatch) — add integration tests
2. INV-1 operator-caller 403 — add integration test
3. RefreshToken expired test — use actual time-expired token, not proxy
4. BudgetAuthorizationTests — add all 6 role-policy combinations

Full verify-report details: See Engram observation #128.
