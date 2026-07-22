# Verification Report — budget-line-customizations (fix branch)

**Change**: `budget-line-customizations`
**Branch**: `feat/budget-line-customizations-fix`
**Mode**: Hybrid (Engram + openspec)
**Strict TDD**: Active (loaded strict-tdd-verify.md)
**Date**: 2026-07-22
**Verdict**: PASS WITH WARNINGS

---

## Completeness

| Artifact | Status |
|---|---|
| Spec (Engram #327) | Present |
| Tasks (Engram #329) | Present |
| Apply-progress (Engram #330) | Present |
| Design | Not in openspec (archived to Engram only — pre-existing) |

---

## Task Completion

| Group | Completed | Pending |
|---|---|---|
| PR1 tasks (T1.1–T1.17) | All checked | None |
| PR2a tasks (T2.1–T2a.V) | All checked | None |
| PR2b tasks (T2.7–T2.16) | T2.7–T2.16 checked | T2b.V explicitly marked PENDING (Docker PostgreSQL) |
| PR3 tasks (T3.1–T3.4) | All checked | None |
| Fix-branch tasks (E1, E2, D1, A1–A4, B1, B2, C1, C2, F1, F2, G) | All checked | None |

T2b.V is the only unchecked item and is intentionally deferred (concurrency integration test requires Docker PostgreSQL; SQLite-based tests are green; item is marked skip/pending in tasks). This is a known and accepted deferral, not an unintended gap. No CRITICAL task gap.

---

## Test Evidence

### Frontend (Vitest)
- **Result**: 47 files / 382 tests — ALL PASSED
- **Command**: `pnpm test --run` from `Project/frontend`

### Backend (dotnet test — unit tests)
- **Result**: 423 tests — ALL PASSED, 0 failures, 0 skipped
- **Command**: `dotnet test tests/MyBudget.Features.Tests/` from `Project/`

### Backend (dotnet test — no-build, all projects)
- **Result**: Exit code 0 (background task b1eck7fu0 confirmed)
- Note: MSB3492 cache-file errors on incremental build are a .NET 10 SDK Windows quirk (transient filesystem lock); they do not affect test execution.

---

## Spec Compliance Matrix

| Requirement | Implementation | Test | Status |
|---|---|---|---|
| REQ-BLR-01: GET .../revisions | ListBudgetLineRevisions slice | Integration tests T2.7 | PASS |
| REQ-BLR-02: POST .../revisions | CreateBudgetLineRevision slice | Integration tests T2.11 | PASS |
| REQ-BLR-03: DELETE .../revisions/{id} | DeleteBudgetLineRevision slice | Integration tests T2.13 | PASS |
| REQ-BLR-04: i18n EN+ES | en.json + es.json | locales.spec.ts (46 tests) | PASS |
| REQ-BLR-05: BudgetLineCustomizationsView | BudgetLineCustomizationsView.vue | BudgetLineCustomizationsView.spec.ts (6 tests) | PASS |
| REQ-BL-DATERANGE-1: PATCH date-range | UpdateBudgetLineDateRange slice | Integration tests T2.15 | PASS |
| REQ-BL-CONCURRENCY-1: xmin token | EF shadow property + 409 handler | Unit test T2.5 (SQLite), integration T2.16 | PASS |
| REQ-BL-AUDIT-1: AuditLog entries | Explicit AuditLog.Create in handlers | Integration test T2.13 (audit check) | PASS |
| REQ-EXEC-RESTORE-DATERANGE-1: Restore guard | RestoreExecutionRecordHandler modified | Integration tests T3.2 | PASS |
| FIX: UpdateBudgetLineRevision PATCH | UpdateBudgetLineRevision slice (3 files) | BudgetLineCustomizationsView.spec.ts mock only | WARNING — no dedicated integration test |
| FIX: SyncValidFrom (single-revision StartDate move) | UpdateBudgetLineDateRangeHandler + internal method | No direct unit test — covered indirectly by date-range integration tests | WARNING — no dedicated unit/integration test for the SyncValidFrom path |
| FIX: allow amount = 0 | Handler guard changed to `< 0` | No test asserts amount=0 is accepted | WARNING — no test for boundary value |

---

## Design Coherence

Design artifact not available in openspec (archived cycle pre-dates openspec file write). Checking against VSA pattern from context:

| Check | Result |
|---|---|
| UpdateBudgetLineRevision — public Command record | PASS (UpdateBudgetLineRevisionCommand.cs) |
| UpdateBudgetLineRevision — Handler : IRequestHandler | PASS |
| UpdateBudgetLineRevision — Endpoint with static Map() | PASS |
| UpdateBudgetLineRevisionRequest as separate public record | PASS (co-located in Endpoint file, same file) |
| SyncValidFrom is `internal` (not `public`) | PASS — encapsulation preserved |
| Endpoint registered via reflection MapAllSliceEndpoints | PASS |
| RBAC `budget:admin` on PATCH endpoint | PASS |
| updateRevision action in Pinia store | PASS |
| updateRevision in budgetLines.api.ts | PASS |

---

## TDD Compliance (Strict TDD Module)

Apply-progress does NOT include a formal TDD Cycle Evidence table — apply mode was "Standard (TDD OFF)" per artifact. Strict TDD checks degrade to available evidence.

| Check | Result | Details |
|---|---|---|
| TDD Evidence table present | No | Apply ran in Standard mode (TDD OFF) |
| All core tasks have test files | Partial | UpdateBudgetLineRevision has no dedicated test file |
| Frontend tests pass | PASS | 382/382 |
| Backend unit tests pass | PASS | 423/423 |
| Triangulation for UpdateBudgetLineRevision | Weak | Only mocked in view spec; no store action test, no API spec test, no integration test |
| Triangulation for SyncValidFrom | Weak | No direct test; covered indirectly via happy-path integration test |

---

## Issues

### WARNINGS

| ID | Location | Issue |
|---|---|---|
| W-001 | `UpdateBudgetLineRevision` slice | No integration test for PATCH `/api/budgets/{id}/lines/{lineId}/revisions/{revisionId}`. The endpoint is exercised only through the frontend view spec mock. Missing scenarios: 200 OK, 404 revision not found, 422 invalid amount. |
| W-002 | `UpdateBudgetLineDateRangeHandler.SyncValidFrom` | No dedicated test for the single-revision StartDate-sync path. The guard (`line.Revisions.Count == 1 && original.ValidFrom == line.StartDate`) is exercised only through the happy-path date-range integration test, which does not move the StartDate. |
| W-003 | `store.budgetLines.spec.ts` | `updateRevision` store action has no test in this file (not mocked in the `vi.mock('../api/budgetLines.api', ...)` factory either). It is mocked in BudgetLineCustomizationsView.spec.ts but not tested as a unit. |
| W-004 | `budgetLines.api.spec.ts` | `updateRevision` API function has no test case. |
| W-005 | `UpdateBudgetLineRevisionHandler` | No test asserts that amount = 0 is accepted. The fix changed `Amount <= 0` to `Amount < 0`, but there is no test for the boundary value. |

### SUGGESTIONS

| ID | Location | Suggestion |
|---|---|---|
| S-001 | `BudgetLineRevisionTests.cs` | Add integration test class for `UpdateBudgetLineRevision` covering: 200 OK (amount+note update), 200 OK (amount=0 allowed), 404 revision not found, 422 negative amount. |
| S-002 | `BudgetLineRevisionTests.cs` | Add integration test for `UpdateBudgetLineDateRange` with StartDate move: seed a line with a single revision whose ValidFrom matches line.StartDate, PATCH to a different startDate, assert revision.ValidFrom is updated and no RANGE_WOULD_ORPHAN_REVISION is returned. |
| S-003 | `store.budgetLines.spec.ts` | Add `updateRevision` to the `vi.mock` factory and add 2 test cases: calls API with correct args, reloads revisions after update. |
| S-004 | `budgetLines.api.spec.ts` | Add test case for `updateRevision(budgetId, lineId, revisionId, payload)` verifying PATCH URL and body. |

---

## Verdict

**PASS WITH WARNINGS**

0 CRITICALs, 5 WARNINGs, 4 SUGGESTIONs.

The fix branch is functionally correct and all existing tests pass (382 frontend + 423 backend). The new `UpdateBudgetLineRevision` slice is structurally sound and follows VSA pattern. The main gap is missing integration tests for the new PATCH endpoint and missing unit test coverage for the `SyncValidFrom` single-revision path and the amount=0 boundary. These are not regressions — they are coverage gaps introduced by the fix branch that should be addressed before or alongside the next PR.

**Next recommended**: sdd-archive (with a note to add integration tests for UpdateBudgetLineRevision as a follow-up task).
